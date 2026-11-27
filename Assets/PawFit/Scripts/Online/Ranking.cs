using System;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    /// <summary>Luật rank thuần toán (GDD G10): Elo, bậc, mùa giải, reset mềm.</summary>
    public static class RankRules
    {
        /// <summary>Xác suất thắng kỳ vọng của mình trước đối thủ (công thức Elo).</summary>
        public static double Expected(int elo, int opponentElo) => 1.0 / (1.0 + Math.Pow(10.0, (opponentElo - elo) / 400.0));

        /// <summary>Elo mới. score: thắng 1, hòa 0.5, thua 0.</summary>
        public static int NewElo(int elo, int opponentElo, double score, int k) =>
            Math.Max(0, (int)Math.Round(elo + k * (score - Expected(elo, opponentElo)), MidpointRounding.AwayFromZero));

        /// <summary>Điểm của bên `side` trong kết quả trận (thắng 1 / hòa 0.5 / thua 0).</summary>
        public static double Score(BattleOutcome outcome, int side)
        {
            if (outcome == BattleOutcome.Draw) return 0.5;
            bool win = (outcome == BattleOutcome.Win0 && side == 0) || (outcome == BattleOutcome.Win1 && side == 1);
            return win ? 1.0 : 0.0;
        }

        public static RankTier TierOf(int elo, RankConfig c) =>
            elo >= c.MasterAt ? RankTier.Master
            : elo >= c.DiamondAt ? RankTier.Diamond
            : elo >= c.GoldAt ? RankTier.Gold
            : elo >= c.SilverAt ? RankTier.Silver
            : RankTier.Bronze;

        /// <summary>Mùa thứ mấy tại thời điểm `utc` (mùa 1 bắt đầu ở SeasonEpochUtc; trước mốc vẫn tính là mùa 1).</summary>
        public static int SeasonIndex(DateTime utc, RankConfig c)
        {
            double days = (utc - c.SeasonEpochUtc).TotalDays;
            return days < 0 ? 1 : 1 + (int)Math.Floor(days / Math.Max(1, c.SeasonDays));
        }

        public static DateTime SeasonEndUtc(int season, RankConfig c) => c.SeasonEpochUtc.AddDays((double)season * c.SeasonDays);

        /// <summary>Hết mùa: kéo Elo về gần mốc khởi đầu (1000 + (Elo - 1000) / 2).</summary>
        public static int SoftReset(int elo, RankConfig c) =>
            c.StartElo + (int)Math.Round((elo - c.StartElo) * c.SoftResetFactor, MidpointRounding.AwayFromZero);
    }

    /// <summary>Kết quả 1 trận PvP với rank (View hiện "+16 Elo · lên Silver!").</summary>
    public struct RankResult
    {
        public int EloBefore;
        public int EloAfter;
        public RankTier TierBefore;
        public RankTier TierAfter;
        public int CoinsWon;

        public int Delta => EloAfter - EloBefore;
        public bool Promoted => TierAfter > TierBefore;
        public bool Demoted => TierAfter < TierBefore;
    }

    /// <summary>
    /// Rank của người chơi (C# thuần, Phase 4.3): Elo K = 32, 5 bậc, mùa 30 ngày + reset mềm, thắng PvP +30 Coin,
    /// ghi bậc cao nhất của thú (lên bia mộ khi thú mất). Dữ liệu ở <see cref="PetService.Rank"/>.
    /// Sang mùa mới được tính lười: lần đầu đọc rank sau khi qua mốc mùa thì mới reset.
    /// </summary>
    public sealed class RankService
    {
        private readonly PetService _pets;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly GameConfig _cfg;
        private readonly ICoinWallet _wallet;
        private readonly ILog _log;

        public RankService(PetService pets, ITimeProvider time, EventBus bus, GameConfig cfg, ICoinWallet wallet = null, ILog log = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _wallet = wallet;
            _log = log ?? NullLog.Instance;
        }

        private RankConfig C => _cfg.Rank;

        /// <summary>Dữ liệu rank (đã tính sang mùa mới nếu tới lúc). null nếu chưa nạp file lưu.</summary>
        public RankData Current
        {
            get
            {
                RankData d = _pets.Rank;
                if (d != null) RollSeason(d);
                return d;
            }
        }

        public int Elo => Current?.Elo ?? C.StartElo;
        public RankTier Tier => RankRules.TierOf(Elo, C);
        public int Season => Current?.Season ?? RankRules.SeasonIndex(_time.UtcNow, C);
        public DateTime SeasonEndsUtc => RankRules.SeasonEndUtc(Season, C);

        /// <summary>Đấu PvP được không: cần thú sống, không ốm (GDD G3: ốm cấm Rank), không ngủ.</summary>
        public static bool CanPlayRanked(PetService pets, HealthService health, out string reason) =>
            BattleSession.CanStart(pets, health, out reason);

        /// <summary>Ghi kết quả 1 trận PvP (mình là bên `localSide`). Trả kết quả để View hiện.</summary>
        public RankResult ApplyMatch(int opponentElo, BattleOutcome outcome, int localSide = 0)
        {
            RankData d = Current ?? throw new InvalidOperationException("Chưa nạp file lưu");
            double score = RankRules.Score(outcome, localSide);
            var r = new RankResult { EloBefore = d.Elo, TierBefore = RankRules.TierOf(d.Elo, C) };
            d.Elo = RankRules.NewElo(d.Elo, opponentElo, score, C.K);
            r.EloAfter = d.Elo;
            r.TierAfter = RankRules.TierOf(d.Elo, C);
            if (score >= 1.0) d.Wins++;
            else if (score <= 0.0) d.Losses++;
            else d.Draws++;
            d.SeasonPeakElo = Math.Max(d.SeasonPeakElo, d.Elo);
            if (_pets.HasPet && r.TierAfter > _pets.Current.BestTier) _pets.Current.BestTier = r.TierAfter;   // lên bia mộ khi thú mất
            if (score >= 1.0 && _wallet != null && _cfg.Economy.PvpWinCoins > 0)
            {
                _wallet.Add(_cfg.Economy.PvpWinCoins, "thắng PvP");
                r.CoinsWon = _cfg.Economy.PvpWinCoins;
            }
            _pets.Save();
            _log.Info("[PawFit] PvP " + outcome + ": Elo " + r.EloBefore + " -> " + r.EloAfter + " (" + r.TierAfter + ")");
            _bus.Publish(new RankChanged(r.EloBefore, r.EloAfter, r.TierBefore, r.TierAfter, false));
            return r;
        }

        /// <summary>Qua mốc mùa: mỗi mùa đã qua reset mềm 1 lần. Đồng hồ chỉnh lùi thì giữ nguyên mùa đang ở.</summary>
        private void RollSeason(RankData d)
        {
            int now = RankRules.SeasonIndex(_time.UtcNow, C);
            if (d.Season <= 0)   // dữ liệu mới / file cũ: bắt đầu ở mùa hiện tại, không reset
            {
                d.Season = now;
                return;
            }
            if (d.Season >= now) return;
            int before = d.Elo;
            RankTier tierBefore = RankRules.TierOf(before, C);
            int steps = 0;
            while (d.Season < now && steps++ < 1000)
            {
                d.LastSeasonTier = RankRules.TierOf(d.Elo, C);
                d.Elo = RankRules.SoftReset(d.Elo, C);
                d.SeasonPeakElo = d.Elo;
                d.Season++;
            }
            _pets.Save();
            _log.Info("[PawFit] Sang mùa " + d.Season + ": Elo " + before + " -> " + d.Elo);
            _bus.Publish(new RankChanged(before, d.Elo, tierBefore, RankRules.TierOf(d.Elo, C), true));
        }
    }
}
