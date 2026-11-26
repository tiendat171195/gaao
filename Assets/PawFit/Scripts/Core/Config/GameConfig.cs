namespace PawFit.Core
{
    /// <summary>
    /// Mọi con số cân bằng game ở 1 chỗ. Hiện lấy giá trị mặc định trong code;
    /// sau này đọc từ file JSON để chỉnh số mà không phải sửa code.
    /// </summary>
    public sealed class GameConfig
    {
        public NeedsConfig Needs = new NeedsConfig();
        public HealthConfig Health = new HealthConfig();

        public static GameConfig CreateDefault() => new GameConfig();
    }

    /// <summary>4 nhu cầu ẩn (thang 0–100), giảm theo giờ thực — GDD mục G1, G2.</summary>
    public sealed class NeedsConfig
    {
        /// <summary>No bụng giảm mỗi giờ (~25 giờ từ đầy tới cạn).</summary>
        public float SatietyDecayPerHour = 4f;

        /// <summary>Sạch sẽ giảm mỗi giờ (~33 giờ).</summary>
        public float HygieneDecayPerHour = 3f;

        /// <summary>Năng lượng giảm mỗi giờ khi thức.</summary>
        public float EnergyDecayPerHour = 4f;

        /// <summary>Năng lượng hồi mỗi giờ khi ngủ.</summary>
        public float EnergyRegenPerHour = 15f;

        /// <summary>Vui vẻ giảm mỗi giờ.</summary>
        public float MoodDecayPerHour = 3.5f;

        /// <summary>"Chăm đủ" = cả 4 nhu cầu >= ngưỡng này cùng lúc. Chăm đủ thì đồng hồ bỏ bê về 0 (GDD G2).</summary>
        public float FullCareThreshold = 60f;

        /// <summary>Giá trị 4 nhu cầu lúc thú mới nở.</summary>
        public float StartValue = 80f;
    }

    /// <summary>Bệnh / chết / trứng — GDD mục G2 (bỏ bê), G3 (bệnh), G4 (chữa), G5 (chết + trứng).</summary>
    public sealed class HealthConfig
    {
        // ---- G3: bỏ bê liên tục bao nhiêu giờ thì sang giai đoạn nào ----
        public float TiredAfterHours = 24f;
        public float SickAfterHours = 72f;
        public float CriticalAfterHours = 120f;
        public float DeathAfterHours = 168f;

        /// <summary>Hệ số chỉ số khi Mệt / Ốm / Nguy kịch (-30% / -40% / -60%). Dùng ở Phase 2.2.</summary>
        public float TiredStatFactor = 0.7f;
        public float SickStatFactor = 0.6f;
        public float CriticalStatFactor = 0.4f;

        // ---- G4: chữa ----
        /// <summary>Ốm / Nguy kịch hồi phục đủ bấy nhiêu giờ thì khỏi.</summary>
        public float SickRecoveryHours = 24f;
        public float CriticalRecoveryHours = 48f;

        /// <summary>Cháo + chăn + lau người xong trong vòng bấy nhiêu giờ thì tính là 1 lượt chăm kỹ.</summary>
        public float CarefulRoundWindowHours = 12f;

        /// <summary>Mỗi lượt chăm kỹ (hoặc thuốc + chăm thường) giữ cho thú hồi phục tiếp trong bấy nhiêu giờ.</summary>
        public float CareCoverHours = 12f;

        /// <summary>Đang hồi phục mà quá bấy nhiêu giờ không ai chăm thì tái phát.</summary>
        public float RelapseAfterHours = 24f;

        /// <summary>Giá thuốc (Coin) — Phase 5.1 mới trừ tiền thật.</summary>
        public int MedicineCostSick = 60;
        public int MedicineCostCritical = 150;

        // ---- G5: trứng ----
        /// <summary>Thú mất bấy nhiêu giờ sau thì trứng mới nở được.</summary>
        public float EggAfterDeathHours = 24f;

        /// <summary>Tính bù thời gian vắng mặt theo từng bước bấy nhiêu giờ (đủ mịn để biết đúng lúc nhu cầu tụt).</summary>
        public float SimulationStepHours = 0.25f;
    }
}
