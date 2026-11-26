using System;
using System.Collections.Generic;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Giai đoạn tiến hóa (GDD: Baby ngày 0 → Child 3 → Teen 7 → Adult 14 → Ultimate 30 nếu đủ điều kiện).</summary>
    public enum EvolutionStage { Baby = 0, Child = 1, Teen = 2, Adult = 3, Ultimate = 4 }

    /// <summary>5 dạng Adult của bộ Gen 1. None = chưa tới Adult.</summary>
    public enum AdultForm { None = 0, Fire = 1, Water = 2, Light = 3, Steel = 4, Shadow = 5 }

    /// <summary>4 hướng tập (TrainingVector).</summary>
    public enum TrainingAxis { Strength = 0, Speed = 1, Care = 2, Shadow = 3 }

    public enum EvolutionChange { None, Evolved, DnaDrifted }

    /// <summary>Điểm tập 4 hướng.</summary>
    public sealed class TrainingVector
    {
        public float Strength;
        public float Speed;
        public float Care;
        public float Shadow;

        public float Get(TrainingAxis a)
        {
            switch (a)
            {
                case TrainingAxis.Strength: return Strength;
                case TrainingAxis.Speed: return Speed;
                case TrainingAxis.Care: return Care;
                default: return Shadow;
            }
        }

        public void Add(TrainingAxis a, float v)
        {
            switch (a)
            {
                case TrainingAxis.Strength: Strength += v; break;
                case TrainingAxis.Speed: Speed += v; break;
                case TrainingAxis.Care: Care += v; break;
                default: Shadow += v; break;
            }
        }

        public float Sum => Strength + Speed + Care + Shadow;

        public void Clear() => Strength = Speed = Care = Shadow = 0f;

        public override string ToString() =>
            "Sức mạnh " + Strength.ToString("0") + " · Tốc độ " + Speed.ToString("0") + " · Chăm sóc " + Care.ToString("0") +
            " · Bóng tối " + Shadow.ToString("0");
    }

    /// <summary>Dữ liệu lớn lên của 1 con thú (lưu trong file).</summary>
    public sealed class PetGrowth
    {
        public EvolutionStage Stage = EvolutionStage.Baby;

        /// <summary>Dạng Adult đã chốt lúc lên Adult (None khi chưa tới).</summary>
        public AdultForm Form = AdultForm.None;

        public TrainingVector Training = new TrainingVector();

        /// <summary>Điểm tập chủ động trong ngày tuổi `TrainingDay` (để giới hạn mỗi ngày).</summary>
        public TrainingVector TrainedToday = new TrainingVector();
        public int TrainingDay;

        /// <summary>Mức chăm sóc từng giờ (0..100) của tối đa 168 giờ gần nhất => CareScore.</summary>
        public List<float> CareHours = new List<float>();
        public double PartialCare;
        public double PartialHours;

        /// <summary>Số chiêu đã thuộc (0..4) — tính lại từ SkillProgress mỗi khi tập.</summary>
        public int LearnedSkills;

        /// <summary>Số lần vuốt đúng của 4 chiêu: 0 = ngang (Nhanh), 1 = lên (Mạnh), 2 = V (Riêng), 3 = Z (Ultimate).</summary>
        public int[] SkillProgress = new int[4];

        /// <summary>Lần gần nhất đang ốm (Ultimate cần 7 ngày không bệnh).</summary>
        public DateTime? LastIllUtc;

        /// <summary>DNA đã trượt về dạng đang nghiêng tới hết ngày tuổi này.</summary>
        public int DriftedDay;
    }

    /// <summary>Hình dáng theo giai đoạn: trọng số blendshape 5 dạng Adult (tổng = mức lớn) + Ultimate.</summary>
    public struct GrowthLook
    {
        public float Fire, Water, Light, Steel, Shadow;
        public bool Ultimate;

        public float Get(AdultForm f)
        {
            switch (f)
            {
                case AdultForm.Fire: return Fire;
                case AdultForm.Water: return Water;
                case AdultForm.Light: return Light;
                case AdultForm.Steel: return Steel;
                case AdultForm.Shadow: return Shadow;
                default: return 0f;
            }
        }

        public void Set(AdultForm f, float v)
        {
            switch (f)
            {
                case AdultForm.Fire: Fire = v; break;
                case AdultForm.Water: Water = v; break;
                case AdultForm.Light: Light = v; break;
                case AdultForm.Steel: Steel = v; break;
                case AdultForm.Shadow: Shadow = v; break;
            }
        }

        public float Total => Fire + Water + Light + Steel + Shadow;
    }

    /// <summary>Bước tính thêm cho mỗi khoảng thời gian (PetService gọi sau nhu cầu + sức khỏe). Dùng cho tiến hóa.</summary>
    public interface IPetStepper
    {
        void Step(PetData pet, double hours, DateTime endUtc);
    }

    /// <summary>
    /// Luật tiến hóa (C# thuần, không trạng thái) — GDD G7:
    /// giai đoạn theo tuổi; CareScore 7 ngày; dạng Adult theo hướng tập trội (+ tính cách, seed);
    /// từ Teen dáng + DNA trượt dần về dạng đang nghiêng; Ultimate khi đủ điều kiện.
    /// </summary>
    public static class EvolutionRules
    {
        public static readonly AdultForm[] Forms = { AdultForm.Fire, AdultForm.Water, AdultForm.Light, AdultForm.Steel, AdultForm.Shadow };

        public static EvolutionStage StageByAge(double ageDays, EvolutionConfig c) =>
            ageDays >= c.AdultAtDays ? EvolutionStage.Adult
            : ageDays >= c.TeenAtDays ? EvolutionStage.Teen
            : ageDays >= c.ChildAtDays ? EvolutionStage.Child
            : EvolutionStage.Baby;

        /// <summary>CareScore 0..100 = trung bình mức chăm sóc các giờ gần nhất (chưa đủ 1 giờ thì lấy giờ đang tính dở).</summary>
        public static float CareScore(PetGrowth g)
        {
            if (g == null) return 0f;
            double sum = 0;
            int n = 0;
            foreach (float h in g.CareHours)
            {
                sum += h;
                n++;
            }
            if (g.PartialHours > 1e-6)
            {
                double partial = g.PartialCare / g.PartialHours;
                double w = Math.Min(1.0, g.PartialHours);
                return (float)((sum + partial * w) / (n + w));
            }
            return n == 0 ? 100f : (float)(sum / n);
        }

        /// <summary>Điểm 4 hướng sau khi cộng tính cách (+15% hướng hợp tính) và nhiễu theo seed (±10%).</summary>
        public static float[] AxisScores(TrainingVector v, HiddenGenes hidden, EvolutionConfig c)
        {
            var s = new[] { v.Strength, v.Speed, v.Care, v.Shadow };
            uint seed = hidden != null ? hidden.Seed : 0u;
            var rng = new SeededRandom(seed, 77UL);
            for (int i = 0; i < 4; i++)
                s[i] *= 1f + (float)(rng.NextDouble() * 2.0 - 1.0) * c.SeedNoise;
            if (hidden != null)
            {
                float b = c.PersonalityBonus;
                switch (hidden.Personality)
                {
                    case Personality.Fiery: s[0] *= 1f + b; break;
                    case Personality.Playful: s[1] *= 1f + b; break;
                    case Personality.Calm: s[2] *= 1f + b; break;
                    case Personality.Moody: s[3] *= 1f + b; break;
                    case Personality.Proud: s[0] *= 1f + b * 0.6f; s[1] *= 1f + b * 0.4f; break;
                    case Personality.Curious: s[1] *= 1f + b * 0.6f; s[2] *= 1f + b * 0.4f; break;
                    case Personality.Shy: s[2] *= 1f + b * 0.6f; s[3] *= 1f + b * 0.4f; break;
                    case Personality.Lazy: s[3] *= 1f + b * 0.6f; s[2] *= 1f + b * 0.4f; break;
                }
            }
            return s;
        }

        /// <summary>
        /// Chọn dạng Adult (G7): CareScore &lt; 40 => Bóng Tối; tập cân bằng => Thép; Sức mạnh trội => Lửa;
        /// Tốc độ trội => Nước; Chăm sóc trội + CareScore ≥ 70 => Ánh Sáng (không đủ 70 thì theo hướng mạnh thứ 2);
        /// Bóng tối trội => Bóng Tối.
        /// </summary>
        public static AdultForm DecideForm(TrainingVector v, HiddenGenes hidden, float careScore, EvolutionConfig c)
        {
            if (careScore < c.ShadowMaxCareScore) return AdultForm.Shadow;
            float[] s = AxisScores(v, hidden, c);
            float sum = s[0] + s[1] + s[2] + s[3];
            if (sum <= 1e-4f) return AdultForm.Steel;
            int top = 0, second = -1;
            for (int i = 1; i < 4; i++)
                if (s[i] > s[top]) top = i;
            for (int i = 0; i < 4; i++)
                if (i != top && (second < 0 || s[i] > s[second])) second = i;
            if (s[top] / sum <= c.BalancedTopShare) return AdultForm.Steel;
            if (top == (int)TrainingAxis.Care && careScore < c.LightMinCareScore)   // chăm nhiều nhưng chưa đủ kỹ
                return s[second] / sum < 0.1f ? AdultForm.Steel : FormOf((TrainingAxis)second);
            return FormOf((TrainingAxis)top);
        }

        private static AdultForm FormOf(TrainingAxis a)
        {
            switch (a)
            {
                case TrainingAxis.Strength: return AdultForm.Fire;
                case TrainingAxis.Speed: return AdultForm.Water;
                case TrainingAxis.Care: return AdultForm.Light;
                default: return AdultForm.Shadow;
            }
        }

        /// <summary>Dạng đang nghiêng về (đã chốt thì là dạng đã chốt).</summary>
        public static AdultForm Leaning(PetData pet, EvolutionConfig c) =>
            pet.Growth.Form != AdultForm.None ? pet.Growth.Form
            : DecideForm(pet.Growth.Training, pet.Hidden, CareScore(pet.Growth), c);

        /// <summary>Đủ điều kiện Ultimate (G7): ≥ 30 ngày, CareScore ≥ 80, thuộc đủ 4 chiêu, 7 ngày không bệnh.</summary>
        public static bool CanUltimate(PetData pet, DateTime now, EvolutionConfig c)
        {
            PetGrowth g = pet.Growth;
            if (g.Stage != EvolutionStage.Adult || pet.AgeDays(now) < c.UltimateAtDays) return false;
            if (CareScore(g) < c.UltimateMinCareScore || g.LearnedSkills < c.UltimateSkillsNeeded) return false;
            double healthyDays = g.LastIllUtc.HasValue ? (now - g.LastIllUtc.Value).TotalDays : pet.AgeDays(now);
            return healthyDays >= c.UltimateHealthyDays;
        }

        /// <summary>
        /// Hình dáng theo tuổi: Baby = gốc; Child lớn đều (trung bình 5 dạng); Teen nghiêng dần về dạng đang nghiêng;
        /// Adult / Ultimate = đúng dạng đã chốt.
        /// </summary>
        public static GrowthLook Look(PetData pet, DateTime now, EvolutionConfig c)
        {
            var look = new GrowthLook();
            PetGrowth g = pet.Growth;
            double age = pet.AgeDays(now);
            if (g.Stage >= EvolutionStage.Adult && g.Form != AdultForm.None)
            {
                look.Set(g.Form, 1f);
                look.Ultimate = g.Stage == EvolutionStage.Ultimate;
                return look;
            }
            if (g.Stage == EvolutionStage.Teen)
            {
                float t = Clamp01((float)((age - c.TeenAtDays) / Math.Max(0.01, c.AdultAtDays - c.TeenAtDays)));
                float total = c.ChildGrowth + (c.TeenGrowth - c.ChildGrowth) * t;
                AdultForm lean = Leaning(pet, c);
                foreach (AdultForm f in Forms)
                    look.Set(f, f == lean ? total * (0.2f + 0.8f * t) : total * 0.2f * (1f - t));
                return look;
            }
            if (g.Stage == EvolutionStage.Child)
            {
                float t = Clamp01((float)((age - c.ChildAtDays) / Math.Max(0.01, c.TeenAtDays - c.ChildAtDays)));
                float each = c.ChildGrowth * t / Forms.Length;
                foreach (AdultForm f in Forms) look.Set(f, each);
            }
            return look;
        }

        /// <summary>
        /// 1 bước thời gian: ghi mức chăm sóc, cộng Bóng tối khi bị bỏ bê / ốm, lên giai đoạn khi đủ tuổi,
        /// trượt DNA mỗi ngày từ Teen. Trả về thay đổi lớn nhất (Evolved / DnaDrifted / None).
        /// </summary>
        public static EvolutionChange Step(PetData pet, double hours, DateTime endUtc, GameConfig cfg)
        {
            if (pet?.Growth == null || hours <= 0) return EvolutionChange.None;
            EvolutionConfig c = cfg.Evolution;
            PetGrowth g = pet.Growth;
            bool ill = pet.Health != null && HealthRules.IsIll(pet.Health.Stage);
            if (ill) g.LastIllUtc = endUtc;

            // mức chăm sóc của bước này: trung bình 4 nhu cầu (ốm thì chỉ tính nửa)
            PetNeeds n = pet.Needs;
            double q = (n.Satiety + n.Hygiene + n.Energy + n.Mood) / 4.0 * (ill ? c.SickCareFactor : 1.0);
            g.PartialCare += q * hours;
            g.PartialHours += hours;
            while (g.PartialHours >= 1.0 - 1e-9)
            {
                double avg = g.PartialCare / g.PartialHours;
                g.CareHours.Add((float)avg);
                g.PartialCare = Math.Max(0.0, g.PartialCare - avg);
                g.PartialHours = Math.Max(0.0, g.PartialHours - 1.0);
                if (g.PartialHours < 1e-9) g.PartialCare = 0;
            }
            int extra = g.CareHours.Count - c.CareWindowHours;
            if (extra > 0) g.CareHours.RemoveRange(0, extra);

            // bỏ bê / ốm => nghiêng về Bóng tối (không giới hạn theo ngày, nhưng chậm)
            if (!n.AllAtLeast(cfg.Needs.FullCareThreshold)) g.Training.Shadow += (float)(hours * c.ShadowPerNeglectHour);
            if (ill) g.Training.Shadow += (float)(hours * c.ShadowPerSickHour);

            EvolutionChange change = EvolutionChange.None;
            double age = pet.AgeDays(endUtc);
            EvolutionStage byAge = StageByAge(age, c);
            if (byAge > g.Stage && g.Stage < EvolutionStage.Adult)
            {
                g.Stage = (EvolutionStage)((int)g.Stage + 1);   // lên từng bậc (tắt app lâu vẫn qua đủ các bậc)
                if (g.Stage == EvolutionStage.Adult) g.Form = DecideForm(g.Training, pet.Hidden, CareScore(g), c);
                change = EvolutionChange.Evolved;
            }
            else if (g.Stage == EvolutionStage.Adult && CanUltimate(pet, endUtc, c))
            {
                g.Stage = EvolutionStage.Ultimate;
                change = EvolutionChange.Evolved;
            }

            // DNA trượt dần về dạng đang nghiêng: từ ngày tuổi Teen, mỗi ngày 1 bước
            int day = (int)Math.Floor(age);
            if (change == EvolutionChange.None && g.Stage >= EvolutionStage.Teen && day > g.DriftedDay && day >= (int)c.TeenAtDays)
            {
                g.DriftedDay = day;
                if (DriftDna(pet.Dna, Leaning(pet, c), day, c)) change = EvolutionChange.DnaDrifted;
            }
            return change;
        }

        // gene đích của 5 dạng (Gen 1): màu lông + tỉ lệ thân
        private struct FormGenes
        {
            public int Hue, Sat, Val, Belly, Paws, Head, Chubby, Legs;
        }

        private static FormGenes Target(AdultForm f)
        {
            switch (f)
            {
                case AdultForm.Fire: return new FormGenes { Hue = 3, Sat = 6, Val = 6, Belly = 2, Paws = 3, Head = 7, Chubby = 7, Legs = 8 };
                case AdultForm.Water: return new FormGenes { Hue = 36, Sat = 5, Val = 6, Belly = 3, Paws = 1, Head = 8, Chubby = 10, Legs = 5 };
                case AdultForm.Light: return new FormGenes { Hue = 9, Sat = 4, Val = 7, Belly = 3, Paws = 0, Head = 7, Chubby = 6, Legs = 10 };
                case AdultForm.Steel: return new FormGenes { Hue = 38, Sat = 1, Val = 4, Belly = 1, Paws = 2, Head = 9, Chubby = 12, Legs = 6 };
                default: return new FormGenes { Hue = 48, Sat = 4, Val = 2, Belly = 1, Paws = 3, Head = 5, Chubby = 5, Legs = 11 };
            }
        }

        /// <summary>1 bước trượt DNA về dạng `form` (màu lông + 1 gene tỉ lệ luân phiên theo ngày). Trả true nếu có đổi.</summary>
        public static bool DriftDna(PetDna dna, AdultForm form, int day, EvolutionConfig c)
        {
            if (dna == null || form == AdultForm.None) return false;
            FormGenes t = Target(form);
            bool changed = false;
            int hue = StepHue(dna.FurHue, t.Hue, c.DriftHueStep);
            changed |= hue != dna.FurHue;
            dna.FurHue = hue;
            changed |= StepToward(ref dna.FurSat, t.Sat);
            changed |= StepToward(ref dna.FurVal, t.Val);
            changed |= StepToward(ref dna.BellyTone, t.Belly);
            changed |= StepToward(ref dna.PawsDark, t.Paws);
            switch (((day % 3) + 3) % 3)
            {
                case 0: changed |= StepToward(ref dna.HeadSize, t.Head); break;
                case 1: changed |= StepToward(ref dna.Chubby, t.Chubby); break;
                default: changed |= StepToward(ref dna.LegLength, t.Legs); break;
            }
            return changed;
        }

        private static bool StepToward(ref int gene, int target)
        {
            if (gene == target) return false;
            gene += gene < target ? 1 : -1;
            return true;
        }

        /// <summary>Đi tối đa `step` nấc trên vòng màu 64 nấc theo hướng gần hơn.</summary>
        private static int StepHue(int hue, int target, int step)
        {
            int diff = ((target - hue) % 64 + 64) % 64;   // 0..63
            if (diff == 0) return hue;
            int move = diff <= 32 ? Math.Min(step, diff) : -Math.Min(step, 64 - diff);
            return ((hue + move) % 64 + 64) % 64;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>Chỉ số chiến đấu (G8).</summary>
    public struct PetStats
    {
        public float Hp, Atk, Def, Spd;

        /// <summary>Tỉ lệ chí mạng (%).</summary>
        public float Crit;

        public float Power;

        public override string ToString() =>
            "HP " + Hp.ToString("0") + " · ATK " + Atk.ToString("0") + " · DEF " + Def.ToString("0") + " · SPD " + Spd.ToString("0") +
            " · Crit " + Crit.ToString("0") + "% · Lực chiến " + Power.ToString("0");
    }

    /// <summary>Tính chỉ số + lực chiến (C# thuần) — G8.</summary>
    public static class StatsRules
    {
        public static PetStats Compute(EvolutionStage stage, AdultForm form, float healthFactor, StatsConfig c)
        {
            int si = Math.Max(0, Math.Min(c.StageMultiplier.Length - 1, (int)stage));
            float k = c.StageMultiplier[si];
            var s = new PetStats { Hp = c.BaseHp * k, Atk = c.BaseAtk * k, Def = c.BaseDef * k, Spd = c.BaseSpd * k, Crit = c.BaseCrit };
            if (stage >= EvolutionStage.Adult)
            {
                switch (form)
                {
                    case AdultForm.Fire: s.Atk *= 1f + c.FormBonus; break;
                    case AdultForm.Water: s.Spd *= 1f + c.FormBonus; break;
                    case AdultForm.Light: s.Hp *= 1f + c.FormBonus; break;
                    case AdultForm.Steel: s.Def *= 1f + c.FormBonus; break;
                    case AdultForm.Shadow: s.Crit += c.ShadowCritBonus; break;
                }
            }
            // bệnh: -30 / -40 / -60% (HealthService.StatFactor)
            s.Hp *= healthFactor;
            s.Atk *= healthFactor;
            s.Def *= healthFactor;
            s.Spd *= healthFactor;
            s.Power = Power(s, c);
            return s;
        }

        /// <summary>Lực chiến = 0.25·HP + 2·ATK + 1.5·DEF + 1.5·SPD + 10·Crit%.</summary>
        public static float Power(PetStats s, StatsConfig c) =>
            c.PowerHp * s.Hp + c.PowerAtk * s.Atk + c.PowerDef * s.Def + c.PowerSpd * s.Spd + c.PowerCrit * s.Crit;
    }
}
