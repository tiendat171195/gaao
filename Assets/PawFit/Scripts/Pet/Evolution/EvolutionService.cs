using System;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Thú vừa lên giai đoạn mới (Child / Teen / Adult / Ultimate).</summary>
    public readonly struct PetEvolved
    {
        public readonly PetData Pet;
        public readonly EvolutionStage From;
        public readonly EvolutionStage To;
        public readonly AdultForm Form;

        public PetEvolved(PetData pet, EvolutionStage from, EvolutionStage to, AdultForm form)
        {
            Pet = pet;
            From = from;
            To = to;
            Form = form;
        }
    }

    /// <summary>DNA vừa trượt 1 bước về dạng đang nghiêng (màu lông / tỉ lệ đổi nhẹ) — View dựng lại hình.</summary>
    public readonly struct PetDnaChanged
    {
        public readonly PetData Pet;
        public PetDnaChanged(PetData pet) { Pet = pet; }
    }

    public enum WorkoutResult { Ok, NoPet, Sick, TooTired, DailyLimit }

    /// <summary>
    /// Cổng gameplay cho tiến hóa + chỉ số (C# thuần): cộng điểm tập (có giới hạn mỗi ngày), tập sức mạnh ở sân tập,
    /// thuộc chiêu (Phase 3.2), đọc giai đoạn / dạng / CareScore / hình dáng / chỉ số / lực chiến.
    /// Tự gắn vào PetService để chạy mỗi bước thời gian.
    /// </summary>
    public sealed class EvolutionService : IPetStepper
    {
        private readonly PetService _pets;
        private readonly HealthService _health;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly GameConfig _cfg;

        public EvolutionService(PetService pets, HealthService health, ITimeProvider time, EventBus bus, GameConfig cfg)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _pets.AddStepper(this);
        }

        private PetGrowth Growth => _pets.HasPet ? _pets.Current.Growth : null;

        public EvolutionStage Stage => Growth?.Stage ?? EvolutionStage.Baby;
        public AdultForm Form => Growth?.Form ?? AdultForm.None;
        public AdultForm LeaningForm => _pets.HasPet ? EvolutionRules.Leaning(_pets.Current, _cfg.Evolution) : AdultForm.None;
        public float CareScore => EvolutionRules.CareScore(Growth);
        public TrainingVector Training => Growth?.Training;
        public int LearnedSkills => Growth?.LearnedSkills ?? 0;
        public bool CanUltimate => _pets.HasPet && EvolutionRules.CanUltimate(_pets.Current, _time.UtcNow, _cfg.Evolution);

        /// <summary>Hình dáng hiện tại (trọng số blendshape tiến hóa).</summary>
        public GrowthLook Look => _pets.HasPet ? EvolutionRules.Look(_pets.Current, _time.UtcNow, _cfg.Evolution) : new GrowthLook();

        /// <summary>Chỉ số hiện tại (đã nhân hệ số bệnh).</summary>
        public PetStats Stats => StatsRules.Compute(Stage, Form, _pets.HasPet ? _health.StatFactor : 0f, _cfg.Stats);

        public float Power => Stats.Power;

        /// <summary>Cộng điểm tập chủ động (chăm, tập, chạy...). Mỗi hướng tối đa DailyTrainingCap / ngày tuổi. Trả số điểm thật sự cộng.</summary>
        public float AddTraining(TrainingAxis axis, float points)
        {
            PetGrowth g = Growth;
            if (g == null || points <= 0f) return 0f;
            int day = (int)Math.Floor(_pets.Current.AgeDays(_time.UtcNow));
            if (day != g.TrainingDay)
            {
                g.TrainingDay = day;
                g.TrainedToday.Clear();
            }
            float room = Math.Max(0f, _cfg.Evolution.DailyTrainingCap - g.TrainedToday.Get(axis));
            float add = Math.Min(points, room);
            if (add <= 0f) return 0f;
            g.TrainedToday.Add(axis, add);
            g.Training.Add(axis, add);
            return add;
        }

        /// <summary>Còn tập được hướng này hôm nay không.</summary>
        public bool HasRoomToday(TrainingAxis axis)
        {
            PetGrowth g = Growth;
            if (g == null) return false;
            int day = (int)Math.Floor(_pets.Current.AgeDays(_time.UtcNow));
            return day != g.TrainingDay || g.TrainedToday.Get(axis) < _cfg.Evolution.DailyTrainingCap - 1e-3f;
        }

        /// <summary>
        /// Tập sức mạnh ở sân tập (đấm hình nộm): ốm thì không tập (G3), quá mệt thì từ chối, hết giới hạn ngày thì từ chối.
        /// Tập tốn năng lượng + no bụng.
        /// </summary>
        public WorkoutResult TryWorkout()
        {
            if (!_pets.HasPet) return WorkoutResult.NoPet;
            _pets.Tick();
            if (!_pets.HasPet) return WorkoutResult.NoPet;
            if (!_health.CanTrain) return WorkoutResult.Sick;
            EvolutionConfig c = _cfg.Evolution;
            if (_pets.Current.Needs.Energy < c.WorkoutMinEnergy) return WorkoutResult.TooTired;
            if (!HasRoomToday(TrainingAxis.Strength)) return WorkoutResult.DailyLimit;
            AddTraining(TrainingAxis.Strength, c.WorkoutStrength);
            _pets.Spend(NeedType.Energy, c.WorkoutEnergyCost);
            _pets.Spend(NeedType.Satiety, c.WorkoutSatietyCost);
            return WorkoutResult.Ok;
        }

        /// <summary>Thuộc thêm 1 chiêu (Phase 3.2 gọi khi vuốt đúng 10 lần). Tối đa 4.</summary>
        public void LearnSkill()
        {
            PetGrowth g = Growth;
            if (g != null && g.LearnedSkills < _cfg.Evolution.UltimateSkillsNeeded) g.LearnedSkills++;
        }

        void IPetStepper.Step(PetData pet, double hours, DateTime endUtc)
        {
            EvolutionStage before = pet.Growth.Stage;
            EvolutionChange change = EvolutionRules.Step(pet, hours, endUtc, _cfg);
            if (change == EvolutionChange.Evolved)
                _bus.Publish(new PetEvolved(pet, before, pet.Growth.Stage, pet.Growth.Form));
            else if (change == EvolutionChange.DnaDrifted)
                _bus.Publish(new PetDnaChanged(pet));
        }
    }
}
