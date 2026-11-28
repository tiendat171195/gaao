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
}
