using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    public class DnaTests
    {
        [Test]
        public void EncodeDecode_RoundTrip_2000RandomDna()
        {
            var rng = new SeededRandom(12345);
            for (int i = 0; i < 2000; i++)
            {
                PetDna dna = DnaGenerator.Random(rng);
                string code = DnaCodec.Encode(dna);
                Assert.IsTrue(DnaCodec.TryDecode(code, out PetDna back), code);
                Assert.AreEqual(dna, back, code);
            }
        }

        [Test]
        public void Code_HasExpectedFormat()
        {
            string code = DnaCodec.Encode(DnaGenerator.Random(new SeededRandom(3)));
            Assert.IsTrue(Regex.IsMatch(code, "^PF-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}-[0-9A-HJKMNP-TV-Z]{4}$"), code);
        }

        [Test]
        public void Decode_IsTolerant_ToCaseDashesAndLookalikes()
        {
            PetDna dna = DnaGenerator.Random(new SeededRandom(5));
            string code = DnaCodec.Encode(dna);
            string messy = code.ToLowerInvariant().Replace("-", " ").Replace('1', 'l').Replace('0', 'o');
            Assert.IsTrue(DnaCodec.TryDecode(messy, out PetDna back), messy);
            Assert.AreEqual(dna, back);
            Assert.IsTrue(DnaCodec.TryDecode(code.Substring(3), out back), "không có tiền tố PF- vẫn đọc được");
            Assert.AreEqual(dna, back);
        }

        [Test]
        public void Decode_DetectsEverySingleCharTypo()
        {
            const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
            var rng = new SeededRandom(77);
            for (int n = 0; n < 50; n++)
            {
                string body = DnaCodec.Encode(DnaGenerator.Random(rng)).Substring(3).Replace("-", "");
                for (int pos = 0; pos < body.Length; pos++)
                {
                    char wrong = alphabet[(alphabet.IndexOf(body[pos]) + 1 + rng.Next(31)) % 32];
                    string typo = body.Substring(0, pos) + wrong + body.Substring(pos + 1);
                    Assert.IsFalse(DnaCodec.TryDecode(typo, out _), "lỗi gõ ở vị trí " + pos + " phải bị phát hiện");
                }
            }
        }

        [Test]
        public void Decode_RejectsGarbage()
        {
            Assert.IsFalse(DnaCodec.TryDecode(null, out _));
            Assert.IsFalse(DnaCodec.TryDecode("", out _));
            Assert.IsFalse(DnaCodec.TryDecode("PF-1234", out _));
            Assert.IsFalse(DnaCodec.TryDecode("PF-!!!!-????-####", out _));
            Assert.Throws<FormatException>(() => DnaCodec.Decode("xin chào"));
        }

        [Test]
        public void Encode_RejectsOutOfRangeGene()
        {
            PetDna dna = DnaGenerator.Random(new SeededRandom(1));
            dna.EyeColor = 12;
            Assert.Throws<ArgumentOutOfRangeException>(() => DnaCodec.Encode(dna));
        }

        [Test]
        public void Generator_SameSeed_SameDna()
        {
            Assert.AreEqual(DnaGenerator.Random(new SeededRandom(99)), DnaGenerator.Random(new SeededRandom(99)));
            Assert.AreNotEqual(DnaGenerator.Random(new SeededRandom(99)), DnaGenerator.Random(new SeededRandom(100)));
        }

        [Test]
        public void Generator_CoversEveryGeneValue_AndIsGen1()
        {
            var rng = new SeededRandom(2026);
            var seen = new HashSet<string>();
            for (int i = 0; i < 3000; i++)
            {
                PetDna d = DnaGenerator.Random(rng);
                Assert.AreEqual(1, d.Gen);
                seen.Add("eye" + d.EyeShape);
                seen.Add("ear" + d.Ear);
                seen.Add("tail" + d.Tail);
                seen.Add("horn" + d.HornCount);
                seen.Add("pat" + d.Pattern);
                seen.Add("col" + d.EyeColor);
            }
            foreach (EyeShape v in Enum.GetValues(typeof(EyeShape))) Assert.IsTrue(seen.Contains("eye" + v), "thiếu mắt " + v);
            foreach (EarType v in Enum.GetValues(typeof(EarType))) Assert.IsTrue(seen.Contains("ear" + v), "thiếu tai " + v);
            foreach (TailType v in Enum.GetValues(typeof(TailType))) Assert.IsTrue(seen.Contains("tail" + v), "thiếu đuôi " + v);
            foreach (FurPattern v in Enum.GetValues(typeof(FurPattern))) Assert.IsTrue(seen.Contains("pat" + v));
            for (int h = 0; h <= 3; h++) Assert.IsTrue(seen.Contains("horn" + h), "thiếu " + h + " sừng");
            for (int c = 0; c < 12; c++) Assert.IsTrue(seen.Contains("col" + c), "thiếu màu mắt " + c);
        }

        [Test]
        public void Gen2_IsEncodedInCode()
        {
            PetDna dna = DnaGenerator.Random(new SeededRandom(4), gen: 2);
            Assert.AreEqual(2, DnaCodec.Decode(DnaCodec.Encode(dna)).Gen);
        }
    }

    public class NeedsTests
    {
        private readonly NeedsConfig _cfg = new NeedsConfig();

        [Test]
        public void Advance_DecaysByHours_AndClampsAtZero()
        {
            PetNeeds n = PetNeeds.Filled(80f);
            NeedsSimulator.Advance(n, 10, false, _cfg);
            Assert.AreEqual(80f - 10 * _cfg.SatietyDecayPerHour, n.Satiety, 0.001f);
            Assert.AreEqual(80f - 10 * _cfg.HygieneDecayPerHour, n.Hygiene, 0.001f);
            Assert.AreEqual(80f - 10 * _cfg.EnergyDecayPerHour, n.Energy, 0.001f);
            NeedsSimulator.Advance(n, 1000, false, _cfg);
            Assert.AreEqual(0f, n.Satiety);
            Assert.AreEqual(0f, n.Mood);
        }

        [Test]
        public void Sleeping_RegensEnergy_UpTo100()
        {
            PetNeeds n = PetNeeds.Filled(50f);
            NeedsSimulator.Advance(n, 2, true, _cfg);
            Assert.AreEqual(50f + 2 * _cfg.EnergyRegenPerHour, n.Energy, 0.001f);
            NeedsSimulator.Advance(n, 100, true, _cfg);
            Assert.AreEqual(PetNeeds.Max, n.Energy);
        }

        [Test]
        public void LowestNeed_AndThreshold()
        {
            var n = new PetNeeds { Satiety = 70, Hygiene = 20, Energy = 90, Mood = 65 };
            Assert.AreEqual(NeedType.Hygiene, n.LowestNeed);
            Assert.IsFalse(n.AllAtLeast(60));
            n.Add(NeedType.Hygiene, 50);
            Assert.IsTrue(n.AllAtLeast(60));
            n.Add(NeedType.Hygiene, 500);
            Assert.AreEqual(PetNeeds.Max, n.Hygiene);
        }
    }

    public class PetServiceTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);
        private ManualTimeProvider _time;
        private InMemorySaveService _save;
        private EventBus _bus;
        private GameConfig _cfg;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
        }

        private PetService NewService(ulong seed = 1) => new PetService(_time, new SeededRandom(seed), _save, _bus, _cfg);

        [Test]
        public void FirstLaunch_HatchesPet_AndSaves()
        {
            int created = 0;
            _bus.Subscribe<PetCreated>(e => created++);
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            Assert.IsTrue(pets.HasPet);
            Assert.AreEqual("Kem", pets.Current.Name);
            Assert.AreEqual(1, created);
            Assert.AreEqual(_cfg.Needs.StartValue, pets.Current.Needs.Satiety);
            Assert.IsTrue(_save.Exists(PetService.SaveKey));
            StringAssert.StartsWith("PF-", pets.DnaCode);
        }

        [Test]
        public void Reload_RestoresSamePet()
        {
            PetService first = NewService(seed: 5);
            first.LoadOrCreate("Kem");
            string code = first.DnaCode;
            string id = first.Current.Id;

            PetService second = NewService(seed: 999);   // seed khác: vẫn phải nạp đúng con cũ
            second.LoadOrCreate("Khác");
            Assert.AreEqual(code, second.DnaCode);
            Assert.AreEqual(id, second.Current.Id);
            Assert.AreEqual("Kem", second.Current.Name);
            Assert.AreEqual(first.Current.Hidden.Seed, second.Current.Hidden.Seed);
        }

        [Test]
        public void OfflineCatchUp_AppliesHoursAway()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            _time.Advance(TimeSpan.FromHours(10));   // tắt app 10 giờ

            double away = -1;
            _bus.Subscribe<PetLoaded>(e => away = e.HoursAway);
            PetService again = NewService();
            again.LoadOrCreate("Kem");
            Assert.AreEqual(10.0, away, 0.001);
            float expected = _cfg.Needs.StartValue - 10 * _cfg.Needs.SatietyDecayPerHour;
            Assert.AreEqual(expected, again.Current.Needs.Satiety, 0.01f);
        }

        [Test]
        public void Care_RaisesNeed_ButNotAbove100()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            _time.Advance(TimeSpan.FromHours(5));
            pets.Care(NeedType.Satiety, 30f);
            Assert.AreEqual(_cfg.Needs.StartValue - 5 * _cfg.Needs.SatietyDecayPerHour + 30f, pets.Current.Needs.Satiety, 0.01f);
            pets.Care(NeedType.Satiety, 500f);
            Assert.AreEqual(PetNeeds.Max, pets.Current.Needs.Satiety);
            Assert.Throws<ArgumentOutOfRangeException>(() => pets.Care(NeedType.Mood, -5f));
        }

        [Test]
        public void SetNeedForDebug_SetsAnyValue_Clamped_AndNotifies()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            int changed = 0;
            _bus.Subscribe<PetNeedsChanged>(e => changed++);
            pets.SetNeedForDebug(NeedType.Hygiene, 7f);
            Assert.AreEqual(7f, pets.Current.Needs.Hygiene);
            pets.SetNeedForDebug(NeedType.Energy, -50f);
            Assert.AreEqual(0f, pets.Current.Needs.Energy);
            pets.SetNeedForDebug(NeedType.Mood, 250f);
            Assert.AreEqual(PetNeeds.Max, pets.Current.Needs.Mood);
            Assert.AreEqual(3, changed);
        }

        [Test]
        public void Sleeping_RestoresEnergy_OverTime()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            pets.SetSleeping(true);
            _time.Advance(TimeSpan.FromHours(1));
            pets.Tick();
            Assert.AreEqual(_cfg.Needs.StartValue + _cfg.Needs.EnergyRegenPerHour, pets.Current.Needs.Energy, 0.01f);
        }

        [Test]
        public void ClockMovedBackwards_DoesNotChangeNeeds()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            float before = pets.Current.Needs.Satiety;
            _time.UtcNow = T0.AddHours(-48);   // người chơi chỉnh giờ lùi
            pets.Tick();
            Assert.AreEqual(before, pets.Current.Needs.Satiety);
        }

        [Test]
        public void FullyCared_FollowsThreshold()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            Assert.IsTrue(pets.IsFullyCared);                      // mới nở: 80 >= 60
            _time.Advance(TimeSpan.FromHours(8));                  // no bụng: 80 - 32 = 48 < 60
            pets.Tick();
            Assert.IsFalse(pets.IsFullyCared);
        }

        [Test]
        public void SaveFile_IsReadableJson_WithEnumNames()
        {
            PetService pets = NewService();
            pets.LoadOrCreate("Kem");
            string json = _save.RawJson(PetService.SaveKey);
            StringAssert.Contains("\"Name\": \"Kem\"", json);
            StringAssert.Contains("\"Ear\": \"" + pets.Current.Dna.Ear + "\"", json);
        }
    }
}
