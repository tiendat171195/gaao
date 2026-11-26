using PawFit.Core.Unity;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>Đánh dấu khay đồ ăn ở chòi ăn: kéo từ đây ra = lấy đồ ăn.</summary>
    public sealed class FoodSourceView : MonoBehaviour
    {
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
            foreach (ZoneType z in new[] { ZoneType.FoodHut, ZoneType.Pond, ZoneType.TrainingGround })
            {
                NVec2 a = l.PetSpawn, b = l.Get(z).UsePoint;
                for (int i = 1; i < 8; i++)
                {
                    NVec2 p = NVec2.Lerp(a, b, i / 8f) + new NVec2((i % 2 == 0 ? 0.18f : -0.18f), 0f);
                    if (l.ZoneAt(p) != ZoneType.None) continue;
                    Prim.Make(PrimitiveType.Cylinder, decor, W(p, 0.01f), new Vector3(0.45f, 0.01f, 0.38f), Prim.Hex("#D8D2C4"), smoothness: 0.1f);
                }
            }
        }

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
                Prim.Make(PrimitiveType.Cube, f, new Vector3(x0, y, (z0 + z1) / 2), new Vector3(0.06f, 0.07f, z1 - z0), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Cube, f, new Vector3(x1, y, (z0 + z1) / 2), new Vector3(0.06f, 0.07f, z1 - z0), Wood, keepCollider: true);
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
                Prim.Make(PrimitiveType.Cylinder, dummy, new Vector3(0f, 0.6f, 0f), new Vector3(0.14f, 0.6f, 0.14f), Wood, keepCollider: true);
                Prim.Make(PrimitiveType.Capsule, dummy, new Vector3(0f, 1.25f, 0f), new Vector3(0.55f, 0.42f, 0.55f), Prim.Hex(colors[i]));
                Prim.Make(PrimitiveType.Sphere, dummy, new Vector3(0f, 1.85f, 0f), Vector3.one * 0.38f, Prim.Hex("#E8D9C4"));
            }
            // cổng đấu trường (PvP, Phase 4)
            Transform gate = Prim.Group(obstacles, "ArenaGate", W(z.Center + new NVec2(0f, 3.4f)));
            foreach (float s in new[] { -1.3f, 1.3f })
                Prim.Make(PrimitiveType.Cylinder, gate, new Vector3(s, 1.2f, 0f), new Vector3(0.3f, 1.2f, 0.3f), Prim.Hex("#9AA0A6"), keepCollider: true);
            Prim.Make(PrimitiveType.Cube, gate, new Vector3(0f, 2.45f, 0f), new Vector3(3.2f, 0.3f, 0.4f), Prim.Hex("#7E8389"));
            Prim.Make(PrimitiveType.Cube, gate, new Vector3(0f, 1.95f, 0f), new Vector3(1.6f, 0.5f, 0.05f), Prim.Hex("#C0392B"));
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
