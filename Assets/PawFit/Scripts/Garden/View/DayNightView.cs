using System;
using System.Collections.Generic;
using PawFit.Core.Unity;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Ngày / đêm trong vườn (Phase 10.1) — chỉ là hình + tiếng, luật giờ giấc nằm ở <see cref="DayCycle"/> (C# thuần):
    ///  * nắng đổi theo giờ máy: sáng sớm cam hồng chiếu xiên, trưa trắng ấm, chiều tà vàng cam bóng đổ dài, chạng vạng tím,
    ///    đêm ánh trăng xanh; ánh sáng môi trường + sương + màu nền đổi theo, đêm bớt rực màu một chút;
    ///  * đêm: đom đóm lập lòe quanh hồ / gốc cây / ổ ngủ (thỉnh thoảng 1 con bay tới chỗ bé, bé ngước nhìn),
    ///    sao + trăng in bóng lấp lánh trên mặt hồ, đèn nấm cạnh ổ + đèn lồng chòi ăn tự sáng, dế kêu thay chim, cú mèo xa xa;
    ///  * vào màn đấu / săn quái (Paused, dùng chung đèn + camera): ánh sáng trưa cho dễ nhìn; về vườn thì đúng giờ ngay.
    /// Bảng DEV ép giờ thì trời chuyển mượt trong khoảng 2 giây.
    /// </summary>
    public sealed class DayNightView : MonoBehaviour
    {
        private const int FireflyCount = 16, StarCount = 14;
        private const float FireflySize = 0.06f;

        private sealed class Firefly
        {
            public Transform T;
            public Vector3 Home, Pos;
            public float Phase, Speed, Radius, BlinkRate;
        }

        private sealed class Lamp
        {
            public Renderer[] Parts;
            public Material[] Day, Glow;
            public Light Light;
            public float Intensity;
            public bool Lit;
        }

        private DayCycle _cycle;
        private Light _sun;
        private Camera _cam;
        private ColorAdjustments _color;
        private Func<PetController> _pet;
        private DayLight _shown, _goal, _applied;
        private float _sampleIn, _visitIn = 25f, _visitLeft;
        private int _visitor = -1;
        private bool _paused, _admired;
        private Transform _fliesRoot, _pondRoot, _moon;
        private readonly List<Firefly> _flies = new List<Firefly>();
        private readonly List<Transform> _stars = new List<Transform>();
        private readonly List<float> _starPhase = new List<float>();
        private readonly List<Lamp> _lamps = new List<Lamp>();

        /// <summary>0 = ngày, 1 = đêm hẳn (giá trị đang hiển thị, đã chuyển mượt). Đèn lồng trang trí đọc giá trị này.</summary>
        public float Night => _paused || _shown == null ? 0f : _shown.Night;

        /// <summary>Vào màn đấu / săn quái: ánh sáng trưa (dễ nhìn), tắt đom đóm / đèn; ra lại thì về đúng giờ ngay, không chuyển dần.</summary>
        public bool Paused
        {
            get => _paused;
            set
            {
                if (_paused == value || _cycle == null) return;
                _paused = value;
                _goal = value ? DayCycle.Sample(_cycle.Config.FixedHour, _cycle.Config) : _cycle.Light;
                _shown = _goal.Clone();
                Apply(_shown);
                UpdateNightBits(0f);
            }
        }

        public void Init(DayCycle cycle, Light sun, Camera cam, ColorAdjustments color, GardenLayout layout, GardenBuildResult built,
                         Func<PetController> pet)
        {
            _cycle = cycle ?? throw new ArgumentNullException(nameof(cycle));
            _sun = sun;
            _cam = cam;
            _color = color;
            _pet = pet;
            BuildFireflies(layout);
            BuildPondSky(layout);
            if (built != null)
            {
                if (built.NightLamp != null)   // đèn nấm: mũ hồng ấm + chấm trắng tự sáng, đèn điểm soi ổ ngủ
                {
                    var parts = new List<Renderer>();
                    var glow = new List<Material>();
                    foreach (Transform c in built.NightLamp)
                    {
                        Renderer r = c.GetComponent<Renderer>();
                        if (r == null || c.name == "Stem") continue;
                        parts.Add(r);
                        glow.Add(MaterialFactory.Glow(c.name == "Cap" ? new Color(1.9f, 0.78f, 0.62f) : new Color(1.8f, 1.8f, 1.6f)));
                    }
                    AddLamp(built.NightLamp, parts, glow, new Vector3(0f, 0.65f, 0f), new Color(1f, 0.74f, 0.62f), 3.4f, 1.3f);
                }
                if (built.HutLamp != null)   // đèn lồng chòi ăn: soi khay táo để tối vẫn kéo táo cho bé ăn được
                {
                    Transform core = built.HutLamp.Find("Core");
                    Renderer r = core != null ? core.GetComponent<Renderer>() : null;
                    if (r != null)
                        AddLamp(built.HutLamp, new List<Renderer> { r }, new List<Material> { MaterialFactory.Glow(new Color(2f, 1.5f, 0.7f)) },
                                Vector3.zero, new Color(1f, 0.8f, 0.5f), 4.2f, 1.2f);
                }
            }
            _goal = cycle.Light;
            _shown = _goal.Clone();   // vào vườn: đúng giờ ngay
            Apply(_shown);
            UpdateNightBits(0f);
        }

        private void Update()
        {
            if (_cycle == null) return;
            float dt = Time.deltaTime;
            if (!_paused)
            {
                _sampleIn -= dt;
                if (_sampleIn <= 0f)
                {
                    _sampleIn = 0.5f;
                    _goal = _cycle.Light;
                }
                _shown.MoveToward(_goal, 1f - Mathf.Exp(-dt / 0.7f));
                if (_applied == null || Changed(_applied, _shown)) Apply(_shown);   // trời đứng yên thì không ghi lại mỗi frame
                Sfx.Night(_shown.Night, _shown.Birds);
            }
            UpdateNightBits(dt);
        }

        // ------------------------------------------------------------------ ánh sáng

        private static bool Changed(DayLight a, DayLight b) =>
            Mathf.Abs(a.SunIntensity - b.SunIntensity) > 0.002f || Mathf.Abs(a.SunYaw - b.SunYaw) > 0.05f ||
            Mathf.Abs(a.SunPitch - b.SunPitch) > 0.05f || Mathf.Abs(a.Night - b.Night) > 0.002f ||
            Rgb.Distance(a.Fog, b.Fog) > 0.002f || Rgb.Distance(a.AmbientSky, b.AmbientSky) > 0.002f ||
            Rgb.Distance(a.SunColor, b.SunColor) > 0.002f || Rgb.Distance(a.AmbientEquator, b.AmbientEquator) > 0.002f ||
            Rgb.Distance(a.AmbientGround, b.AmbientGround) > 0.002f;

        private void Apply(DayLight l)
        {
            _applied = l.Clone();
            if (_sun != null)
            {
                _sun.transform.rotation = Quaternion.Euler(l.SunPitch, l.SunYaw, 0f);
                _sun.color = C(l.SunColor);
                _sun.intensity = l.SunIntensity;
                _sun.shadowStrength = l.ShadowStrength;
            }
            RenderSettings.ambientSkyColor = C(l.AmbientSky);
            RenderSettings.ambientEquatorColor = C(l.AmbientEquator);
            RenderSettings.ambientGroundColor = C(l.AmbientGround);
            RenderSettings.fogColor = C(l.Fog);
            if (_cam != null) _cam.backgroundColor = C(l.Fog);
            if (_color != null) _color.saturation.value = Mathf.Lerp(8f, -4f, l.Night);   // đêm: màu dịu như dưới trăng
        }

        private static Color C(Rgb c) => new Color(c.R, c.G, c.B);

        /// <summary>Đom đóm, sao trên hồ, đèn tự sáng: hiện dần theo độ tối (ban ngày ẩn hẳn, không tốn gì).</summary>
        private void UpdateNightBits(float dt)
        {
            float night = Night;
            float fade = Smooth(0.15f, 0.65f, night);
            UpdateFireflies(fade, dt);
            UpdatePondSky(fade);
            float lampFade = Smooth(0.25f, 0.6f, night);
            foreach (Lamp l in _lamps) SetLamp(l, lampFade);
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------ đom đóm

        private void BuildFireflies(GardenLayout layout)
        {
            _fliesRoot = Prim.Group(transform, "Fireflies");
            Material mat = MaterialFactory.Glow(new Color(1.5f, 1.75f, 0.45f));
            var rng = new System.Random(31);
            var homes = new List<Vector3>();
            GardenZone pond = layout.Get(ZoneType.Pond);
            if (pond != null)   // quanh hồ nhiều nhất
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f + 0.3f;
                    homes.Add(pond.Center.ToWorld() + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * (pond.Radius + 0.8f));
                }
            foreach (ZoneType z in new[] { ZoneType.Memorial, ZoneType.Memorial, ZoneType.FoodHut, ZoneType.Bed, ZoneType.Bed,
                                           ZoneType.TrainingGround, ZoneType.TrainingGround, ZoneType.Adventure })
            {
                GardenZone zone = layout.Get(z);
                if (zone != null) homes.Add(zone.Center.ToWorld() + new Vector3(Rand(rng, -2.2f, 2.2f), 0f, Rand(rng, -2.2f, 2.2f)));
            }
            while (homes.Count < FireflyCount)   // còn lại: rải trên bãi cỏ
            {
                var p = new NVec2(Rand(rng, layout.Min.X + 1.5f, layout.Max.X - 1.5f), Rand(rng, layout.Min.Y + 1.5f, layout.Max.Y - 1.5f));
                homes.Add(p.ToWorld());
            }
            for (int i = 0; i < FireflyCount; i++)
            {
                GameObject go = Prim.Make(PrimitiveType.Sphere, _fliesRoot, Vector3.zero, Vector3.one * FireflySize, Color.white, name: "Firefly" + i);
                go.GetComponent<Renderer>().sharedMaterial = mat;
                Vector3 home = homes[i];
                home.y = Rand(rng, 0.35f, 1.5f);
                var f = new Firefly
                {
                    T = go.transform, Home = home, Pos = home, Phase = Rand(rng, 0f, 10f), Speed = Rand(rng, 0.7f, 1.3f),
                    Radius = Rand(rng, 0.6f, 1.4f), BlinkRate = Rand(rng, 1.1f, 2.2f),
                };
                _flies.Add(f);
            }
            _fliesRoot.gameObject.SetActive(false);
        }

        private static float Rand(System.Random rng, float a, float b) => a + (float)rng.NextDouble() * (b - a);

        private void UpdateFireflies(float fade, float dt)
        {
            bool show = fade > 0.01f && !_paused;
            if (_fliesRoot.gameObject.activeSelf != show) _fliesRoot.gameObject.SetActive(show);
            if (!show)
            {
                _visitor = -1;
                return;
            }
            float t = Time.time;
            UpdateVisit(fade, dt);
            for (int i = 0; i < _flies.Count; i++)
            {
                Firefly f = _flies[i];
                Vector3 goal;
                if (i == _visitor && _pet() != null)   // bay tới lượn quanh đầu bé
                {
                    Vector3 head = _pet().transform.position + Vector3.up * 1.05f;
                    goal = head + new Vector3(Mathf.Sin(t * 1.7f) * 0.35f, Mathf.Sin(t * 2.3f) * 0.12f, Mathf.Cos(t * 1.7f) * 0.35f);
                    f.Pos = Vector3.MoveTowards(f.Pos, goal, dt * 1.6f);
                    if (!_admired && Vector3.Distance(f.Pos, head) < 0.6f)
                    {
                        _admired = true;
                        PetController pet = _pet();
                        if (pet.IsFree && UnityEngine.Random.value < 0.5f) pet.Admire(f.Pos);   // không phải lần nào cũng nhảy lên (đêm cho yên)
                    }
                }
                else
                {
                    float s = f.Speed;
                    goal = f.Home + new Vector3(Mathf.Sin(t * 0.31f * s + f.Phase) * f.Radius,
                                                Mathf.Sin(t * 0.7f * s + f.Phase * 2f) * 0.25f,
                                                Mathf.Cos(t * 0.23f * s + f.Phase * 3f) * f.Radius);
                    f.Pos = Vector3.MoveTowards(f.Pos, goal, dt * 1.2f);
                }
                f.T.localPosition = f.Pos;
                // lập lòe: phần lớn thời gian mờ, thỉnh thoảng lóe sáng
                float blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * f.BlinkRate + f.Phase)), 3f);
                f.T.localScale = Vector3.one * (FireflySize * fade * (0.35f + 0.65f * blink));
            }
        }

        /// <summary>Đêm hẳn: cứ 20–40 giây 1 con đom đóm bay tới lượn quanh đầu bé ~8 giây, bé rảnh thì ngước nhìn.</summary>
        private void UpdateVisit(float fade, float dt)
        {
            PetController pet = _pet();
            if (_visitor >= 0)
            {
                _visitLeft -= dt;
                if (_visitLeft <= 0f || pet == null) _visitor = -1;
                return;
            }
            if (pet == null || fade < 0.9f) return;
            _visitIn -= dt;
            if (_visitIn > 0f) return;
            _visitIn = UnityEngine.Random.Range(20f, 40f);
            _visitor = UnityEngine.Random.Range(0, _flies.Count);
            _visitLeft = 8f;
            _admired = false;
        }

        // ------------------------------------------------------------------ sao + trăng in bóng trên mặt hồ

        private void BuildPondSky(GardenLayout layout)
        {
            GardenZone pond = layout.Get(ZoneType.Pond);
            if (pond == null) return;
            _pondRoot = Prim.Group(transform, "PondSky", pond.Center.ToWorld(0.045f));
            Material starMat = MaterialFactory.Glow(new Color(1.5f, 1.6f, 1.9f));
            var lily = new[] { new Vector2(0.9f, 0.6f), new Vector2(-0.7f, -0.9f), new Vector2(-1.1f, 0.8f), new Vector2(0.35f, -0.25f) };
            var rng = new System.Random(53);
            float maxR = pond.Radius * 0.72f - 0.25f;   // trong lòng hồ (phần nước sâu)
            for (int i = 0, tries = 0; i < StarCount && tries < 400; tries++)
            {
                var p = new Vector2(Rand(rng, -maxR, maxR), Rand(rng, -maxR, maxR));
                if (p.magnitude > maxR) continue;
                bool clear = true;
                foreach (Vector2 l in lily)
                    if (Vector2.Distance(p, l) < 0.4f) clear = false;
                if (!clear) continue;
                float size = Rand(rng, 0.04f, 0.08f);
                GameObject s = Prim.Make(PrimitiveType.Quad, _pondRoot, new Vector3(p.x, 0f, p.y), Vector3.one * size, Color.white,
                                         rotation: Quaternion.Euler(90f, Rand(rng, 0f, 90f), 0f), name: "Star" + i);
                s.GetComponent<Renderer>().sharedMaterial = starMat;
                _stars.Add(s.transform);
                _starPhase.Add(Rand(rng, 0f, 10f));
                i++;
            }
            // bóng trăng: đĩa sáng nhạt, gợn nhẹ theo sóng (chỗ đã chừa ở bảng lá súng)
            GameObject moon = Prim.Make(PrimitiveType.Cylinder, _pondRoot, new Vector3(0.35f, 0f, -0.25f), new Vector3(0.42f, 0.002f, 0.42f),
                                        Color.white, name: "Moon");
            moon.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Glow(new Color(1.35f, 1.35f, 1.15f));
            _moon = moon.transform;
            _pondRoot.gameObject.SetActive(false);
        }

        private void UpdatePondSky(float fade)
        {
            if (_pondRoot == null) return;
            bool show = fade > 0.01f && !_paused;
            if (_pondRoot.gameObject.activeSelf != show) _pondRoot.gameObject.SetActive(show);
            if (!show) return;
            float t = Time.time;
            for (int i = 0; i < _stars.Count; i++)
            {
                float tw = 0.55f + 0.45f * Mathf.Sin(t * (2f + i % 3) + _starPhase[i]);   // lấp lánh
                float baseSize = 0.04f + (i % 5) * 0.01f;
                _stars[i].localScale = Vector3.one * (baseSize * fade * tw);
            }
            float ripple = 1f + 0.05f * Mathf.Sin(t * 1.3f);
            _moon.localScale = new Vector3(0.42f * ripple * fade, 0.002f, 0.42f * (2f - ripple) * fade);
        }

        // ------------------------------------------------------------------ đèn tự sáng (nấm cạnh ổ, đèn lồng chòi ăn)

        private void AddLamp(Transform root, List<Renderer> parts, List<Material> glow, Vector3 lightOffset, Color color, float range, float intensity)
        {
            var lamp = new Lamp
            {
                Parts = parts.ToArray(),
                Day = new Material[parts.Count],
                Glow = glow.ToArray(),
                Intensity = intensity,
            };
            for (int i = 0; i < parts.Count; i++) lamp.Day[i] = parts[i].sharedMaterial;
            var lg = new GameObject("Light");
            lg.transform.SetParent(root, false);
            lg.transform.localPosition = lightOffset;
            lamp.Light = lg.AddComponent<Light>();
            lamp.Light.type = LightType.Point;
            lamp.Light.color = color;
            lamp.Light.range = range;
            lamp.Light.shadows = LightShadows.None;
            lamp.Light.intensity = 0f;
            lamp.Light.enabled = false;
            _lamps.Add(lamp);
        }

        /// <summary>Tối dần thì đèn điểm sáng dần; đủ tối thì "bật đèn" (đổi sang vật liệu tự sáng), sáng ra thì tắt.</summary>
        private static void SetLamp(Lamp l, float fade)
        {
            float k = l.Intensity * fade * (0.94f + 0.06f * Mathf.Sin(Time.time * 3.1f));   // ánh đèn hơi lung linh
            l.Light.intensity = k;
            l.Light.enabled = k > 0.02f;
            bool lit = l.Lit ? fade > 0.25f : fade > 0.35f;   // ngưỡng lệch nhau: không nhấp nháy khi ở sát ngưỡng
            if (lit == l.Lit) return;
            l.Lit = lit;
            for (int i = 0; i < l.Parts.Length; i++)
                if (l.Parts[i] != null) l.Parts[i].sharedMaterial = lit ? l.Glow[i] : l.Day[i];
        }
    }
}
