using System;
using System.IO;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PawFit.Core;
using PawFit.Online;

namespace PawFit.Server
{
    /// <summary>
    /// REST của máy chủ (JSON, cùng DTO với game ở Assets/PawFit/Scripts/Online/Protocol.cs):
    ///   GET  /                    kiểm tra máy chủ sống
    ///   GET  /v1/time             giờ máy chủ
    ///   POST /v1/auth/guest       tạo tài khoản khách (giới hạn theo IP)
    ///   POST /v1/auth/redeem      nhập mã chuyển máy (giới hạn theo IP)
    ///   GET  /v1/me               hồ sơ + giờ + rank + bản lưu mới nhất          (cần đăng nhập)
    ///   PUT  /v1/me/name          đổi biệt danh                                    (cần đăng nhập)
    ///   GET  /v1/save, PUT /v1/save   tải về / tải lên file game                   (cần đăng nhập)
    ///   POST /v1/transfer-code    xin mã chuyển máy                                (cần đăng nhập)
    ///   GET  /v1/pvp              WebSocket tìm trận + PvP (PvpHub)
    /// Đăng nhập = header "Authorization: Bearer &lt;mã đăng nhập&gt;".
    /// </summary>
    public static class Api
    {
        public static void Map(WebApplication app)
        {
            GameStore store = app.Services.GetRequiredService<GameStore>();
            ServerOptions opt = app.Services.GetRequiredService<ServerOptions>();
            ITimeProvider time = app.Services.GetRequiredService<ITimeProvider>();
            PvpHub hub = app.Services.GetRequiredService<PvpHub>();
            ILogger log = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PawFit.Api");

            app.MapGet("/", () => Results.Text("PawFit server OK · proto " + NetProto.Version + "\n", "text/plain", Encoding.UTF8));

            app.MapGet("/v1/time", () => Json(new TimeReply { UtcMs = NetProto.ToMs(time.UtcNow) }));

            app.MapPost("/v1/auth/guest", async (HttpContext ctx) =>
            {
                GuestRequest req = await ReadAsync<GuestRequest>(ctx) ?? new GuestRequest();
                if (req.Proto != 0 && req.Proto != NetProto.Version) return Err(426, "proto", "Bản game cũ, hãy cập nhật");
                var rng = new SeededRandom(BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8), 0));
                PlayerRow p = store.CreateGuest(BotPlayers.RandomNick(rng), time.UtcNow, out string token);
                log.LogInformation("Tài khoản khách mới {Id} ({Name})", p.Id, p.Name);
                return Json(new AuthReply { PlayerId = p.Id, Token = token, Name = p.Name });
            }).RequireRateLimiting("guest");

            app.MapPost("/v1/auth/redeem", async (HttpContext ctx) =>
            {
                RedeemRequest req = await ReadAsync<RedeemRequest>(ctx);
                string code = NetProto.NormalizeCode(req?.Code);
                if (code == null) return Err(400, "bad", "Mã chuyển máy không đúng dạng");
                PlayerRow p = store.Redeem(code, time.UtcNow, out string token);
                if (p == null) return Err(404, "not_found", "Mã không đúng hoặc đã hết hạn");
                hub.Kick(p.Id);   // máy cũ đang nối PvP thì cắt luôn
                log.LogInformation("Chuyển máy: tài khoản {Id} sang máy mới", p.Id);
                return Json(new AuthReply { PlayerId = p.Id, Token = token, Name = p.Name });
            }).RequireRateLimiting("redeem");

            app.MapGet("/v1/me", (HttpContext ctx) =>
            {
                PlayerRow p = Auth(ctx, store, time);
                if (p == null) return Unauthorized();
                DateTime now = time.UtcNow;
                return Json(new MeReply
                {
                    PlayerId = p.Id,
                    Name = p.Name,
                    UtcMs = NetProto.ToMs(now),
                    Rank = store.GetRank(p.Id, now),
                    SaveRevision = store.GetSave(p.Id).Revision,
                });
            });

            app.MapPut("/v1/me/name", async (HttpContext ctx) =>
            {
                PlayerRow p = Auth(ctx, store, time);
                if (p == null) return Unauthorized();
                RenameRequest req = await ReadAsync<RenameRequest>(ctx);
                string name = NetProto.CleanName(req?.Name);
                if (name == null) return Err(400, "bad", "Biệt danh cần " + NetProto.NameMin + "–" + NetProto.NameMax + " ký tự");
                PlayerRow renamed = store.Rename(p.Id, name);
                return Json(new AuthReply { PlayerId = p.Id, Name = renamed?.Name ?? name });
            });

            app.MapGet("/v1/save", (HttpContext ctx) =>
            {
                PlayerRow p = Auth(ctx, store, time);
                return p == null ? Unauthorized() : Json(store.GetSave(p.Id));
            });

            app.MapPut("/v1/save", async (HttpContext ctx) =>
            {
                PlayerRow p = Auth(ctx, store, time);
                if (p == null) return Unauthorized();
                SavePut req = await ReadAsync<SavePut>(ctx);
                if (req == null || string.IsNullOrEmpty(req.Data)) return Err(400, "bad", "Thiếu dữ liệu game");
                if (Encoding.UTF8.GetByteCount(req.Data) > opt.MaxSaveBytes) return Err(413, "too_large", "File game quá lớn");
                if (!LooksLikeGameSave(req.Data)) return Err(400, "bad", "Dữ liệu game hỏng");
                if (!store.PutSave(p.Id, req.BaseRevision, req.Data, time.UtcNow, out long revision))
                    return Err(409, "conflict", "Máy chủ có bản lưu mới hơn", revision);
                return Json(new SaveReply { Revision = revision, UpdatedUtcMs = NetProto.ToMs(time.UtcNow) });
            });

            app.MapPost("/v1/transfer-code", (HttpContext ctx) =>
            {
                PlayerRow p = Auth(ctx, store, time);
                if (p == null) return Unauthorized();
                DateTime expires = time.UtcNow.AddDays(Math.Max(1, opt.TransferCodeDays));
                string code = store.CreateTransferCode(p.Id, expires);
                return Json(new TransferCodeReply { Code = NetProto.FormatCode(code), ExpiresUtcMs = NetProto.ToMs(expires) });
            });

            app.Map("/v1/pvp", async (HttpContext ctx) =>
            {
                if (!ctx.WebSockets.IsWebSocketRequest)
                {
                    ctx.Response.StatusCode = 400;
                    await ctx.Response.WriteAsync("Cần WebSocket");
                    return;
                }
                using (WebSocket ws = await ctx.WebSockets.AcceptWebSocketAsync())
                    await hub.RunAsync(ws, ctx.RequestAborted);
            });
        }

        /// <summary>File game phải là object JSON có số "Version" (máy chủ không cần hiểu hết nội dung).</summary>
        private static bool LooksLikeGameSave(string data)
        {
            try
            {
                JObject o = JObject.Parse(data);
                return o["Version"]?.Type == JTokenType.Integer;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static PlayerRow Auth(HttpContext ctx, GameStore store, ITimeProvider time)
        {
            string h = ctx.Request.Headers.Authorization.ToString();
            const string prefix = "Bearer ";
            if (!h.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return null;
            return store.FindByToken(h.Substring(prefix.Length).Trim(), time.UtcNow);
        }

        private static async Task<T> ReadAsync<T>(HttpContext ctx) where T : class
        {
            try
            {
                using (var reader = new StreamReader(ctx.Request.Body, Encoding.UTF8))
                {
                    string text = await reader.ReadToEndAsync();
                    return string.IsNullOrWhiteSpace(text) ? null : NetProto.Read<T>(text);
                }
            }
            catch (Exception)
            {
                return null;   // thân hỏng / quá lớn: coi như không có
            }
        }

        private static IResult Json(object value, int status = 200) =>
            Results.Text(NetProto.Write(value), "application/json", Encoding.UTF8, status);

        private static IResult Err(int status, string code, string message, long revision = 0) =>
            Json(new ApiError { Code = code, Message = message, Revision = revision }, status);

        private static IResult Unauthorized() => Err(401, "auth", "Chưa đăng nhập hoặc mã đăng nhập đã hết hiệu lực");
    }
}
