using System.Collections.Generic;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>
    /// Hình 1 con quái rừng (Phase 9.3), dựng bằng khối cơ bản + hoạt cảnh bằng code. Chỉ đọc trạng thái từ HuntMonster (C# thuần):
    /// chất nhờn nảy tưng tưng, dơi vỗ cánh, nấm phồng mũ, heo cào đất rồi lao, người đá giơ tay đập. Lấy đà thì hiện dấu ! đỏ
    /// (đập đất: vòng đỏ trên đất = vùng trúng; lao húc: vạch đỏ chỉ hướng lao) để người chơi kịp đỡ / lướt.
    /// </summary>
    public sealed class MonsterView : MonoBehaviour
    {
        private HuntMonster _m;
        private Transform _body, _alert, _ring, _arrow, _hpRoot, _hpFill, _stars;
        private Transform[] _wings, _legs, _arms;
        private Transform _cap;
        private readonly List<Material> _mats = new List<Material>();
        private readonly List<Color> _base = new List<Color>();
        private float _x, _t, _flash, _deathT = -1f, _scale, _hpShow;
        private int _lastFacing = 1;

        public HuntMonster Monster => _m;
        public Vector3 Center => transform.position + Vector3.up * (Height * 0.55f);
        public float Height { get; private set; }

        public static MonsterView Create(Transform parent, HuntMonster m, int region)
        {
            var go = new GameObject("Monster_" + m.Kind + "_" + m.Id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<MonsterView>();
            v._m = m;
            v._x = m.X;
            v.Build(region);
            v.Tick(0f);
            return v;
        }

        // ------------------------------------------------------------------ dựng hình

        private static Color C(string hex) => Prim.Hex(hex);

        /// <summary>Màu thân theo loại + vùng (mỗi vùng 1 bảng màu).</summary>
        private static Color BodyColor(MonsterKind k, int region)
        {
            switch (k)
            {
                case MonsterKind.Slime: return C(new[] { "#7DDB6B", "#9CC24D", "#6EC6FF", "#FF8C2E", "#C08CF2" }[region]);
                case MonsterKind.Bat: return C(new[] { "#7B5EA7", "#5B4636", "#8796B0", "#B8432E", "#E3A6F5" }[region]);
                case MonsterKind.Mushroom: return C(new[] { "#D9443B", "#D9443B", "#4FB3A9", "#E36A2E", "#7FE3F0" }[region]);
                case MonsterKind.Boar: return C(new[] { "#8A5A3C", "#7A5236", "#6F6A7E", "#8A4B3A", "#5A4E7D" }[region]);
                case MonsterKind.Golem: return C(new[] { "#9AA0A6", "#8C9A86", "#8C9A86", "#5C4A45", "#8E7BC0" }[region]);
                case MonsterKind.KingSlime: return C("#5FC94F");
                case MonsterKind.MushroomKing: return C("#B04BC0");
                case MonsterKind.StoneGolem: return C("#7D8C7A");
                case MonsterKind.BoarChief: return C("#5E2E22");
                default: return C("#6FD3E6");
            }
        }

        private void Build(int region)
        {
            region = Mathf.Clamp(region, 0, 4);
            MonsterDef d = _m.Def;
            float s = d.Size;
            _scale = s;
            _body = Prim.Group(transform, "Body");
            Color body = BodyColor(_m.Kind, region);
            switch (_m.Kind)
            {
                case MonsterKind.Slime:
                case MonsterKind.KingSlime:
                    BuildSlime(s, body);
                    if (_m.Kind == MonsterKind.KingSlime) Crown(new Vector3(0f, s * 1.62f, 0f), s * 0.7f);
                    break;
                case MonsterKind.Bat:
                    BuildBat(s, body);
                    break;
                case MonsterKind.Mushroom:
                case MonsterKind.MushroomKing:
                    BuildMushroom(s, body, _m.Kind == MonsterKind.MushroomKing ? C("#F2C84B") : Color.white);
                    if (_m.Kind == MonsterKind.MushroomKing) Crown(new Vector3(0f, s * 2.45f, 0f), s * 0.5f);
                    break;
                case MonsterKind.Boar:
                case MonsterKind.BoarChief:
                    BuildBoar(s, body, _m.Kind == MonsterKind.BoarChief);
                    break;
                default:
                    BuildGolem(s, body, _m.Kind == MonsterKind.CrystalGolem || (_m.Kind == MonsterKind.Golem && region == 4),
                               _m.Kind == MonsterKind.StoneGolem ? C("#7FD3FF") : _m.Kind == MonsterKind.CrystalGolem ? C("#FFFFFF") : C("#FFD23F"));
                    break;
            }
            BuildAlert();
            BuildHpBar();
            // bản riêng của vật liệu: chớp trắng khi trúng đòn không làm đổi màu quái khác
            foreach (Renderer r in _body.GetComponentsInChildren<Renderer>())
            {
                if (r.sharedMaterial == null) continue;
                var m = new Material(r.sharedMaterial);
                r.sharedMaterial = m;
                _mats.Add(m);
                _base.Add(m.color);
            }
        }

        private void Eyes(Vector3 at, float size, float gap, Color pupil)
        {
            foreach (float z in new[] { -gap, gap })
            {
                Prim.Make(PrimitiveType.Sphere, _body, at + new Vector3(0f, 0f, z), Vector3.one * size, Color.white, smoothness: 0.6f);
                Prim.Make(PrimitiveType.Sphere, _body, at + new Vector3(size * 0.38f, 0f, z), Vector3.one * size * 0.55f, pupil, smoothness: 0.8f);
            }
        }

        private void Crown(Vector3 at, float w)
        {
            Color gold = C("#F2C84B");
            Prim.Make(PrimitiveType.Cylinder, _body, at, new Vector3(w, w * 0.18f, w), gold, smoothness: 0.8f);
            for (int i = 0; i < 5; i++)
            {
                float a = i / 5f * Mathf.PI * 2f;
                Prim.Make(PrimitiveType.Cube, _body, at + new Vector3(Mathf.Cos(a) * w * 0.42f, w * 0.28f, Mathf.Sin(a) * w * 0.42f),
                          new Vector3(w * 0.14f, w * 0.36f, w * 0.14f), gold, smoothness: 0.8f, rotation: Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 45f));
            }
        }

        private void BuildSlime(float s, Color body)
        {
            Height = s * 1.7f;
            Prim.Make(PrimitiveType.Sphere, _body, new Vector3(0f, s * 0.8f, 0f), new Vector3(s * 2.3f, s * 1.7f, s * 2.1f), body, smoothness: 0.75f);
            Prim.Make(PrimitiveType.Sphere, _body, new Vector3(-s * 0.35f, s * 1.3f, -s * 0.55f), Vector3.one * s * 0.4f, Color.white, smoothness: 1f);
            Eyes(new Vector3(s * 0.95f, s * 1f, 0f), s * 0.36f, s * 0.34f, C("#2B2B2B"));
        }

        private void BuildBat(float s, Color body)
        {
            Height = s * 2f;
            Prim.Make(PrimitiveType.Sphere, _body, new Vector3(0f, 0f, 0f), new Vector3(s * 1.7f, s * 1.5f, s * 1.5f), body, smoothness: 0.3f);
            foreach (float z in new[] { -0.35f, 0.35f })
                Prim.Make(PrimitiveType.Cube, _body, new Vector3(s * 0.1f, s * 0.75f, z * s), new Vector3(s * 0.3f, s * 0.55f, s * 0.12f), body,
                          rotation: Quaternion.Euler(0f, 0f, 10f));
            Eyes(new Vector3(s * 0.7f, s * 0.15f, 0f), s * 0.3f, s * 0.3f, C("#E2463F"));
            _wings = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _wings[i] = Prim.Group(_body, "Wing" + i, new Vector3(0f, s * 0.1f, side * s * 0.6f));
                Prim.Make(PrimitiveType.Cube, _wings[i], new Vector3(0f, 0f, side * s * 0.85f), new Vector3(s * 1.1f, s * 0.07f, s * 1.7f), body * 0.8f,
                          smoothness: 0.2f);
            }
        }

        private void BuildMushroom(float s, Color cap, Color dots)
        {
            Height = s * 2.6f;
            Prim.Make(PrimitiveType.Cylinder, _body, new Vector3(0f, s * 0.7f, 0f), new Vector3(s * 1.15f, s * 0.7f, s * 1.15f), C("#F3E6C4"), smoothness: 0.2f);
            Eyes(new Vector3(s * 0.52f, s * 0.85f, 0f), s * 0.3f, s * 0.26f, C("#2B2B2B"));
            _cap = Prim.Group(_body, "Cap", new Vector3(0f, s * 1.55f, 0f));
            Prim.Make(PrimitiveType.Sphere, _cap, Vector3.zero, new Vector3(s * 2.5f, s * 1.3f, s * 2.5f), cap, smoothness: 0.5f);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.3f;
                Prim.Make(PrimitiveType.Sphere, _cap, new Vector3(Mathf.Cos(a) * s * 0.6f, s * 0.5f, Mathf.Sin(a) * s * 0.6f), Vector3.one * s * 0.32f, dots, smoothness: 0.5f);
            }
        }

        private void BuildBoar(float s, Color body, bool chief)
        {
            Height = s * 1.7f;
            Prim.Make(PrimitiveType.Sphere, _body, new Vector3(0f, s * 0.95f, 0f), new Vector3(s * 2.6f, s * 1.5f, s * 1.6f), body, smoothness: 0.15f);
            Prim.Make(PrimitiveType.Sphere, _body, new Vector3(s * 1.25f, s * 1.05f, 0f), new Vector3(s * 1.1f, s * 1.05f, s * 1.1f), body, smoothness: 0.15f);
            Prim.Make(PrimitiveType.Cylinder, _body, new Vector3(s * 1.78f, s * 0.95f, 0f), new Vector3(s * 0.5f, s * 0.12f, s * 0.5f), C("#E8A0A0"),
                      rotation: Quaternion.Euler(0f, 0f, 90f));
            foreach (float z in new[] { -0.22f, 0.22f })
            {
                Prim.Make(PrimitiveType.Cylinder, _body, new Vector3(s * 1.7f, s * 0.78f, z * s), new Vector3(s * 0.08f, s * 0.22f, s * 0.08f), C("#FFF4E6"),
                          rotation: Quaternion.Euler(0f, 0f, -35f));
                Prim.Make(PrimitiveType.Cube, _body, new Vector3(s * 1.1f, s * 1.55f, z * s * 1.6f), new Vector3(s * 0.3f, s * 0.35f, s * 0.1f), body * 0.8f,
                          rotation: Quaternion.Euler(0f, 0f, -15f));
            }
            Eyes(new Vector3(s * 1.55f, s * 1.22f, 0f), s * 0.2f, s * 0.3f, C("#2B2B2B"));
            _legs = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                _legs[i] = Prim.Group(_body, "Leg" + i, new Vector3((i < 2 ? 0.7f : -0.7f) * s, s * 0.45f, (i % 2 == 0 ? -0.45f : 0.45f) * s));
                Prim.Make(PrimitiveType.Cylinder, _legs[i], new Vector3(0f, -s * 0.22f, 0f), new Vector3(s * 0.3f, s * 0.25f, s * 0.3f), body * 0.7f);
            }
            if (chief)   // bờm lửa dọc lưng
                for (int i = 0; i < 6; i++)
                    Prim.Make(PrimitiveType.Sphere, _body, new Vector3((0.7f - i * 0.28f) * s, s * (1.7f + (i % 2) * 0.12f), 0f), Vector3.one * s * (0.45f - i * 0.03f),
                              C(i % 2 == 0 ? "#FF7A2F" : "#FFD23F"), smoothness: 0.9f);
            _stars = Prim.Group(_body, "Stars", new Vector3(s * 1.2f, s * 2.1f, 0f));
            for (int i = 0; i < 3; i++)
                Prim.Make(PrimitiveType.Sphere, _stars, new Vector3(Mathf.Cos(i * 2.1f) * s * 0.45f, 0f, Mathf.Sin(i * 2.1f) * s * 0.45f), Vector3.one * s * 0.18f,
                          C("#FFE066"), smoothness: 0.9f);
            _stars.gameObject.SetActive(false);
        }

        private void BuildGolem(float s, Color body, bool crystal, Color eye)
        {
            Height = s * 2.6f;
            Prim.Make(PrimitiveType.Cube, _body, new Vector3(0f, s * 0.45f, 0f), new Vector3(s * 1.5f, s * 0.9f, s * 1.3f), body * 0.85f, smoothness: crystal ? 0.9f : 0.1f);
            Prim.Make(PrimitiveType.Cube, _body, new Vector3(0f, s * 1.35f, 0f), new Vector3(s * 1.9f, s * 1.1f, s * 1.6f), body, smoothness: crystal ? 0.9f : 0.1f,
                      rotation: Quaternion.Euler(0f, 8f, 0f));
            Prim.Make(PrimitiveType.Cube, _body, new Vector3(s * 0.15f, s * 2.15f, 0f), new Vector3(s * 0.95f, s * 0.75f, s * 0.95f), body * 1.05f, smoothness: crystal ? 0.9f : 0.1f);
            foreach (float z in new[] { -0.2f, 0.2f })
                Prim.Make(PrimitiveType.Cube, _body, new Vector3(s * 0.63f, s * 2.2f, z * s), new Vector3(s * 0.08f, s * 0.14f, s * 0.18f), eye, smoothness: 1f);
            if (!crystal)   // rêu
                for (int i = 0; i < 4; i++)
                    Prim.Make(PrimitiveType.Sphere, _body, new Vector3((i - 1.5f) * s * 0.4f, s * (1.9f + (i % 2) * 0.1f), (i % 2 == 0 ? 0.6f : -0.6f) * s),
                              new Vector3(s * 0.6f, s * 0.2f, s * 0.5f), C("#5FA84F"), smoothness: 0.1f);
            else
                for (int i = 0; i < 4; i++)
                    Prim.Make(PrimitiveType.Cube, _body, new Vector3((i % 2 == 0 ? -0.3f : 0.3f) * s, s * 2.1f, (i < 2 ? -0.85f : 0.85f) * s),
                              new Vector3(s * 0.25f, s * 0.8f, s * 0.25f), C(i % 2 == 0 ? "#7FE3F0" : "#E3A6F5"), smoothness: 0.95f,
                              rotation: Quaternion.Euler(i < 2 ? -25f : 25f, 0f, 15f));
            _arms = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                _arms[i] = Prim.Group(_body, "Arm" + i, new Vector3(0f, s * 1.75f, side * s * 1.05f));
                Prim.Make(PrimitiveType.Cube, _arms[i], new Vector3(0f, -s * 0.6f, 0f), new Vector3(s * 0.55f, s * 1.2f, s * 0.55f), body * 0.9f,
                          smoothness: crystal ? 0.9f : 0.1f);
            }
        }

        /// <summary>Dấu ! đỏ trên đầu + vòng đỏ trên đất (đập đất) + vạch đỏ (lao húc), chỉ hiện lúc lấy đà.</summary>
        private void BuildAlert()
        {
            Color red = C("#FF3B30");
            _alert = Prim.Group(transform, "Alert", Vector3.up * (Height + 0.35f + _m.Def.Flying));
            Prim.Make(PrimitiveType.Cube, _alert, new Vector3(0f, 0.14f, 0f), new Vector3(0.09f, 0.26f, 0.09f), red, smoothness: 0.6f);
            Prim.Make(PrimitiveType.Sphere, _alert, new Vector3(0f, -0.07f, 0f), Vector3.one * 0.11f, red, smoothness: 0.6f);
            _alert.gameObject.SetActive(false);
            _ring = Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, 0.015f, 0f), new Vector3(1f, 0.004f, 1f), C("#E8463B"), smoothness: 0f).transform;
            _ring.gameObject.SetActive(false);
            _arrow = Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, 0.02f, 0f), new Vector3(1f, 0.006f, 0.22f), C("#E8463B"), smoothness: 0f).transform;
            _arrow.gameObject.SetActive(false);
        }

        private void BuildHpBar()
        {
            _hpRoot = Prim.Group(transform, "Hp", Vector3.up * (Height + 0.2f + _m.Def.Flying));
            float w = Mathf.Max(0.5f, _m.Def.Size * 1.6f);
            Prim.Make(PrimitiveType.Cube, _hpRoot, Vector3.zero, new Vector3(w + 0.04f, 0.075f, 0.01f), C("#2B2B2B"), smoothness: 0f);
            _hpFill = Prim.Group(_hpRoot, "Fill", new Vector3(-w * 0.5f, 0f, -0.006f));
            Prim.Make(PrimitiveType.Cube, _hpFill, new Vector3(w * 0.5f, 0f, 0f), new Vector3(w, 0.05f, 0.01f), C("#7CFC6B"), smoothness: 0f);
            _hpRoot.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ hoạt cảnh

        /// <summary>Trúng đòn: chớp trắng + hiện vạch máu 1 lúc.</summary>
        public void Hit()
        {
            _flash = 0.12f;
            _hpShow = 2.5f;
        }

        public void Die()
        {
            if (_deathT >= 0f) return;
            _deathT = 0f;
            _alert.gameObject.SetActive(false);
            _ring.gameObject.SetActive(false);
            _arrow.gameObject.SetActive(false);
            _hpRoot.gameObject.SetActive(false);
        }

        public bool Gone => _deathT >= 0.6f;

        public void Tick(float dt)
        {
            _t += dt;
            if (_deathT >= 0f)   // chết: xoay tròn, xẹp xuống
            {
                _deathT += dt;
                float k = Mathf.Clamp01(_deathT / 0.5f);
                _body.localScale = new Vector3(1f + k * 0.4f, 1f - k, 1f + k * 0.4f);
                _body.localRotation = Quaternion.Euler(0f, _deathT * 720f, 0f);
                return;
            }
            HuntMonster m = _m;
            float kx = 1f - Mathf.Exp(-dt * 20f);
            float prevX = _x;
            _x = Mathf.Lerp(_x, m.X, dt <= 0f ? 1f : kx);
            float speed = dt > 0f ? Mathf.Abs(_x - prevX) / dt : 0f;
            transform.localPosition = new Vector3(_x, 0f, 0f);
            if (m.Facing != 0) _lastFacing = m.Facing;
            Quaternion face = Quaternion.Euler(0f, _lastFacing > 0 ? 20f : 160f, 0f);   // mặt hơi quay về camera
            _body.localRotation = Quaternion.Slerp(_body.localRotation, face, dt <= 0f ? 1f : 1f - Mathf.Exp(-dt * 12f));

            bool windup = m.Phase == MonsterPhase.Windup;
            float w01 = windup ? 1f - Mathf.Clamp01(m.WindupLeft01) : 0f;   // 0 → 1 trong lúc lấy đà
            float s = _scale;
            Vector3 pos = Vector3.zero;
            Vector3 scl = Vector3.one;
            switch (m.Kind)
            {
                case MonsterKind.Slime:
                case MonsterKind.KingSlime:
                {
                    float hop = speed > 0.1f ? Mathf.Abs(Mathf.Sin(_t * 7f)) * s * 0.5f : 0f;
                    float squash = windup ? 1f - 0.3f * w01 : 1f + 0.05f * Mathf.Sin(_t * 4f) - (hop < s * 0.05f && speed > 0.1f ? 0.12f : 0f);
                    pos.y = hop;
                    if (m.Phase == MonsterPhase.Act || (m.Phase == MonsterPhase.Recover && m.PhaseT < 0.15f)) pos.x = 0.35f * _lastFacing;
                    scl = new Vector3(1f / Mathf.Sqrt(squash), squash, 1f / Mathf.Sqrt(squash));
                    break;
                }
                case MonsterKind.Bat:
                {
                    float flap = Mathf.Sin(_t * 22f) * 55f;
                    _wings[0].localRotation = Quaternion.Euler(-flap, 0f, 0f);
                    _wings[1].localRotation = Quaternion.Euler(flap, 0f, 0f);
                    float fly = m.Def.Flying + Mathf.Sin(_t * 3f + m.Id) * 0.12f;
                    if (windup) fly += 0.35f * w01;   // vút lên lấy đà
                    if (m.Phase == MonsterPhase.Act || (m.Phase == MonsterPhase.Recover && m.PhaseT < 0.2f)) fly = 0.55f;   // bổ nhào
                    pos.y = fly;
                    break;
                }
                case MonsterKind.Mushroom:
                case MonsterKind.MushroomKing:
                {
                    float puff = windup ? 1f + 0.28f * w01 : (m.Phase == MonsterPhase.Act ? 0.8f : 1f);
                    _cap.localScale = new Vector3(puff, 1f / puff, puff);
                    _body.localRotation *= Quaternion.Euler(Mathf.Sin(_t * 9f) * (speed > 0.1f ? 7f : 2f), 0f, 0f);   // lạch bạch
                    break;
                }
                case MonsterKind.Boar:
                case MonsterKind.BoarChief:
                {
                    bool charging = m.Phase == MonsterPhase.Act && m.Attack != null && m.Attack.Move == MonsterMove.Charge;
                    float run = charging ? 26f : speed > 0.1f ? 12f : 0f;
                    for (int i = 0; i < _legs.Length; i++)
                        _legs[i].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * run + (i % 2) * Mathf.PI) * (run > 0f ? 35f : 0f));
                    if (windup)   // cúi đầu cào đất, rung người
                    {
                        _legs[0].localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_t * 30f) * 40f);
                        pos.x = Mathf.Sin(_t * 50f) * 0.03f;
                        _body.localRotation *= Quaternion.Euler(0f, 0f, -8f * w01);
                    }
                    if (charging) _body.localRotation *= Quaternion.Euler(0f, 0f, -10f);
                    _stars.gameObject.SetActive(m.Dizzy || m.Phase == MonsterPhase.Stunned);
                    _stars.localRotation = Quaternion.Euler(0f, _t * 300f, 0f);
                    break;
                }
                default:   // người đá: giơ 2 tay lên rồi đập xuống
                {
                    float raise = windup ? w01 : (m.Phase == MonsterPhase.Act || (m.Phase == MonsterPhase.Recover && m.PhaseT < 0.12f)) ? -0.25f : 0f;
                    float swing = Mathf.Sin(_t * 5f) * (speed > 0.1f ? 12f : 3f);
                    _arms[0].localRotation = Quaternion.Euler(-160f * raise + swing, 0f, 0f);
                    _arms[1].localRotation = Quaternion.Euler(-160f * raise - swing, 0f, 0f);
                    pos.y = Mathf.Abs(Mathf.Sin(_t * 5f)) * (speed > 0.1f ? 0.05f : 0f);
                    if (windup) _body.localRotation *= Quaternion.Euler(0f, 0f, 8f * w01);
                    break;
                }
            }
            if (m.Phase == MonsterPhase.Stunned && m.Kind != MonsterKind.Boar && m.Kind != MonsterKind.BoarChief)
                _body.localRotation *= Quaternion.Euler(Mathf.Sin(_t * 18f) * 8f, 0f, 0f);   // choáng: lắc lư
            _body.localPosition = pos;
            _body.localScale = scl;

            // báo đòn: dấu ! nhấp nháy, vòng đỏ (đập đất), vạch đỏ (lao húc)
            _alert.gameObject.SetActive(windup);
            if (windup) _alert.localScale = Vector3.one * (1f + 0.25f * Mathf.Abs(Mathf.Sin(_t * 14f)));
            bool smash = windup && m.Attack != null && m.Attack.Move == MonsterMove.Smash;
            _ring.gameObject.SetActive(smash);
            if (smash)
            {
                float r = m.Attack.Radius * 2f * (0.35f + 0.65f * w01);
                _ring.localScale = new Vector3(r, 0.004f, r * 0.55f);
            }
            bool charge = windup && m.Attack != null && m.Attack.Move == MonsterMove.Charge;
            _arrow.gameObject.SetActive(charge);
            if (charge)
            {
                float len = Mathf.Min(6f, m.Attack.Range) * (0.3f + 0.7f * w01);
                _arrow.localPosition = new Vector3(_lastFacing * len * 0.5f, 0.02f, 0f);
                _arrow.localScale = new Vector3(len, 0.006f, 0.22f);
            }

            // vạch máu: trùm luôn hiện (HUD có vạch to riêng), quái thường hiện 1 lúc sau khi trúng đòn
            _hpShow = Mathf.Max(0f, _hpShow - dt);
            bool showHp = _hpShow > 0f && !m.Def.Boss;
            _hpRoot.gameObject.SetActive(showHp);
            if (showHp)
            {
                _hpFill.localScale = new Vector3(Mathf.Max(0.001f, m.Hp01), 1f, 1f);
                _hpRoot.localPosition = new Vector3(0f, Height + 0.2f + pos.y, 0f);
                Camera cam = Camera.main;
                if (cam != null) _hpRoot.rotation = cam.transform.rotation;
            }

            // chớp trắng khi trúng; đỏ ửng khi trùm nổi giận
            _flash = Mathf.Max(0f, _flash - dt);
            float white = _flash > 0f ? 0.75f : 0f;
            float rage = m.Enraged ? 0.25f + 0.1f * Mathf.Sin(_t * 8f) : 0f;
            for (int i = 0; i < _mats.Count; i++)
            {
                Color c = Color.Lerp(_base[i], new Color(1f, 0.25f, 0.2f), rage);
                _mats[i].color = Color.Lerp(c, Color.white, white);
            }
        }

        private void OnDestroy()
        {
            foreach (Material m in _mats)
                if (m != null) Destroy(m);
        }
    }

    /// <summary>Viên đạn của quái: bào tử nấm (tròn, phát sáng), sóng đất (người đá, sát đất), mảnh pha lê.</summary>
    public sealed class ShotView : MonoBehaviour
    {
        private HuntProjectile _p;
        private Transform _ball;
        private float _t;

        public HuntProjectile Shot => _p;

        public static ShotView Create(Transform parent, HuntProjectile p)
        {
            var go = new GameObject("Shot_" + p.Id);
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<ShotView>();
            v._p = p;
            Color c = p.OwnerKind == MonsterKind.CrystalGolem ? Prim.Hex("#7FE3F0")
                    : p.Low ? Prim.Hex("#9A8A70") : Prim.Hex(p.OwnerKind == MonsterKind.MushroomKing ? "#D98CF2" : "#B8F27A");
            v._ball = p.Low
                ? Prim.Make(PrimitiveType.Cube, go.transform, Vector3.zero, new Vector3(0.45f, 0.35f, 0.8f), c, smoothness: 0.1f).transform
                : Prim.Make(PrimitiveType.Sphere, go.transform, Vector3.zero, Vector3.one * 0.26f, c, smoothness: 0.9f).transform;
            v.Tick(0f);
            return v;
        }

        public void Tick(float dt)
        {
            _t += dt;
            float y = _p.Low ? 0.17f : 0.65f + Mathf.Sin(_t * 12f) * 0.05f;
            transform.localPosition = new Vector3(_p.X, y, 0f);
            _ball.localRotation = Quaternion.Euler(_t * 400f, _t * 260f, 0f);
        }
    }
}
