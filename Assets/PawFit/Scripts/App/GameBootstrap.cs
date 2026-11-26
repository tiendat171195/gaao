using System;
using System.IO;
using PawFit.Core;
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
        }

        private void LogPet(ITimeProvider time)
        {
            PetData p = _pets.Current;
            PetNeeds n = p.Needs;
            Debug.Log($"[PawFit] Thú '{p.Name}' · DNA {_pets.DnaCode} · {p.Dna} · tuổi {p.AgeDays(time.UtcNow):0.00} ngày · " +
                      $"no {n.Satiety:0} / sạch {n.Hygiene:0} / năng lượng {n.Energy:0} / vui {n.Mood:0} · " +
                      $"file lưu: {Application.persistentDataPath}/save");
        }

        private void Update()
        {
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
