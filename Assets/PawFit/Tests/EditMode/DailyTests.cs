using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Việc mỗi ngày (Phase 7.3): quà mở game theo chuỗi ngày, 3 nhiệm vụ, điểm đào kho báu.</summary>
    public class DailyTests
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

        private static readonly DateTime T0 = new DateTime(2026, 11, 28, 9, 0, 0, DateTimeKind.Utc);
        private ManualTimeProvider _time;
        private EventBus _bus;
        private GameConfig _cfg;
        private InMemorySaveService _save;
        private PetService _pets;
        private GardenLayout _layout;
        private TestWallet _wallet;
        private DailyService _daily;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            _pets = new PetService(_time, new SeededRandom(41), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _layout = GardenLayout.CreateDefault();
            _wallet = new TestWallet();
            _daily = NewService();
        }

        [TearDown]
        public void TearDown() => _daily.Dispose();

        private DailyService NewService() => new DailyService(_pets, _time, _bus, _wallet, _layout, toLocal: utc => utc);

        /// <summary>Thay bộ nhiệm vụ hôm nay bằng bộ cho trước (bộ thật ngẫu nhiên theo ngày + người chơi).</summary>
        private void SetQuests(params (QuestKind kind, int target)[] quests)
        {
            Assert.IsNotNull(_daily.Quests);
            List<QuestState> list = _pets.Daily.Quests;
            list.Clear();
            foreach ((QuestKind k, int t) in quests) list.Add(new QuestState { Kind = k, Target = t, Coins = 10 });
        }

        private void NextDay(int days = 1) => _time.Advance(TimeSpan.FromDays(days));

        [Test]
        public void Gift_StreakGrowsDaily_ResetsWhenSkipped_Cycles7Days()
        {
            Assert.AreEqual(1, _daily.Streak);
            Assert.IsTrue(_daily.GiftReady);
            Assert.AreEqual(20, _daily.ClaimGift());
            Assert.IsFalse(_daily.GiftReady, "mỗi ngày nhận 1 lần");
            Assert.AreEqual(0, _daily.ClaimGift());
            NextDay();
            Assert.AreEqual(2, _daily.Streak);
            Assert.AreEqual(25, _daily.ClaimGift());
            NextDay(2);   // bỏ 1 ngày
            Assert.AreEqual(1, _daily.Streak, "bỏ 1 ngày: chuỗi về ngày 1");
            for (int i = 0; i < 6; i++)
            {
                NextDay();
                _daily.ClaimGift();
            }
            Assert.AreEqual(7, _daily.GiftDay);
            NextDay();
            Assert.AreEqual(8, _daily.Streak);
            Assert.AreEqual(1, _daily.GiftDay, "hết 7 ngày quay lại quà ngày 1");
            Assert.AreEqual(20 + 25 + 25 + 30 + 35 + 40 + 50 + 80, _wallet.Coins, "ngày chuỗi về 1 (sau khi bỏ ngày) chưa bấm nhận");
        }

        [Test]
        public void Quests_ThreePerDay_CareBattleAndMore_StableWithinDay()
        {
            IReadOnlyList<QuestState> q = _daily.Quests;
            Assert.AreEqual(3, q.Count);
            Assert.That(q[0].Kind, Is.AnyOf(QuestKind.Feed, QuestKind.Bathe, QuestKind.Cuddle, QuestKind.Workout), "1 việc chăm");
            Assert.That(q[1].Kind, Is.AnyOf(QuestKind.WinAdventure, QuestKind.AdventureStar, QuestKind.WinBattle, QuestKind.PlayPvp), "1 việc đấu");
            Assert.AreNotEqual(q[0].Kind, q[2].Kind);
            var kinds = new[] { q[0].Kind, q[1].Kind, q[2].Kind };
            _daily.Dispose();
            _daily = NewService();   // mở lại game cùng ngày: giữ nguyên bộ nhiệm vụ
            CollectionAssert.AreEqual(kinds, new[] { _daily.Quests[0].Kind, _daily.Quests[1].Kind, _daily.Quests[2].Kind });
            _bus.Publish(new CareDone(CareKind.Feed, CareResult.Ok, null));
            NextDay();
            Assert.AreEqual(3, _daily.Quests.Count);
            foreach (QuestState s in _daily.Quests) Assert.AreEqual(0, s.Progress, "ngày mới: nhiệm vụ mới");
            var noStars = new DailyService(_pets, _time, _bus, _wallet, _layout, toLocal: utc => utc, starsLeft: () => false);
            for (int d = 0; d < 40; d++)
            {
                NextDay();
                Assert.AreNotEqual(QuestKind.AdventureStar, noStars.Quests[1].Kind, "hết sao Thám hiểm thì không ra nhiệm vụ kiếm sao");
            }
            noStars.Dispose();
        }

        [Test]
        public void Quests_CountFromGameEvents()
        {
            SetQuests((QuestKind.Feed, 2), (QuestKind.WinBattle, 2), (QuestKind.PlayPvp, 1));
            _bus.Publish(new CareDone(CareKind.Feed, CareResult.Refused, "no rồi"));
            Assert.AreEqual(0, _daily.Quests[0].Progress, "bị từ chối thì không tính");
            _bus.Publish(new CareDone(CareKind.Feed, CareResult.Ok, null));
            _bus.Publish(new CareDone(CareKind.Feed, CareResult.Ok, null));
            _bus.Publish(new CareDone(CareKind.Feed, CareResult.Ok, null));
            Assert.AreEqual(2, _daily.Quests[0].Progress, "không vượt quá mục tiêu");
            _bus.Publish(new BattleEnded(0, true));
            Assert.AreEqual(0, _daily.Quests[1].Progress, "thắng Huấn luyện không tính");
            _bus.Publish(new BattleEnded(3, true));
            _bus.Publish(new BattleEnded(2, false));
            Assert.AreEqual(1, _daily.Quests[1].Progress, "thắng Thám hiểm tính, thua PvP không tính thắng");
            Assert.IsTrue(_daily.Quests[2].Done, "đấu PvP (thắng hay thua) đều tính");

            SetQuests((QuestKind.WinAdventure, 2), (QuestKind.AdventureStar, 2), (QuestKind.Cuddle, 2));
            _bus.Publish(new AdventureWon(0, 3, 3, true));
            Assert.AreEqual(1, _daily.Quests[0].Progress);
            Assert.IsTrue(_daily.Quests[1].Done, "3 sao mới");
            _bus.Publish(new CareDone(CareKind.Pet, CareResult.Ok, null));
            _bus.Publish(new CareDone(CareKind.Pet, CareResult.Ok, null));
            Assert.AreEqual(1, _daily.Quests[2].Progress, "vuốt dồn dập chỉ tính 1");
            _time.Advance(TimeSpan.FromSeconds(5));
            _bus.Publish(new CareDone(CareKind.Pet, CareResult.Ok, null));
            Assert.AreEqual(2, _daily.Quests[2].Progress);
        }

        [Test]
        public void Claim_OnlyDone_Once_BonusWhenAllDone_MailboxFlag()
        {
            _daily.ClaimGift();
            SetQuests((QuestKind.Feed, 1), (QuestKind.Bathe, 1), (QuestKind.Workout, 1));
            Assert.IsFalse(_daily.HasSomethingToClaim);
            Assert.AreEqual(0, _daily.Claim(0), "chưa xong thì chưa nhận");
            _bus.Publish(new CareDone(CareKind.Feed, CareResult.Ok, null));
            Assert.IsTrue(_daily.HasSomethingToClaim, "cờ hòm thư dựng lên");
            int before = _wallet.Coins;
            Assert.AreEqual(10, _daily.Claim(0));
            Assert.AreEqual(0, _daily.Claim(0), "nhận 1 lần");
            Assert.AreEqual(before + 10, _wallet.Coins);
            Assert.IsFalse(_daily.HasSomethingToClaim);
            _bus.Publish(new CareDone(CareKind.Bathe, CareResult.Ok, null));
            _bus.Publish(new CareDone(CareKind.Workout, CareResult.Ok, null));
            Assert.IsTrue(_daily.BonusReady);
            Assert.AreEqual(_daily.Config.AllDoneBonusCoins, _daily.ClaimBonus());
            Assert.IsFalse(_daily.BonusReady);
            _daily.Claim(1);
            _daily.Claim(2);
            Assert.IsFalse(_daily.HasSomethingToClaim);
        }

        [Test]
        public void Dig_NearSpotOnly_OncePerDay_NewSpotsTomorrow()
        {
            SetQuests((QuestKind.Dig, 2), (QuestKind.WinBattle, 1), (QuestKind.Feed, 1));
            IReadOnlyList<Vector2> spots = _daily.DigSpots;
            Assert.AreEqual(2, spots.Count);
            foreach (Vector2 p in spots) Assert.IsTrue(_layout.DigSpots.Contains(p), "điểm đào lấy từ danh sách chỗ trống của vườn");
            Assert.AreNotEqual(spots[0], spots[1]);
            DigResult far = _daily.Dig(0, spots[0] + new Vector2(5f, 0f));
            Assert.IsFalse(far.Ok);
            StringAssert.Contains("xa", far.Reason);
            float energy = _pets.Current.Needs.Energy;
            DigResult ok = _daily.Dig(0, spots[0] + new Vector2(0.5f, 0.5f));
            Assert.IsTrue(ok.Ok);
            Assert.Greater(ok.Coins, 0);
            Assert.AreEqual(ok.Coins, _wallet.Coins);
            if (ok.Prize == DigPrize.Energy) Assert.Greater(_pets.Current.Needs.Energy, energy);
            Assert.IsFalse(_daily.Dig(0, spots[0]).Ok, "mỗi điểm đào 1 lần / ngày");
            Assert.AreEqual(1, _daily.Quests[0].Progress, "tính nhiệm vụ đào");
            Assert.AreEqual(1, _daily.DigsLeft);
            _pets.SetSleeping(true);
            Assert.IsFalse(_daily.Dig(1, spots[1]).Ok, "bé đang ngủ");
            _pets.SetSleeping(false);
            NextDay();
            Assert.AreEqual(2, _daily.DigsLeft, "ngày mới: điểm đào mới");
            Assert.IsFalse(_daily.IsDug(0));
        }

        [Test]
        public void Dig_SamePrizeAfterReload()
        {
            IReadOnlyList<Vector2> spots = _daily.DigSpots;
            Vector2 p = spots[1];
            _save.TryLoad(PetService.SaveKey, out GameSave snapshot);   // trước khi đào
            DigResult a = _daily.Dig(1, p);
            _save.Save(PetService.SaveKey, snapshot);   // "mở lại game" từ file trước khi đào
            var again = new PetService(_time, new SeededRandom(42), _save, new EventBus(), _cfg);
            again.LoadOrCreate("Kem");
            var d2 = new DailyService(again, _time, new EventBus(), new TestWallet(), _layout, toLocal: utc => utc);
            Assert.AreEqual(p, d2.DigSpots[1], "cùng ngày + cùng người chơi = cùng điểm đào");
            DigResult b = d2.Dig(1, p);
            Assert.AreEqual(a.Prize, b.Prize, "đào lại không đổi được phần thưởng");
            Assert.AreEqual(a.Coins, b.Coins);
            d2.Dispose();
        }

        [Test]
        public void DigSpots_AreOnOpenGrass()
        {
            Assert.GreaterOrEqual(_layout.DigSpots.Count, 8);
            foreach (Vector2 p in _layout.DigSpots)
            {
                Assert.IsTrue(_layout.Contains(p, 1f), p + " phải nằm trong vườn");
                foreach (GardenZone z in _layout.Zones)
                    Assert.Greater(Vector2.Distance(p, z.Center), z.Radius + 0.8f, p + " đè lên khu " + z.Type);
            }
        }

        [Test]
        public void OldSave_WithoutDaily_StartsFresh()
        {
            _pets.Save();
            Assert.IsTrue(_save.TryLoad(PetService.SaveKey, out GameSave g));
            g.Daily = null;   // như file từ trước Phase 7.3
            g.Version = 6;
            _save.Save(PetService.SaveKey, g);
            var old = new PetService(_time, new SeededRandom(43), _save, new EventBus(), _cfg);
            old.LoadOrCreate("Kem");
            Assert.IsNotNull(old.Daily);
            var d = new DailyService(old, _time, new EventBus(), null, _layout, toLocal: utc => utc);
            Assert.AreEqual(3, d.Quests.Count);
            Assert.AreEqual(1, d.Streak);
            d.Dispose();
        }
    }
}
