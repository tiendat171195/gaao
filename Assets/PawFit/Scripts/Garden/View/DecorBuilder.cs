using PawFit.Core;
using PawFit.Core.Unity;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Đồ trang trí vườn (Phase 5.2), dựng bằng khối cơ bản ở chỗ cố định của vườn mặc định (tránh các khu, lối đá, cây).
    /// Phần chắc (cột, chân, đế) có collider nằm trong nhóm Obstacles => NavMesh nướng lại thì thú đi vòng qua.
    /// Chỉ là hình: mua món nào do CosmeticsService quyết định.
    /// </summary>
    public static class DecorBuilder
    {
        private static readonly Color Wood = Prim.Hex("#B9865A");
        private static readonly Color WoodDark = Prim.Hex("#7A5236");
        private static readonly Color Cream = Prim.Hex("#FFF4E6");

        /// <summary>Chỗ đặt (tọa độ mặt đất) + góc xoay của từng món. Mặt trước của món nhìn về phía camera.</summary>
        public static NVec2 SpotOf(DecorKind kind)
        {
            switch (kind)
            {
                case DecorKind.Lanterns: return new NVec2(4.9f, 5.6f);     // sau hồ, cạnh sân tập
                case DecorKind.Bench: return new NVec2(5.2f, -2.7f);       // trước hồ
                case DecorKind.Windmill: return new NVec2(-5.6f, 5.6f);    // sau chòi ăn
                case DecorKind.FlowerArch: return new NVec2(-4.6f, -7.4f); // phía trước bên trái
                default: return NVec2.Zero;
            }
        }

        private static float YawOf(DecorKind kind) =>
            kind == DecorKind.Bench ? 25f : kind == DecorKind.Windmill ? -15f : kind == DecorKind.FlowerArch ? -20f : 0f;

        /// <summary>Dựng 1 món. `pop` = vừa mua: món bật lên từ mặt đất.</summary>
        public static Transform Build(DecorKind kind, Transform obstacles, bool pop = false)
        {
            if (kind == DecorKind.None || obstacles == null) return null;
            Transform t = Prim.Group(obstacles, "Decor_" + kind, SpotOf(kind).ToWorld());
            t.localRotation = Quaternion.Euler(0f, YawOf(kind), 0f);
            switch (kind)
            {
                case DecorKind.Lanterns: BuildLanterns(t); break;
                case DecorKind.Bench: BuildBench(t); break;
                case DecorKind.Windmill: BuildWindmill(t); break;
                case DecorKind.FlowerArch: BuildArch(t); break;
            }
            if (pop)
            {
                t.localScale = Vector3.one * 0.01f;   // đặt ngay (Start của PopIn chạy frame sau)
                t.gameObject.AddComponent<PopIn>();
            }
            return t;
        }

        /// <summary>Dây đèn lồng giữa 2 cột: 3 lồng đỏ / cam / vàng đung đưa nhẹ.</summary>
        private static void BuildLanterns(Transform t)
        {
            foreach (float x in new[] { -1.3f, 1.3f })
            {
                Prim.Make(PrimitiveType.Cylinder, t, new Vector3(x, 1.1f, 0f), new Vector3(0.1f, 1.1f, 0.1f), WoodDark, keepCollider: true);
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(x, 2.24f, 0f), Vector3.one * 0.16f, WoodDark);
            }
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 2.12f, 0f), new Vector3(0.025f, 1.3f, 0.025f), WoodDark,
                      rotation: Quaternion.Euler(0f, 0f, 90f));   // dây
            string[] colors = { "#E2463F", "#F29A2E", "#F2C84B" };
            for (int i = 0; i < 3; i++)
            {
                Transform hang = Prim.Group(t, "Lantern" + i, new Vector3(-0.75f + i * 0.75f, 2.12f, 0f));   // điểm treo
                Prim.Make(PrimitiveType.Cylinder, hang, new Vector3(0f, -0.1f, 0f), new Vector3(0.015f, 0.1f, 0.015f), WoodDark);
                Prim.Make(PrimitiveType.Cylinder, hang, new Vector3(0f, -0.22f, 0f), new Vector3(0.2f, 0.025f, 0.2f), WoodDark);
                Prim.Make(PrimitiveType.Sphere, hang, new Vector3(0f, -0.42f, 0f), new Vector3(0.36f, 0.42f, 0.36f), Prim.Hex(colors[i]),
                          smoothness: 0.35f);
                Prim.Make(PrimitiveType.Cylinder, hang, new Vector3(0f, -0.63f, 0f), new Vector3(0.18f, 0.025f, 0.18f), WoodDark);
                Prim.Make(PrimitiveType.Cylinder, hang, new Vector3(0f, -0.72f, 0f), new Vector3(0.03f, 0.07f, 0.03f), Prim.Hex("#F2C84B"));
                DecorMotion m = hang.gameObject.AddComponent<DecorMotion>();
                m.Axis = Vector3.forward;
                m.SwayDegrees = 6f;
                m.SwaySpeed = 1.6f + i * 0.25f;
                m.Phase = i * 1.3f;
            }
        }

        /// <summary>Ghế gỗ có tựa lưng.</summary>
        private static void BuildBench(Transform t)
        {
            Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.45f, 0f), new Vector3(1.6f, 0.08f, 0.45f), Wood, keepCollider: true);
            foreach (float x in new[] { -0.7f, 0.7f })
            foreach (float z in new[] { -0.17f, 0.17f })
                Prim.Make(PrimitiveType.Cube, t, new Vector3(x, 0.22f, z), new Vector3(0.08f, 0.45f, 0.08f), WoodDark);
            foreach (float x in new[] { -0.7f, 0.7f })   // cột tựa
                Prim.Make(PrimitiveType.Cube, t, new Vector3(x, 0.72f, 0.2f), new Vector3(0.07f, 0.55f, 0.06f), WoodDark,
                          rotation: Quaternion.Euler(10f, 0f, 0f));
            foreach (float y in new[] { 0.7f, 0.9f })   // 2 thanh tựa lưng
                Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, y, 0.2f + (y - 0.45f) * 0.18f), new Vector3(1.6f, 0.12f, 0.05f), Wood,
                          rotation: Quaternion.Euler(10f, 0f, 0f));
        }

        /// <summary>Cối xay gió nhỏ: tháp trắng, mái đỏ, cánh quạt quay chậm.</summary>
        private static void BuildWindmill(Transform t)
        {
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 0.9f, 0f), new Vector3(1.1f, 0.9f, 1.1f), Prim.Hex("#F2EEE4"), keepCollider: true);
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 2.4f, 0f), new Vector3(0.85f, 0.6f, 0.85f), Cream);
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 3.03f, 0f), new Vector3(1.0f, 0.05f, 1.0f), WoodDark);
            Prim.Make(PrimitiveType.Sphere, t, new Vector3(0f, 3.15f, 0f), new Vector3(0.95f, 0.75f, 0.95f), Prim.Hex("#C0392B"), smoothness: 0.3f);
            Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 0.32f, -0.54f), new Vector3(0.36f, 0.62f, 0.05f), WoodDark);   // cửa
            Prim.Make(PrimitiveType.Cube, t, new Vector3(0f, 1.35f, -0.54f), new Vector3(0.24f, 0.24f, 0.04f), Prim.Hex("#5EC1E3"), smoothness: 0.8f);
            Prim.Make(PrimitiveType.Cylinder, t, new Vector3(0f, 2.6f, -0.5f), new Vector3(0.22f, 0.08f, 0.22f), WoodDark,
                      rotation: Quaternion.Euler(90f, 0f, 0f));   // trục
            Transform blades = Prim.Group(t, "Blades", new Vector3(0f, 2.6f, -0.6f));
            for (int i = 0; i < 4; i++)
            {
                Transform arm = Prim.Group(blades, "Blade" + i);
                arm.localRotation = Quaternion.Euler(0f, 0f, i * 90f + 20f);
                Prim.Make(PrimitiveType.Cube, arm, new Vector3(0f, 0.72f, 0f), new Vector3(0.05f, 1.4f, 0.04f), WoodDark);
                Prim.Make(PrimitiveType.Cube, arm, new Vector3(0.14f, 0.82f, 0.01f), new Vector3(0.26f, 1.1f, 0.02f), Cream);
            }
            DecorMotion spin = blades.gameObject.AddComponent<DecorMotion>();
            spin.Axis = Vector3.forward;
            spin.SpinDegreesPerSecond = 35f;
        }

        /// <summary>Vòm hoa: 2 cột dây leo, vòm hoa hồng / trắng / vàng / tím, 2 chậu hoa ở chân.</summary>
        private static void BuildArch(Transform t)
        {
            Color vine = Prim.Hex("#5FA84F"), leaf = Prim.Hex("#4E9444"), pot = Prim.Hex("#C9794A");
            string[] flowers = { "#F7C6D9", "#FFFFFF", "#F2C84B", "#B39DDB" };
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                Prim.Make(PrimitiveType.Cylinder, t, new Vector3(x, 1.1f, 0f), new Vector3(0.1f, 1.1f, 0.1f), vine, keepCollider: true);
                Prim.Make(PrimitiveType.Cylinder, t, new Vector3(x, 0.15f, 0f), new Vector3(0.42f, 0.15f, 0.42f), pot);
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(x, 0.36f, 0f), new Vector3(0.38f, 0.2f, 0.38f), leaf);
            }
            const int n = 13;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.PI * i / (n - 1);
                var p = new Vector3(Mathf.Cos(a) * 0.9f, 2.2f + Mathf.Sin(a) * 0.62f, 0f);
                Prim.Make(PrimitiveType.Sphere, t, p + new Vector3(0f, 0f, 0.06f), new Vector3(0.36f, 0.32f, 0.3f), leaf, smoothness: 0.1f);
                Prim.Make(PrimitiveType.Sphere, t, p + new Vector3(0f, 0.02f, -0.1f), Vector3.one * 0.2f, Prim.Hex(flowers[i % flowers.Length]),
                          smoothness: 0.3f);
            }
            for (int i = 0; i < 6; i++)   // hoa leo trên 2 cột
            {
                float x = i % 2 == 0 ? -0.9f : 0.9f;
                Prim.Make(PrimitiveType.Sphere, t, new Vector3(x + (i % 3 - 1) * 0.04f, 0.8f + i * 0.22f, -0.08f), Vector3.one * 0.15f,
                          Prim.Hex(flowers[(i + 1) % flowers.Length]), smoothness: 0.3f);
            }
        }
    }

    /// <summary>Chuyển động nhỏ cho đồ trang trí: quay đều (cánh cối xay) hoặc đung đưa (đèn lồng) quanh trục local.</summary>
    public sealed class DecorMotion : MonoBehaviour
    {
        public Vector3 Axis = Vector3.forward;
        public float SpinDegreesPerSecond;
        public float SwayDegrees;
        public float SwaySpeed = 1.5f;
        public float Phase;
        private Quaternion _rest;
        private float _angle;

        private void Start() => _rest = transform.localRotation;

        private void Update()
        {
            _angle += SpinDegreesPerSecond * Time.deltaTime;
            float sway = SwayDegrees * Mathf.Sin(Time.time * SwaySpeed + Phase);
            transform.localRotation = _rest * Quaternion.AngleAxis(_angle + sway, Axis);
        }
    }

    /// <summary>Đồ vừa mua bật lên: phóng to từ 0, vượt nhẹ rồi về đúng cỡ (<see cref="Seconds"/> giây), xong tự gỡ.</summary>
    public sealed class PopIn : MonoBehaviour
    {
        public const float Seconds = 0.6f;
        private float _t;

        private void Update()
        {
            _t += Time.deltaTime / Seconds;
            float k = Mathf.Clamp01(_t);
            float s = k < 0.7f ? Mathf.Lerp(0.01f, 1.12f, k / 0.7f) : Mathf.Lerp(1.12f, 1f, (k - 0.7f) / 0.3f);
            transform.localScale = Vector3.one * s;
            if (k >= 1f) Destroy(this);
        }
    }
}
