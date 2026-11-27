using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PawFit.Online
{
    /// <summary>1 lời gọi HTTP tới máy chủ.</summary>
    public sealed class HttpCall
    {
        public string Method = "GET";
        public string Url;

        /// <summary>Thân JSON (null = không có).</summary>
        public string Body;

        /// <summary>Mã đăng nhập (gửi ở header Authorization: Bearer).</summary>
        public string Token;

        public int TimeoutSeconds = 10;
    }

    /// <summary>Trả lời HTTP. Status 0 = không tới được máy chủ (mất mạng, sai địa chỉ, quá giờ).</summary>
    public sealed class HttpReply
    {
        public int Status;
        public string Body;
        public string Error;
    }

    /// <summary>
    /// Gửi HTTP. Trong game Unity: UnityWebRequest (PawFit.App). Ngoài Unity (test, công cụ): <see cref="DotNetHttpTransport"/>.
    /// `done` có thể chạy ở luồng khác: <see cref="ServerApi"/> tự chuyển về luồng chính.
    /// </summary>
    public interface IHttpTransport
    {
        void Send(HttpCall call, Action<HttpReply> done);
    }

    /// <summary>HTTP bằng HttpClient của .NET (test + công cụ chạy ngoài Unity).</summary>
    public sealed class DotNetHttpTransport : IHttpTransport
    {
        private static readonly HttpClient Client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };

        public void Send(HttpCall call, Action<HttpReply> done)
        {
            if (call == null) throw new ArgumentNullException(nameof(call));
            Task.Run(async () =>
            {
                var reply = new HttpReply();
                try
                {
                    using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, call.TimeoutSeconds))))
                    using (var req = new HttpRequestMessage(new HttpMethod(call.Method), call.Url))
                    {
                        if (call.Body != null) req.Content = new StringContent(call.Body, Encoding.UTF8, "application/json");
                        if (!string.IsNullOrEmpty(call.Token)) req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + call.Token);
                        using (HttpResponseMessage res = await Client.SendAsync(req, cts.Token).ConfigureAwait(false))
                        {
                            reply.Status = (int)res.StatusCode;
                            reply.Body = await res.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                    }
                }
                catch (Exception e)
                {
                    reply.Status = 0;
                    reply.Error = e.Message;
                }
                done?.Invoke(reply);
            });
        }
    }

    public enum SocketEventKind { Open = 0, Message = 1, Closed = 2 }

    public struct SocketEvent
    {
        public SocketEventKind Kind;
        public string Text;
        public string Error;
    }

    /// <summary>
    /// 1 kết nối WebSocket dùng 1 lần, kiểu "bơm": việc mạng chạy nền, sự kiện xếp hàng chờ luồng chính lấy ra bằng
    /// <see cref="TryReceive"/> (Unity không cho gọi API của nó ở luồng khác). Nối lại = tạo kết nối mới.
    /// </summary>
    public interface ISocketTransport
    {
        bool IsOpen { get; }
        void Connect(string url);
        void Send(string text);
        void Close();
        bool TryReceive(out SocketEvent e);
    }

    /// <summary>WebSocket bằng System.Net.WebSockets.ClientWebSocket (chạy được trên Unity Android / iOS lẫn .NET).</summary>
    public sealed class ClientWebSocketTransport : ISocketTransport
    {
        private readonly ConcurrentQueue<SocketEvent> _events = new ConcurrentQueue<SocketEvent>();
        private readonly ConcurrentQueue<string> _outbox = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private ClientWebSocket _ws;
        private volatile bool _open;
        private int _started;

        public bool IsOpen => _open;

        public void Connect(string url)
        {
            if (Interlocked.Exchange(ref _started, 1) != 0) throw new InvalidOperationException("Mỗi kết nối chỉ dùng 1 lần");
            _ws = new ClientWebSocket();
            _ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            Task.Run(() => RunAsync(url));
        }

        public void Send(string text)
        {
            if (!_open || text == null) return;
            _outbox.Enqueue(text);
            _signal.Release();
        }

        /// <summary>Đóng (gửi khung đóng rồi thôi chờ). Sự kiện Closed vẫn được báo 1 lần.</summary>
        public void Close()
        {
            ClientWebSocket ws = _ws;
            if (ws == null || _cts.IsCancellationRequested) return;
            Task.Run(async () =>
            {
                try
                {
                    if (ws.State == WebSocketState.Open)
                        using (var t = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                            await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", t.Token).ConfigureAwait(false);
                    await Task.Delay(500).ConfigureAwait(false);   // chờ máy chủ đáp khung đóng cho đẹp
                }
                catch (Exception)
                {
                    // đang đóng: lỗi gì cũng bỏ qua
                }
                finally
                {
                    Cancel();
                }
            });
        }

        public bool TryReceive(out SocketEvent e) => _events.TryDequeue(out e);

        private void Cancel()
        {
            try
            {
                _cts.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // đã đóng hẳn
            }
        }

        private async Task RunAsync(string url)
        {
            string error = null;
            try
            {
                using (var connectTimeout = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                {
                    connectTimeout.CancelAfter(TimeSpan.FromSeconds(10));
                    await _ws.ConnectAsync(new Uri(url), connectTimeout.Token).ConfigureAwait(false);
                }
                _open = true;
                _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Open });
                Task sending = SendLoopAsync();
                await ReceiveLoopAsync().ConfigureAwait(false);
                Cancel();
                await sending.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (!_cts.IsCancellationRequested) error = e.Message;
            }
            finally
            {
                _open = false;
                Cancel();
                try
                {
                    _ws.Dispose();
                }
                catch (Exception)
                {
                    // bỏ qua
                }
                _events.Enqueue(new SocketEvent { Kind = SocketEventKind.Closed, Error = error });
            }
        }

        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[4096];
            using (var message = new MemoryStream())
            {
                while (_ws.State == WebSocketState.Open && !_cts.IsCancellationRequested)
                {
                    WebSocketReceiveResult r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token).ConfigureAwait(false);
                    if (r.MessageType == WebSocketMessageType.Close)   // máy chủ đóng: đáp khung đóng rồi thôi
                    {
                        try
                        {
                            using (var t = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", t.Token).ConfigureAwait(false);
                        }
                        catch (Exception)
                        {
                            // bỏ qua
                        }
                        return;
                    }
                    message.Write(buffer, 0, r.Count);
                    if (message.Length > NetProto.MaxMessageBytes) throw new InvalidDataException("Tin nhắn quá dài");
                    if (!r.EndOfMessage) continue;
                    if (r.MessageType == WebSocketMessageType.Text)
                        _events.Enqueue(new SocketEvent
                        {
                            Kind = SocketEventKind.Message,
                            Text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length),
                        });
                    message.SetLength(0);
                }
            }
        }

        private async Task SendLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    await _signal.WaitAsync(_cts.Token).ConfigureAwait(false);
                    while (_outbox.TryDequeue(out string text))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(text);
                        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token).ConfigureAwait(false);
                    }
                }
            }
            catch (Exception)
            {
                Cancel();   // gửi hỏng = kết nối hỏng: vòng nhận tự dừng và báo Closed
            }
        }
    }
}
