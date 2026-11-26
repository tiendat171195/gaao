using System;

namespace PawFit.Core
{
    /// <summary>Nguồn số ngẫu nhiên. Cùng seed => cùng dãy số trên mọi máy (Unity Mono/IL2CPP, server, test).</summary>
    public interface IRandom
    {
        /// <summary>Số nguyên 32 bit không dấu.</summary>
        uint NextUInt();

        /// <summary>Số nguyên trong [0, maxExclusive).</summary>
        int Next(int maxExclusive);

        /// <summary>Số nguyên trong [minInclusive, maxExclusive).</summary>
        int Range(int minInclusive, int maxExclusive);

        /// <summary>Số thực trong [0, 1).</summary>
        double NextDouble();
    }

    /// <summary>
    /// PCG32 (Melissa O'Neill): nhanh, chất lượng tốt, kết quả giống hệt nhau trên mọi nền tảng.
    /// Không dùng System.Random vì thuật toán có thể khác nhau giữa Mono / IL2CPP / .NET.
    /// </summary>
    public sealed class SeededRandom : IRandom
    {
        private ulong _state;
        private readonly ulong _inc;

        public SeededRandom(ulong seed, ulong stream = 54UL)
        {
            _state = 0UL;
            _inc = (stream << 1) | 1UL;
            NextUInt();
            _state = unchecked(_state + seed);
            NextUInt();
        }

        public uint NextUInt()
        {
            ulong old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
            int rot = (int)(old >> 59);
            return (xorshifted >> rot) | (xorshifted << ((-rot) & 31));
        }

        public int Next(int maxExclusive)
        {
            if (maxExclusive <= 0) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            uint bound = (uint)maxExclusive;
            // loại bỏ thiên lệch modulo: bỏ các giá trị nhỏ hơn ngưỡng
            uint threshold = (uint)((0x100000000UL - bound) % bound);
            while (true)
            {
                uint r = NextUInt();
                if (r >= threshold) return (int)(r % bound);
            }
        }

        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) throw new ArgumentOutOfRangeException(nameof(maxExclusive));
            return minInclusive + Next(maxExclusive - minInclusive);
        }

        public double NextDouble() => NextUInt() * (1.0 / 4294967296.0);
    }
}
