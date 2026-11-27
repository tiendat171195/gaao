using System;
using System.Collections.Generic;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    public enum NetworkState { Offline = 0, Online = 1, Searching = 2, InMatch = 3 }

    /// <summary>Đối thủ của 1 trận PvP (máy chủ gửi về, hoặc người chơi ảo sinh trên máy).</summary>
    public sealed class OpponentInfo
    {
        public string PlayerId;

        /// <summary>Tên người chơi (biệt danh) + tên thú.</summary>
        public string Name;
        public string PetName;

        public PetDna Dna;
        public string DnaCode;
        public int Elo;
        public float Power;

        /// <summary>Thú đối thủ trông thế nào: giai đoạn, dạng (Teen = dạng đang nghiêng), phụ kiện đang đeo.</summary>
        public EvolutionStage Stage;
        public AdultForm Form;
        public AccessoryKind Accessory;

        /// <summary>Người chơi ảo: lối đánh + tay nghề 0..1 (BotConfig.Persona).</summary>
        public BotStyle Style;
        public float Skill;

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

    /// <summary>
    /// Số liệu của mạng giả lập: người chơi ảo vào hàng chờ cách nhau 1,5–6 giây, Elo ±180, lực chiến ±20% quanh mình
    /// => thường tìm được trận sau 5–20 giây như có nhiều người đang chơi (giải quyết chuyện ít người tìm trận PvP).
    /// </summary>
    public sealed class LocalNetworkConfig
    {
        public int MaxGhosts = 30;
        public double GhostEverySecondsMin = 1.5;
        public double GhostEverySecondsMax = 6.0;
        public int GhostEloSpread = 180;
        public float GhostPowerSpread = 0.20f;

        /// <summary>Không gặp lại người chơi ảo trong bấy nhiêu trận gần nhất.</summary>
        public int AvoidRecentOpponents = 12;
    }

    /// <summary>
    /// Mạng giả lập (C# thuần, chưa cần Photon): người chơi ảo (<see cref="BotPlayers"/>) sinh quanh Elo + lực chiến của mình
    /// lần lượt vào hàng chờ, ghép bằng <see cref="Matchmaker"/> thật (đúng luật ±100 / ±10%, 30 giây nới, 60 giây gợi ý Bot).
    /// Ghép trúng người chơi ảo thì báo MatchFound với IsGhost = true: màn đấu điều khiển đối thủ bằng BattleBot theo tính cách
    /// của người đó. Sau này có Photon: tìm người thật trước, quá lâu mới thả người chơi ảo vào.
    /// App gọi <see cref="Update"/> mỗi frame (thời gian lấy từ ITimeProvider).
    /// </summary>
    public sealed class LocalNetworkManager : INetworkManager
    {
        private readonly ITimeProvider _time;
        private readonly IRandom _rng;
        private readonly GameConfig _game;
        private readonly MatchmakingConfig _cfg;
        private readonly LocalNetworkConfig _net;
        private readonly Matchmaker _matchmaker;
        private readonly Dictionary<string, OpponentInfo> _ghostInfo = new Dictionary<string, OpponentInfo>();
        private readonly Queue<int> _recent = new Queue<int>();
        private MatchTicket _mine;
        private DateTime _searchStartUtc, _nextGhostUtc;
        private int _ghosts;
        private string _playerId;

        public LocalNetworkManager(ITimeProvider time, IRandom rng, GameConfig cfg, LocalNetworkConfig net = null)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _game = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _cfg = cfg.Matchmaking;
            _net = net ?? new LocalNetworkConfig();
            _matchmaker = new Matchmaker(_cfg);
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
                if (other == null) continue;   // 2 người chơi ảo ghép với nhau (giống người khác tìm được trận)
                OpponentInfo opponent = ToOpponent(other);
                ClearQueue();
                State = NetworkState.InMatch;
                var info = new MatchInfo
                {
                    MatchId = "local-" + _rng.NextUInt().ToString("x8"),
                    Seed = ((ulong)_rng.NextUInt() << 32) | _rng.NextUInt(),
                    LocalSide = 0,
                    Opponent = opponent,
                };
                MatchFound?.Invoke(info);
                return;
            }
        }

        /// <summary>Bỏ cả người chơi ảo còn chờ: lần tìm sau sinh người mới quanh Elo lúc đó.</summary>
        private void ClearQueue()
        {
            _matchmaker.Clear();
            _ghostInfo.Clear();
            _mine = null;
        }

        private double NextGap() =>
            _net.GhostEverySecondsMin + _rng.NextDouble() * Math.Max(0.0, _net.GhostEverySecondsMax - _net.GhostEverySecondsMin);

        private MatchTicket MakeGhost(DateTime queuedUtc)
        {
            _ghosts++;
            float powerK = 1f + (float)((_rng.NextDouble() * 2.0 - 1.0) * _net.GhostPowerSpread);
            int elo = Math.Max(0, _mine.Elo + _rng.Range(-_net.GhostEloSpread, _net.GhostEloSpread + 1));
            OpponentInfo info = BotPlayers.Create(PickIdentity(), elo, Math.Max(1f, _mine.Power * powerK), _mine.Stage, _game);
            var t = new MatchTicket
            {
                PlayerId = info.PlayerId + "#" + _ghosts,   // mỗi lần vào hàng là 1 vé riêng
                Name = info.Name,
                PetName = info.PetName,
                DnaCode = info.DnaCode,
                Elo = info.Elo,
                Power = info.Power,
                Stage = info.Stage,
                Stats = info.Stats,
                LearnedMask = info.LearnedMask,
                QueuedUtc = queuedUtc,
            };
            _ghostInfo[t.PlayerId] = info;
            return t;
        }

        /// <summary>Chọn người chơi ảo chưa gặp gần đây.</summary>
        private int PickIdentity()
        {
            int id = _rng.Next(BotPlayers.IdentityCount);
            for (int tries = 0; tries < 8 && _recent.Contains(id); tries++) id = _rng.Next(BotPlayers.IdentityCount);
            return id;
        }

        private OpponentInfo ToOpponent(MatchTicket t)
        {
            if (!_ghostInfo.TryGetValue(t.PlayerId, out OpponentInfo info)) return null;
            int id = int.Parse(info.PlayerId.Substring("bot-".Length));
            _recent.Enqueue(id);
            while (_recent.Count > Math.Max(0, _net.AvoidRecentOpponents)) _recent.Dequeue();
            return info;
        }
    }
}
