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

        /// <summary>
        /// Sang mùa mới nếu đã qua mốc: mỗi mùa đã qua reset mềm 1 lần. Trả true nếu vừa sang mùa.
        /// Dữ liệu mới (Season = 0) thì chỉ ghi mùa hiện tại, không reset. Đồng hồ chỉnh lùi thì giữ nguyên mùa đang ở.
        /// Máy chủ và máy dùng chung hàm này nên tính mùa giống hệt nhau.
        /// </summary>
        public static bool RollSeason(RankData d, DateTime utc, RankConfig c)
        {
            int now = SeasonIndex(utc, c);
            if (d.Season <= 0)
            {
                d.Season = now;
                return false;
            }
            if (d.Season >= now) return false;
            int steps = 0;
            while (d.Season < now && steps++ < 1000)
            {
                d.LastSeasonTier = TierOf(d.Elo, c);
                d.Elo = SoftReset(d.Elo, c);
                d.SeasonPeakElo = d.Elo;
                d.Season++;
            }
            return true;
        }

        /// <summary>Ghi 1 trận vào dữ liệu rank: Elo mới, thắng / thua / hòa, Elo cao nhất mùa. Trả Elo + bậc trước / sau.</summary>
        public static RankResult Apply(RankData d, int opponentElo, double score, RankConfig c)
        {
            var r = new RankResult { EloBefore = d.Elo, TierBefore = TierOf(d.Elo, c) };
            d.Elo = NewElo(d.Elo, opponentElo, score, c.K);
            r.EloAfter = d.Elo;
            r.TierAfter = TierOf(d.Elo, c);
            if (score >= 1.0) d.Wins++;
            else if (score <= 0.0) d.Losses++;
            else d.Draws++;
            d.SeasonPeakElo = Math.Max(d.SeasonPeakElo, d.Elo);
            return r;
        }

        /// <summary>Chép dữ liệu rank (bản máy chủ gửi về) sang bản trên máy.</summary>
        public static void Copy(RankData from, RankData to)
        {
            to.Elo = from.Elo;
            to.Season = from.Season;
            to.Wins = from.Wins;
            to.Losses = from.Losses;
            to.Draws = from.Draws;
            to.SeasonPeakElo = from.SeasonPeakElo;
            to.LastSeasonTier = from.LastSeasonTier;
        }
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
    /// Có máy chủ riêng: máy chủ tính Elo bằng đúng <see cref="RankRules"/>, máy chỉ chép về (<see cref="Adopt"/>, <see cref="ApplyServerMatch"/>).
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

        /// <summary>
        /// Ghi kết quả 1 trận PvP tính trên máy (mạng giả lập, chưa có máy chủ; mình là bên `localSide`). Trả kết quả để View hiện.
        /// Có máy chủ thì dùng <see cref="ApplyServerMatch"/>: máy chủ mới là nơi tính Elo.
        /// </summary>
        public RankResult ApplyMatch(int opponentElo, BattleOutcome outcome, int localSide = 0)
        {
            RankData d = Current ?? throw new InvalidOperationException("Chưa nạp file lưu");
            double score = RankRules.Score(outcome, localSide);
            RankResult r = RankRules.Apply(d, opponentElo, score, C);
            if (score >= 1.0 && _wallet != null && _cfg.Economy.PvpWinCoins > 0)
            {
                _wallet.Add(_cfg.Economy.PvpWinCoins, "thắng PvP");
                r.CoinsWon = _cfg.Economy.PvpWinCoins;
            }
            Finish(r, outcome.ToString());
            return r;
        }

        /// <summary>
        /// Kết quả trận PvP do máy chủ tính (Phase 4.2 / 4.3): chép rank mới của máy chủ về máy, cộng Coin máy chủ cho,
        /// ghi bậc cao nhất của thú. `eloBefore` = Elo trước trận theo máy chủ.
        /// </summary>
        public RankResult ApplyServerMatch(RankData server, int eloBefore, int coins)
        {
            RankData d = _pets.Rank ?? throw new InvalidOperationException("Chưa nạp file lưu");
            if (server == null) throw new ArgumentNullException(nameof(server));
            RankRules.Copy(server, d);
            var r = new RankResult
            {
                EloBefore = eloBefore,
                TierBefore = RankRules.TierOf(eloBefore, C),
                EloAfter = d.Elo,
                TierAfter = RankRules.TierOf(d.Elo, C),
            };
            if (coins > 0 && _wallet != null)
            {
                _wallet.Add(coins, "thắng PvP");
                r.CoinsWon = coins;
            }
            Finish(r, "máy chủ");
            return r;
        }

        /// <summary>Lúc đăng nhập: rank trên máy chủ là bản thật, chép về máy (đổi thì báo RankChanged để View cập nhật).</summary>
        public void Adopt(RankData server)
        {
            RankData d = _pets.Rank;
            if (d == null || server == null) return;
            int before = d.Elo;
            RankTier tierBefore = RankRules.TierOf(before, C);
            bool newSeason = d.Season > 0 && server.Season > d.Season;
            bool same = d.Elo == server.Elo && d.Season == server.Season && d.Wins == server.Wins && d.Losses == server.Losses &&
                        d.Draws == server.Draws && d.SeasonPeakElo == server.SeasonPeakElo && d.LastSeasonTier == server.LastSeasonTier;
            if (same) return;
            RankRules.Copy(server, d);
            _pets.Save();
            _bus.Publish(new RankChanged(before, d.Elo, tierBefore, RankRules.TierOf(d.Elo, C), newSeason));
        }

        private void Finish(RankResult r, string how)
        {
            if (_pets.HasPet && r.TierAfter > _pets.Current.BestTier) _pets.Current.BestTier = r.TierAfter;   // lên bia mộ khi thú mất
            _pets.Save();
            _log.Info("[PawFit] PvP (" + how + "): Elo " + r.EloBefore + " -> " + r.EloAfter + " (" + r.TierAfter + ")");
            _bus.Publish(new RankChanged(r.EloBefore, r.EloAfter, r.TierBefore, r.TierAfter, false));
        }

        /// <summary>Qua mốc mùa: mỗi mùa đã qua reset mềm 1 lần (luật chung ở <see cref="RankRules.RollSeason"/>).</summary>
        private void RollSeason(RankData d)
        {
            int before = d.Elo;
            RankTier tierBefore = RankRules.TierOf(before, C);
            if (!RankRules.RollSeason(d, _time.UtcNow, C)) return;
            _pets.Save();
            _log.Info("[PawFit] Sang mùa " + d.Season + ": Elo " + before + " -> " + d.Elo);
            _bus.Publish(new RankChanged(before, d.Elo, tierBefore, RankRules.TierOf(d.Elo, C), true));
        }
    }
}
