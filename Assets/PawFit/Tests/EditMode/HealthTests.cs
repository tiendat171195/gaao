using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Bệnh / chết / bia mộ / trứng (GDD G2–G5).</summary>
    public class HealthTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);
        private ManualTimeProvider _time;
        private InMemorySaveService _save;
        private EventBus _bus;
        private GameConfig _cfg;
        private PetService _pets;
        private HealthService _health;

        // Nhu cầu lúc nở = 80, no bụng / năng lượng giảm 4/giờ => sau đúng 5 giờ thì hết "chăm đủ" (< 60), đồng hồ bỏ bê bắt đầu chạy.
        private const double NeglectStartsAt = 5.0;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _pets = new PetService(_time, new SeededRandom(11), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _health = new HealthService(_pets, _time, _cfg);
        }

        /// <summary>Tua tới đúng `hours` giờ kể từ lúc nở rồi tính.</summary>
        private void At(double hours)
        {
            _time.UtcNow = T0.AddHours(hours);
            _pets.Tick();
        }

        private void FullCare()
        {
            foreach (NeedType t in new[] { NeedType.Satiety, NeedType.Hygiene, NeedType.Energy, NeedType.Mood })
                _pets.Care(t, 100f);
        }

        private void CarefulRound()
        {
            _health.MarkCarefulCare(CarefulCareKind.Porridge);
            _health.MarkCarefulCare(CarefulCareKind.Blanket);
            _health.MarkCarefulCare(CarefulCareKind.Wipe);
        }

        [Test]
        public void Neglect_GoesTiredSickCriticalDead_OnSchedule()
        {
            At(NeglectStartsAt + 23.5);
            Assert.AreEqual(HealthStage.Healthy, _health.Stage);
            At(NeglectStartsAt + 24.5);
            Assert.AreEqual(HealthStage.Tired, _health.Stage, "bỏ bê 24h: mệt");
            At(NeglectStartsAt + 72.5);
            Assert.AreEqual(HealthStage.Sick, _health.Stage, "bỏ bê 72h (3 ngày): ốm");
            At(NeglectStartsAt + 120.5);
            Assert.AreEqual(HealthStage.Critical, _health.Stage, "bỏ bê 120h: nguy kịch");
            At(NeglectStartsAt + 167.5);
            Assert.IsTrue(_pets.HasPet, "chưa đủ 7 ngày: còn sống");
            At(NeglectStartsAt + 168.5);
            Assert.IsFalse(_pets.HasPet, "bỏ bê 168h (7 ngày): chết");
        }

        [Test]
        public void OfflineCatchUp_FindsExactDeathTime()
        {
            var died = new List<PetDied>();
            _bus.Subscribe<PetDied>(e => died.Add(e));
            At(400);   // tắt app 400 giờ, mở lại tính bù 1 lần
            Assert.AreEqual(1, died.Count);
            Assert.AreEqual(T0.AddHours(NeglectStartsAt + 168), died[0].Memorial.DiedAtUtc);
            Assert.AreEqual(died[0].Memorial.DiedAtUtc.AddHours(24), died[0].Egg.ReadyAtUtc);
        }

        [Test]
        public void FullCare_ResetsNeglect_AndCuresTired()
        {
            At(NeglectStartsAt + 30);
            Assert.AreEqual(HealthStage.Tired, _health.Stage);
            FullCare();
            Assert.AreEqual(HealthStage.Healthy, _health.Stage, "Mệt: chăm đủ là hết");
            Assert.AreEqual(0.0, _health.NeglectHours);
        }

        [Test]
        public void Sick_NotCuredByFullCare_ButStopsGettingWorse()
        {
            At(NeglectStartsAt + 80);
            Assert.AreEqual(HealthStage.Sick, _health.Stage);
            double neglect = _health.NeglectHours;
            for (int i = 1; i <= 20; i++)   // chăm đủ đều đặn 100 giờ
            {
                FullCare();
                At(NeglectStartsAt + 80 + 5 * i);
            }
            Assert.AreEqual(HealthStage.Sick, _health.Stage, "ốm thì chăm đủ không tự khỏi, phải chữa");
            Assert.AreEqual(neglect, _health.NeglectHours, 0.01, "nhưng không nặng thêm");
        }

        [Test]
        public void CarefulCare_OneRoundEvery12h_CuresSickAfter24h()
        {
            At(NeglectStartsAt + 80);
            double t = NeglectStartsAt + 80;
            Assert.IsTrue(_health.MarkCarefulCare(CarefulCareKind.Porridge) == false);
            _health.MarkCarefulCare(CarefulCareKind.Blanket);
            Assert.AreEqual(2, _health.CarefulMarksDone);
            Assert.IsTrue(_health.MarkCarefulCare(CarefulCareKind.Wipe), "đủ cháo + chăn + lau = 1 lượt");
            Assert.IsTrue(_health.IsRecovering);
            At(t + 10);
            CarefulRound();
            At(t + 20);
            CarefulRound();
            At(t + 23.5);
            Assert.AreEqual(HealthStage.Sick, _health.Stage, "chưa đủ 24 giờ");
            Assert.That(_health.RecoveryProgress01, Is.InRange(0.9f, 1f));
            At(t + 24.5);
            Assert.AreEqual(HealthStage.Healthy, _health.Stage, "khỏi sau 24 giờ hồi phục");
            Assert.IsFalse(_health.IsRecovering);
            Assert.Less(_health.NeglectHours, 1.0, "khỏi bệnh: đồng hồ bỏ bê chạy lại từ 0");
        }

        [Test]
        public void Recovery_StallsWithoutCare_ThenRelapses()
        {
            At(NeglectStartsAt + 80);
            double t = NeglectStartsAt + 80;
            CarefulRound();
            At(t + 20);
            Assert.AreEqual(12f / 24f, _health.RecoveryProgress01, 0.02f, "1 lượt chỉ giữ hồi phục 12 giờ");
            Assert.IsTrue(_health.IsRecovering);
            var changes = new List<HealthChange>();
            _bus.Subscribe<PetHealthChanged>(e => changes.Add(e.Change));
            At(t + 24.5);
            Assert.IsFalse(_health.IsRecovering, "quá 24 giờ không chăm: tái phát");
            Assert.Contains(HealthChange.Relapsed, changes);
            Assert.AreEqual(HealthStage.Sick, _health.Stage);
        }

        [Test]
        public void CarefulMarks_OlderThan12h_DoNotCount()
        {
            At(NeglectStartsAt + 80);
            double t = NeglectStartsAt + 80;
            _health.MarkCarefulCare(CarefulCareKind.Porridge);
            At(t + 13);
            _health.MarkCarefulCare(CarefulCareKind.Blanket);
            Assert.IsFalse(_health.MarkCarefulCare(CarefulCareKind.Wipe), "bát cháo đã quá 12 giờ");
            Assert.IsFalse(_health.IsRecovering);
            Assert.IsTrue(_health.MarkCarefulCare(CarefulCareKind.Porridge));
        }

        [Test]
        public void Medicine_CuresCriticalAfter48h_WithBasicCare()
        {
            At(NeglectStartsAt + 130);
            Assert.AreEqual(HealthStage.Critical, _health.Stage);
            Assert.AreEqual(_cfg.Health.MedicineCostCritical, _health.MedicineCost);
            double t = NeglectStartsAt + 130;
            Assert.IsTrue(_health.GiveMedicine());
            Assert.IsTrue(_health.IsRecovering);
            for (int i = 1; i <= 4; i++)   // chăm thường (cho ăn) mỗi 10 giờ là đủ khi đã uống thuốc
            {
                At(t + 10 * i);
                _pets.Care(NeedType.Satiety, 30f);
            }
            At(t + 47.5);
            Assert.AreEqual(HealthStage.Critical, _health.Stage, "nguy kịch cần 48 giờ");
            At(t + 48.5);
            Assert.AreEqual(HealthStage.Healthy, _health.Stage);
        }

        [Test]
        public void Medicine_OnHealthyPet_DoesNothing()
        {
            Assert.IsFalse(_health.GiveMedicine());
            Assert.IsFalse(_health.IsRecovering);
        }

        [Test]
        public void Sickness_LowersStats_AndBlocksTraining()
        {
            Assert.AreEqual(1f, _health.StatFactor);
            Assert.IsTrue(_health.CanTrain);
            At(NeglectStartsAt + 30);
            Assert.AreEqual(0.7f, _health.StatFactor, 1e-4);
            Assert.IsTrue(_health.CanTrain, "mệt vẫn tập được");
            At(NeglectStartsAt + 80);
            Assert.AreEqual(0.6f, _health.StatFactor, 1e-4);
            Assert.IsFalse(_health.CanTrain, "ốm: cấm tập + cấm Rank");
            Assert.IsFalse(_health.CanBathe, "ốm: không chịu xuống hồ");
            At(NeglectStartsAt + 130);
            Assert.AreEqual(0.4f, _health.StatFactor, 1e-4);
        }

        [Test]
        public void Death_AddsMemorial_AndEggWithOneInheritedTrait()
        {
            PetData kem = _pets.Current;
            string code = _pets.DnaCode;
            PetDied? died = null;
            _bus.Subscribe<PetDied>(e => died = e);
            At(NeglectStartsAt + 169);
            Assert.IsTrue(died.HasValue);
            Assert.AreEqual(HealthStage.Dead, _health.Stage);
            Assert.AreEqual(1, _pets.Memorials.Count);
            MemorialEntry m = _pets.Memorials[0];
            Assert.AreEqual("Kem", m.Name);
            Assert.AreEqual(code, m.DnaCode);
            Assert.AreEqual(kem.Id, m.PetId);
            Assert.AreEqual((NeglectStartsAt + 168) / 24.0, m.AgeDaysAtDeath(), 0.01);
            Assert.IsNull(m.BestRank, "Rank có từ Phase 4.3");
            EggData egg = _pets.Egg;
            Assert.IsNotNull(egg);
            Assert.AreEqual("Kem", egg.ParentName);
            Assert.IsTrue(EggFactory.SameTrait(egg.Dna, kem.Dna, egg.Trait), "trứng giữ đúng trait được chọn");
            Assert.IsFalse(_pets.IsEggReady);
        }

        [Test]
        public void Egg_HatchesOnlyAfter24h_AndGetsName()
        {
            At(NeglectStartsAt + 169);
            EggData egg = _pets.Egg;
            Assert.IsNull(_pets.HatchEgg("Mít"), "chưa tới lúc");
            int ready = 0, created = 0;
            _bus.Subscribe<EggReady>(e => ready++);
            _bus.Subscribe<PetCreated>(e => created++);
            At(NeglectStartsAt + 168 + 24.1);
            At(NeglectStartsAt + 168 + 25);
            Assert.AreEqual(1, ready, "báo trứng sẵn sàng đúng 1 lần");
            PetData baby = _pets.HatchEgg("   Mít   Ướt  ");
            Assert.IsNotNull(baby);
            Assert.AreEqual("Mít Ướt", baby.Name);
            Assert.AreEqual(egg.Dna, baby.Dna);
            Assert.AreEqual(HealthStage.Healthy, baby.Health.Stage);
            Assert.IsNull(_pets.Egg);
            Assert.AreEqual(1, created);
            Assert.AreEqual(1, _pets.Memorials.Count, "bia mộ vẫn còn");
        }

        [Test]
        public void SaveLoad_KeepsEggAndMemorials()
        {
            At(NeglectStartsAt + 169);
            EggData egg = _pets.Egg;
            var again = new PetService(_time, new SeededRandom(99), _save, _bus, _cfg);
            again.LoadOrCreate("Khác");
            Assert.IsFalse(again.HasPet, "không tự nở thú khác khi đang có trứng");
            Assert.AreEqual(egg.Dna, again.Egg.Dna);
            Assert.AreEqual(egg.ReadyAtUtc, again.Egg.ReadyAtUtc);
            Assert.AreEqual(1, again.Memorials.Count);
            Assert.AreEqual("Kem", again.Memorials[0].Name);
        }

        [Test]
        public void Version1SaveFile_StillLoads()
        {
            var store = new InMemorySaveService(new NewtonsoftSerializer());
            store.Save(PetService.SaveKey, new
            {
                Version = 1,
                Pet = new
                {
                    Id = "abc",
                    Name = "Cũ",
                    Dna = new PetDna(),
                    Hidden = new HiddenGenes { Seed = 5 },
                    BornAtUtc = T0,
                    LastSimulatedUtc = T0,
                    Needs = PetNeeds.Filled(80f),
                    IsSleeping = false,
                },
            });
            var pets = new PetService(_time, new SeededRandom(1), store, _bus, _cfg);
            pets.LoadOrCreate("Mới");
            Assert.AreEqual("Cũ", pets.Current.Name);
            Assert.IsNotNull(pets.Current.Health);
            Assert.AreEqual(HealthStage.Healthy, pets.Current.Health.Stage);
            pets.Save();
            StringAssert.Contains("\"Version\": " + GameSave.CurrentVersion, store.RawJson(PetService.SaveKey));
            Assert.AreEqual(GameConfig.CreateDefault().Economy.StartCoins, pets.Wallet.Coins, "file cũ được cấp Coin khởi đầu");
        }

        [Test]
        public void EggFactory_KeepsExactlyTheChosenTrait_AllTraitsHappen()
        {
            var seen = new HashSet<InheritedTrait>();
            var rng = new SeededRandom(2024);
            for (int i = 0; i < 400; i++)
            {
                var parent = new PetData { Id = "p" + i, Name = "P", Dna = DnaGenerator.Random(rng) };
                EggData egg = EggFactory.Lay(parent, T0, 24, rng);
                Assert.IsTrue(EggFactory.SameTrait(egg.Dna, parent.Dna, egg.Trait), "seed " + i);
                Assert.AreEqual(parent.Dna.Gen, egg.Dna.Gen);
                Assert.AreEqual(T0.AddHours(24), egg.ReadyAtUtc);
                Assert.IsNotNull(egg.Hidden);
                seen.Add(egg.Trait);
            }
            Assert.AreEqual(EggFactory.TraitCount, seen.Count, "trait nào cũng có thể được thừa hưởng");
        }

        [Test]
        public void PetNames_AreNormalized()
        {
            Assert.AreEqual("Bé", PetNames.Normalize(null));
            Assert.AreEqual("Bé", PetNames.Normalize("   "));
            Assert.AreEqual("Mít Ướt", PetNames.Normalize("  Mít \t  Ướt "));
            Assert.AreEqual(PetNames.MaxLength, PetNames.Normalize(new string('a', 40)).Length);
            string composed = PetNames.Normalize("Hòa");   // "Hòa" gõ bằng dấu rời
            Assert.AreEqual("Hòa", composed);
        }
    }
}
