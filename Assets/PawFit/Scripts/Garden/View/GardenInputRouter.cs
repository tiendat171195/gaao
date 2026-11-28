using System;
using PawFit.Core;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Đổi chạm / kéo thành lệnh (không có nút nào):
    ///   chạm đất = thú chạy tới · kéo đất = kéo camera · chạm thú = bé mừng (đang ngủ thì dậy)
    ///   xoa qua lại trên người thú = vuốt ve (Phase 8.2) · kéo thú ra chỗ khác = dắt đi (thả vào hồ = tắm, vào ổ = ngủ)
    ///   kéo từ khay đồ ăn thả lên thú = cho ăn
    ///   chạm hình nộm = vào sân tập · chạm quầy hàng = mở quầy · chạm hòm thư (cạnh cổng đấu trường) = cài đặt
    ///   chạm biển Thám hiểm (góc sau bên trái) = bản đồ Thám hiểm · chạm ụ đất kho báu = bé chạy tới đào
    ///   đồ chơi (Phase 8.3): vuốt bóng = ném (chạm = đá nhẹ) · kéo cần lông vũ = bé đuổi · chạm máy bong bóng = thổi, chạm bong bóng = vỡ
    ///   đồ trang trí (Phase 8.4): chạm ghế = bé ra ngồi · chạm đèn lồng = lấp lánh · chạm cối xay gió = bé ra ăn bánh · chạm vòm hoa = gọi bướm
    ///   camera (Phase 9.1): 2 ngón chụm / mở = zoom · 2 ngón xoay = xoay vườn · 2 ngón kéo lên / xuống = đổi góc nghiêng ·
    ///   máy tính: lăn chuột = zoom, giữ chuột phải kéo = xoay + nghiêng.
    /// Phân loại cử chỉ nằm ở PointerGestureClassifier (C# thuần); lớp này chỉ raycast + chuyển lệnh.
    /// </summary>
    public sealed class GardenInputRouter : MonoBehaviour
    {
        /// <summary>
        /// Công cụ test (Debug overlay) đặt hàm này: chạm lên vùng overlay thì vườn bỏ qua.
        /// Nhận tọa độ màn hình của Input (gốc ở góc dưới trái).
        /// </summary>
        public static Func<Vector2, bool> PointerBlocker { get; set; }

        /// <summary>Chạm trúng quả trứng / bia mộ (GardenScene xử lý).</summary>
        public Action<EggView> EggTapped { get; set; }
        public Action<MemorialStone> StoneTapped { get; set; }

        /// <summary>Chạm trúng hình nộm ở sân tập (mức 0..2).</summary>
        public Action<int> DummyTapped { get; set; }

        /// <summary>Chạm trúng quầy hàng.</summary>
        public Action ShopTapped { get; set; }

        /// <summary>Chạm trúng hòm thư (mở cài đặt).</summary>
        public Action MailboxTapped { get; set; }

        /// <summary>Chạm trúng cổng đấu trường (PvP / rank).</summary>
        public Action ArenaTapped { get; set; }

        /// <summary>Chạm trúng biển chỉ đường Thám hiểm.</summary>
        public Action AdventureTapped { get; set; }

        /// <summary>Chạm trúng ụ đất kho báu (tham số = chỉ số điểm đào hôm nay).</summary>
        public Action<int> TreasureTapped { get; set; }

        /// <summary>Đồ chơi trong vườn (GardenScene gắn). null = chưa có đồ chơi.</summary>
        public ToyPlayground Toys { get; set; }

        /// <summary>Chạm trúng 1 đồ trang trí (Phase 8.4).</summary>
        public Action<DecorKind> DecorTapped { get; set; }

        /// <summary>Mẹo hướng dẫn: báo người chơi đã chạm đất / dắt thú (mẹo đó không hiện nữa). Có thể null.</summary>
        public TipService Tips { get; set; }

        private Camera _cam;
        private IPointerInput _input;
        private EggView _downEgg;
        private MemorialStone _downStone;
        private TrainingDummyView _downDummy;
        private ShopStallView _downShop;
        private MailboxView _downMailbox;
        private ArenaGateView _downArena;
        private AdventureSignView _downAdventure;
        private TreasureSpotView _downTreasure;
        private ToyHandle _downToy;
        private DecorHandle _downDecor;
        private PointerGestureClassifier _gestures;
        private readonly StrokeTracker _stroke = new StrokeTracker();
        private bool _leading, _strokingPet;
        private bool _twoFinger, _orbitHeld;
        private float _wheelIdle;
        private GardenCamera _camCtl;
        private PetController _pet;
        private GardenConfig _cfg;
        private ILog _log;
        private FoodItemView _food;

        public void Init(Camera cam, IPointerInput input, GardenCamera camCtl, PetController pet, GardenConfig cfg, ILog log)
        {
            _cam = cam;
            _input = input;
            _camCtl = camCtl;
            _pet = pet;
            _cfg = cfg;
            _log = log;
            float dpiScale = Screen.dpi > 0 ? Screen.dpi / 160f : 1f;   // ngưỡng kéo tính theo mật độ điểm ảnh thật
            _gestures = new PointerGestureClassifier(cfg.TapMaxSeconds, cfg.DragThresholdPx * Mathf.Max(1f, dpiScale));
        }

        /// <summary>Đổi thú đang điều khiển (null khi thú đã mất, chờ trứng nở).</summary>
        public void SetPet(PetController pet)
        {
            _pet = pet;
            if (_food != null)
            {
                _food.Drop();
                _food = null;
            }
        }

        private void Update()
        {
            if (_gestures == null) return;
            PointerSample s = _input.Read();
            if (GardenUi.IsOpen)   // đang mở bảng đặt tên / thẻ bia mộ
            {
                if (s.Phase == PointerPhase.Began) _gestures.Cancel();
                return;
            }
            CameraMouse(s);
            if (TwoFinger(s)) return;
            switch (s.Phase)
            {
                case PointerPhase.Began:
                    if (PointerBlocker != null && PointerBlocker(s.Position)) break;   // chạm lên Debug overlay
                    _gestures.Down(s.Position.ToNumerics(), s.Time, Pick(s.Position));
                    break;
                case PointerPhase.Moved:
                    Handle(_gestures.Move(s.Position.ToNumerics(), s.Time));
                    break;
                case PointerPhase.Ended:
                    Handle(_gestures.Up(s.Position.ToNumerics(), s.Time));
                    break;
            }
        }

        /// <summary>Máy tính: lăn chuột = zoom, giữ chuột phải kéo = xoay + đổi góc. Thôi chỉnh thì lưu góc.</summary>
        private void CameraMouse(PointerSample s)
        {
            bool blocked = PointerBlocker != null && PointerBlocker(s.Position);   // đang lăn / kéo trên bảng DEV
            if (Mathf.Abs(s.Scroll) > 0.01f && !blocked)
            {
                _camCtl.Wheel(s.Scroll);
                _wheelIdle = 0.4f;
            }
            else if (_wheelIdle > 0f)
            {
                _wheelIdle -= Time.unscaledDeltaTime;
                if (_wheelIdle <= 0f) CameraDone();
            }
            if (s.OrbitHeld && !blocked)
            {
                _camCtl.OrbitDrag(s.OrbitDelta);
                _orbitHeld = true;
            }
            else if (_orbitHeld && !s.OrbitHeld)
            {
                _orbitHeld = false;
                CameraDone();
            }
        }

        /// <summary>
        /// 2 ngón trên màn hình = chỉnh camera. Trả true khi đang ở chế độ 2 ngón: bỏ qua chạm 1 ngón cho tới khi nhấc hết tay
        /// (nhấc 1 ngón trước thì ngón còn lại không bị coi là kéo / chạm). Đang dắt bé / cầm đồ ăn / đồ chơi thì ngón 2 không tính.
        /// </summary>
        private bool TwoFinger(PointerSample s)
        {
            if (!_twoFinger)
            {
                if (s.Count < 2) return false;
                PointerTarget t = _gestures.Target;
                if (_gestures.IsDragging && (t == PointerTarget.Pet || t == PointerTarget.FoodSource || t == PointerTarget.Toy)) return false;
                if (PointerBlocker != null && (PointerBlocker(s.Position) || PointerBlocker(s.Second))) return false;
                if (_gestures.IsDragging) _camCtl.EndPan();
                _gestures.Cancel();
                _twoFinger = true;
                _camCtl.BeginTwoFinger(s.Position, s.Second);
                return true;
            }
            if (s.Count >= 2)
            {
                _camCtl.MoveTwoFinger(s.Position, s.Second);
                return true;
            }
            if (_camCtl.Orbit.TwoFingerActive)
            {
                _camCtl.EndTwoFinger();
                Tips?.Done(TipId.Camera);
            }
            if (s.Count == 0) _twoFinger = false;
            return true;
        }

        private void CameraDone()
        {
            _camCtl.CommitView();
            Tips?.Done(TipId.Camera);
        }

        private PointerTarget Pick(Vector2 screen)
        {
            _downEgg = null;
            _downStone = null;
            _downDummy = null;
            _downShop = null;
            _downMailbox = null;
            _downArena = null;
            _downAdventure = null;
            _downTreasure = null;
            _downToy = null;
            _downDecor = null;
            if (Physics.Raycast(_cam.ScreenPointToRay(screen), out RaycastHit hit, 200f))
            {
                if (_pet != null && hit.collider.GetComponentInParent<PetController>() != null) return PointerTarget.Pet;
                if (hit.collider.GetComponentInParent<FoodSourceView>() != null) return PointerTarget.FoodSource;
                _downEgg = hit.collider.GetComponentInParent<EggView>();
                if (_downEgg != null) return PointerTarget.Egg;
                _downStone = hit.collider.GetComponentInParent<MemorialStone>();
                if (_downStone != null) return PointerTarget.Memorial;
                _downDummy = hit.collider.GetComponentInParent<TrainingDummyView>();
                if (_downDummy != null) return PointerTarget.Dummy;
                _downShop = hit.collider.GetComponentInParent<ShopStallView>();
                if (_downShop != null) return PointerTarget.Shop;
                _downMailbox = hit.collider.GetComponentInParent<MailboxView>();
                if (_downMailbox != null) return PointerTarget.Mailbox;
                _downArena = hit.collider.GetComponentInParent<ArenaGateView>();
                if (_downArena != null) return PointerTarget.Arena;
                _downAdventure = hit.collider.GetComponentInParent<AdventureSignView>();
                if (_downAdventure != null) return PointerTarget.Adventure;
                _downTreasure = hit.collider.GetComponentInParent<TreasureSpotView>();
                if (_downTreasure != null) return PointerTarget.Treasure;
                _downToy = hit.collider.GetComponentInParent<ToyHandle>();
                if (_downToy != null && Toys != null) return PointerTarget.Toy;
                _downDecor = hit.collider.GetComponentInParent<DecorHandle>();
                if (_downDecor != null && DecorTapped != null) return PointerTarget.Decor;
            }
            return PointerTarget.Ground;
        }

        private void Handle(GestureEvent? ge)
        {
            if (!ge.HasValue) return;
            GestureEvent e = ge.Value;
            Vector2 screen = e.Position.ToUnity();
            bool onGround = _camCtl.GroundPoint(screen, out Vector3 g);
            switch (e.Target)
            {
                case PointerTarget.Pet: HandlePet(e, onGround, g); break;
                case PointerTarget.FoodSource: HandleFood(e, onGround, g); break;
                case PointerTarget.Egg:
                    if (e.Kind == GestureKind.Tap && _downEgg != null) EggTapped?.Invoke(_downEgg);
                    else HandleGround(e, screen, onGround, g);   // kéo từ trứng = kéo camera
                    break;
                case PointerTarget.Memorial:
                    if (e.Kind == GestureKind.Tap && _downStone != null) StoneTapped?.Invoke(_downStone);
                    else HandleGround(e, screen, onGround, g);
                    break;
                case PointerTarget.Dummy:
                    if (e.Kind == GestureKind.Tap && _downDummy != null)
                    {
                        TapBounce.Play(_downDummy);
                        DummyTapped?.Invoke(_downDummy.Level);
                    }
                    else HandleGround(e, screen, onGround, g);
                    break;
                case PointerTarget.Shop:
                    if (e.Kind == GestureKind.Tap)
                    {
                        TapBounce.Play(_downShop);
                        ShopTapped?.Invoke();
                    }
                    else HandleGround(e, screen, onGround, g);   // kéo từ quầy = kéo camera
                    break;
                case PointerTarget.Mailbox:
                    if (e.Kind == GestureKind.Tap)
                    {
                        TapBounce.Play(_downMailbox);
                        MailboxTapped?.Invoke();
                    }
                    else HandleGround(e, screen, onGround, g);
                    break;
                case PointerTarget.Arena:
                    if (e.Kind == GestureKind.Tap)
                    {
                        TapBounce.Play(_downArena);
                        ArenaTapped?.Invoke();
                    }
                    else HandleGround(e, screen, onGround, g);
                    break;
                case PointerTarget.Adventure:
                    if (e.Kind == GestureKind.Tap)
                    {
                        TapBounce.Play(_downAdventure);
                        AdventureTapped?.Invoke();
                    }
                    else HandleGround(e, screen, onGround, g);
                    break;
                case PointerTarget.Treasure:
                    if (e.Kind == GestureKind.Tap && _downTreasure != null) TreasureTapped?.Invoke(_downTreasure.Index);
                    else HandleGround(e, screen, onGround, g);
                    break;
                case PointerTarget.Toy:
                    HandleToy(e, screen, onGround, g);
                    break;
                case PointerTarget.Decor:   // chạm = dùng đồ trang trí; kéo = kéo camera như trên đất
                    if (e.Kind == GestureKind.Tap && _downDecor != null) DecorTapped?.Invoke(_downDecor.Kind);
                    else HandleGround(e, screen, onGround, g);
                    break;
                default: HandleGround(e, screen, onGround, g); break;
            }
        }

        private void HandleGround(GestureEvent e, Vector2 screen, bool onGround, Vector3 g)
        {
            switch (e.Kind)
            {
                case GestureKind.Tap:
                    if (!onGround || _pet == null) break;
                    _pet.MoveTo(g);
                    Tips?.Done(TipId.Move);
                    _camCtl.ResumeFollow();
                    break;
                case GestureKind.DragStart:
                    _camCtl.BeginPan(e.StartPosition.ToUnity());
                    _camCtl.PanTo(screen);
                    break;
                case GestureKind.DragMove:
                    _camCtl.PanTo(screen);
                    break;
                case GestureKind.DragEnd:
                    _camCtl.PanTo(screen);
                    _camCtl.EndPan();
                    break;
            }
        }

        /// <summary>
        /// Chạm = bé mừng. Kéo bắt đầu trên người bé: StrokeTracker quyết định — xoa qua lại / vòng vòng trên người bé = vuốt ve,
        /// kéo ra khỏi người bé = dắt đi. Kéo ngắn chưa rõ ý mà nhấc tay = coi như chạm.
        /// </summary>
        private void HandlePet(GestureEvent e, bool onGround, Vector3 g)
        {
            if (_pet == null) return;
            switch (e.Kind)
            {
                case GestureKind.Tap:
                    _pet.Tap();
                    break;
                case GestureKind.DragStart:
                    _leading = _strokingPet = false;
                    _stroke.Begin(e.StartPosition);
                    TrackPetDrag(e, onGround, g);
                    break;
                case GestureKind.DragMove:
                    TrackPetDrag(e, onGround, g);
                    break;
                case GestureKind.DragEnd:
                    if (_leading)
                    {
                        if (onGround) _pet.EndLead(g);
                        Tips?.Done(TipId.Lead);
                    }
                    else if (_strokingPet)
                    {
                        _pet.EndStroke();
                        Tips?.Done(TipId.Pet);
                    }
                    else _pet.Tap();
                    _leading = _strokingPet = false;
                    break;
            }
        }

        /// <summary>Đồ chơi: bóng (kéo = cầm, thả = ném, chạm = đá), lông vũ (kéo = bé đuổi), máy bong bóng (chạm = thổi), bong bóng (chạm = vỡ).</summary>
        private void HandleToy(GestureEvent e, Vector2 screen, bool onGround, Vector3 g)
        {
            ToyHandle h = _downToy;
            if (h == null || Toys == null) return;
            if (h.Bubble != null)   // bong bóng đang bay: chạm = vỡ, kéo = kéo camera
            {
                if (e.Kind == GestureKind.Tap) Toys.BubbleTap(h.Bubble);
                else HandleGround(e, screen, onGround, g);
                return;
            }
            switch (h.Kind)
            {
                case ToyKind.Ball:
                    if (e.Kind == GestureKind.Tap) Toys.BallTap();
                    else if (e.Kind == GestureKind.DragStart) Toys.BallGrab();
                    else if (e.Kind == GestureKind.DragMove && onGround) Toys.BallHold(g);
                    else if (e.Kind == GestureKind.DragEnd) Toys.BallThrow();
                    break;
                case ToyKind.Feather:
                    if (e.Kind == GestureKind.Tap) GardenUi.Toast("Giữ cần lông vũ rồi kéo đi, bé sẽ đuổi theo vồ");
                    else if (e.Kind == GestureKind.DragStart && onGround) Toys.FeatherGrab(g);
                    else if (e.Kind == GestureKind.DragMove && onGround) Toys.FeatherHold(g);
                    else if (e.Kind == GestureKind.DragEnd) Toys.FeatherRelease();
                    break;
                default:
                    if (e.Kind == GestureKind.Tap) Toys.MachineTap();
                    else HandleGround(e, screen, onGround, g);
                    break;
            }
        }

        private void TrackPetDrag(GestureEvent e, bool onGround, Vector3 g)
        {
            Vector2 screen = e.Position.ToUnity();
            Vector3 center = _pet.BodyCenter;   // bé ngồi ghế: tâm ở trên mặt ghế
            Vector3 sc = _cam.WorldToScreenPoint(center);
            Vector3 se = _cam.WorldToScreenPoint(center + _cam.transform.right * 0.6f);   // bán kính thú trên màn hình
            float radius = Mathf.Max(40f, Vector2.Distance(sc, se));
            DragIntent intent = _stroke.Move(e.Position, new System.Numerics.Vector2(sc.x, sc.y), radius);
            if (intent == DragIntent.Stroke)
            {
                if (!_strokingPet)
                {
                    _strokingPet = true;
                    _pet.BeginStroke();
                }
                Vector3 hand = Physics.Raycast(_cam.ScreenPointToRay(screen), out RaycastHit hit, 200f) &&
                               hit.collider.GetComponentInParent<PetController>() != null ? hit.point : center;
                _pet.StrokeAt(hand, _stroke.TakePasses());
                return;
            }
            if (intent != DragIntent.Lead) return;
            if (_strokingPet)   // đang vuốt mà kéo hẳn ra xa: chuyển sang dắt
            {
                _strokingPet = false;
                _pet.EndStroke();
            }
            if (!onGround) return;
            if (!_leading)
            {
                _leading = true;
                _pet.BeginLead(g);
                _camCtl.ResumeFollow();
            }
            else _pet.LeadTo(g);
        }

        private void HandleFood(GestureEvent e, bool onGround, Vector3 g)
        {
            if (_pet == null) return;   // thú đã mất: khay đồ ăn không dùng được
            switch (e.Kind)
            {
                case GestureKind.Tap:
                    _pet.GoToZone(ZoneType.FoodHut);
                    _log.Info("[PawFit] Mẹo: kéo đồ ăn từ khay thả lên thú để cho ăn");
                    break;
                case GestureKind.DragStart:
                    if (_food != null) _food.Drop();
                    _food = FoodItemView.Spawn(transform, onGround ? g : _pet.transform.position, _pet.IsIll);   // ốm: bát cháo
                    break;
                case GestureKind.DragMove:
                    if (_food != null && onGround) _food.Hold(g);
                    break;
                case GestureKind.DragEnd:
                    if (_food == null) break;
                    Vector3 pet = _pet.transform.position;
                    Vector3 drop = onGround ? g : _food.transform.position;
                    pet.y = drop.y = 0f;
                    PetController eater = _pet;
                    if (Vector3.Distance(pet, drop) <= _cfg.FeedReach && eater.TryFeed())
                        _food.FlyInto(() => eater != null ? eater.MouthPosition : drop);
                    else _food.Drop();
                    _food = null;
                    break;
            }
        }
    }

    /// <summary>Quả táo đang cầm trên tay: bay vào miệng thú (ăn) hoặc rơi xuống biến mất.</summary>
    public sealed class FoodItemView : MonoBehaviour
    {
        public const float HoldHeight = 0.55f;

        private enum State { Held, Eaten, Dropped }

        private State _state;
        private Vector3 _from;
        private Func<Vector3> _mouth;
        private float _t;

        /// <summary>Tạo đồ ăn đang cầm: quả táo, hoặc bát cháo khi thú đang ốm (1 việc của lượt chăm kỹ).</summary>
        public static FoodItemView Spawn(Transform parent, Vector3 ground, bool porridge = false)
        {
            var go = new GameObject(porridge ? "Porridge" : "Food");
            go.transform.SetParent(parent, false);
            go.transform.position = ground + Vector3.up * HoldHeight;
            if (porridge)
            {
                Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, -0.04f, 0f), new Vector3(0.34f, 0.07f, 0.34f), Prim.Hex("#8D5A3B"), smoothness: 0.4f);
                Prim.Make(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.3f, 0.08f, 0.3f), Prim.Hex("#FFF3D6"), smoothness: 0.3f);
                Prim.Make(PrimitiveType.Sphere, go.transform, new Vector3(0.05f, 0.06f, 0.02f), new Vector3(0.05f, 0.02f, 0.05f), Prim.Hex("#7CC36B"));
            }
            else
            {
                Prim.Make(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.3f, Prim.Hex("#E8453C"), smoothness: 0.6f);
                Prim.Make(PrimitiveType.Cube, go.transform, new Vector3(0.03f, 0.18f, 0f), new Vector3(0.12f, 0.03f, 0.07f),
                          Prim.Hex("#5DBB4A"), rotation: Quaternion.Euler(0f, 0f, 25f));
            }
            return go.AddComponent<FoodItemView>();
        }

        public void Hold(Vector3 ground)
        {
            if (_state == State.Held) transform.position = Vector3.Lerp(transform.position, ground + Vector3.up * HoldHeight, 0.6f);
        }

        public void FlyInto(Func<Vector3> mouth)
        {
            _state = State.Eaten;
            _from = transform.position;
            _mouth = mouth;
            _t = 0f;
        }

        public void Drop()
        {
            _state = State.Dropped;
            _from = transform.position;
            _t = 0f;
        }

        private void Update()
        {
            if (_state == State.Held) return;
            _t += Time.deltaTime / (_state == State.Eaten ? 0.35f : 0.7f);
            if (_state == State.Eaten)
            {
                transform.position = Vector3.Lerp(_from, _mouth(), _t);
                transform.localScale = Vector3.one * Mathf.Max(0.05f, 1f - _t * 0.95f);
            }
            else
            {
                Vector3 p = _from;
                p.y = Mathf.Lerp(_from.y, 0.15f, Mathf.Clamp01(_t * 1.6f));
                transform.position = p;
                transform.localScale = Vector3.one * Mathf.Clamp01(1f - (_t - 0.6f) / 0.4f);
            }
            if (_t >= 1f) Destroy(gameObject);
        }
    }
}
