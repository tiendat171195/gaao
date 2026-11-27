using System;
using System.Collections.Generic;
using System.Diagnostics;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    /// <summary>
    /// Nhịp 1 trận qua máy chủ riêng (máy chủ gửi về): bước đã chốt, nét vuốt của 2 bên, máu để đối chiếu, kết quả + rank mới.
    /// BattleSession đọc qua <see cref="IMatchFeed"/>; App đọc <see cref="End"/> để hiện Elo / Coin.
    /// </summary>
    public sealed class NetMatchFeed : IMatchFeed
    {
        private readonly Action _ready;
        private readonly Action<SwipeResult> _swipe;
        private readonly Action _leave;
        private readonly Queue<TickInput> _inputs = new Queue<TickInput>();
        private readonly Queue<HpCheck> _hp = new Queue<HpCheck>();
        private readonly FighterStats[] _stats = new FighterStats[2];
        private readonly bool[][] _learned = new bool[2][];

        /// <param name="ready">Báo máy chủ màn đấu đã sẵn sàng.</param>
        /// <param name="swipe">Gửi 1 nét vuốt lên máy chủ.</param>
        /// <param name="leave">Bỏ cuộc.</param>
        public NetMatchFeed(FoundMsg m, Action ready = null, Action<SwipeResult> swipe = null, Action leave = null)
        {
            if (m == null || m.Players == null || m.Players.Length < 2) throw new ArgumentException("Tin ghép trận thiếu người chơi", nameof(m));
            _ready = ready;
            _swipe = swipe;
            _leave = leave;
            MatchId = m.MatchId;
            ServerSide = m.Side == 1 ? 1 : 0;
            Seed = m.Seed;
            DurationSeconds = m.DurationMs > 0 ? m.DurationMs / 1000f : 0f;
            for (int i = 0; i < 2; i++)
            {
                NetPlayer p = m.Players[i];
                _stats[i] = p.Stats != null ? p.Stats.ToStats() : new FighterStats(1f, 0f, 0f, 0f, 0f);
                _learned[i] = BotPlayers.Learned(p.LearnedMask);
            }
        }

        public string MatchId { get; }
        public int ServerSide { get; }
        public ulong Seed { get; }
        public float DurationSeconds { get; }

        public FighterStats StatsOf(int serverSide) => _stats[serverSide == 1 ? 1 : 0];
        public bool[] LearnedOf(int serverSide) => (bool[])_learned[serverSide == 1 ? 1 : 0].Clone();

        public bool Started { get; private set; }
        public int ConfirmedTicks { get; private set; }
        public bool Ended { get; private set; }
        public int EndTick { get; private set; }
        public BattleOutcome EndOutcome { get; private set; }
        public string EndReason { get; private set; }
        public bool Lost { get; private set; }

        /// <summary>Tin hết trận của máy chủ (rank mới, Elo trước trận, Coin thưởng). null khi chưa hết.</summary>
        public EndMsg End { get; private set; }

        /// <summary>Mình thắng theo máy chủ.</summary>
        public bool Won => Ended && FeedSides.Local(EndOutcome, ServerSide) == BattleOutcome.Win0;

        public bool TryPeekInput(out TickInput input)
        {
            if (_inputs.Count == 0)
            {
                input = default;
                return false;
            }
            input = _inputs.Peek();
            return true;
        }

        public void PopInput()
        {
            if (_inputs.Count > 0) _inputs.Dequeue();
        }

        public bool TryPeekHp(out HpCheck check)
        {
            if (_hp.Count == 0)
            {
                check = default;
                return false;
            }
            check = _hp.Peek();
            return true;
        }

        public void PopHp()
        {
            if (_hp.Count > 0) _hp.Dequeue();
        }

        public void Ready() => _ready?.Invoke();

        public void SendSwipe(SwipeResult swipe)
        {
            if (!Ended && !Lost) _swipe?.Invoke(swipe);
        }

        public void Leave() => _leave?.Invoke();

        /// <summary>Máy chủ báo bắt đầu trận.</summary>
        public void ApplyStart() => Started = true;

        /// <summary>Máy chủ gửi nhịp trận: nét vuốt đã chốt + số bước đã chạy + máu.</summary>
        public void ApplyFrame(FrameMsg f)
        {
            if (f.I != null)
                for (int i = 0; i + 4 < f.I.Length; i += 5)
                    _inputs.Enqueue(new TickInput
                    {
                        Tick = f.I[i],
                        Side = f.I[i + 1] == 1 ? 1 : 0,
                        Swipe = SwipeMsg.ToSwipe(f.I[i + 2], f.I[i + 3], f.I[i + 4]),
                    });
            if (f.H != null && f.H.Length >= 2)
                _hp.Enqueue(new HpCheck { Ticks = f.N, Hp0 = NetProto.Float(f.H[0]), Hp1 = NetProto.Float(f.H[1]) });
            if (f.N > ConfirmedTicks) ConfirmedTicks = f.N;
            Started = true;
        }

        /// <summary>Máy chủ báo hết trận (kết quả + rank mới).</summary>
        public void ApplyEnd(EndMsg e)
        {
            End = e;
            EndTick = Math.Max(0, e.EndTick);
            EndOutcome = (BattleOutcome)FeedSides.Clamp(e.Outcome, 0, 3);
            EndReason = e.Reason;
            if (EndTick > ConfirmedTicks) ConfirmedTicks = EndTick;
            Started = true;
            Ended = true;
        }

        /// <summary>Mất kết nối giữa trận.</summary>
        public void MarkLost()
        {
            if (!Ended) Lost = true;
        }
    }

    /// <summary>
    /// Mạng PvP qua máy chủ riêng (WebSocket, C# thuần): nối + chào bằng mã đăng nhập, xếp hàng, nhận trận, gửi nét vuốt,
    /// nhận nhịp trận. Máy chủ ghép người thật trước; chờ vài giây không có thì thả người chơi ảo (chạy trên máy chủ, máy không
    /// biết là ảo). Elo do máy chủ tính. App gọi <see cref="Update"/> mỗi frame; mọi event chạy trong Update.
    /// </summary>
    public sealed class ServerNetworkManager : INetworkManager
    {
        private readonly string _url;
        private readonly Func<string> _token;
        private readonly Func<ISocketTransport> _newSocket;
        private readonly ITimeProvider _time;
        private readonly MatchmakingConfig _cfg;
        private readonly ILog _log;
        private readonly string _build;
        private readonly List<Action<bool>> _connectDone = new List<Action<bool>>();
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private ISocketTransport _socket;
        private DateTime _searchStartUtc;
        private NetMatchFeed _match;
        private double _nextPingAt;

        /// <param name="pvpUrl">vd "wss://pawfit.example.com/v1/pvp" (<see cref="ServerApi.PvpUrl"/>).</param>
        /// <param name="token">Mã đăng nhập hiện tại (OnlineService); null = chưa đăng nhập.</param>
        /// <param name="newSocket">Tạo 1 kết nối WebSocket mới (mặc định ClientWebSocketTransport).</param>
        public ServerNetworkManager(string pvpUrl, Func<string> token, ITimeProvider time, GameConfig cfg,
                                    Func<ISocketTransport> newSocket = null, ILog log = null, string build = null)
        {
            if (string.IsNullOrWhiteSpace(pvpUrl)) throw new ArgumentException("Thiếu địa chỉ máy chủ", nameof(pvpUrl));
            _url = pvpUrl;
            _token = token ?? throw new ArgumentNullException(nameof(token));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _cfg = (cfg ?? throw new ArgumentNullException(nameof(cfg))).Matchmaking;
            _newSocket = newSocket ?? (() => new ClientWebSocketTransport());
            _log = log ?? NullLog.Instance;
            _build = build;
        }

        public NetworkState State { get; private set; } = NetworkState.Offline;

        public double SearchSeconds => State == NetworkState.Searching ? Math.Max(0.0, (_time.UtcNow - _searchStartUtc).TotalSeconds) : 0.0;
        public MatchSearchStatus SearchStatus => MatchmakingRules.Status(SearchSeconds, _cfg);

        public string PlayerId { get; private set; }

        /// <summary>Elo máy chủ đang tính cho mình (lúc chào / lúc xếp hàng).</summary>
        public int ServerElo { get; private set; }

        /// <summary>Độ trễ đi + về gần nhất (ms), -1 = chưa đo.</summary>
        public int PingMs { get; private set; } = -1;

        public string LastError { get; private set; }

        /// <summary>Trận đang đánh (null nếu không ở trong trận).</summary>
        public NetMatchFeed CurrentMatch => _match;

        public event Action<MatchInfo> MatchFound;

        /// <summary>Mất kết nối lúc đang tìm / đang trong trận (kèm lý do). View báo người chơi.</summary>
        public event Action<string> ConnectionLost;

        public void Connect(string playerId, Action<bool> done)
        {
            if (State == NetworkState.Online || State == NetworkState.Searching || State == NetworkState.InMatch)
            {
                done?.Invoke(true);
                return;
            }
            if (done != null) _connectDone.Add(done);
            if (State == NetworkState.Connecting) return;
            if (string.IsNullOrEmpty(_token()))
            {
                LastError = "Chưa đăng nhập được máy chủ";
                FinishConnect(false);
                return;
            }
            State = NetworkState.Connecting;
            LastError = null;
            _socket = _newSocket();
            _socket.Connect(_url);
        }

        public void Disconnect()
        {
            _socket?.Close();
            Drop(null);
        }

        public void FindMatch(MatchTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (State != NetworkState.Online) throw new InvalidOperationException("Chưa kết nối hoặc đang trong trận / đang tìm");
            DateTime now = _time.UtcNow;
            ticket.QueuedUtc = now;
            _searchStartUtc = now;
            Send(new QueueMsg { T = MsgType.Queue, Ticket = NetTicket.From(ticket) });
            State = NetworkState.Searching;
        }

        public void CancelFindMatch()
        {
            if (State != NetworkState.Searching) return;
            Send(new NetMsg { T = MsgType.Cancel });
            State = NetworkState.Online;
        }

        public void LeaveMatch()
        {
            if (_match != null && !_match.Ended && State == NetworkState.InMatch) Send(new MatchMsg { T = MsgType.Leave, MatchId = _match.MatchId });
            _match = null;
            if (State == NetworkState.InMatch) State = NetworkState.Online;
        }

        private void SendReady(NetMatchFeed feed)
        {
            if (feed == _match && State == NetworkState.InMatch) Send(new MatchMsg { T = MsgType.Ready, MatchId = feed.MatchId });
        }

        private void SendSwipe(NetMatchFeed feed, SwipeResult swipe)
        {
            if (feed == _match && State == NetworkState.InMatch && !feed.Ended) Send(SwipeMsg.From(swipe));
        }

        public void Update()
        {
            ISocketTransport socket = _socket;
            if (socket == null) return;
            while (socket == _socket && socket.TryReceive(out SocketEvent e))
            {
                switch (e.Kind)
                {
                    case SocketEventKind.Open:
                        Send(new HelloMsg { T = MsgType.Hello, Token = _token(), Proto = NetProto.Version, Build = _build });
                        break;
                    case SocketEventKind.Message:
                        Handle(e.Text);
                        break;
                    case SocketEventKind.Closed:
                        Drop(e.Error ?? "Máy chủ đã đóng kết nối");
                        return;
                }
            }
            if (_socket != null && State != NetworkState.Connecting && _clock.Elapsed.TotalSeconds >= _nextPingAt)
            {
                _nextPingAt = _clock.Elapsed.TotalSeconds + 5.0;
                Send(new PingMsg { T = MsgType.Ping, C = (long)_clock.Elapsed.TotalMilliseconds });
            }
        }

        private void Handle(string text)
        {
            NetMsg head;
            try
            {
                head = NetProto.Read<NetMsg>(text);
            }
            catch (Exception)
            {
                _log.Warn("[PawFit] Tin máy chủ hỏng");
                return;
            }
            switch (head?.T)
            {
                case MsgType.Welcome:
                    {
                        var m = NetProto.Read<WelcomeMsg>(text);
                        PlayerId = m.PlayerId;
                        ServerElo = m.Elo;
                        State = NetworkState.Online;
                        _nextPingAt = _clock.Elapsed.TotalSeconds + 5.0;
                        FinishConnect(true);
                        break;
                    }
                case MsgType.Error:
                    {
                        var m = NetProto.Read<ErrorMsg>(text);
                        LastError = m.Code == "proto" ? "Bản game cũ, hãy cập nhật để đấu PvP" : m.Message;
                        _log.Warn("[PawFit] Máy chủ báo lỗi " + m.Code + ": " + m.Message);
                        if (State == NetworkState.Connecting || m.Code == "auth" || m.Code == "proto")
                        {
                            _socket?.Close();
                            Drop(LastError);
                        }
                        else if (State == NetworkState.Searching)
                            State = NetworkState.Online;   // vé bị từ chối (vd thú không hợp lệ)
                        break;
                    }
                case MsgType.Queued:
                    ServerElo = NetProto.Read<QueuedMsg>(text).Elo;
                    break;
                case MsgType.Found:
                    OnFound(NetProto.Read<FoundMsg>(text));
                    break;
                case MsgType.Start:
                    {
                        var m = NetProto.Read<MatchMsg>(text);
                        if (_match != null && m.MatchId == _match.MatchId) _match.ApplyStart();
                        break;
                    }
                case MsgType.Frame:
                    _match?.ApplyFrame(NetProto.Read<FrameMsg>(text));
                    break;
                case MsgType.End:
                    {
                        var m = NetProto.Read<EndMsg>(text);
                        if (_match != null && m.MatchId == _match.MatchId) _match.ApplyEnd(m);
                        if (m.Rank != null) ServerElo = m.Rank.Elo;
                        break;
                    }
                case MsgType.Pong:
                    {
                        var m = NetProto.Read<PingMsg>(text);
                        PingMs = (int)Math.Max(0L, (long)_clock.Elapsed.TotalMilliseconds - m.C);
                        break;
                    }
            }
        }

        private void OnFound(FoundMsg m)
        {
            if (State != NetworkState.Searching || m.Players == null || m.Players.Length < 2) return;
            int side = m.Side == 1 ? 1 : 0;
            NetMatchFeed feed = null;
            feed = new NetMatchFeed(m, () => SendReady(feed), sw => SendSwipe(feed, sw), LeaveMatch);
            _match = feed;
            State = NetworkState.InMatch;
            var info = new MatchInfo
            {
                MatchId = m.MatchId,
                Seed = m.Seed,
                LocalSide = side,
                Opponent = ToOpponent(m.Players[1 - side]),
                Feed = _match,
            };
            MatchFound?.Invoke(info);
        }

        /// <summary>Đối thủ máy chủ gửi về → dữ liệu để dựng thú bên phải. Số lạ thì về giá trị an toàn.</summary>
        public static OpponentInfo ToOpponent(NetPlayer p)
        {
            PetDna dna;
            if (!DnaCodec.TryDecode(p.DnaCode, out dna)) dna = DnaGenerator.Random(new SeededRandom((ulong)(p.PlayerId ?? "?").GetHashCode() ^ 0x5bd1e995UL));
            return new OpponentInfo
            {
                PlayerId = p.PlayerId,
                Name = string.IsNullOrEmpty(p.Name) ? "Người chơi" : p.Name,
                PetName = string.IsNullOrEmpty(p.PetName) ? "Thú" : p.PetName,
                Dna = dna,
                DnaCode = p.DnaCode,
                Elo = p.Elo,
                Power = FeedSides.Finite(p.Power, 0f),
                Stage = (EvolutionStage)FeedSides.Clamp(p.Stage, 0, (int)EvolutionStage.Ultimate),
                Form = (AdultForm)FeedSides.Clamp(p.Form, 0, (int)AdultForm.Shadow),
                Accessory = (AccessoryKind)FeedSides.Clamp(p.Accessory, 0, (int)AccessoryKind.Crown),
                Stats = p.Stats != null ? p.Stats.ToStats() : new FighterStats(1f, 0f, 0f, 0f, 0f),
                LearnedMask = p.LearnedMask & 15,
                IsGhost = false,
            };
        }

        private void Send(NetMsg m)
        {
            if (_socket != null && _socket.IsOpen) _socket.Send(NetProto.Write(m));
        }

        private void FinishConnect(bool ok)
        {
            if (_connectDone.Count == 0) return;
            var list = new List<Action<bool>>(_connectDone);
            _connectDone.Clear();
            foreach (Action<bool> a in list) a(ok);
        }

        /// <summary>Mất kết nối: trận đang đánh tính là bỏ cuộc (máy chủ cũng tính vậy).</summary>
        private void Drop(string reason)
        {
            NetworkState was = State;
            _socket = null;
            State = NetworkState.Offline;
            PingMs = -1;
            if (reason != null) LastError = reason;
            _match?.MarkLost();
            FinishConnect(false);
            if (was == NetworkState.Searching || was == NetworkState.InMatch) ConnectionLost?.Invoke(reason);
        }
    }
}
