using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>
    /// Tác dụng chăm sóc của đồ đã mua (Phase 8.4): nhu cầu hao chậm hơn / ngủ hồi năng lượng nhanh hơn.
    /// Mặc định (default) = không có tác dụng gì. Dùng chung cho mô phỏng trong game, lúc vắng mặt và lúc hẹn thông báo.
    /// </summary>
    public struct CarePerks
    {
        /// <summary>Hao chậm đi bấy nhiêu phần (0,15 = chậm 15%).</summary>
        public float SatietyCut, HygieneCut, EnergyCut, MoodCut;

        /// <summary>Ngủ hồi năng lượng nhanh hơn bấy nhiêu phần (0,25 = nhanh hơn 25%).</summary>
        public float SleepBoost;

        public bool Any => SatietyCut > 0f || HygieneCut > 0f || EnergyCut > 0f || MoodCut > 0f || SleepBoost > 0f;

        public float CutOf(NeedType need)
        {
            switch (need)
            {
                case NeedType.Satiety: return SatietyCut;
                case NeedType.Hygiene: return HygieneCut;
                case NeedType.Energy: return EnergyCut;
                default: return MoodCut;
            }
        }

        /// <summary>Tính từ đồ trang trí đang có trong vườn + phụ kiện đang đeo.</summary>
        public static CarePerks From(CosmeticsData owned, PerksConfig cfg)
        {
            var p = new CarePerks();
            if (owned == null || cfg == null) return p;
            if (Has(owned, DecorKind.Bench)) p.EnergyCut += cfg.BenchEnergyCut;
            if (Has(owned, DecorKind.FlowerArch)) p.MoodCut += cfg.FlowerArchMoodCut;
            if (Has(owned, DecorKind.Lanterns)) p.SleepBoost += cfg.LanternsSleepBoost;
            switch (owned.Equipped)
            {
                case AccessoryKind.Bow: p.MoodCut += cfg.BowMoodCut; break;
                case AccessoryKind.Beanie: p.EnergyCut += cfg.BeanieEnergyCut; break;
                case AccessoryKind.Scarf: p.SatietyCut += cfg.ScarfSatietyCut; break;
                case AccessoryKind.Crown:
                    p.SatietyCut += cfg.CrownAllCut;
                    p.HygieneCut += cfg.CrownAllCut;
                    p.EnergyCut += cfg.CrownAllCut;
                    p.MoodCut += cfg.CrownAllCut;
                    break;
            }
            float max = cfg.MaxCut < 0f ? 0f : (cfg.MaxCut > 1f ? 1f : cfg.MaxCut);
            p.SatietyCut = Clamp(p.SatietyCut, max);
            p.HygieneCut = Clamp(p.HygieneCut, max);
            p.EnergyCut = Clamp(p.EnergyCut, max);
            p.MoodCut = Clamp(p.MoodCut, max);
            if (p.SleepBoost < 0f) p.SleepBoost = 0f;
            return p;
        }

        private static bool Has(CosmeticsData owned, DecorKind kind) => owned.Decor != null && owned.Decor.Contains(kind);

        private static float Clamp(float v, float max) => v < 0f ? 0f : (v > max ? max : v);
    }
}
