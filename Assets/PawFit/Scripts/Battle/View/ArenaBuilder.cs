using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Battle.View
{
    /// <summary>Dựng sân bằng code: đấu trường (trận 1-1) hoặc cảnh 5 vùng Thám hiểm (dùng chung cho màn đấu + màn Săn quái).</summary>
    public static class ArenaBuilder
    {
        // màu sân theo vùng: sân ngoài, sân trong, nền đất, trời / vách
        public static readonly string[][] ThemeColors =
        {
            new[] { "#B9DB8A", "#A7CF77", "#8CC36A", "#CDEBF7" },   // 0 Đồi Cỏ Gió
            new[] { "#7D9A5E", "#6E8C52", "#4F6B3E", "#8E7BB5" },   // 1 Rừng Nấm
            new[] { "#CFE0E2", "#BCD3D7", "#9FC3C9", "#DDE8EE" },   // 2 Hồ Sương
            new[] { "#6B5751", "#5C4A45", "#4A3B37", "#E9967A" },   // 3 Núi Lửa
            new[] { "#5A4E7D", "#4C416B", "#3E3557", "#2B2547" },   // 4 Hang Pha Lê
        };

        /// <summary>
        /// Sân: theme -1 = đấu trường (cát + hàng rào gỗ); 0..4 = vùng Thám hiểm, mỗi vùng màu + đồ vật riêng.
        /// `lane` = Săn quái (Phase 9.3): nền là dải đường dài thay cho sân tròn, quái đi vào từ 2 bên.
        /// </summary>
        public static void Build(Transform transform, int theme, bool lane = false)
        {
            bool region = theme >= 0 && theme < ThemeColors.Length;
            string[] c = region ? ThemeColors[theme] : new[] { "#E3C88F", "#D9B877", "#8CC36A", "#BFE0F2" };
            if (lane)   // dải đường dài cho Săn quái
            {
                Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, -0.06f, 0f), new Vector3(19f, 0.05f, 3.4f), Prim.Hex(c[0]), smoothness: 0.05f);
                Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, -0.03f, 0f), new Vector3(18f, 0.03f, 2.4f), Prim.Hex(c[1]), smoothness: 0.05f);
            }
            else
            {
                Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.05f, 0f), new Vector3(6.5f, 0.05f, 4f), Prim.Hex(c[0]), smoothness: 0.05f);
                Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.02f, 0f), new Vector3(5.6f, 0.03f, 3.3f), Prim.Hex(c[1]), smoothness: 0.05f);
            }
            Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, -0.3f, 6f), new Vector3(40f, 0.5f, 20f), Prim.Hex(c[2]), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Cube, transform, new Vector3(0f, 3f, 7f), new Vector3(40f, 8f, 0.2f), Prim.Hex(c[3]), smoothness: 0f);
            if (!region)
            {
                for (int i = -4; i <= 4; i++)   // hàng rào gỗ phía sau
                {
                    Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(i * 1.2f, 0.45f, 2.6f), new Vector3(0.12f, 0.45f, 0.12f), Prim.Hex("#B9865A"));
                    if (i < 4) Prim.Make(PrimitiveType.Cube, transform, new Vector3(i * 1.2f + 0.6f, 0.65f, 2.6f), new Vector3(1.2f, 0.08f, 0.06f), Prim.Hex("#7A5236"));
                }
                return;
            }
            var rng = new System.Random(17 + theme);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            switch (theme)
            {
                case 0:   // đồi cỏ: 3 quả đồi xa + bụi cây + hoa
                    for (int i = 0; i < 3; i++)
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(-7f + i * 7f, -0.5f, 6f), new Vector3(9f, 4.5f, 4f),
                                  Prim.Hex(i == 1 ? "#7FBF5A" : "#93CC6B"), smoothness: 0.05f);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = R(-6f, 6f);
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(x, 0.2f, R(2.6f, 4f)), Vector3.one * R(0.5f, 0.9f), Prim.Hex("#5FA84F"), smoothness: 0.1f);
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(R(-5f, 5f), 0.05f, R(1.8f, 3f)), Vector3.one * 0.14f,
                                  Prim.Hex(i % 2 == 0 ? "#F7C6D9" : "#FFF1A8"));
                    }
                    break;
                case 1:   // rừng nấm: thân cây tối + nấm đỏ chấm trắng
                    for (int i = 0; i < 6; i++)
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(-7.5f + i * 3f, 2f, R(4.5f, 6f)), new Vector3(0.6f, 2.5f, 0.6f),
                                  Prim.Hex("#5B4636"));
                    for (int i = 0; i < 6; i++)
                    {
                        float x = (i < 3 ? -1f : 1f) * R(3.4f, 6f), z = R(2.4f, 4f), h = R(0.5f, 1.1f);
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(x, h * 0.5f, z), new Vector3(0.18f, h * 0.5f, 0.18f), Prim.Hex("#F3E6C4"));
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(x, h, z), new Vector3(0.9f, 0.45f, 0.9f) * (0.6f + h * 0.5f),
                                  Prim.Hex(i % 2 == 0 ? "#D9443B" : "#B04BC0"), smoothness: 0.4f);
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(x + 0.15f, h + 0.18f, z - 0.25f), Vector3.one * 0.12f, Color.white);
                    }
                    break;
                case 2:   // hồ sương: mặt hồ phía sau, cỏ lau, sương mờ
                    Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(0f, -0.04f, 5f), new Vector3(18f, 0.02f, 5f), Prim.Hex("#7FB7D6"), smoothness: 0.9f);
                    for (int i = 0; i < 12; i++)
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3(R(-7f, 7f), 0.45f, R(2.4f, 3f)), new Vector3(0.04f, R(0.35f, 0.6f), 0.04f),
                                  Prim.Hex("#8FA86E"));
                    for (int i = 0; i < 5; i++)
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(-8f + i * 4f, R(0.6f, 1.2f), R(5.5f, 6.5f)), new Vector3(4f, 1.2f, 1.5f),
                                  Prim.Hex("#EEF3F6"), smoothness: 0f);
                    break;
                case 3:   // núi lửa: núi xa đỉnh lửa, vũng dung nham, đá
                    Prim.Make(PrimitiveType.Sphere, transform, new Vector3(2f, 1f, 6.5f), new Vector3(10f, 7f, 3f), Prim.Hex("#4A3B37"), smoothness: 0.05f);
                    Prim.Make(PrimitiveType.Sphere, transform, new Vector3(2f, 4.3f, 6.2f), new Vector3(2.2f, 0.8f, 1f), Prim.Hex("#FF7A2F"), smoothness: 0.9f);
                    for (int i = 0; i < 4; i++)
                        Prim.Make(PrimitiveType.Cylinder, transform, new Vector3((i % 2 == 0 ? -1f : 1f) * R(3.8f, 6f), -0.01f, R(2f, 3.6f)),
                                  new Vector3(R(0.9f, 1.6f), 0.01f, R(0.6f, 1f)), Prim.Hex("#FF8C2E"), smoothness: 0.9f);
                    for (int i = 0; i < 6; i++)
                        Prim.Make(PrimitiveType.Sphere, transform, new Vector3(R(-6.5f, 6.5f), 0.15f, R(2.6f, 4.2f)), new Vector3(R(0.5f, 1f), R(0.4f, 0.7f), R(0.5f, 0.9f)),
                                  Prim.Hex("#3A2E2B"), smoothness: 0.1f);
                    break;
                default:   // hang pha lê: tinh thể xanh / tím lấp lánh, nhũ đá
                    for (int i = 0; i < 9; i++)
                    {
                        float x = (i % 2 == 0 ? -1f : 1f) * R(3.2f, 7f), h = R(0.8f, 2f);
                        Prim.Make(PrimitiveType.Cube, transform, new Vector3(x, h * 0.45f, R(2.4f, 4.5f)), new Vector3(0.35f, h, 0.35f),
                                  Prim.Hex(i % 3 == 0 ? "#7FE3F0" : i % 3 == 1 ? "#B58CF2" : "#E3A6F5"), smoothness: 0.95f,
                                  rotation: Quaternion.Euler(R(-12f, 12f), R(0f, 90f), R(-15f, 15f)));
                    }
                    for (int i = 0; i < 6; i++)
                        Prim.Make(PrimitiveType.Capsule, transform, new Vector3(-7.5f + i * 3f, 6f, R(5f, 6.5f)), new Vector3(0.5f, R(1f, 1.8f), 0.5f),
                                  Prim.Hex("#463C66"));
                    break;
            }
        }
    }
}
