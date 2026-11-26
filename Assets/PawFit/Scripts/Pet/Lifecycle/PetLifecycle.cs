using System;
using System.Text;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Trait trứng được thừa hưởng từ thú đã mất (GDD G5: "1 gene hình dạng"; ở đây gom theo nhóm gene).</summary>
    public enum InheritedTrait
    {
        EyeShape = 0,   // dạng mắt
        EyeColor = 1,   // màu mắt
        FurColor = 2,   // màu lông (sắc + đậm + sáng)
        Pattern = 3,    // họa tiết + màu họa tiết
        Ears = 4,       // dạng tai
        Horns = 5,      // số sừng + dáng sừng
        Tail = 6,       // dạng đuôi
        BodyShape = 7,  // tỉ lệ đầu / thân / chân
    }

    /// <summary>Trứng chờ nở sau khi thú mất.</summary>
    public sealed class EggData
    {
        public PetDna Dna;
        public HiddenGenes Hidden;
        public InheritedTrait Trait;
        public string ParentId;
        public string ParentName;
        public DateTime LaidAtUtc;
        public DateTime ReadyAtUtc;
    }

    /// <summary>1 bia mộ ở Khu tưởng niệm (GDD G5: tên, tuổi, rank cao nhất, mã DNA).</summary>
    public sealed class MemorialEntry
    {
        public string PetId;
        public string Name;
        public PetDna Dna;
        public string DnaCode;
        public DateTime BornAtUtc;
        public DateTime DiedAtUtc;

        /// <summary>Rank cao nhất từng đạt — có từ Phase 4.3 (Ranking). Chưa có thì null.</summary>
        public string BestRank;

        /// <summary>Giai đoạn + dạng lúc mất (Phase 2.2).</summary>
        public EvolutionStage Stage;
        public AdultForm Form;

        public double AgeDaysAtDeath() => Math.Max(0.0, (DiedAtUtc - BornAtUtc).TotalDays);
    }

    // ---- Sự kiện vòng đời (View lắng nghe qua EventBus) ----

    /// <summary>Giai đoạn sức khỏe vừa đổi (Mệt, Ốm, Nguy kịch, bắt đầu hồi phục, khỏi, tái phát...).</summary>
    public readonly struct PetHealthChanged
    {
        public readonly PetData Pet;
        public readonly HealthStage From;
        public readonly HealthStage To;
        public readonly HealthChange Change;

        public PetHealthChanged(PetData pet, HealthStage from, HealthStage to, HealthChange change)
        {
            Pet = pet;
            From = from;
            To = to;
            Change = change;
        }
    }

    /// <summary>Thú đã mất (chết vĩnh viễn). Có bia mộ mới + trứng chờ nở.</summary>
    public readonly struct PetDied
    {
        public readonly PetData Pet;
        public readonly MemorialEntry Memorial;
        public readonly EggData Egg;

        public PetDied(PetData pet, MemorialEntry memorial, EggData egg)
        {
            Pet = pet;
            Memorial = memorial;
            Egg = egg;
        }
    }

    /// <summary>Trứng đã tới lúc nở (chạm vào để nở).</summary>
    public readonly struct EggReady
    {
        public readonly EggData Egg;
        public EggReady(EggData egg) { Egg = egg; }
    }

    /// <summary>Đẻ trứng từ thú đã mất: DNA mới ngẫu nhiên nhưng giữ lại đúng 1 trait của thú cũ.</summary>
    public static class EggFactory
    {
        public const int TraitCount = 8;

        public static EggData Lay(PetData parent, DateTime laidAtUtc, double hatchAfterHours, IRandom rng)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            PetDna dna = DnaGenerator.Random(rng, parent.Dna.Gen);
            var trait = (InheritedTrait)rng.Next(TraitCount);
            CopyTrait(parent.Dna, dna, trait);
            return new EggData
            {
                Dna = dna,
                Hidden = DnaGenerator.RandomHidden(rng),
                Trait = trait,
                ParentId = parent.Id,
                ParentName = parent.Name,
                LaidAtUtc = laidAtUtc,
                ReadyAtUtc = laidAtUtc.AddHours(hatchAfterHours),
            };
        }

        public static void CopyTrait(PetDna from, PetDna to, InheritedTrait trait)
        {
            switch (trait)
            {
                case InheritedTrait.EyeShape: to.EyeShape = from.EyeShape; break;
                case InheritedTrait.EyeColor: to.EyeColor = from.EyeColor; break;
                case InheritedTrait.FurColor:
                    to.FurHue = from.FurHue;
                    to.FurSat = from.FurSat;
                    to.FurVal = from.FurVal;
                    break;
                case InheritedTrait.Pattern:
                    to.Pattern = from.Pattern;
                    to.PatternHue = from.PatternHue;
                    break;
                case InheritedTrait.Ears: to.Ear = from.Ear; break;
                case InheritedTrait.Horns:
                    to.HornCount = from.HornCount;
                    to.Horn = from.Horn;
                    break;
                case InheritedTrait.Tail: to.Tail = from.Tail; break;
                default:
                    to.HeadSize = from.HeadSize;
                    to.Chubby = from.Chubby;
                    to.LegLength = from.LegLength;
                    break;
            }
        }

        /// <summary>2 DNA có giống nhau ở trait này không.</summary>
        public static bool SameTrait(PetDna a, PetDna b, InheritedTrait trait)
        {
            switch (trait)
            {
                case InheritedTrait.EyeShape: return a.EyeShape == b.EyeShape;
                case InheritedTrait.EyeColor: return a.EyeColor == b.EyeColor;
                case InheritedTrait.FurColor: return a.FurHue == b.FurHue && a.FurSat == b.FurSat && a.FurVal == b.FurVal;
                case InheritedTrait.Pattern: return a.Pattern == b.Pattern && a.PatternHue == b.PatternHue;
                case InheritedTrait.Ears: return a.Ear == b.Ear;
                case InheritedTrait.Horns: return a.HornCount == b.HornCount && a.Horn == b.Horn;
                case InheritedTrait.Tail: return a.Tail == b.Tail;
                default: return a.HeadSize == b.HeadSize && a.Chubby == b.Chubby && a.LegLength == b.LegLength;
            }
        }
    }

    /// <summary>Chuẩn hóa tên thú người chơi gõ: bỏ khoảng trắng thừa, tối đa 16 ký tự, trống thì là "Bé".</summary>
    public static class PetNames
    {
        public const int MaxLength = 16;
        public const string Default = "Bé";

        public static string Normalize(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return Default;
            string s = raw;
            try
            {
                s = s.Normalize(NormalizationForm.FormC);   // gộp dấu tiếng Việt thành 1 ký tự trước khi cắt độ dài
            }
            catch (Exception)
            {
                // nền tảng không hỗ trợ chuẩn hóa Unicode: dùng nguyên chuỗi
            }
            var sb = new StringBuilder(s.Length);
            bool space = false;
            foreach (char ch in s)
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch))
                {
                    space = sb.Length > 0;
                    continue;
                }
                if (space) sb.Append(' ');
                space = false;
                sb.Append(ch);
            }
            string name = sb.ToString();
            if (name.Length > MaxLength) name = name.Substring(0, MaxLength).TrimEnd();
            if (name.Length > 0 && char.IsHighSurrogate(name[name.Length - 1])) name = name.Substring(0, name.Length - 1);
            return name.Length == 0 ? Default : name;
        }
    }
}
