using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Kinh tế Phase 5.1: ví Coin, quảng cáo tự nguyện 5 lần / ngày, quầy hàng, Coin khi thắng Bot.</summary>
    public class EconomyTests
    {
        // 16:00 UTC = 23:00 giờ Việt Nam (UTC+7): tiện thử "sang ngày mới" lúc 0 giờ địa phương
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 16, 0, 0, DateTimeKind.Utc);
        private static readonly Func<DateTime, DateTime> Vietnam = utc => utc.AddHours(7);

        private ManualTimeProvider _time;
        private EventBus _bus;
        private GameConfig _cfg;
        private InMemorySaveService _save;
        private PetService _pets;
        private HealthService _health;
        private EvolutionService _evo;
        private MockAdService _ads;
        private EconomyService _eco;
        private ShopService _shop;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            Boot();
        }

        /// <summary>Dựng lại toàn bộ service trên cùng kho lưu (giống tắt app mở lại).</summary>
        private void Boot()
        {
            _pets = new PetService(_time, new SeededRandom(31), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _health = new HealthService(_pets, _time, _cfg);
            _evo = new EvolutionService(_pets, _health, _time, _bus, _cfg);
            _ads = new MockAdService(_cfg.Economy.MockAdSeconds);
            _eco = new EconomyService(_pets, _ads, _time, _bus, _cfg, null, Vietnam);
            _shop = new ShopService(_pets, _health, _eco, _bus, _cfg);
        }

        /// <summary>Xem 1 quảng cáo tới hết (quảng cáo giả 3 giây).</summary>
        private AdOutcome WatchFully(AdReward reward)
        {
            AdOutcome? got = null;
            _eco.WatchAd(reward, o => got = o);
            for (int i = 0; i < 100 && !got.HasValue; i++) _ads.Update(0.1f);
            Assert.IsTrue(got.HasValue, "quảng cáo phải kết thúc");
            return got.Value;
        }

        // ------------------------------------------------------------------
        // Ví Coin
        // ------------------------------------------------------------------

        [Test]
        public void Wallet_NewPlayer_StartsWith200()
        {
            Assert.AreEqual(200, _eco.Coins);
            Assert.GreaterOrEqual(GameSave.CurrentVersion, 3);
        }

        [Test]
        public void Wallet_Spend_RefusesWhenNotEnough()
        {
            var seen = new List<CoinsChanged>();
            _bus.Subscribe<CoinsChanged>(seen.Add);
            Assert.IsTrue(_eco.TrySpend(50, "thử"));
            Assert.AreEqual(150, _eco.Coins);
            Assert.IsFalse(_eco.TrySpend(151, "thử"), "không đủ tiền");
            Assert.AreEqual(150, _eco.Coins, "không đủ thì không trừ gì");
            Assert.AreEqual(1, seen.Count);
            Assert.AreEqual(-50, seen[0].Delta);
            Assert.AreEqual(150, seen[0].Coins);
            Assert.Throws<ArgumentOutOfRangeException>(() => _eco.TrySpend(-1, "âm"));
            Assert.Throws<ArgumentOutOfRangeException>(() => _eco.Add(-1, "âm"));
        }

        [Test]
        public void Wallet_Add_IsCappedAtMax()
        {
            _eco.Add(_cfg.Economy.MaxCoins, "nhiều");
            Assert.AreEqual(_cfg.Economy.MaxCoins, _eco.Coins);
            Assert.AreEqual(_cfg.Economy.MaxCoins - 200, _pets.Wallet.Earned, "chỉ tính phần thật sự vào ví");
        }

        [Test]
        public void Wallet_PersistsAcrossRestart()
        {
            _eco.TrySpend(75, "thử");
            _eco.Add(10, "thử");
            Boot();   // mở lại app
            Assert.AreEqual(135, _eco.Coins);
            Assert.AreEqual(75, _pets.Wallet.Spent);
            Assert.AreEqual(10, _pets.Wallet.Earned);
        }

        [Test]
        public void Wallet_SurvivesPetDeath_AndNewPet()
        {
            _eco.TrySpend(40, "thử");
            _pets.SetNeglectForDebug(_cfg.Health.DeathAfterHours + 1);
            Assert.IsFalse(_pets.HasPet, "thú đã mất");
            Assert.AreEqual(160, _eco.Coins, "ví là của người chơi, không mất theo thú");
            _pets.MakeEggReadyForDebug();
            Assert.IsNotNull(_pets.HatchEgg("Mới"));
            Assert.AreEqual(160, _eco.Coins);
            Boot();
            Assert.AreEqual(160, _eco.Coins);
        }

        [Test]
        public void OldSave_Version2_GetsStartCoins()
        {
            var store = new InMemorySaveService(new NewtonsoftSerializer());
            var old = new PetService(_time, new SeededRandom(5), store, new EventBus(), _cfg);
            old.LoadOrCreate("Cũ");
            JObject file = JObject.Parse(store.RawJson(PetService.SaveKey));   // làm giả file bản 2: chưa có ví
            file.Remove("Wallet");
            file["Version"] = 2;
            store.Save(PetService.SaveKey, file);
            StringAssert.DoesNotContain("Wallet", store.RawJson(PetService.SaveKey));

            var pets = new PetService(_time, new SeededRandom(5), store, new EventBus(), _cfg);
            pets.LoadOrCreate("Cũ");
            Assert.AreEqual("Cũ", pets.Current.Name, "vẫn là thú cũ");
            Assert.AreEqual(200, pets.Wallet.Coins, "file bản 2 được cấp 200 Coin khởi đầu");
            pets.Save();
            StringAssert.Contains("\"Version\": " + GameSave.CurrentVersion, store.RawJson(PetService.SaveKey));
            StringAssert.Contains("\"Coins\": 200", store.RawJson(PetService.SaveKey), "lưu lại thì có ví trong file");
        }

        // ------------------------------------------------------------------
        // Quảng cáo
        // ------------------------------------------------------------------

        [Test]
        public void Ad_Completes_After3Seconds_Gives20Coins()
        {
            AdOutcome? got = null;
            Assert.IsTrue(_eco.WatchAd(AdReward.Coins, o => got = o));
            Assert.IsTrue(_ads.IsShowing);
            _ads.Update(2.9f);
            Assert.IsFalse(got.HasValue, "chưa hết 3 giây");
            Assert.AreEqual(200, _eco.Coins);
            _ads.Update(0.2f);
            Assert.IsTrue(got.HasValue);
            Assert.IsTrue(got.Value.Rewarded);
            Assert.AreEqual(220, _eco.Coins);
            Assert.AreEqual(4, _eco.AdsLeftToday);
        }

        [Test]
        public void Ad_Skipped_GivesNothing_AndIsNotCounted()
        {
            AdOutcome? got = null;
            _eco.WatchAd(AdReward.Coins, o => got = o);
            _ads.Update(1f);
            _ads.Skip();
            Assert.AreEqual(AdResult.Skipped, got.Value.Result);
            Assert.IsFalse(got.Value.Rewarded);
            Assert.AreEqual(200, _eco.Coins);
            Assert.AreEqual(5, _eco.AdsLeftToday, "bỏ qua thì không mất lượt");
        }

        [Test]
        public void Ad_FivePerDay_ResetsAtLocalMidnight()
        {
            for (int i = 0; i < 5; i++) Assert.IsTrue(WatchFully(AdReward.Coins).Rewarded);
            Assert.AreEqual(300, _eco.Coins);
            Assert.AreEqual(0, _eco.AdsLeftToday);
            AdOutcome? refused = null;
            Assert.IsFalse(_eco.WatchAd(AdReward.Coins, o => refused = o));
            Assert.AreEqual(AdRefusal.DailyLimit, refused.Value.Refusal, "lần thứ 6 bị từ chối");
            Assert.IsFalse(_ads.IsShowing);

            _time.Advance(TimeSpan.FromMinutes(50));   // 23:50 giờ VN: vẫn hôm nay
            Assert.AreEqual(0, _eco.AdsLeftToday);
            _time.Advance(TimeSpan.FromMinutes(20));   // 00:10 hôm sau
            Assert.AreEqual(5, _eco.AdsLeftToday, "sang ngày mới (giờ địa phương) có lại 5 lượt");
            Assert.IsTrue(WatchFully(AdReward.Coins).Rewarded);
            Assert.AreEqual(4, _eco.AdsLeftToday);
        }

        [Test]
        public void Ad_ClockMovedBack_DoesNotGiveMoreAds()
        {
            for (int i = 0; i < 5; i++) WatchFully(AdReward.Coins);
            _time.UtcNow = T0.AddDays(-2);   // chỉnh đồng hồ lùi 2 ngày
            Assert.AreEqual(0, _eco.AdsLeftToday);
        }

        [Test]
        public void Ad_LimitIsKeptAfterRestart()
        {
            WatchFully(AdReward.Coins);
            WatchFully(AdReward.Coins);
            Boot();
            Assert.AreEqual(3, _eco.AdsLeftToday);
            Assert.AreEqual(240, _eco.Coins);
        }

        [Test]
        public void Ad_Energy_Gives20Energy_NotWhenFullOrNoPet()
        {
            _pets.SetNeedForDebug(NeedType.Energy, 30f);
            AdOutcome o = WatchFully(AdReward.Energy);
            Assert.IsTrue(o.Rewarded);
            Assert.AreEqual(AdReward.Energy, o.Reward);
            Assert.AreEqual(50f, _pets.Current.Needs.Energy, 0.01f);
            Assert.AreEqual(200, _eco.Coins, "lấy năng lượng thì không có Coin");

            _pets.SetNeedForDebug(NeedType.Energy, 95f);
            Assert.AreEqual(AdRefusal.EnergyFull, _eco.CanWatchAd(AdReward.Energy));
            Assert.AreEqual(AdRefusal.None, _eco.CanWatchAd(AdReward.Coins));

            _pets.SetNeglectForDebug(_cfg.Health.DeathAfterHours + 1);
            Assert.AreEqual(AdRefusal.NoPet, _eco.CanWatchAd(AdReward.Energy));
            Assert.AreEqual(AdRefusal.None, _eco.CanWatchAd(AdReward.Coins), "chờ trứng nở vẫn xem lấy Coin được");
        }

        [Test]
        public void Ad_BusyOrNoFill_IsRefused()
        {
            _eco.WatchAd(AdReward.Coins, null);
            AdOutcome? second = null;
            Assert.IsFalse(_eco.WatchAd(AdReward.Coins, o => second = o));
            Assert.AreEqual(AdRefusal.Busy, second.Value.Refusal);
            _ads.Skip();
            _ads.NoFill = true;
            Assert.AreEqual(AdRefusal.NotReady, _eco.CanWatchAd(AdReward.Coins));
            Assert.AreEqual(5, _eco.AdsLeftToday);
        }

        [Test]
        public void NoAdService_ReleaseBuild_NeverShowsOrRewards()
        {
            var eco = new EconomyService(_pets, new NoAdService(), _time, _bus, _cfg, null, Vietnam);
            Assert.AreEqual(AdRefusal.NotReady, eco.CanWatchAd(AdReward.Coins));
            AdOutcome? got = null;
            Assert.IsFalse(eco.WatchAd(AdReward.Coins, o => got = o));
            Assert.AreEqual(AdRefusal.NotReady, got.Value.Refusal);
            Assert.AreEqual(200, eco.Coins, "bản phát hành chưa có quảng cáo thật: không thưởng");
            Assert.AreEqual(5, eco.AdsLeftToday);
        }

        [Test]
        public void Ad_ResetForDebug_GivesBackAllAds()
        {
            WatchFully(AdReward.Coins);
            _eco.ResetAdsForDebug();
            Assert.AreEqual(5, _eco.AdsLeftToday);
        }

        // ------------------------------------------------------------------
        // Quầy hàng
        // ------------------------------------------------------------------

        [Test]
        public void Shop_Medicine_OnlyWhenIll_PriceByStage()
        {
            Assert.AreEqual(ShopRefusal.NotIll, _shop.Buy(ShopItem.Medicine), "khỏe thì không bán thuốc");
            _pets.SetNeglectForDebug(_cfg.Health.TiredAfterHours + 1);
            Assert.AreEqual(ShopRefusal.NotIll, _shop.Check(ShopItem.Medicine), "Mệt chỉ cần chăm đủ, không cần thuốc");

            _pets.SetNeglectForDebug(_cfg.Health.SickAfterHours + 1);
            Assert.AreEqual(60, _shop.PriceOf(ShopItem.Medicine));
            var bought = new List<ShopPurchased>();
            _bus.Subscribe<ShopPurchased>(bought.Add);
            Assert.AreEqual(ShopRefusal.None, _shop.Buy(ShopItem.Medicine));
            Assert.AreEqual(140, _eco.Coins);
            Assert.IsTrue(_health.IsRecovering, "uống thuốc thì bắt đầu hồi phục");
            Assert.AreEqual(TreatmentKind.Medicine, _pets.Current.Health.Recovery.Method);
            Assert.AreEqual(1, bought.Count);
            Assert.AreEqual(ShopRefusal.AlreadyMedicated, _shop.Buy(ShopItem.Medicine), "đã uống thuốc thì không bán thêm");
            Assert.AreEqual(140, _eco.Coins);

            _pets.SetNeglectForDebug(_cfg.Health.CriticalAfterHours + 1);
            Assert.AreEqual(150, _shop.PriceOf(ShopItem.Medicine), "nguy kịch thì thuốc đắt hơn");
            Assert.AreEqual(ShopRefusal.NotEnoughCoins, _shop.Buy(ShopItem.Medicine));
            _eco.Add(10, "thử");
            Assert.AreEqual(ShopRefusal.None, _shop.Buy(ShopItem.Medicine));
            Assert.AreEqual(0, _eco.Coins);
        }

        [Test]
        public void Shop_Medicine_CuresAfter24Hours_WithNormalCare()
        {
            _pets.SetNeglectForDebug(_cfg.Health.SickAfterHours + 1);
            _shop.Buy(ShopItem.Medicine);
            for (int h = 0; h < 30 && _health.IsIll; h++)
            {
                _time.Advance(TimeSpan.FromHours(1));
                _pets.Care(NeedType.Satiety, 10f);   // chăm thường mỗi giờ là đủ giữ hồi phục khi đã uống thuốc
                _pets.Care(NeedType.Hygiene, 10f);
                _pets.Care(NeedType.Energy, 10f);
                _pets.Care(NeedType.Mood, 10f);
            }
            Assert.AreEqual(HealthStage.Healthy, _health.Stage);
        }

        [Test]
        public void Shop_Treat_FeedsAndCheersUp()
        {
            _pets.SetNeedForDebug(NeedType.Satiety, 50f);
            _pets.SetNeedForDebug(NeedType.Mood, 40f);
            Assert.AreEqual(25, _shop.PriceOf(ShopItem.Treat));
            Assert.AreEqual(ShopRefusal.None, _shop.Buy(ShopItem.Treat));
            Assert.AreEqual(175, _eco.Coins);
            Assert.AreEqual(65f, _pets.Current.Needs.Satiety, 0.01f);
            Assert.AreEqual(65f, _pets.Current.Needs.Mood, 0.01f);

            _pets.SetNeedForDebug(NeedType.Satiety, 95f);
            Assert.AreEqual(ShopRefusal.NotNeeded, _shop.Buy(ShopItem.Treat), "no rồi thì không bán");
            Assert.AreEqual(175, _eco.Coins);
            _pets.SetNeedForDebug(NeedType.Satiety, 50f);
            _pets.SetSleeping(true);
            Assert.AreEqual(ShopRefusal.Sleeping, _shop.Buy(ShopItem.Treat));
        }

        [Test]
        public void Shop_EnergyDrink_GivesEnergy()
        {
            _pets.SetNeedForDebug(NeedType.Energy, 40f);
            Assert.AreEqual(ShopRefusal.None, _shop.Buy(ShopItem.EnergyDrink));
            Assert.AreEqual(170, _eco.Coins);
            Assert.AreEqual(70f, _pets.Current.Needs.Energy, 0.01f);
            _pets.SetNeedForDebug(NeedType.Energy, 93f);
            Assert.AreEqual(ShopRefusal.NotNeeded, _shop.Check(ShopItem.EnergyDrink));
        }

        [Test]
        public void Shop_NotEnoughCoins_KeepsEverything()
        {
            _eco.TrySpend(190, "thử");
            _pets.SetNeedForDebug(NeedType.Satiety, 30f);
            Assert.AreEqual(ShopRefusal.NotEnoughCoins, _shop.Buy(ShopItem.Treat));
            Assert.AreEqual(10, _eco.Coins);
            Assert.AreEqual(30f, _pets.Current.Needs.Satiety, 0.01f, "không trả tiền thì không cho ăn");
        }

        [Test]
        public void Shop_NoPet_SellsNothing()
        {
            _pets.SetNeglectForDebug(_cfg.Health.DeathAfterHours + 1);
            foreach (ShopItem item in ShopService.Items) Assert.AreEqual(ShopRefusal.NoPet, _shop.Buy(item));
            Assert.AreEqual(200, _eco.Coins);
        }

        // ------------------------------------------------------------------
        // Coin khi thắng Bot
        // ------------------------------------------------------------------

        private static SwipeResult S(SwipeKind k) => new SwipeResult { Kind = k, Quality = 1f };

        [Test]
        public void BotFight_Win_GivesCoins_LossAndTrainingGiveNone()
        {
            var win = new BattleSession(BattleMode.BotFight, BotLevel.Easy, _pets, _evo, 8, wallet: _eco);
            for (int i = 0; i < 2000 && !win.IsOver; i++)
            {
                win.PlayerSwipe(S(i % 2 == 0 ? SwipeKind.Horizontal : SwipeKind.Up));
                win.Advance(0.1f);
            }
            Assert.IsTrue(win.Result.PlayerWon);
            Assert.AreEqual(_cfg.Economy.BotWinCoins[0], win.Result.Coins);
            Assert.AreEqual(205, _eco.Coins);

            var quit = new BattleSession(BattleMode.BotFight, BotLevel.Hard, _pets, _evo, 2, wallet: _eco);
            quit.Advance(1f);
            quit.Quit();
            Assert.AreEqual(0, quit.Result.Coins, "bỏ cuộc = thua, không có Coin");

            var training = new BattleSession(BattleMode.Training, BotLevel.Easy, _pets, _evo, 5, wallet: _eco);
            training.Advance(2f);
            training.Quit();
            Assert.AreEqual(0, training.Result.Coins);
            Assert.AreEqual(205, _eco.Coins);
        }
    }
}
