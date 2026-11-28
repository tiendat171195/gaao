using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Economy
{
    /// <summary>Lý do không mua / đeo được (None = được).</summary>
    public enum CosmeticRefusal { None = 0, AlreadyOwned = 1, NotOwned = 2, NotEnoughCoins = 3, Invalid = 4 }

    /// <summary>
    /// Phụ kiện + đồ trang trí (C# thuần, Phase 5.2). Bán ở quầy hàng, mua 1 lần là có mãi (của người chơi, thú mất vẫn còn).
    /// Phụ kiện: mua xong tự đeo, mỗi lúc đeo 1 món, chạm lại món đang đeo để tháo. Trang trí: mua xong hiện luôn trong vườn.
    /// Đồ chơi (Phase 8.3): mua xong bày trong vườn, chơi cùng bé (luật chơi ở PlayService).
    /// Không đổi chỉ số đấu (PvP công bằng); tác dụng chăm sóc của đồ ở CarePerks (Phase 8.4).
    /// </summary>
    public sealed class CosmeticsService
    {
        /// <summary>Thứ tự bày trong quầy.</summary>
        public static readonly AccessoryKind[] Accessories = { AccessoryKind.Bow, AccessoryKind.Beanie, AccessoryKind.Scarf, AccessoryKind.Crown };
        public static readonly DecorKind[] Decorations = { DecorKind.Lanterns, DecorKind.Bench, DecorKind.Windmill, DecorKind.FlowerArch };
        public static readonly ToyKind[] Toys = { ToyKind.Ball, ToyKind.Feather, ToyKind.Bubbles };

        private readonly PetService _pets;
        private readonly ICoinWallet _wallet;
        private readonly EventBus _bus;
        private readonly EconomyConfig _cfg;
        private readonly ILog _log;

        public CosmeticsService(PetService pets, ICoinWallet wallet, EventBus bus, GameConfig cfg, ILog log = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = (cfg ?? throw new ArgumentNullException(nameof(cfg))).Economy;
            _log = log ?? NullLog.Instance;
        }

        private CosmeticsData Data => _pets.Cosmetics;

        /// <summary>Phụ kiện đang đeo (None = không đeo).</summary>
        public AccessoryKind Equipped => Data?.Equipped ?? AccessoryKind.None;

        public int PriceOf(AccessoryKind kind) => Price(_cfg.AccessoryPrices, (int)kind);
        public int PriceOf(DecorKind kind) => Price(_cfg.DecorPrices, (int)kind);
        public int PriceOf(ToyKind kind) => Price(_cfg.ToyPrices, (int)kind);

        public bool Owns(AccessoryKind kind) => Data != null && Data.Accessories.Contains(kind);
        public bool Owns(DecorKind kind) => Data != null && Data.Decor.Contains(kind);
        public bool Owns(ToyKind kind) => Data != null && Data.Toys.Contains(kind);

        public CosmeticRefusal CheckBuy(ToyKind kind)
        {
            if (kind == ToyKind.None || PriceOf(kind) < 0 || Data == null) return CosmeticRefusal.Invalid;
            if (Owns(kind)) return CosmeticRefusal.AlreadyOwned;
            return _wallet.Coins >= PriceOf(kind) ? CosmeticRefusal.None : CosmeticRefusal.NotEnoughCoins;
        }

        /// <summary>Mua 1 đồ chơi: trừ Coin, vườn bày thêm món đó (chơi cùng bé được ngay).</summary>
        public CosmeticRefusal Buy(ToyKind kind)
        {
            CosmeticRefusal r = CheckBuy(kind);
            if (r != CosmeticRefusal.None) return r;
            if (!_wallet.TrySpend(PriceOf(kind), "đồ chơi " + kind)) return CosmeticRefusal.NotEnoughCoins;
            Data.Toys.Add(kind);
            _pets.Save();
            _log.Info("[PawFit] Mua đồ chơi " + kind);
            _bus.Publish(new ToyAdded(kind));
            return CosmeticRefusal.None;
        }

        public CosmeticRefusal CheckBuy(AccessoryKind kind)
        {
            if (kind == AccessoryKind.None || PriceOf(kind) < 0 || Data == null) return CosmeticRefusal.Invalid;
            if (Owns(kind)) return CosmeticRefusal.AlreadyOwned;
            return _wallet.Coins >= PriceOf(kind) ? CosmeticRefusal.None : CosmeticRefusal.NotEnoughCoins;
        }

        public CosmeticRefusal CheckBuy(DecorKind kind)
        {
            if (kind == DecorKind.None || PriceOf(kind) < 0 || Data == null) return CosmeticRefusal.Invalid;
            if (Owns(kind)) return CosmeticRefusal.AlreadyOwned;
            return _wallet.Coins >= PriceOf(kind) ? CosmeticRefusal.None : CosmeticRefusal.NotEnoughCoins;
        }

        /// <summary>Mua 1 phụ kiện: trừ Coin, thêm vào đồ đã có, đeo luôn.</summary>
        public CosmeticRefusal Buy(AccessoryKind kind)
        {
            CosmeticRefusal r = CheckBuy(kind);
            if (r != CosmeticRefusal.None) return r;
            if (!_wallet.TrySpend(PriceOf(kind), "phụ kiện " + kind)) return CosmeticRefusal.NotEnoughCoins;
            Data.Accessories.Add(kind);
            _log.Info("[PawFit] Mua phụ kiện " + kind);
            Equip(kind);   // lưu + báo View
            return CosmeticRefusal.None;
        }

        /// <summary>Mua 1 đồ trang trí: trừ Coin, vườn dựng thêm món đó.</summary>
        public CosmeticRefusal Buy(DecorKind kind)
        {
            CosmeticRefusal r = CheckBuy(kind);
            if (r != CosmeticRefusal.None) return r;
            if (!_wallet.TrySpend(PriceOf(kind), "trang trí " + kind)) return CosmeticRefusal.NotEnoughCoins;
            Data.Decor.Add(kind);
            _pets.Save();
            _log.Info("[PawFit] Mua trang trí " + kind);
            _bus.Publish(new DecorAdded(kind));
            return CosmeticRefusal.None;
        }

        /// <summary>Đeo 1 phụ kiện đã có (None = tháo ra). Chưa có thì không đeo được.</summary>
        public CosmeticRefusal Equip(AccessoryKind kind)
        {
            CosmeticsData d = Data;
            if (d == null) return CosmeticRefusal.Invalid;
            if (kind != AccessoryKind.None && !d.Accessories.Contains(kind)) return CosmeticRefusal.NotOwned;
            if (d.Equipped == kind) return CosmeticRefusal.None;
            d.Equipped = kind;
            _pets.Save();
            _bus.Publish(new AccessoryChanged(kind));
            return CosmeticRefusal.None;
        }

        /// <summary>Chạm 1 phụ kiện đã có trong quầy: đang đeo thì tháo, chưa đeo thì đeo (thay món cũ).</summary>
        public CosmeticRefusal Toggle(AccessoryKind kind) => Equip(Equipped == kind ? AccessoryKind.None : kind);

        /// <summary>CHỈ dùng cho công cụ test: có hết phụ kiện + đồ trang trí, không trừ Coin.</summary>
        public void GrantAllForDebug()
        {
            CosmeticsData d = Data;
            if (d == null) return;
            foreach (AccessoryKind a in Accessories)
                if (!d.Accessories.Contains(a)) d.Accessories.Add(a);
            _pets.Save();
            foreach (DecorKind k in Decorations)
            {
                if (d.Decor.Contains(k)) continue;
                d.Decor.Add(k);
                _pets.Save();
                _bus.Publish(new DecorAdded(k));
            }
            foreach (ToyKind t in Toys)
            {
                if (d.Toys.Contains(t)) continue;
                d.Toys.Add(t);
                _pets.Save();
                _bus.Publish(new ToyAdded(t));
            }
        }

        /// <summary>Giá theo thứ tự enum (bỏ None = 0). Ngoài bảng giá thì -1 (không bán).</summary>
        private static int Price(int[] prices, int kind)
        {
            int i = kind - 1;
            return prices != null && i >= 0 && i < prices.Length ? prices[i] : -1;
        }
    }
}
