using System;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>
    /// Cổng gameplay cho sức khỏe (C# thuần): đọc giai đoạn bệnh, ghi nhận việc chăm kỹ (cháo / chăn / lau),
    /// cho uống thuốc, cho biết được tập / đấu Rank / tắm không. Luật nằm ở <see cref="HealthRules"/>.
    /// </summary>
    public sealed class HealthService
    {
        private readonly PetService _pets;
        private readonly ITimeProvider _time;
        private readonly GameConfig _cfg;

        public HealthService(PetService pets, ITimeProvider time, GameConfig cfg)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        private PetHealth Health => _pets.HasPet ? _pets.Current.Health : null;

        /// <summary>Không có thú sống thì coi như Dead.</summary>
        public HealthStage Stage => Health?.Stage ?? HealthStage.Dead;

        public bool IsIll => HealthRules.IsIll(Stage);
        public bool IsRecovering => Health?.Recovery != null;
        public float RecoveryProgress01 => HealthRules.RecoveryProgress01(Health);
        public double NeglectHours => Health?.NeglectHours ?? 0.0;

        /// <summary>Số việc chăm kỹ đã làm trong lượt hiện tại (0..3).</summary>
        public int CarefulMarksDone => HealthRules.CarefulMarksDone(Health, _time.UtcNow, _cfg.Health);

        /// <summary>Hệ số chỉ số (Phase 2.2 nhân vào HP / ATK / ...).</summary>
        public float StatFactor => HealthRules.StatFactor(Stage, _cfg.Health);

        /// <summary>Ốm / Nguy kịch thì cấm tập luyện + đấu Rank (Phase 3 / 4 hỏi ở đây).</summary>
        public bool CanTrain => _pets.HasPet && HealthRules.CanTrain(Stage);

        /// <summary>Thú ốm không chịu xuống hồ tắm (GDD G4).</summary>
        public bool CanBathe => _pets.HasPet && !IsIll;

        /// <summary>Giá thuốc theo giai đoạn (quầy hàng trong vườn bán theo giá này).</summary>
        public int MedicineCost => Stage == HealthStage.Critical ? _cfg.Health.MedicineCostCritical : _cfg.Health.MedicineCostSick;

        /// <summary>Ghi nhận 1 việc chăm kỹ khi thú đang ốm. Trả true nếu vừa đủ 1 lượt (cháo + chăn + lau).</summary>
        public bool MarkCarefulCare(CarefulCareKind kind)
        {
            if (!_pets.HasPet) return false;
            _pets.Tick();
            if (!_pets.HasPet || !IsIll) return false;
            PetData pet = _pets.Current;
            HealthStage before = pet.Health.Stage;
            HealthChange change = HealthRules.MarkCarefulCare(pet.Health, kind, _time.UtcNow, _cfg.Health);
            if (change != HealthChange.None) _pets.PublishHealth(pet, before, change);
            return change == HealthChange.RecoveryStarted || change == HealthChange.CarefulRoundDone;
        }

        /// <summary>Cho uống thuốc (đang ốm mới có tác dụng). Mua ở quầy hàng: ShopService trừ <see cref="MedicineCost"/> Coin rồi mới gọi hàm này.</summary>
        public bool GiveMedicine()
        {
            if (!_pets.HasPet) return false;
            _pets.Tick();
            if (!_pets.HasPet || !IsIll) return false;
            PetData pet = _pets.Current;
            HealthStage before = pet.Health.Stage;
            HealthChange change = HealthRules.GiveMedicine(pet.Health, _time.UtcNow, _cfg.Health);
            if (change != HealthChange.None) _pets.PublishHealth(pet, before, change);
            return true;
        }
    }
}
