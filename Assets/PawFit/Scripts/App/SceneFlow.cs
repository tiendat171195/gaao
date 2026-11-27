using System;
using System.Collections.Generic;
using PawFit.Battle;
using PawFit.Battle.View;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Garden.View;
using PawFit.Pet;
using UnityEngine;

namespace PawFit.App
{
    /// <summary>
    /// Chuyển màn: Vườn (dọc) ⇄ Sân tập / Màn đấu (ngang). Phase 5.1: quầy hàng + quảng cáo giả lập trong vườn.
    /// Phase 5.2: thẻ Phụ kiện / Trang trí trong quầy hàng.
    /// Phase 4.2 thêm PvP ở cổng đấu trường. Chỉ nối UI với service — luật nằm ở BattleSession / ShopService / EconomyService.
    /// </summary>
    public static class SceneFlow
    {
        private static readonly string[] DummyNames = { "Hình nộm Dễ", "Hình nộm Thường", "Hình nộm Khó" };
        private static readonly string[] AccessoryNames = { "", "Nơ hồng", "Mũ len", "Khăn quàng", "Vương miện" };
        private static readonly string[] DecorNames = { "", "Dây đèn lồng", "Ghế gỗ", "Cối xay gió", "Vòm hoa" };
        private static ServiceLocator _services;

        public static GardenScene Garden { get; private set; }
        public static BattleScene Battle { get; private set; }

        public static void EnterGarden(ServiceLocator services)
        {
            if (Garden != null) return;
            _services = services;
            Garden = GardenScene.Enter(services);
            Garden.DummyTapped = OnDummyTapped;
            Garden.ShopTapped = OpenShop;
            Garden.MailboxTapped = OpenSettings;
        }

        /// <summary>Chạm hình nộm ở sân tập: chọn Huấn luyện (học chiêu) hoặc Đấu (90 giây) với Bot mức đó.</summary>
        private static void OnDummyTapped(int level)
        {
            var pets = _services.Get<PetService>();
            if (!BattleSession.CanStart(pets, _services.Get<HealthService>(), out string reason))
            {
                GardenUi.ShowCard("Chưa vào sân tập được", "Thú " + reason + ".");
                return;
            }
            int[] wins = _services.Get<GameConfig>().Economy.BotWinCoins;
            int coins = wins.Length > 0 ? wins[Mathf.Clamp(level, 0, wins.Length - 1)] : 0;
            GardenUi.ShowChoice(DummyNames[level],
                                "Học chiêu với hình nộm (vuốt đúng 10 lần = thuộc chiêu), hoặc đấu thử 90 giây (thắng +" + coins + " Coin).",
                                new[] { "Huấn luyện", "Đấu", "Thôi" }, i =>
                                {
                                    if (i == 0) EnterBattle(BattleMode.Training, (BotLevel)level);
                                    else if (i == 1) EnterBattle(BattleMode.BotFight, (BotLevel)level);
                                });
        }

        /// <summary>Vào màn đấu (vườn tạm dừng), xong thì quay về vườn.</summary>
        public static void EnterBattle(BattleMode mode, BotLevel level)
        {
            if (Battle != null || Garden == null) return;
            var pets = _services.Get<PetService>();
            if (!BattleSession.CanStart(pets, _services.Get<HealthService>(), out string reason))
            {
                GardenUi.ShowCard("Chưa vào sân tập được", "Thú " + reason + ".");
                return;
            }
            GardenUi.Close();
            Garden.SetPaused(true);
            Battle = BattleScene.Enter(_services, mode, level, () =>
            {
                Battle = null;
                if (Garden != null) Garden.SetPaused(false);
            });
        }

        // ------------------------------------------------------------------
        // Quầy hàng (Phase 5.1)
        // ------------------------------------------------------------------

        /// <summary>Chạm quầy hàng: thẻ có số Coin, 3 món dùng ngay (xám nếu chưa mua được), Phụ kiện, Trang trí, 2 kiểu quảng cáo, Thôi.</summary>
        public static void OpenShop()
        {
            if (Garden == null || Battle != null) return;
            var shop = _services.Get<ShopService>();
            var eco = _services.Get<EconomyService>();
            EconomyConfig e = eco.Config;
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            foreach (ShopItem item in ShopService.Items)
            {
                ShopRefusal r = shop.Check(item);
                labels.Add(ItemName(item) + " · " + shop.PriceOf(item) + " Coin" + (r == ShopRefusal.None ? "" : " (" + Why(r, item) + ")"));
                enabled.Add(r == ShopRefusal.None);
                actions.Add(() => Buy(item));
            }
            labels.Add("Phụ kiện cho bé...");
            enabled.Add(true);
            actions.Add(OpenAccessories);
            labels.Add("Trang trí vườn...");
            enabled.Add(true);
            actions.Add(OpenDecor);
            AdRefusal coinAd = eco.CanWatchAd(AdReward.Coins);
            labels.Add("Xem quảng cáo: +" + e.AdCoins + " Coin" + AdNote(coinAd));
            enabled.Add(coinAd == AdRefusal.None);
            actions.Add(() => WatchAd(AdReward.Coins));
            AdRefusal energyAd = eco.CanWatchAd(AdReward.Energy);
            labels.Add("Xem quảng cáo: +" + e.AdEnergy.ToString("0") + " năng lượng" + AdNote(energyAd));
            enabled.Add(energyAd == AdRefusal.None);
            actions.Add(() => WatchAd(AdReward.Energy));
            labels.Add("Thôi");
            enabled.Add(true);
            actions.Add(null);
            string body = "Bạn có " + eco.Coins + " Coin · quảng cáo còn " + eco.AdsLeftToday + "/" + eco.MaxAdsPerDay + " lượt hôm nay\n" +
                          "Đồ ăn thường ở chòi ăn vẫn miễn phí";
            GardenUi.ShowChoice("Quầy hàng", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        private static void Buy(ShopItem item)
        {
            var shop = _services.Get<ShopService>();
            int price = shop.PriceOf(item);
            ShopRefusal r = shop.Buy(item);
            if (r != ShopRefusal.None)
            {
                GardenUi.Toast("Chưa mua được: " + Why(r, item));
                return;
            }
            PetController pet = Garden != null ? Garden.Pet : null;
            if (pet != null && pet.Avatar != null) pet.Avatar.PlayEat();   // bé ăn bánh / uống nước / uống thuốc ngay
            GardenUi.Toast(ItemName(item) + ": -" + price + " Coin · còn " + _services.Get<EconomyService>().Coins + " Coin");
        }

        // ------------------------------------------------------------------
        // Hòm thư = cài đặt (GDD: "cài đặt = hòm thư ở cổng")
        // ------------------------------------------------------------------

        /// <summary>Chạm hòm thư: bật / tắt âm thanh, xem + copy mã DNA của thú, phiên bản game.</summary>
        public static void OpenSettings()
        {
            if (Garden == null || Battle != null) return;
            var settings = _services.Get<SettingsService>();
            var pets = _services.Get<PetService>();
            string dna = pets.DnaCode;
            string body = (dna != null ? "Mã DNA của " + pets.Current.Name + ": " + dna : "Chưa có thú (trứng đang chờ nở)") +
                          "\nPawFit " + Application.version;
            var labels = new List<string> { "Âm thanh: " + (settings.SoundOn ? "Bật (chạm để tắt)" : "Tắt (chạm để bật)") };
            var enabled = new List<bool> { true };
            var actions = new List<Action> { () =>
            {
                settings.ToggleSound();
                GardenUi.Toast(settings.SoundOn ? "Đã bật âm thanh" : "Đã tắt âm thanh");
                OpenSettings();
            } };
            labels.Add("Copy mã DNA");
            enabled.Add(dna != null);
            actions.Add(() =>
            {
                GUIUtility.systemCopyBuffer = dna;   // dán vào tin nhắn để khoe với bạn bè
                GardenUi.Toast("Đã copy " + dna);
            });
            labels.Add("Thôi");
            enabled.Add(true);
            actions.Add(null);
            GardenUi.ShowChoice("Hòm thư", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        /// <summary>Thẻ Phụ kiện: món chưa có = mua (mua xong đeo luôn); món đã có = chạm để đeo / tháo. Mỗi lúc đeo 1 món.</summary>
        private static void OpenAccessories()
        {
            var cos = _services.Get<CosmeticsService>();
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            foreach (AccessoryKind a in CosmeticsService.Accessories)
            {
                string name = AccessoryNames[(int)a];
                if (cos.Owns(a))
                {
                    bool wearing = cos.Equipped == a;
                    labels.Add(name + (wearing ? " · đang đeo (chạm để tháo)" : " · đã có (chạm để đeo)"));
                    enabled.Add(true);
                    actions.Add(() =>
                    {
                        cos.Toggle(a);
                        GardenUi.Toast(wearing ? "Đã tháo " + name : "Bé đeo " + name);
                    });
                    continue;
                }
                CosmeticRefusal r = cos.CheckBuy(a);
                labels.Add(name + " · " + cos.PriceOf(a) + " Coin" + (r == CosmeticRefusal.NotEnoughCoins ? " (thiếu Coin)" : ""));
                enabled.Add(r == CosmeticRefusal.None);
                actions.Add(() => BuyAccessory(a));
            }
            labels.Add("Quay lại");
            enabled.Add(true);
            actions.Add(OpenShop);
            string body = "Bạn có " + _services.Get<EconomyService>().Coins + " Coin · mỗi lúc đeo 1 món\nChỉ để đẹp, không đổi chỉ số";
            if (!_services.Get<PetService>().HasPet) body += " · thú mới nở sẽ đeo";
            GardenUi.ShowChoice("Phụ kiện", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        private static void BuyAccessory(AccessoryKind a)
        {
            var cos = _services.Get<CosmeticsService>();
            int price = cos.PriceOf(a);
            CosmeticRefusal r = cos.Buy(a);
            GardenUi.Toast(r == CosmeticRefusal.None
                               ? "Bé đeo " + AccessoryNames[(int)a] + " · -" + price + " Coin · còn " + _services.Get<EconomyService>().Coins + " Coin"
                               : "Chưa mua được: " + Why(r));
        }

        /// <summary>Thẻ Trang trí: mua 1 lần, món đó đứng luôn ở chỗ của nó trong vườn (camera lia tới cho xem).</summary>
        private static void OpenDecor()
        {
            var cos = _services.Get<CosmeticsService>();
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            foreach (DecorKind d in CosmeticsService.Decorations)
            {
                string name = DecorNames[(int)d];
                CosmeticRefusal r = cos.CheckBuy(d);
                labels.Add(r == CosmeticRefusal.AlreadyOwned ? name + " (đã có trong vườn)"
                               : name + " · " + cos.PriceOf(d) + " Coin" + (r == CosmeticRefusal.NotEnoughCoins ? " (thiếu Coin)" : ""));
                enabled.Add(r == CosmeticRefusal.None);
                actions.Add(() => BuyDecor(d));
            }
            labels.Add("Quay lại");
            enabled.Add(true);
            actions.Add(OpenShop);
            string body = "Bạn có " + _services.Get<EconomyService>().Coins + " Coin\nMua 1 lần là có mãi, mỗi món có chỗ riêng trong vườn";
            GardenUi.ShowChoice("Trang trí vườn", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        private static void BuyDecor(DecorKind d)
        {
            var cos = _services.Get<CosmeticsService>();
            int price = cos.PriceOf(d);
            CosmeticRefusal r = cos.Buy(d);   // vườn nghe DecorAdded: dựng món mới + camera lia tới
            GardenUi.Toast(r == CosmeticRefusal.None
                               ? DecorNames[(int)d] + " đã vào vườn · -" + price + " Coin · còn " + _services.Get<EconomyService>().Coins + " Coin"
                               : "Chưa mua được: " + Why(r));
        }

        private static string Why(CosmeticRefusal r) =>
            r == CosmeticRefusal.NotEnoughCoins ? "thiếu Coin" : r == CosmeticRefusal.AlreadyOwned ? "đã có rồi" : r.ToString();

        /// <summary>Quảng cáo tự nguyện: màn giả lập đếm ngược (bản dev), xem hết thì thưởng, xong mở lại quầy.</summary>
        private static void WatchAd(AdReward reward)
        {
            var eco = _services.Get<EconomyService>();
            bool started = eco.WatchAd(reward, outcome =>
            {
                GardenUi.Close();   // tắt màn quảng cáo giả
                GardenUi.Toast(AdText(outcome, eco));
                if (Garden != null && Battle == null) OpenShop();
            });
            if (started && eco.Ads is MockAdService mock && mock.IsShowing)
                GardenUi.ShowLive("Quảng cáo (giả lập)",
                                  () => "Bản dev chưa có quảng cáo thật.\nXem hết để nhận thưởng · còn " +
                                        Mathf.CeilToInt(Mathf.Max(0f, mock.SecondsLeft)) + " giây",
                                  "Bỏ qua (không nhận thưởng)", mock.Skip);
        }

        private static string ItemName(ShopItem item) =>
            item == ShopItem.Medicine ? "Thuốc chữa bệnh" : item == ShopItem.Treat ? "Bánh thưởng (no + vui)" : "Nước tăng lực";

        private static string Why(ShopRefusal r, ShopItem item)
        {
            switch (r)
            {
                case ShopRefusal.NoPet: return "chưa có thú";
                case ShopRefusal.NotIll: return "bé đang khỏe";
                case ShopRefusal.AlreadyMedicated: return "đã uống thuốc rồi";
                case ShopRefusal.Sleeping: return "bé đang ngủ";
                case ShopRefusal.NotNeeded: return item == ShopItem.Treat ? "bé no rồi" : "bé còn khỏe re";
                case ShopRefusal.NotEnoughCoins: return "thiếu Coin";
                default: return r.ToString();
            }
        }

        private static string AdNote(AdRefusal r)
        {
            switch (r)
            {
                case AdRefusal.None: return "";
                case AdRefusal.DailyLimit: return " (hết lượt hôm nay)";
                case AdRefusal.NoPet: return " (chưa có thú)";
                case AdRefusal.EnergyFull: return " (bé còn đầy sức)";
                default: return " (chưa có quảng cáo)";
            }
        }

        private static string AdText(AdOutcome o, EconomyService eco)
        {
            if (o.Refusal != AdRefusal.None) return "Chưa xem được quảng cáo" + AdNote(o.Refusal);
            switch (o.Result)
            {
                case AdResult.Completed:
                    string gift = o.Reward == AdReward.Coins ? "+" + o.Amount.ToString("0") + " Coin" : "+" + o.Amount.ToString("0") + " năng lượng cho bé";
                    return gift + " · còn " + eco.AdsLeftToday + " lượt hôm nay";
                case AdResult.Skipped: return "Bỏ qua giữa chừng nên không có thưởng";
                default: return "Quảng cáo lỗi, thử lại sau nhé";
            }
        }
    }
}
