using System.Collections.Generic;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Core.Unity
{
    /// <summary>Đổi tọa độ logic (Vector2 mặt đất X, Y) <-> Unity (Vector3 x, 0, z).</summary>
    public static class VecExt
    {
        public static Vector3 ToWorld(this NVec2 p, float y = 0f) => new Vector3(p.X, y, p.Y);
        public static NVec2 ToGround(this Vector3 v) => new NVec2(v.x, v.z);
        public static NVec2 ToNumerics(this Vector2 v) => new NVec2(v.x, v.y);
        public static Vector2 ToUnity(this NVec2 v) => new Vector2(v.X, v.Y);
    }

    /// <summary>
    /// Tạo vật liệu URP Lit lúc chạy. Dùng chung 1 material gốc "Resources/PawFit/LitBase" (menu Setup tạo)
    /// để khi build, shader URP Lit chắc chắn được đóng gói.
    /// </summary>
    public static class MaterialFactory
    {
        private static Material _base;
        private static readonly Dictionary<long, Material> Cache = new Dictionary<long, Material>();

        public static Material Lit(Color color, float smoothness = 0.2f, float metallic = 0f)
        {
            Color32 c = color;
            long key = ((long)c.r << 40) | ((long)c.g << 32) | ((long)c.b << 24) | ((long)c.a << 16)
                       | ((long)Mathf.RoundToInt(smoothness * 100f) << 8) | (long)Mathf.RoundToInt(metallic * 100f);
            if (Cache.TryGetValue(key, out Material m) && m != null) return m;
            m = new Material(Base) { color = color };
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            Cache[key] = m;
            return m;
        }

        private static Material Base
        {
            get
            {
                if (_base != null) return _base;
                _base = Resources.Load<Material>("PawFit/LitBase");
                if (_base == null)
                {
                    Shader sh = Shader.Find("Universal Render Pipeline/Lit");
                    if (sh == null) sh = Shader.Find("Standard");
                    _base = new Material(sh);
                    Debug.LogWarning("[PawFit] Chưa có Resources/PawFit/LitBase — chạy menu PawFit ▸ Setup Project (cần cho bản build).");
                }
                return _base;
            }
        }
    }

    /// <summary>Tạo khối cơ bản có màu (thú tạm, đồ vật trong vườn dựng bằng code).</summary>
    public static class Prim
    {
        public static GameObject Make(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color,
                                      bool keepCollider = false, float smoothness = 0.2f, Quaternion? rotation = null,
                                      string name = null)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (name != null) go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(color, smoothness);
            // xóa ngay (không đợi cuối frame) để NavMesh không gom nhầm collider trang trí
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;

        public static Transform Group(Transform parent, string name, Vector3 localPos = default)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            return t;
        }
    }
}
