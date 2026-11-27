using System;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace PawFit.Online
{
    /// <summary>Kết quả 1 lời gọi API. Status 0 = không tới được máy chủ.</summary>
    public sealed class ApiResult<T>
    {
        public int Status;
        public T Value;

        /// <summary>Lỗi đọc được cho người (tiếng Việt nếu máy chủ gửi) + mã lỗi máy chủ (ApiError.Code).</summary>
        public string Error;
        public string ErrorCode;

        /// <summary>409 khi lưu game: bản mới nhất trên máy chủ.</summary>
        public long ConflictRevision;

        /// <summary>Thời gian đi + về (giây) — để tính giờ máy chủ cho đúng.</summary>
        public double RoundTripSeconds;

        public bool Ok => Status >= 200 && Status < 300;
        public bool Unreachable => Status == 0;
    }

    /// <summary>
    /// Gọi REST của máy chủ riêng (C# thuần). Mọi callback chạy trong <see cref="Update"/> trên luồng gọi Update
    /// (luồng chính của game), dù mạng chạy ở luồng nào.
    /// </summary>
    public sealed class ServerApi
    {
        private readonly IHttpTransport _http;
        private readonly ConcurrentQueue<Action> _done = new ConcurrentQueue<Action>();

        public ServerApi(string baseUrl, IHttpTransport http)
        {
            if (string.IsNullOrWhiteSpace(baseUrl)) throw new ArgumentException("Thiếu địa chỉ máy chủ", nameof(baseUrl));
            BaseUrl = baseUrl.Trim().TrimEnd('/');
            _http = http ?? throw new ArgumentNullException(nameof(http));
        }

        /// <summary>vd "https://pawfit.example.com" hoặc "http://192.168.1.10:5080" (máy Mac trong nhà, bản Dev).</summary>
        public string BaseUrl { get; }

        /// <summary>Mã đăng nhập hiện tại (gửi kèm mọi lời gọi cần tài khoản).</summary>
        public string Token { get; set; }

        /// <summary>Số lời gọi đang chờ trả lời.</summary>
        public int Pending { get; private set; }

        /// <summary>Địa chỉ WebSocket PvP: http → ws, https → wss.</summary>
        public string PvpUrl => ToWs(BaseUrl) + "/v1/pvp";

        public static string ToWs(string url)
        {
            if (url.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return "wss://" + url.Substring(8);
            if (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return "ws://" + url.Substring(7);
            return url;
        }

        public void Time(Action<ApiResult<TimeReply>> done) => Call("GET", "/v1/time", null, done, false);

        public void Guest(GuestRequest body, Action<ApiResult<AuthReply>> done) => Call("POST", "/v1/auth/guest", body, done, false);

        public void Me(Action<ApiResult<MeReply>> done) => Call("GET", "/v1/me", null, done);

        public void Rename(string name, Action<ApiResult<AuthReply>> done) => Call("PUT", "/v1/me/name", new RenameRequest { Name = name }, done);

        public void GetSave(Action<ApiResult<SaveReply>> done) => Call("GET", "/v1/save", null, done);

        public void PutSave(SavePut body, Action<ApiResult<SaveReply>> done) => Call("PUT", "/v1/save", body, done);

        public void TransferCode(Action<ApiResult<TransferCodeReply>> done) => Call("POST", "/v1/transfer-code", null, done);

        public void Redeem(string code, Action<ApiResult<AuthReply>> done) => Call("POST", "/v1/auth/redeem", new RedeemRequest { Code = code }, done, false);

        /// <summary>Gọi 1 API bất kỳ. `auth` = gửi kèm Token.</summary>
        public void Call<T>(string method, string path, object body, Action<ApiResult<T>> done, bool auth = true)
        {
            var call = new HttpCall
            {
                Method = method,
                Url = BaseUrl + path,
                Body = body == null ? null : NetProto.Write(body),
                Token = auth ? Token : null,
            };
            Pending++;
            Stopwatch sw = Stopwatch.StartNew();
            _http.Send(call, reply =>
            {
                double rtt = sw.Elapsed.TotalSeconds;
                _done.Enqueue(() =>
                {
                    Pending--;
                    ApiResult<T> r = Parse<T>(reply);
                    r.RoundTripSeconds = rtt;
                    done?.Invoke(r);
                });
            });
        }

        /// <summary>Chạy các callback đã có trả lời (gọi mỗi frame trên luồng chính).</summary>
        public void Update()
        {
            while (_done.TryDequeue(out Action a)) a();
        }

        private static ApiResult<T> Parse<T>(HttpReply reply)
        {
            var r = new ApiResult<T> { Status = reply?.Status ?? 0, Error = reply?.Error };
            if (reply == null) return r;
            try
            {
                string body = reply.Body;
                bool json = !string.IsNullOrEmpty(body) && body.TrimStart().StartsWith("{", StringComparison.Ordinal);
                if (r.Ok)
                {
                    if (json) r.Value = NetProto.Read<T>(body);
                }
                else if (json)
                {
                    ApiError e = NetProto.Read<ApiError>(body);
                    if (e != null)
                    {
                        r.ErrorCode = e.Code;
                        r.Error = e.Message ?? r.Error;
                        r.ConflictRevision = e.Revision;
                    }
                }
                if (!r.Ok && string.IsNullOrEmpty(r.Error)) r.Error = r.Status == 0 ? "Không kết nối được máy chủ" : "Máy chủ báo lỗi " + r.Status;
            }
            catch (Exception e)
            {
                r.Status = 0;
                r.Error = "Trả lời của máy chủ bị hỏng: " + e.Message;
            }
            return r;
        }
    }
}
