using System;
using System.Collections.Generic;

namespace PawFit.Core
{
    /// <summary>Loại cây trồng ở vườn rau (Phase 10.2). Số = chỉ số trong Seeds / Basket của FarmData (không đổi số cũ).</summary>
    public enum CropKind { Strawberry = 0, Carrot = 1, Blueberry = 2, Pumpkin = 3 }

    /// <summary>1 luống đất. Cây lớn theo giờ thật (kể cả lúc tắt app); đất ướt thì lớn nhanh hơn.</summary>
    public sealed class PlotState
    {
        public bool Planted;
        public CropKind Crop;

        /// <summary>Cây đã lớn được bấy nhiêu "giờ lớn" (giờ đất ướt tính nhân hệ số tưới). Đủ thời gian của loại cây = chín.</summary>
        public double GrownHours;

        /// <summary>Lần cuối tính lớn (UTC). Tính lại từ mốc này nên không cần lưu mỗi giây.</summary>
        public DateTime UpdatedUtc;

        /// <summary>Đất còn ẩm tới lúc này (UTC). Quá mốc = đất khô, tưới được.</summary>
        public DateTime WetUntilUtc;
    }

    /// <summary>Vườn rau (Phase 10.2, trong file lưu game; thú mất vẫn giữ): luống đất, hạt giống, giỏ quả đã hái.</summary>
    public sealed class FarmData
    {
        public List<PlotState> Plots = new List<PlotState>();

        /// <summary>Hạt giống đang có, theo loại (chỉ số = CropKind).</summary>
        public List<int> Seeds = new List<int>();

        /// <summary>Quả trong giỏ cạnh chòi ăn, theo loại (chỉ số = CropKind).</summary>
        public List<int> Basket = new List<int>();

        /// <summary>Tổng số lần thu hoạch (thống kê).</summary>
        public int Harvests;

        /// <summary>Đã tặng gói hạt khởi đầu chưa (mở vườn rau lần đầu được tặng ít hạt để gieo thử).</summary>
        public bool StarterGiven;
    }
}
