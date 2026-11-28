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
            Assert.Greater(_adv.Stages[4].Skill, _adv.Stages[0].Skill, "trùm đánh giỏi hơn");
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
        public void Opponent_MatchesStage_DnaIsStableAndThemed()
        {
            RankedOpponent o = _adv.OpponentFor(0);
            PetStats p = _adv.CreatureStats(0);
            Assert.AreEqual(p.Atk, o.Stats.Atk, 1e-4f);
            Assert.AreEqual(_adv.Stages[0].Creature, o.Name);
            Assert.IsNotNull(o.Bot, "sinh vật do bot điều khiển");
            Assert.IsTrue(o.Learned[0] && o.Learned[1] && !o.Learned[3], "vùng 1 mới thuộc ngang + lên");
            Assert.IsTrue(_adv.OpponentFor(24).Learned[3], "vùng cuối thuộc đủ 4 chiêu");
            Assert.AreEqual(DnaCodec.Encode(_adv.CreatureDna(3)), DnaCodec.Encode(_adv.CreatureDna(3)), "cùng màn = cùng hình");
            Assert.AreEqual(_adv.Regions[0].Hues[0], _adv.CreatureDna(0).FurHue, "màu lông theo vùng");
        }

        [Test]
        public void EndToEnd_FightFirstStage_WinStarsCoins_Saved()
        {
            var won = new List<AdventureWon>();
            _bus.Subscribe<AdventureWon>(e => won.Add(e));
            float energy = _pets.Current.Needs.Energy;
            BattleSession s = _adv.StartBattle(0, 12345);
            Assert.AreEqual(BattleMode.Adventure, s.Mode);
            for (int i = 0; i < 3000 && !s.IsOver; i++)
            {
                s.PlayerSwipe(new SwipeResult { Kind = i % 3 == 2 ? SwipeKind.Up : SwipeKind.Horizontal, Quality = 1.1f });
                s.Advance(0.1f);
            }
            Assert.IsTrue(s.IsOver);
            Assert.IsTrue(s.Result.PlayerWon, "bé đánh liên tục thắng sinh vật màn 1-1");
            Assert.AreEqual(s.Sim.Fighters[0].HpPercent, s.Result.HpLeft, 1e-6f);
            AdventureResult r = _adv.Complete(0, s.Result);
            Assert.GreaterOrEqual(r.Stars, 1);
            Assert.Greater(r.Coins, 0);
            Assert.AreEqual(r.Coins, _wallet.Coins);
            Assert.Less(_pets.Current.Needs.Energy, energy, "trận tốn năng lượng");
            Assert.AreEqual(1, won.Count, "báo sự kiện thắng (cho nhiệm vụ ngày)");
            Assert.IsTrue(won[0].FirstClear);

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
