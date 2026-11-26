using System;
using System.Text;

namespace PawFit.Pet
{
    /// <summary>
    /// Mã DNA chia sẻ dạng "PF-XXXX-XXXX-XXXX": 55 bit gene -> 11 ký tự Base32 (Crockford) + 1 ký tự kiểm tra.
    /// Đọc mã dễ dãi: không phân biệt hoa thường, bỏ qua dấu gạch / khoảng trắng, I/L -> 1, O -> 0.
    /// Gõ sai 1 ký tự sẽ bị phát hiện nhờ ký tự kiểm tra.
    /// </summary>
    public static class DnaCodec
    {
        public const string Prefix = "PF";
        private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        private const int DataChars = 11;   // 55 bit / 5 bit mỗi ký tự

        public static string Encode(PetDna dna)
        {
            if (dna == null) throw new ArgumentNullException(nameof(dna));
            dna.Validate();
            var w = new BitWriter();
            w.Put(dna.Gen, 4);
            w.Put((int)dna.EyeShape, 3);
            w.Put(dna.EyeColor, 4);
            w.Put(dna.FurHue, 6);
            w.Put(dna.FurSat, 3);
            w.Put(dna.FurVal, 3);
            w.Put(dna.BellyTone, 2);
            w.Put((int)dna.Pattern, 2);
            w.Put(dna.PatternHue, 3);
            w.Put((int)dna.Ear, 2);
            w.Put(dna.HornCount, 2);
            w.Put((int)dna.Horn, 2);
            w.Put((int)dna.Tail, 3);
            w.Put(dna.HeadSize, 4);
            w.Put(dna.Chubby, 4);
            w.Put(dna.LegLength, 4);
            w.Put(dna.PawsDark, 2);
            w.Put(dna.EarTipDark, 2);
            var chars = new char[DataChars + 1];
            for (int i = 0; i < DataChars; i++) chars[i] = Alphabet[(int)((w.Value >> (5 * i)) & 31UL)];
            chars[DataChars] = Alphabet[Checksum(chars, DataChars)];
            var s = new string(chars);
            return Prefix + "-" + s.Substring(0, 4) + "-" + s.Substring(4, 4) + "-" + s.Substring(8, 4);
        }

        public static bool TryDecode(string code, out PetDna dna)
        {
            dna = null;
            string s = Normalize(code);
            if (s == null) return false;
            if (s.Length == DataChars + 1 + Prefix.Length && s.StartsWith(Prefix, StringComparison.Ordinal))
                s = s.Substring(Prefix.Length);
            if (s.Length != DataChars + 1) return false;
            var chars = s.ToCharArray();
            if (Alphabet[Checksum(chars, DataChars)] != chars[DataChars]) return false;
            ulong v = 0;
            for (int i = 0; i < DataChars; i++) v |= (ulong)Alphabet.IndexOf(chars[i]) << (5 * i);
            var r = new BitReader(v);
            var d = new PetDna
            {
                Gen = r.Take(4),
                EyeShape = (EyeShape)r.Take(3),
                EyeColor = r.Take(4),
                FurHue = r.Take(6),
                FurSat = r.Take(3),
                FurVal = r.Take(3),
                BellyTone = r.Take(2),
                Pattern = (FurPattern)r.Take(2),
                PatternHue = r.Take(3),
                Ear = (EarType)r.Take(2),
                HornCount = r.Take(2),
                Horn = (HornShape)r.Take(2),
                Tail = (TailType)r.Take(3),
                HeadSize = r.Take(4),
                Chubby = r.Take(4),
                LegLength = r.Take(4),
                PawsDark = r.Take(2),
                EarTipDark = r.Take(2),
            };
            try
            {
                d.Validate();
            }
            catch (ArgumentOutOfRangeException)
            {
                return false;
            }
            dna = d;
            return true;
        }

        public static PetDna Decode(string code)
        {
            if (!TryDecode(code, out var dna)) throw new FormatException("Mã DNA không hợp lệ: " + code);
            return dna;
        }

        /// <summary>Viết hoa, bỏ ký tự ngăn cách, sửa ký tự dễ nhầm. Trả null nếu có ký tự lạ.</summary>
        private static string Normalize(string code)
        {
            if (string.IsNullOrWhiteSpace(code)) return null;
            var sb = new StringBuilder(code.Length);
            foreach (char raw in code.ToUpperInvariant())
            {
                char c = raw;
                if (c == '-' || c == ' ' || c == '_') continue;
                if (c == 'I' || c == 'L') c = '1';
                else if (c == 'O') c = '0';
                if (Alphabet.IndexOf(c) < 0 && c != 'P' && c != 'F') return null;
                sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>Tổng có trọng số lẻ (mod 32): bắt được mọi lỗi sai 1 ký tự.</summary>
        private static int Checksum(char[] chars, int count)
        {
            int sum = 0;
            for (int i = 0; i < count; i++) sum += (2 * i + 1) * (Alphabet.IndexOf(chars[i]) + 1);
            return sum % 32;
        }

        private sealed class BitWriter
        {
            public ulong Value;
            private int _shift;

            public void Put(int value, int bits)
            {
                Value |= ((ulong)value & ((1UL << bits) - 1UL)) << _shift;
                _shift += bits;
            }
        }

        private sealed class BitReader
        {
            private readonly ulong _value;
            private int _shift;

            public BitReader(ulong value) { _value = value; }

            public int Take(int bits)
            {
                int v = (int)((_value >> _shift) & ((1UL << bits) - 1UL));
                _shift += bits;
                return v;
            }
        }
    }
}
