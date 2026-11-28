using System;
using PawFit.Core.Unity;
using UnityEngine;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Điểm đào kho báu hôm nay (Phase 7.3): ụ đất nhỏ có dấu X đỏ + mấy đốm sáng xoay quanh. Chạm vào = bé chạy tới đào.
    /// Có collider để chạm trúng nhưng không nằm trong nhóm vật cản (không chắn đường thú).
    /// </summary>
    public sealed class TreasureSpotView : MonoBehaviour
    {
        private Transform _sparkles;
        private float _t, _popT = -1f;

        /// <summary>Chỉ số điểm đào trong danh sách hôm nay (DailyService.DigSpots).</summary>
        public int Index { get; private set; }

        public static TreasureSpotView Create(Transform parent, Vector3 at, int index)
        {
            var go = new GameObject("Treasure" + index);
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            var v = go.AddComponent<TreasureSpotView>();
            v.Index = index;
            var col = go.AddComponent<SphereCollider>();   // chạm trúng (to hơn ụ đất cho dễ chạm)
            col.center = new Vector3(0f, 0.25f, 0f);
            col.radius = 0.65f;
            Prim.Make(PrimitiveType.Sphere, go.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.95f, 0.28f, 0.8f), Prim.Hex("#9C7048"), smoothness: 0.05f);
            Prim.Make(PrimitiveType.Sphere, go.transform, new Vector3(0.25f, 0.04f, 0.2f), new Vector3(0.3f, 0.12f, 0.26f), Prim.Hex("#7E5A39"), smoothness: 0.05f);
            foreach (float a in new[] { 45f, -45f })   // dấu X đỏ trên ụ đất
                Prim.Make(PrimitiveType.Cube, go.transform, new Vector3(0f, 0.15f, 0f), new Vector3(0.55f, 0.02f, 0.09f), Prim.Hex("#D9443B"),
                          rotation: Quaternion.Euler(0f, a, 0f));
            v._sparkles = Prim.Group(go.transform, "Sparkles", new Vector3(0f, 0.45f, 0f));
            for (int i = 0; i < 4; i++)
            {
                float ang = i * Mathf.PI * 0.5f;
                Prim.Make(PrimitiveType.Sphere, v._sparkles, new Vector3(Mathf.Cos(ang) * 0.42f, (i % 2) * 0.15f, Mathf.Sin(ang) * 0.42f),
                          Vector3.one * 0.09f, Prim.Hex("#FFE27A"), smoothness: 0.9f);
            }
            return v;
        }

        /// <summary>Vừa đào xong: ụ đất bung lên rồi biến mất.</summary>
        public void Pop()
        {
            if (_popT >= 0f) return;
            _popT = 0f;
            Collider c = GetComponent<Collider>();
            if (c != null) c.enabled = false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            _t += dt;
            if (_sparkles != null)
            {
                _sparkles.localRotation = Quaternion.Euler(0f, _t * 90f, 0f);
                _sparkles.localPosition = new Vector3(0f, 0.45f + Mathf.Sin(_t * 3f) * 0.06f, 0f);
            }
            if (_popT < 0f) return;
            _popT += dt / 0.45f;
            float k = Mathf.Clamp01(_popT);
            transform.localScale = Vector3.one * (1f + Mathf.Sin(k * Mathf.PI) * 0.6f) * (1f - k * k);
            if (_popT >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Cờ đỏ của hòm thư (Phase 7.3): dựng lên + dấu "!" nhấp nhô khi có quà / thưởng nhiệm vụ chờ nhận, hạ xuống khi đã nhận hết.
    /// Chỉ hỏi `raised` (DailyService.HasSomethingToClaim) mỗi nửa giây.
    /// </summary>
    public sealed class MailboxFlag : MonoBehaviour
    {
        private Transform _flag;
        private Transform _bang;
        private Func<bool> _raised;
        private float _angle, _check, _t;
        private bool _up;

        public void Init(Transform flagPivot, Func<bool> raised)
        {
            _flag = flagPivot;
            _raised = raised;
            _bang = MemorialView.Text3D(transform, new Vector3(0f, 1.95f, -0.1f), "!", 0.42f, Prim.Hex("#FFD23F"), FontStyle.Bold).transform;
            _up = _raised != null && _raised();
            _angle = _up ? 0f : 80f;
            Apply();
        }

        private void Update()
        {
            if (_flag == null) return;
            float dt = Time.deltaTime;
            _t += dt;
            if ((_check -= dt) <= 0f)
            {
                _check = 0.5f;
                _up = _raised != null && _raised();
            }
            _angle = Mathf.MoveTowards(_angle, _up ? 0f : 80f, dt * 260f);   // dựng / hạ cờ
            Apply();
        }

        private void Apply()
        {
            _flag.localRotation = Quaternion.Euler(_angle, 0f, 0f);
            if (_bang == null) return;
            _bang.gameObject.SetActive(_up);
            _bang.localPosition = new Vector3(0f, 1.95f + Mathf.Abs(Mathf.Sin(_t * 4f)) * 0.12f, -0.1f);
        }
    }
}
