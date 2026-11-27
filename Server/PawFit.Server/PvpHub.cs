using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PawFit.Core;
using PawFit.Online;

namespace PawFit.Server
{
    /// <summary>1 kết nối WebSocket của 1 người chơi. Gửi được từ mọi luồng (xếp hàng, 1 vòng gửi duy nhất).</summary>
    public sealed class PvpConnection
    {
        private static long _next;

        private readonly WebSocket _ws;
        private readonly ConcurrentQueue<string> _out = new ConcurrentQueue<string>();
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _cts;
        private volatile bool _closeAfterFlush;

        /// <param name="aborted">Máy kia ngắt.</param>
        /// <param name="stopping">Máy chủ đang tắt: cắt ngay, không bắt máy chủ chờ.</param>
        public PvpConnection(WebSocket ws, CancellationToken aborted, CancellationToken stopping)
        {
            _ws = ws;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(aborted, stopping);
            Id = Interlocked.Increment(ref _next);
        }

        public long Id { get; }

        /// <summary>Người chơi (sau khi chào đúng mã đăng nhập).</summary>
        public PlayerRow Player { get; set; }

        public bool IsClosed => _cts.IsCancellationRequested || _closeAfterFlush;

        public void Send(NetMsg m) => Send(NetProto.Write(m));

        public void Send(string json)
        {
            if (IsClosed || json == null) return;
            _out.Enqueue(json);
            _signal.Release();
        }

        /// <summary>Gửi nốt các tin đang chờ rồi đóng kết nối.</summary>
        public void CloseAfterSend()
        {
            _closeAfterFlush = true;
            _signal.Release();
        }

        internal async Task SendLoopAsync()
        {
            try
            {
                while (!_cts.IsCancellationRequested)
                {
                    await _signal.WaitAsync(_cts.Token);
                    while (_out.TryDequeue(out string json))
                    {
                        byte[] bytes = Encoding.UTF8.GetBytes(json);
                        await _ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
                    }
                    if (_closeAfterFlush)
                    {
                        if (_ws.State == WebSocketState.Open || _ws.State == WebSocketState.CloseReceived)
                            using (var t = new CancellationTokenSource(TimeSpan.FromSeconds(2)))
                                await _ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", t.Token);
                        break;
                    }
                }
            }
            catch (Exception)
            {
                // kết nối hỏng / đã đóng: thôi gửi
            }
            finally
            {
                _cts.Cancel();
            }
        }

        /// <summary>Đọc 1 tin văn bản. null = đã đóng / quá giờ / tin quá dài.</summary>
        internal async Task<string> ReceiveAsync(TimeSpan? timeout)
        {
            var buffer = new byte[4096];
            using (var message = new MemoryStream())
            using (CancellationTokenSource cts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
            {
                if (timeout.HasValue) cts.CancelAfter(timeout.Value);
                try
                {
                    while (true)
                    {
                        WebSocketReceiveResult r = await _ws.ReceiveAsync(new ArraySegment<byte>(buffer), cts.Token);
                        if (r.MessageType == WebSocketMessageType.Close) return null;
                        message.Write(buffer, 0, r.Count);
                        if (message.Length > NetProto.MaxMessageBytes) return null;
                        if (!r.EndOfMessage) continue;
                        if (r.MessageType != WebSocketMessageType.Text)
                        {
                            message.SetLength(0);
                            continue;
                        }
                        return Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }

    /// <summary>
    /// Cửa WebSocket PvP: chào bằng mã đăng nhập (10 giây), rồi chuyển tin vào <see cref="MatchService"/> (1 luồng xử lý trận).
    /// Mỗi tài khoản 1 kết nối: nối từ máy / tab khác thì kết nối cũ bị cắt (trận đang đánh tính bỏ cuộc).
    /// </summary>
    public sealed class PvpHub
    {
        /// <summary>Mỗi kết nối gửi tối đa bấy nhiêu tin / giây (người vuốt không thể nhanh vậy; quá = phá, cắt).</summary>
        public const int MaxMessagesPerSecond = 40;

        private readonly GameStore _store;
        private readonly MatchService _match;
        private readonly ITimeProvider _time;
        private readonly ILogger<PvpHub> _log;
        private readonly CancellationToken _stopping;
        private readonly ConcurrentDictionary<string, PvpConnection> _online = new ConcurrentDictionary<string, PvpConnection>();

        public PvpHub(GameStore store, MatchService match, ITimeProvider time, ILogger<PvpHub> log, IHostApplicationLifetime lifetime)
        {
            _store = store;
            _match = match;
            _time = time;
            _log = log;
            _stopping = lifetime.ApplicationStopping;
        }

        public int OnlineCount => _online.Count;

        /// <summary>Cắt kết nối PvP của tài khoản (vd vừa chuyển sang máy khác).</summary>
        public void Kick(string playerId)
        {
            if (playerId != null && _online.TryGetValue(playerId, out PvpConnection c))
            {
                c.Send(Error("moved", "Tài khoản đã chuyển sang máy khác"));
                c.CloseAfterSend();
            }
        }

        public async Task RunAsync(WebSocket ws, CancellationToken aborted)
        {
            var conn = new PvpConnection(ws, aborted, _stopping);
            Task sending = conn.SendLoopAsync();
            try
            {
                HelloMsg hello = Read<HelloMsg>(await conn.ReceiveAsync(TimeSpan.FromSeconds(10)));
                if (hello == null || hello.T != MsgType.Hello)
                {
                    conn.Send(Error("bad", "Thiếu lời chào"));
                    return;
                }
                if (hello.Proto != NetProto.Version)
                {
                    conn.Send(Error("proto", "Bản game cũ, hãy cập nhật"));
                    return;
                }
                PlayerRow p = _store.FindByToken(hello.Token, _time.UtcNow);
                if (p == null)
                {
                    conn.Send(Error("auth", "Mã đăng nhập không đúng hoặc đã hết hiệu lực"));
                    return;
                }
                conn.Player = p;
                if (_online.TryGetValue(p.Id, out PvpConnection old) && old != conn)
                {
                    old.Send(Error("replaced", "Tài khoản vừa nối từ chỗ khác"));
                    old.CloseAfterSend();
                }
                _online[p.Id] = conn;
                DateTime now = _time.UtcNow;
                RankData rank = _store.GetRank(p.Id, now);
                conn.Send(new WelcomeMsg { T = MsgType.Welcome, PlayerId = p.Id, Name = p.Name, UtcMs = NetProto.ToMs(now), Elo = rank?.Elo ?? 0 });
                _match.Post(() => _match.OnConnected(conn));

                var rate = Stopwatch.StartNew();
                int count = 0;
                while (true)
                {
                    string text = await conn.ReceiveAsync(null);
                    if (text == null) break;
                    if (rate.ElapsedMilliseconds >= 1000)
                    {
                        rate.Restart();
                        count = 0;
                    }
                    if (++count > MaxMessagesPerSecond)
                    {
                        conn.Send(Error("busy", "Gửi quá nhiều tin"));
                        break;
                    }
                    Dispatch(conn, text);
                }
            }
            catch (Exception e)
            {
                _log.LogDebug(e, "Kết nối PvP lỗi");
            }
            finally
            {
                if (conn.Player != null)
                {
                    _online.TryRemove(new KeyValuePair<string, PvpConnection>(conn.Player.Id, conn));
                    _match.Post(() => _match.OnDisconnected(conn));
                }
                conn.CloseAfterSend();
                await sending;
            }
        }

        private void Dispatch(PvpConnection conn, string text)
        {
            NetMsg head = Read<NetMsg>(text);
            switch (head?.T)
            {
                case MsgType.Queue:
                    {
                        QueueMsg m = Read<QueueMsg>(text);
                        if (m?.Ticket != null) _match.Post(() => _match.OnQueue(conn, m.Ticket));
                        break;
                    }
                case MsgType.Cancel:
                    _match.Post(() => _match.OnCancel(conn));
                    break;
                case MsgType.Ready:
                    {
                        MatchMsg m = Read<MatchMsg>(text);
                        _match.Post(() => _match.OnReady(conn, m?.MatchId));
                        break;
                    }
                case MsgType.Swipe:
                    {
                        SwipeMsg m = Read<SwipeMsg>(text);
                        if (m != null) _match.Post(() => _match.OnSwipe(conn, m));
                        break;
                    }
                case MsgType.Leave:
                    _match.Post(() => _match.OnLeave(conn));
                    break;
                case MsgType.Ping:
                    {
                        PingMsg m = Read<PingMsg>(text);
                        conn.Send(new PingMsg { T = MsgType.Pong, C = m?.C ?? 0, S = NetProto.ToMs(_time.UtcNow) });
                        break;
                    }
            }
        }

        internal static ErrorMsg Error(string code, string message) => new ErrorMsg { T = MsgType.Error, Code = code, Message = message };

        private static T Read<T>(string text) where T : class
        {
            if (string.IsNullOrEmpty(text)) return null;
            try
            {
                return NetProto.Read<T>(text);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
