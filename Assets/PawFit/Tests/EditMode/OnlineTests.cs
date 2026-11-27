using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Online;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Phase 4 phần không cần tài khoản: rank Elo + bậc + mùa giải, luật ghép trận, mạng giả lập.</summary>
    public class OnlineTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);

        private ManualTimeProvider _time;
        private EventBus _bus;
        private GameConfig _cfg;
        private InMemorySaveService _save;
        private PetService _pets;
        private EconomyService _eco;
        private RankService _rank;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            Boot();
        }

        private void Boot()
        {
            _pets = new PetService(_time, new SeededRandom(9), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _eco = new EconomyService(_pets, new MockAdService(), _time, _bus, _cfg);
            _rank = new RankService(_pets, _time, _bus, _cfg, _eco);
        }

        private static MatchTicket Ticket(string id, int elo, float power, DateTime at) =>
            new MatchTicket { PlayerId = id, Name = id, Elo = elo, Power = power, QueuedUtc = at, Stats = new FighterStats(100, 10, 10, 10, 5) };

        // ------------------------------------------------------------------
        // Rank
        // ------------------------------------------------------------------

        [Test]
        public void Elo_K32_EqualOpponents()
        {
            Assert.AreEqual(1016, RankRules.NewElo(1000, 1000, 1.0, 32), "thắng người ngang cơ +16");
            Assert.AreEqual(984, RankRules.NewElo(1000, 1000, 0.0, 32), "thua -16");
            Assert.AreEqual(1000, RankRules.NewElo(1000, 1000, 0.5, 32), "hòa không đổi");
            Assert.Greater(RankRules.NewElo(1000, 1200, 1.0, 32) - 1000, 16, "thắng người mạnh hơn được nhiều hơn");
            Assert.Less(RankRules.NewElo(1200, 1000, 1.0, 32) - 1200, 16, "thắng người yếu hơn được ít hơn");
            Assert.AreEqual(1.0, RankRules.Score(BattleOutcome.Win0, 0));
            Assert.AreEqual(0.0, RankRules.Score(BattleOutcome.Win0, 1));
            Assert.AreEqual(0.5, RankRules.Score(BattleOutcome.Draw, 1));
        }

        [Test]
        public void Tiers_FollowGddThresholds()
        {
            RankConfig c = _cfg.Rank;
            Assert.AreEqual(RankTier.Bronze, RankRules.TierOf(1099, c));
            Assert.AreEqual(RankTier.Silver, RankRules.TierOf(1100, c));
            Assert.AreEqual(RankTier.Silver, RankRules.TierOf(1299, c));
            Assert.AreEqual(RankTier.Gold, RankRules.TierOf(1300, c));
            Assert.AreEqual(RankTier.Diamond, RankRules.TierOf(1500, c));
            Assert.AreEqual(RankTier.Master, RankRules.TierOf(1700, c));
            Assert.AreEqual(RankTier.Bronze, RankRules.TierOf(0, c));
        }

        [Test]
        public void NewPlayer_Starts1000Bronze_InCurrentSeason()
        {
            Assert.AreEqual(1000, _rank.Elo);
            Assert.AreEqual(RankTier.Bronze, _rank.Tier);
            Assert.AreEqual(RankRules.SeasonIndex(T0, _cfg.Rank), _rank.Season);
            Assert.Greater(_rank.SeasonEndsUtc, T0);
            Assert.LessOrEqual((_rank.SeasonEndsUtc - T0).TotalDays, 30.0);
        }

        [Test]
        public void Win_GivesEloCoinsAndBestTierOnPet()
        {
            _pets.Rank.Elo = 1090;
            RankResult r = _rank.ApplyMatch(1090, BattleOutcome.Win0);
            Assert.AreEqual(16, r.Delta);
            Assert.IsTrue(r.Promoted, "1090 -> 1106: lên Silver");
            Assert.AreEqual(RankTier.Silver, r.TierAfter);
            Assert.AreEqual(30, r.CoinsWon);
            Assert.AreEqual(230, _eco.Coins, "thắng PvP +30 Coin");
            Assert.AreEqual(RankTier.Silver, _pets.Current.BestTier);
            Assert.AreEqual(1, _pets.Rank.Wins);

            RankResult lose = _rank.ApplyMatch(1106, BattleOutcome.Win1);
            Assert.AreEqual(-16, lose.Delta);
            Assert.IsTrue(lose.Demoted);
            Assert.AreEqual(0, lose.CoinsWon, "thua không có Coin");
            Assert.AreEqual(RankTier.Silver, _pets.Current.BestTier, "bậc cao nhất của thú không tụt");
            Assert.AreEqual(1, _pets.Rank.Losses);
        }

        [Test]
        public void Memorial_ShowsBestRank_RankStaysWithPlayer()
        {
            _pets.Rank.Elo = 1290;
            _rank.ApplyMatch(1290, BattleOutcome.Win0);   // lên Gold
            int elo = _rank.Elo;
            _pets.SetNeglectForDebug(_cfg.Health.DeathAfterHours + 1);
            Assert.AreEqual("Gold", _pets.Memorials[_pets.Memorials.Count - 1].BestRank, "bia mộ ghi rank cao nhất");
            _pets.MakeEggReadyForDebug();
            _pets.HatchEgg("Mới");
            Assert.AreEqual(RankTier.None, _pets.Current.BestTier, "thú mới chưa đấu");
            Assert.AreEqual(elo, _rank.Elo, "Elo là của người chơi");
            Boot();
            Assert.AreEqual(elo, _rank.Elo, "lưu qua mở lại app");
        }

        [Test]
        public void Season_SoftResetsOncePerPassedSeason()
        {
            _pets.Rank.Elo = 1400;
            Assert.AreEqual(1400, _rank.Elo);
            int season = _rank.Season;
            var changes = new List<RankChanged>();
            _bus.Subscribe<RankChanged>(changes.Add);
            _time.UtcNow = RankRules.SeasonEndUtc(season, _cfg.Rank).AddHours(1);
            Assert.AreEqual(1200, _rank.Elo, "1000 + (1400 - 1000) / 2");
            Assert.AreEqual(season + 1, _rank.Season);
            Assert.AreEqual(RankTier.Gold, _pets.Rank.LastSeasonTier);
            Assert.AreEqual(1, changes.Count);
            Assert.IsTrue(changes[0].NewSeason);

            _time.UtcNow = RankRules.SeasonEndUtc(season + 2, _cfg.Rank).AddHours(1);   // vắng 2 mùa liền
            Assert.AreEqual(1050, _rank.Elo, "1200 -> 1100 -> 1050");
            Assert.AreEqual(season + 3, _rank.Season);

            _time.UtcNow = T0;   // chỉnh đồng hồ lùi: không đổi gì
            Assert.AreEqual(1050, _rank.Elo);
            Assert.AreEqual(season + 3, _rank.Season);
        }

        [Test]
        public void OldSave_Version4_GetsStartElo()
        {
            _pets.Save();
            JObject file = JObject.Parse(_save.RawJson(PetService.SaveKey));
            file.Remove("Rank");
            file["Version"] = 4;
            _save.Save(PetService.SaveKey, file);
            Boot();
            Assert.AreEqual(1000, _rank.Elo);
            _rank.ApplyMatch(1000, BattleOutcome.Draw);
            StringAssert.Contains("\"Version\": " + GameSave.CurrentVersion, _save.RawJson(PetService.SaveKey));
            StringAssert.Contains("\"Draws\": 1", _save.RawJson(PetService.SaveKey));
        }

        [Test]
        public void SickPet_CannotPlayRanked()
        {
            var health = new HealthService(_pets, _time, _cfg);
            Assert.IsTrue(RankService.CanPlayRanked(_pets, health, out _));
            _pets.SetNeglectForDebug(_cfg.Health.SickAfterHours + 1);
            Assert.IsFalse(RankService.CanPlayRanked(_pets, health, out string reason), "GDD G3: ốm cấm Rank");
            StringAssert.Contains("ốm", reason);
        }

        // ------------------------------------------------------------------
        // Ghép trận
        // ------------------------------------------------------------------

        [Test]
        public void MatchWindow_WidensAt30s_SuggestsBotAt60s()
        {
            MatchmakingConfig c = _cfg.Matchmaking;
            Assert.AreEqual(100, MatchmakingRules.EloWindow(29.9, c));
            Assert.AreEqual(0.10f, MatchmakingRules.PowerWindow(0, c), 1e-6f);
            Assert.AreEqual(200, MatchmakingRules.EloWindow(30, c));
            Assert.AreEqual(0.20f, MatchmakingRules.PowerWindow(45, c), 1e-6f);
            Assert.AreEqual(MatchSearchStatus.Searching, MatchmakingRules.Status(10, c));
            Assert.AreEqual(MatchSearchStatus.Widened, MatchmakingRules.Status(30, c));
            Assert.AreEqual(MatchSearchStatus.SuggestBot, MatchmakingRules.Status(60, c));
        }

        [Test]
        public void CanMatch_EloAndPowerWindows()
        {
            MatchmakingConfig c = _cfg.Matchmaking;
            MatchTicket me = Ticket("me", 1000, 500f, T0);
            Assert.IsTrue(MatchmakingRules.CanMatch(me, Ticket("a", 1100, 540f, T0), T0, c), "±100 Elo, lực chiến chênh 7%");
            Assert.IsFalse(MatchmakingRules.CanMatch(me, Ticket("b", 1101, 500f, T0), T0, c), "chênh 101 Elo");
            Assert.IsFalse(MatchmakingRules.CanMatch(me, Ticket("c", 1000, 600f, T0), T0, c), "lực chiến chênh 16.7%");
            Assert.IsTrue(MatchmakingRules.CanMatch(me, Ticket("b", 1101, 500f, T0), T0.AddSeconds(30), c), "chờ 30 giây thì nới ±200");
            Assert.IsTrue(MatchmakingRules.CanMatch(me, Ticket("c", 1000, 600f, T0), T0.AddSeconds(30), c), "và ±20% lực chiến");
            Assert.IsFalse(MatchmakingRules.CanMatch(me, Ticket("me", 1000, 500f, T0), T0, c), "không tự ghép với mình");
        }

        [Test]
        public void Matchmaker_OldestFirst_ClosestElo()
        {
            var mm = new Matchmaker(_cfg.Matchmaking);
            mm.Enqueue(Ticket("old", 1000, 500f, T0));
            mm.Enqueue(Ticket("far", 1090, 500f, T0.AddSeconds(5)));
            mm.Enqueue(Ticket("near", 1020, 500f, T0.AddSeconds(8)));
            List<MatchPair> pairs = mm.Tick(T0.AddSeconds(10));
            Assert.AreEqual(1, pairs.Count);
            Assert.AreEqual("old", pairs[0].A.PlayerId, "người chờ lâu nhất được ghép trước");
            Assert.AreEqual("near", pairs[0].B.PlayerId, "với người gần Elo nhất");
            Assert.AreEqual(1, mm.Count);
            Assert.IsTrue(mm.Cancel("far"));
            Assert.AreEqual(0, mm.Count);
        }

        // ------------------------------------------------------------------
        // Mạng giả lập
        // ------------------------------------------------------------------

        private MatchTicket MyTicket() => new MatchTicket
        {
            Name = "Kem", Elo = 1000, Power = 500f, Stats = new FighterStats(200, 20, 20, 20, 5), LearnedMask = 1,
        };

        [Test]
        public void LocalNetwork_FindsGhostOpponent_WithinRules()
        {
            var net = new LocalNetworkManager(_time, new SeededRandom(4), _cfg.Matchmaking);
            bool connected = false;
            net.Connect("me", ok => connected = ok);
            Assert.IsTrue(connected);
            Assert.AreEqual(NetworkState.Online, net.State);
            MatchInfo found = null;
            net.MatchFound += m => found = m;
            net.FindMatch(MyTicket());
            Assert.AreEqual(NetworkState.Searching, net.State);
            double waited = 0;
            for (int i = 0; i < 300 && found == null; i++)
            {
                _time.Advance(TimeSpan.FromSeconds(1));
                waited = net.SearchSeconds;
                net.Update();
            }
            Assert.IsNotNull(found, "trong 5 phút phải tìm được đối thủ giả");
            Assert.AreEqual(NetworkState.InMatch, net.State);
            Assert.IsTrue(found.Opponent.IsGhost);
            int window = MatchmakingRules.EloWindow(waited, _cfg.Matchmaking);
            Assert.LessOrEqual(Math.Abs(found.Opponent.Elo - 1000), window, "đúng luật Elo lúc ghép");
            Assert.Greater(found.Opponent.Stats.Hp, 0f);
            Assert.AreEqual(0, found.LocalSide);
            StringAssert.StartsWith("PF-", found.Opponent.DnaCode);
            net.LeaveMatch();
            Assert.AreEqual(NetworkState.Online, net.State);
        }

        [Test]
        public void LocalNetwork_NoOpponents_StatusWidensThenSuggestsBot_CancelStops()
        {
            var net = new LocalNetworkManager(_time, new SeededRandom(4), _cfg.Matchmaking, new LocalNetworkConfig { MaxGhosts = 0 });
            net.Connect("me", null);
            bool found = false;
            net.MatchFound += _ => found = true;
            net.FindMatch(MyTicket());
            Assert.AreEqual(MatchSearchStatus.Searching, net.SearchStatus);
            _time.Advance(TimeSpan.FromSeconds(31));
            net.Update();
            Assert.AreEqual(MatchSearchStatus.Widened, net.SearchStatus);
            _time.Advance(TimeSpan.FromSeconds(30));
            net.Update();
            Assert.AreEqual(MatchSearchStatus.SuggestBot, net.SearchStatus, "60 giây: gợi ý đấu Bot");
            Assert.AreEqual(NetworkState.Searching, net.State, "vẫn tìm tiếp");
            net.CancelFindMatch();
            Assert.AreEqual(NetworkState.Online, net.State);
            Assert.AreEqual(0.0, net.SearchSeconds);
            Assert.IsFalse(found);
            Assert.Throws<InvalidOperationException>(() => new LocalNetworkManager(_time, new SeededRandom(1), _cfg.Matchmaking)
                                                         .FindMatch(MyTicket()), "chưa kết nối thì không tìm được");
        }
    }
}
