using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Thám hiểm (Phase 7.2): bản đồ, mở khóa, sao, Coin, độ khó, điều kiện vào trận, trận thật, lưu.</summary>
    public class AdventureTests
    {
        private sealed class TestWallet : ICoinWallet
        {
            public int Coins { get; private set; }
            public void Add(int amount, string reason) => Coins += amount;

            public bool TrySpend(int amount, string reason)
            {
                if (amount > Coins) return false;
                Coins -= amount;
                return true;
            }
        }

        private static readonly DateTime T0 = new DateTime(2026, 11, 28, 8, 0, 0, DateTimeKind.Utc);
        private ManualTimeProvider _time;
        private EventBus _bus;
        private GameConfig _cfg;
        private InMemorySaveService _save;
        private PetService _pets;
        private HealthService _health;
        private EvolutionService _evo;
        private TestWallet _wallet;
        private AdventureService _adv;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            _pets = new PetService(_time, new SeededRandom(31), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _health = new HealthService(_pets, _time, _cfg);
            _evo = new EvolutionService(_pets, _health, _time, _bus, _cfg);
            _wallet = new TestWallet();
            _adv = new AdventureService(_pets, _evo, _health, _wallet, _bus);
        }

        [Test]
        public void Map_FiveRegions_FiveStagesEach_BossLast_StageFollowsEvolution()
        {
            Assert.AreEqual(5, _adv.Regions.Count);
            Assert.AreEqual(25, _adv.Stages.Count);
            Assert.AreEqual(75, _adv.MaxStars);
            for (int r = 0; r < 5; r++)
            {
                AdventureRegion region = _adv.Regions[r];
                Assert.AreEqual((EvolutionStage)r, region.Stage, "vùng " + (r + 1) + " theo giai đoạn tiến hóa");
                for (int n = 0; n < region.StageCount; n++)
                {
                    AdventureStage s = _adv.Stages[region.FirstStage + n];
                    Assert.AreEqual(r, s.Region);
                    Assert.AreEqual(n == region.StageCount - 1, s.Boss, "màn cuối vùng là trùm");
                    Assert.IsFalse(string.IsNullOrEmpty(s.Creature));
                }
            }
            Assert.AreEqual("1-1", _adv.Stages[0].Code);
            Assert.AreEqual("5-5", _adv.Stages[24].Code);
            Assert.Greater(_adv.CreatureStats(4).Power, _adv.CreatureStats(0).Power, "trùm mạnh hơn màn đầu");
            Assert.Greater(_adv.CreatureStats(5).Power, _adv.CreatureStats(0).Power, "vùng sau mạnh hơn vùng trước");
            CollectionAssert.Contains(_adv.PlanFor(4).Kinds, MonsterKind.KingSlime, "màn cuối vùng 1 có trùm Vua Chất Nhờn");
            Assert.AreEqual(MonsterBook.Get(MonsterKind.KingSlime).Name, _adv.Stages[4].Creature, "màn trùm mang tên trùm");
        }

        [Test]
        public void Unlock_StageByStage_BossOpensNextRegion()
        {
            Assert.IsTrue(_adv.IsUnlocked(0));
            Assert.IsFalse(_adv.IsUnlocked(1));
            Assert.IsFalse(_adv.IsRegionUnlocked(1));
            Assert.IsFalse(_adv.CanStart(1, out string why));
            StringAssert.Contains("chưa mở", why);
            _adv.Complete(0, false, 0f);
            Assert.IsFalse(_adv.IsUnlocked(1), "thua thì chưa mở màn sau");
            for (int i = 0; i < 4; i++) Assert.AreEqual(-1, _adv.Complete(i, true, 0.2f).UnlockedRegion);
            AdventureResult boss = _adv.Complete(4, true, 0.2f);
            Assert.AreEqual(1, boss.UnlockedRegion, "thắng trùm lần đầu: mở vùng 2");
            Assert.IsTrue(_adv.IsRegionUnlocked(1));
            Assert.AreEqual(5, _adv.NextStage, "màn nên đánh tiếp = màn đầu vùng 2");
            Assert.AreEqual(-1, _adv.Complete(4, true, 0.9f).UnlockedRegion, "thắng lại trùm không báo mở vùng nữa");
        }

        [Test]
        public void Stars_ByHpLeft_BestIsKept()
        {
            Assert.AreEqual(0, _adv.StarsFor(false, 1f));
            Assert.AreEqual(1, _adv.StarsFor(true, 0.39f));
            Assert.AreEqual(2, _adv.StarsFor(true, 0.4f));
            Assert.AreEqual(3, _adv.StarsFor(true, 0.7f));
            Assert.AreEqual(3, _adv.Complete(0, true, 0.95f).BestStars);
            AdventureResult worse = _adv.Complete(0, true, 0.1f);
            Assert.AreEqual(1, worse.Stars);
            Assert.AreEqual(3, worse.BestStars, "giữ số sao tốt nhất");
            Assert.AreEqual(3, _adv.StarsOf(0));
            Assert.AreEqual(3, _adv.TotalStars);
            Assert.AreEqual(3, _adv.RegionStars(0));
        }

        [Test]
        public void Coins_FirstClear_NewStars_Repeat_Boss()
        {
            AdventureStage s = _adv.Stages[0];
            AdventureResult first = _adv.Complete(0, true, 0.1f);   // 1 sao
            Assert.IsTrue(first.FirstClear);
            Assert.AreEqual(s.Coins, first.Coins);
            AdventureResult better = _adv.Complete(0, true, 0.8f);   // lên 3 sao: +2 sao mới
            Assert.AreEqual(2, better.NewStars);
            Assert.AreEqual((int)Math.Round(s.Coins * 0.5f * 2), better.Coins);
            AdventureResult again = _adv.Complete(0, true, 0.8f);   // không thêm sao
            Assert.AreEqual(0, again.NewStars);
            Assert.AreEqual(Math.Max(1, (int)Math.Round(s.Coins * 0.2f)), again.Coins, "thắng lại: ít Coin hơn");
            Assert.AreEqual(first.Coins + better.Coins + again.Coins, _wallet.Coins, "Coin vào ví");
            Assert.AreEqual(0, _adv.Complete(1, false, 0f).Coins, "thua: không có Coin");
            AdventureStage boss = _adv.Stages[4], before = _adv.Stages[3];
            Assert.Greater(boss.Coins, before.Coins * 1.8f, "trùm thưởng gấp đôi");
        }

        [Test]
        public void Difficulty_ComparedToPet()
        {
            Assert.AreEqual(EvolutionStage.Baby, _evo.Stage);
            Assert.AreEqual(AdventureDifficulty.Easy, _adv.DifficultyOf(0), "bé mới nở vs màn 1-1");
            Assert.GreaterOrEqual((int)_adv.DifficultyOf(4), (int)AdventureDifficulty.Fair, "trùm vùng 1 khó hơn");
            Assert.AreEqual(AdventureDifficulty.VeryHard, _adv.DifficultyOf(24), "Baby vs trùm cuối");
        }

        [Test]
        public void CanStart_NeedsHealthyAwakePetWithEnergy()
        {
            Assert.IsTrue(_adv.CanStart(0, out _));
            _pets.SetSleeping(true);
            Assert.IsFalse(_adv.CanStart(0, out string sleeping));
            StringAssert.Contains("ngủ", sleeping);
            _pets.SetSleeping(false);
            _pets.Spend(NeedType.Energy, 100f);
            Assert.IsFalse(_adv.CanStart(0, out string tired));
            StringAssert.Contains("mệt", tired);
        }

        [Test]
        public void HuntPlan_MatchesStage_StableAndThemed()
        {
            HuntPlan p = _adv.PlanFor(0);
            Assert.AreEqual(_adv.CreatureStats(0).Atk, p.RefAtk, 1e-4f, "quái theo chỉ số chuẩn của màn");
            Assert.AreEqual(3, p.Waves.Count, "màn thường: 3 đợt quái");
            foreach (MonsterKind k in p.Kinds)
                Assert.IsTrue(k == MonsterKind.Slime || k == MonsterKind.Bat, "Đồi Cỏ Gió chỉ có chất nhờn + dơi: " + k);
            Assert.Less(p.Waves[0].Spawns.Count, p.Waves[2].Spawns.Count, "đông dần");
            HuntPlan again = _adv.PlanFor(0);
            for (int w = 0; w < p.Waves.Count; w++)
                for (int i = 0; i < p.Waves[w].Spawns.Count; i++)
                {
                    Assert.AreEqual(p.Waves[w].Spawns[i].Kind, again.Waves[w].Spawns[i].Kind, "cùng màn = cùng đợt quái");
                    Assert.AreEqual(p.Waves[w].Spawns[i].Side, again.Waves[w].Spawns[i].Side);
                }
            HuntPlan boss = _adv.PlanFor(9);   // trùm vùng 2
            Assert.IsTrue(boss.Boss);
            Assert.AreEqual(2, boss.Waves.Count, "màn trùm: 1 đợt quái + đợt trùm");
            Assert.AreEqual(MonsterKind.MushroomKing, boss.Waves[1].Spawns[0].Kind);
            Assert.Greater(_adv.PlanFor(24).RefHp, _adv.PlanFor(0).RefHp, "vùng cuối quái trâu hơn hẳn");
        }

        [Test]
        public void EndToEnd_HuntFirstStage_WinStarsCoins_Saved()
        {
            var won = new List<AdventureWon>();
            var ended = new List<BattleEnded>();
            _bus.Subscribe<AdventureWon>(e => won.Add(e));
            _bus.Subscribe<BattleEnded>(e => ended.Add(e));
            float energy = _pets.Current.Needs.Energy;
            HuntSession s = _adv.StartHunt(0, 12345);
            s.Events = _bus;
            var bot = new HuntBot(0.6f, 9);
            float dt = s.Sim.Config.TickSeconds;
            for (int i = 0; i < 20000 && !s.Finished; i++)
            {
                bot.Act(s.Sim, dt);
                s.Update(dt);
            }
            Assert.IsTrue(s.Finished);
            Assert.IsTrue(s.Won, "bé mới nở đánh khá thắng màn 1-1");
            AdventureResult r = s.Result;
            Assert.GreaterOrEqual(r.Stars, 1);
            Assert.Greater(r.Coins, 0);
            int book = 0;
            foreach (BookReward b in s.BookRewards) book += b.Coins;
            Assert.AreEqual(r.Coins + book, _wallet.Coins, "Coin thắng + quà sổ tay quái rừng (Phase 10.3)");
            Assert.Less(_pets.Current.Needs.Energy, energy, "trận tốn năng lượng");
            Assert.AreEqual(1, won.Count, "báo sự kiện thắng (cho nhiệm vụ ngày)");
            Assert.IsTrue(won[0].FirstClear);
            Assert.AreEqual(1, ended.Count, "báo hết trận (nhiệm vụ \"thắng N trận\")");
            Assert.AreEqual((int)BattleMode.Adventure, ended[0].Mode);
            Assert.IsTrue(ended[0].Won);
            Assert.Greater(s.StrengthGained, 0f, "hạ quái = điểm Sức mạnh");

            // nạp lại từ file lưu: tiến độ còn nguyên
            var again = new PetService(_time, new SeededRandom(32), _save, new EventBus(), _cfg);
            again.LoadOrCreate("Kem");
            var health = new HealthService(again, _time, _cfg);
            var adv = new AdventureService(again, new EvolutionService(again, health, _time, new EventBus(), _cfg), health);
            Assert.AreEqual(r.BestStars, adv.StarsOf(0));
            Assert.IsTrue(adv.IsUnlocked(1));
        }

        [Test]
        public void OldSave_WithoutAdventure_StartsEmpty()
        {
            _pets.Adventure.Stars.Add(2);
            _pets.Save();
            Assert.IsTrue(_save.TryLoad(PetService.SaveKey, out GameSave g));
            g.Adventure = null;   // như file từ trước Phase 7.2
            g.Version = 5;
            _save.Save(PetService.SaveKey, g);
            var old = new PetService(_time, new SeededRandom(33), _save, new EventBus(), _cfg);
            old.LoadOrCreate("Kem");
            Assert.IsNotNull(old.Adventure);
            Assert.AreEqual(0, old.Adventure.Stars.Count);
        }
    }
}
