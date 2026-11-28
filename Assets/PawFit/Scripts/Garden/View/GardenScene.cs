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
    /// Phase 5.2: đồ trang trí đã mua đứng sẵn trong vườn; mua món mới thì camera lia tới, món đó bật lên, NavMesh nướng lại.
    /// </summary>
    public sealed class GardenScene : MonoBehaviour
    {
        private NavMeshDataInstance _navMesh;
        private NavMeshData _navData;
        private NavMeshBuildSettings _navSettings;
        private Bounds _navBounds;
        private GardenBuildResult _built;
        private DailyService _daily;
        private readonly List<TreasureSpotView> _treasures = new List<TreasureSpotView>();
        private string _treasureDay;
        private int _pendingDig = -1;
        private float _pendingDigLeft, _dayCheck;
        private bool _digging;
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

        /// <summary>Chạm hòm thư cạnh cổng đấu trường (App mở cài đặt).</summary>
        public Action MailboxTapped { get; set; }

        /// <summary>Chạm cổng đấu trường (App mở thẻ rank / PvP).</summary>
        public Action ArenaTapped { get; set; }

        /// <summary>Chạm biển chỉ đường Thám hiểm (App mở bản đồ Thám hiểm, Phase 7.2).</summary>
        public Action AdventureTapped { get; set; }

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
            _built = built;
            CosmeticsData owned = _pets.Cosmetics;   // đồ trang trí đã mua: dựng trước khi nướng NavMesh để thú đi vòng qua
            if (owned != null)
                foreach (DecorKind d in owned.Decor) DecorBuilder.Build(d, built.Obstacles);
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
            _router.MailboxTapped = () => MailboxTapped?.Invoke();
            _router.ArenaTapped = () => ArenaTapped?.Invoke();
            _router.AdventureTapped = () => AdventureTapped?.Invoke();
            if (s.TryGet(out _daily))   // việc mỗi ngày (Phase 7.3): điểm đào kho báu + cờ hòm thư
            {
                _router.TreasureTapped = OnTreasureTapped;
                MailboxView box = built.Obstacles.GetComponentInChildren<MailboxView>();
                Transform flag = box != null ? box.transform.Find("Flag") : null;
                if (flag != null) box.gameObject.AddComponent<MailboxFlag>().Init(flag, () => _daily.HasSomethingToClaim);
                RebuildTreasures();
                _subs.Add(bus.Subscribe<DailyChanged>(_ => RebuildTreasures()));
            }
            _router.Tips = s.TryGet(out TipService tips) ? tips : null;

            _subs.Add(bus.Subscribe<PetDied>(e => StartCoroutine(Farewell(e))));
            _subs.Add(bus.Subscribe<PetCreated>(OnPetCreated));
            _subs.Add(bus.Subscribe<EggReady>(e =>
            {
                if (_egg != null) _egg.SetReady(true);
            }));
            _subs.Add(bus.Subscribe<AccessoryChanged>(e =>
            {
                if (Pet != null) Pet.SetAccessory(e.Equipped, true);
            }));
            _subs.Add(bus.Subscribe<DecorAdded>(e => StartCoroutine(ShowNewDecor(e.Kind))));

            _log.Info("[PawFit] Vườn sẵn sàng · chạm đất = thú chạy tới · kéo đất = kéo camera · chạm thú = vuốt ve · " +
                      "kéo thú = dắt (thả vào hồ = tắm, vào ổ = ngủ) · kéo táo từ khay ở chòi ăn thả lên thú = cho ăn · " +
                      "chạm quầy hàng (trước bên phải) = mua đồ / xem quảng cáo · chạm hòm thư (cạnh cổng đấu trường) = cài đặt");
        }

        // ------------------------------------------------------------------ điểm đào kho báu (Phase 7.3)

        /// <summary>Dựng ụ đất cho các điểm đào hôm nay chưa đào (sang ngày mới thì đổi chỗ).</summary>
        private void RebuildTreasures()
        {
            if (_daily == null || _digging) return;   // đang đào: DoDig tự bỏ ụ đất vừa đào (để kịp chạy hiệu ứng bung)
            string day = _daily.Today;
            int alive = 0;
            foreach (TreasureSpotView t in _treasures)
                if (t != null) alive++;
            if (day == _treasureDay && alive == _daily.DigsLeft) return;
            foreach (TreasureSpotView t in _treasures)
                if (t != null) Destroy(t.gameObject);
            _treasures.Clear();
            _treasureDay = day;
            _pendingDig = -1;
            for (int i = 0; i < _daily.DigSpots.Count; i++)
                if (!_daily.IsDug(i)) _treasures.Add(TreasureSpotView.Create(transform, _daily.DigSpots[i].ToWorld(), i));
        }

        /// <summary>Chạm ụ đất: bé đứng gần thì đào luôn, xa thì chạy tới rồi tự đào.</summary>
        private void OnTreasureTapped(int index)
        {
            if (_daily == null || Pet == null) return;
            if (_pets.HasPet && _pets.Current.IsSleeping)
            {
                GardenUi.Toast("Bé đang ngủ, dậy rồi hãy đi đào kho báu nhé");
                return;
            }
            NVec2 spot = _daily.DigSpots[index];
            if (NVec2.Distance(Pet.transform.position.ToGround(), spot) <= _daily.Config.DigReach)
            {
                DoDig(index);
                return;
            }
            Pet.MoveTo(spot.ToWorld());
            _pendingDig = index;
            _pendingDigLeft = 12f;   // chạy tới không được (bị chắn / bị dắt đi chỗ khác) thì thôi
        }

        private void DoDig(int index)
        {
            _pendingDig = -1;
            NVec2 pos = Pet.transform.position.ToGround();
            _digging = true;
            DigResult r = _daily.Dig(index, pos);
            _digging = false;
            if (!r.Ok)
            {
                GardenUi.Toast("Chưa đào được: " + r.Reason);
                return;
            }
            Pet.Avatar?.PlayEat();   // cúi đầu bới đất
            for (int i = _treasures.Count - 1; i >= 0; i--)
            {
                if (_treasures[i] == null || _treasures[i].Index != index) continue;
                _treasures[i].Pop();   // bung lên rồi tự hủy
                _treasures.RemoveAt(i);
            }
            string prize = r.Prize == DigPrize.Treat ? "bánh thưởng (bé no + vui hơn) và " + r.Coins + " Coin"
                         : r.Prize == DigPrize.Energy ? "nước tăng lực (bé khỏe hơn) và " + r.Coins + " Coin"
                         : r.Coins + " Coin";
            GardenUi.Toast("Bé đào được " + prize + "!" + (_daily.DigsLeft > 0 ? " Còn " + _daily.DigsLeft + " kho báu hôm nay" : ""), 3f);
        }

        private void Update()
        {
            if (_daily == null) return;
            if ((_dayCheck -= Time.deltaTime) <= 0f)   // để qua đêm: sang ngày mới thì đổi chỗ kho báu
            {
                _dayCheck = 5f;
                if (_daily.Today != _treasureDay) RebuildTreasures();
            }
            if (_pendingDig < 0 || Pet == null) return;
            _pendingDigLeft -= Time.deltaTime;
            if (_pendingDigLeft <= 0f)
            {
                _pendingDig = -1;
                return;
            }
            NVec2 spot = _daily.DigSpots[_pendingDig];
            if (NVec2.Distance(Pet.transform.position.ToGround(), spot) <= _daily.Config.DigReach * 0.8f) DoDig(_pendingDig);
        }

        private void SpawnPet(Vector3 at)
        {
            Pet = PetController.Spawn(at, _pets.Current.Dna, _s.Get<PetBrain>(), _s.Get<CareActionService>(),
                                      _s.Get<PetExpressionService>(), _s.Get<EvolutionService>(), _layout, _s.Get<GardenConfig>(),
                                      _s.Get<EventBus>(), transform);
            Pet.SetAccessory(_pets.Cosmetics?.Equipped ?? AccessoryKind.None);   // phụ kiện là của người chơi: thú mới vẫn đeo
        }

        /// <summary>Vừa mua đồ trang trí: camera lia tới, món đó bật lên, nướng lại NavMesh, rồi camera về lại thú.</summary>
        private IEnumerator ShowNewDecor(DecorKind kind)
        {
            Transform decor = DecorBuilder.Build(kind, _built.Obstacles, pop: true);
            if (decor == null) yield break;
            GardenUi.Close();
            _focus.position = decor.position;
            _camCtl.SetFollow(_focus);
            yield return new WaitForSeconds(PopIn.Seconds + 0.1f);
            RebakeNavMesh();   // món đã đủ cỡ: thú sẽ đi vòng qua
            yield return new WaitForSeconds(1.8f);
            Transform back = Pet != null ? Pet.transform : (_egg != null ? _egg.transform : null);
            if (back != null) _camCtl.SetFollow(back);
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
            _navSettings = NavMesh.GetSettingsByID(0);
            _navSettings.agentRadius = 0.3f;
            _navSettings.agentHeight = 1f;
            _navSettings.agentClimb = 0.25f;
            _navSettings.agentSlope = 35f;
            NVec2 size = layout.Max - layout.Min, c = (layout.Min + layout.Max) * 0.5f;
            _navBounds = new Bounds(new Vector3(c.X, 0.5f, c.Y), new Vector3(size.X + 2f, 4f, size.Y + 2f));
            _navData = NavMeshBuilder.BuildNavMeshData(_navSettings, NavSources(built), _navBounds, Vector3.zero, Quaternion.identity);
            _navMesh = NavMesh.AddNavMeshData(_navData);
        }

        /// <summary>Có vật cản mới (đồ trang trí vừa mua): nướng lại ngay trên dữ liệu cũ, thú không bị rơi khỏi NavMesh.</summary>
        private void RebakeNavMesh()
        {
            if (_navData == null || _built == null) return;
            if (!NavMeshBuilder.UpdateNavMeshData(_navData, _navSettings, NavSources(_built), _navBounds))
                _log.Warn("[PawFit] Nướng lại NavMesh không được — thú có thể đi xuyên đồ trang trí mới");
        }

        private static List<NavMeshBuildSource> NavSources(GardenBuildResult built)
        {
            var sources = new List<NavMeshBuildSource>();
            var markups = new List<NavMeshBuildMarkup>
            {
                new NavMeshBuildMarkup { root = built.Obstacles, overrideArea = true, area = 1 },   // 1 = Not Walkable
            };
            NavMeshBuilder.CollectSources(built.Root, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, sources);
            return sources;
        }

        private void OnDestroy()
        {
            foreach (IDisposable d in _subs) d.Dispose();
            _subs.Clear();
            if (_navMesh.valid) _navMesh.Remove();
        }
    }
}
