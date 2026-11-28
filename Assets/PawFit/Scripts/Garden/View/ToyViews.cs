using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using UnityEngine;
using UnityEngine.AI;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>Đánh dấu đồ chơi chạm được (router đọc Kind; bong bóng thì kèm Bubble).</summary>
    public sealed class ToyHandle : MonoBehaviour
    {
        public ToyKind Kind;
        public BubbleView Bubble;

        /// <summary>Đồ chơi đứng yên (chậu lông vũ, máy bong bóng): khoét NavMesh để bé đi vòng qua, không đi xuyên.</summary>
        public static void Block(GameObject go, Vector3 center, Vector3 size)
        {
            var o = go.AddComponent<NavMeshObstacle>();
            o.shape = NavMeshObstacleShape.Box;
            o.center = center;
            o.size = size;
            o.carving = true;
            o.carveOnlyStationary = true;
        }
    }

    /// <summary>
    /// Bóng cao su (Phase 8.3): có vật lý thật — vuốt để ném (hướng + lực theo ngón tay), nảy, lăn, đụng hàng rào / cây bật lại.
    /// Bé ngậm thì bóng bám theo miệng bé. Bay ra ngoài vườn thì tự về chỗ cũ.
    /// </summary>
    public sealed class BallView : MonoBehaviour
    {
        private const float Radius = 0.16f;
        private Rigidbody _rb;
        private SphereCollider _body;
        private bool _held;
        private Func<Vector3> _carry;
        private readonly List<Vector3> _trailPos = new List<Vector3>();
        private readonly List<float> _trailT = new List<float>();
        private Vector3 _home;
        private float _bounceCd;

        public bool Held => _held;
        public bool Free => !_held && _carry == null;
        public Collider Body => _body;
        public float Speed => _rb != null && !_rb.isKinematic ? _rb.linearVelocity.magnitude : 0f;
        public Vector3 Ground => new Vector3(transform.position.x, 0f, transform.position.z);

        public static BallView Create(Transform parent, Vector3 home)
        {
            var go = new GameObject("Ball");
            go.transform.SetParent(parent, false);
            go.transform.position = home + Vector3.up * Radius;
            var v = go.AddComponent<BallView>();
            v._home = home;
            Prim.Make(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * Radius * 2f, Prim.Hex("#E8483B"), smoothness: 0.55f);
            Prim.Make(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(Radius * 2.04f, 0.018f, Radius * 2.04f), Color.white, smoothness: 0.5f);
            Prim.Make(PrimitiveType.Cylinder, go.transform, Vector3.zero, new Vector3(Radius * 2.04f, 0.018f, Radius * 2.04f), Prim.Hex("#FFD23F"),
                      smoothness: 0.5f, rotation: Quaternion.Euler(90f, 0f, 0f));
            v._body = go.AddComponent<SphereCollider>();
            v._body.radius = Radius;
            v._body.sharedMaterial = new PhysicsMaterial("PawFitBall")
            {
                bounciness = 0.6f, dynamicFriction = 0.45f, staticFriction = 0.5f, bounceCombine = PhysicsMaterialCombine.Maximum,
            };
            var touch = new GameObject("Touch").AddComponent<SphereCollider>();   // vùng chạm to hơn bóng (trigger: không va chạm vật lý)
            touch.transform.SetParent(go.transform, false);
            touch.radius = 0.42f;
            touch.isTrigger = true;
            go.AddComponent<ToyHandle>().Kind = ToyKind.Ball;
            v._rb = go.AddComponent<Rigidbody>();
            v._rb.mass = 0.25f;
            v._rb.linearDamping = 0.2f;
            v._rb.angularDamping = 0.6f;
            v._rb.interpolation = RigidbodyInterpolation.Interpolate;
            v._rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            return v;
        }

        /// <summary>Ngón tay nhấc bóng lên.</summary>
        public void Grab()
        {
            _held = true;
            _carry = null;
            SetKinematic(true);
            _trailPos.Clear();
            _trailT.Clear();
        }

        /// <summary>Bóng theo ngón tay (cao 0,55 m trên mặt đất), ghi lại vị trí 0,15 giây gần nhất để tính lực ném.</summary>
        public void Hold(Vector3 ground)
        {
            if (!_held) return;
            transform.position = Vector3.Lerp(transform.position, ground + Vector3.up * 0.55f, 0.6f);
            _trailPos.Add(transform.position);
            _trailT.Add(Time.time);
            while (_trailT.Count > 0 && Time.time - _trailT[0] > 0.15f)
            {
                _trailPos.RemoveAt(0);
                _trailT.RemoveAt(0);
            }
        }

        /// <summary>Thả tay: bóng bay theo hướng + tốc độ ngón tay lúc thả (vuốt nhanh = ném xa), vòng lên cao 1 chút.</summary>
        public void Throw()
        {
            if (!_held) return;
            _held = false;
            SetKinematic(false);
            Vector3 v = Vector3.zero;
            int n = _trailPos.Count;
            if (n >= 2) v = (_trailPos[n - 1] - _trailPos[0]) / Mathf.Max(0.02f, _trailT[n - 1] - _trailT[0]);
            v.y = 0f;
            v *= 1.4f;
            float sp = v.magnitude;
            if (sp > 11f) v *= 11f / sp;
            sp = Mathf.Min(sp, 11f);
            v.y = 2f + sp * 0.3f;
            _rb.linearVelocity = v;
            _rb.angularVelocity = new Vector3(v.z, 0f, -v.x) * 3f;   // lăn theo hướng ném
            Sfx.PlayVaried(SoundId.Throw, Mathf.Clamp01(0.25f + sp * 0.04f));
        }

        /// <summary>Chạm vào bóng: đá nhẹ ra xa khỏi `from` (thường là chỗ bé đứng).</summary>
        public void Kick(Vector3 from)
        {
            if (!Free) return;
            Vector3 d = Ground - new Vector3(from.x, 0f, from.z);
            if (d.sqrMagnitude < 0.01f) d = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0f, UnityEngine.Random.Range(-1f, 1f));
            d.y = 0f;
            _rb.linearVelocity = d.normalized * 3.5f + Vector3.up * 2.2f;
            Sfx.PlayVaried(SoundId.BallBounce, 0.45f);
        }

        /// <summary>Bé tự chơi: hất bóng đi theo hướng `dir`.</summary>
        public void Nudge(Vector3 dir, float speed)
        {
            if (!Free) return;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            _rb.linearVelocity = dir.normalized * speed + Vector3.up * 1f;
            Sfx.PlayVaried(SoundId.BallBounce, 0.35f);
        }

        /// <summary>Bé ngậm bóng: bóng bám theo miệng bé.</summary>
        public void CarryWith(Func<Vector3> mouth)
        {
            _held = false;
            _carry = mouth;
            SetKinematic(true);
        }

        /// <summary>Bé nhả bóng xuống trước mặt.</summary>
        public void Drop(Vector3 forward)
        {
            if (_carry == null) return;
            _carry = null;
            SetKinematic(false);
            forward.y = 0f;
            _rb.linearVelocity = forward * 1.2f + Vector3.up * 1.5f;
        }

        private void LateUpdate()
        {
            if (_carry != null) transform.position = _carry();
        }

        private void Update()
        {
            _bounceCd -= Time.deltaTime;
            Vector3 p = transform.position;
            if (p.y < -3f || Mathf.Abs(p.x) > 14f || p.z < -17f || p.z > 18f) Respawn();   // bay ra ngoài vườn: về chỗ cũ
        }

        private void Respawn()
        {
            _held = false;
            _carry = null;
            SetKinematic(false);
            _rb.linearVelocity = Vector3.zero;
            Vector3 p = _home + Vector3.up * 1.2f;
            _rb.position = p;   // dời thẳng (không nội suy vệt bay ngang vườn)
            transform.position = p;
        }

        /// <summary>Cầm / ngậm: tắt vật lý (bóng theo tay / miệng). Tắt luôn nội suy để vị trí đặt tay không bị kéo lại.</summary>
        private void SetKinematic(bool on)
        {
            _rb.isKinematic = on;
            _rb.interpolation = on ? RigidbodyInterpolation.None : RigidbodyInterpolation.Interpolate;
        }

        private void OnCollisionEnter(Collision c)
        {
            float v = c.relativeVelocity.magnitude;
            if (v < 1.2f || _bounceCd > 0f) return;
            _bounceCd = 0.08f;
            Sfx.Play(SoundId.BallBounce, Mathf.Clamp01(v / 9f) * 0.6f, UnityEngine.Random.Range(0.9f, 1.15f));
        }
    }

    /// <summary>
    /// Cần lông vũ (Phase 8.3): chậu nhỏ cắm cần, đầu cần là chùm lông vũ 3 màu. Kéo từ cần: chùm lông bay theo ngón tay,
    /// lắc lư; bé đuổi theo vồ. Thả tay: chùm lông bay về đầu cần.
    /// </summary>
    public sealed class FeatherWandView : MonoBehaviour
    {
        private Transform _lure;
        private bool _held;
        private Vector3 _target;
        private float _wiggle;

        public bool Held => _held;
        public Vector3 LureGround => new Vector3(_lure.position.x, 0f, _lure.position.z);

        public static FeatherWandView Create(Transform parent, Vector3 home)
        {
            var go = new GameObject("FeatherWand");
            go.transform.SetParent(parent, false);
            go.transform.position = home;
            var v = go.AddComponent<FeatherWandView>();
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.12f, 0.32f), Prim.Hex("#C77D4A"), smoothness: 0.2f);
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.245f, 0f), new Vector3(0.28f, 0.01f, 0.28f), Prim.Hex("#7A5236"));
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0.04f, 0.55f, 0f), new Vector3(0.03f, 0.36f, 0.03f), Prim.Hex("#B9865A"),
                      rotation: Quaternion.Euler(0f, 0f, -8f));
            v._lure = Prim.Group(go.transform, "Lure", new Vector3(0.09f, 0.93f, 0f));
            string[] colors = { "#F25C9B", "#6EC6FF", "#FFD23F" };
            for (int i = 0; i < 3; i++)
                Prim.Make(PrimitiveType.Sphere, v._lure, new Vector3((i - 1) * 0.05f, 0.04f * (i % 2), 0f), new Vector3(0.07f, 0.2f, 0.03f), Prim.Hex(colors[i]),
                          smoothness: 0.3f, rotation: Quaternion.Euler(0f, 0f, (i - 1) * 25f));
            var col = go.AddComponent<BoxCollider>();   // chạm trúng cả chậu + cần (to hơn hình cho dễ chạm)
            col.center = new Vector3(0f, 0.5f, 0f);
            col.size = new Vector3(0.7f, 1.1f, 0.7f);
            go.AddComponent<ToyHandle>().Kind = ToyKind.Feather;
            ToyHandle.Block(go, new Vector3(0f, 0.15f, 0f), new Vector3(0.36f, 0.3f, 0.36f));
            v._target = v._lure.position;
            return v;
        }

        public void Grab() => _held = true;

        public void Hold(Vector3 ground)
        {
            if (_held) _target = ground + Vector3.up * 0.35f;
        }

        public void Release() => _held = false;

        /// <summary>Bé vồ: chùm lông "giật" né sang bên 1 chút.</summary>
        public void Dodge()
        {
            _lure.position += new Vector3(UnityEngine.Random.Range(-0.35f, 0.35f), 0.25f, UnityEngine.Random.Range(-0.35f, 0.35f));
            Sfx.PlayVaried(SoundId.Swish, 0.3f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _wiggle += dt;
            Vector3 goal = _held ? _target + Vector3.up * Mathf.Sin(_wiggle * 6f) * 0.08f : transform.position + new Vector3(0.09f, 0.93f, 0f);
            _lure.position = Vector3.Lerp(_lure.position, goal, 1f - Mathf.Exp(-dt * (_held ? 10f : 6f)));
            float k = _held ? 1f : 0.25f;
            _lure.localRotation = Quaternion.Euler(Mathf.Sin(_wiggle * 9f) * 25f * k, _wiggle * 140f * k, Mathf.Sin(_wiggle * 7f) * 18f * k);
        }
    }

    /// <summary>Máy thổi bong bóng (Phase 8.3): hộp xanh bạc hà có vòi hồng + tay quay. Chạm = thổi 1 loạt bong bóng (chờ 4 giây).</summary>
    public sealed class BubbleMachineView : MonoBehaviour
    {
        private Transform _crank;
        private float _spin, _cooldown;

        public bool Ready => _cooldown <= 0f;
        public Vector3 Nozzle => transform.position + new Vector3(0f, 0.85f, -0.1f);

        public static BubbleMachineView Create(Transform parent, Vector3 home)
        {
            var go = new GameObject("BubbleMachine");
            go.transform.SetParent(parent, false);
            go.transform.position = home;
            var v = go.AddComponent<BubbleMachineView>();
            Color mint = Prim.Hex("#7FD6C2"), pink = Prim.Hex("#FF9EC4");
            foreach (float x in new[] { -0.2f, 0.2f })
                foreach (float z in new[] { -0.15f, 0.15f })
                    Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(x, 0.08f, z), new Vector3(0.06f, 0.08f, 0.06f), Prim.Hex("#9AA0A6"));
            Prim.Make(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.38f, 0f), new Vector3(0.55f, 0.45f, 0.45f), mint, smoothness: 0.4f);
            Prim.Make(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.62f, 0f), new Vector3(0.45f, 0.22f, 0.38f), mint, smoothness: 0.4f);
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.78f, -0.1f), new Vector3(0.12f, 0.08f, 0.12f), pink, smoothness: 0.5f);
            Prim.Make(PrimitiveType.Cylinder, go.transform, new Vector3(0f, 0.86f, -0.1f), new Vector3(0.18f, 0.012f, 0.18f), pink, smoothness: 0.5f);
            v._crank = Prim.Group(go.transform, "Crank", new Vector3(0.3f, 0.4f, 0f));
            Prim.Make(PrimitiveType.Cube, v._crank, new Vector3(0.03f, 0.08f, 0f), new Vector3(0.04f, 0.18f, 0.04f), Prim.Hex("#F2C84B"));
            Prim.Make(PrimitiveType.Sphere, v._crank, new Vector3(0.05f, 0.17f, 0f), Vector3.one * 0.07f, Prim.Hex("#E2463F"));
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.45f, 0f);
            col.size = new Vector3(0.8f, 0.95f, 0.7f);
            go.AddComponent<ToyHandle>().Kind = ToyKind.Bubbles;
            ToyHandle.Block(go, new Vector3(0f, 0.4f, 0f), new Vector3(0.6f, 0.8f, 0.5f));
            return v;
        }

        public void Puff()
        {
            _cooldown = 4f;
            _spin = 1f;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _cooldown -= dt;
            if (_spin <= 0f) return;
            _spin -= dt;
            _crank.Rotate(new Vector3(720f * dt, 0f, 0f), Space.Self);
        }
    }

    /// <summary>1 bong bóng: vọt lên khỏi vòi rồi lững lờ chìm dần (như bong bóng xà phòng thật), trôi lắc lư, phồng xẹp;
    /// chạm hoặc bé đớp thì vỡ "póc". Tự vỡ sau 8 giây.</summary>
    public sealed class BubbleView : MonoBehaviour
    {
        private Vector3 _vel;
        private float _age, _size, _seed, _popT = -1f;

        public bool Popped => _popT >= 0f;
        public float Height => transform.position.y;
        public Vector3 Ground => new Vector3(transform.position.x, 0f, transform.position.z);

        public static BubbleView Create(Transform parent, Vector3 at, Vector3 velocity, float size, float seed)
        {
            var go = new GameObject("Bubble");
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var v = go.AddComponent<BubbleView>();
            v._vel = velocity;
            v._size = size;
            v._seed = seed;
            Prim.Make(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * size, Prim.Hex("#BFEFFF"), smoothness: 0.95f);
            Prim.Make(PrimitiveType.Sphere, go.transform, new Vector3(0.22f, 0.22f, -0.3f) * size, Vector3.one * size * 0.22f, Color.white, smoothness: 1f);
            var col = go.AddComponent<SphereCollider>();
            col.radius = size * 0.8f;   // to hơn hình cho dễ chạm
            col.isTrigger = true;       // bóng ném bay xuyên qua, chỉ để chạm
            ToyHandle h = go.AddComponent<ToyHandle>();
            h.Kind = ToyKind.Bubbles;
            h.Bubble = v;
            return v;
        }

        public void Pop()
        {
            if (Popped) return;
            _popT = 0f;
            Collider c = GetComponent<Collider>();
            if (c != null) c.enabled = false;
            Sfx.PlayVaried(SoundId.Pop, 0.45f, 0.15f);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_popT >= 0f)   // vỡ: phồng to rồi biến mất
            {
                _popT += dt / 0.12f;
                transform.localScale = Vector3.one * (1f + _popT * 0.6f) * (1f - Mathf.Clamp01(_popT));
                if (_popT >= 1f) Destroy(gameObject);
                return;
            }
            _age += dt;
            _vel.y = Mathf.MoveTowards(_vel.y, -0.25f, dt * 0.6f);   // vọt lên rồi chìm dần
            _vel.x *= 1f - dt * 0.3f;
            _vel.z *= 1f - dt * 0.3f;
            Vector3 drift = new Vector3(Mathf.Sin(_age * 1.3f + _seed) * 0.25f, 0f, Mathf.Cos(_age * 1.1f + _seed) * 0.15f);
            Vector3 p = transform.position + (_vel + drift) * dt;
            p.y = Mathf.Max(0.3f, p.y);
            transform.position = p;
            float wob = 1f + Mathf.Sin(_age * 5f + _seed) * 0.06f;
            transform.localScale = new Vector3(wob, 2f - wob, wob);
            if (_age > 8f) Pop();
        }
    }

    /// <summary>
    /// Sân chơi trong vườn (Phase 8.3): bày đồ chơi đã mua, nhận chạm / kéo từ router, cho bé chơi (PetController.PlayTo),
    /// thấy bé nhặt được bóng / vồ trúng lông vũ / đớp trúng bong bóng thì gọi PlayService (vui + điểm Tốc độ) + tim + tiếng.
    /// Ưu tiên: đang nhặt bóng > đuổi lông vũ > đớp bong bóng > tự đẩy bóng chơi. Không có luật game ở đây.
    /// </summary>
    public sealed class ToyPlayground : MonoBehaviour
    {
        public static readonly NVec2 BallHome = new NVec2(2.6f, -0.4f);
        public static readonly NVec2 FeatherHome = new NVec2(-2.4f, 1.8f);
        public static readonly NVec2 BubbleHome = new NVec2(1f, 3.6f);

        private enum Fetch { None, Chasing, Carrying }

        private PlayService _play;
        private Func<PetController> _pet;
        private BallView _ball;
        private FeatherWandView _feather;
        private BubbleMachineView _machine;
        private readonly List<BubbleView> _bubbles = new List<BubbleView>();
        /// <summary>Ném gần quá (bé chạy chưa tới 2 m) thì vẫn nhặt về nhưng không tính 1 lượt chơi (chống ném lấy lệ cho nhanh vui).</summary>
        private const float MinFetchMeters = 2f;

        private Fetch _fetch;
        private Vector3 _returnPoint;
        private float _fetchLeft, _pounceCd, _nudgeCd, _refusalCd, _selfPlayLeft, _fetchMeters;
        private int _nudges;
        private bool _bubbleChase, _selfPlaying;
        private PetController _ignoredPet;

        public bool Paused { get; set; }

        /// <summary>Vị trí bóng đang nằm yên (cho bé tự tới chơi), null nếu không có bóng / bóng đang bay / đang bị cầm.</summary>
        public NVec2? BallRest => _ball != null && _ball.Free && _ball.Speed < 0.5f ? new NVec2(_ball.Ground.x, _ball.Ground.z) : (NVec2?)null;

        public void Init(PlayService play, Func<PetController> pet, CosmeticsData owned)
        {
            _play = play;
            _pet = pet;
            if (owned != null)
                foreach (ToyKind k in owned.Toys) Build(k);
        }

        /// <summary>Bày 1 đồ chơi (chưa có thì dựng). Trả chỗ đặt (camera lia tới khi vừa mua).</summary>
        public Transform Build(ToyKind kind)
        {
            switch (kind)
            {
                case ToyKind.Ball:
                    if (_ball == null) _ball = BallView.Create(transform, BallHome.ToWorld());
                    return _ball.transform;
                case ToyKind.Feather:
                    if (_feather == null) _feather = FeatherWandView.Create(transform, FeatherHome.ToWorld());
                    return _feather.transform;
                case ToyKind.Bubbles:
                    if (_machine == null) _machine = BubbleMachineView.Create(transform, BubbleHome.ToWorld());
                    return _machine.transform;
                default:
                    return null;
            }
        }

        // ------------------------------------------------------------------ chạm / kéo (router gọi)

        public void BallGrab()
        {
            if (_ball == null) return;
            CancelFetch();
            _ball.Grab();
        }

        public void BallHold(Vector3 ground) => _ball?.Hold(ground);

        /// <summary>Thả bóng: bóng bay đi, bé chạy nhặt rồi mang về chỗ bạn ném.</summary>
        public void BallThrow()
        {
            if (_ball == null || !_ball.Held) return;
            _returnPoint = _ball.Ground;
            _ball.Throw();
            StartFetch();
        }

        public void BallTap()
        {
            if (_ball == null || !_ball.Free) return;   // bé đang ngậm / bạn đang cầm thì thôi
            PetController p = _pet();
            _returnPoint = p != null ? new Vector3(p.transform.position.x, 0f, p.transform.position.z) : _ball.Ground;
            _ball.Kick(p != null ? p.transform.position : _ball.transform.position + Vector3.back);
            StartFetch();
        }

        public void FeatherGrab(Vector3 ground)
        {
            if (_feather == null) return;
            CancelFetch();
            _feather.Grab();
            _feather.Hold(ground);
            CanPlay(true);
        }

        public void FeatherHold(Vector3 ground) => _feather?.Hold(ground);

        public void FeatherRelease()
        {
            if (_feather == null || !_feather.Held) return;
            _feather.Release();
            PetController p = _pet();
            if (p != null && p.IsPlaying && !p.SelfPlaying) p.StopPlay();
        }

        /// <summary>Chạm máy bong bóng: thổi 8 bong bóng, bé nhảy đớp.</summary>
        public void MachineTap()
        {
            if (_machine == null || !_machine.Ready) return;
            _machine.Puff();
            Sfx.Play(SoundId.Swish, 0.35f, 0.8f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var vel = new Vector3(Mathf.Cos(a) * UnityEngine.Random.Range(0.3f, 0.8f), UnityEngine.Random.Range(0.2f, 0.6f), Mathf.Sin(a) * UnityEngine.Random.Range(0.3f, 0.8f) - 0.3f);
                _bubbles.Add(BubbleView.Create(transform, _machine.Nozzle + Vector3.up * 0.1f, vel, UnityEngine.Random.Range(0.18f, 0.3f), i * 1.7f));
            }
            _bubbleChase = CanPlay(true);
        }

        public void BubbleTap(BubbleView b)
        {
            if (b != null && !b.Popped) b.Pop();
        }

        // ------------------------------------------------------------------ mỗi frame

        private void Update()
        {
            float dt = Time.deltaTime;
            _refusalCd -= dt;
            _pounceCd -= dt;
            _nudgeCd -= dt;
            _bubbles.RemoveAll(b => b == null || b.Popped);
            PetController pet = _pet();
            if (Paused || pet == null) return;
            IgnoreBallHits(pet);
            Vector3 pp = pet.transform.position;
            pp.y = 0f;
            bool selfPlay = _ball != null && pet.IsPlaying && pet.SelfPlaying && pet.PlayOwner == null;   // không phải đang đuổi bướm
            if (selfPlay && !_selfPlaying)   // bé vừa tự tới chơi bóng
            {
                _selfPlayLeft = 10f;
                _nudges = 0;
            }
            _selfPlaying = selfPlay;
            if (_fetch != Fetch.None && _ball != null) UpdateFetch(pet, pp, dt);
            else if (_feather != null && _feather.Held) UpdateLure(pet, pp);
            else if (_bubbleChase) UpdateBubbles(pet, pp);
            else if (selfPlay) UpdateSelfPlay(pet, pp, dt);
        }

        /// <summary>
        /// Thân bé (collider to để dễ chạm) không đụng vật lý với bóng: không thì bé chạy tới là đẩy bóng văng đi, chẳng bao giờ ngậm được,
        /// và lúc nhả bóng (bóng đang nằm trong collider của bé) bóng bị bắn ra. Bé tự hất bóng bằng Nudge.
        /// </summary>
        private void IgnoreBallHits(PetController pet)
        {
            if (_ball == null || pet == _ignoredPet) return;
            _ignoredPet = pet;
            foreach (Collider c in pet.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(_ball.Body, c, true);
        }

        private void StartFetch()
        {
            PetController pet = _pet();
            if (_ball == null || pet == null || !CanPlay(true)) return;
            _fetch = Fetch.Chasing;
            _fetchLeft = 15f;
            pet.PlayTo(_ball.Ground);
        }

        private void UpdateFetch(PetController pet, Vector3 pp, float dt)
        {
            _fetchLeft -= dt;
            if (_fetchLeft <= 0f || !pet.IsPlaying || !_play.CanPlay())   // hết giờ / bạn bảo bé làm việc khác / bé mệt
            {
                CancelFetch();
                return;
            }
            if (_fetch == Fetch.Chasing)
            {
                if (_ball.Held)
                {
                    CancelFetch();
                    return;
                }
                Vector3 b = _ball.Ground;
                pet.PlayTo(b);
                if (Vector3.Distance(pp, b) < 0.65f && _ball.Speed < 3f && _ball.transform.position.y < 0.6f)
                {
                    _ball.CarryWith(() => pet != null ? pet.MouthPosition + pet.transform.forward * 0.08f : _ball.transform.position);
                    _fetch = Fetch.Carrying;
                    _fetchMeters = Vector3.Distance(b, _returnPoint);
                    Sfx.PlayVaried(SoundId.Happy, 0.45f);
                }
                return;
            }
            pet.PlayTo(_returnPoint);   // ngậm bóng chạy về chỗ bạn ném
            if (Vector3.Distance(pp, _returnPoint) > 0.9f) return;
            _ball.Drop(pet.transform.forward);
            _fetch = Fetch.None;
            if (_fetchMeters >= MinFetchMeters && _play.Reward(PlayKind.Fetch)) pet.Cheer(2);
            else Sfx.PlayVaried(SoundId.Happy, 0.3f);
            pet.StopPlay();
        }

        private void CancelFetch()
        {
            if (_fetch == Fetch.None) return;
            if (_ball != null) _ball.Drop(Vector3.zero);
            _fetch = Fetch.None;
            PetController p = _pet();
            if (p != null && p.IsPlaying && !p.SelfPlaying) p.StopPlay();
        }

        private void UpdateLure(PetController pet, Vector3 pp)
        {
            if (!_play.CanPlay()) return;
            Vector3 lure = _feather.LureGround;
            pet.PlayTo(lure);
            if (Vector3.Distance(pp, lure) >= 0.8f || _pounceCd > 0f) return;
            _pounceCd = 1.3f;
            pet.Pounce();
            _feather.Dodge();
            if (_play.Reward(PlayKind.Pounce)) pet.Cheer(1);
        }

        /// <summary>Đuổi bong bóng: ưu tiên quả thấp (bé với tới) gần nhất; quả còn cao thì đứng chờ bên dưới. Vỡ hết thì thôi.</summary>
        private void UpdateBubbles(PetController pet, Vector3 pp)
        {
            if (_bubbles.Count == 0 || !_play.CanPlay())
            {
                _bubbleChase = false;
                if (pet.IsPlaying && !pet.SelfPlaying) pet.StopPlay();
                return;
            }
            BubbleView best = null;
            float bestScore = float.MaxValue, bd = 0f;
            foreach (BubbleView b in _bubbles)
            {
                float d = Vector3.Distance(pp, b.Ground);
                float score = d + (b.Height > 1.7f ? 6f : 0f);   // cao quá 1,7 m: bé chưa với tới
                if (score < bestScore)
                {
                    bestScore = score;
                    bd = d;
                    best = b;
                }
            }
            pet.PlayTo(best.Ground);
            if (best.Height > 1.7f || bd >= 0.7f || _pounceCd > 0f) return;
            _pounceCd = 0.6f;
            pet.Pounce();
            best.Pop();
            if (_play.Reward(PlayKind.BubblePop)) pet.Cheer(1);
        }

        /// <summary>Bé tự chơi (bộ não quyết lúc rảnh): chạy tới hất bóng 3 lần rồi thôi (quá 10 giây chưa xong cũng thôi: bóng kẹt góc rào).</summary>
        private void UpdateSelfPlay(PetController pet, Vector3 pp, float dt)
        {
            _selfPlayLeft -= dt;
            if (_selfPlayLeft <= 0f)
            {
                _nudges = 0;
                pet.StopPlay();
                return;
            }
            Vector3 b = _ball.Ground;
            pet.PlayTo(b, true);
            if (Vector3.Distance(pp, b) >= 0.7f || _nudgeCd > 0f) return;
            _nudgeCd = 1.5f;
            Vector3 dir = pet.transform.forward + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), 0f, UnityEngine.Random.Range(-0.4f, 0.4f));
            _ball.Nudge(dir, UnityEngine.Random.Range(2f, 3.5f));
            if (_play.Reward(PlayKind.SelfPlay)) pet.Cheer(1);
            if (++_nudges < 3) return;
            _nudges = 0;
            pet.StopPlay();
        }

        /// <summary>Bé chơi được không; không được thì (có `toast`) nhắc lý do + bé lắc đầu (cách nhau ≥ 4 giây).</summary>
        private bool CanPlay(bool toast)
        {
            if (_play.CanPlay(out string why)) return true;
            if (!toast || _refusalCd > 0f) return false;
            _refusalCd = 4f;
            GardenUi.Toast("Bé chưa chơi được: " + why);
            _pet()?.Refuse();
            return false;
        }
    }
}
