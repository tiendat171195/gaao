using PawFit.Core.Unity;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>Đánh dấu khay đồ ăn ở chòi ăn: kéo từ đây ra = lấy đồ ăn.</summary>
    public sealed class FoodSourceView : MonoBehaviour
    {
    }

    /// <summary>Hình nộm ở sân tập: chạm vào = vào sân tập (Huấn luyện / Đấu Bot). Level 0 = Dễ, 1 = Thường, 2 = Khó.</summary>
    public sealed class TrainingDummyView : MonoBehaviour
    {
        public int Level;
    }

    /// <summary>Quầy hàng (Phase 5.1): chạm vào = mở quầy (mua thuốc / bánh / nước, xem quảng cáo lấy thưởng).</summary>
    public sealed class ShopStallView : MonoBehaviour
    {
    }

    /// <summary>Cổng đấu trường phía sau sân tập (PvP, GDD): chạm vào = thẻ rank / tìm trận.</summary>
    public sealed class ArenaGateView : MonoBehaviour
    {
    }

    /// <summary>Hòm thư cạnh cổng đấu trường (GDD: "cài đặt = hòm thư ở cổng"): chạm vào = mở cài đặt.</summary>
    public sealed class MailboxView : MonoBehaviour
    {
    }

    /// <summary>Biển chỉ đường Thám hiểm ở góc sau bên trái vườn (Phase 7.2): chạm vào = mở bản đồ Thám hiểm.</summary>
    public sealed class AdventureSignView : MonoBehaviour
    {
    }

    /// <summary>Đồ vật nảy nhẹ khi được chạm (quầy hàng, hình nộm): co xuống rồi bật lên, về đúng cỡ sau 0,35 giây.</summary>
    public sealed class TapBounce : MonoBehaviour
    {
        private const float Seconds = 0.35f;
        private Vector3 _rest;
        private float _t = -1f;

        /// <summary>Cho `target` nảy (tự gắn TapBounce nếu chưa có).</summary>
        public static void Play(Component target)
        {
            if (target == null) return;
            TapBounce b = target.GetComponent<TapBounce>();
            if (b == null) b = target.gameObject.AddComponent<TapBounce>();
            b.Bounce();
        }

        public void Bounce()
        {
            if (_t < 0f) _rest = transform.localScale;   // đang nảy dở thì giữ cỡ gốc cũ
            _t = 0f;
        }

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.deltaTime / Seconds;
            float k = Mathf.Clamp01(_t);
            float wave = -0.1f * Mathf.Sin(k * Mathf.PI * 2f) * (1f - k);   // âm = co xuống, dương = vươn lên
            transform.localScale = Vector3.Scale(_rest, new Vector3(1f - wave * 0.5f, 1f + wave, 1f - wave * 0.5f));
            if (k < 1f) return;
            transform.localScale = _rest;
            _t = -1f;
        }
    }

    public sealed class GardenBuildResult
    {
        public Transform Root;
        public Transform Walkable;    // mặt đất (đi được)
        public Transform Obstacles;   // vật cản (NavMesh đánh dấu không đi được)
        public FoodSourceView FoodSource;
    }

    /// <summary>
    /// Dựng toàn bộ vườn bằng code từ GardenLayout (không cần scene / prefab vẽ tay).
    /// Đồ trang trí không có collider; chỉ mặt đất + vật cản có collider để NavMesh và chạm trúng.
    /// </summary>
    public static class GardenBuilder
    {
        private static readonly Color Grass = Prim.Hex("#8CC36A");
        private static readonly Color GrassFar = Prim.Hex("#7AAE58");
        private static readonly Color Wood = Prim.Hex("#B9865A");
        private static readonly Color WoodDark = Prim.Hex("#7A5236");
        private static readonly Color Stone = Prim.Hex("#A9A9A0");

        public static GardenBuildResult Build(GardenLayout layout, Transform parent)
        {
            var r = new GardenBuildResult { Root = Prim.Group(parent, "Garden") };
            r.Walkable = Prim.Group(r.Root, "Walkable");
            r.Obstacles = Prim.Group(r.Root, "Obstacles");
            Transform decor = Prim.Group(r.Root, "Decor");
            BuildGround(layout, r.Walkable, decor);
            BuildFence(layout, r.Obstacles);
            BuildBed(layout.Get(ZoneType.Bed), decor);
            r.FoodSource = BuildFoodHut(layout.Get(ZoneType.FoodHut), r.Obstacles, decor);
            BuildPond(layout.Get(ZoneType.Pond), decor);
            BuildTraining(layout.Get(ZoneType.TrainingGround), r.Obstacles, decor);
            BuildMemorial(layout.Get(ZoneType.Memorial), r.Obstacles, decor);
            BuildShop(layout.Get(ZoneType.Shop), r.Obstacles);
            BuildAdventure(layout, r.Obstacles, decor);
            BuildTrees(r.Obstacles, decor);
            BuildFlowers(layout, decor);
            return r;
        }

        private static Vector3 W(NVec2 p, float y = 0f) => p.ToWorld(y);

        private static void BuildGround(GardenLayout l, Transform walk, Transform decor)
        {
            NVec2 size = l.Max - l.Min, c = (l.Min + l.Max) * 0.5f;
            Prim.Make(PrimitiveType.Cube, walk, new Vector3(c.X, -0.1f, c.Y), new Vector3(size.X, 0.2f, size.Y), Grass,
                      keepCollider: true, smoothness: 0.05f, name: "Ground");
            Prim.Make(PrimitiveType.Cube, decor, new Vector3(c.X, -0.22f, c.Y), new Vector3(90f, 0.1f, 100f), GrassFar,
                      smoothness: 0.02f, name: "FarGround");
            // đường đá nhỏ từ chỗ thú đứng tới các khu
            foreach (ZoneType z in new[] { ZoneType.FoodHut, ZoneType.Pond, ZoneType.TrainingGround, ZoneType.Shop })
            {
                GardenZone zone = l.Get(z);
                if (zone == null) continue;
                NVec2 a = l.PetSpawn, b = zone.UsePoint;
                for (int i = 1; i < 8; i++)
                {
                    NVec2 p = NVec2.Lerp(a, b, i / 8f) + new NVec2((i % 2 == 0 ? 0.18f : -0.18f), 0f);
                    if (l.ZoneAt(p) != ZoneType.None) continue;
                    Prim.Make(PrimitiveType.Cylinder, decor, W(p, 0.01f), new Vector3(0.45f, 0.01f, 0.38f), Prim.Hex("#D8D2C4"), smoothness: 0.1f);
                }
            }
        }

        /// <summary>Khe hở ở hàng rào trái (giữa 2 cột) cho lối mòn Thám hiểm đi ra ngoài vườn.</summary>
        private const float GateZ0 = 9f, GateZ1 = 11f;

        private static void BuildFence(GardenLayout l, Transform obstacles)
        {
            Transform f = Prim.Group(obstacles, "Fence");
            float x0 = l.Min.X, x1 = l.Max.X, z0 = l.Min.Y, z1 = l.Max.Y;
            for (float x = x0; x <= x1 + 0.01f; x += 2f)
            {
                Prim.Make(PrimitiveType.Cylinder, f, new Vector3(x, 0.4f, z0), new Vector3(0.14f, 0.4f, 0.14f), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Cylinder, f, new Vector3(x, 0.4f, z1), new Vector3(0.14f, 0.4f, 0.14f), Wood, keepCollider: true);
            }
            for (float z = z0 + 2f; z < z1; z += 2f)
            {
                Prim.Make(PrimitiveType.Cylinder, f, new Vector3(x0, 0.4f, z), new Vector3(0.14f, 0.4f, 0.14f), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Cylinder, f, new Vector3(x1, 0.4f, z), new Vector3(0.14f, 0.4f, 0.14f), Wood, keepCollider: true);
            }
            foreach (float y in new[] { 0.3f, 0.6f })
            {
                Prim.Make(PrimitiveType.Cube, f, new Vector3((x0 + x1) / 2, y, z0), new Vector3(x1 - x0, 0.07f, 0.06f), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Cube, f, new Vector3((x0 + x1) / 2, y, z1), new Vector3(x1 - x0, 0.07f, 0.06f), Wood, keepCollider: true);
                // hàng rào trái chừa khe GateZ0..GateZ1 cho lối mòn Thám hiểm
                Prim.Make(PrimitiveType.Cube, f, new Vector3(x0, y, (z0 + GateZ0) / 2), new Vector3(0.06f, 0.07f, GateZ0 - z0), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Cube, f, new Vector3(x0, y, (GateZ1 + z1) / 2), new Vector3(0.06f, 0.07f, z1 - GateZ1), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Cube, f, new Vector3(x1, y, (z0 + z1) / 2), new Vector3(0.06f, 0.07f, z1 - z0), Wood, keepCollider: true);
            }
            foreach (float z in new[] { GateZ0, GateZ1 })   // 2 cột cổng cao hơn, có đèn
            {
                Prim.Make(PrimitiveType.Cylinder, f, new Vector3(x0, 0.75f, z), new Vector3(0.2f, 0.75f, 0.2f), WoodDark, keepCollider: true);
                Prim.Make(PrimitiveType.Sphere, f, new Vector3(x0, 1.6f, z), Vector3.one * 0.22f, Prim.Hex("#FFD27A"), smoothness: 0.8f);
            }
        }

        /// <summary>
        /// Lối Thám hiểm: biển chỉ đường gỗ (chạm = bản đồ), ba lô + cuộn bản đồ dưới chân biển, đường đá dẫn ra khe hở
        /// hàng rào trái, rồi đường đất đi xa ra ngoài vườn (gợi cảm giác "còn cả thế giới bên ngoài").
        /// </summary>
        private static void BuildAdventure(GardenLayout l, Transform obstacles, Transform decor)
        {
            GardenZone z = l.Get(ZoneType.Adventure);
            if (z == null) return;
            Transform s = Prim.Group(obstacles, "AdventureSign", W(z.Center));
            s.localRotation = Quaternion.Euler(0f, 18f, 0f);   // hơi quay về phía giữa vườn / camera
            s.gameObject.AddComponent<AdventureSignView>();
            var col = s.gameObject.AddComponent<BoxCollider>();   // chạm trúng + vật cản NavMesh
            col.center = new Vector3(0f, 0.9f, 0f);
            col.size = new Vector3(1.5f, 1.8f, 0.7f);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0f, 0.85f, 0f), new Vector3(0.12f, 0.85f, 0.12f), WoodDark);
            Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 1.78f, 0f), Vector3.one * 0.2f, Prim.Hex("#FFD27A"), smoothness: 0.8f);   // đèn đầu cột
            // tấm trên: mũi tên chỉ ra ngoài vườn (sang trái)
            Prim.Make(PrimitiveType.Cube, s, new Vector3(-0.12f, 1.45f, -0.07f), new Vector3(1.3f, 0.36f, 0.06f), Wood);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(-0.78f, 1.45f, -0.07f), new Vector3(0.26f, 0.26f, 0.06f), Wood,
                      rotation: Quaternion.Euler(0f, 0f, 45f));
            MemorialView.Text3D(s, new Vector3(-0.1f, 1.45f, -0.11f), "THÁM HIỂM", 0.17f, Prim.Hex("#4A2E1A"), FontStyle.Bold);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0.1f, 1.04f, -0.07f), new Vector3(0.95f, 0.26f, 0.06f), Prim.Hex("#A8774D"));
            MemorialView.Text3D(s, new Vector3(0.1f, 1.04f, -0.11f), "5 vùng · 25 màn", 0.11f, Prim.Hex("#4A2E1A"), FontStyle.Normal);
            // ba lô + cuộn bản đồ dưới chân biển
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0.42f, 0.2f, -0.1f), new Vector3(0.32f, 0.38f, 0.2f), Prim.Hex("#C0573E"));
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0.42f, 0.27f, -0.21f), new Vector3(0.22f, 0.14f, 0.04f), Prim.Hex("#9E4430"));
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(-0.4f, 0.07f, -0.15f), new Vector3(0.12f, 0.22f, 0.12f), Prim.Hex("#F3E6C4"),
                      rotation: Quaternion.Euler(0f, 30f, 90f));
            // đường đá từ biển ra khe hở hàng rào
            var gate = new NVec2(l.Min.X, (GateZ0 + GateZ1) * 0.5f);
            for (int i = 1; i <= 6; i++)
            {
                NVec2 p = NVec2.Lerp(z.Center, gate, i / 6.5f) + new NVec2(0f, i % 2 == 0 ? 0.16f : -0.16f);
                Prim.Make(PrimitiveType.Cylinder, decor, W(p, 0.01f), new Vector3(0.42f, 0.01f, 0.36f), Prim.Hex("#D8D2C4"), smoothness: 0.1f);
            }
            // ngoài vườn: đường đất uốn lượn đi xa + bụi cây, tảng đá 2 bên
            Color dirt = Prim.Hex("#C9A46B");
            for (int i = 0; i < 9; i++)
            {
                float x = l.Min.X - 0.8f - i * 1.6f;
                float zz = gate.Y + Mathf.Sin(i * 0.7f) * 1.1f;
                Prim.Make(PrimitiveType.Cube, decor, new Vector3(x, -0.165f, zz), new Vector3(1.8f, 0.02f, 1.1f), dirt, smoothness: 0.05f,
                          rotation: Quaternion.Euler(0f, Mathf.Cos(i * 0.7f) * 25f, 0f));
                if (i % 2 == 0)
                    Prim.Make(PrimitiveType.Sphere, decor, new Vector3(x - 0.4f, 0.05f, zz + 1.4f), new Vector3(0.9f, 0.6f, 0.9f),
                              Prim.Hex(i % 4 == 0 ? "#5FA84F" : "#6FBF5C"), smoothness: 0.1f);
                else
                    Prim.Make(PrimitiveType.Sphere, decor, new Vector3(x + 0.3f, -0.05f, zz - 1.3f), new Vector3(0.6f, 0.35f, 0.5f), Stone, smoothness: 0.15f);
            }
        }

        private static void BuildBed(GardenZone z, Transform decor)
        {
            Transform b = Prim.Group(decor, "Bed", W(z.Center));
            Prim.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 0.06f, 0f), new Vector3(2.5f, 0.06f, 2.5f), Prim.Hex("#E8D9C4"));
            Prim.Make(PrimitiveType.Cylinder, b, new Vector3(0f, 0.13f, 0f), new Vector3(1.9f, 0.05f, 1.9f), Prim.Hex("#F2B8B0"), smoothness: 0.1f);
            Prim.Make(PrimitiveType.Sphere, b, new Vector3(0f, 0.22f, 0.75f), new Vector3(0.9f, 0.26f, 0.5f), Prim.Hex("#FFF4E6"));
        }

        private static FoodSourceView BuildFoodHut(GardenZone z, Transform obstacles, Transform decor)
        {
            Transform hut = Prim.Group(obstacles, "FoodHut", W(z.Center + new NVec2(-0.6f, 0.2f)));
            Prim.Make(PrimitiveType.Cube, hut, new Vector3(0f, 0.85f, 0f), new Vector3(2.2f, 1.7f, 2f), Wood, keepCollider: true);
            Prim.Make(PrimitiveType.Cube, hut, new Vector3(1.11f, 0.6f, 0f), new Vector3(0.04f, 1.1f, 0.7f), WoodDark);   // cửa nhìn ra vườn
            foreach (float s in new[] { 1f, -1f })   // mái chữ A
                Prim.Make(PrimitiveType.Cube, hut, new Vector3(0.62f * s, 2.0f, 0f), new Vector3(1.55f, 0.12f, 2.4f), Prim.Hex("#D9534F"),
                          rotation: Quaternion.Euler(0f, 0f, -32f * s));
            // khay đồ ăn: kéo từ đây ra để cho ăn (collider to hơn hình để dễ chạm,
            // nhưng vẫn cách chỗ thú đứng > nửa collider + bán kính thú để NavMesh không chặn chỗ đứng)
            NVec2 trayPos = NVec2.Lerp(z.UsePoint, z.Center, 0.55f);
            var tray = new GameObject("FoodTray");
            tray.transform.SetParent(obstacles, false);
            tray.transform.position = W(trayPos);
            var col = tray.AddComponent<BoxCollider>();
            col.size = new Vector3(1.05f, 0.8f, 1.05f);
            col.center = new Vector3(0f, 0.35f, 0f);
            Prim.Make(PrimitiveType.Cylinder, tray.transform, new Vector3(0f, 0.12f, 0f), new Vector3(1.0f, 0.06f, 1.0f), WoodDark);
            string[] fruit = { "#E8453C", "#F29A2E", "#7BC043", "#E8453C", "#F2C84B" };
            for (int i = 0; i < fruit.Length; i++)
            {
                float a = i * Mathf.PI * 2f / fruit.Length;
                Prim.Make(PrimitiveType.Sphere, tray.transform, new Vector3(Mathf.Cos(a) * 0.25f, 0.26f, Mathf.Sin(a) * 0.25f),
                          Vector3.one * 0.2f, Prim.Hex(fruit[i]), smoothness: 0.55f);
            }
            return tray.AddComponent<FoodSourceView>();
        }

        private static void BuildPond(GardenZone z, Transform decor)
        {
            Transform p = Prim.Group(decor, "Pond", W(z.Center));
            float d = z.Radius * 2f;
            Prim.Make(PrimitiveType.Cylinder, p, new Vector3(0f, 0.015f, 0f), new Vector3(d, 0.015f, d), Prim.Hex("#5EC1E3"), smoothness: 0.92f);
            Prim.Make(PrimitiveType.Cylinder, p, new Vector3(0f, 0.02f, 0f), new Vector3(d * 0.72f, 0.015f, d * 0.72f), Prim.Hex("#3A9CC9"), smoothness: 0.95f);
            var rng = new System.Random(11);
            for (int i = 0; i < 18; i++)   // đá viền hồ
            {
                float a = i * Mathf.PI * 2f / 18f;
                float s = 0.35f + (float)rng.NextDouble() * 0.25f;
                Prim.Make(PrimitiveType.Sphere, p, new Vector3(Mathf.Cos(a) * z.Radius, 0.05f, Mathf.Sin(a) * z.Radius),
                          new Vector3(s, s * 0.55f, s), Stone, smoothness: 0.15f);
            }
            foreach (Vector3 lp in new[] { new Vector3(0.9f, 0.03f, 0.6f), new Vector3(-0.7f, 0.03f, -0.9f), new Vector3(-1.1f, 0.03f, 0.8f) })
                Prim.Make(PrimitiveType.Cylinder, p, lp, new Vector3(0.45f, 0.01f, 0.45f), Prim.Hex("#4E9A3F"), smoothness: 0.4f);
        }

        private static void BuildTraining(GardenZone z, Transform obstacles, Transform decor)
        {
            Transform t = Prim.Group(decor, "Training", W(z.Center));
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.01f, 0f), new Vector3(z.Radius * 2f, 0.01f, z.Radius * 2f), Prim.Hex("#E3C88F"), smoothness: 0.05f);
            // 3 hình nộm: Easy / Normal / Hard (Phase 3 dùng để chọn cấp Bot)
            string[] colors = { "#7BC67B", "#F2C84B", "#E06A5A" };
            NVec2[] spots = { new NVec2(-2f, 1.3f), new NVec2(0f, 2f), new NVec2(2f, 1.3f) };
            for (int i = 0; i < 3; i++)
            {
                Transform dummy = Prim.Group(obstacles, "Dummy" + i, W(z.Center + spots[i]));
                dummy.gameObject.AddComponent<TrainingDummyView>().Level = i;
                Prim.Make(PrimitiveType.Cylinder, dummy, new Vector3(0f, 0.6f, 0f), new Vector3(0.14f, 0.6f, 0.14f), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Capsule, dummy, new Vector3(0f, 1.25f, 0f), new Vector3(0.55f, 0.42f, 0.55f), Prim.Hex(colors[i]),
                          keepCollider: true);   // giữ collider để chạm trúng hình nộm
                Prim.Make(PrimitiveType.Sphere, dummy, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.38f, Prim.Hex("#E8D9C4"));
            }
            // cổng đấu trường (PvP, Phase 4)
            Transform gate = Prim.Group(obstacles, "ArenaGate", W(z.Center + new NVec2(0f, 3.4f)));
            gate.gameObject.AddComponent<ArenaGateView>();   // 2 cột có collider => chạm trúng cổng
            foreach (float s in new[] { -1.3f, 1.3f })
                Prim.Make(PrimitiveType.Cylinder, gate, new Vector3(s, 1.2f, 0f), new Vector3(0.3f, 1.2f, 0.3f), Prim.Hex("#9AA0A6"), keepCollider: true);
            Prim.Make(PrimitiveType.Cube, gate, new Vector3(0f, 2.45f, 0f), new Vector3(3.2f, 0.3f, 0.4f), Prim.Hex("#7E8389"));
            Prim.Make(PrimitiveType.Cube, gate, new Vector3(0f, 1.95f, 0f), new Vector3(1.6f, 0.5f, 0.05f), Prim.Hex("#C0392B"));
            BuildMailbox(obstacles, z.Center + new NVec2(-2.4f, 3.0f));
        }

        /// <summary>Hòm thư: cột gỗ, thân xanh mái tròn, cờ đỏ, phong thư trắng thò ra ở cửa (mặt cửa nhìn về camera).</summary>
        private static void BuildMailbox(Transform obstacles, NVec2 at)
        {
            Transform m = Prim.Group(obstacles, "Mailbox", W(at));
            m.localRotation = Quaternion.Euler(0f, -15f, 0f);
            m.gameObject.AddComponent<MailboxView>();
            var col = m.gameObject.AddComponent<BoxCollider>();   // vừa để chạm trúng, vừa là vật cản NavMesh
            col.center = new Vector3(0f, 0.75f, 0f);
            col.size = new Vector3(0.8f, 1.5f, 1.0f);
            Color blue = Prim.Hex("#4A90D9");
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0f, 0.5f, 0f), new Vector3(0.12f, 1.0f, 0.12f), WoodDark);
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0f, 1.12f, 0f), new Vector3(0.46f, 0.3f, 0.7f), blue, smoothness: 0.45f);
            Prim.Make(PrimitiveType.Cylinder, m, new Vector3(0f, 1.27f, 0f), new Vector3(0.46f, 0.35f, 0.46f), blue, smoothness: 0.45f,
                      rotation: Quaternion.Euler(90f, 0f, 0f));   // mái tròn (nửa trên của trụ nằm ngang)
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0f, 1.16f, -0.36f), new Vector3(0.42f, 0.36f, 0.02f), Prim.Hex("#3A78BE"));   // cửa
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0f, 1.38f, -0.37f), new Vector3(0.28f, 0.16f, 0.02f), Color.white);   // phong thư
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0.26f, 1.35f, 0.12f), new Vector3(0.03f, 0.34f, 0.03f), WoodDark);   // cán cờ
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0.27f, 1.46f, 0.02f), new Vector3(0.02f, 0.12f, 0.18f), Prim.Hex("#E2463F"));   // cờ
        }

        private static void BuildMemorial(GardenZone z, Transform obstacles, Transform decor)
        {
            Transform m = Prim.Group(obstacles, "Memorial", W(z.Center));
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0f, 0.06f, 0f), new Vector3(1.2f, 0.12f, 0.7f), Stone);
            Prim.Make(PrimitiveType.Cube, m, new Vector3(0f, 0.55f, 0f), new Vector3(0.75f, 0.9f, 0.22f), Prim.Hex("#C2C2BA"),
                      keepCollider: true, rotation: Quaternion.Euler(0f, -25f, 2f));
            string[] flowers = { "#F7C6D9", "#FFFFFF", "#F2C84B", "#F7C6D9", "#B39DDB" };
            for (int i = 0; i < flowers.Length; i++)
                Prim.Make(PrimitiveType.Sphere, decor, W(z.Center + new NVec2(-0.6f + 0.3f * i, -0.55f), 0.08f), Vector3.one * 0.14f, Prim.Hex(flowers[i]));
        }

        /// <summary>Quầy hàng gỗ mái bạt sọc, biển "QUẦY HÀNG" + đồng Coin, trên quầy bày lọ thuốc / bánh / lon nước.</summary>
        private static void BuildShop(GardenZone z, Transform obstacles)
        {
            if (z == null) return;
            Transform s = Prim.Group(obstacles, "ShopStall", W(z.Center));
            s.localRotation = Quaternion.Euler(0f, 20f, 0f);   // mặt quầy nhìn về camera, hơi nghiêng vào giữa vườn
            s.gameObject.AddComponent<ShopStallView>();
            var col = s.gameObject.AddComponent<BoxCollider>();   // 1 hộp: vừa để chạm trúng, vừa là vật cản cho NavMesh
            col.center = new Vector3(0f, 1.3f, 0f);
            col.size = new Vector3(2.4f, 2.6f, 1.3f);
            Color red = Prim.Hex("#E2463F"), cream = Prim.Hex("#FFF4E6");

            // quầy gỗ + mặt quầy + dải màu mặt trước
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0f, 0.45f, 0f), new Vector3(2.0f, 0.9f, 0.8f), Wood);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0f, 0.93f, -0.03f), new Vector3(2.2f, 0.07f, 0.95f), WoodDark);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(0f, 0.5f, -0.41f), new Vector3(1.9f, 0.32f, 0.02f), Prim.Hex("#6FC2B0"));
            foreach (float x in new[] { -1f, 1f })   // 4 cột, cột sau cao hơn để mái nghiêng về trước
            {
                Prim.Make(PrimitiveType.Cylinder, s, new Vector3(x, 1.2f, -0.38f), new Vector3(0.08f, 1.2f, 0.08f), WoodDark);
                Prim.Make(PrimitiveType.Cylinder, s, new Vector3(x, 1.3f, 0.38f), new Vector3(0.08f, 1.3f, 0.08f), WoodDark);
            }
            // mái bạt sọc đỏ trắng (mép trước thấp hơn) + diềm hạt tròn
            for (int i = 0; i < 5; i++)
                Prim.Make(PrimitiveType.Cube, s, new Vector3(-0.92f + i * 0.46f, 2.5f, 0f), new Vector3(0.46f, 0.05f, 1.25f),
                          i % 2 == 0 ? red : cream, rotation: Quaternion.Euler(-14f, 0f, 0f));
            for (int i = 0; i < 9; i++)
                Prim.Make(PrimitiveType.Sphere, s, new Vector3(-1f + i * 0.25f, 2.31f, -0.62f), Vector3.one * 0.12f, i % 2 == 0 ? red : cream);

            // biển hiệu ngửa nhẹ về phía camera (camera nhìn chéo từ trên xuống)
            Transform sign = Prim.Group(s, "Sign", new Vector3(0f, 2.95f, 0.1f));
            sign.localRotation = Quaternion.Euler(25f, 0f, 0f);
            Prim.Make(PrimitiveType.Cube, sign, Vector3.zero, new Vector3(1.8f, 0.42f, 0.06f), cream);
            Prim.Make(PrimitiveType.Cube, sign, new Vector3(0f, 0f, 0.01f), new Vector3(1.9f, 0.5f, 0.04f), WoodDark);
            MemorialView.Text3D(sign, new Vector3(-0.14f, 0f, -0.05f), "QUẦY HÀNG", 0.2f, Prim.Hex("#7A3B2E"), FontStyle.Bold);
            Prim.Make(PrimitiveType.Cylinder, sign, new Vector3(0.68f, 0f, -0.05f), new Vector3(0.28f, 0.02f, 0.28f), Prim.Hex("#F2C84B"),
                      smoothness: 0.85f, rotation: Quaternion.Euler(90f, 0f, 0f));   // đồng Coin
            foreach (float x in new[] { -0.6f, 0.6f })
                Prim.Make(PrimitiveType.Cylinder, s, new Vector3(x, 2.63f, 0.1f), new Vector3(0.04f, 0.11f, 0.04f), WoodDark);

            // hàng bày trên quầy: lọ thuốc (nắp đỏ, chữ thập đỏ), bánh hồng có quả anh đào, lon nước tăng lực
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(-0.62f, 1.1f, -0.1f), new Vector3(0.16f, 0.13f, 0.16f), Color.white, smoothness: 0.6f);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(-0.62f, 1.25f, -0.1f), new Vector3(0.17f, 0.04f, 0.17f), red);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(-0.62f, 1.1f, -0.185f), new Vector3(0.08f, 0.025f, 0.01f), red);
            Prim.Make(PrimitiveType.Cube, s, new Vector3(-0.62f, 1.1f, -0.185f), new Vector3(0.025f, 0.08f, 0.01f), red);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0f, 1.04f, -0.1f), new Vector3(0.3f, 0.07f, 0.3f), Prim.Hex("#F7C6D9"));
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0f, 1.12f, -0.1f), new Vector3(0.31f, 0.015f, 0.31f), Color.white);
            Prim.Make(PrimitiveType.Sphere, s, new Vector3(0f, 1.17f, -0.1f), Vector3.one * 0.08f, red, smoothness: 0.7f);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0.6f, 1.1f, -0.1f), new Vector3(0.12f, 0.13f, 0.12f), Prim.Hex("#B5E655"), smoothness: 0.75f);
            Prim.Make(PrimitiveType.Cylinder, s, new Vector3(0.6f, 1.12f, -0.1f), new Vector3(0.125f, 0.03f, 0.125f), Prim.Hex("#2E86C1"), smoothness: 0.75f);
        }

        private static void BuildTrees(Transform obstacles, Transform decor)
        {
            Vector3[] trees =
            {
                new Vector3(-8.5f, 1.0f, -11f), new Vector3(8.4f, 1.1f, -10.5f), new Vector3(-8.7f, 0.9f, -5.5f),
                new Vector3(9f, 0.9f, 5f), new Vector3(-8.7f, 1.1f, 6.5f), new Vector3(-5f, 1f, 12.7f),
                new Vector3(4.7f, 1.2f, 13f), new Vector3(9f, 1f, 8.6f), new Vector3(-8.8f, 1f, 12.4f),
                new Vector3(4.6f, 0.9f, -11.6f), new Vector3(-4.4f, 1f, -11.8f),
            };
            string[] greens = { "#5FA84F", "#6FBF5C", "#4E9444" };
            for (int i = 0; i < trees.Length; i++)
            {
                Vector3 p = new Vector3(trees[i].x, 0f, trees[i].z);
                float s = trees[i].y;
                Transform t = Prim.Group(obstacles, "Tree" + i, p);
                Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.8f * s, 0f), new Vector3(0.32f, 0.8f, 0.32f) * s, Prim.Hex("#8B5E3C"), keepCollider: true);
                Transform crown = Prim.Group(decor, "Crown" + i, p);
                Prim.Make(PrimitiveType.Sphere, crown, new Vector3(0f, 2.2f * s, 0f), Vector3.one * 2.0f * s, Prim.Hex(greens[i % 3]), smoothness: 0.1f);
                Prim.Make(PrimitiveType.Sphere, crown, new Vector3(0.55f * s, 2.7f * s, 0.2f), Vector3.one * 1.3f * s, Prim.Hex(greens[(i + 1) % 3]), smoothness: 0.1f);
                Prim.Make(PrimitiveType.Sphere, crown, new Vector3(-0.5f * s, 2.55f * s, -0.25f), Vector3.one * 1.2f * s, Prim.Hex(greens[(i + 2) % 3]), smoothness: 0.1f);
            }
        }

        private static void BuildFlowers(GardenLayout l, Transform decor)
        {
            var rng = new System.Random(7);
            string[] colors = { "#F7C6D9", "#FFFFFF", "#F2C84B", "#B39DDB", "#F29A9A" };
            Transform f = Prim.Group(decor, "Flowers");
            for (int i = 0; i < 70; i++)
            {
                var p = new NVec2(Mathf.Lerp(l.Min.X + 0.8f, l.Max.X - 0.8f, (float)rng.NextDouble()),
                                  Mathf.Lerp(l.Min.Y + 0.8f, l.Max.Y - 0.8f, (float)rng.NextDouble()));
                if (l.ZoneAt(p) != ZoneType.None || NVec2.Distance(p, l.PetSpawn) < 2.5f) continue;
                Prim.Make(PrimitiveType.Sphere, f, W(p, 0.07f), Vector3.one * (0.1f + (float)rng.NextDouble() * 0.08f), Prim.Hex(colors[i % colors.Length]));
                Prim.Make(PrimitiveType.Sphere, f, W(p + new NVec2(0.12f, 0.05f), 0.04f), new Vector3(0.14f, 0.05f, 0.1f), Prim.Hex("#5FA84F"));
            }
        }
    }
}
