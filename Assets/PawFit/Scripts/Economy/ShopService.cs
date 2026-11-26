using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Economy
{
    /// <summary>Món bán ở quầy hàng trong vườn (Phase 5.1). Mua xong là cho thú dùng luôn, không có túi đồ.</summary>
    public enum ShopItem
    {
        /// <summary>Thuốc chữa Ốm / Nguy kịch: 60 / 150 Coin (GDD G4). Đã uống thuốc thì chăm thường cũng giữ được hồi phục.</summary>
        Medicine = 0,

        /// <summary>Bánh thưởng: +no, +vui nhiều.</summary>
        Treat = 1,

        /// <summary>Nước tăng lực: +năng lượng.</summary>
        EnergyDrink = 2,
    }

    /// <summary>Lý do quầy không bán món đó lúc này (None = mua được).</summary>
    public enum ShopRefusal
    {
        None = 0,

        /// <summary>Không có thú (đã mất, đang chờ trứng nở).</summary>
        NoPet = 1,

        /// <summary>Thú đang khỏe (hoặc chỉ Mệt), chưa cần thuốc.</summary>
        NotIll = 2,

        /// <summary>Đã uống thuốc trong đợt hồi phục này rồi.</summary>
        AlreadyMedicated = 3,

        /// <summary>Thú đang ngủ, không ăn / uống được.</summary>
        Sleeping = 4,

        /// <summary>Thú đang no / đầy năng lượng, mua thì phí.</summary>
        NotNeeded = 5,

        NotEnoughCoins = 6,
    }

    /// <summary>Vừa mua xong 1 món (View cho thú ăn / uống).</summary>
    public readonly struct ShopPurchased
    {
        public readonly ShopItem Item;
        public readonly int Price;

        public ShopPurchased(ShopItem item, int price)
        {
            Item = item;
            Price = price;
        }
    }

    /// <summary>
    /// Quầy hàng trong vườn (C# thuần): giá, điều kiện bán, mua = trừ Coin rồi dùng luôn cho thú.
    /// Thuốc đi qua HealthService (đúng luật G4); bánh / nước đi qua PetService.Care.
    /// </summary>
    public sealed class ShopService
    {
        /// <summary>Thứ tự bày trên quầy.</summary>
        public static readonly ShopItem[] Items = { ShopItem.Medicine, ShopItem.Treat, ShopItem.EnergyDrink };

        private readonly PetService _pets;
        private readonly HealthService _health;
        private readonly ICoinWallet _wallet;
        private readonly EventBus _bus;
        private readonly GameConfig _cfg;
        private readonly ILog _log;

        public ShopService(PetService pets, HealthService health, ICoinWallet wallet, EventBus bus, GameConfig cfg, ILog log = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _log = log ?? NullLog.Instance;
        }

        /// <summary>Giá hiện tại (thuốc đắt hơn khi Nguy kịch).</summary>
        public int PriceOf(ShopItem item)
        {
            switch (item)
            {
                case ShopItem.Medicine: return _health.MedicineCost;
                case ShopItem.Treat: return _cfg.Economy.TreatPrice;
                default: return _cfg.Economy.EnergyDrinkPrice;
            }
        }

        /// <summary>Mua được không (chỉ hỏi, không đổi gì).</summary>
        public ShopRefusal Check(ShopItem item)
        {
            if (!_pets.HasPet) return ShopRefusal.NoPet;
            PetData pet = _pets.Current;
            EconomyConfig e = _cfg.Economy;
            switch (item)
            {
                case ShopItem.Medicine:
                    if (!_health.IsIll) return ShopRefusal.NotIll;
                    RecoveryState r = pet.Health.Recovery;
                    if (r != null && r.Method == TreatmentKind.Medicine) return ShopRefusal.AlreadyMedicated;
                    break;
                case ShopItem.Treat:
                    if (pet.IsSleeping) return ShopRefusal.Sleeping;
                    if (pet.Needs.Satiety >= e.ShopFullAbove) return ShopRefusal.NotNeeded;
                    break;
                default:
                    if (pet.IsSleeping) return ShopRefusal.Sleeping;
                    if (pet.Needs.Energy >= e.ShopFullAbove) return ShopRefusal.NotNeeded;
                    break;
            }
            return _wallet.Coins >= PriceOf(item) ? ShopRefusal.None : ShopRefusal.NotEnoughCoins;
        }

        /// <summary>Mua 1 món: kiểm tra, trừ Coin, cho thú dùng ngay. Bị từ chối thì không trừ tiền.</summary>
        public ShopRefusal Buy(ShopItem item)
        {
            if (_pets.HasPet) _pets.Tick();   // tính tới đúng bây giờ (thú có thể vừa khỏi / vừa ốm)
            ShopRefusal refusal = Check(item);
            if (refusal != ShopRefusal.None) return refusal;
            int price = PriceOf(item);
            if (!_wallet.TrySpend(price, "mua " + item)) return ShopRefusal.NotEnoughCoins;
            EconomyConfig e = _cfg.Economy;
            switch (item)
            {
                case ShopItem.Medicine:
                    _health.GiveMedicine();
                    break;
                case ShopItem.Treat:
                    _pets.Care(NeedType.Satiety, e.TreatSatiety);
                    _pets.Care(NeedType.Mood, e.TreatMood);
                    break;
                default:
                    _pets.Care(NeedType.Energy, e.EnergyDrinkEnergy);
                    break;
            }
            _log.Info("[PawFit] Quầy hàng: mua " + item + " giá " + price + " Coin");
            _bus.Publish(new ShopPurchased(item, price));
            return ShopRefusal.None;
        }
    }
}
