using System;
using System.Collections.Generic;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    /// <summary>Offline → (Connecting) → Online → Searching → InMatch → Online. Connecting chỉ có ở mạng thật.</summary>
    public enum NetworkState { Offline = 0, Online = 1, Searching = 2, InMatch = 3, Connecting = 4 }

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

        /// <summary>
        /// true = người chơi ảo của bản giả lập: máy tự điều khiển đối thủ bằng BattleBot.
        /// Trận qua máy chủ luôn là false (người chơi ảo chạy trên máy chủ, máy không biết đối thủ là ai).
        /// </summary>
        public bool IsGhost;
    }

    /// <summary>1 trận vừa ghép được.</summary>
    public sealed class MatchInfo
    {
        public string MatchId;
        public OpponentInfo Opponent;

        /// <summary>Seed chung của trận: 2 máy + máy chủ chạy BattleSimulation giống hệt nhau (bước cố định có seed).</summary>
        public ulong Seed;

        /// <summary>Mình là bên 0 hay bên 1 trong trận ở máy chủ (trên máy này mình luôn đứng bên trái = bên 0).</summary>
        public int LocalSide;

        /// <summary>
        /// Trận qua máy chủ riêng: nhịp trận (nét vuốt đã chốt của 2 bên, kết quả, Elo mới). null = mạng giả lập,
        /// đối thủ là người chơi ảo chạy ngay trên máy (BattleBot).
        /// </summary>
        public NetMatchFeed Feed;
    }

    /// <summary>
    /// Cổng mạng cho PvP (brief: NetworkManager phải là interface). Có 2 bản, chỉ App chọn:
    ///  * <see cref="LocalNetworkManager"/>: giả lập trên máy (chưa cấu hình máy chủ), đối thủ là người chơi ảo.
    ///  * <see cref="ServerNetworkManager"/>: máy chủ riêng qua WebSocket. Máy chủ ghép người thật trước, vắng người thì
    ///    thả người chơi ảo (chạy trên máy chủ), tự chạy trận và tính Elo.
    /// Sau này đông người có thể thay bằng Photon mà không đổi màn tìm trận / màn đấu.
    /// </summary>
    public interface INetworkManager
    {
        NetworkState State { get; }

        /// <summary>Nối máy chủ (bản giả lập: nối ngay). `done(true)` = đã Online. `done` có thể được gọi ở lần Update sau.</summary>
        void Connect(string playerId, Action<bool> done);

        void Disconnect();

        /// <summary>Xếp hàng tìm trận (cần State == Online). Tìm thấy thì báo <see cref="MatchFound"/>.</summary>
        void FindMatch(MatchTicket ticket);

        void CancelFindMatch();

        /// <summary>Đã tìm bao lâu (giây) + trạng thái: đang tìm / đã nới khoảng / gợi ý đấu Bot.</summary>
        double SearchSeconds { get; }
        MatchSearchStatus SearchStatus { get; }

        /// <summary>Rời trận (hết trận, bỏ cuộc, hủy trước khi vào). Trận qua máy chủ mà chưa hết = bỏ cuộc (tính thua).</summary>
        void LeaveMatch();

        /// <summary>Cho thời gian trôi + đọc tin nhắn máy chủ. App gọi mỗi frame trên luồng chính (mọi event chạy ở đây).</summary>
        void Update();

        event Action<MatchInfo> MatchFound;
    }

    /// <summary>
    /// Số liệu người chơi ảo: vào hàng chờ cách nhau 1,5–6 giây, Elo ±180, lực chiến ±20% quanh mình
    /// => thường tìm được trận sau 5–20 giây như có nhiều người đang chơi (giải quyết chuyện ít người tìm trận PvP).
    /// Máy chủ dùng cùng số liệu này khi vắng người thật.
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
    /// Mạng giả lập (C# thuần, dùng khi chưa cấu hình máy chủ): người chơi ảo (<see cref="GhostSearch"/>) sinh quanh Elo +
    /// lực chiến của mình lần lượt vào hàng chờ, ghép bằng <see cref="Matchmaker"/> thật (±100 / ±10%, 30 giây nới,
    /// 60 giây gợi ý Bot). Ghép trúng người chơi ảo thì báo MatchFound với IsGhost = true: màn đấu điều khiển đối thủ bằng
    /// BattleBot theo tính cách của người đó, Elo tính trên máy. App gọi <see cref="Update"/> mỗi frame.
    /// </summary>
    public sealed class LocalNetworkManager : INetworkManager
    {
        private readonly ITimeProvider _time;
        private readonly IRandom _rng;
        private readonly MatchmakingConfig _cfg;
        private readonly GhostSearch _ghosts;
        private DateTime _searchStartUtc;
        private string _playerId;

        public LocalNetworkManager(ITimeProvider time, IRandom rng, GameConfig cfg, LocalNetworkConfig net = null)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            _cfg = cfg.Matchmaking;
            _ghosts = new GhostSearch(_rng, cfg, net);
        }

        public NetworkState State { get; private set; } = NetworkState.Offline;

        public double SearchSeconds => State == NetworkState.Searching ? Math.Max(0.0, (_time.UtcNow - _searchStartUtc).TotalSeconds) : 0.0;
        public MatchSearchStatus SearchStatus => MatchmakingRules.Status(SearchSeconds, _cfg);

        /// <summary>Vé của mình đang tìm (để View lấy Elo / lực chiến hiện lên).</summary>
        public MatchTicket CurrentTicket => _ghosts.Mine;

        public event Action<MatchInfo> MatchFound;

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
            if (string.IsNullOrEmpty(ticket.PlayerId)) ticket.PlayerId = _playerId;
            ticket.QueuedUtc = now;
            _searchStartUtc = now;
            _ghosts.Start(ticket, now);
            State = NetworkState.Searching;
        }

        public void CancelFindMatch()
        {
            if (State != NetworkState.Searching) return;
            _ghosts.Stop();
            State = NetworkState.Online;
        }

        public void LeaveMatch()
        {
            if (State == NetworkState.InMatch) State = NetworkState.Online;
        }

        /// <summary>Cho thời gian trôi: người chơi ảo tới hàng chờ, ghép trận.</summary>
        public void Update()
        {
            if (State != NetworkState.Searching) return;
            OpponentInfo opponent = _ghosts.Update(_time.UtcNow);
            if (opponent == null) return;
            State = NetworkState.InMatch;
            var info = new MatchInfo
            {
                MatchId = "local-" + _rng.NextUInt().ToString("x8"),
                Seed = ((ulong)_rng.NextUInt() << 32) | _rng.NextUInt(),
                LocalSide = 0,
                Opponent = opponent,
            };
            MatchFound?.Invoke(info);
        }
    }
}
