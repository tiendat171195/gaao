using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Tiến hóa + chỉ số + lực chiến (GDD G7, G8).</summary>
    public class EvolutionTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);
        private ManualTimeProvider _time;
        private InMemorySaveService _save;
        private EventBus _bus;
        private GameConfig _cfg;
        private PetService _pets;
        private HealthService _health;
        private EvolutionService _evo;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _pets = new PetService(_time, new SeededRandom(21), _save, _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _health = new HealthService(_pets, _time, _cfg);
            _evo = new EvolutionService(_pets, _health, _time, _bus, _cfg);
        }

        private void FullCare()
        {
            foreach (NeedType t in new[] { NeedType.Satiety, NeedType.Hygiene, NeedType.Energy, NeedType.Mood })
                _pets.Care(t, 100f);
        }

        /// <summary>Sống `days` ngày, được chăm đủ mỗi `everyHours` giờ.</summary>
        private void LiveCaredFor(double days, double everyHours = 6)
        {
            double left = days * 24;
            while (left > 1e-9)
            {
                double step = Math.Min(everyHours, left);
                left -= step;
                FullCare();
                _time.Advance(TimeSpan.FromHours(step));
                _pets.Tick();
            }
        }

        private static HiddenGenes Hidden(Personality p = Personality.Shy, uint seed = 1) => new HiddenGenes { Seed = seed, Personality = p };

        private static TrainingVector V(float str, float spd, float care, float shadow) =>
            new TrainingVector { Strength = str, Speed = spd, Care = care, Shadow = shadow };

        [Test]
        public void Stages_FollowAge_Baby3Child7Teen14Adult()
        {
            var evolved = new List<EvolutionStage>();
            _bus.Subscribe<PetEvolved>(e => evolved.Add(e.To));
            Assert.AreEqual(EvolutionStage.Baby, _evo.Stage);
            LiveCaredFor(2.9);
            Assert.AreEqual(EvolutionStage.Baby, _evo.Stage);
            LiveCaredFor(0.25);
            Assert.AreEqual(EvolutionStage.Child, _evo.Stage);
            LiveCaredFor(4);
            Assert.AreEqual(EvolutionStage.Teen, _evo.Stage);
            Assert.AreEqual(AdultForm.None, _evo.Form, "chưa tới Adult thì chưa chốt dạng");
            LiveCaredFor(7);
            Assert.AreEqual(EvolutionStage.Adult, _evo.Stage);
            Assert.AreNotEqual(AdultForm.None, _evo.Form);
            CollectionAssert.AreEqual(new[] { EvolutionStage.Child, EvolutionStage.Teen, EvolutionStage.Adult }, evolved);
        }

        [Test]
        public void CareScore_IsAverageCareOfRecentHours()
        {
            for (int h = 0; h < 10; h++)
            {
                foreach (NeedType t in new[] { NeedType.Satiety, NeedType.Hygiene, NeedType.Energy, NeedType.Mood })
                    _pets.SetNeedForDebug(t, 50f);
                _time.Advance(TimeSpan.FromHours(1));
                _pets.Tick();
            }
            Assert.That(_evo.CareScore, Is.InRange(46f, 50f), "giữ nhu cầu quanh 50 => CareScore ~ 48");
            LiveCaredFor(2, 3);
            Assert.Greater(_evo.CareScore, 80f, "chăm kỹ 2 ngày => CareScore cao");
        }

        [Test]
        public void DecideForm_FollowsGddRules()
        {
            EvolutionConfig c = _cfg.Evolution;
            HiddenGenes h = Hidden();
            Assert.AreEqual(AdultForm.Fire, EvolutionRules.DecideForm(V(100, 10, 10, 10), h, 75, c), "Sức mạnh trội => Lửa");
            Assert.AreEqual(AdultForm.Water, EvolutionRules.DecideForm(V(10, 100, 10, 10), h, 75, c), "Tốc độ trội => Nước");
            Assert.AreEqual(AdultForm.Light, EvolutionRules.DecideForm(V(10, 10, 100, 10), h, 75, c), "Chăm sóc trội + CareScore >= 70 => Ánh Sáng");
            Assert.AreEqual(AdultForm.Fire, EvolutionRules.DecideForm(V(40, 10, 100, 10), h, 60, c), "Chăm sóc trội nhưng CareScore < 70 => theo hướng mạnh thứ 2");
            Assert.AreEqual(AdultForm.Shadow, EvolutionRules.DecideForm(V(10, 10, 10, 100), h, 75, c), "Bóng tối trội => Bóng Tối");
            Assert.AreEqual(AdultForm.Shadow, EvolutionRules.DecideForm(V(100, 10, 10, 10), h, 35, c), "CareScore < 40 => Bóng Tối");
            Assert.AreEqual(AdultForm.Steel, EvolutionRules.DecideForm(V(25, 26, 24, 25), h, 75, c), "tập cân bằng => Thép");
            Assert.AreEqual(AdultForm.Steel, EvolutionRules.DecideForm(V(0, 0, 0, 0), h, 75, c));
        }

        [Test]
        public void Personality_TipsCloseCalls()
        {
            EvolutionConfig c = _cfg.Evolution;
            c.SeedNoise = 0f;   // bỏ nhiễu seed để thấy rõ ảnh hưởng tính cách
            Assert.AreEqual(AdultForm.Fire, EvolutionRules.DecideForm(V(50, 50, 5, 5), Hidden(Personality.Fiery), 75, c));
            Assert.AreEqual(AdultForm.Water, EvolutionRules.DecideForm(V(50, 50, 5, 5), Hidden(Personality.Playful), 75, c));
        }

        [Test]
        public void SeedNoise_StaysWithin10Percent_AndIsStablePerPet()
        {
            EvolutionConfig c = _cfg.Evolution;
            for (uint seed = 1; seed < 200; seed++)
            {
                float[] a = EvolutionRules.AxisScores(V(100, 100, 100, 100), Hidden(Personality.Shy, seed), c);
                float[] b = EvolutionRules.AxisScores(V(100, 100, 100, 100), Hidden(Personality.Shy, seed), c);
                CollectionAssert.AreEqual(a, b, "cùng con thú => cùng nhiễu");
                Assert.That(a[0], Is.InRange(90f, 110f));
                Assert.That(a[1], Is.InRange(90f, 110f));
            }
        }

        [Test]
        public void Look_GrowsSmoothly_ThenLocksToForm()
        {
            PetData pet = _pets.Current;
            pet.Growth.Training = V(80, 5, 5, 0);   // nghiêng Lửa
            GrowthLook baby = _evo.Look;
            Assert.AreEqual(0f, baby.Total);
            pet.Growth.Stage = EvolutionStage.Child;
            pet.BornAtUtc = T0.AddDays(-5);   // giữa Child (3..7)
            GrowthLook child = _evo.Look;
            Assert.AreEqual(_cfg.Evolution.ChildGrowth * 0.5f, child.Total, 0.01f);
            Assert.AreEqual(child.Fire, child.Water, 1e-4f, "Child lớn đều, chưa nghiêng");
            pet.Growth.Stage = EvolutionStage.Teen;
            pet.BornAtUtc = T0.AddDays(-13.9);
            GrowthLook teen = _evo.Look;
            Assert.Greater(teen.Fire, 0.6f, "cuối Teen gần như đã thành dạng đang nghiêng");
            Assert.Less(teen.Water, 0.02f);
            pet.Growth.Stage = EvolutionStage.Adult;
            pet.Growth.Form = AdultForm.Fire;
            Assert.AreEqual(1f, _evo.Look.Fire);
            Assert.AreEqual(1f, _evo.Look.Total);
        }

        [Test]
        public void Training_HasDailyCap_PerAxis()
        {
            float cap = _cfg.Evolution.DailyTrainingCap;
            Assert.AreEqual(20f, _evo.AddTraining(TrainingAxis.Strength, 20f));
            Assert.AreEqual(cap - 20f, _evo.AddTraining(TrainingAxis.Strength, 20f), 1e-4f);
            Assert.AreEqual(0f, _evo.AddTraining(TrainingAxis.Strength, 5f));
            Assert.AreEqual(5f, _evo.AddTraining(TrainingAxis.Speed, 5f), "hướng khác vẫn còn chỗ");
            _pets.AgeForDebug(1);   // sang ngày tuổi mới
            Assert.AreEqual(5f, _evo.AddTraining(TrainingAxis.Strength, 5f));
            Assert.AreEqual(cap + 5f, _evo.Training.Strength, 1e-3f);
        }

        [Test]
        public void Workout_CostsEnergy_RefusedWhenTiredOrSick()
        {
            float energy = _pets.Current.Needs.Energy;
            Assert.AreEqual(WorkoutResult.Ok, _evo.TryWorkout());
            Assert.AreEqual(_cfg.Evolution.WorkoutStrength, _evo.Training.Strength, 1e-4f);
            Assert.AreEqual(energy - _cfg.Evolution.WorkoutEnergyCost, _pets.Current.Needs.Energy, 0.01f);
            _pets.SetNeedForDebug(NeedType.Energy, 10f);
            Assert.AreEqual(WorkoutResult.TooTired, _evo.TryWorkout());
            _pets.SetNeedForDebug(NeedType.Energy, 90f);
            _pets.SetNeglectForDebug(80);
            Assert.AreEqual(WorkoutResult.Sick, _evo.TryWorkout(), "ốm: cấm tập (G3)");
        }

        [Test]
        public void Neglect_PushesTowardShadow()
        {
            _time.Advance(TimeSpan.FromHours(30));   // 80 -> dưới 60 sau 5 giờ => 25 giờ bị bỏ bê
            _pets.Tick();
            Assert.AreEqual(25f * _cfg.Evolution.ShadowPerNeglectHour, _evo.Training.Shadow, 0.3f);
        }

        [Test]
        public void Ultimate_NeedsCareSkillsAndHealth()
        {
            LiveCaredFor(30.2, 3);
            Assert.AreEqual(EvolutionStage.Adult, _evo.Stage, "chưa thuộc chiêu nào => chưa Ultimate");
            Assert.Greater(_evo.CareScore, _cfg.Evolution.UltimateMinCareScore);
            for (int i = 0; i < 4; i++) _evo.LearnSkill();
            Assert.IsTrue(_evo.CanUltimate);
            LiveCaredFor(0.25, 3);
            Assert.AreEqual(EvolutionStage.Ultimate, _evo.Stage);
            Assert.IsTrue(_evo.Look.Ultimate);
        }

        [Test]
        public void Ultimate_BlockedByRecentSickness()
        {
            LiveCaredFor(29, 3);
            for (int i = 0; i < 4; i++) _evo.LearnSkill();
            _pets.Current.Growth.LastIllUtc = _time.UtcNow;   // vừa khỏi bệnh
            LiveCaredFor(1.5, 3);
            Assert.AreEqual(EvolutionStage.Adult, _evo.Stage, "chưa đủ 7 ngày không bệnh");
        }

        [Test]
        public void DnaDrifts_DailyTowardLeaningForm_FromTeen()
        {
            PetData pet = _pets.Current;
            pet.Dna.FurHue = 40;
            pet.Growth.Training = V(200, 0, 0, 0);   // nghiêng Lửa (màu đích 3)
            int drifted = 0;
            _bus.Subscribe<PetDnaChanged>(e => drifted++);
            LiveCaredFor(6.5);
            Assert.AreEqual(40, pet.Dna.FurHue, "Baby / Child chưa trượt DNA");
            LiveCaredFor(4);
            Assert.Greater(drifted, 0);
            int hue = pet.Dna.FurHue;
            Assert.IsTrue(hue > 40 && hue <= 52, "đi theo hướng gần hơn trên vòng màu (40 -> 64/0 -> 3): " + hue);
            Assert.DoesNotThrow(() => DnaCodec.Encode(pet.Dna), "DNA sau khi trượt vẫn hợp lệ");
        }

        [Test]
        public void Stats_FollowG8()
        {
            StatsConfig c = _cfg.Stats;
            PetStats baby = StatsRules.Compute(EvolutionStage.Baby, AdultForm.None, 1f, c);
            Assert.AreEqual(100f, baby.Hp);
            Assert.AreEqual(10f, baby.Atk);
            Assert.AreEqual(5f, baby.Crit);
            Assert.AreEqual(0.25f * 100 + 2 * 10 + 1.5f * 10 + 1.5f * 10 + 10 * 5, baby.Power, 1e-3f);
            PetStats fire = StatsRules.Compute(EvolutionStage.Adult, AdultForm.Fire, 1f, c);
            Assert.AreEqual(10f * 3.2f * 1.25f, fire.Atk, 1e-3f, "Lửa +25% ATK");
            Assert.AreEqual(10f * 3.2f, fire.Def, 1e-3f);
            Assert.AreEqual(15f, StatsRules.Compute(EvolutionStage.Adult, AdultForm.Shadow, 1f, c).Crit, "Bóng Tối +10 Crit");
            Assert.AreEqual(100f * 4.2f * 1.25f, StatsRules.Compute(EvolutionStage.Ultimate, AdultForm.Light, 1f, c).Hp, 1e-2f);
            PetStats sick = StatsRules.Compute(EvolutionStage.Adult, AdultForm.Steel, 0.6f, c);
            Assert.AreEqual(10f * 3.2f * 1.25f * 0.6f, sick.Def, 1e-3f, "ốm -40%");
            Assert.AreEqual(5f, sick.Crit, "bệnh không giảm chí mạng");
        }

        [Test]
        public void ServiceStats_UseHealthFactor()
        {
            float power = _evo.Power;
            _pets.SetNeglectForDebug(30);   // Mệt: -30%
            Assert.Less(_evo.Power, power);
            Assert.AreEqual(70f, _evo.Stats.Hp, 1e-3f);
        }

        [Test]
        public void SaveLoad_KeepsGrowth_AndMemorialKeepsForm()
        {
            LiveCaredFor(14.5);
            AdultForm form = _evo.Form;
            _pets.Save();   // game lưu khi tạm dừng / thoát app
            var again = new PetService(_time, new SeededRandom(5), _save, _bus, _cfg);
            again.LoadOrCreate("Khác");
            Assert.AreEqual(EvolutionStage.Adult, again.Current.Growth.Stage);
            Assert.AreEqual(form, again.Current.Growth.Form);
            Assert.Greater(again.Current.Growth.CareHours.Count, 100);
            _pets.SetNeglectForDebug(500);
            Assert.AreEqual(EvolutionStage.Adult, _pets.Memorials[0].Stage);
            Assert.AreEqual(form, _pets.Memorials[0].Form);
        }
    }
}
