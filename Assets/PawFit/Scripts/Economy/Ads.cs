using System;

namespace PawFit.Economy
{
    /// <summary>Kết quả 1 lần xem quảng cáo có thưởng.</summary>
    public enum AdResult
    {
        /// <summary>Xem hết: được thưởng.</summary>
        Completed = 0,

        /// <summary>Tắt giữa chừng: không thưởng, không tính lượt.</summary>
        Skipped = 1,

        /// <summary>Không có / không tải được quảng cáo: không thưởng, không tính lượt.</summary>
        Failed = 2,
    }

    /// <summary>
    /// Cổng quảng cáo có thưởng. Hiện dùng <see cref="MockAdService"/>; bản phát hành (Phase 5.3) thay bằng
    /// SDK quảng cáo thật mà không phải sửa luật kinh tế. Chỉ App chọn implementation.
    /// </summary>
    public interface IAdService
    {
        /// <summary>Có quảng cáo sẵn sàng để chiếu không.</summary>
        bool IsReady { get; }

        /// <summary>Đang chiếu quảng cáo.</summary>
        bool IsShowing { get; }

        /// <summary>Chiếu 1 quảng cáo có thưởng. `done` được gọi đúng 1 lần khi xem xong / bỏ qua / lỗi.</summary>
        void ShowRewarded(string placement, Action<AdResult> done);
    }

    /// <summary>
    /// Quảng cáo giả lập (C# thuần): chiếu "quảng cáo" dài <see cref="Seconds"/> giây, xem hết thì Completed,
    /// bấm bỏ qua thì Skipped. Ai đó (App) phải gọi <see cref="Update"/> mỗi frame để thời gian trôi.
    /// </summary>
    public sealed class MockAdService : IAdService
    {
        private Action<AdResult> _done;

        public MockAdService(float seconds = 3f)
        {
            Seconds = Math.Max(0f, seconds);
        }

        /// <summary>Thời lượng 1 quảng cáo giả.</summary>
        public float Seconds { get; set; }

        /// <summary>Giả lập "hết quảng cáo" để thử nhánh lỗi.</summary>
        public bool NoFill { get; set; }

        public bool IsShowing => _done != null;
        public bool IsReady => !IsShowing && !NoFill;

        /// <summary>Còn bao nhiêu giây (màn quảng cáo giả hiện đếm ngược).</summary>
        public float SecondsLeft { get; private set; }

        /// <summary>Chỗ đang chiếu (vd "shop_coins").</summary>
        public string Placement { get; private set; }

        public void ShowRewarded(string placement, Action<AdResult> done)
        {
            if (!IsReady)   // đang chiếu dở hoặc "hết quảng cáo"
            {
                done?.Invoke(AdResult.Failed);
                return;
            }
            Placement = placement;
            SecondsLeft = Seconds;
            _done = done ?? (_ => { });
        }

        /// <summary>Cho thời gian trôi (giây thật, không phụ thuộc tốc độ game).</summary>
        public void Update(float seconds)
        {
            if (!IsShowing) return;
            SecondsLeft -= Math.Max(0f, seconds);
            if (SecondsLeft <= 0f) Finish(AdResult.Completed);
        }

        /// <summary>Người xem tắt giữa chừng.</summary>
        public void Skip()
        {
            if (IsShowing) Finish(AdResult.Skipped);
        }

        private void Finish(AdResult result)
        {
            Action<AdResult> done = _done;
            _done = null;
            SecondsLeft = 0f;
            Placement = null;
            done(result);
        }
    }
}
