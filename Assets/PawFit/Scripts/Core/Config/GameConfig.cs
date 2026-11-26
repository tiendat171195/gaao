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
        public EvolutionConfig Evolution = new EvolutionConfig();
        public StatsConfig Stats = new StatsConfig();

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

    /// <summary>Tiến hóa — GDD mục G7. Điểm tập (TrainingVector) có 4 hướng: Sức mạnh / Tốc độ / Chăm sóc / Bóng tối.</summary>
    public sealed class EvolutionConfig
    {
        // ---- giai đoạn theo tuổi (ngày) ----
        public float ChildAtDays = 3f;
        public float TeenAtDays = 7f;
        public float AdultAtDays = 14f;
        public float UltimateAtDays = 30f;

        // ---- CareScore: trung bình mức chăm sóc của 7 ngày gần nhất (0..100) ----
        public int CareWindowHours = 168;

        /// <summary>Giờ đang ốm chỉ tính bấy nhiêu phần điểm chăm.</summary>
        public float SickCareFactor = 0.5f;

        // ---- chọn dạng Adult ----
        public float LightMinCareScore = 70f;
        public float ShadowMaxCareScore = 40f;

        /// <summary>Hướng tập cao nhất chiếm không quá tỉ lệ này của tổng => tập cân bằng => Thép.</summary>
        public float BalancedTopShare = 0.32f;

        /// <summary>Nhiễu theo seed: mỗi hướng ×(1 ± 0.1). Tính cách: hướng hợp tính +15%. Tổng nhiễu 10–20% (G7).</summary>
        public float SeedNoise = 0.1f;
        public float PersonalityBonus = 0.15f;

        // ---- Ultimate ----
        public float UltimateMinCareScore = 80f;
        public int UltimateSkillsNeeded = 4;
        public float UltimateHealthyDays = 7f;

        // ---- nguồn điểm tập ----
        /// <summary>Mỗi hướng tập chủ động (chăm, tập, chạy) cộng tối đa bấy nhiêu điểm 1 ngày.</summary>
        public float DailyTrainingCap = 30f;
        public float ShadowPerNeglectHour = 0.4f;
        public float ShadowPerSickHour = 1f;
        public float CareFeed = 2f;
        public float CareBathe = 3f;
        public float CarePet = 0.5f;
        public float CareWipe = 1f;
        public float CareSleep = 1f;
        public float WorkoutStrength = 6f;
        public float WorkoutEnergyCost = 8f;
        public float WorkoutSatietyCost = 5f;
        public float WorkoutMinEnergy = 20f;
        public float SpeedPerMeterLed = 0.08f;

        // ---- hình dáng theo giai đoạn (tổng trọng số blendshape tiến hóa) ----
        public float ChildGrowth = 0.35f;
        public float TeenGrowth = 0.75f;
        public float UltimateScale = 1.08f;

        // ---- DNA trượt dần về dạng đang nghiêng (từ Teen, mỗi ngày 1 bước) ----
        public int DriftHueStep = 3;
    }

    /// <summary>Chỉ số + lực chiến — GDD mục G8.</summary>
    public sealed class StatsConfig
    {
        public float BaseHp = 100f;
        public float BaseAtk = 10f;
        public float BaseDef = 10f;
        public float BaseSpd = 10f;

        /// <summary>Tỉ lệ chí mạng gốc (%).</summary>
        public float BaseCrit = 5f;

        /// <summary>Hệ số theo giai đoạn: Baby / Child / Teen / Adult / Ultimate.</summary>
        public float[] StageMultiplier = { 1f, 1.5f, 2.2f, 3.2f, 4.2f };

        /// <summary>Dạng Adult: Lửa +25% ATK, Nước +25% SPD, Ánh Sáng +25% HP, Thép +25% DEF, Bóng Tối +10 điểm Crit.</summary>
        public float FormBonus = 0.25f;
        public float ShadowCritBonus = 10f;

        // Lực chiến = 0.25·HP + 2·ATK + 1.5·DEF + 1.5·SPD + 10·Crit%
        public float PowerHp = 0.25f;
        public float PowerAtk = 2f;
        public float PowerDef = 1.5f;
        public float PowerSpd = 1.5f;
        public float PowerCrit = 10f;
    }
}
