using System;
using System.Collections;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Audio;
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
    /// Phase 10.1: ngày / đêm theo giờ máy (DayNightView): nắng đổi theo giờ, đêm có đom đóm, sao trên hồ, đèn tự sáng, dế kêu.
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
        private bool _paused;
        private Vector3 _pondPos;
        private ToyPlayground _toys;
        private PlayService _play;
        private DecorLife _decor;
        private DayNightView _dayNight;
        private SettingsService _settings;
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

        /// <summary>Tạm dừng vườn khi đang ở màn đấu: không nhận chạm, camera đứng, thú đứng yên, tắt nền gió / nước.</summary>
        public void SetPaused(bool paused)
        {
            _paused = paused;
            if (_toys != null) _toys.Paused = paused;
            if (_decor != null) _decor.Paused = paused;
            if (_dayNight != null) _dayNight.Paused = paused;   // màn đấu dùng chung đèn: luôn sáng như buổi trưa
            Sfx.Ambience(!paused);
            if (!paused) Sfx.Music(MusicId.Garden);
            else Sfx.Loop(LoopId.Pond, false);
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

            Light sun = SetupLighting();
            Camera cam = SetupCamera();
            Sfx.Music(MusicId.Garden);   // nhạc nền vườn + gió, lá, chim (Phase 8.1)
            Sfx.Ambience(true);
            GardenZone pond = _layout.Get(ZoneType.Pond);
            _pondPos = pond != null ? pond.Center.ToWorld() : new Vector3(999f, 0f, 999f);
            ColorAdjustments color = SetupPostFx(cam);
            GardenBuildResult built = GardenBuilder.Build(_layout, transform);
            _built = built;
            // ngày / đêm theo giờ máy (Phase 10.1): App đăng ký DayCycle (theo giờ của game, bảng DEV tua / ép giờ được)
            DayCycle cycle = s.TryGet(out DayCycle registered) ? registered : new DayCycle(() => DateTime.Now);
            _dayNight = gameObject.AddComponent<DayNightView>();
            _dayNight.Init(cycle, sun, cam, color, _layout, built, () => Pet);
            CosmeticsData owned = _pets.Cosmetics;   // đồ trang trí đã mua: dựng trước khi nướng NavMesh để thú đi vòng qua
            var startDecor = new List<KeyValuePair<DecorKind, Transform>>();
            if (owned != null)
                foreach (DecorKind d in owned.Decor) startDecor.Add(new KeyValuePair<DecorKind, Transform>(d, DecorBuilder.Build(d, built.Obstacles)));
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
            var orbit = new CameraOrbit();   // góc camera bạn đã chỉnh lần trước (Phase 9.1)
            _settings = s.TryGet(out SettingsService settings) ? settings : null;
            if (_settings != null && _settings.HasCameraView)
                orbit.Set(_settings.Current.CameraYaw.Value, _settings.Current.CameraPitch.Value, _settings.Current.CameraDistance.Value);
            _camCtl.Init(cam, Pet != null ? Pet.transform : (_egg != null ? _egg.transform : null), _layout, orbit);
            _camCtl.ViewChanged = SaveCameraView;
            _router = gameObject.AddComponent<GardenInputRouter>();
            _router.Init(cam, new LegacyPointerInput(), _camCtl, Pet, cfg, _log);
            _router.EggTapped = OnEggTapped;
            _router.StoneTapped = stone => GardenUi.ShowCard(stone.Entry.Name, MemorialView.CardText(stone.Entry));
            _router.DummyTapped = level => DummyTapped?.Invoke(level);
            _router.ShopTapped = () => ShopTapped?.Invoke();
            _router.MailboxTapped = () => MailboxTapped?.Invoke();
            _router.ArenaTapped = () => ArenaTapped?.Invoke();
            _router.AdventureTapped = () => AdventureTapped?.Invoke();
            if (s.TryGet(out _play))   // đồ chơi trong vườn (Phase 8.3)
            {
                _toys = new GameObject("Toys").AddComponent<ToyPlayground>();
                _toys.transform.SetParent(transform, false);
                _toys.Init(_play, () => Pet, _pets.Cosmetics);
                _router.Toys = _toys;
                HookSelfPlay();
                _subs.Add(bus.Subscribe<ToyAdded>(e => StartCoroutine(ShowNewToy(e.Kind))));
            }
            if (s.TryGet(out _daily))   // việc mỗi ngày (Phase 7.3): điểm đào kho báu + cờ hòm thư
            {
                _router.TreasureTapped = OnTreasureTapped;
                MailboxView box = built.Obstacles.GetComponentInChildren<MailboxView>();
                Transform flag = box != null ? box.transform.Find("Flag") : null;
                if (flag != null) box.gameObject.AddComponent<MailboxFlag>().Init(flag, () => _daily.HasSomethingToClaim);
                RebuildTreasures();
                _subs.Add(bus.Subscribe<DailyChanged>(_ => RebuildTreasures()));
            }
            // đồ trang trí có tác dụng + bé dùng được (Phase 8.4): ghế ngồi, đèn lồng, túi bánh cối xay gió, bướm ở vòm hoa
            _decor = new GameObject("DecorLife").AddComponent<DecorLife>();
            _decor.transform.SetParent(transform, false);
            _decor.Init(s.Get<PetBrain>(), _play, _daily, () => Pet);
            _decor.NightLevel = () => _dayNight.Night;   // đèn lồng sáng, bướm đi ngủ theo ngày / đêm của vườn
            foreach (KeyValuePair<DecorKind, Transform> d in startDecor) _decor.Add(d.Key, d.Value);
            _router.DecorTapped = k => _decor.Tap(k);
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
                      "chạm quầy hàng (trước bên phải) = mua đồ / xem quảng cáo · chạm hòm thư (cạnh cổng đấu trường) = cài đặt · " +
                      "vuốt bóng = ném cho bé nhặt · kéo cần lông vũ = bé vồ · chạm máy bong bóng = thổi · " +
                      "chạm ghế = bé ra ngồi · chạm đèn lồng = lấp lánh · chạm cối xay gió = bé ăn bánh · chạm vòm hoa = gọi bướm · " +
                      "trời sáng / tối theo giờ máy (" + cycle.Phase + ", " + cycle.Hour.ToString("0.0") + " giờ)");
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
            Sfx.Play(SoundId.Dig, 0.6f);
            Sfx.Play(SoundId.Treasure, 0.5f);
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
            UpdatePondSound();
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

        // ------------------------------------------------------------------ góc camera (Phase 9.1)

        /// <summary>Camera đang ở góc mặc định không (hòm thư hiện nút "về góc mặc định" khi đã chỉnh).</summary>
        public bool CameraIsDefault => _camCtl == null || _camCtl.Orbit.IsDefault;

        /// <summary>Về góc camera mặc định (hòm thư).</summary>
        public void ResetCameraView()
        {
            if (_camCtl != null) _camCtl.ResetView();
        }

        private void SaveCameraView(CameraOrbit o)
        {
            if (_settings == null) return;
            if (o.IsDefault) _settings.ClearCameraView();
            else _settings.SetCameraView(o.Yaw, o.Pitch, o.Distance);
        }

        /// <summary>Bé (mới) biết tự tới chơi bóng khi rảnh.</summary>
        private void HookSelfPlay()
        {
            if (Pet != null && _toys != null && _play != null) Pet.SetSelfPlay(() => _toys.BallRest, roll => _play.WantsSelfPlay(roll));
        }

        /// <summary>Vừa mua đồ chơi: camera lia tới, món đó bật lên, bé chạy tới xem, rồi camera về lại bé.</summary>
        private IEnumerator ShowNewToy(ToyKind kind)
        {
            Transform toy = _toys != null ? _toys.Build(kind) : null;
            if (toy == null) yield break;
            toy.gameObject.AddComponent<PopIn>();
            GardenUi.Close();
            _focus.position = toy.position;
            _camCtl.SetFollow(_focus);
            if (Pet != null) Pet.MoveTo(toy.position + new Vector3(0.8f, 0f, -0.8f));
            yield return new WaitForSeconds(2.2f);
            Transform back = Pet != null ? Pet.transform : (_egg != null ? _egg.transform : null);
            if (back != null) _camCtl.SetFollow(back);
            GardenUi.Toast(kind == ToyKind.Ball ? "Vuốt quả bóng để ném, bé sẽ chạy nhặt mang về cho bạn!"
                         : kind == ToyKind.Feather ? "Giữ cần lông vũ rồi kéo đi khắp vườn, bé sẽ đuổi theo vồ!"
                         : "Chạm máy để thổi bong bóng, bé sẽ nhảy lên đớp!", 4f);
        }

        /// <summary>Tiếng nước hồ to dần khi bé (camera bám theo bé) tới gần hồ.</summary>
        private void UpdatePondSound()
        {
            if (_paused) return;
            Vector3 ear = Pet != null ? Pet.transform.position : (_egg != null ? _egg.transform.position : _pondPos + Vector3.one * 50f);
            float d = Vector3.Distance(new Vector3(ear.x, 0f, ear.z), _pondPos);
            float v = Mathf.Clamp01(1f - (d - 2f) / 7f) * 0.35f;
            Sfx.Loop(LoopId.Pond, v > 0.01f, v);
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
            if (_decor != null) _decor.Add(kind, decor);
            GardenUi.Close();
            _focus.position = decor.position;
            _camCtl.SetFollow(_focus);
            yield return new WaitForSeconds(PopIn.Seconds + 0.1f);
            RebakeNavMesh();   // món đã đủ cỡ: thú sẽ đi vòng qua
            yield return new WaitForSeconds(1.8f);
            Transform back = Pet != null ? Pet.transform : (_egg != null ? _egg.transform : null);
            if (back != null) _camCtl.SetFollow(back);
            GardenUi.Toast(DecorHowTo(kind), 4.5f);
        }

        /// <summary>Vừa mua đồ trang trí: chỉ cách bé dùng món đó (Phase 8.4).</summary>
        private static string DecorHowTo(DecorKind kind)
        {
            switch (kind)
            {
                case DecorKind.Lanterns: return "Tối đèn lồng sẽ sáng, bé ngủ ngon hơn · chạm đèn lồng cho đèn lấp lánh";
                case DecorKind.Bench: return "Bé mệt sẽ tự ra ghế ngồi nghỉ · chạm ghế (hoặc dắt bé thả lên ghế) để bé ra ngồi";
                case DecorKind.Windmill: return "Mỗi ngày cối xay gió xay 1 túi bánh · chạm cối xay gió để bé ra ăn";
                case DecorKind.FlowerArch: return "Bướm sẽ bay ra rủ bé đuổi, bé vui lâu hơn · chạm vòm hoa để gọi bướm";
                default: return "";
            }
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
            HookSelfPlay();
            _router.SetPet(Pet);
            _camCtl.SetFollow(Pet.transform);
            Pet.Avatar.PlayHop();
        }

        /// <summary>Đèn mặt trời + ánh sáng môi trường + sương (giá trị buổi trưa; DayNightView đổi theo giờ ngay sau đó).</summary>
        private static Light SetupLighting()
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
            return sun;
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

        private ColorAdjustments SetupPostFx(Camera cam)
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
            return color;
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
