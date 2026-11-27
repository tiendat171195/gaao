namespace PawFit.Core
{
    /// <summary>Bậc rank theo Elo (GDD G10). None = chưa từng đấu PvP.</summary>
    public enum RankTier { None = 0, Bronze = 1, Silver = 2, Gold = 3, Diamond = 4, Master = 5 }

    /// <summary>
    /// Elo + mùa giải của người chơi (Phase 4.3, trong file lưu game; thú mất vẫn giữ).
    /// Có máy chủ riêng: máy chủ quyết định Elo thật (cùng RankRules), bản trên máy chép từ máy chủ để hiện; không có máy chủ thì tính trên máy.
    /// </summary>
    public sealed class RankData
    {
        public int Elo = 1000;

        /// <summary>Đang ở mùa thứ mấy (mùa 1 bắt đầu ở RankConfig.SeasonEpochUtc). 0 = chưa tính.</summary>
        public int Season;

        public int Wins;
        public int Losses;
        public int Draws;

        /// <summary>Elo cao nhất trong mùa hiện tại.</summary>
        public int SeasonPeakElo = 1000;

        /// <summary>Bậc lúc hết mùa trước (sau này gửi thư thưởng mùa về hòm thư).</summary>
        public RankTier LastSeasonTier;
    }

    /// <summary>Elo / bậc vừa đổi sau 1 trận PvP hoặc lúc sang mùa mới.</summary>
    public readonly struct RankChanged
    {
        public readonly int EloBefore;
        public readonly int EloAfter;
        public readonly RankTier TierBefore;
        public readonly RankTier TierAfter;
        public readonly bool NewSeason;

        public RankChanged(int eloBefore, int eloAfter, RankTier tierBefore, RankTier tierAfter, bool newSeason)
        {
            EloBefore = eloBefore;
            EloAfter = eloAfter;
            TierBefore = tierBefore;
            TierAfter = tierAfter;
            NewSeason = newSeason;
        }
    }
}
