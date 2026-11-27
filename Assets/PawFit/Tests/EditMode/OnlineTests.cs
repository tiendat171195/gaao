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

        private MatchTicket MyTicket(EvolutionStage stage = EvolutionStage.Baby) => new MatchTicket
        {
            Name = "Kem", Elo = 1000, Power = 500f, Stats = new FighterStats(200, 20, 20, 20, 5), LearnedMask = 1, Stage = stage,
        };

        [Test]
        public void LocalNetwork_FindsGhostOpponent_WithinRules()
        {
            var net = new LocalNetworkManager(_time, new SeededRandom(4), _cfg);
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
            Assert.IsNotNull(found.Opponent.Dna, "người chơi ảo có thú thật (DNA) để dựng hình");
            Assert.IsFalse(string.IsNullOrEmpty(found.Opponent.PetName));
            net.LeaveMatch();
            Assert.AreEqual(NetworkState.Online, net.State);
        }

        [Test]
        public void LocalNetwork_NoOpponents_StatusWidensThenSuggestsBot_CancelStops()
        {
            var net = new LocalNetworkManager(_time, new SeededRandom(4), _cfg, new LocalNetworkConfig { MaxGhosts = 0 });
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
            Assert.Throws<InvalidOperationException>(() => new LocalNetworkManager(_time, new SeededRandom(1), _cfg)
                                                         .FindMatch(MyTicket()), "chưa kết nối thì không tìm được");
        }

        [Test]
        public void LocalNetwork_ManyBotPlayers_UsuallyFoundWithin20Seconds()
        {
            var waits = new List<double>();
            for (ulong seed = 1; seed <= 30; seed++)
            {
                var net = new LocalNetworkManager(_time, new SeededRandom(seed), _cfg);
                net.Connect("me", null);
                MatchInfo found = null;
                net.MatchFound += m => found = m;
                net.FindMatch(MyTicket(EvolutionStage.Adult));
                double waited = 0;
                for (int i = 0; i < 1200 && found == null; i++)
                {
                    _time.Advance(TimeSpan.FromSeconds(0.25));
                    waited = net.SearchSeconds;
                    net.Update();
                }
                Assert.IsNotNull(found, "seed " + seed + ": phải tìm được trận");
                Assert.AreEqual(EvolutionStage.Adult, found.Opponent.Stage, "đối thủ cùng giai đoạn tiến hóa");
                Assert.AreNotEqual(AdultForm.None, found.Opponent.Form, "Adult thì có dạng");
                waits.Add(waited);
            }
            waits.Sort();
            TestContext.Progress.WriteLine("Chờ tìm trận (giây): ngắn nhất " + waits[0].ToString("0.0") + " · giữa " +
                                           waits[waits.Count / 2].ToString("0.0") + " · lâu nhất " + waits[waits.Count - 1].ToString("0.0"));
            Assert.Less(waits[waits.Count / 2], 20.0, "nửa số lần tìm thấy trong vòng 20 giây (giống có nhiều người chơi)");
            Assert.Less(waits[waits.Count - 1], 60.0, "hầu như không phải chờ tới mức gợi ý đấu Bot");
        }

        // ------------------------------------------------------------------
        // Người chơi ảo
        // ------------------------------------------------------------------

        [Test]
        public void BotPlayer_SameIdentity_SamePerson_StatsMatchPower()
        {
            OpponentInfo a = BotPlayers.Create(37, 1200, 800f, EvolutionStage.Teen, _cfg);
            OpponentInfo b = BotPlayers.Create(37, 1350, 900f, EvolutionStage.Teen, _cfg);
            Assert.AreEqual(a.Name, b.Name, "cùng số hiệu = cùng biệt danh");
            Assert.AreEqual(a.PetName, b.PetName);
            Assert.AreEqual(a.DnaCode, b.DnaCode, "cùng thú");
            Assert.AreEqual(a.Style, b.Style, "cùng lối đánh");
            Assert.AreEqual(a.Accessory, b.Accessory);
            Assert.AreEqual(1350, b.Elo);
            PetStats asPet = new PetStats { Hp = b.Stats.Hp, Atk = b.Stats.Atk, Def = b.Stats.Def, Spd = b.Stats.Spd, Crit = b.Stats.Crit };
            Assert.AreEqual(900f, StatsRules.Power(asPet, _cfg.Stats), 900f * 0.01f, "chỉ số khớp lực chiến");
            var names = new HashSet<string>();
            for (int i = 0; i < 60; i++) names.Add(BotPlayers.Create(i, 1000, 500f, EvolutionStage.Baby, _cfg).Name);
            Assert.Greater(names.Count, 30, "nhiều người chơi ảo khác nhau");
        }

        [Test]
        public void BotPlayer_SkillRisesWithElo_UltimateKnowsAllSkills()
        {
            float low = 0f, high = 0f;
            for (int i = 0; i < 40; i++)
            {
                low += BotPlayers.Create(i, 900, 500f, EvolutionStage.Baby, _cfg).Skill;
                high += BotPlayers.Create(i, 1700, 500f, EvolutionStage.Baby, _cfg).Skill;
            }
            Assert.Greater(high, low + 40 * 0.5f, "Elo cao đánh giỏi hơn hẳn");
            OpponentInfo ult = BotPlayers.Create(5, 1500, 2000f, EvolutionStage.Ultimate, _cfg);
            Assert.AreEqual(15, ult.LearnedMask, "Ultimate đã thuộc đủ 4 chiêu");
            GrowthLook look = BotPlayers.LookFor(ult.Stage, ult.Form, _cfg.Evolution);
            Assert.IsTrue(look.Ultimate);
            Assert.AreEqual(1f, look.Get(ult.Form), 1e-4f);
            Assert.AreEqual(0f, BotPlayers.LookFor(EvolutionStage.Baby, AdultForm.Fire, _cfg.Evolution).Total, 1e-6f);
        }

        [Test]
        public void Persona_InterpolatesLevels_StyleChangesBehaviour()
        {
            var d = new BotConfig();
            BotConfig easy = BotConfig.Persona(0f, BotStyle.Balanced), normal = BotConfig.Persona(0.5f, BotStyle.Balanced);
            BotConfig hard = BotConfig.Persona(1f, BotStyle.Balanced);
            Assert.AreEqual(d.ReactionSeconds[0], easy.ReactionSeconds[1], 1e-5f);
            Assert.AreEqual(d.GuardChance[1], normal.GuardChance[1], 1e-5f);
            Assert.AreEqual(d.WrongChance[2], hard.WrongChance[1], 1e-5f);
            Assert.IsTrue(hard.AimParry[1], "giỏi thì biết canh phản đòn");
            Assert.IsFalse(easy.AimParry[1]);
            BotConfig aggr = BotConfig.Persona(0.5f, BotStyle.Aggressive), def = BotConfig.Persona(0.5f, BotStyle.Defensive);
            Assert.Less(aggr.AttackGapMax[1], def.AttackGapMax[1], "Hăng đánh dồn hơn Thủ");
            Assert.Less(aggr.GuardChance[1], def.GuardChance[1], "Thủ hay đỡ hơn Hăng");
            Assert.IsTrue(def.AimParry[1], "Thủ sớm biết canh phản đòn");
        }

        [Test]
        public void EndToEnd_FindBotPlayer_Fight90s_RankUpdated()
        {
            var health = new HealthService(_pets, _time, _cfg);
            var evo = new EvolutionService(_pets, health, _time, _bus, _cfg);
            var net = new LocalNetworkManager(_time, new SeededRandom(21), _cfg);
            net.Connect("me", null);
            MatchInfo match = null;
            net.MatchFound += m => match = m;
            PetStats ps = evo.Stats;
            net.FindMatch(new MatchTicket
            {
                Name = "Bạn", PetName = "Kem", Elo = _rank.Elo, Power = ps.Power, Stage = _pets.Current.Growth.Stage,
                Stats = new FighterStats(ps.Hp, ps.Atk, ps.Def, ps.Spd, ps.Crit),
            });
            for (int i = 0; i < 600 && match == null; i++)
            {
                _time.Advance(TimeSpan.FromSeconds(0.25));
                net.Update();
            }
            Assert.IsNotNull(match);
            var session = new BattleSession(_pets, evo, BotPlayers.ToRanked(match.Opponent), match.Seed);
            for (int i = 0; i < 3000 && !session.IsOver; i++)   // mình vuốt liên tục, người chơi ảo tự đánh
            {
                session.PlayerSwipe(new SwipeResult { Kind = i % 2 == 0 ? SwipeKind.Horizontal : SwipeKind.Up, Quality = 1f });
                session.Advance(0.1f);
            }
            Assert.IsTrue(session.IsOver, "trận kết thúc (hạ gục hoặc hết 90 giây)");
            int elo = _rank.Elo, coins = _eco.Coins;
            RankResult r = _rank.ApplyMatch(match.Opponent.Elo, session.Result.Outcome);
            net.LeaveMatch();
            Assert.AreNotEqual(elo, _rank.Elo, "thắng hay thua đều đổi Elo (đối thủ gần Elo mình)");
            Assert.AreEqual(session.Result.PlayerWon ? coins + 30 : coins, _eco.Coins);
            Assert.AreEqual(r.EloAfter, _rank.Elo);
            Assert.AreEqual(NetworkState.Online, net.State, "đấu xong tìm trận tiếp được");
        }

        [Test]
        public void RankedSession_UsesOpponentStats_BotFights_QuitIsLoss()
        {
            var health = new HealthService(_pets, _time, _cfg);
            var evo = new EvolutionService(_pets, health, _time, _bus, _cfg);
            OpponentInfo o = BotPlayers.Create(11, 1000, evo.Power, _pets.Current.Growth.Stage, _cfg);
            RankedOpponent opp = BotPlayers.ToRanked(o);
            var s = new BattleSession(_pets, evo, opp, 99);
            Assert.AreEqual(BattleMode.Ranked, s.Mode);
            Assert.AreEqual(o.Stats.Atk, s.Sim.Fighters[1].Stats.Atk, 1e-4f, "đối thủ dùng chỉ số của thú họ");
            Assert.AreEqual(1f, s.TimeScale, "PvP chạy tốc độ thường");
            var events = new List<BattleEvent>();
            for (int i = 0; i < 300 && !s.IsOver; i++) s.Advance(0.1f, events);
            Assert.IsTrue(events.Exists(e => e.Side == 1 && e.Kind == BattleEventKind.AttackStarted), "người chơi ảo tự ra đòn");
            int coins = _eco.Coins;
            s.Quit();
            Assert.IsTrue(s.IsOver);
            Assert.AreEqual(BattleOutcome.Win1, s.Result.Outcome, "bỏ cuộc = thua");
            Assert.AreEqual(0, s.Result.Coins, "PvP không có Coin thắng Bot (Coin PvP do RankService)");
            Assert.AreEqual(coins, _eco.Coins);
        }
    }
}
