using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Ví giả cho test (không cần cả EconomyService).</summary>
    internal sealed class TestWallet : ICoinWallet
    {
        public int Coins { get; set; }

        public void Add(int amount, string reason) => Coins += amount;

        public bool TrySpend(int amount, string reason)
        {
            if (Coins < amount) return false;
            Coins -= amount;
            return true;
        }
    }

    /// <summary>Vườn rau (Phase 10.2): gieo, lớn theo giờ thật, tưới lớn nhanh gấp đôi, hái vào giỏ, bé ăn quả, lưu file, thông báo.</summary>
    public class FarmTests
    {
        private GardenFixture _f;
        private TestWallet _wallet;
        private FarmService _farm;

        [SetUp]
        public void SetUp()
        {
            _f = new GardenFixture();
            _wallet = new TestWallet { Coins = 100 };
            _farm = new FarmService(_f.Pets, _f.Care, _f.Time, _f.Bus, _wallet);
        }

        private void Advance(double hours) => _f.Time.Advance(TimeSpan.FromHours(hours));

        [Test]
        public void StarterSeeds_GivenOnce_PlotsStartEmpty()
        {
            Assert.AreEqual(3, _farm.PlotCount);
            Assert.AreEqual(3, _farm.Seeds(CropKind.Strawberry), "tặng 3 hạt dâu tây để gieo thử");
            Assert.AreEqual(2, _farm.Seeds(CropKind.Carrot));
            Assert.AreEqual(0, _farm.Seeds(CropKind.Pumpkin));
            for (int i = 0; i < 3; i++) Assert.AreEqual(PlotStage.Empty, _farm.Stage(i));
            var again = new FarmService(_f.Pets, _f.Care, _f.Time, _f.Bus, _wallet);
            Assert.AreEqual(3, again.Seeds(CropKind.Strawberry), "không tặng lại lần 2");
            Assert.IsNull(_farm.Plot(3), "chỉ có 3 luống");
            Assert.AreEqual(FarmResult.BadPlot, _farm.Plant(-1, CropKind.Carrot));
        }

        [Test]
        public void Plant_GrowsWithRealTime_WetSoilTwiceAsFast()
        {
            Assert.AreEqual(FarmResult.Ok, _farm.Plant(0, CropKind.Strawberry));   // 1,5 giờ; gieo xong tưới luôn (ẩm 2 giờ)
            Assert.AreEqual(2, _farm.Seeds(CropKind.Strawberry));
            Assert.AreEqual(PlotStage.Seeded, _farm.Stage(0));
            Assert.IsTrue(_farm.IsWet(0));
            Assert.AreEqual(FarmResult.NotEmpty, _farm.Plant(0, CropKind.Carrot));
            Advance(0.5);   // ướt: 30 phút = 1 giờ lớn
            Assert.AreEqual(2f / 3f, _farm.Progress01(0), 1e-3f);
            Assert.AreEqual(PlotStage.Growing, _farm.Stage(0));
            Advance(0.25);
            Assert.AreEqual(PlotStage.Ripe, _farm.Stage(0));
            Assert.IsTrue(_farm.IsRipe(0));
            Assert.IsTrue(_farm.AnyRipe);
            Assert.IsFalse(_farm.IsWet(0), "chín rồi thì không cần tưới");
        }

        [Test]
        public void Pumpkin_DryAndWetSchedule_RipeTimePredicted()
        {
            Assert.AreEqual(FarmResult.Ok, _farm.BuySeeds(CropKind.Pumpkin));
            Assert.AreEqual(60, _wallet.Coins);
            Assert.AreEqual(2, _farm.Seeds(CropKind.Pumpkin));
            _farm.Plant(0, CropKind.Pumpkin);   // 10 giờ
            DateTime t0 = _f.Time.UtcNow;
            Assert.AreEqual(t0.AddHours(8), _farm.RipeAtUtc(0).Value, "tưới 1 lần lúc gieo: 2 giờ ẩm (= 4 giờ lớn) + 6 giờ khô");
            Advance(3);
            Assert.AreEqual(0.5f, _farm.Progress01(0), 1e-4f);
            Assert.IsFalse(_farm.IsWet(0), "đất khô sau 2 giờ");
            Assert.AreEqual(FarmResult.Ok, _farm.Water(0));
            Assert.AreEqual(FarmResult.AlreadyWet, _farm.Water(0));
            Assert.AreEqual(_f.Time.UtcNow.AddHours(3), _farm.RipeAtUtc(0).Value, "5 giờ còn lại: 2 giờ ẩm (= 4) + 1 giờ khô");
            Assert.AreEqual(_f.Time.UtcNow.AddHours(3), _farm.NextRipeUtc.Value);
            Assert.AreEqual(TimeSpan.FromHours(3), _farm.TimeToRipe(0).Value);
            Advance(1);
            Assert.AreEqual(0.7f, _farm.Progress01(0), 1e-4f);
            Advance(20);
            Assert.IsTrue(_farm.IsRipe(0), "để lâu vẫn chín đó chờ bạn, không héo");
            Assert.AreEqual(FarmResult.Ripe, _farm.Water(0));
            Assert.IsNull(_farm.NextRipeUtc, "chín hết rồi thì không hẹn báo nữa");
        }

        [Test]
        public void Harvest_FillsBasket_PlotEmptyAgain()
        {
            _farm.Plant(2, CropKind.Strawberry);
            Assert.AreEqual(FarmResult.NotRipe, _farm.Harvest(2, out _));
            var got = new List<CropHarvested>();
            _f.Bus.Subscribe<CropHarvested>(e => got.Add(e));
            Advance(2);
            Assert.AreEqual(FarmResult.Ok, _farm.Harvest(2, out int n));
            Assert.AreEqual(3, n);
            Assert.AreEqual(3, _farm.InBasket(CropKind.Strawberry));
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual(CropKind.Strawberry, got[0].Kind);
            Assert.AreEqual(PlotStage.Empty, _farm.Stage(2));
            Assert.AreEqual(FarmResult.Empty, _farm.Harvest(2, out _));
            Assert.AreEqual(FarmResult.Empty, _farm.Water(2));
        }

        [Test]
        public void Basket_HasCapPerKind()
        {
            var farm = new FarmService(_f.Pets, _f.Care, _f.Time, _f.Bus, _wallet, new FarmConfig { BasketMaxPerKind = 4 });
            farm.Plant(0, CropKind.Strawberry);
            farm.Plant(1, CropKind.Strawberry);
            farm.Plant(2, CropKind.Strawberry);
            Advance(2);
            Assert.AreEqual(FarmResult.Ok, farm.Harvest(0, out int a));
            Assert.AreEqual(FarmResult.Ok, farm.Harvest(1, out int b));
            Assert.AreEqual(3, a);
            Assert.AreEqual(1, b, "giỏ chỉ còn chỗ cho 1 quả");
            Assert.AreEqual(FarmResult.BasketFull, farm.Harvest(2, out _));
            Assert.IsTrue(farm.IsRipe(2), "giỏ đầy: quả vẫn ở luống chờ");
        }

        [Test]
        public void EatFruit_AppliesEffects_CountsAsFeeding_RefusedKeepsFruit()
        {
            _farm.Plant(0, CropKind.Carrot);
            Advance(3);
            _farm.Harvest(0, out _);
            _f.Pets.SetNeedForDebug(NeedType.Satiety, 40f);
            _f.Pets.SetNeedForDebug(NeedType.Energy, 50f);
            int feeds = 0;
            _f.Bus.Subscribe<CareDone>(e =>
            {
                if (e.Kind == CareKind.Feed && e.Result == CareResult.Ok) feeds++;
            });
            Assert.IsTrue(_farm.CanEat(CropKind.Carrot, out _));
            Assert.AreEqual(CareResult.Ok, _farm.Eat(CropKind.Carrot));
            Assert.AreEqual(65f, _f.Pets.Current.Needs.Satiety, 0.01f, "cà rốt +25 no");
            Assert.AreEqual(62f, _f.Pets.Current.Needs.Energy, 0.01f, "cà rốt +12 năng lượng");
            Assert.AreEqual(2, _farm.InBasket(CropKind.Carrot));
            Assert.AreEqual(1, feeds, "ăn quả = 1 lần cho ăn (nhiệm vụ ngày, mẹo)");
            _f.Pets.SetNeedForDebug(NeedType.Satiety, 95f);
            Assert.IsFalse(_farm.CanEat(CropKind.Carrot, out string why));
            StringAssert.Contains("no", why);
            Assert.AreEqual(CareResult.Refused, _farm.Eat(CropKind.Carrot));
            Assert.AreEqual(2, _farm.InBasket(CropKind.Carrot), "bé không ăn thì quả vẫn còn trong giỏ");
            Assert.IsFalse(_farm.CanEat(CropKind.Pumpkin, out why), "giỏ không có bí ngô");
        }

        [Test]
        public void Blueberry_TrainsSpeed_ButNotWhenIll()
        {
            _farm.AddSeeds(CropKind.Blueberry, 2);
            _farm.Plant(0, CropKind.Blueberry);
            _farm.Plant(1, CropKind.Blueberry);
            Advance(6);
            _farm.Harvest(0, out _);
            _farm.Harvest(1, out _);
            Assert.AreEqual(8, _farm.InBasket(CropKind.Blueberry));
            _f.Pets.SetNeedForDebug(NeedType.Satiety, 30f);
            float speed = _f.Evo.Training.Get(TrainingAxis.Speed);
            Assert.AreEqual(CareResult.Ok, _farm.Eat(CropKind.Blueberry));
            Assert.AreEqual(speed + 2f, _f.Evo.Training.Get(TrainingAxis.Speed), 1e-3f, "việt quất: +2 điểm Tốc độ");
        }

        [Test]
        public void BuySeeds_NotEnoughCoins_BuysNothing()
        {
            _wallet.Coins = 10;
            Assert.AreEqual(FarmResult.NotEnoughCoins, _farm.BuySeeds(CropKind.Strawberry));
            Assert.AreEqual(3, _farm.Seeds(CropKind.Strawberry));
            Assert.AreEqual(10, _wallet.Coins);
            Assert.AreEqual(FarmResult.NoSeed, _farm.Plant(0, CropKind.Pumpkin));
            var noWallet = new FarmService(_f.Pets, _f.Care, _f.Time, _f.Bus);
            Assert.AreEqual(FarmResult.NotEnoughCoins, noWallet.BuySeeds(CropKind.Carrot));
        }

        [Test]
        public void ClockGoingBack_DoesNotShrinkGrowth()
        {
            _farm.Plant(0, CropKind.Carrot);
            Advance(1);
            float p = _farm.Progress01(0);
            _f.Time.UtcNow = _f.Time.UtcNow.AddHours(-5);   // đổi giờ máy lùi
            Assert.AreEqual(p, _farm.Progress01(0), 1e-5f, "cây đứng yên, không tụt");
            Assert.IsFalse(_farm.IsRipe(0));
        }

        [Test]
        public void Farm_PersistsInSave_OldSaveGetsAFreshFarm()
        {
            var save = new InMemorySaveService(new NewtonsoftSerializer());
            var time = new ManualTimeProvider(GardenFixture.T0);
            var cfg = GameConfig.CreateDefault();
            FarmService Open(out PetService pets)
            {
                var bus = new EventBus();
                pets = new PetService(time, new SeededRandom(5), save, bus, cfg);
                pets.LoadOrCreate("Kem");
                var health = new HealthService(pets, time, cfg);
                var evo = new EvolutionService(pets, health, time, bus, cfg);
                var care = new CareActionService(pets, health, evo, new GardenConfig(), time, bus);
                return new FarmService(pets, care, time, bus);
            }

            FarmService farm = Open(out _);
            farm.Plant(1, CropKind.Carrot);
            time.Advance(TimeSpan.FromHours(1));
            FarmService reopened = Open(out _);
            Assert.AreEqual(1, reopened.Seeds(CropKind.Carrot), "đã gieo 1 trong 2 hạt");
            Assert.AreEqual(2f / 3f, reopened.Progress01(1), 1e-3f, "tắt app cây vẫn lớn (1 giờ ẩm = 2 giờ lớn)");
            Assert.IsTrue(save.TryLoad(PetService.SaveKey, out GameSave g));
            Assert.AreEqual(GameSave.CurrentVersion, g.Version);
            Assert.IsNotNull(g.Farm);
            g.Farm = null;   // như file từ trước Phase 10.2
            g.Version = 7;
            save.Save(PetService.SaveKey, g);
            FarmService old = Open(out _);
            Assert.AreEqual(3, old.Seeds(CropKind.Strawberry), "file cũ: vườn rau mới + gói hạt khởi đầu");
            Assert.AreEqual(PlotStage.Empty, old.Stage(1));
        }

        [Test]
        public void Notification_CropRipe_IsPlannedWhenRipe()
        {
            DateTime now = GardenFixture.T0;   // 8:00 UTC, giờ trên máy = UTC: ngoài giờ yên tĩnh
            List<PlannedNotification> plan = NotificationPlanner.Plan(null, null, now, TimeSpan.Zero, GameConfig.CreateDefault(),
                                                                      new ExpressionConfig(), null, default, now.AddHours(3));
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(PetAlertKind.CropRipe, plan[0].Kind);
            Assert.AreEqual(now.AddHours(3), plan[0].FireUtc);
            StringAssert.Contains("chín", plan[0].Title);
            Assert.AreEqual(0, NotificationPlanner.Plan(null, null, now, TimeSpan.Zero, GameConfig.CreateDefault(), new ExpressionConfig(),
                                                        null, default, now.AddHours(-1)).Count, "đã chín sẵn lúc rời game: không báo");
        }
    }
}
