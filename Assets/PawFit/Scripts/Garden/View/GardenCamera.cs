using UnityEngine;

namespace PawFit.Garden.View
{
    /// <summary>
    /// Camera vườn: nhìn chéo xuống, đi theo thú. Kéo đất = "nắm" mặt đất kéo đi (điểm dưới ngón tay luôn
    /// dính theo ngón tay). Thả tay ra một lúc thì camera tự quay lại theo thú.
    /// </summary>
    public sealed class GardenCamera : MonoBehaviour
    {
        public float Pitch = 50f;
        public float Distance = 12.5f;
        public float FollowSharpness = 3f;
        public float ResumeDelay = 2.5f;

        private Camera _cam;
        private Transform _follow;
        private GardenLayout _layout;
        private Vector3 _focus;
        private bool _panning;
        private Vector3 _grab;
        private float _resumeAt;

        public void Init(Camera cam, Transform follow, GardenLayout layout)
        {
            _cam = cam;
            _follow = follow;
            _layout = layout;
            _focus = follow != null ? follow.position : Vector3.zero;
            Apply();
        }

        private void LateUpdate()
        {
            if (_cam == null) return;
            if (!_panning && Time.time >= _resumeAt && _follow != null)
                _focus = Vector3.Lerp(_focus, _follow.position, 1f - Mathf.Exp(-FollowSharpness * Time.deltaTime));
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

        private void Apply()
        {
            Quaternion rot = Quaternion.Euler(Pitch, 0f, 0f);
            _cam.transform.SetPositionAndRotation(_focus + Vector3.up * 0.5f + rot * new Vector3(0f, 0f, -Distance), rot);
        }
    }
}
