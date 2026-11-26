using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Phase 5.2: phụ kiện (đeo 1 món, tự đeo khi mua) + đồ trang trí vườn, mua bằng Coin, còn qua đời thú.</summary>
    public class CosmeticsTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);

        private ManualTimeProvider _time;
        private EventBus _bus;
        private GameConfig _cfg;
        private InMemorySaveService _save;
        private PetService _pets;
        private EconomyService _eco;
        private CosmeticsService _cos;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            Boot();
        }

        /// <summary>Dựng lại service trên cùng kho lưu (giống tắt app mở lại).</summary>
        private void Boot()
        {
            _pets = new PetService(_time, new SeededRandom(3), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _eco = new EconomyService(_pets, new MockAdService(), _time, _bus, _cfg);
            _cos = new CosmeticsService(_pets, _eco, _bus, _cfg);
        }

        [Test]
        public void Accessory_Buy_SpendsCoins_OwnsAndWears()
        {
            var changes = new List<AccessoryChanged>();
            _bus.Subscribe<AccessoryChanged>(changes.Add);
            Assert.AreEqual(80, _cos.PriceOf(AccessoryKind.Bow));
            Assert.AreEqual(CosmeticRefusal.None, _cos.Buy(AccessoryKind.Bow));
            Assert.AreEqual(120, _eco.Coins);
            Assert.IsTrue(_cos.Owns(AccessoryKind.Bow));
            Assert.AreEqual(AccessoryKind.Bow, _cos.Equipped, "mua xong đeo luôn");
            Assert.AreEqual(1, changes.Count);
            Assert.AreEqual(AccessoryKind.Bow, changes[0].Equipped);
            Assert.AreEqual(CosmeticRefusal.AlreadyOwned, _cos.Buy(AccessoryKind.Bow), "có rồi thì không bán lại");
            Assert.AreEqual(120, _eco.Coins);
        }

        [Test]
        public void Accessory_NotEnoughCoins_KeepsEverything()
        {
            Assert.AreEqual(250, _cos.PriceOf(AccessoryKind.Crown));
            Assert.AreEqual(CosmeticRefusal.NotEnoughCoins, _cos.Buy(AccessoryKind.Crown));
            Assert.AreEqual(200, _eco.Coins);
            Assert.IsFalse(_cos.Owns(AccessoryKind.Crown));
            Assert.AreEqual(AccessoryKind.None, _cos.Equipped);
            Assert.AreEqual(CosmeticRefusal.Invalid, _cos.CheckBuy(AccessoryKind.None));
        }

        [Test]
        public void Accessory_OneAtATime_ToggleTakesOff()
        {
            _eco.Add(500, "thử");
            _cos.Buy(AccessoryKind.Bow);
            _cos.Buy(AccessoryKind.Beanie);
            Assert.AreEqual(AccessoryKind.Beanie, _cos.Equipped, "món mới mua thay món đang đeo");
            Assert.IsTrue(_cos.Owns(AccessoryKind.Bow), "món cũ vẫn còn trong đồ đã có");
            Assert.AreEqual(CosmeticRefusal.None, _cos.Toggle(AccessoryKind.Bow));
            Assert.AreEqual(AccessoryKind.Bow, _cos.Equipped);
            _cos.Toggle(AccessoryKind.Bow);
            Assert.AreEqual(AccessoryKind.None, _cos.Equipped, "chạm lại món đang đeo = tháo");
            Assert.AreEqual(CosmeticRefusal.NotOwned, _cos.Equip(AccessoryKind.Crown), "chưa mua thì không đeo được");
            Assert.AreEqual(AccessoryKind.None, _cos.Equipped);
        }

        [Test]
        public void Decor_Buy_ShowsInGarden_NoDuplicates()
        {
            var added = new List<DecorAdded>();
            _bus.Subscribe<DecorAdded>(added.Add);
            Assert.AreEqual(CosmeticRefusal.None, _cos.Buy(DecorKind.Bench));
            Assert.AreEqual(80, _eco.Coins);
            Assert.IsTrue(_cos.Owns(DecorKind.Bench));
            Assert.AreEqual(1, added.Count);
            Assert.AreEqual(DecorKind.Bench, added[0].Kind);
            Assert.AreEqual(CosmeticRefusal.AlreadyOwned, _cos.Buy(DecorKind.Bench));
            Assert.AreEqual(CosmeticRefusal.NotEnoughCoins, _cos.Buy(DecorKind.Windmill));
            Assert.AreEqual(CosmeticRefusal.Invalid, _cos.CheckBuy(DecorKind.None));
            Assert.AreEqual(80, _eco.Coins);
            Assert.AreEqual(1, added.Count);
        }

        [Test]
        public void Cosmetics_SurviveRestartAndPetDeath()
        {
            _eco.Add(300, "thử");
            _cos.Buy(AccessoryKind.Scarf);
            _cos.Buy(DecorKind.Lanterns);
            _pets.SetNeglectForDebug(_cfg.Health.DeathAfterHours + 1);
            Assert.IsFalse(_pets.HasPet);
            Boot();
            Assert.IsTrue(_cos.Owns(AccessoryKind.Scarf));
            Assert.IsTrue(_cos.Owns(DecorKind.Lanterns));
            Assert.AreEqual(AccessoryKind.Scarf, _cos.Equipped);
            _pets.MakeEggReadyForDebug();
            Assert.IsNotNull(_pets.HatchEgg("Mới"));
            Assert.AreEqual(AccessoryKind.Scarf, _cos.Equipped, "thú mới vẫn đeo phụ kiện của người chơi");
            Assert.AreEqual(250, _eco.Coins);
        }

        [Test]
        public void OldSave_Version3_LoadsWithNothingBought()
        {
            _pets.Save();
            JObject file = JObject.Parse(_save.RawJson(PetService.SaveKey));   // làm giả file bản 3: chưa có đồ trang trí
            file.Remove("Cosmetics");
            file["Version"] = 3;
            _save.Save(PetService.SaveKey, file);
            StringAssert.DoesNotContain("Cosmetics", _save.RawJson(PetService.SaveKey));
            Boot();
            Assert.AreEqual(AccessoryKind.None, _cos.Equipped);
            Assert.IsFalse(_cos.Owns(DecorKind.Bench));
            Assert.AreEqual(CosmeticRefusal.None, _cos.Buy(DecorKind.Bench));
            StringAssert.Contains("\"Version\": " + GameSave.CurrentVersion, _save.RawJson(PetService.SaveKey));
            StringAssert.Contains("\"Bench\"", _save.RawJson(PetService.SaveKey), "enum lưu bằng tên");
        }

        [Test]
        public void GrantAllForDebug_OwnsEverything_Free()
        {
            var added = new List<DecorAdded>();
            _bus.Subscribe<DecorAdded>(added.Add);
            _cos.Buy(DecorKind.Bench);
            _cos.GrantAllForDebug();
            foreach (AccessoryKind a in CosmeticsService.Accessories) Assert.IsTrue(_cos.Owns(a));
            foreach (DecorKind d in CosmeticsService.Decorations) Assert.IsTrue(_cos.Owns(d));
            Assert.AreEqual(4, added.Count, "mỗi món trang trí báo đúng 1 lần (ghế đã mua trước)");
            Assert.AreEqual(80, _eco.Coins, "công cụ test không trừ Coin");
            Assert.AreEqual(AccessoryKind.None, _cos.Equipped, "có đồ nhưng chưa tự đeo");
        }

        [Test]
        public void Prices_ComeFromConfig()
        {
            _cfg.Economy.DecorPrices[2] = 7;
            Assert.AreEqual(7, _cos.PriceOf(DecorKind.Windmill));
            Assert.AreEqual(CosmeticRefusal.None, _cos.Buy(DecorKind.Windmill));
            Assert.AreEqual(193, _eco.Coins);
            foreach (AccessoryKind a in CosmeticsService.Accessories) Assert.Greater(_cos.PriceOf(a), 0, a + " phải có giá");
            foreach (DecorKind d in CosmeticsService.Decorations) Assert.Greater(_cos.PriceOf(d), 0, d + " phải có giá");
        }
    }
}
