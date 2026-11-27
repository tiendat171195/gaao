using System;

namespace PawFit.Core
{
    /// <summary>
    /// Nguồn thời gian (UTC). Mọi luật chạy theo giờ thực (nhu cầu, bệnh, tiến hóa) PHẢI đi qua đây,
    /// không gọi DateTime.UtcNow trực tiếp — để test tua giờ được và dùng giờ máy chủ (<see cref="ServerClock"/>, Phase 4.1).
    /// </summary>
    public interface ITimeProvider
    {
        DateTime UtcNow { get; }
    }

    /// <summary>Giờ hệ thống của máy.</summary>
    public sealed class SystemTimeProvider : ITimeProvider
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    /// <summary>Bọc 1 nguồn giờ khác và cho phép tua nhanh (chỉ dùng để debug / test trong Editor).</summary>
    public sealed class DebugTimeProvider : ITimeProvider
    {
        private readonly ITimeProvider _inner;

        public TimeSpan Offset { get; private set; }

        public DebugTimeProvider(ITimeProvider inner)
        {
            _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        }

        public DateTime UtcNow => _inner.UtcNow + Offset;

        /// <summary>Tua tới (vd +24h để xem thú đói / bẩn).</summary>
        public void Advance(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delta), "Chỉ được tua tới");
            Offset += delta;
        }

        public void Reset() => Offset = TimeSpan.Zero;
    }

    /// <summary>Giờ đặt tay, dùng cho unit test.</summary>
    public sealed class ManualTimeProvider : ITimeProvider
    {
        public DateTime UtcNow { get; set; }

        public ManualTimeProvider(DateTime startUtc) { UtcNow = startUtc; }

        public void Advance(TimeSpan delta) => UtcNow += delta;
    }
}
