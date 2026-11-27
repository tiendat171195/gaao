using System;
using System.IO;
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
    /// Composition root: nơi DUY NHẤT tạo và nối các Service.
    /// Tự chạy khi bấm Play ở bất kỳ scene nào (không cần kéo thả gì vào scene).
    /// MonoBehaviour này chỉ nối dây + chuyển vòng đời Unity thành lệnh gọi Service, không chứa luật game.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        /// <summary>Kho service cho View (Phase 1.2+) lấy ra dùng.</summary>
        public static ServiceLocator Services { get; private set; }

        private PetService _pets;
        private INetworkManager _net;
        private OnlineService _online;
        private TipService _tips;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private MockAdService _mockAds;   // chỉ bản Dev / Editor có quảng cáo giả lập
#endif
        private float _tickTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoCreate()
        {
            if (FindAnyObjectByType<GameBootstrap>() != null) return;
            new GameObject("PawFit_Game").AddComponent<GameBootstrap>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            Application.targetFrameRate = 60;   // điện thoại mặc định chỉ 30 khung hình / giây: vuốt + hoạt cảnh sẽ giật
            var log = new UnityLog();
            var cfg = GameConfig.CreateDefault();
            var serializer = new NewtonsoftSerializer();
            var save = new JsonFileSaveService(Path.Combine(Application.persistentDataPath, "save"), serializer);
            // vừa nhập mã chuyển máy ở lần mở trước: thay file game bằng bản tải về TRƯỚC khi đọc thú
            if (OnlineService.ApplyPendingRestore(save)) Debug.Log("[PawFit] Đã nhận dữ liệu từ mã chuyển máy");
            AccountData account = OnlineService.LoadAccount(save);
            // giờ theo máy chủ (Phase 4.1): chưa nối được thì giờ máy + độ lệch đo lần trước
            var clock = new ServerClock(new SystemTimeProvider(), account.ClockOffsetSeconds);
            var time = new DebugTimeProvider(clock);
            var rng = new SeededRandom((ulong)DateTime.UtcNow.Ticks);
            var bus = new EventBus();
            _pets = new PetService(time, rng, save, bus, cfg, log);

            Services = new ServiceLocator();
            Services.Register<ILog>(log);
            Services.Register<ITimeProvider>(time);
            Services.Register(time);            // DebugTimeProvider: để Debug overlay tua giờ (Phase 1.3)
            Services.Register(clock);
            Services.Register(cfg);
            Services.Register<ISaveService>(save);
            Services.Register<IRandom>(rng);
            Services.Register(bus);
            Services.Register(_pets);

            _pets.LoadOrCreate("Kem");
            LogPet(time);

            // Vườn (Phase 1.2): luật chăm sóc + "bộ não" thú đều là C# thuần
            var gardenCfg = new GardenConfig();
            var layout = GardenLayout.CreateDefault();
            var health = new HealthService(_pets, time, cfg);
            Services.Register(health);
            var evo = new EvolutionService(_pets, health, time, bus, cfg);   // tự gắn vào PetService (chạy mỗi bước thời gian)
            Services.Register(evo);
            var care = new CareActionService(_pets, health, evo, gardenCfg, time, bus, log);
            var brain = new PetBrain(layout, gardenCfg, new SeededRandom(rng.NextUInt()), _pets, care);
            Services.Register(gardenCfg);
            Services.Register(layout);
            Services.Register(care);
            Services.Register(brain);

            // Biểu cảm (Phase 1.3): đói / bẩn / buồn ngủ / ngủ thể hiện bằng hình, vẫn là C# thuần
            var exprCfg = new ExpressionConfig();
            Services.Register(exprCfg);
            Services.Register(new PetExpressionService(_pets, brain, exprCfg, new SeededRandom(rng.NextUInt())));

            // Kinh tế (Phase 5.1): ví Coin + quảng cáo tự nguyện (5 lần / ngày) + quầy hàng, vẫn là C# thuần.
            // Editor / bản Dev: quảng cáo giả lập. Bản phát hành: chưa có SDK quảng cáo thật thì không có quảng cáo
            // (gắn SDK thật = thay NoAdService bằng 1 lớp IAdService mới, không đổi luật kinh tế).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _mockAds = new MockAdService(cfg.Economy.MockAdSeconds);
            IAdService ads = _mockAds;
#else
            IAdService ads = new NoAdService();
#endif
            var economy = new EconomyService(_pets, ads, time, bus, cfg, log);
            Services.Register(ads);
            Services.Register(economy);
            Services.Register<ICoinWallet>(economy);   // màn đấu cộng Coin khi thắng Bot qua interface này
            Services.Register(new ShopService(_pets, health, economy, bus, cfg, log));
            Services.Register(new CosmeticsService(_pets, economy, bus, cfg, log));   // phụ kiện + trang trí (Phase 5.2)
            var rank = new RankService(_pets, time, bus, cfg, economy, log);   // rank Elo + mùa giải (Phase 4.3)
            Services.Register(rank);
            // Máy chủ riêng (Phase 4): có địa chỉ máy chủ thì tài khoản khách + giờ + sao lưu + PvP qua máy chủ
            // (máy chủ ghép người thật trước, vắng người thì thả người chơi ảo, tự tính Elo).
            // Không có địa chỉ: mạng giả lập trên máy như trước (người chơi ảo, Elo tính trên máy).
            string url = ServerUrl.Resolve(account);
            if (url != null)
            {
                var api = new ServerApi(url, new UnityHttpTransport());
                _online = new OnlineService(api, save, serializer, _pets, rank, clock, bus, log);
                Services.Register(_online);
                var serverNet = new ServerNetworkManager(api.PvpUrl, () => _online.Account.Token, time, cfg, null, log, Application.version);
                Services.Register(serverNet);
                _net = serverNet;
                _online.Start();
                Debug.Log("[PawFit] Máy chủ: " + url);
            }
            else
            {
                _net = new LocalNetworkManager(time, new SeededRandom(rng.NextUInt()), cfg);
                Debug.Log("[PawFit] Chưa cấu hình máy chủ: chơi offline, PvP với người chơi ảo trên máy");
            }
            Services.Register(_net);
            gameObject.AddComponent<UiSounds>().Init(bus);   // tiếng "ting" khi nhận / tiêu Coin

            // Cài đặt ở hòm thư: lưu file riêng của máy này. Tắt tiếng = tắt mọi âm thanh của game 1 chỗ.
            var settings = new SettingsService(save, bus);
            Services.Register(settings);
            AudioListener.volume = settings.SoundOn ? 1f : 0f;
            bus.Subscribe<SettingsChanged>(e => AudioListener.volume = e.Settings.SoundOn ? 1f : 0f);

            // Mẹo hướng dẫn theo tình huống (vườn không có nút nên chỉ cho người mới đúng lúc), mỗi mẹo 1 lần
            _tips = new TipService(settings, _pets, health, evo, exprCfg, bus);
            Services.Register(_tips);
            Debug.Log("[PawFit] Ví: " + economy.Coins + " Coin · quảng cáo còn " + economy.AdsLeftToday + "/" + economy.MaxAdsPerDay + " lượt hôm nay");

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            gameObject.AddComponent<DebugOverlay>();   // công cụ test, không có trong bản phát hành
#endif
            SceneFlow.EnterGarden(Services);
        }

        private void LogPet(ITimeProvider time)
        {
            PetData p = _pets.Current;
            if (p == null)   // thú đã mất (kể cả lúc tắt app), đang chờ trứng nở
            {
                EggData egg = _pets.Egg;
                Debug.Log("[PawFit] Thú đã mất · " + _pets.Memorials.Count + " bia mộ · " +
                          (egg == null ? "không có trứng" : _pets.IsEggReady ? "trứng đã sẵn sàng nở"
                              : "trứng nở sau " + (egg.ReadyAtUtc - time.UtcNow).TotalHours.ToString("0.0") + " giờ"));
                return;
            }
            PetNeeds n = p.Needs;
            Debug.Log($"[PawFit] Thú '{p.Name}' · DNA {_pets.DnaCode} · {p.Dna} · tuổi {p.AgeDays(time.UtcNow):0.00} ngày · " +
                      $"no {n.Satiety:0} / sạch {n.Hygiene:0} / năng lượng {n.Energy:0} / vui {n.Mood:0} · " +
                      $"file lưu: {Application.persistentDataPath}/save");
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _mockAds?.Update(Time.unscaledDeltaTime);   // quảng cáo giả lập chạy theo giờ thật
#endif
            _online?.Update(Time.unscaledDeltaTime);   // tài khoản, giờ máy chủ, sao lưu
            _net?.Update();   // tìm trận / nhịp trận PvP (mạng giả lập: người chơi ảo vào hàng chờ)
            if (_tips != null && SceneFlow.Garden != null && SceneFlow.Battle == null && !GardenUi.IsOpen)
            {
                TipId? tip = _tips.Update(Time.unscaledDeltaTime);
                if (tip.HasValue) GardenUi.Tip(TipService.Text(tip.Value));
            }
            _tickTimer += Time.unscaledDeltaTime;
            if (_tickTimer < 1f) return;
            _tickTimer = 0f;
            _pets.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_pets == null) return;
            if (paused)
            {
                _pets.Save();
                _online?.OnPause();   // tải file game lên máy chủ (có thể bị tắt hẳn sau đó)
            }
            else
            {
                _pets.Tick();   // quay lại app: tính bù thời gian vắng mặt
                _online?.OnResume();   // đồng bộ giờ lại / nối lại máy chủ
            }
        }

        private void OnApplicationQuit() => _pets?.Save();
    }

    /// <summary>Ghi log ra Console của Unity.</summary>
    public sealed class UnityLog : ILog
    {
        public void Info(string message) => Debug.Log(message);
        public void Warn(string message) => Debug.LogWarning(message);
        public void Error(string message) => Debug.LogError(message);
    }
}
