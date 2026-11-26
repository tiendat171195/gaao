using System;
using System.Globalization;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Economy
{
    /// <summary>Thưởng khi xem hết 1 quảng cáo: +20 Coin, hoặc +20 năng lượng cho thú (GDD G11).</summary>
    public enum AdReward { Coins = 0, Energy = 1 }

    /// <summary>Lý do không cho xem quảng cáo lúc này (khác None = không chiếu gì cả).</summary>
    public enum AdRefusal
    {
        None = 0,

        /// <summary>Hôm nay đã xem đủ 5 lần.</summary>
        DailyLimit = 1,

        /// <summary>Đang chiếu 1 quảng cáo khác.</summary>
        Busy = 2,

        /// <summary>Chưa có quảng cáo để chiếu.</summary>
        NotReady = 3,

        /// <summary>Lấy năng lượng mà không có thú (thú đã mất, đang chờ trứng nở).</summary>
        NoPet = 4,

        /// <summary>Lấy năng lượng mà thú còn đầy năng lượng (xem thì phí lượt).</summary>
        EnergyFull = 5,
    }

    /// <summary>Kết quả gửi lại View sau 1 lần mời xem quảng cáo.</summary>
    public struct AdOutcome
    {
        /// <summary>Loại thưởng thực nhận (lấy năng lượng mà thú vừa mất lúc đang xem thì đổi sang Coin).</summary>
        public AdReward Reward;
        public AdRefusal Refusal;
        public AdResult Result;

        /// <summary>Đã thưởng bao nhiêu (Coin hoặc điểm năng lượng). 0 = không thưởng.</summary>
        public float Amount;

        public bool Rewarded => Refusal == AdRefusal.None && Result == AdResult.Completed && Amount > 0f;
    }

    /// <summary>
    /// Kinh tế của người chơi (C# thuần): ví Coin + quảng cáo tự nguyện tối đa 5 lần / ngày (GDD G11).
    /// Ví nằm trong file lưu game (<see cref="PetService.Wallet"/>), đổi là lưu ngay. "Ngày" tính theo giờ trên máy,
    /// sang 0 giờ là có lại 5 lượt; chỉnh đồng hồ lùi về trước thì không được thêm lượt.
    /// Phase 4.1 đổi sang giờ server để chống chỉnh giờ.
    /// </summary>
    public sealed class EconomyService : ICoinWallet
    {
        public const string CoinsPlacement = "garden_shop_coins";
        public const string EnergyPlacement = "garden_shop_energy";

        private readonly PetService _pets;
        private readonly IAdService _ads;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly EconomyConfig _cfg;
        private readonly ILog _log;
        private readonly Func<DateTime, DateTime> _toLocal;

        /// <param name="toLocal">Đổi giờ UTC sang giờ địa phương để biết "hôm nay" (mặc định: múi giờ của máy).</param>
        public EconomyService(PetService pets, IAdService ads, ITimeProvider time, EventBus bus, GameConfig cfg,
                              ILog log = null, Func<DateTime, DateTime> toLocal = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = (cfg ?? throw new ArgumentNullException(nameof(cfg))).Economy;
            _log = log ?? NullLog.Instance;
            _toLocal = toLocal ?? (utc => utc.ToLocalTime());
        }

        public EconomyConfig Config => _cfg;
        public IAdService Ads => _ads;

        // ------------------------------------------------------------------
        // Ví Coin
        // ------------------------------------------------------------------

        public int Coins => _pets.Wallet?.Coins ?? 0;

        public bool CanAfford(int price) => price >= 0 && Coins >= price;

        /// <summary>Cộng Coin (thắng đấu, xem quảng cáo...). Ví có trần <see cref="EconomyConfig.MaxCoins"/>.</summary>
        public void Add(int amount, string reason)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Cộng Coin phải >= 0, muốn trừ thì dùng TrySpend");
            WalletData w = _pets.Wallet;
            if (w == null || amount == 0) return;
            int before = w.Coins;
            w.Coins = (int)Math.Min(_cfg.MaxCoins, (long)w.Coins + amount);
            int delta = w.Coins - before;
            if (delta <= 0) return;   // ví đã đầy
            w.Earned += delta;
            Changed(w, delta, reason);
        }

        /// <summary>Trừ Coin nếu đủ tiền; không đủ thì không trừ gì cả.</summary>
        public bool TrySpend(int amount, string reason)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số Coin tiêu phải >= 0");
            WalletData w = _pets.Wallet;
            if (w == null || w.Coins < amount) return false;
            if (amount == 0) return true;
            w.Coins -= amount;
            w.Spent += amount;
            Changed(w, -amount, reason);
            return true;
        }

        private void Changed(WalletData w, int delta, string reason)
        {
            _pets.Save();
            _log.Info("[PawFit] Coin " + (delta > 0 ? "+" : "") + delta + " (" + reason + ") · còn " + w.Coins);
            _bus.Publish(new CoinsChanged(w.Coins, delta, reason));
        }

        // ------------------------------------------------------------------
        // Quảng cáo tự nguyện
        // ------------------------------------------------------------------

        public int MaxAdsPerDay => Math.Max(0, _cfg.MaxAdsPerDay);

        /// <summary>Số lượt đã xem hết trong hôm nay (giờ trên máy).</summary>
        public int AdsWatchedToday
        {
            get
            {
                WalletData w = _pets.Wallet;
                if (w == null || string.IsNullOrEmpty(w.AdDay)) return 0;
                // đã sang ngày mới thì đếm lại từ 0; đồng hồ bị chỉnh lùi về ngày cũ thì vẫn giữ số lượt đã xem
                return string.CompareOrdinal(TodayKey(), w.AdDay) > 0 ? 0 : w.AdsToday;
            }
        }

        public int AdsLeftToday => Math.Max(0, MaxAdsPerDay - AdsWatchedToday);

        /// <summary>Có mời xem quảng cáo được không (None = được).</summary>
        public AdRefusal CanWatchAd(AdReward reward)
        {
            if (_ads.IsShowing) return AdRefusal.Busy;
            if (AdsLeftToday <= 0) return AdRefusal.DailyLimit;
            if (reward == AdReward.Energy)
            {
                if (!_pets.HasPet) return AdRefusal.NoPet;
                if (_pets.Current.Needs.Energy >= _cfg.AdEnergyFullAbove) return AdRefusal.EnergyFull;
            }
            return _ads.IsReady ? AdRefusal.None : AdRefusal.NotReady;
        }

        /// <summary>
        /// Người chơi tự chọn xem 1 quảng cáo. Xem hết thì thưởng + tính 1 lượt; bỏ qua / lỗi thì không thưởng, không tính lượt.
        /// `done` luôn được gọi đúng 1 lần (bị từ chối thì gọi ngay). Trả true nếu đã bắt đầu chiếu.
        /// </summary>
        public bool WatchAd(AdReward reward, Action<AdOutcome> done)
        {
            AdRefusal refusal = CanWatchAd(reward);
            if (refusal != AdRefusal.None)
            {
                done?.Invoke(new AdOutcome { Reward = reward, Refusal = refusal, Result = AdResult.Failed });
                return false;
            }
            _ads.ShowRewarded(reward == AdReward.Coins ? CoinsPlacement : EnergyPlacement, result =>
            {
                var outcome = new AdOutcome { Reward = reward, Result = result };
                if (result == AdResult.Completed)
                {
                    CountAd();
                    if (outcome.Reward == AdReward.Energy && !_pets.HasPet) outcome.Reward = AdReward.Coins;   // thú vừa mất lúc đang xem
                    outcome.Amount = Grant(outcome.Reward);
                }
                else _log.Info("[PawFit] Quảng cáo " + result + ": không có thưởng, không tính lượt");
                done?.Invoke(outcome);
            });
            return true;
        }

        private float Grant(AdReward reward)
        {
            if (reward == AdReward.Energy)
            {
                _pets.Care(NeedType.Energy, _cfg.AdEnergy);
                _log.Info("[PawFit] Xem quảng cáo: +" + _cfg.AdEnergy.ToString("0") + " năng lượng · còn " + AdsLeftToday + " lượt hôm nay");
                return _cfg.AdEnergy;
            }
            Add(_cfg.AdCoins, "xem quảng cáo");
            return _cfg.AdCoins;
        }

        private void CountAd()
        {
            WalletData w = _pets.Wallet;
            if (w == null) return;
            string today = TodayKey();
            if (string.IsNullOrEmpty(w.AdDay) || string.CompareOrdinal(today, w.AdDay) > 0)
            {
                w.AdDay = today;
                w.AdsToday = 0;
            }
            w.AdsToday++;
            _pets.Save();
        }

        private string TodayKey() => _toLocal(_time.UtcNow).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        // ------------------------------------------------------------------
        // CHỈ dùng cho công cụ test (Debug overlay)
        // ------------------------------------------------------------------

        /// <summary>Trả lại đủ 5 lượt quảng cáo hôm nay.</summary>
        public void ResetAdsForDebug()
        {
            WalletData w = _pets.Wallet;
            if (w == null) return;
            w.AdDay = null;
            w.AdsToday = 0;
            _pets.Save();
        }
    }
}
