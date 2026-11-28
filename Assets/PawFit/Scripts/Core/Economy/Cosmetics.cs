using System.Collections.Generic;

namespace PawFit.Core
{
    /// <summary>Phụ kiện đeo trên đầu / cổ thú (Phase 5.2). Chỉ để đẹp, không đổi chỉ số (PvP công bằng).</summary>
    public enum AccessoryKind { None = 0, Bow = 1, Beanie = 2, Scarf = 3, Crown = 4 }

    /// <summary>Đồ trang trí vườn (Phase 5.2), mỗi món có 1 chỗ đặt cố định trong vườn.</summary>
    public enum DecorKind { None = 0, Lanterns = 1, Bench = 2, Windmill = 3, FlowerArch = 4 }

    /// <summary>Đồ chơi trong vườn (Phase 8.3): bóng (ném cho bé nhặt), cần lông vũ (bé đuổi vồ), máy bong bóng (bé nhảy đớp).</summary>
    public enum ToyKind { None = 0, Ball = 1, Feather = 2, Bubbles = 3 }

    /// <summary>
    /// Đồ đã mua của người chơi (nằm trong file lưu game, KHÔNG mất khi thú mất: thú mới vẫn đeo được phụ kiện cũ).
    /// </summary>
    public sealed class CosmeticsData
    {
        public List<AccessoryKind> Accessories = new List<AccessoryKind>();

        /// <summary>Phụ kiện đang đeo (mỗi lúc 1 món). None = không đeo.</summary>
        public AccessoryKind Equipped;

        public List<DecorKind> Decor = new List<DecorKind>();

        /// <summary>Đồ chơi đã mua (Phase 8.3).</summary>
        public List<ToyKind> Toys = new List<ToyKind>();
    }

    /// <summary>Đổi phụ kiện đang đeo (View gắn / gỡ hình trên đầu thú).</summary>
    public readonly struct AccessoryChanged
    {
        public readonly AccessoryKind Equipped;
        public AccessoryChanged(AccessoryKind equipped) { Equipped = equipped; }
    }

    /// <summary>Vừa mua 1 đồ chơi (vườn bày thêm món đó + camera lia tới).</summary>
    public readonly struct ToyAdded
    {
        public readonly ToyKind Kind;
        public ToyAdded(ToyKind kind) { Kind = kind; }
    }

    /// <summary>Vừa mua 1 đồ trang trí (vườn dựng thêm món đó + camera lia tới).</summary>
    public readonly struct DecorAdded
    {
        public readonly DecorKind Kind;
        public DecorAdded(DecorKind kind) { Kind = kind; }
    }
}
