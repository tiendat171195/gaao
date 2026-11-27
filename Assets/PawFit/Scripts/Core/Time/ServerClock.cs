using System;

namespace PawFit.Core
{
    /// <summary>
    /// Giờ theo máy chủ (Phase 4.1, chống chỉnh giờ trên máy): giờ máy + độ lệch đo được khi đồng bộ với máy chủ.
    /// Chưa đồng bộ (mới mở app, không có mạng) thì dùng giờ máy + độ lệch đo lần trước (lưu trong file tài khoản).
    /// App đồng bộ lại lúc mở game, lúc quay lại game và mỗi 10 phút; chỉnh giờ máy giữa 2 lần đồng bộ chỉ có tác dụng
    /// tới lần đồng bộ kế (giờ chỉnh lùi về thì PetService bỏ qua, không trừ ngược).
    /// </summary>
    public sealed class ServerClock : ITimeProvider
    {
        private readonly ITimeProvider _device;

        /// <summary>Lệch dưới bấy nhiêu giây thì giữ độ lệch cũ (đỡ giật giờ vì độ trễ mạng).</summary>
        public double IgnoreBelowSeconds = 1.0;

        public ServerClock(ITimeProvider device, double knownOffsetSeconds = 0.0)
        {
            _device = device ?? throw new ArgumentNullException(nameof(device));
            OffsetSeconds = double.IsNaN(knownOffsetSeconds) || double.IsInfinity(knownOffsetSeconds) ? 0.0 : knownOffsetSeconds;
        }

        /// <summary>Giờ máy chủ − giờ máy (giây).</summary>
        public double OffsetSeconds { get; private set; }

        /// <summary>Đã đồng bộ với máy chủ trong lần mở app này.</summary>
        public bool Synced { get; private set; }

        public DateTime UtcNow => _device.UtcNow.AddSeconds(OffsetSeconds);

        /// <summary>Máy chủ trả giờ `serverUtc`; lời gọi mất `roundTripSeconds` đi + về (giờ máy chủ ứng với khoảng giữa).</summary>
        public void Sync(DateTime serverUtc, double roundTripSeconds)
        {
            DateTime now = serverUtc.AddSeconds(Math.Max(0.0, roundTripSeconds) * 0.5);
            double offset = (now - _device.UtcNow).TotalSeconds;
            if (!Synced || Math.Abs(offset - OffsetSeconds) >= IgnoreBelowSeconds) OffsetSeconds = offset;
            Synced = true;
        }
    }
}
