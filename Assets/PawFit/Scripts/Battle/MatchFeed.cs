using System;

namespace PawFit.Battle
{
    /// <summary>1 nét vuốt máy chủ đã nhận trong trận PvP qua mạng: áp vào trước bước mô phỏng `Tick`, của bên `Side` (theo máy chủ).</summary>
    public struct TickInput
    {
        public int Tick;
        public int Side;
        public SwipeResult Swipe;
    }

    /// <summary>Máu 2 bên (theo bên máy chủ) sau khi máy chủ chạy xong `Ticks` bước: để sửa nếu máy này lỡ lệch số lẻ.</summary>
    public struct HpCheck
    {
        public int Ticks;
        public float Hp0;
        public float Hp1;
    }

    /// <summary>
    /// Nhịp 1 trận PvP qua máy chủ riêng (bản cài ở PawFit.Online). Máy chủ chạy trận thật và quyết định kết quả;
    /// máy này chạy lại đúng các bước máy chủ đã chạy, với đúng các nét vuốt máy chủ đã nhận của cả 2 bên
    /// (cùng seed, cùng chỉ số tới từng bit) => 2 máy + máy chủ ra cùng 1 trận mà không cần gửi cả trạng thái.
    /// Bên (0 / 1) trong interface này là bên theo máy chủ; <see cref="BattleSession"/> tự đổi để mình luôn là bên 0.
    /// </summary>
    public interface IMatchFeed
    {
        /// <summary>Mình là bên nào ở máy chủ (0 / 1).</summary>
        int ServerSide { get; }

        ulong Seed { get; }

        /// <summary>Thời lượng trận (giây) máy chủ dùng.</summary>
        float DurationSeconds { get; }

        /// <summary>Chỉ số + chiêu đã thuộc của bên `serverSide`, đúng như máy chủ dùng.</summary>
        FighterStats StatsOf(int serverSide);
        bool[] LearnedOf(int serverSide);

        /// <summary>Máy chủ đã bắt đầu chạy trận (cả 2 bên đã vào màn đấu, hoặc hết giờ chờ).</summary>
        bool Started { get; }

        /// <summary>Số bước máy chủ đã chạy xong: bước 0 .. ConfirmedTicks - 1 đã chốt.</summary>
        int ConfirmedTicks { get; }

        /// <summary>Nét vuốt đã chốt kế tiếp (theo đúng thứ tự máy chủ áp vào), chưa lấy ra.</summary>
        bool TryPeekInput(out TickInput input);
        void PopInput();

        /// <summary>Số máu máy chủ báo kế tiếp, chưa lấy ra.</summary>
        bool TryPeekHp(out HpCheck check);
        void PopHp();

        /// <summary>Máy chủ báo hết trận: hết ở bước nào, ai thắng (theo bên máy chủ), vì sao (ko / time / left).</summary>
        bool Ended { get; }
        int EndTick { get; }
        BattleOutcome EndOutcome { get; }
        string EndReason { get; }

        /// <summary>Mất kết nối giữa trận (máy chủ tính là bỏ cuộc = thua).</summary>
        bool Lost { get; }

        /// <summary>Màn đấu đã dựng xong, sẵn sàng đánh.</summary>
        void Ready();

        /// <summary>Gửi 1 nét vuốt của mình lên máy chủ (máy chủ chốt bước nào thì 2 máy áp ở bước đó).</summary>
        void SendSwipe(SwipeResult swipe);

        /// <summary>Bỏ cuộc giữa trận.</summary>
        void Leave();
    }

    /// <summary>Đổi bên / kết quả giữa "theo máy chủ" và "trên máy này" (mình luôn là bên 0 trên máy).</summary>
    public static class FeedSides
    {
        public static int Local(int serverSide, int mySide) => serverSide == mySide ? 0 : 1;

        public static BattleOutcome Local(BattleOutcome serverOutcome, int mySide)
        {
            if (mySide == 0 || serverOutcome == BattleOutcome.Ongoing || serverOutcome == BattleOutcome.Draw) return serverOutcome;
            return serverOutcome == BattleOutcome.Win0 ? BattleOutcome.Win1 : BattleOutcome.Win0;
        }

        /// <summary>Kiểm tra số đọc từ mạng: lỗi thì trả giá trị an toàn (NaN / vô cực làm hỏng cả trận).</summary>
        public static float Finite(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : v;

        public static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max, v));
    }
}
