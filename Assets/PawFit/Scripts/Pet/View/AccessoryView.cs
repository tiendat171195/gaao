using PawFit.Core;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Pet.View
{
    /// <summary>
    /// Phụ kiện trên đầu thú (Phase 5.2), dựng bằng khối cơ bản, gắn vào <see cref="IPetAvatar.HeadAnchor"/>
    /// nên xoay / gật / lớn lên cùng đầu. Mọi kích thước tính theo bán kính đầu (r = 1), chạy được cho cả thú Gen 1 lẫn thú tạm.
    /// Chỉ là hình: đeo món nào do CosmeticsService quyết định.
    /// </summary>
    public static class AccessoryView
    {
        public const string NodeName = "PF_Accessory";

        /// <summary>Gắn phụ kiện lên đầu thú, gỡ món cũ nếu có. None = chỉ gỡ. Trả về gốc của phụ kiện (null nếu không gắn).</summary>
        public static Transform Attach(IPetAvatar avatar, AccessoryKind kind)
        {
            Transform head = avatar?.HeadAnchor;
            if (head == null) return null;
            Transform old = head.Find(NodeName);
            if (old != null)
            {
                old.name = NodeName + "_Old";   // Destroy chạy cuối frame: đổi tên để lần tìm sau không trúng món cũ
                Object.Destroy(old.gameObject);
            }
            if (kind == AccessoryKind.None) return null;
            Transform root = Prim.Group(head, NodeName);
            root.localRotation = Quaternion.identity;
            root.localScale = Vector3.one * Mathf.Max(0.01f, avatar.HeadRadius);
            switch (kind)
            {
                case AccessoryKind.Bow: BuildBow(root); break;
                case AccessoryKind.Beanie: BuildBeanie(root); break;
                case AccessoryKind.Scarf: BuildScarf(root); break;
                case AccessoryKind.Crown: BuildCrown(root); break;
            }
            return root;
        }

        /// <summary>Nơ hồng đỏ cài lệch 1 bên đỉnh đầu.</summary>
        private static void BuildBow(Transform root)
        {
            Color ribbon = Prim.Hex("#E8456B"), knot = Prim.Hex("#C7304F");
            Transform bow = Prim.Group(root, "Bow", new Vector3(0.52f, 0.86f, 0.1f));
            bow.localRotation = Quaternion.Euler(0f, 0f, -30f);
            bow.localScale = Vector3.one * 1.25f;
            Prim.Make(PrimitiveType.Sphere, bow, new Vector3(-0.25f, 0.02f, 0f), new Vector3(0.38f, 0.27f, 0.15f), ribbon, smoothness: 0.55f,
                      rotation: Quaternion.Euler(0f, 0f, 15f));
            Prim.Make(PrimitiveType.Sphere, bow, new Vector3(0.25f, 0.02f, 0f), new Vector3(0.38f, 0.27f, 0.15f), ribbon, smoothness: 0.55f,
                      rotation: Quaternion.Euler(0f, 0f, -15f));
            Prim.Make(PrimitiveType.Sphere, bow, Vector3.zero, new Vector3(0.2f, 0.2f, 0.19f), knot, smoothness: 0.55f);
            Prim.Make(PrimitiveType.Cube, bow, new Vector3(-0.08f, -0.17f, 0f), new Vector3(0.08f, 0.2f, 0.04f), ribbon,
                      rotation: Quaternion.Euler(0f, 0f, 18f));
            Prim.Make(PrimitiveType.Cube, bow, new Vector3(0.08f, -0.17f, 0f), new Vector3(0.08f, 0.2f, 0.04f), ribbon,
                      rotation: Quaternion.Euler(0f, 0f, -18f));
        }

        /// <summary>Mũ len xanh có viền gập + quả bông trắng. Mép mũ nằm trên mắt.</summary>
        private static void BuildBeanie(Transform root)
        {
            Color knit = Prim.Hex("#4A90D9"), rim = Prim.Hex("#3A78BE"), pom = Prim.Hex("#F5F0E6");
            Prim.Make(PrimitiveType.Sphere, root, new Vector3(0f, 0.6f, -0.06f), new Vector3(2.06f, 1.45f, 2.06f), knit, smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cylinder, root, new Vector3(0f, 0.4f, -0.04f), new Vector3(2.1f, 0.07f, 2.1f), rim, smoothness: 0.05f);
            Prim.Make(PrimitiveType.Sphere, root, new Vector3(0f, 1.38f, -0.1f), Vector3.one * 0.44f, pom, smoothness: 0.02f);
        }

        /// <summary>Khăn quàng sọc đỏ kem quanh cổ (dưới cằm), có 1 đuôi khăn thả trước ngực.</summary>
        private static void BuildScarf(Transform root)
        {
            Color red = Prim.Hex("#D9483B"), cream = Prim.Hex("#FFF4E6");
            Transform ring = Prim.Group(root, "Scarf", new Vector3(0f, -0.86f, -0.3f));
            ring.localRotation = Quaternion.Euler(18f, 0f, 0f);   // trước thấp hơn sau, ôm theo cổ
            const int n = 14;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2f / n;
                Prim.Make(PrimitiveType.Sphere, ring, new Vector3(Mathf.Cos(a) * 0.62f, 0f, Mathf.Sin(a) * 0.62f), new Vector3(0.44f, 0.38f, 0.44f),
                          (i / 2) % 2 == 0 ? red : cream, smoothness: 0.05f);
            }
            Prim.Make(PrimitiveType.Sphere, ring, new Vector3(0.32f, -0.3f, 0.6f), new Vector3(0.34f, 0.42f, 0.2f), red, smoothness: 0.05f);
            Prim.Make(PrimitiveType.Sphere, ring, new Vector3(0.38f, -0.62f, 0.62f), new Vector3(0.3f, 0.36f, 0.18f), cream, smoothness: 0.05f);
        }

        /// <summary>Vương miện vàng: vòng 12 mảnh + 6 chóp hình thoi có hạt ngọc, đội hơi lệch.</summary>
        private static void BuildCrown(Transform root)
        {
            Color gold = Prim.Hex("#F2C84B"), ruby = Prim.Hex("#E2463F"), sapphire = Prim.Hex("#3D7BD9");
            Transform crown = Prim.Group(root, "Crown", new Vector3(0f, 0.9f, 0f));
            crown.localRotation = Quaternion.Euler(-6f, 0f, 10f);
            const int n = 12;
            for (int i = 0; i < n; i++)
            {
                float deg = i * 360f / n, a = deg * Mathf.Deg2Rad;
                var pos = new Vector3(Mathf.Sin(a) * 0.46f, 0f, Mathf.Cos(a) * 0.46f);
                Prim.Make(PrimitiveType.Cube, crown, pos, new Vector3(0.26f, 0.2f, 0.06f), gold, smoothness: 0.85f,
                          rotation: Quaternion.Euler(0f, deg, 0f));
                if (i % 2 != 0) continue;   // 6 chóp
                Prim.Make(PrimitiveType.Cube, crown, pos + Vector3.up * 0.16f, new Vector3(0.13f, 0.13f, 0.05f), gold, smoothness: 0.85f,
                          rotation: Quaternion.Euler(0f, deg, 45f));
                Prim.Make(PrimitiveType.Sphere, crown, pos + Vector3.up * 0.3f, Vector3.one * 0.08f, i % 4 == 0 ? ruby : sapphire,
                          smoothness: 0.9f);
            }
            Prim.Make(PrimitiveType.Sphere, crown, new Vector3(0f, 0f, 0.49f), new Vector3(0.13f, 0.13f, 0.07f), ruby, smoothness: 0.9f);
        }
    }
}
