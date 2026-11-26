using System.Collections.Generic;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Pet.View
{
    /// <summary>
    /// Hiệu ứng "nói bằng hình" quanh thú (không có chữ / thanh số):
    ///   ruồi bay quanh khi bẩn · bong bóng có quả táo khi đòi ăn · chữ Z bay lên khi ngủ · tiếng kêu / ngáp.
    /// Dựng bằng khối cơ bản + âm thanh tạo bằng code, không cần asset.
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

        private static AudioClip _chirp, _yawn;

        private IPetAvatar _avatar;
        private Transform _root;
        private Camera _cam;
        private AudioSource _audio;
        private readonly List<Fly> _flies = new List<Fly>();
        private readonly List<Glyph> _zs = new List<Glyph>();
        private Transform _bubble;
        private float _bubbleT = -1f;
        private bool _sleeping;
        private float _zTimer;

        public void Init(IPetAvatar avatar)
        {
            _avatar = avatar;
            if (_root == null) _root = new GameObject("PetFx").transform;   // không làm con của thú: thú xoay thì hiệu ứng không xoay theo
            if (_audio == null)
            {
                _audio = gameObject.AddComponent<AudioSource>();
                _audio.playOnAwake = false;
                _audio.spatialBlend = 0f;
                _audio.volume = 0.55f;
            }
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

        /// <summary>Đòi ăn: bong bóng có quả táo trên đầu + tiếng kêu.</summary>
        public void Cry()
        {
            if (_root == null) return;
            if (_bubble == null) _bubble = MakeFoodBubble();
            _bubbleT = 0f;
            Play(ref _chirp, "pawfit_chirp", MakeChirp);
        }

        public void Yawn() => Play(ref _yawn, "pawfit_yawn", MakeYawn);

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

        // ------------------------------------------------------------------
        // Âm thanh tạo bằng code (không cần file âm thanh)
        // ------------------------------------------------------------------

        private void Play(ref AudioClip clip, string name, System.Func<string, AudioClip> make)
        {
            if (_audio == null) return;
            if (clip == null) clip = make(name);
            _audio.pitch = 0.94f + Random.value * 0.12f;
            _audio.PlayOneShot(clip);
        }

        /// <summary>Tiếng "mi-meo" ngắn: 2 nốt cao dần.</summary>
        private static AudioClip MakeChirp(string name) =>
            Synth(name, 0.42f, t =>
            {
                float note = t < 0.17f ? t / 0.17f : (t - 0.2f) / 0.22f;
                bool second = t >= 0.2f;
                if (t >= 0.17f && t < 0.2f) return 0f;
                float f0 = second ? 820f : 640f, f1 = second ? 1180f : 900f;
                float f = Mathf.Lerp(f0, f1, Mathf.Clamp01(note)) * (1f + 0.02f * Mathf.Sin(t * 70f));
                float env = Mathf.Sin(Mathf.Clamp01(note) * Mathf.PI);
                return env * 0.5f * (Mathf.Sin(Phase(f, t)) + 0.25f * Mathf.Sin(2f * Phase(f, t)));
            });

        /// <summary>Tiếng ngáp: trầm dần, dài hơn.</summary>
        private static AudioClip MakeYawn(string name) =>
            Synth(name, 0.85f, t =>
            {
                float u = t / 0.85f;
                float f = Mathf.Lerp(520f, 240f, u);
                float env = Mathf.Sin(u * Mathf.PI) * (1f - 0.3f * u);
                return env * 0.4f * (Mathf.Sin(Phase(f, t)) + 0.35f * Mathf.Sin(3f * Phase(f, t)));
            });

        private static float Phase(float freq, float t) => 2f * Mathf.PI * freq * t;

        private static AudioClip Synth(string name, float seconds, System.Func<float, float> sample)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(sample(i / (float)rate), -1f, 1f);
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
