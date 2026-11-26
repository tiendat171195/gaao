using System;
using PawFit.Core;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Đổi chạm / kéo thành lệnh (không có nút nào):
    ///   chạm đất = thú chạy tới · kéo đất = kéo camera · chạm thú = vuốt ve (đang ngủ thì dậy)
    ///   kéo thú = dắt đi (thả vào hồ = tắm, vào ổ = ngủ) · kéo từ khay đồ ăn thả lên thú = cho ăn.
    /// Phân loại cử chỉ nằm ở PointerGestureClassifier (C# thuần); lớp này chỉ raycast + chuyển lệnh.
    /// </summary>
    public sealed class GardenInputRouter : MonoBehaviour
    {
        private Camera _cam;
        private IPointerInput _input;
        private PointerGestureClassifier _gestures;
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

        private void Update()
        {
            if (_gestures == null) return;
            PointerSample s = _input.Read();
            switch (s.Phase)
            {
                case PointerPhase.Began:
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

        private PointerTarget Pick(Vector2 screen)
        {
            if (Physics.Raycast(_cam.ScreenPointToRay(screen), out RaycastHit hit, 200f))
            {
                if (hit.collider.GetComponentInParent<PetController>() != null) return PointerTarget.Pet;
                if (hit.collider.GetComponentInParent<FoodSourceView>() != null) return PointerTarget.FoodSource;
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
                default: HandleGround(e, screen, onGround, g); break;
            }
        }

        private void HandleGround(GestureEvent e, Vector2 screen, bool onGround, Vector3 g)
        {
            switch (e.Kind)
            {
                case GestureKind.Tap:
                    if (!onGround) break;
                    _pet.MoveTo(g);
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

        private void HandlePet(GestureEvent e, bool onGround, Vector3 g)
        {
            switch (e.Kind)
            {
                case GestureKind.Tap:
                    _pet.Tap();
                    break;
                case GestureKind.DragStart:
                    if (onGround) _pet.BeginLead(g);
                    _camCtl.ResumeFollow();
                    break;
                case GestureKind.DragMove:
                    if (onGround) _pet.LeadTo(g);
                    break;
                case GestureKind.DragEnd:
                    if (onGround) _pet.EndLead(g);
                    break;
            }
        }

        private void HandleFood(GestureEvent e, bool onGround, Vector3 g)
        {
            switch (e.Kind)
            {
                case GestureKind.Tap:
                    _pet.GoToZone(ZoneType.FoodHut);
                    _log.Info("[PawFit] Mẹo: kéo đồ ăn từ khay thả lên thú để cho ăn");
                    break;
                case GestureKind.DragStart:
                    if (_food != null) _food.Drop();
                    _food = FoodItemView.Spawn(transform, onGround ? g : _pet.transform.position);
                    break;
                case GestureKind.DragMove:
                    if (_food != null && onGround) _food.Hold(g);
                    break;
                case GestureKind.DragEnd:
                    if (_food == null) break;
                    Vector3 pet = _pet.transform.position;
                    Vector3 drop = onGround ? g : _food.transform.position;
                    pet.y = drop.y = 0f;
                    if (Vector3.Distance(pet, drop) <= _cfg.FeedReach && _pet.TryFeed()) _food.FlyInto(() => _pet.MouthPosition);
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

        public static FoodItemView Spawn(Transform parent, Vector3 ground)
        {
            var go = new GameObject("Food");
            go.transform.SetParent(parent, false);
            go.transform.position = ground + Vector3.up * HoldHeight;
            Prim.Make(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.3f, Prim.Hex("#E8453C"), smoothness: 0.6f);
            Prim.Make(PrimitiveType.Cube, go.transform, new Vector3(0.03f, 0.18f, 0f), new Vector3(0.12f, 0.03f, 0.07f),
                      Prim.Hex("#5DBB4A"), rotation: Quaternion.Euler(0f, 0f, 25f));
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
