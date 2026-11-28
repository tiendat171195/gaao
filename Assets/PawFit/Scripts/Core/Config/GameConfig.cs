using System;

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
        public EconomyConfig Economy = new EconomyConfig();
        public RankConfig Rank = new RankConfig();
        public MatchmakingConfig Matchmaking = new MatchmakingConfig();
        public PerksConfig Perks = new PerksConfig();

        public static GameConfig CreateDefault() => new GameConfig();
    }

    /// <summary>
    /// Tác dụng của đồ đã mua (Phase 8.4) — GDD G18. "Cắt" = nhu cầu hao chậm đi bấy nhiêu phần (0,15 = chậm 15%).
    /// Chỉ đổi chuyện chăm sóc trong vườn, KHÔNG đổi chỉ số trận (PvP công bằng). Áp dụng cả lúc tắt app (tính bù) và lúc hẹn thông báo.
    /// </summary>
    public sealed class PerksConfig
    {
        // ---- đồ trang trí: có trong vườn là có tác dụng ----
        /// <summary>Ghế gỗ: bé hay ra ghế ngồi nghỉ => năng lượng (lúc thức) hao chậm hơn.</summary>
        public float BenchEnergyCut = 0.15f;

        /// <summary>Vòm hoa: hoa thơm, bướm bay quanh vườn => vui hao chậm hơn.</summary>
        public float FlowerArchMoodCut = 0.15f;

        /// <summary>Dây đèn lồng: ánh đèn ấm, bé ngủ ngon => ngủ hồi năng lượng nhanh hơn bấy nhiêu phần.</summary>
        public float LanternsSleepBoost = 0.25f;

        // ---- phụ kiện: chỉ món đang đeo ----
        /// <summary>Nơ: bé thấy mình xinh => vui hao chậm hơn.</summary>
        public float BowMoodCut = 0.1f;

        /// <summary>Mũ len: ấm đầu => năng lượng hao chậm hơn.</summary>
        public float BeanieEnergyCut = 0.1f;

        /// <summary>Khăn quàng: ấm bụng => no hao chậm hơn.</summary>
        public float ScarfSatietyCut = 0.1f;

        /// <summary>Vương miện: cả 4 nhu cầu hao chậm hơn 1 chút.</summary>
        public float CrownAllCut = 0.05f;

        /// <summary>Nhiều món cộng dồn thì mỗi nhu cầu cắt tối đa bấy nhiêu.</summary>
        public float MaxCut = 0.4f;
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

        /// <summary>Giá thuốc (Coin) — bán ở quầy hàng trong vườn (ShopService, Phase 5.1).</summary>
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

        /// <summary>Mode Huấn luyện: vuốt đúng chiêu bấy nhiêu lần thì thuộc chiêu đó (GDD).</summary>
        public int PracticeToLearn = 10;
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

    /// <summary>Kinh tế — GDD mục G11 (Phase 5.1). Không nạp tiền; giá thuốc nằm ở <see cref="HealthConfig"/> (60 / 150).</summary>
    public sealed class EconomyConfig
    {
        public int StartCoins = 200;

        /// <summary>Ví giữ tối đa bấy nhiêu Coin (chặn tràn số).</summary>
        public int MaxCoins = 999999;

        // ---- quảng cáo tự nguyện ----
        public int MaxAdsPerDay = 5;
        public int AdCoins = 20;
        public float AdEnergy = 20f;

        /// <summary>Năng lượng từ mức này trở lên thì không cho xem quảng cáo lấy năng lượng (tránh phí lượt).</summary>
        public float AdEnergyFullAbove = 90f;

        /// <summary>Quảng cáo giả lập (MockAdService) dài bấy nhiêu giây.</summary>
        public float MockAdSeconds = 3f;

        // ---- quầy hàng trong vườn (thức ăn thường ở chòi ăn vẫn miễn phí) ----
        public int TreatPrice = 25;
        public float TreatSatiety = 15f;
        public float TreatMood = 25f;
        public int EnergyDrinkPrice = 30;
        public float EnergyDrinkEnergy = 30f;

        /// <summary>No (bánh thưởng) / năng lượng (nước tăng lực) từ mức này trở lên thì quầy không bán: bé không cần.</summary>
        public float ShopFullAbove = 92f;

        // ---- phần thưởng ----
        /// <summary>Thắng hình nộm Dễ / Thường / Khó được bấy nhiêu Coin.</summary>
        public int[] BotWinCoins = { 5, 10, 15 };

        /// <summary>Thắng PvP (Phase 4.2).</summary>
        public int PvpWinCoins = 30;

        // ---- đồ trang trí / phụ kiện (Phase 5.2), chỉ để đẹp ----
        /// <summary>Giá phụ kiện theo thứ tự AccessoryKind: Nơ, Mũ len, Khăn quàng, Vương miện.</summary>
        public int[] AccessoryPrices = { 80, 120, 100, 250 };

        /// <summary>Giá trang trí vườn theo thứ tự DecorKind: Đèn lồng, Ghế gỗ, Cối xay gió, Vòm hoa.</summary>
        public int[] DecorPrices = { 150, 120, 300, 200 };

        /// <summary>Giá đồ chơi theo thứ tự ToyKind: Bóng, Cần lông vũ, Máy bong bóng (Phase 8.3).</summary>
        public int[] ToyPrices = { 60, 90, 140 };
    }

    /// <summary>Rank Elo + bậc + mùa giải — GDD mục G10 (Phase 4.3).</summary>
    public sealed class RankConfig
    {
        public int StartElo = 1000;
        public int K = 32;

        /// <summary>Bronze &lt; 1100 ≤ Silver &lt; 1300 ≤ Gold &lt; 1500 ≤ Diamond &lt; 1700 ≤ Master.</summary>
        public int SilverAt = 1100;
        public int GoldAt = 1300;
        public int DiamondAt = 1500;
        public int MasterAt = 1700;

        public int SeasonDays = 30;

        /// <summary>Mùa 1 bắt đầu lúc này (UTC); mùa n bắt đầu sau đó (n - 1) × 30 ngày.</summary>
        public DateTime SeasonEpochUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        /// <summary>Hết mùa: Elo mới = 1000 + (Elo - 1000) × 0.5 (reset mềm).</summary>
        public float SoftResetFactor = 0.5f;
    }

    /// <summary>Ghép trận PvP — GDD mục 1 + G10 (Phase 4.2).</summary>
    public sealed class MatchmakingConfig
    {
        /// <summary>Lúc đầu: chênh Elo tối đa ±100 và lực chiến tối đa ±10%.</summary>
        public int EloWindow = 100;
        public float PowerWindow = 0.10f;

        /// <summary>Tìm bấy nhiêu giây chưa có trận thì nới khoảng (tôi tự đặt: ±200 Elo, ±20% lực chiến).</summary>
        public float WidenAfterSeconds = 30f;
        public int WidenedEloWindow = 200;
        public float WidenedPowerWindow = 0.20f;

        /// <summary>Tìm bấy nhiêu giây vẫn chưa có thì gợi ý đấu Bot (vẫn tìm tiếp).</summary>
        public float SuggestBotAfterSeconds = 60f;
    }
}
