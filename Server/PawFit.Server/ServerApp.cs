using System;
using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PawFit.Core;
using PawFit.Online;

namespace PawFit.Server
{
    /// <summary>Cấu hình máy chủ (appsettings.json mục "PawFit", hoặc biến môi trường PawFit__Tên).</summary>
    public sealed class ServerOptions
    {
        /// <summary>File SQLite (thư mục tự tạo). Docker: đặt trong volume để không mất khi cập nhật.</summary>
        public string DbPath = "data/pawfit.db";

        /// <summary>Chạy sau Caddy / nginx: tin header X-Forwarded-For để biết IP thật (giới hạn tạo tài khoản theo IP).</summary>
        public bool BehindProxy;

        /// <summary>Mỗi IP tạo tối đa bấy nhiêu tài khoản khách / nhập mã chuyển máy mỗi giờ.</summary>
        public int GuestPerHourPerIp = 30;
        public int RedeemPerHourPerIp = 30;

        /// <summary>Tìm trận: chờ người thật bấy nhiêu giây rồi mới thả người chơi ảo vào hàng của người đó.</summary>
        public double BotDelaySeconds = 6.0;

        /// <summary>Ghép xong mà 1 bên chưa vào màn đấu sau bấy nhiêu giây thì vẫn bắt đầu.</summary>
        public double ReadyTimeoutSeconds = 15.0;

        /// <summary>Cứ bấy nhiêu bước gửi nhịp trận 1 lần (1 = 30 tin / giây, mượt nhất).</summary>
        public int FrameEveryTicks = 1;

        /// <summary>Thời lượng trận PvP (giây). Test đặt ngắn cho nhanh.</summary>
        public float BattleSeconds = 90f;

        /// <summary>File game tải lên tối đa bấy nhiêu byte.</summary>
        public int MaxSaveBytes = 256 * 1024;

        /// <summary>Mã chuyển máy hết hạn sau bấy nhiêu ngày.</summary>
        public int TransferCodeDays = 7;

        /// <summary>Người chơi ảo (cùng số liệu với mạng giả lập trên máy).</summary>
        public LocalNetworkConfig Ghosts = new LocalNetworkConfig();
    }

    /// <summary>Dựng ứng dụng máy chủ (Program.cs và test cùng gọi).</summary>
    public static class ServerApp
    {
        public const int DefaultPort = 5080;

        public static WebApplication Build(string[] args, Action<ServerOptions> configure = null)
        {
            WebApplicationBuilder builder = WebApplication.CreateBuilder(args ?? Array.Empty<string>());
            ServerOptions opt = ReadOptions(builder.Configuration);
            configure?.Invoke(opt);

            builder.Services.AddSingleton(opt);
            builder.Services.AddSingleton(GameConfig.CreateDefault());
            builder.Services.AddSingleton<ITimeProvider>(new SystemTimeProvider());
            builder.Services.AddSingleton(_ => new GameStore(opt.DbPath, GameConfig.CreateDefault().Rank));
            builder.Services.AddSingleton<MatchService>();
            builder.Services.AddHostedService(sp => sp.GetRequiredService<MatchService>());
            builder.Services.AddSingleton<PvpHub>();
            builder.Services.AddRateLimiter(o =>
            {
                o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                o.AddPolicy("guest", ctx => PerIp(ctx, opt.GuestPerHourPerIp));
                o.AddPolicy("redeem", ctx => PerIp(ctx, opt.RedeemPerHourPerIp));
            });
            builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = Math.Max(64 * 1024, opt.MaxSaveBytes * 2 + 4096));
            // cổng mặc định 5080 (mọi địa chỉ mạng); đổi bằng --urls hoặc biến ASPNETCORE_URLS
            if (string.IsNullOrWhiteSpace(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://0.0.0.0:" + DefaultPort);

            WebApplication app = builder.Build();
            if (opt.BehindProxy)
            {
                var fh = new ForwardedHeadersOptions { ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto };
                fh.KnownNetworks.Clear();   // cổng máy chủ chỉ mở cho Caddy (docker-compose) nên tin được header của nó
                fh.KnownProxies.Clear();
                app.UseForwardedHeaders(fh);
            }
            app.UseRateLimiter();
            app.UseWebSockets(new WebSocketOptions { KeepAliveInterval = TimeSpan.FromSeconds(20) });
            Api.Map(app);
            return app;
        }

        private static ServerOptions ReadOptions(IConfiguration c)
        {
            var o = new ServerOptions();
            IConfigurationSection s = c.GetSection("PawFit");
            if (!string.IsNullOrWhiteSpace(s["DbPath"])) o.DbPath = s["DbPath"];
            if (bool.TryParse(s["BehindProxy"], out bool proxy)) o.BehindProxy = proxy;
            if (int.TryParse(s["GuestPerHourPerIp"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int g)) o.GuestPerHourPerIp = g;
            if (int.TryParse(s["RedeemPerHourPerIp"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int r)) o.RedeemPerHourPerIp = r;
            if (double.TryParse(s["BotDelaySeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out double d)) o.BotDelaySeconds = d;
            if (double.TryParse(s["ReadyTimeoutSeconds"], NumberStyles.Float, CultureInfo.InvariantCulture, out double rt)) o.ReadyTimeoutSeconds = rt;
            if (int.TryParse(s["FrameEveryTicks"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int f)) o.FrameEveryTicks = Math.Max(1, f);
            if (int.TryParse(s["TransferCodeDays"], NumberStyles.Integer, CultureInfo.InvariantCulture, out int days)) o.TransferCodeDays = Math.Max(1, days);
            return o;
        }

        private static RateLimitPartition<string> PerIp(HttpContext ctx, int perHour) =>
            RateLimitPartition.GetFixedWindowLimiter(ctx.Connection.RemoteIpAddress?.ToString() ?? "?", _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, perHour),
                Window = TimeSpan.FromHours(1),
                QueueLimit = 0,
            });
    }
}
