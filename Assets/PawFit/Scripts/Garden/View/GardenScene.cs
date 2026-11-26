using System;
using System.Collections;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Unity;
using PawFit.Pet;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Màn vườn (màn dọc): dựng vườn bằng code, ánh sáng mềm + hậu kỳ URP, NavMesh lúc chạy,
    /// thả thú, camera, input. Lấy service qua ServiceLocator (do App đăng ký).
    /// Phase 2.1: thú mất -> cảnh chia tay -> bia mộ mới -> trứng trong ổ -> chạm trứng nở -> đặt tên -> thú mới.
    /// </summary>
    public sealed class GardenScene : MonoBehaviour
    {
        private NavMeshDataInstance _navMesh;
        private ServiceLocator _s;
        private GardenLayout _layout;
        private PetService _pets;
        private ILog _log;
        private GardenCamera _camCtl;
        private GardenInputRouter _router;
        private MemorialView _memorial;
        private EggView _egg;
        private Transform _focus;
        private readonly List<IDisposable> _subs = new List<IDisposable>();

        public PetController Pet { get; private set; }

        /// <summary>Chạm hình nộm ở sân tập (App mở màn đấu). Tham số = mức 0..2.</summary>
        public Action<int> DummyTapped { get; set; }

        /// <summary>Chạm quầy hàng (App mở thẻ quầy hàng, Phase 5.1).</summary>
        public Action ShopTapped { get; set; }

        /// <summary>Tạm dừng vườn khi đang ở màn đấu: không nhận chạm, camera đứng, thú đứng yên.</summary>
        public void SetPaused(bool paused)
        {
            if (_router != null) _router.enabled = !paused;
            if (_camCtl != null) _camCtl.enabled = !paused;
            if (Pet != null) Pet.SetPaused(paused);
            if (!paused && _camCtl != null) _camCtl.ResumeFollow();
        }

        public static GardenScene Enter(ServiceLocator services)
        {
            var scene = new GameObject("PawFit_Garden").AddComponent<GardenScene>();
            scene.Build(services);
            return scene;
        }

        private void Build(ServiceLocator s)
        {
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;   // vườn: màn dọc
            _s = s;
            _layout = s.Get<GardenLayout>();
            _pets = s.Get<PetService>();
            _log = s.Get<ILog>();
            var cfg = s.Get<GardenConfig>();
            var bus = s.Get<EventBus>();

            SetupLighting();
            Camera cam = SetupCamera();
            SetupPostFx(cam);
            GardenBuildResult built = GardenBuilder.Build(_layout, transform);
            BakeNavMesh(built, _layout);
            _memorial = MemorialView.Create(transform, _layout, _pets.Memorials);
            _focus = new GameObject("CameraFocus").transform;
            _focus.SetParent(transform, false);

            if (_pets.HasPet)
            {
                NVec2 spawn = _pets.Current.IsSleeping ? _layout.Get(ZoneType.Bed).UsePoint : _layout.PetSpawn;
                SpawnPet(spawn.ToWorld());
            }
            else if (_pets.Egg != null) ShowEgg();

            _camCtl = cam.GetComponent<GardenCamera>();
            if (_camCtl == null) _camCtl = cam.gameObject.AddComponent<GardenCamera>();
            _camCtl.Init(cam, Pet != null ? Pet.transform : (_egg != null ? _egg.transform : null), _layout);
            _router = gameObject.AddComponent<GardenInputRouter>();
            _router.Init(cam, new LegacyPointerInput(), _camCtl, Pet, cfg, _log);
            _router.EggTapped = OnEggTapped;
            _router.StoneTapped = stone => GardenUi.ShowCard(stone.Entry.Name, MemorialView.CardText(stone.Entry));
            _router.DummyTapped = level => DummyTapped?.Invoke(level);
            _router.ShopTapped = () => ShopTapped?.Invoke();

            _subs.Add(bus.Subscribe<PetDied>(e => StartCoroutine(Farewell(e))));
            _subs.Add(bus.Subscribe<PetCreated>(OnPetCreated));
            _subs.Add(bus.Subscribe<EggReady>(e =>
            {
                if (_egg != null) _egg.SetReady(true);
            }));

            _log.Info("[PawFit] Vườn sẵn sàng · chạm đất = thú chạy tới · kéo đất = kéo camera · chạm thú = vuốt ve · " +
                      "kéo thú = dắt (thả vào hồ = tắm, vào ổ = ngủ) · kéo táo từ khay ở chòi ăn thả lên thú = cho ăn · " +
                      "chạm quầy hàng (trước bên phải) = mua đồ / xem quảng cáo");
        }

        private void SpawnPet(Vector3 at)
        {
            Pet = PetController.Spawn(at, _pets.Current.Dna, _s.Get<PetBrain>(), _s.Get<CareActionService>(),
                                      _s.Get<PetExpressionService>(), _s.Get<EvolutionService>(), _layout, _s.Get<GardenConfig>(),
                                      _s.Get<EventBus>(), transform);
        }

        /// <summary>Trứng nằm trong ổ ngủ (giữa vườn).</summary>
        private void ShowEgg()
        {
            if (_egg != null || _pets.Egg == null) return;
            _egg = EggView.Create(transform, _layout.Get(ZoneType.Bed).Center.ToWorld(0.13f), _pets.Egg, _pets.IsEggReady);
        }

        /// <summary>Cảnh chia tay: thú nằm xuống, hồn bay lên -> camera sang khu tưởng niệm, bia mới nhô lên -> trứng trong ổ.</summary>
        private IEnumerator Farewell(PetDied e)
        {
            PetController dying = Pet;
            Pet = null;
            _router.SetPet(null);
            GardenUi.Close();
            if (dying != null)
            {
                bool done = false;
                dying.PlayDeath(() => done = true);
                while (!done) yield return null;
            }
            _focus.position = _memorial.Focus;
            _camCtl.SetFollow(_focus);
            yield return new WaitForSeconds(1.2f);
            _memorial.Add(e.Memorial);
            yield return new WaitForSeconds(3.2f);
            ShowEgg();
            if (_egg != null) _camCtl.SetFollow(_egg.transform);
        }

        /// <summary>Chạm trứng: chưa tới lúc thì trứng chỉ nhúc nhích; tới lúc thì nở + hỏi tên.</summary>
        private void OnEggTapped(EggView egg)
        {
            if (egg != _egg || egg.IsHatching) return;
            egg.Tap(() => GardenUi.AskName("Trứng nở rồi! Đặt tên cho bé nhé", name =>
            {
                if (_pets.HatchEgg(name) == null) _log.Warn("[PawFit] Trứng chưa nở được (chưa tới lúc?)");
            }));
        }

        /// <summary>Có thú mới: nếu vườn đang không có thú (vừa nở từ trứng) thì thả thú ra ở chỗ quả trứng.</summary>
        private void OnPetCreated(PetCreated e)
        {
            if (Pet != null) return;   // đã có thú: PetController tự dựng lại hình (công cụ test "Thú mới")
            Vector3 at = _egg != null ? _egg.transform.position : _layout.Get(ZoneType.Bed).Center.ToWorld();
            if (_egg != null && !_egg.IsHatching) Destroy(_egg.gameObject);
            _egg = null;
            _s.Get<PetBrain>().Reset();
            SpawnPet(at);
            _router.SetPet(Pet);
            _camCtl.SetFollow(Pet.transform);
            Pet.Avatar.PlayHop();
        }

        private static void SetupLighting()
        {
            Light sun = null;
            foreach (Light l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(50f, -35f, 0f);
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.7f;

            // ánh sáng môi trường 3 tầng (trời / chân trời / đất) cho bóng mềm
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.72f, 0.82f, 0.92f);
            RenderSettings.ambientEquatorColor = new Color(0.74f, 0.80f, 0.68f);
            RenderSettings.ambientGroundColor = new Color(0.42f, 0.40f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.88f, 0.94f);
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 70f;
        }

        private static Camera SetupCamera()
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                var go = new GameObject("Main Camera") { tag = "MainCamera" };
                cam = go.AddComponent<Camera>();
            }
            cam.fieldOfView = 40f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 120f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.78f, 0.88f, 0.94f);
            if (FindAnyObjectByType<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();   // nghe tiếng thú kêu
            return cam;
        }

        private void SetupPostFx(Camera cam)
        {
            cam.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var vol = gameObject.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(0.35f);
            bloom.threshold.Override(1.05f);
            ColorAdjustments color = profile.Add<ColorAdjustments>(true);
            color.saturation.Override(8f);
            color.postExposure.Override(0.1f);
            Tonemapping tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.Neutral);
            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.18f);
            vol.sharedProfile = profile;
        }

        /// <summary>Nướng NavMesh lúc chạy từ collider: mặt đất đi được, nhóm Obstacles không đi được.</summary>
        private void BakeNavMesh(GardenBuildResult built, GardenLayout layout)
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>
            {
                new NavMeshBuildMarkup { root = built.Obstacles, overrideArea = true, area = 1 },   // 1 = Not Walkable
            };
            NavMeshBuilder.CollectSources(built.Root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(0);
            settings.agentRadius = 0.3f;
            settings.agentHeight = 1f;
            settings.agentClimb = 0.25f;
            settings.agentSlope = 35f;
            NVec2 size = layout.Max - layout.Min, c = (layout.Min + layout.Max) * 0.5f;
            var bounds = new Bounds(new Vector3(c.X, 0.5f, c.Y), new Vector3(size.X + 2f, 4f, size.Y + 2f));
            NavMeshData data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            _navMesh = NavMesh.AddNavMeshData(data);
        }

        private void OnDestroy()
        {
            foreach (IDisposable d in _subs) d.Dispose();
            _subs.Clear();
            if (_navMesh.valid) _navMesh.Remove();
        }
    }
}
