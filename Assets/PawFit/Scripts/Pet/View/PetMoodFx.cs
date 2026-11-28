using System.Collections.Generic;
using PawFit.Core.Audio;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Pet.View
{
    /// <summary>
    /// Hiệu ứng "nói bằng hình" quanh thú (không có chữ / thanh số):
    ///   ruồi bay quanh khi bẩn · bong bóng có quả táo khi đòi ăn · chữ Z bay lên khi ngủ · tiếng kêu / ngáp
    ///   ốm: túi chườm trên đầu, nguy kịch có đám mây mưa nhỏ, đắp chăn khi nằm, khăn lau, dấu + xanh khi đang hồi phục.
    /// Dựng bằng khối cơ bản; tiếng phát qua Sfx (âm tạo bằng code, không cần asset).
    /// </summary>
    public sealed class PetMoodFx : MonoBehaviour
    {
        private sealed class Fly
        {
            public Transform T;
            public Transform WingL, WingR;
            public float Angle, Speed, Radius, Height, Seed;
        }

        private sealed class Glyph
        {
            public Transform T;
            public float Age;
            public Vector3 Start;
        }

        private IPetAvatar _avatar;
        private Transform _root;
        private Camera _cam;
        private readonly List<Fly> _flies = new List<Fly>();
        private readonly List<Glyph> _zs = new List<Glyph>();
        private readonly List<Glyph> _hearts = new List<Glyph>();
        private Transform _bubble;
        private float _bubbleT = -1f;
        private bool _sleeping;
        private float _zTimer;

        // ---- ốm (Phase 2.1) ----
        private float _sickness;
        private bool _critical, _recovering;
        private Transform _iceBag, _cloud, _blanket, _towel;
        private readonly List<Transform> _drops = new List<Transform>();
        private readonly List<Glyph> _pluses = new List<Glyph>();
        private float _plusTimer, _towelT = -1f;

        // ---- tiến hóa (Phase 2.2) ----
        private readonly List<Transform> _burst = new List<Transform>();
        private readonly List<Vector3> _burstVel = new List<Vector3>();
        private Transform _glow;
        private float _evolveT = -1f;
        private bool _aura;
        private Transform[] _auraDots;

        public void Init(IPetAvatar avatar)
        {
            _avatar = avatar;
            if (_root == null) _root = new GameObject("PetFx").transform;   // không làm con của thú: thú xoay thì hiệu ứng không xoay theo
        }

        public void SetFlies(int count)
        {
            if (_root == null) return;
            while (_flies.Count < count) _flies.Add(MakeFly(_flies.Count));
            while (_flies.Count > count)
            {
                Fly f = _flies[_flies.Count - 1];
                _flies.RemoveAt(_flies.Count - 1);
                Destroy(f.T.gameObject);
            }
        }

        public void SetSleeping(bool sleeping) => _sleeping = sleeping;

        /// <summary>Mức ốm (0 khỏe .. 1 nguy kịch) + có đang hồi phục không.</summary>
        public void SetHealth(float sickness, bool critical, bool recovering)
        {
            _sickness = sickness;
            _critical = critical;
            _recovering = recovering;
        }

        /// <summary>Lau người cho thú ốm: khăn ấm chà qua lưng.</summary>
        public void Wipe()
        {
            if (_root == null) return;
            if (_towel == null)
            {
                _towel = Prim.Group(_root, "Towel");
                Prim.Make(PrimitiveType.Cube, _towel, Vector3.zero, new Vector3(0.34f, 0.06f, 0.22f), Prim.Hex("#F4F7FB"), smoothness: 0.1f);
                Prim.Make(PrimitiveType.Cube, _towel, new Vector3(0f, 0.001f, 0f), new Vector3(0.35f, 0.055f, 0.05f), Prim.Hex("#8EC5E8"));
            }
            _towelT = 0f;
        }

        /// <summary>Đòi ăn: bong bóng có quả táo trên đầu + tiếng kêu.</summary>
        public void Cry()
        {
            if (_root == null) return;
            if (_bubble == null) _bubble = MakeFoodBubble();
            _bubbleT = 0f;
            Sfx.PlayVaried(SoundId.Cry, 0.55f);
        }

        public void Yawn() => Sfx.PlayVaried(SoundId.Yawn, 0.5f);

        /// <summary>Được chạm / vuốt ve: `count` trái tim hồng bay lên từ đầu (Phase 8.2).</summary>
        public void Hearts(int count)
        {
            if (_root == null || _avatar == null) return;
            Vector3 head = _avatar.HeadTop;
            for (int i = 0; i < count; i++)
                _hearts.Add(new Glyph { T = MakeHeart(), Start = head + new Vector3(Random.Range(-0.18f, 0.18f), 0.05f, Random.Range(-0.08f, 0.08f)), Age = -i * 0.12f });
        }

        /// <summary>Tiến hóa: quầng sáng nở ra + tia lấp lánh bắn tung tóe + tiếng chuông.</summary>
        public void Evolve(bool ultimate)
        {
            if (_root == null) return;
            foreach (Transform t in _burst) Destroy(t.gameObject);
            _burst.Clear();
            _burstVel.Clear();
            Color c = ultimate ? Prim.Hex("#FFD54A") : Prim.Hex("#FFF6C8");
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2f;
                Transform s = Prim.Make(PrimitiveType.Sphere, _root, Vector3.zero, Vector3.one * 0.07f, c, smoothness: 0.9f).transform;
                s.position = transform.position + Vector3.up * 0.6f;
                _burst.Add(s);
                _burstVel.Add(new Vector3(Mathf.Cos(a) * 1.6f, 1.2f + (i % 3) * 0.5f, Mathf.Sin(a) * 1.6f));
            }
            if (_glow == null)
                _glow = Prim.Make(PrimitiveType.Sphere, _root, Vector3.zero, Vector3.zero, Prim.Hex("#FFFBEA"), smoothness: 0.95f).transform;
            _evolveT = 0f;
            Sfx.Play(SoundId.Chime, 0.6f);
        }

        /// <summary>Ultimate: 3 đốm vàng bay chậm quanh thú.</summary>
        public void SetAura(bool on) => _aura = on;

        private void Update()
        {
            if (_root == null || _avatar == null) return;
            if (_cam == null) _cam = Camera.main;
            float dt = Time.deltaTime, time = Time.time;
            Vector3 center = transform.position + Vector3.up * 0.65f;
            Vector3 head = _avatar.HeadTop;

            // ruồi: bay vòng quanh thân, lượn lên xuống, vỗ cánh rất nhanh
            foreach (Fly f in _flies)
            {
                f.Angle += f.Speed * dt;
                float r = f.Radius + Mathf.Sin(time * 2.3f + f.Seed) * 0.08f;
                Vector3 p = center + new Vector3(Mathf.Cos(f.Angle) * r, f.Height + Mathf.Sin(time * 3.1f + f.Seed * 2f) * 0.12f,
                                                 Mathf.Sin(f.Angle) * r);
                p += new Vector3(Mathf.Sin(time * 17f + f.Seed), Mathf.Sin(time * 13f + f.Seed * 3f), Mathf.Cos(time * 15f + f.Seed)) * 0.015f;
                Vector3 dir = p - f.T.position;
                f.T.position = p;
                if (dir.sqrMagnitude > 1e-6f) f.T.rotation = Quaternion.LookRotation(dir);
                float flap = Mathf.Sin(time * 95f + f.Seed) * 40f;
                f.WingL.localRotation = Quaternion.Euler(0f, 0f, 25f + flap);
                f.WingR.localRotation = Quaternion.Euler(0f, 0f, -25f - flap);
            }

            // bong bóng đòi ăn: nảy ra, giữ 1 lúc, thu lại
            if (_bubble != null)
            {
                if (_bubbleT >= 0f)
                {
                    _bubbleT += dt;
                    float s = _bubbleT < 0.18f ? EaseOutBack(_bubbleT / 0.18f)
                            : _bubbleT < 1.4f ? 1f
                            : 1f - Mathf.Clamp01((_bubbleT - 1.4f) / 0.2f);
                    _bubble.localScale = Vector3.one * s;
                    _bubble.position = head + Vector3.up * (0.28f + Mathf.Sin(_bubbleT * 5f) * 0.02f) + Vector3.right * 0.18f;
                    Billboard(_bubble);
                    if (_bubbleT >= 1.6f) _bubbleT = -1f;
                }
                else _bubble.localScale = Vector3.zero;
            }

            UpdateHealth(dt, time, head);
            UpdateEvolve(dt, time);

            // Zzz khi ngủ
            if (_sleeping)
            {
                _zTimer -= dt;
                if (_zTimer <= 0f)
                {
                    _zTimer = 1.15f;
                    _zs.Add(new Glyph { T = MakeZ(), Start = head + new Vector3(0.12f, 0.05f, 0f) });
                }
            }
            for (int i = _hearts.Count - 1; i >= 0; i--)   // tim: nảy ra, bay lên lắc lư, mờ dần
            {
                Glyph h = _hearts[i];
                h.Age += dt;
                if (h.Age < 0f)
                {
                    h.T.localScale = Vector3.zero;
                    continue;
                }
                float t = h.Age / 1.3f;
                if (t >= 1f)
                {
                    Destroy(h.T.gameObject);
                    _hearts.RemoveAt(i);
                    continue;
                }
                h.T.position = h.Start + new Vector3(Mathf.Sin(t * 9f + i) * 0.06f, 0.55f * t, 0f);
                h.T.localScale = Vector3.one * (t < 0.15f ? EaseOutBack(t / 0.15f) : 1f) * (t > 0.75f ? (1f - t) / 0.25f : 1f);
                Billboard(h.T);
            }
            for (int i = _zs.Count - 1; i >= 0; i--)
            {
                Glyph z = _zs[i];
                z.Age += dt;
                float t = z.Age / 2.4f;
                if (t >= 1f)
                {
                    Destroy(z.T.gameObject);
                    _zs.RemoveAt(i);
                    continue;
                }
                z.T.position = z.Start + new Vector3(0.25f * t + Mathf.Sin(t * 6f) * 0.05f, 0.6f * t, 0f);
                z.T.localScale = Vector3.one * (Mathf.Lerp(0.6f, 1.15f, t) * (t > 0.8f ? (1f - t) / 0.2f : 1f));
                Billboard(z.T);
            }
        }

        private void UpdateHealth(float dt, float time, Vector3 head)
        {
            bool ill = _sickness >= 0.6f;
            // túi chườm trên đầu
            if (ill && _iceBag == null) _iceBag = MakeIceBag();
            if (_iceBag != null)
            {
                _iceBag.gameObject.SetActive(ill);
                if (ill)
                {
                    _iceBag.position = head + Vector3.up * (-0.1f + Mathf.Sin(time * 2f) * 0.005f);
                    _iceBag.rotation = transform.rotation * Quaternion.Euler(0f, 0f, 8f);
                }
            }
            // nguy kịch: đám mây mưa nhỏ lơ lửng trên đầu
            if (_critical && _cloud == null) _cloud = MakeCloud();
            if (_cloud != null)
            {
                _cloud.gameObject.SetActive(_critical);
                if (_critical)
                {
                    _cloud.position = head + new Vector3(Mathf.Sin(time * 0.8f) * 0.05f, 0.42f, 0f);
                    for (int i = 0; i < _drops.Count; i++)
                    {
                        float t = (time * 0.9f + i / (float)_drops.Count) % 1f;
                        _drops[i].localPosition = new Vector3(-0.12f + 0.12f * i, -0.06f - 0.42f * t, 0f);
                        _drops[i].localScale = new Vector3(0.025f, 0.045f, 0.025f) * (1f - t * 0.6f);
                    }
                }
            }
            // đắp chăn khi đang ốm mà nằm ngủ
            bool blanket = ill && _sleeping;
            if (blanket && _blanket == null) _blanket = MakeBlanket();
            if (_blanket != null)
            {
                _blanket.gameObject.SetActive(blanket);
                if (blanket)
                {
                    _blanket.position = transform.position + Vector3.up * 0.36f + transform.forward * (-0.05f);
                    _blanket.rotation = transform.rotation;
                }
            }
            // khăn lau chà qua lại trên lưng
            if (_towel != null)
            {
                if (_towelT >= 0f)
                {
                    _towelT += dt / 0.9f;
                    float s = Mathf.Sin(_towelT * Mathf.PI * 4f);
                    _towel.gameObject.SetActive(true);
                    _towel.position = transform.position + Vector3.up * 0.62f + transform.right * (s * 0.15f) +
                                      transform.forward * (-0.05f);
                    _towel.rotation = transform.rotation * Quaternion.Euler(0f, s * 12f, 0f);
                    _towel.localScale = Vector3.one * Mathf.Clamp01(Mathf.Min(_towelT * 6f, (1f - _towelT) * 6f));
                    if (_towelT >= 1f) _towelT = -1f;
                }
                else _towel.gameObject.SetActive(false);
            }
            // đang hồi phục: dấu + xanh thỉnh thoảng bay lên
            if (_recovering)
            {
                _plusTimer -= dt;
                if (_plusTimer <= 0f)
                {
                    _plusTimer = 2.4f;
                    _pluses.Add(new Glyph { T = MakePlus(), Start = head + new Vector3(-0.15f, 0.08f, 0f) });
                }
            }
            for (int i = _pluses.Count - 1; i >= 0; i--)
            {
                Glyph g = _pluses[i];
                g.Age += dt;
                float t = g.Age / 1.6f;
                if (t >= 1f)
                {
                    Destroy(g.T.gameObject);
                    _pluses.RemoveAt(i);
                    continue;
                }
                g.T.position = g.Start + new Vector3(0f, 0.45f * t, 0f);
                g.T.localScale = Vector3.one * (t < 0.2f ? t / 0.2f : t > 0.75f ? (1f - t) / 0.25f : 1f);
                Billboard(g.T);
            }
        }

        private void UpdateEvolve(float dt, float time)
        {
            if (_evolveT >= 0f)
            {
                _evolveT += dt;
                float t = _evolveT / 1.8f;
                if (_glow != null)
                {
                    _glow.position = transform.position + Vector3.up * 0.6f;
                    float s = t < 0.35f ? t / 0.35f * 1.9f : 1.9f * Mathf.Max(0f, 1f - (t - 0.35f) / 0.65f);
                    _glow.localScale = Vector3.one * s;
                }
                for (int i = 0; i < _burst.Count; i++)
                {
                    _burstVel[i] += Vector3.up * (-2.5f * dt);
                    _burst[i].position += _burstVel[i] * dt;
                    _burst[i].localScale = Vector3.one * (0.07f * Mathf.Max(0f, 1f - t));
                }
                if (t >= 1f)
                {
                    foreach (Transform b in _burst) Destroy(b.gameObject);
                    _burst.Clear();
                    _burstVel.Clear();
                    if (_glow != null) _glow.localScale = Vector3.zero;
                    _evolveT = -1f;
                }
            }
            if (_aura && _auraDots == null)
            {
                _auraDots = new Transform[3];
                for (int i = 0; i < 3; i++)
                    _auraDots[i] = Prim.Make(PrimitiveType.Sphere, _root, Vector3.zero, Vector3.one * 0.06f, Prim.Hex("#FFD54A"), smoothness: 0.9f).transform;
            }
            if (_auraDots != null)
                for (int i = 0; i < _auraDots.Length; i++)
                {
                    _auraDots[i].gameObject.SetActive(_aura);
                    float a = time * 0.9f + i * Mathf.PI * 2f / 3f;
                    _auraDots[i].position = transform.position + new Vector3(Mathf.Cos(a) * 0.7f, 0.45f + Mathf.Sin(time * 1.3f + i) * 0.2f, Mathf.Sin(a) * 0.7f);
                }
        }

        private Transform MakeIceBag()
        {
            Transform t = Prim.Group(_root, "IceBag");
            Prim.Make(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.26f, 0.08f, 0.22f), Prim.Hex("#A9D8F5"), smoothness: 0.7f);
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 0.05f, 0f), new Vector3(0.07f, 0.06f, 0.07f), Prim.Hex("#E8F4FC"), smoothness: 0.5f);
            return t;
        }

        private Transform MakeCloud()
        {
            Transform t = Prim.Group(_root, "RainCloud");
            Color c = Prim.Hex("#8C939E");
            Prim.Make(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.22f, 0.15f, 0.15f), c);
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(-0.13f, -0.02f, 0f), new Vector3(0.16f, 0.12f, 0.13f), c);
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0.13f, -0.02f, 0f), new Vector3(0.17f, 0.12f, 0.13f), c);
            for (int i = 0; i < 3; i++)
                _drops.Add(Prim.Make(PrimitiveType.Sphere, t, Vector3.zero, Vector3.one * 0.03f, Prim.Hex("#5FA8E6"), smoothness: 0.8f).transform);
            return t;
        }

        private Transform MakeBlanket()
        {
            Transform t = Prim.Group(_root, "Blanket");
            Prim.Make(PrimitiveType.Cube, t, Vector3.zero, new Vector3(0.7f, 0.1f, 0.78f), Prim.Hex("#F2A65A"), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.002f, 0.25f), new Vector3(0.71f, 0.1f, 0.12f), Prim.Hex("#FFF1D6"));
            Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.002f, -0.15f), new Vector3(0.71f, 0.1f, 0.06f), Prim.Hex("#E07B39"));
            return t;
        }

        /// <summary>Dấu + màu xanh (đang khỏe dần).</summary>
        private Transform MakePlus()
        {
            Transform p = Prim.Group(_root, "Plus");
            Color c = Prim.Hex("#4CC38A");
            Prim.Make(PrimitiveType.Cube, p, Vector3.zero, new Vector3(0.12f, 0.035f, 0.02f), c);
            Prim.Make(PrimitiveType.Cube, p, Vector3.zero, new Vector3(0.035f, 0.12f, 0.02f), c);
            return p;
        }

        private void Billboard(Transform t)
        {
            if (_cam == null) return;
            Vector3 d = t.position - _cam.transform.position;
            if (d.sqrMagnitude > 1e-6f) t.rotation = Quaternion.LookRotation(d);
        }

        private void OnDestroy()
        {
            if (_root != null) Destroy(_root.gameObject);
        }

        // ------------------------------------------------------------------
        // Dựng hình
        // ------------------------------------------------------------------

        private Fly MakeFly(int i)
        {
            Transform t = Prim.Group(_root, "Fly");
            Prim.Make(PrimitiveType.Sphere, t, Vector3.zero, new Vector3(0.035f, 0.032f, 0.05f), Prim.Hex("#1B1B1E"), smoothness: 0.6f);
            Color wing = Prim.Hex("#D9E2EA");
            Transform wl = Prim.Group(t, "WingL", new Vector3(-0.012f, 0.014f, 0f));
            Prim.Make(PrimitiveType.Sphere, wl, new Vector3(-0.02f, 0f, 0f), new Vector3(0.04f, 0.004f, 0.022f), wing, smoothness: 0.8f);
            Transform wr = Prim.Group(t, "WingR", new Vector3(0.012f, 0.014f, 0f));
            Prim.Make(PrimitiveType.Sphere, wr, new Vector3(0.02f, 0f, 0f), new Vector3(0.04f, 0.004f, 0.022f), wing, smoothness: 0.8f);
            return new Fly
            {
                T = t, WingL = wl, WingR = wr,
                Angle = i * 2.4f, Speed = (i % 2 == 0 ? 1f : -1f) * (2.0f + 0.35f * i), Radius = 0.5f + 0.07f * i,
                Height = 0.05f + 0.12f * (i % 3), Seed = i * 1.7f + 0.3f,
            };
        }

        private Transform MakeFoodBubble()
        {
            Transform b = Prim.Group(_root, "FoodBubble");
            Color white = Prim.Hex("#FFFFFF");
            Prim.Make(PrimitiveType.Sphere, b, Vector3.zero, new Vector3(0.36f, 0.3f, 0.07f), white, smoothness: 0.3f);
            Prim.Make(PrimitiveType.Sphere, b, new Vector3(-0.12f, -0.17f, 0f), new Vector3(0.08f, 0.08f, 0.05f), white, smoothness: 0.3f);
            Prim.Make(PrimitiveType.Sphere, b, new Vector3(-0.18f, -0.24f, 0f), new Vector3(0.045f, 0.045f, 0.03f), white, smoothness: 0.3f);
            // quả táo (mặt bong bóng hướng -Z về phía camera vì Billboard nhìn từ camera ra)
            Prim.Make(PrimitiveType.Sphere, b, new Vector3(0f, -0.01f, -0.04f), new Vector3(0.15f, 0.14f, 0.06f), Prim.Hex("#E2463F"), smoothness: 0.55f);
            Prim.Make(PrimitiveType.Cube, b, new Vector3(0f, 0.075f, -0.05f), new Vector3(0.012f, 0.04f, 0.012f), Prim.Hex("#6B4A2B"));
            Prim.Make(PrimitiveType.Sphere, b, new Vector3(0.035f, 0.085f, -0.05f), new Vector3(0.05f, 0.022f, 0.012f), Prim.Hex("#4CAF50"),
                      rotation: Quaternion.Euler(0f, 0f, 25f));
            b.localScale = Vector3.zero;
            return b;
        }

        /// <summary>Trái tim hồng: 2 quả cầu + 1 khối vuông xoay 45°.</summary>
        private Transform MakeHeart()
        {
            Transform h = Prim.Group(_root, "Heart");
            Color c = Prim.Hex("#FF6F91");
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(-0.034f, 0.025f, 0f), Vector3.one * 0.075f, c, smoothness: 0.6f);
            Prim.Make(PrimitiveType.Sphere, h, new Vector3(0.034f, 0.025f, 0f), Vector3.one * 0.075f, c, smoothness: 0.6f);
            Prim.Make(PrimitiveType.Cube, h, new Vector3(0f, -0.012f, 0f), new Vector3(0.068f, 0.068f, 0.05f), c, smoothness: 0.6f,
                      rotation: Quaternion.Euler(0f, 0f, 45f));
            h.localScale = Vector3.zero;
            return h;
        }

        /// <summary>Chữ Z ghép từ 3 thanh.</summary>
        private Transform MakeZ()
        {
            Transform z = Prim.Group(_root, "Z");
            Color c = Prim.Hex("#EAF2FF");
            Prim.Make(PrimitiveType.Cube, z, new Vector3(0f, 0.05f, 0f), new Vector3(0.11f, 0.022f, 0.02f), c);
            Prim.Make(PrimitiveType.Cube, z, new Vector3(0f, -0.05f, 0f), new Vector3(0.11f, 0.022f, 0.02f), c);
            Prim.Make(PrimitiveType.Cube, z, Vector3.zero, new Vector3(0.13f, 0.022f, 0.02f), c, rotation: Quaternion.Euler(0f, 0f, 42f));
            return z;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
