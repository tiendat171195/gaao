using System.Collections.Generic;

namespace PawFit.Core
{
    /// <summary>
    /// Tiến độ Thám hiểm (Phase 7.2, trong file lưu game; thú mất vẫn giữ — bé mới đi tiếp từ chỗ cũ).
    /// </summary>
    public sealed class AdventureData
    {
        /// <summary>Số sao (0..3) của từng màn, theo thứ tự trên bản đồ. Thiếu phần tử = 0 sao.</summary>
        public List<int> Stars = new List<int>();

        public int Wins;
        public int Losses;
    }

    /// <summary>Vừa thắng 1 màn Thám hiểm (nhiệm vụ ngày nghe sự kiện này). NewStars = số sao mới có thêm.</summary>
    public readonly struct AdventureWon
    {
        public readonly int Stage;
        public readonly int Stars;
        public readonly int NewStars;
        public readonly bool FirstClear;

        public AdventureWon(int stage, int stars, int newStars, bool firstClear)
        {
            Stage = stage;
            Stars = stars;
            NewStars = newStars;
            FirstClear = firstClear;
        }
    }
}
