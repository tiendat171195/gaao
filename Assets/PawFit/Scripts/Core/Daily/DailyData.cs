using System.Collections.Generic;

namespace PawFit.Core
{
    /// <summary>
    /// Loại nhiệm vụ ngày (Phase 7.3). Chăm: Feed / Bathe / Cuddle / Workout / Play / Harvest (vườn rau, Phase 10.3) ·
    /// Đấu: WinAdventure / AdventureStar / WinBattle / PlayPvp / Hunt (hạ N quái rừng, Phase 10.3) · Khám phá: Dig.
    /// </summary>
    public enum QuestKind
    {
        Feed = 0, Bathe = 1, Cuddle = 2, Workout = 3, WinAdventure = 4, AdventureStar = 5, WinBattle = 6, PlayPvp = 7, Dig = 8, Play = 9,
        Hunt = 10, Harvest = 11,
    }

    /// <summary>1 nhiệm vụ của hôm nay: cần làm `Target` lần, đã làm `Progress` lần, thưởng `Coins` khi nhận ở hòm thư.</summary>
    public sealed class QuestState
    {
        public QuestKind Kind;
        public int Target;
        public int Progress;
        public int Coins;
        public bool Claimed;

        public bool Done => Progress >= Target;
    }

    /// <summary>
    /// Nhiệm vụ ngày + quà đăng nhập theo chuỗi ngày + điểm đào kho báu (Phase 7.3, trong file lưu game; thú mất vẫn giữ).
    /// "Ngày" tính theo giờ trên máy (yyyy-MM-dd), giống lượt quảng cáo.
    /// </summary>
    public sealed class DailyData
    {
        /// <summary>Ngày của bộ nhiệm vụ + điểm đào hiện tại.</summary>
        public string Day;

        public List<QuestState> Quests = new List<QuestState>();

        /// <summary>Đã nhận thưởng thêm khi xong cả 3 nhiệm vụ hôm nay chưa.</summary>
        public bool BonusClaimed;

        /// <summary>Ngày mở game gần nhất + chuỗi ngày mở game liên tiếp (1..) + quà hôm nay đã nhận chưa.</summary>
        public string LastLoginDay;
        public int Streak;
        public bool GiftClaimed;

        /// <summary>Điểm đào hôm nay đã đào (chỉ số trong danh sách điểm đào của hôm nay).</summary>
        public List<int> Dug = new List<int>();

        /// <summary>Số ngẫu nhiên riêng của người chơi: mỗi người 1 bộ nhiệm vụ / điểm đào khác nhau mỗi ngày.</summary>
        public uint Salt;

        /// <summary>Ngày bé đã ăn túi bánh của cối xay gió (Phase 8.4). Khác hôm nay = hôm nay cối xay gió còn bánh.</summary>
        public string WindmillDay;
    }

    /// <summary>Vừa hết 1 trận. Mode = số của BattleMode (0 Huấn luyện, 1 Đấu Bot, 2 PvP, 3 Thám hiểm). Nhiệm vụ ngày nghe sự kiện này.</summary>
    public readonly struct BattleEnded
    {
        public readonly int Mode;
        public readonly bool Won;

        public BattleEnded(int mode, bool won)
        {
            Mode = mode;
            Won = won;
        }
    }
}
