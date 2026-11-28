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
    /// Phase 4.2 thêm PvP ở cổng đấu trường. Phase 7.2: Thám hiểm ở biển chỉ đường góc vườn.
    /// Chỉ nối UI với service — luật nằm ở BattleSession / ShopService / EconomyService / AdventureService.
    /// </summary>
    public static class SceneFlow
    {
        private static readonly string[] DummyNames = { "Hình nộm Dễ", "Hình nộm Thường", "Hình nộm Khó" };
        private static readonly string[] AccessoryNames = { "", "Nơ hồng", "Mũ len", "Khăn quàng", "Vương miện" };
        private static readonly string[] DecorNames = { "", "Dây đèn lồng", "Ghế gỗ", "Cối xay gió", "Vòm hoa" };
        private static readonly string[] ToyNames = { "", "Bóng cao su", "Cần lông vũ", "Máy thổi bong bóng" };
        private static readonly string[] ToyUses = { "", "vuốt để ném, bé chạy nhặt mang về", "kéo đi khắp vườn, bé đuổi theo vồ", "chạm để thổi, bé nhảy lên đớp" };
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
            Garden.AdventureTapped = OpenAdventure;
            if (services.TryGet(out DailyService daily))   // việc mỗi ngày (Phase 7.3): báo xong nhiệm vụ / có quà
            {
                SnapshotQuests(daily);
                services.Get<EventBus>().Subscribe<DailyChanged>(_ => OnDailyChanged(daily));
                if (daily.GiftReady) GardenUi.Toast("Hòm thư có quà ngày " + daily.GiftDay + "/7 cho bạn!", 3f);
            }
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
            Battle = BattleScene.Enter(_services, mode, level, BattleClosed);
        }

        /// <summary>Rời màn đấu (mọi kiểu trận): mở lại vườn, báo nhiệm vụ ngày vừa xong trong trận.</summary>
        private static void BattleClosed()
        {
            Battle = null;
            if (Garden != null) Garden.SetPaused(false);
            FlushQuestToast();
        }

        // ------------------------------------------------------------------
        // Việc mỗi ngày (Phase 7.3): quà mở game + 3 nhiệm vụ nhận ở hòm thư
        // ------------------------------------------------------------------

        private static bool[] _questDone = new bool[0];
        private static string _questDay;
        private static readonly List<string> _questToasts = new List<string>();

        private static void SnapshotQuests(DailyService daily)
        {
            IReadOnlyList<QuestState> q = daily.Quests;
            _questDay = daily.Today;
            _questDone = new bool[q.Count];
            for (int i = 0; i < q.Count; i++) _questDone[i] = q[i].Done;
        }

        /// <summary>Nhiệm vụ vừa xong: báo ngay ở vườn; đang đấu thì để lúc về vườn mới báo.</summary>
        private static void OnDailyChanged(DailyService daily)
        {
            IReadOnlyList<QuestState> q = daily.Quests;
            if (daily.Today == _questDay && q.Count == _questDone.Length)
                for (int i = 0; i < q.Count; i++)
                    if (q[i].Done && !_questDone[i]) _questToasts.Add(DailyService.QuestText(q[i]));
            SnapshotQuests(daily);
            if (Battle == null) FlushQuestToast();
        }

        private static void FlushQuestToast()
        {
            if (_questToasts.Count == 0 || Garden == null) return;
            GardenUi.Toast("Xong nhiệm vụ: " + string.Join(" · ", _questToasts) + ". Mở hòm thư nhận Coin!", 3.5f);
            _questToasts.Clear();
        }

        /// <summary>Thẻ nhiệm vụ hôm nay: mỗi nhiệm vụ 1 dòng (xong thì bấm nhận Coin), thưởng thêm khi xong cả 3.</summary>
        private static void OpenQuests()
        {
            var daily = _services.Get<DailyService>();
            IReadOnlyList<QuestState> quests = daily.Quests;
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            for (int i = 0; i < quests.Count; i++)
            {
                QuestState q = quests[i];
                int qi = i;
                string text = DailyService.QuestText(q);
                if (q.Claimed) labels.Add("Đã nhận: " + text);
                else if (q.Done) labels.Add("Nhận +" + q.Coins + " Coin: " + text);
                else labels.Add(text + " · " + q.Progress + "/" + q.Target + " · +" + q.Coins + " Coin");
                enabled.Add(daily.CanClaim(i));
                actions.Add(() =>
                {
                    int coins = daily.Claim(qi);
                    if (coins > 0) GardenUi.Toast("+" + coins + " Coin");
                    OpenQuests();
                });
            }
            labels.Add(daily.BonusReady ? "Nhận thưởng xong cả 3: +" + daily.Config.AllDoneBonusCoins + " Coin"
                                        : "Xong cả 3 nhiệm vụ: thêm +" + daily.Config.AllDoneBonusCoins + " Coin");
            enabled.Add(daily.BonusReady);
            actions.Add(() =>
            {
                int coins = daily.ClaimBonus();
                if (coins > 0) GardenUi.Toast("+" + coins + " Coin · giỏi lắm!");
                OpenQuests();
            });
            labels.Add("Quay lại");
            enabled.Add(true);
            actions.Add(OpenSettings);
            string body = "Đã xong " + daily.DoneCount + "/" + quests.Count + " · nhiệm vụ mới mỗi ngày\n" +
                          "Kho báu trong vườn hôm nay còn " + daily.DigsLeft;
            GardenUi.ShowChoice("Nhiệm vụ hôm nay", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        // ------------------------------------------------------------------
        // Thám hiểm (Phase 7.2): biển chỉ đường góc sau bên trái vườn
        // ------------------------------------------------------------------

        private static readonly string[] DifficultyNames = { "Dễ", "Vừa", "Khó", "Rất khó" };
        private static readonly string[] StyleNames = { "cân bằng", "hăng máu, đánh dồn", "thủ chắc, hay phản đòn", "kỹ thuật, hay ra chiêu riêng" };

        private static string StarText(int n) => new string('★', Mathf.Clamp(n, 0, 3)) + new string('☆', 3 - Mathf.Clamp(n, 0, 3));

        /// <summary>Chạm biển Thám hiểm: tổng sao, nút "Đánh tiếp" màn kế, 5 vùng (vùng chưa mở xám).</summary>
        public static void OpenAdventure()
        {
            if (Garden == null || Battle != null || !_services.TryGet(out AdventureService adv)) return;
            TipDone(TipId.Adventure);
            var pets = _services.Get<PetService>();
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            int next = adv.NextStage;
            if (next >= 0)
            {
                AdventureStage ns = adv.Stages[next];
                labels.Add("Đánh tiếp: " + ns.Code + " " + ns.Creature + " · " + DifficultyNames[(int)adv.DifficultyOf(next)]);
                enabled.Add(true);
                actions.Add(() => OpenStage(next));
            }
            foreach (AdventureRegion r in adv.Regions)
            {
                bool open = adv.IsRegionUnlocked(r.Index);
                int ri = r.Index;
                labels.Add((ri + 1) + ". " + r.Name + " · ★ " + adv.RegionStars(ri) + "/" + r.StageCount * 3 + (open ? "" : " (khóa)"));
                enabled.Add(open);
                actions.Add(() => OpenRegion(ri));
            }
            labels.Add("Thôi");
            enabled.Add(true);
            actions.Add(null);
            float energy = pets.HasPet ? pets.Current.Needs.Energy : 0f;
            string body = "Tổng ★ " + adv.TotalStars + "/" + adv.MaxStars + " · năng lượng bé " + energy.ToString("0") + "/100" +
                          " (mỗi trận tốn " + new BattleSessionConfig().FightEnergyCost.ToString("0") + ")\n" +
                          "Thắng = 1 sao · còn ≥ 40% máu = 2 sao · ≥ 70% = 3 sao. Thắng trùm để mở vùng sau.";
            GardenUi.ShowChoice("Thám hiểm", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        /// <summary>1 vùng: 5 màn với sao + độ khó so với bé (màn chưa mở xám), Quay lại.</summary>
        private static void OpenRegion(int region)
        {
            var adv = _services.Get<AdventureService>();
            AdventureRegion r = adv.Regions[region];
            var labels = new List<string>();
            var enabled = new List<bool>();
            for (int k = 0; k < r.StageCount; k++)
            {
                int i = r.FirstStage + k;
                AdventureStage s = adv.Stages[i];
                bool open = adv.IsUnlocked(i);
                labels.Add(s.Code + " " + (s.Boss ? "TRÙM " : "") + s.Creature + " · " + StarText(adv.StarsOf(i)) +
                           (open ? " · " + DifficultyNames[(int)adv.DifficultyOf(i)] : " (khóa)"));
                enabled.Add(open);
            }
            labels.Add("Quay lại");
            enabled.Add(true);
            string body = r.Blurb + "\nSinh vật mạnh ngang bé giai đoạn " + r.Stage + ".";
            GardenUi.ShowChoice(r.Name, body, labels.ToArray(), i =>
            {
                if (i < r.StageCount) OpenStage(r.FirstStage + i);
                else OpenAdventure();
            }, enabled.ToArray());
        }

        /// <summary>1 màn: sinh vật, độ khó, thưởng, lối đánh; Đánh! (xám kèm lý do nếu bé chưa đi được).</summary>
        private static void OpenStage(int stage)
        {
            var adv = _services.Get<AdventureService>();
            AdventureStage s = adv.Stages[stage];
            AdventureConfig c = adv.Config;
            int stars = adv.StarsOf(stage);
            string reward = stars == 0 ? "Thắng lần đầu: +" + s.Coins + " Coin" + (s.Boss ? " + mở vùng sau" : "")
                          : stars < 3 ? "Mỗi sao mới: +" + Mathf.RoundToInt(s.Coins * c.NewStarCoinFactor) + " Coin"
                          : "Đủ 3 sao · thắng lại: +" + Mathf.Max(1, Mathf.RoundToInt(s.Coins * c.RepeatCoinFactor)) + " Coin";
            string body = (s.Boss ? "TRÙM vùng · " : "") + "Độ khó với bé: " + DifficultyNames[(int)adv.DifficultyOf(stage)] +
                          " · " + StarText(stars) + "\n" + reward + "\nLối đánh: " + StyleNames[(int)s.Style];
            bool ok = adv.CanStart(stage, out string reason);
            if (!ok) body += "\nChưa đi được: " + reason + ".";
            GardenUi.ShowChoice(s.Code + " · " + s.Creature, body, new[] { "Đánh!", "Quay lại" }, i =>
            {
                if (i == 0) EnterAdventure(stage);
                else OpenRegion(s.Region);
            }, new[] { ok, true });
        }

        /// <summary>Vào trận Thám hiểm (vườn tạm dừng). Hết trận: sao + Coin ở thẻ kết quả, có nút Đánh lại.</summary>
        public static void EnterAdventure(int stage)
        {
            if (Battle != null || Garden == null || !_services.TryGet(out AdventureService adv)) return;
            if (!adv.CanStart(stage, out string reason))
            {
                GardenUi.ShowCard("Chưa đi được", "Chưa vào màn này được: " + reason + ".");
                return;
            }
            CancelSearch();   // đang tìm trận PvP mà đi thám hiểm thì thôi tìm
            AdventureStage s = adv.Stages[stage];
            AdventureRegion r = adv.Regions[s.Region];
            var view = new RankedMatchView
            {
                Mode = BattleMode.Adventure,
                Theme = s.Region,
                Opponent = adv.OpponentFor(stage),
                Seed = (ulong)DateTime.UtcNow.Ticks,
                PetName = s.Creature,
                PlayerName = r.Name,
                Title = "màn " + s.Code + (s.Boss ? " · TRÙM" : ""),
                Dna = adv.CreatureDna(stage),
                Look = BotPlayers.LookFor(s.Stage, s.Form, _services.Get<GameConfig>().Evolution),
                Accessory = AccessoryKind.None,
                OpponentScale = s.Boss ? 1.25f : 1f,
                OnResult = result => AdventureLines(adv, adv.Complete(stage, result)),
                AgainLabel = "Đánh lại",
                Again = () => EnterAdventure(stage),
            };
            GardenUi.Close();
            Garden.SetPaused(true);
            Battle = BattleScene.EnterRanked(_services, view, BattleClosed);
        }

        private static List<string> AdventureLines(AdventureService adv, AdventureResult r)
        {
            AdventureStage s = adv.Stages[r.Stage];
            var lines = new List<string>();
            if (!r.Won)
            {
                lines.Add("Chưa thắng được " + s.Creature);
                lines.Add("Thử đỡ sát đòn để phản đòn, nối combo, hoặc đợi bé lớn thêm");
                return lines;
            }
            lines.Add(StarText(r.Stars) + (r.NewStars > 0 ? "  (+" + r.NewStars + " sao mới)" : ""));
            if (r.Coins > 0) lines.Add("+" + r.Coins + " Coin");
            if (r.UnlockedRegion >= 0) lines.Add("Mở vùng mới: " + adv.Regions[r.UnlockedRegion].Name + "!");
            else if (r.FirstClear && r.Stage + 1 < adv.Stages.Count && adv.Stages[r.Stage + 1].Region == s.Region)
                lines.Add("Mở màn " + adv.Stages[r.Stage + 1].Code);
            return lines;
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
            labels.Add("Đồ chơi cho bé...");
            enabled.Add(true);
            actions.Add(OpenToys);
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
            PawFit.Core.Unity.Sfx.PlayVaried(item == ShopItem.Treat ? PawFit.Core.Audio.SoundId.Munch : PawFit.Core.Audio.SoundId.Gulp, 0.5f);
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
                BattleClosed();
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
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            if (_services.TryGet(out DailyService daily))   // việc mỗi ngày (Phase 7.3) ở đầu hòm thư
            {
                labels.Add(daily.GiftReady ? "Nhận quà ngày " + daily.GiftDay + "/7: +" + daily.GiftCoins + " Coin"
                                           : "Quà ngày " + daily.GiftDay + "/7: đã nhận · mai quay lại nhé");
                enabled.Add(daily.GiftReady);
                actions.Add(() =>
                {
                    int coins = daily.ClaimGift();
                    if (coins > 0) GardenUi.Toast("+" + coins + " Coin · chuỗi " + daily.Streak + " ngày liên tiếp");
                    OpenSettings();
                });
                int claimable = 0;
                for (int i = 0; i < daily.Quests.Count; i++)
                    if (daily.CanClaim(i)) claimable++;
                if (daily.BonusReady) claimable++;
                labels.Add("Nhiệm vụ hôm nay: xong " + daily.DoneCount + "/" + daily.Quests.Count + (claimable > 0 ? " · " + claimable + " phần thưởng chờ nhận" : ""));
                enabled.Add(true);
                actions.Add(OpenQuests);
                body = "Mở game liên tiếp " + daily.Streak + " ngày · ngày 7 quà to nhất\n" + body;
            }
            labels.Add("Âm thanh: " + (settings.SoundOn ? "Bật (chạm để tắt)" : "Tắt (chạm để bật)"));
            enabled.Add(true);
            actions.Add(() =>
            {
                settings.ToggleSound();
                GardenUi.Toast(settings.SoundOn ? "Đã bật âm thanh" : "Đã tắt âm thanh");
                OpenSettings();
            });
            labels.Add("Nhạc nền: " + (settings.MusicOn ? "Bật (chạm để tắt)" : "Tắt (chạm để bật)"));
            enabled.Add(true);
            actions.Add(() =>
            {
                settings.ToggleMusic();
                GardenUi.Toast(settings.MusicOn ? "Đã bật nhạc nền" : "Đã tắt nhạc nền");
                OpenSettings();
            });
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

        /// <summary>Thẻ Đồ chơi (Phase 8.3): mua 1 lần, món đó nằm trong vườn để chơi với bé (camera lia tới + chỉ cách chơi).</summary>
        private static void OpenToys()
        {
            var cos = _services.Get<CosmeticsService>();
            var labels = new List<string>();
            var enabled = new List<bool>();
            var actions = new List<Action>();
            foreach (ToyKind t in CosmeticsService.Toys)
            {
                string name = ToyNames[(int)t];
                CosmeticRefusal r = cos.CheckBuy(t);
                labels.Add(r == CosmeticRefusal.AlreadyOwned ? name + " (có trong vườn: " + ToyUses[(int)t] + ")"
                               : name + " · " + cos.PriceOf(t) + " Coin · " + ToyUses[(int)t] + (r == CosmeticRefusal.NotEnoughCoins ? " (thiếu Coin)" : ""));
                enabled.Add(r == CosmeticRefusal.None);
                actions.Add(() => BuyToy(t));
            }
            labels.Add("Quay lại");
            enabled.Add(true);
            actions.Add(OpenShop);
            string body = "Bạn có " + _services.Get<EconomyService>().Coins + " Coin · mua 1 lần là có mãi\n" +
                          "Mỗi lượt chơi: bé vui lên + tập Tốc độ (tốn chút sức) · bé chán thì tự đẩy bóng chơi";
            GardenUi.ShowChoice("Đồ chơi", body, labels.ToArray(), i => actions[i]?.Invoke(), enabled.ToArray());
        }

        private static void BuyToy(ToyKind t)
        {
            var cos = _services.Get<CosmeticsService>();
            int price = cos.PriceOf(t);
            CosmeticRefusal r = cos.Buy(t);   // vườn nghe ToyAdded: bày món mới + camera lia tới + chỉ cách chơi
            GardenUi.Toast(r == CosmeticRefusal.None
                               ? ToyNames[(int)t] + " đã vào vườn · -" + price + " Coin · còn " + _services.Get<EconomyService>().Coins + " Coin"
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
