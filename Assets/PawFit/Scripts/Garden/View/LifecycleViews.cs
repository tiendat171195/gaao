using System;
using System.Collections.Generic;
using PawFit.Core.Unity;
using PawFit.Pet;
using PawFit.Pet.View;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Thú mất: nằm xuống nhắm mắt, 1 đốm sáng nhỏ (hồn) bay lên, thú chìm dần rồi biến mất.
    /// Chỉ là hình — việc chết đã được PetService quyết định trước đó.
    /// </summary>
    public sealed class PetDeathFx : MonoBehaviour
    {
        private IPetAvatar _avatar;
        private Action _done;
        private Transform _spirit;
        private float _t;
        private Vector3 _scale;

        public static void Play(GameObject pet, IPetAvatar avatar, Action done)
        {
            var fx = pet.AddComponent<PetDeathFx>();
            fx._avatar = avatar;
            fx._done = done;
            fx._scale = pet.transform.localScale;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            if (_avatar != null)
            {
                _avatar.SetPose(AvatarPose.Sleep);
                _avatar.SetMoveSpeed(0f);
                _avatar.LookAt(null);
                _avatar.SetLook(new AvatarLook { EyeOpen = 0f, Droop = 1f, Sick = 1f });
            }
            if (_t > 1.4f && _spirit == null)
            {
                _spirit = Prim.Group(null, "PetSpirit");
                _spirit.position = transform.position + Vector3.up * 0.6f;
                Prim.Make(PrimitiveType.Sphere, _spirit, Vector3.zero, Vector3.one * 0.2f, Prim.Hex("#FFFBEA"), smoothness: 0.9f);
                Prim.Make(PrimitiveType.Sphere, _spirit, new Vector3(-0.13f, 0.02f, 0f), new Vector3(0.12f, 0.04f, 0.07f), Prim.Hex("#FFFFFF"));
                Prim.Make(PrimitiveType.Sphere, _spirit, new Vector3(0.13f, 0.02f, 0f), new Vector3(0.12f, 0.04f, 0.07f), Prim.Hex("#FFFFFF"));
            }
            if (_spirit != null)
            {
                float u = _t - 1.4f;
                _spirit.position += new Vector3(Mathf.Sin(u * 2.4f) * 0.25f * Time.deltaTime, 0.55f * Time.deltaTime, 0f);
                _spirit.localScale = Vector3.one * Mathf.Clamp01(Mathf.Min(u * 3f, (4.2f - u) / 1.2f));
            }
            if (_t > 2f) transform.localScale = _scale * Mathf.Clamp01(1f - (_t - 2f) / 1.6f);   // thú chìm dần
            if (_t > 5.6f)
            {
                if (_spirit != null) Destroy(_spirit.gameObject);
                Action done = _done;
                _done = null;
                Destroy(gameObject);
                done?.Invoke();
            }
        }
    }

    /// <summary>Gắn vào mỗi bia mộ để biết chạm trúng bia của ai.</summary>
    public sealed class MemorialStone : MonoBehaviour
    {
        public MemorialEntry Entry;
    }

    /// <summary>
    /// Khu tưởng niệm: mỗi thú đã mất có 1 bia mộ khắc tên, số ngày đã sống, rank cao nhất, mã DNA (GDD G5).
    /// Hiện tối đa 6 bia gần nhất (2 hàng trước tượng đá). Chạm vào bia: hiện thẻ chữ to cho dễ đọc.
    /// </summary>
    public sealed class MemorialView : MonoBehaviour
    {
        private const int MaxStones = 6;
        private static readonly Color StoneColor = new Color(0.78f, 0.78f, 0.74f);

        private GardenZone _zone;
        private readonly List<Transform> _stones = new List<Transform>();
        private Transform _rising;
        private float _riseT = -1f;

        public static MemorialView Create(Transform parent, GardenLayout layout, IReadOnlyList<MemorialEntry> entries)
        {
            var go = new GameObject("MemorialStones");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<MemorialView>();
            v._zone = layout.Get(ZoneType.Memorial);
            int from = Math.Max(0, entries.Count - MaxStones);
            for (int i = from; i < entries.Count; i++) v.AddStone(entries[i], false);
            return v;
        }

        /// <summary>Điểm để camera nhìn vào khu tưởng niệm.</summary>
        public Vector3 Focus => _zone != null ? _zone.Center.ToWorld() + new Vector3(0f, 0f, -1.2f) : transform.position;

        /// <summary>Thêm bia cho thú vừa mất (bia nhô lên từ mặt đất).</summary>
        public void Add(MemorialEntry entry) => AddStone(entry, true);

        /// <summary>Chữ trên thẻ khi chạm vào bia.</summary>
        public static string CardText(MemorialEntry e)
        {
            string rank = string.IsNullOrEmpty(e.BestRank) ? "chưa có" : e.BestRank;
            return "Sống " + e.AgeDaysAtDeath().ToString("0.#") + " ngày\n" +
                   e.BornAtUtc.ToLocalTime().ToString("dd/MM/yyyy") + " - " + e.DiedAtUtc.ToLocalTime().ToString("dd/MM/yyyy") + "\n" +
                   "Rank cao nhất: " + rank + "\n" + e.DnaCode;
        }

        private void AddStone(MemorialEntry entry, bool animate)
        {
            if (_zone == null) return;
            if (_stones.Count >= MaxStones)
            {
                Destroy(_stones[0].gameObject);
                _stones.RemoveAt(0);
                for (int i = 0; i < _stones.Count; i++) _stones[i].position = SlotPosition(i);
            }
            Transform s = BuildStone(entry);
            s.position = SlotPosition(_stones.Count);
            _stones.Add(s);
            if (animate)
            {
                _rising = s;
                _riseT = 0f;
                s.localScale = new Vector3(1f, 0.01f, 1f);
            }
        }

        private Vector3 SlotPosition(int i)
        {
            // 2 hàng × 3 bia trước tượng đá (phía camera = -Z)
            int row = i / 3, col = i % 3;
            NVec2 c = _zone.Center;
            return new Vector3(c.X - 1.0f + col * 1.0f, 0f, c.Y - 1.0f - row * 0.95f);
        }

        private Transform BuildStone(MemorialEntry e)
        {
            Transform s = Prim.Group(transform, "Stone_" + e.Name);
            s.gameObject.AddComponent<MemorialStone>().Entry = e;
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0f, 0.05f, 0f), new Vector3(0.85f, 0.1f, 0.4f), StoneColor * 0.9f);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0f, 0.45f, 0f), new Vector3(0.72f, 0.7f, 0.14f), StoneColor,
                      keepCollider: true, smoothness: 0.15f);
            Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 0.8f, 0f), new Vector3(0.72f, 0.26f, 0.14f), StoneColor, smoothness: 0.15f);
            // chữ khắc trên mặt bia (mặt hướng về camera)
            string rank = string.IsNullOrEmpty(e.BestRank) ? "—" : e.BestRank;
            Text3D(s, new Vector3(0f, 0.66f, -0.075f), e.Name, 0.11f, new Color(0.2f, 0.18f, 0.2f), FontStyle.Bold);
            Text3D(s, new Vector3(0f, 0.47f, -0.075f), e.AgeDaysAtDeath().ToString("0.#") + " ngày · Rank " + rank,
                   0.055f, new Color(0.28f, 0.26f, 0.28f), FontStyle.Normal);
            Text3D(s, new Vector3(0f, 0.26f, -0.075f), e.DnaCode ?? "", 0.04f, new Color(0.32f, 0.3f, 0.32f), FontStyle.Normal);
            // 3 bông hoa nhỏ trước bia
            string[] flowers = { "#F7C6D9", "#FFFFFF", "#F2C84B" };
            for (int i = 0; i < flowers.Length; i++)
                Prim.Make(PrimitiveType.Sphere, s, new Vector3(-0.2f + 0.2f * i, 0.07f, -0.27f), Vector3.one * 0.1f, Prim.Hex(flowers[i]));
            return s;
        }

        /// <summary>Chữ 3D (TextMesh, font có sẵn của Unity — có dấu tiếng Việt). `height` = chiều cao chữ (mét).</summary>
        public static TextMesh Text3D(Transform parent, Vector3 localPos, string text, float height, Color color, FontStyle style)
        {
            var go = new GameObject("Text");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var tm = go.AddComponent<TextMesh>();
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                MeshRenderer mr = go.GetComponent<MeshRenderer>();
                if (mr == null) mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = font.material;
            }
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = height * 0.16f;   // cỡ chữ lớn + characterSize nhỏ => chữ nét
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = style;
            tm.color = color;
            return tm;
        }

        private void Update()
        {
            if (_riseT < 0f || _rising == null) return;
            _riseT += Time.deltaTime / 1.6f;
            float t = Mathf.Clamp01(_riseT);
            _rising.localScale = new Vector3(1f, Mathf.Max(0.01f, 1f - (1f - t) * (1f - t)), 1f);
            if (_riseT >= 1f) _riseT = -1f;
        }
    }

    /// <summary>
    /// Trứng nằm trong ổ sau khi thú mất (GDD G5). Chưa tới lúc: nằm im, thỉnh thoảng nhúc nhích, sáng dần.
    /// Tới lúc: lắc lư + lấp lánh; chạm vào thì nứt, vỡ vỏ và hỏi tên thú mới.
    /// </summary>
    public sealed class EggView : MonoBehaviour
    {
        private Transform _body;
        private Transform[] _sparkles;
        private bool _ready;
        private float _wobbleT = -1f, _wobbleTimer = 3f, _hatchT = -1f;
        private Action _hatched;
        private readonly List<Transform> _shells = new List<Transform>();
        private readonly List<Vector3> _shellVel = new List<Vector3>();

        public bool IsHatching => _hatchT >= 0f;

        public static EggView Create(Transform parent, Vector3 position, EggData egg, bool ready)
        {
            var go = new GameObject("Egg");
            go.transform.SetParent(parent, false);
            go.transform.position = position;
            var col = go.AddComponent<SphereCollider>();   // để chạm trúng trứng
            col.center = new Vector3(0f, 0.32f, 0f);
            col.radius = 0.42f;
            var v = go.AddComponent<EggView>();
            v.Build(egg);
            v.SetReady(ready);
            return v;
        }

        private void Build(EggData egg)
        {
            _body = Prim.Group(transform, "Shell");
            Color fur = egg != null ? DnaVisuals.Fur(egg.Dna) : Prim.Hex("#F2D1A8");
            Color shell = Color.Lerp(Color.white, fur, 0.25f);
            Prim.Make(PrimitiveType.Sphere, _body, new Vector3(0f, 0.3f, 0f), new Vector3(0.44f, 0.56f, 0.44f), shell, smoothness: 0.45f);
            // đốm trên vỏ mang màu lông của thú sắp nở
            var spots = new[] { new Vector3(0.16f, 0.38f, -0.12f), new Vector3(-0.15f, 0.24f, -0.13f), new Vector3(0.02f, 0.46f, -0.18f),
                                new Vector3(-0.1f, 0.42f, 0.16f), new Vector3(0.17f, 0.18f, 0.08f) };
            foreach (Vector3 p in spots)
                Prim.Make(PrimitiveType.Sphere, _body, p, Vector3.one * 0.11f, fur, smoothness: 0.4f);
            _sparkles = new Transform[4];
            for (int i = 0; i < _sparkles.Length; i++)
                _sparkles[i] = Prim.Make(PrimitiveType.Sphere, transform, Vector3.zero, Vector3.zero, Prim.Hex("#FFE27A"), smoothness: 0.9f).transform;
        }

        public void SetReady(bool ready) => _ready = ready;

        /// <summary>Người chơi chạm trứng. Chưa tới lúc: nhúc nhích nhẹ. Tới lúc: nở rồi gọi `hatched`.</summary>
        public void Tap(Action hatched)
        {
            if (IsHatching) return;
            if (!_ready)
            {
                _wobbleT = 0f;
                return;
            }
            _hatched = hatched;
            _hatchT = 0f;
        }

        private void Update()
        {
            float dt = Time.deltaTime, time = Time.time;
            if (_hatchT >= 0f)
            {
                UpdateHatch(dt);
                return;
            }
            // nhúc nhích: tới lúc nở thì lắc nhiều + thường xuyên
            _wobbleTimer -= dt;
            if (_wobbleTimer <= 0f)
            {
                _wobbleT = 0f;
                _wobbleTimer = _ready ? 1.4f : 6f;
            }
            float roll = 0f;
            if (_wobbleT >= 0f)
            {
                _wobbleT += dt / (_ready ? 0.7f : 0.5f);
                roll = Mathf.Sin(_wobbleT * Mathf.PI * 4f) * (_ready ? 12f : 5f) * (1f - _wobbleT);
                if (_wobbleT >= 1f) _wobbleT = -1f;
            }
            _body.localRotation = Quaternion.Euler(0f, 0f, roll);
            _body.localScale = Vector3.one * (1f + (_ready ? Mathf.Sin(time * 3f) * 0.02f : 0f));
            for (int i = 0; i < _sparkles.Length; i++)
            {
                float a = time * 1.6f + i * Mathf.PI * 0.5f;
                _sparkles[i].localPosition = new Vector3(Mathf.Cos(a) * 0.4f, 0.35f + Mathf.Sin(a * 1.7f) * 0.18f, Mathf.Sin(a) * 0.4f);
                _sparkles[i].localScale = Vector3.one * (_ready ? 0.05f + Mathf.Abs(Mathf.Sin(time * 4f + i)) * 0.03f : 0f);
            }
        }

        private void UpdateHatch(float dt)
        {
            _hatchT += dt;
            if (_hatchT < 0.9f)   // nứt: rung mạnh dần
            {
                float k = _hatchT / 0.9f;
                _body.localRotation = Quaternion.Euler(Mathf.Sin(_hatchT * 70f) * 4f * k, 0f, Mathf.Sin(_hatchT * 55f) * 10f * k);
                return;
            }
            if (_shells.Count == 0)   // vỡ: 2 nửa vỏ bay ra 2 bên
            {
                _body.gameObject.SetActive(false);
                foreach (Transform s in _sparkles) s.gameObject.SetActive(false);
                for (int i = 0; i < 2; i++)
                {
                    float side = i == 0 ? -1f : 1f;
                    Transform half = Prim.Make(PrimitiveType.Sphere, transform, new Vector3(0.1f * side, 0.3f, 0f),
                                               new Vector3(0.22f, 0.5f, 0.42f), Prim.Hex("#FFF7EA"), smoothness: 0.45f).transform;
                    _shells.Add(half);
                    _shellVel.Add(new Vector3(1.2f * side, 2.2f, 0f));
                }
                Action hatched = _hatched;
                _hatched = null;
                hatched?.Invoke();
            }
            for (int i = 0; i < _shells.Count; i++)
            {
                _shellVel[i] += Vector3.up * (-6f * dt);
                _shells[i].position += _shellVel[i] * dt;
                _shells[i].Rotate(0f, 0f, (i == 0 ? 220f : -220f) * dt);
                _shells[i].localScale *= Mathf.Max(0f, 1f - dt * 1.2f);
            }
            if (_hatchT > 2.2f) Destroy(gameObject);
        }
    }
}
