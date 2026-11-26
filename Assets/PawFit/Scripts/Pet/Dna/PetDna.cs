using System;

namespace PawFit.Pet
{
    // Tên enum bằng tiếng Anh; chú thích là tên kiểu tương ứng trong bộ dựng thú Gen 1 (Tools/Blender).

    /// <summary>6 dạng mắt: tron, hanh_nhan, meo, lim_dim, cun, sac_hep.</summary>
    public enum EyeShape { Round = 0, Almond = 1, Cat = 2, Sleepy = 3, Puppy = 4, Sharp = 5 }

    /// <summary>4 dạng tai: nhon (cáo), tron (gấu), cup (gập ra trước), vay (vây / lông vũ).</summary>
    public enum EarType { Pointed = 0, Round = 1, Folded = 2, Fin = 3 }

    /// <summary>3 dáng sừng: thang, dai, cong.</summary>
    public enum HornShape { Straight = 0, Long = 1, Curved = 2 }

    /// <summary>5 dạng đuôi: cao (xù), tua (lụ túa), bong (cục bông), vay (vây cá), xoan (xoăn).</summary>
    public enum TailType { Fox = 0, Tuft = 1, Puff = 2, Fin = 3, Curl = 4 }

    /// <summary>Họa tiết lông: trơn / sọc / đốm.</summary>
    public enum FurPattern { Solid = 0, Stripes = 1, Spots = 2 }

    /// <summary>Tính cách ẩn — ảnh hưởng 10–20% ngẫu nhiên khi tiến hóa (Phase 2.2).</summary>
    public enum Personality { Calm = 0, Fiery = 1, Playful = 2, Shy = 3, Proud = 4, Lazy = 5, Curious = 6, Moody = 7 }

    /// <summary>
    /// DNA hình dạng của thú — phần được khoe bằng mã DNA (PF-XXXX-XXXX-XXXX).
    /// Mọi gene là số nguyên trong khoảng cố định để mã hóa chính xác 55 bit.
    /// Gene ẩn (seed, tính cách) nằm ở <see cref="HiddenGenes"/>, KHÔNG có trong mã chia sẻ.
    /// Tiến hóa (Phase 2.2) sẽ đổi dần các gene này theo từng ngày.
    /// </summary>
    public sealed class PetDna : IEquatable<PetDna>
    {
        /// <summary>Thế hệ bộ model: 1 = Gen 1 (bộ dựng bằng script), 2 = Gen 2 (bộ model đẹp làm sau).</summary>
        public int Gen = 1;

        public EyeShape EyeShape;

        /// <summary>0..11: chỉ số trong bảng 12 màu mắt.</summary>
        public int EyeColor;

        /// <summary>0..63: sắc độ lông (vòng màu).</summary>
        public int FurHue;

        /// <summary>0..7: độ đậm màu lông.</summary>
        public int FurSat;

        /// <summary>0..7: độ sáng màu lông.</summary>
        public int FurVal;

        /// <summary>0..3: vùng bụng / mõm / ngực sáng hơn (0 = không có).</summary>
        public int BellyTone;

        public FurPattern Pattern;

        /// <summary>0..7: độ lệch màu hoa văn so với màu lông.</summary>
        public int PatternHue;

        public EarType Ear;

        /// <summary>0..3 sừng.</summary>
        public int HornCount;

        public HornShape Horn;

        public TailType Tail;

        /// <summary>0..15 -> blendshape head_big.</summary>
        public int HeadSize;

        /// <summary>0..15 -> blendshape body_chubby.</summary>
        public int Chubby;

        /// <summary>0..15 -> blendshape legs_long.</summary>
        public int LegLength;

        /// <summary>0..3: chân sẫm màu.</summary>
        public int PawsDark;

        /// <summary>0..3: chóp tai sẫm màu.</summary>
        public int EarTipDark;

        /// <summary>Kiểm tra mọi gene nằm trong khoảng hợp lệ; sai thì ném ArgumentOutOfRangeException.</summary>
        public void Validate()
        {
            Check(Gen, 1, 15, nameof(Gen));
            Check((int)EyeShape, 0, 5, nameof(EyeShape));
            Check(EyeColor, 0, 11, nameof(EyeColor));
            Check(FurHue, 0, 63, nameof(FurHue));
            Check(FurSat, 0, 7, nameof(FurSat));
            Check(FurVal, 0, 7, nameof(FurVal));
            Check(BellyTone, 0, 3, nameof(BellyTone));
            Check((int)Pattern, 0, 2, nameof(Pattern));
            Check(PatternHue, 0, 7, nameof(PatternHue));
            Check((int)Ear, 0, 3, nameof(Ear));
            Check(HornCount, 0, 3, nameof(HornCount));
            Check((int)Horn, 0, 2, nameof(Horn));
            Check((int)Tail, 0, 4, nameof(Tail));
            Check(HeadSize, 0, 15, nameof(HeadSize));
            Check(Chubby, 0, 15, nameof(Chubby));
            Check(LegLength, 0, 15, nameof(LegLength));
            Check(PawsDark, 0, 3, nameof(PawsDark));
            Check(EarTipDark, 0, 3, nameof(EarTipDark));
        }

        private static void Check(int value, int min, int max, string gene)
        {
            if (value < min || value > max)
                throw new ArgumentOutOfRangeException(gene, value, "Gene " + gene + " phải trong [" + min + ", " + max + "]");
        }

        public PetDna Clone() => (PetDna)MemberwiseClone();

        public bool Equals(PetDna o)
        {
            if (ReferenceEquals(o, null)) return false;
            return Gen == o.Gen && EyeShape == o.EyeShape && EyeColor == o.EyeColor && FurHue == o.FurHue
                   && FurSat == o.FurSat && FurVal == o.FurVal && BellyTone == o.BellyTone && Pattern == o.Pattern
                   && PatternHue == o.PatternHue && Ear == o.Ear && HornCount == o.HornCount && Horn == o.Horn
                   && Tail == o.Tail && HeadSize == o.HeadSize && Chubby == o.Chubby && LegLength == o.LegLength
                   && PawsDark == o.PawsDark && EarTipDark == o.EarTipDark;
        }

        public override bool Equals(object obj) => Equals(obj as PetDna);

        public override int GetHashCode()
        {
            unchecked
            {
                int h = 17;
                foreach (int v in new[] { Gen, (int)EyeShape, EyeColor, FurHue, FurSat, FurVal, BellyTone, (int)Pattern,
                                          PatternHue, (int)Ear, HornCount, (int)Horn, (int)Tail, HeadSize, Chubby,
                                          LegLength, PawsDark, EarTipDark })
                    h = h * 31 + v;
                return h;
            }
        }

        public override string ToString() =>
            $"Gen{Gen} mắt={EyeShape}/{EyeColor} lông=({FurHue},{FurSat},{FurVal}) {Pattern} tai={Ear} " +
            $"sừng={HornCount}x{Horn} đuôi={Tail} tỉ lệ=({HeadSize},{Chubby},{LegLength})";
    }

    /// <summary>Gene ẩn: không lộ trong mã DNA, dùng cho phần ngẫu nhiên của tiến hóa.</summary>
    public sealed class HiddenGenes
    {
        public uint Seed;
        public Personality Personality;
    }
}
