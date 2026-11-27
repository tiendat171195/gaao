using System;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    public enum NetworkState { Offline = 0, Online = 1, Searching = 2, InMatch = 3 }

    /// <summary>Đối thủ của 1 trận PvP (máy chủ gửi về).</summary>
    public sealed class OpponentInfo
    {
        public string PlayerId;
        public string Name;
        public string DnaCode;
        public int Elo;
        public float Power;

        /// <summary>Chỉ số chiến đấu của thú đối thủ (đã tính bệnh): dựng bên đối thủ trong BattleSimulation.</summary>
        public FighterStats Stats;

        /// <summary>Chiêu đối thủ đã thuộc (bit 0..3 = ngang / lên / V / Z).</summary>
        public int LearnedMask;

        /// <summary>true = người chơi giả của bản giả lập: máy tự điều khiển đối thủ bằng BattleBot.</summary>
        public bool IsGhost;
    }

    /// <summary>1 trận vừa ghép được.</summary>
    public sealed class MatchInfo
    {
        public string MatchId;
        public OpponentInfo Opponent;

        /// <summary>Seed chung của trận: 2 máy chạy BattleSimulation giống hệt nhau (mô phỏng bước cố định có seed).</summary>
        public ulong Seed;

        /// <summary>Mình là bên 0 hay bên 1 trong BattleSimulation.</summary>
        public int LocalSide;
    }

    /// <summary>
    /// Cổng mạng cho PvP (brief: NetworkManager phải là interface). Bản thật: Firebase Auth (4.1) + Photon Fusion (4.2).
    /// Hiện có <see cref="LocalNetworkManager"/> (giả lập, không cần tài khoản). Chỉ App chọn implementation.
    /// </summary>
    public interface INetworkManager
    {
        NetworkState State { get; }

        /// <summary>Đăng nhập + nối máy chủ. `done(true)` = đã Online.</summary>
        void Connect(string playerId, Action<bool> done);

        void Disconnect();

        /// <summary>Xếp hàng tìm trận (cần State == Online). Tìm thấy thì báo <see cref="MatchFound"/>.</summary>
        void FindMatch(MatchTicket ticket);

        void CancelFindMatch();

        /// <summary>Đã tìm bao lâu (giây) + trạng thái: đang tìm / đã nới khoảng / gợi ý đấu Bot.</summary>
        double SearchSeconds { get; }
        MatchSearchStatus SearchStatus { get; }

        /// <summary>Gửi 1 nét vuốt của mình ở tick mô phỏng `tick` cho đối thủ.</summary>
        void SendSwipe(SwipeResult swipe, int tick);

        /// <summary>Rời trận (hết giờ, bị hạ, bỏ cuộc).</summary>
        void LeaveMatch();

        event Action<MatchInfo> MatchFound;
        event Action<SwipeResult, int> OpponentSwipe;
        event Action OpponentLeft;
    }

    /// <summary>Số liệu của mạng giả lập: người chơi giả vào hàng chờ cách nhau 2–9 giây, Elo ±250, lực chiến ±30% quanh mình.</summary>
    public sealed class LocalNetworkConfig
    {
        public int MaxGhosts = 15;
        public double GhostEverySecondsMin = 2.0;
        public double GhostEverySecondsMax = 9.0;
        public int GhostEloSpread = 250;
        public float GhostPowerSpread = 0.30f;
    }

    /// <summary>
    /// Mạng giả lập (C# thuần, chưa cần Photon): "người chơi giả" sinh theo seed quanh Elo + lực chiến của mình lần lượt vào
    /// hàng chờ, ghép bằng <see cref="Matchmaker"/> thật (đúng luật ±100 / ±10%, 30 giây nới, 60 giây gợi ý Bot).
    /// Ghép trúng người giả thì báo MatchFound với IsGhost = true: màn đấu tự điều khiển đối thủ bằng BattleBot.
    /// App gọi <see cref="Update"/> mỗi frame (thời gian lấy từ ITimeProvider).
    /// </summary>
    public sealed class LocalNetworkManager : INetworkManager
    {
        private static readonly string[] GhostNames = { "Mun", "Bông", "Su Su", "Tôm", "Bơ", "Cốm", "Mochi", "Kẹo", "Mít", "Nấm", "Gạo", "Sữa" };

        private readonly ITimeProvider _time;
        private readonly IRandom _rng;
        private readonly MatchmakingConfig _cfg;
        private readonly LocalNetworkConfig _net;
        private readonly Matchmaker _matchmaker;
        private MatchTicket _mine;
        private DateTime _searchStartUtc, _nextGhostUtc;
        private int _ghosts;
        private string _playerId;

        public LocalNetworkManager(ITimeProvider time, IRandom rng, MatchmakingConfig cfg, LocalNetworkConfig net = null)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _net = net ?? new LocalNetworkConfig();
            _matchmaker = new Matchmaker(cfg);
        }

        public NetworkState State { get; private set; } = NetworkState.Offline;

        public double SearchSeconds => State == NetworkState.Searching ? Math.Max(0.0, (_time.UtcNow - _searchStartUtc).TotalSeconds) : 0.0;
        public MatchSearchStatus SearchStatus => MatchmakingRules.Status(SearchSeconds, _cfg);

        /// <summary>Vé của mình đang tìm (để View lấy Elo / lực chiến hiện lên).</summary>
        public MatchTicket CurrentTicket => _mine;

        public event Action<MatchInfo> MatchFound;

        // Người chơi giả do chính máy này điều khiển (BattleBot) nên không có nét vuốt / rời trận từ xa.
        public event Action<SwipeResult, int> OpponentSwipe { add { } remove { } }
        public event Action OpponentLeft { add { } remove { } }

        public void Connect(string playerId, Action<bool> done)
        {
            _playerId = string.IsNullOrEmpty(playerId) ? "local" : playerId;
            if (State == NetworkState.Offline) State = NetworkState.Online;
            done?.Invoke(true);
        }

        public void Disconnect()
        {
            CancelFindMatch();
            State = NetworkState.Offline;
        }

        public void FindMatch(MatchTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            if (State != NetworkState.Online) throw new InvalidOperationException("Chưa kết nối hoặc đang trong trận / đang tìm");
            DateTime now = _time.UtcNow;
            _mine = ticket;
            if (string.IsNullOrEmpty(_mine.PlayerId)) _mine.PlayerId = _playerId;
            _mine.QueuedUtc = now;
            _searchStartUtc = now;
            _ghosts = 0;
            _nextGhostUtc = now.AddSeconds(NextGap());
            _matchmaker.Enqueue(_mine);
            State = NetworkState.Searching;
        }

        public void CancelFindMatch()
        {
            if (State != NetworkState.Searching) return;
            ClearQueue();
            State = NetworkState.Online;
        }

        public void SendSwipe(SwipeResult swipe, int tick)
        {
            // người chơi giả không cần nhận: máy tự điều khiển đối thủ
        }

        public void LeaveMatch()
        {
            if (State == NetworkState.InMatch) State = NetworkState.Online;
        }

        /// <summary>Cho thời gian trôi: người chơi giả tới hàng chờ, ghép trận.</summary>
        public void Update()
        {
            if (State != NetworkState.Searching) return;
            DateTime now = _time.UtcNow;
            while (_ghosts < _net.MaxGhosts && now >= _nextGhostUtc)
            {
                _matchmaker.Enqueue(MakeGhost(_nextGhostUtc));
                _nextGhostUtc = _nextGhostUtc.AddSeconds(NextGap());
            }
            foreach (MatchPair p in _matchmaker.Tick(now))
            {
                MatchTicket other = p.A == _mine ? p.B : p.B == _mine ? p.A : null;
                if (other == null) continue;   // 2 người giả ghép với nhau (giống người chơi khác tìm được trận)
                ClearQueue();
                State = NetworkState.InMatch;
                var info = new MatchInfo
                {
                    MatchId = "local-" + _rng.NextUInt().ToString("x8"),
                    Seed = ((ulong)_rng.NextUInt() << 32) | _rng.NextUInt(),
                    LocalSide = 0,
                    Opponent = ToOpponent(other),
                };
                MatchFound?.Invoke(info);
                return;
            }
        }

        /// <summary>Bỏ cả người giả còn chờ: lần tìm sau sinh người giả mới quanh Elo lúc đó.</summary>
        private void ClearQueue()
        {
            _matchmaker.Clear();
            _mine = null;
        }

        private double NextGap() =>
            _net.GhostEverySecondsMin + _rng.NextDouble() * Math.Max(0.0, _net.GhostEverySecondsMax - _net.GhostEverySecondsMin);

        private MatchTicket MakeGhost(DateTime queuedUtc)
        {
            _ghosts++;
            float powerK = 1f + (float)((_rng.NextDouble() * 2.0 - 1.0) * _net.GhostPowerSpread);
            int elo = Math.Max(0, _mine.Elo + _rng.Range(-_net.GhostEloSpread, _net.GhostEloSpread + 1));
            return new MatchTicket
            {
                PlayerId = "ghost-" + _ghosts,
                Name = GhostNames[_rng.Next(GhostNames.Length)],
                DnaCode = DnaCodec.Encode(DnaGenerator.Random(_rng)),
                Elo = elo,
                Power = Math.Max(1f, _mine.Power * powerK),
                Stats = Scale(_mine.Stats, powerK),
                LearnedMask = _rng.Next(16),
                QueuedUtc = queuedUtc,
            };
        }

        private static FighterStats Scale(FighterStats s, float k) => new FighterStats(s.Hp * k, s.Atk * k, s.Def * k, s.Spd * k, s.Crit);

        private static OpponentInfo ToOpponent(MatchTicket t) => new OpponentInfo
        {
            PlayerId = t.PlayerId,
            Name = t.Name,
            DnaCode = t.DnaCode,
            Elo = t.Elo,
            Power = t.Power,
            Stats = t.Stats,
            LearnedMask = t.LearnedMask,
            IsGhost = true,
        };
    }
}
