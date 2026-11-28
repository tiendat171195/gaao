using System;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>4 nhu cầu ẩn của thú.</summary>
    public enum NeedType { Satiety = 0, Hygiene = 1, Energy = 2, Mood = 3 }

    /// <summary>
    /// 4 nhu cầu ẩn (thang 0–100). Người chơi KHÔNG thấy con số —
    /// View chỉ đọc để vẽ trạng thái bằng hình (đói nhìn chòi ăn, bẩn có ruồi, buồn ngủ mắt díp...).
    /// </summary>
    public sealed class PetNeeds
    {
        public const float Max = 100f;

        /// <summary>No bụng.</summary>
        public float Satiety;

        /// <summary>Sạch sẽ.</summary>
        public float Hygiene;

        /// <summary>Năng lượng.</summary>
        public float Energy;

        /// <summary>Vui vẻ.</summary>
        public float Mood;

        public static PetNeeds Filled(float value) =>
            new PetNeeds { Satiety = value, Hygiene = value, Energy = value, Mood = value };

        public float Get(NeedType type)
        {
            switch (type)
            {
                case NeedType.Satiety: return Satiety;
                case NeedType.Hygiene: return Hygiene;
                case NeedType.Energy: return Energy;
                case NeedType.Mood: return Mood;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Set(NeedType type, float value)
        {
            float v = Clamp(value);
            switch (type)
            {
                case NeedType.Satiety: Satiety = v; break;
                case NeedType.Hygiene: Hygiene = v; break;
                case NeedType.Energy: Energy = v; break;
                case NeedType.Mood: Mood = v; break;
                default: throw new ArgumentOutOfRangeException(nameof(type));
            }
        }

        public void Add(NeedType type, float delta) => Set(type, Get(type) + delta);

        /// <summary>Nhu cầu thấp nhất (để biết thú đang "khổ" nhất ở mặt nào).</summary>
        public NeedType LowestNeed
        {
            get
            {
                NeedType best = NeedType.Satiety;
                for (int i = 1; i < 4; i++)
                    if (Get((NeedType)i) < Get(best)) best = (NeedType)i;
                return best;
            }
        }

        public bool AllAtLeast(float threshold) =>
            Satiety >= threshold && Hygiene >= threshold && Energy >= threshold && Mood >= threshold;

        public PetNeeds Clone() => (PetNeeds)MemberwiseClone();

        private static float Clamp(float v) => v < 0f ? 0f : (v > Max ? Max : v);
    }

    /// <summary>Tính nhu cầu sau 1 khoảng thời gian — dùng cả khi đang chơi lẫn khi tắt app quay lại.</summary>
    public static class NeedsSimulator
    {
        /// <param name="perks">Tác dụng của đồ đã mua (Phase 8.4): hao chậm hơn / ngủ hồi nhanh hơn. Mặc định = không có.</param>
        public static void Advance(PetNeeds needs, double hours, bool sleeping, NeedsConfig cfg, CarePerks perks = default)
        {
            if (needs == null) throw new ArgumentNullException(nameof(needs));
            if (hours <= 0) return;
            float h = (float)hours;
            needs.Add(NeedType.Satiety, -cfg.SatietyDecayPerHour * (1f - perks.SatietyCut) * h);
            needs.Add(NeedType.Hygiene, -cfg.HygieneDecayPerHour * (1f - perks.HygieneCut) * h);
            needs.Add(NeedType.Mood, -cfg.MoodDecayPerHour * (1f - perks.MoodCut) * h);
            needs.Add(NeedType.Energy, sleeping ? cfg.EnergyRegenPerHour * (1f + perks.SleepBoost) * h
                                                : -cfg.EnergyDecayPerHour * (1f - perks.EnergyCut) * h);
        }
    }
}
