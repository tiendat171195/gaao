using System;
using System.Collections.Generic;
using PawFit.Battle;
using PawFit.Battle.View;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Garden;
using PawFit.Garden.View;
using PawFit.Online;
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
            Garden.ArenaTapped = OpenArena;
            if (services.TryGet(out TipService tips))   // vào game sau 1 thời gian vắng: "trong lúc bạn vắng..."
            {
                string welcome = tips.WelcomeBack(services.Get<PetService>().LastAwayHours);
                if (welcome != null) GardenUi.Toast(welcome, 5f);
            }
        }

        /// <summary>Chạm hình nộm ở sân tập: chọn Huấn luyện (học chiêu) hoặc Đấu (90 giây) với Bot mức đó.</summary>
        private static void OnDummyTapped(int level)
        {
            TipDone(TipId.Training);
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
            CancelSearch();   // đang tìm trận PvP mà vào sân tập thì thôi tìm
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
            TipDone(TipId.Shop);
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
        // Cổng đấu trường = PvP (Phase 4). Có máy chủ riêng: ghép + chạy trận + tính Elo ở máy chủ.
        // Chưa cấu hình máy chủ: mạng giả lập trên máy (người chơi ảo, Elo tính trên máy).
        // ------------------------------------------------------------------

        /// <summary>Chạm cổng đấu trường: thẻ rank (bậc, Elo, thắng / thua, mùa còn mấy ngày) + nút Tìm trận.</summary>
        public static void OpenArena()
        {
            if (Garden == null || Battle != null) return;
            TipDone(TipId.Arena);
            var rank = _services.Get<RankService>();
            RankData d = rank.Current;
            int daysLeft = Math.Max(0, (int)Math.Ceiling((rank.SeasonEndsUtc - _services.Get<ITimeProvider>().UtcNow).TotalDays));
            string body = "Bậc " + rank.Tier + " · Elo " + rank.Elo + " · thắng " + d.Wins + " / thua " + d.Losses + "\n" +
                          "Mùa " + rank.Season + " còn " + daysLeft + " ngày · thắng +" + _services.Get<GameConfig>().Economy.PvpWinCoins + " Coin";
            if (_services.TryGet(out OnlineService online))
                body += "\n" + (online.State == OnlineState.Online ? "Máy chủ: đã nối · " + online.Name
                                 : online.State == OnlineState.Connecting ? "Máy chủ: đang nối..." : "Máy chủ: chưa nối được");
            bool can = RankService.CanPlayRanked(_services.Get<PetService>(), _services.Get<HealthService>(), out string reason);
            GardenUi.ShowChoice("Đấu trường", body,
                                new[] { can ? "Tìm trận" : "Tìm trận (thú " + reason + ")", "Thôi" },
                                i => { if (i == 0) StartSearch(); },
                                new[] { can, true });
        }

        /// <summary>Xếp hàng tìm đối thủ (Elo ±100, lực chiến ±10%; 30 giây nới khoảng; 60 giây gợi ý đấu hình nộm).</summary>
        private static void StartSearch()
        {
            var net = _services.Get<INetworkManager>();
            var pets = _services.Get<PetService>();
            var evo = _services.Get<EvolutionService>();
            var rank = _services.Get<RankService>();
            if (net.State != NetworkState.Online)
            {
                if (net is LocalNetworkManager) net.Connect("local", null);   // giả lập: nối ngay
                else
                {
                    ConnectThenSearch(net);
                    return;
                }
            }
            if (net.State != NetworkState.Online) return;
            PetStats ps = evo.Stats;
            bool[] learned = evo.LearnedMask();
            int mask = 0;
            for (int i = 0; i < learned.Length; i++) if (learned[i]) mask |= 1 << i;
            var ticket = new MatchTicket
            {
                Name = "Bạn",
                PetName = pets.Current.Name,
                DnaCode = pets.DnaCode,
                Elo = rank.Elo,
                Power = ps.Power,
                Stage = pets.Current.Growth.Stage,
                Stats = new FighterStats(ps.Hp, ps.Atk, ps.Def, ps.Spd, ps.Crit),
                LearnedMask = mask,
                Form = evo.Stage >= EvolutionStage.Adult ? evo.Form : evo.LeaningForm,   // đối thủ dựng đúng dáng thú mình
                Accessory = pets.Cosmetics?.Equipped ?? AccessoryKind.None,
            };
            net.MatchFound -= OnMatchFound;
            net.MatchFound += OnMatchFound;
            net.FindMatch(ticket);
            ShowSearching(false);
        }

        /// <summary>
        /// Máy chủ riêng: chờ tài khoản đăng nhập xong + nối WebSocket, rồi mới tìm trận.
        /// Lỗi / quá 12 giây: báo lý do + gợi ý đấu hình nộm ở sân tập.
        /// </summary>
        private static void ConnectThenSearch(INetworkManager net)
        {
            _services.TryGet(out OnlineService online);
            if (online != null && online.State == OnlineState.Offline) online.OnResume();   // thử nối lại ngay
            float started = Time.realtimeSinceStartup;
            bool asked = false, done = false;
            GardenUi.ShowLive("Đang nối máy chủ", () =>
            {
                if (done) return "";
                if (!asked && (online == null || online.State == OnlineState.Online))
                {
                    asked = true;
                    net.Connect(null, ok =>
                    {
                        if (done || Garden == null || Battle != null || !GardenUi.IsOpen) return;   // đã hủy / đã sang màn khác
                        done = true;
                        if (ok) StartSearch();
                        else ShowConnectFailed(net);
                    });
                }
                if (!done && !asked && Time.realtimeSinceStartup - started > 12f)
                {
                    done = true;
                    ShowConnectFailed(net);
                    return "";
                }
                return "Chờ chút nhé...";
            }, new[] { "Hủy" }, i =>
            {
                done = true;
                GardenUi.Close();
            });
        }

        private static void ShowConnectFailed(INetworkManager net)
        {
            string why = net is ServerNetworkManager s && !string.IsNullOrEmpty(s.LastError) ? s.LastError
                       : _services.TryGet(out OnlineService online) && !string.IsNullOrEmpty(online.LastError) ? online.LastError
                       : "Không kết nối được máy chủ";
            GardenUi.ShowChoice("Chưa vào đấu trường được", why + "\nĐấu hình nộm ở sân tập trong lúc chờ nhé?",
                                new[] { "Đấu hình nộm", "Thôi" }, i => { if (i == 0) EnterBattle(BattleMode.BotFight, BotLevel.Normal); });
        }

        private static void ShowSearching(bool suggestBot)
        {
            var net = _services.Get<INetworkManager>();
            var cfg = _services.Get<GameConfig>().Matchmaking;
            int elo = _services.Get<RankService>().Elo;
            string[] buttons = suggestBot ? new[] { "Đấu hình nộm", "Hủy tìm" } : new[] { "Hủy tìm" };
            GardenUi.ShowLive("Đang tìm đối thủ", () =>
            {
                // máy chủ rớt giữa lúc tìm, hoặc máy chủ từ chối vé (vd chỉ số thú không hợp lệ)
                if (net.State != NetworkState.Searching && net.State != NetworkState.InMatch)
                {
                    net.MatchFound -= OnMatchFound;
                    ShowConnectFailed(net);
                    return "";
                }
                double t = net.SearchSeconds;
                MatchSearchStatus status = net.SearchStatus;
                if (status == MatchSearchStatus.SuggestBot && !suggestBot && net.State == NetworkState.Searching)
                    ShowSearching(true);   // 60 giây: thêm nút đấu hình nộm (vẫn tìm tiếp)
                string line = ((int)t / 60) + ":" + ((int)t % 60).ToString("00") + "\nElo " + elo + " ±" + MatchmakingRules.EloWindow(t, cfg) +
                              " · lực chiến ±" + Mathf.RoundToInt(MatchmakingRules.PowerWindow(t, cfg) * 100f) + "%";
                if (status == MatchSearchStatus.Widened) line += "\nĐã nới khoảng tìm";
                if (status == MatchSearchStatus.SuggestBot) line += "\nChưa thấy ai hợp — đấu hình nộm trong lúc chờ?";
                return line;
            }, buttons, i =>
            {
                CancelSearch();
                GardenUi.Close();
                if (suggestBot && i == 0) EnterBattle(BattleMode.BotFight, BotLevel.Normal);
            });
        }

        private static void CancelSearch()
        {
            if (!_services.TryGet(out INetworkManager net) || net.State != NetworkState.Searching) return;
            net.MatchFound -= OnMatchFound;
            net.CancelFindMatch();
        }

        /// <summary>Ghép được: thẻ "Tìm thấy đối thủ" 2,4 giây rồi vào trận.</summary>
        private static void OnMatchFound(MatchInfo match)
        {
            var net = _services.Get<INetworkManager>();
            net.MatchFound -= OnMatchFound;
            string reason = null;
            if (Garden == null || Battle != null || !RankService.CanPlayRanked(_services.Get<PetService>(), _services.Get<HealthService>(), out reason))
            {
                net.LeaveMatch();   // thú vừa ốm / ngủ / mất trong lúc tìm, hoặc đang ở trận khác
                if (Battle == null)
                {
                    GardenUi.Close();
                    GardenUi.Toast("Hủy trận: thú " + (reason ?? "không đấu được lúc này"));
                }
                return;
            }
            OpponentInfo o = match.Opponent;
            var rank = _services.Get<RankService>();
            string me = _services.Get<PetService>().Current.Name + " · " + rank.Tier + " " + rank.Elo;
            string them = o.PetName + " (của " + o.Name + ") · " + TierName(o.Elo) + " " + o.Elo;
            GardenUi.ShowLive("Tìm thấy đối thủ!", () => me + "\nVS\n" + them + "\nLực chiến " + Mathf.RoundToInt(o.Power), (string[])null, null);
            Garden.StartCoroutine(StartRankedAfter(match, 2.4f));
        }

        private static System.Collections.IEnumerator StartRankedAfter(MatchInfo match, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            EnterRanked(match);
        }

        /// <summary>Vào trận PvP (màn ngang). Hết trận: RankService tính Elo + Coin, thẻ kết quả hiện thay đổi.</summary>
        private static void EnterRanked(MatchInfo match)
        {
            var net = _services.Get<INetworkManager>();
            string reason = null;
            if (Garden == null || Battle != null || !RankService.CanPlayRanked(_services.Get<PetService>(), _services.Get<HealthService>(), out reason))
            {
                net.LeaveMatch();   // trong 2,4 giây chờ: thú vừa ốm / ngủ / mất, hoặc đã vào trận khác
                if (Battle == null)
                {
                    GardenUi.Close();
                    GardenUi.Toast("Hủy trận: thú " + (reason ?? "không đấu được lúc này"));
                }
                return;
            }
            OpponentInfo o = match.Opponent;
            var cfg = _services.Get<GameConfig>();
            NetMatchFeed feed = match.Feed;   // != null: trận qua máy chủ (máy chủ chạy trận + tính Elo)
            var view = new RankedMatchView
            {
                Opponent = feed != null ? new RankedOpponent { Name = o.PetName, Stats = o.Stats } : BotPlayers.ToRanked(o),
                Feed = feed,
                Seed = match.Seed,
                PetName = o.PetName,
                PlayerName = o.Name,
                Title = TierName(o.Elo) + " · Elo " + o.Elo,
                Dna = o.Dna ?? DnaCodec.Decode(o.DnaCode),
                Look = BotPlayers.LookFor(o.Stage, o.Form, cfg.Evolution),
                Accessory = o.Accessory,
                OnResult = r => feed != null ? ServerRankLines(feed) : RankLines(_services.Get<RankService>().ApplyMatch(o.Elo, r.Outcome)),
            };
            GardenUi.Close();
            Garden.SetPaused(true);
            Battle = BattleScene.EnterRanked(_services, view, () =>
            {
                net.LeaveMatch();
                Battle = null;
                if (Garden != null) Garden.SetPaused(false);
            });
        }

        /// <summary>Trận qua máy chủ: Elo / Coin theo máy chủ gửi về (bỏ cuộc / mất kết nối: hỏi lại máy chủ sau).</summary>
        private static List<string> ServerRankLines(NetMatchFeed feed)
        {
            EndMsg end = feed.End;
            if (end == null || end.Rank == null)
            {
                if (_services.TryGet(out OnlineService online)) online.Refresh();
                return new List<string> { feed.Lost ? "Mất kết nối máy chủ: tính là bỏ cuộc" : "Bỏ cuộc", "Elo cập nhật khi nối lại máy chủ" };
            }
            List<string> lines = RankLines(_services.Get<RankService>().ApplyServerMatch(end.Rank, end.EloBefore, end.Coins));
            if (end.Reason == "left" && feed.Won) lines.Insert(0, "Đối thủ đã rời trận");
            return lines;
        }

        private static List<string> RankLines(RankResult r)
        {
            var lines = new List<string> { "Elo " + r.EloBefore + " → " + r.EloAfter + " (" + (r.Delta >= 0 ? "+" : "") + r.Delta + ")" };
            if (r.Promoted) lines.Add("Lên bậc " + r.TierAfter + "!");
            else if (r.Demoted) lines.Add("Xuống bậc " + r.TierAfter);
            else lines.Add("Bậc " + r.TierAfter);
            if (r.CoinsWon > 0) lines.Add("+" + r.CoinsWon + " Coin");
            return lines;
        }

        private static void TipDone(TipId id)
        {
            if (_services.TryGet(out TipService tips)) tips.Done(id);
        }

        private static string TierName(int elo) => RankRules.TierOf(elo, _services.Get<GameConfig>().Rank).ToString();

        // ------------------------------------------------------------------
        // Hòm thư = cài đặt (GDD: "cài đặt = hòm thư ở cổng")
        // ------------------------------------------------------------------

        /// <summary>Chạm hòm thư: âm thanh, tài khoản (biệt danh, mã chuyển máy), xem lại hướng dẫn, copy mã DNA, phiên bản game.</summary>
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
            if (_services.TryGet(out OnlineService online))   // máy chủ riêng: tài khoản + chuyển máy
            {
                body += "\nTài khoản: " + (online.Name ?? "chưa có") + " · " +
                        (online.State == OnlineState.Online ? "đã nối máy chủ" : online.State == OnlineState.Connecting ? "đang nối..." : "chưa nối được");
                bool on = online.State == OnlineState.Online && !online.RestartPending;
                labels.Add("Đổi biệt danh");
                enabled.Add(on);
                actions.Add(() => AskNickname(online));
                labels.Add("Mã chuyển máy (sang máy mới)");
                enabled.Add(on);
                actions.Add(() => ShowTransferCode(online));
                labels.Add("Nhập mã chuyển máy");
                enabled.Add(on);
                actions.Add(() => AskTransferCode(online));
            }
            if (_services.TryGet(out NotificationService notify) && notify.Supported)   // thông báo nhắc chăm thú (Phase 4.5)
            {
                labels.Add("Thông báo nhắc chăm bé: " + (notify.Enabled ? "Bật (chạm để tắt)" : "Tắt (chạm để bật)"));
                enabled.Add(true);
                actions.Add(() =>
                {
                    if (notify.Enabled)
                    {
                        notify.Disable();
                        GardenUi.Toast("Đã tắt thông báo");
                    }
                    else EnableNotifications(notify);
                });
            }
            labels.Add("Xem lại hướng dẫn");
            enabled.Add(true);
            actions.Add(() =>
            {
                if (_services.TryGet(out TipService tips)) tips.Restart();
                GardenUi.Toast("Mẹo hướng dẫn sẽ hiện lại từ đầu");
            });
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

        /// <summary>Hỏi bật thông báo (1 lần, sau khi chơi một lúc): thẻ giải thích trước, đồng ý mới xin quyền của máy.</summary>
        public static void AskNotifications()
        {
            if (Garden == null || Battle != null || GardenUi.IsOpen || !_services.TryGet(out NotificationService notify)) return;
            var pets = _services.Get<PetService>();
            string pet = pets.HasPet ? pets.Current.Name : "bé";
            GardenUi.ShowChoice("Bật thông báo?",
                                "PawFit sẽ báo khi " + pet + " đói, buồn ngủ, lấm lem hay bị ốm lúc bạn không mở game.\n" +
                                "Đêm 22:00–08:00 không báo. Tắt lúc nào cũng được ở hòm thư.",
                                new[] { "Bật thông báo", "Để sau" }, i =>
                                {
                                    if (i == 0) EnableNotifications(notify);
                                    else
                                    {
                                        notify.Disable();
                                        GardenUi.Toast("Bật lại lúc nào cũng được ở hòm thư");
                                    }
                                });
        }

        private static void EnableNotifications(NotificationService notify)
        {
            notify.Enable(ok =>
            {
                if (ok)
                {
                    GardenUi.Toast("Đã bật thông báo nhắc chăm bé");
                    return;
                }
                GardenUi.ShowChoice("Máy đang chặn thông báo", "Mở Cài đặt của máy để cho PawFit gửi thông báo?",
                                    new[] { "Mở Cài đặt", "Thôi" }, i => { if (i == 0) notify.OpenSystemSettings(); });
            });
        }

        private static void AskNickname(OnlineService online)
        {
            GardenUi.AskName("Biệt danh của bạn", name =>
                online.Rename(name, (ok, msg) => GardenUi.Toast(ok ? "Biệt danh mới: " + msg : "Chưa đổi được: " + msg)), "Biệt danh...");
        }

        /// <summary>Máy cũ: xin mã chuyển máy (dùng 1 lần, có hạn) để nhập ở máy mới.</summary>
        private static void ShowTransferCode(OnlineService online)
        {
            GardenUi.ShowLive("Mã chuyển máy", () => "Đang lấy mã...", (string[])null, null);
            online.RequestTransferCode((reply, error) =>
            {
                if (Garden == null || Battle != null) return;
                if (reply == null)
                {
                    GardenUi.Close();
                    GardenUi.Toast("Chưa lấy được mã: " + error);
                    return;
                }
                string until = NetProto.FromMs(reply.ExpiresUtcMs).ToLocalTime().ToString("dd/MM HH:mm");
                GardenUi.ShowChoice("Mã chuyển máy", reply.Code + "\nDùng 1 lần, hạn tới " + until +
                                    ".\nỞ máy mới: Hòm thư ▸ Nhập mã chuyển máy. Nhập xong thì thú sang máy mới, máy này thành tài khoản khác.",
                                    new[] { "Copy mã", "Đóng" }, i =>
                                    {
                                        if (i != 0) return;
                                        GUIUtility.systemCopyBuffer = reply.Code;
                                        GardenUi.Toast("Đã copy " + reply.Code);
                                    });
            });
        }

        /// <summary>Máy mới: nhập mã → nhận tài khoản + thú (đặt chờ), đóng game mở lại thì thay dữ liệu.</summary>
        private static void AskTransferCode(OnlineService online)
        {
            GardenUi.AskName("Nhập mã chuyển máy", code =>
                GardenUi.ShowChoice("Chuyển dữ liệu về máy này?",
                                    "Thú, Coin, đồ và rank trên máy này sẽ bị THAY bằng dữ liệu của mã. Không hoàn tác được.",
                                    new[] { "Chuyển", "Thôi" }, i =>
                                    {
                                        if (i != 0) return;
                                        GardenUi.ShowLive("Đang chuyển dữ liệu", () => "Chờ chút nhé...", (string[])null, null);
                                        online.RedeemTransferCode(code, (ok, msg) =>
                                        {
                                            GardenUi.Close();
                                            if (!ok || !online.RestartPending)
                                            {
                                                GardenUi.ShowCard(ok ? "Đã nhận tài khoản" : "Chưa chuyển được", msg);
                                                return;
                                            }
                                            GardenUi.ShowChoice("Đã nhận dữ liệu", msg, new[] { "Đóng game" }, _ => QuitGame());
                                        });
                                    }), "XXXX-XXXX-XXXX");
        }

        /// <summary>Đóng game để lần mở sau thay dữ liệu chuyển máy (iOS không cho app tự tắt: nhắc người chơi tự tắt).</summary>
        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#elif UNITY_IOS
            GardenUi.ShowCard("Mở lại game", "Vuốt tắt hẳn PawFit (đa nhiệm) rồi mở lại để chơi tiếp với thú vừa chuyển.");
#else
            Application.Quit();
#endif
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
