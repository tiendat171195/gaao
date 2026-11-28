using System;
using PawFit.Core.Unity;
using UnityEngine;
using NVec2 = System.Numerics.Vector2;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Camera vườn: nhìn chéo xuống, đi theo thú. Kéo đất = "nắm" mặt đất kéo đi (điểm dưới ngón tay luôn
    /// dính theo ngón tay). Thả tay ra một lúc thì camera tự quay lại theo thú.
    /// Phase 9.1: 2 ngón chụm / mở = zoom, 2 ngón xoay = xoay vườn, 2 ngón kéo lên / xuống = đổi góc nghiêng;
    /// máy tính: lăn chuột = zoom, giữ chuột phải kéo = xoay + nghiêng. Góc tính ở <see cref="CameraOrbit"/> (C# thuần).
    /// </summary>
    public sealed class GardenCamera : MonoBehaviour
    {
        public float FollowSharpness = 3f;
        public float ResumeDelay = 2.5f;

        /// <summary>Độ mượt khi đổi góc (lớn = bám tay sát hơn).</summary>
        public float ViewSharpness = 14f;

        private Camera _cam;
        private Transform _follow;
        private GardenLayout _layout;
        private Vector3 _focus;
        private bool _panning;
        private Vector3 _grab;
        private float _resumeAt;
        private CameraOrbit _orbit = new CameraOrbit();
        private float _yaw, _pitch, _distance;
        private bool _orbitDirty;

        /// <summary>Góc camera vừa đổi xong (thả tay / thôi lăn chuột): GardenScene lưu vào cài đặt của máy.</summary>
        public Action<CameraOrbit> ViewChanged { get; set; }

        public CameraOrbit Orbit => _orbit;

        public void Init(Camera cam, Transform follow, GardenLayout layout, CameraOrbit orbit = null)
        {
            _cam = cam;
            _follow = follow;
            _layout = layout;
            if (orbit != null) _orbit = orbit;
            _focus = follow != null ? follow.position : Vector3.zero;
            SnapView();
            Apply();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (!_panning && Time.time >= _resumeAt && _follow != null)
                _focus = Vector3.Lerp(_focus, _follow.position, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
            float k = 1f - Mathf.Exp(-ViewSharpness * Time.deltaTime);
            _yaw = Mathf.LerpAngle(_yaw, _orbit.Yaw, k);
            _pitch = Mathf.Lerp(_pitch, _orbit.Pitch, k);
            _distance = Mathf.Lerp(_distance, _orbit.Distance, k);
            Apply();
        }

        public void BeginPan(Vector2 screen)
        {
            _panning = GroundPoint(screen, out _grab);
        }

        public void PanTo(Vector2 screen)
        {
            if (!_panning || !GroundPoint(screen, out Vector3 p)) return;
            _focus = Clamp(_focus + (_grab - p));
            Apply();   // sau khi dời, điểm _grab lại nằm dưới ngón tay
        }

        public void EndPan()
        {
            _panning = false;
            _resumeAt = Time.time + ResumeDelay;
        }

        /// <summary>Có lệnh mới cho thú: camera theo thú ngay.</summary>
        public void ResumeFollow() => _resumeAt = 0f;

        /// <summary>Đổi thứ camera đi theo (thú, trứng, khu tưởng niệm...).</summary>
        public void SetFollow(Transform follow)
        {
            _follow = follow;
            _panning = false;
            ResumeFollow();
        }

        // ------------------------------------------------------------------ zoom / xoay / đổi góc (Phase 9.1)

        /// <summary>Lăn chuột (máy tính).</summary>
        public void Wheel(float notches)
        {
            if (Mathf.Abs(notches) < 0.01f) return;
            _orbit.Wheel(notches);
            _orbitDirty = true;
        }

        /// <summary>Giữ chuột phải kéo (máy tính).</summary>
        public void OrbitDrag(Vector2 deltaPx)
        {
            if (deltaPx.sqrMagnitude < 0.01f) return;
            _orbit.OrbitDrag(new NVec2(deltaPx.x, deltaPx.y), new NVec2(Screen.width, Screen.height));
            _orbitDirty = true;
        }

        public void BeginTwoFinger(Vector2 a, Vector2 b)
        {
            if (_panning) EndPan();
            _orbit.BeginTwoFinger(a.ToNumerics(), b.ToNumerics());
        }

        public void MoveTwoFinger(Vector2 a, Vector2 b)
        {
            _orbit.MoveTwoFinger(a.ToNumerics(), b.ToNumerics(), Screen.height);
            if (_orbit.Mode != TwoFingerMode.Undecided) _orbitDirty = true;
        }

        public void EndTwoFinger()
        {
            if (!_orbit.TwoFingerActive) return;
            _orbit.EndTwoFinger();
            CommitView();
        }

        /// <summary>Thôi chỉnh (thả chuột phải / hết lăn chuột): báo lưu góc mới.</summary>
        public void CommitView()
        {
            if (!_orbitDirty) return;
            _orbitDirty = false;
            ViewChanged?.Invoke(_orbit);
        }

        /// <summary>Về góc mặc định (hòm thư).</summary>
        public void ResetView()
        {
            _orbit.Reset();
            _orbitDirty = true;
            CommitView();
        }

        /// <summary>Điểm trên mặt đất (y = 0) nằm dưới vị trí màn hình.</summary>
        public bool GroundPoint(Vector2 screen, out Vector3 point)
        {
            Ray ray = _cam.ScreenPointToRay(screen);
            if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float d))
            {
                point = ray.GetPoint(d);
                return true;
            }
            point = default;
            return false;
        }

        private Vector3 Clamp(Vector3 f)
        {
            if (_layout == null) return f;
            f.x = Mathf.Clamp(f.x, _layout.Min.X + 2f, _layout.Max.X - 2f);
            f.z = Mathf.Clamp(f.z, _layout.Min.Y + 1f, _layout.Max.Y - 3f);
            f.y = 0f;
            return f;
        }

        private void SnapView()
        {
            _yaw = _orbit.Yaw;
            _pitch = _orbit.Pitch;
            _distance = _orbit.Distance;
        }

        private void Apply()
        {
            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _cam.transform.SetPositionAndRotation(_focus + Vector3.up * 0.5f + rot * new Vector3(0f, 0f, -_distance), rot);
        }
    }
}
