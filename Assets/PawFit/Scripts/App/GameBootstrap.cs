using System;
using System.IO;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Garden;
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
        private MockAdService _mockAds;
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
            var log = new UnityLog();
            var time = new DebugTimeProvider(new SystemTimeProvider());
            var cfg = GameConfig.CreateDefault();
            var save = new JsonFileSaveService(Path.Combine(Application.persistentDataPath, "save"),
                                               new NewtonsoftSerializer());
            var rng = new SeededRandom((ulong)DateTime.UtcNow.Ticks);
            var bus = new EventBus();
            _pets = new PetService(time, rng, save, bus, cfg, log);

            Services = new ServiceLocator();
            Services.Register<ILog>(log);
            Services.Register<ITimeProvider>(time);
            Services.Register(time);            // DebugTimeProvider: để Debug overlay tua giờ (Phase 1.3)
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
            // Quảng cáo đang là bản giả lập; Phase 5.3 thay bằng SDK thật (chỉ đổi dòng này).
            _mockAds = new MockAdService(cfg.Economy.MockAdSeconds);
            var economy = new EconomyService(_pets, _mockAds, time, bus, cfg, log);
            Services.Register<IAdService>(_mockAds);
            Services.Register(economy);
            Services.Register<ICoinWallet>(economy);   // màn đấu cộng Coin khi thắng Bot qua interface này
            Services.Register(new ShopService(_pets, health, economy, bus, cfg, log));
            Services.Register(new CosmeticsService(_pets, economy, bus, cfg, log));   // phụ kiện + trang trí (Phase 5.2)
            gameObject.AddComponent<UiSounds>().Init(bus);   // tiếng "ting" khi nhận / tiêu Coin
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
            _mockAds?.Update(Time.unscaledDeltaTime);   // quảng cáo giả lập chạy theo giờ thật
            _tickTimer += Time.unscaledDeltaTime;
            if (_tickTimer < 1f) return;
            _tickTimer = 0f;
            _pets.Tick();
        }

        private void OnApplicationPause(bool paused)
        {
            if (_pets == null) return;
            if (paused) _pets.Save();
            else _pets.Tick();   // quay lại app: tính bù thời gian vắng mặt
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
