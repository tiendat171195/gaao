using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Săn quái (Phase 10.3): rơi hạt giống cho vườn rau, sổ tay quái rừng (mốc thưởng), báo số quái hạ cho nhiệm vụ ngày.</summary>
    public class HuntLootTests
    {
        private sealed class SeedBag : ISeedBag
        {
            public readonly int[] Got = new int[Enum.GetValues(typeof(CropKind)).Length];

            public int Total
            {
                get
                {
                    int n = 0;
                    foreach (int v in Got) n += v;
                    return n;
                }
            }

            public void AddSeeds(CropKind kind, int count) => Got[(int)kind] += count;
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

        private static int[] Kills(params (MonsterKind kind, int n)[] list)
        {
            var k = new int[Enum.GetValues(typeof(MonsterKind)).Length];
            foreach ((MonsterKind kind, int n) in list) k[(int)kind] = n;
            return k;
        }

        /// <summary>Bot chơi tới khi hết trận (hoặc tới khi `stop` đúng).</summary>
        private static void Play(HuntSession s, Func<bool> stop = null)
        {
            var bot = new HuntBot(0.6f, 9);
            float dt = s.Sim.Config.TickSeconds;
            for (int i = 0; i < 20000 && !s.Finished; i++)
            {
                if (stop != null && stop()) return;
                bot.Act(s.Sim, dt);
                s.Update(dt);
            }
        }

        [Test]
        public void Boss_AlwaysDropsRareSeeds()
        {
            var rng = new SeededRandom(1);
            SeedDrop? d = HuntLoot.Roll(MonsterKind.KingSlime, 0, 99, rng, new HuntLootConfig());
            Assert.IsTrue(d.HasValue, "trùm luôn rơi, kể cả đã rơi nhiều");
            Assert.AreEqual(HuntLoot.BossSeed(0), d.Value.Kind);
            Assert.AreEqual(2, d.Value.Count);
            Assert.AreEqual(CropKind.Pumpkin, HuntLoot.BossSeed(4), "vùng xa: hạt quý");
            CollectionAssert.Contains(HuntLoot.SeedsOf(0), CropKind.Strawberry);
        }

        [Test]
        public void CommonMonsters_DropSometimes_FromRegionPool_Capped()
        {
            var rng = new SeededRandom(7);
            var cfg = new HuntLootConfig();
            int drops = 0;
            for (int i = 0; i < 2000; i++)
            {
                SeedDrop? d = HuntLoot.Roll(MonsterKind.Slime, 1, 0, rng, cfg);
                if (!d.HasValue) continue;
                drops++;
                CollectionAssert.Contains(HuntLoot.SeedsOf(1), d.Value.Kind);
                Assert.AreEqual(1, d.Value.Count);
            }
            Assert.AreEqual(cfg.DropChance, drops / 2000.0, 0.05, "khoảng 30% quái thường rơi hạt");
            for (int i = 0; i < 50; i++) Assert.IsNull(HuntLoot.Roll(MonsterKind.Bat, 1, cfg.MaxDrops, rng, cfg), "đủ hạt mỗi lần săn thì thôi");
        }

        [Test]
        public void Hunt_GivesSeeds_RecordsBook_PublishesKills()
        {
            var bag = new SeedBag();
            _adv.Seeds = bag;
            var hunted = new List<MonstersHunted>();
            _bus.Subscribe<MonstersHunted>(e => hunted.Add(e));
            HuntSession s = _adv.StartHunt(0, 12345);
            s.Events = _bus;
            var dropEvents = 0;
            var events = new List<HuntEvent>();
            var bot = new HuntBot(0.6f, 9);
            float dt = s.Sim.Config.TickSeconds;
            for (int i = 0; i < 20000 && !s.Finished; i++)
            {
                bot.Act(s.Sim, dt);
                s.Update(dt);
                events.Clear();
                s.TakeEvents(events);
                foreach (HuntEvent e in events)
                    if (e.Kind == HuntEventKind.Drop) dropEvents++;
            }
            Assert.IsTrue(s.Won);
            Assert.Greater(s.Drops.Count, 0, "12 quái thường: có rơi hạt");
            Assert.AreEqual(s.Drops.Count, dropEvents, "mỗi lần rơi có 1 việc Drop cho View");
            Assert.LessOrEqual(s.Drops.Count, _adv.Loot.MaxDrops);
            Assert.AreEqual(s.Drops.Count, bag.Total, "hết trận: hạt vào vườn rau");
            foreach (SeedDrop d in s.Drops) CollectionAssert.Contains(HuntLoot.SeedsOf(0), d.Kind);
            Assert.AreEqual(1, hunted.Count);
            Assert.AreEqual(s.Sim.Kills, hunted[0].Count, "báo số quái hạ (nhiệm vụ ngày)");
            Assert.AreEqual(s.Sim.KillsByKind[(int)MonsterKind.Slime], _adv.KillsOf(MonsterKind.Slime));
            Assert.IsTrue(_adv.HasMet(MonsterKind.Slime));
            int book = 0;
            foreach (BookReward b in s.BookRewards) book += b.Coins;
            Assert.Greater(book, 0, "lần đầu gặp có quà");
            Assert.AreEqual(s.Result.Coins + book, _wallet.Coins);
        }

        [Test]
        public void Quit_KeepsSeedsPickedUp_NoBagNoDrops()
        {
            var bag = new SeedBag();
            _adv.Seeds = bag;
            HuntSession s = _adv.StartHunt(1, 4242);
            Play(s, () => s.Drops.Count > 0);
            Assert.Greater(s.Drops.Count, 0);
            s.Quit();
            Assert.IsFalse(s.Won);
            Assert.AreEqual(s.Drops.Count, bag.Total, "bỏ cuộc vẫn giữ hạt đã nhặt");
            _adv.Seeds = null;
            HuntSession plain = _adv.StartHunt(0, 12345);
            Play(plain);
            Assert.AreEqual(0, plain.Drops.Count, "không có vườn rau thì không rơi hạt");
        }

        [Test]
        public void Book_MilestonesPayOnce_BossMilestonesLower_Persist()
        {
            List<BookReward> r1 = _adv.RecordKills(Kills((MonsterKind.Slime, 1)));
            Assert.AreEqual(1, r1.Count);
            Assert.AreEqual(1, r1[0].Milestone);
            Assert.AreEqual(5, r1[0].Coins);
            Assert.AreEqual(5, _wallet.Coins);
            Assert.AreEqual((10, 20), _adv.NextMilestone(MonsterKind.Slime));
            Assert.AreEqual(0, _adv.RecordKills(Kills((MonsterKind.Slime, 5))).Count, "chưa tới mốc 10");
            List<BookReward> r2 = _adv.RecordKills(Kills((MonsterKind.Slime, 7)));
            Assert.AreEqual(1, r2.Count);
            Assert.AreEqual(10, r2[0].Milestone);
            Assert.AreEqual(25, _wallet.Coins);
            List<BookReward> r3 = _adv.RecordKills(Kills((MonsterKind.Slime, 200)));
            Assert.AreEqual(2, r3.Count, "vượt 2 mốc 1 lúc: thưởng cả 2");
            Assert.AreEqual((-1, 0), _adv.NextMilestone(MonsterKind.Slime), "đủ mọi mốc");
            Assert.AreEqual(165, _wallet.Coins);
            List<BookReward> boss = _adv.RecordKills(Kills((MonsterKind.KingSlime, 3)));
            Assert.AreEqual(2, boss.Count, "trùm: mốc 1 và 3 con");
            Assert.AreEqual(165 + 15 + 30, _wallet.Coins);
            Assert.IsTrue(_adv.HasMet(MonsterKind.KingSlime));
            Assert.IsFalse(_adv.HasMet(MonsterKind.Golem));
            Assert.AreEqual(2, _adv.MetCount);
            var again = new PetService(_time, new SeededRandom(32), _save, new EventBus(), _cfg);
            again.LoadOrCreate("Kem");
            var health = new HealthService(again, _time, _cfg);
            var adv = new AdventureService(again, new EvolutionService(again, health, _time, new EventBus(), _cfg), health);
            Assert.AreEqual(213, adv.KillsOf(MonsterKind.Slime), "sổ tay lưu trong file game");
            Assert.AreEqual(0, adv.RecordKills(Kills((MonsterKind.Slime, 1))).Count, "mốc đã nhận không nhận lại");
        }

        [Test]
        public void FirstRegionOf_EachMonster()
        {
            Assert.AreEqual(0, HuntPlan.FirstRegionOf(MonsterKind.Slime));
            Assert.AreEqual(0, HuntPlan.FirstRegionOf(MonsterKind.KingSlime));
            Assert.AreEqual(1, HuntPlan.FirstRegionOf(MonsterKind.Mushroom));
            Assert.AreEqual(2, HuntPlan.FirstRegionOf(MonsterKind.Golem));
            Assert.AreEqual(3, HuntPlan.FirstRegionOf(MonsterKind.Boar));
            Assert.AreEqual(4, HuntPlan.FirstRegionOf(MonsterKind.CrystalGolem));
            foreach (MonsterKind k in AdventureService.BookKinds) Assert.GreaterOrEqual(HuntPlan.FirstRegionOf(k), 0, k + " phải có vùng gặp");
        }
    }
}
