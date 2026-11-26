using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Sinh DNA ngẫu nhiên theo phân bố "dễ nhìn" (màu không quá chói / quá tối, tỉ lệ tập trung ở giữa).</summary>
    public static class DnaGenerator
    {
        public static PetDna Random(IRandom rng, int gen = 1)
        {
            return new PetDna
            {
                Gen = gen,
                EyeShape = (EyeShape)rng.Next(6),
                EyeColor = rng.Next(12),
                FurHue = rng.Next(64),
                FurSat = rng.Range(2, 7),                         // 2..6: tránh xám xịt / chói gắt
                FurVal = rng.Range(3, 8),                         // 3..7: tránh quá tối
                BellyTone = Weighted(rng, 10, 30, 35, 25),
                Pattern = (FurPattern)Weighted(rng, 45, 30, 25),  // trơn 45%, sọc 30%, đốm 25%
                PatternHue = rng.Next(8),
                Ear = (EarType)rng.Next(4),
                HornCount = Weighted(rng, 40, 25, 25, 10),        // 0 sừng 40% ... 3 sừng 10%
                Horn = (HornShape)rng.Next(3),
                Tail = (TailType)rng.Next(5),
                HeadSize = Triangle(rng, 16),
                Chubby = Triangle(rng, 16),
                LegLength = Triangle(rng, 16),
                PawsDark = Weighted(rng, 40, 25, 20, 15),
                EarTipDark = Weighted(rng, 35, 25, 25, 15),
            };
        }

        public static HiddenGenes RandomHidden(IRandom rng)
        {
            return new HiddenGenes { Seed = rng.NextUInt(), Personality = (Personality)rng.Next(8) };
        }

        /// <summary>Chọn chỉ số theo trọng số (vd 40, 25, 25, 10).</summary>
        private static int Weighted(IRandom rng, params int[] weights)
        {
            int total = 0;
            foreach (int w in weights) total += w;
            int r = rng.Next(total);
            for (int i = 0; i < weights.Length; i++)
            {
                if (r < weights[i]) return i;
                r -= weights[i];
            }
            return weights.Length - 1;
        }

        /// <summary>Phân bố tam giác trong [0, n): giá trị giữa hay gặp hơn 2 đầu.</summary>
        private static int Triangle(IRandom rng, int n) => (rng.Next(n) + rng.Next(n)) / 2;
    }
}
