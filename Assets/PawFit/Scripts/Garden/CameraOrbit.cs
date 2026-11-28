using System;
using System.Numerics;

namespace PawFit.Garden
{
    /// <summary>Số liệu camera vườn (Phase 9.1) — chỉnh ở đây, không phải sửa code.</summary>
    public sealed class CameraOrbitConfig
    {
        /// <summary>Góc mặc định: xoay (độ, 0 = nhìn từ cổng vào), nghiêng (độ so với mặt đất), khoảng cách tới điểm nhìn (m).</summary>
        public float DefaultYaw = 0f;
        public float DefaultPitch = 50f;
        public float DefaultDistance = 12.5f;

        /// <summary>Giới hạn: nghiêng thấp quá thì thấy hết mép vườn, cao quá thì như nhìn bản đồ; gần quá / xa quá.</summary>
        public float MinPitch = 30f;
        public float MaxPitch = 75f;
        public float MinDistance = 6.5f;
        public float MaxDistance = 20f;

        /// <summary>2 ngón kéo dọc hết chiều cao màn hình = đổi góc nghiêng bấy nhiêu độ.</summary>
        public float TiltDegreesPerScreen = 80f;

        /// <summary>Máy tính: giữ chuột phải kéo ngang hết chiều rộng màn hình = xoay bấy nhiêu độ (kéo dọc = đổi góc nghiêng).</summary>
        public float OrbitDegreesPerScreen = 220f;

        /// <summary>Máy tính: mỗi nấc lăn chuột lại gần / ra xa bấy nhiêu phần.</summary>
        public float WheelZoomPerNotch = 0.1f;

        /// <summary>
        /// Ngưỡng nhận cử chỉ 2 ngón: khoảng cách 2 ngón đổi bấy nhiêu phần = chụm / mở (zoom); 2 ngón xoay bấy nhiêu độ = xoay vườn;
        /// 2 ngón cùng đi lên / xuống bấy nhiêu phần chiều cao màn hình = đổi góc nghiêng. Dưới ngưỡng thì chưa làm gì (tránh rung tay).
        /// </summary>
        public float PinchStartRatio = 0.06f;
        public float TwistStartDegrees = 10f;
        public float TiltStartScreen = 0.035f;
    }

    /// <summary>Cử chỉ 2 ngón đang làm gì: chưa rõ / chụm-mở + xoay (như bản đồ) / kéo dọc đổi góc nghiêng.</summary>
    public enum TwoFingerMode { Undecided = 0, PinchTwist = 1, Tilt = 2 }

    /// <summary>
    /// Góc camera vườn (C# thuần, Phase 9.1): xoay quanh điểm nhìn (Yaw), nghiêng (Pitch), khoảng cách (Distance), luôn trong giới hạn.
    /// Nhận cử chỉ: 2 ngón chụm / mở = zoom, 2 ngón xoay = xoay vườn, 2 ngón kéo lên / xuống = đổi góc nghiêng;
    /// máy tính: lăn chuột = zoom, giữ chuột phải kéo = xoay + nghiêng. GardenCamera chỉ đọc 3 số này để đặt camera.
    /// Tọa độ màn hình như Unity: gốc góc dưới trái, Y hướng lên.
    /// </summary>
    public sealed class CameraOrbit
    {
        private readonly CameraOrbitConfig _cfg;
        private Vector2 _startA, _startB, _prevA, _prevB;
        private bool _pinchOn, _twistOn, _twoFinger;

        public CameraOrbit(CameraOrbitConfig cfg = null)
        {
            _cfg = cfg ?? new CameraOrbitConfig();
            Reset();
        }

        public CameraOrbitConfig Config => _cfg;

        /// <summary>Độ, trong [0, 360).</summary>
        public float Yaw { get; private set; }

        public float Pitch { get; private set; }
        public float Distance { get; private set; }

        public TwoFingerMode Mode { get; private set; }

        /// <summary>Đang ở góc mặc định (để hòm thư biết có cần nút "về góc mặc định" không).</summary>
        public bool IsDefault =>
            Math.Abs(DeltaAngle(Yaw, _cfg.DefaultYaw)) < 0.5f && Math.Abs(Pitch - _cfg.DefaultPitch) < 0.5f &&
            Math.Abs(Distance - _cfg.DefaultDistance) < 0.05f;

        public void Reset() => Set(_cfg.DefaultYaw, _cfg.DefaultPitch, _cfg.DefaultDistance);

        /// <summary>Đặt góc (vd góc đã lưu ở cài đặt của máy). Số ngoài giới hạn bị kẹp lại.</summary>
        public void Set(float yaw, float pitch, float distance)
        {
            Yaw = Wrap(yaw);
            Pitch = Clamp(pitch, _cfg.MinPitch, _cfg.MaxPitch);
            Distance = Clamp(distance, _cfg.MinDistance, _cfg.MaxDistance);
        }

        /// <summary>Phóng to (factor &gt; 1: lại gần) / thu nhỏ (factor &lt; 1: ra xa).</summary>
        public void Zoom(float factor)
        {
            if (factor <= 0f || float.IsNaN(factor)) return;
            Distance = Clamp(Distance / factor, _cfg.MinDistance, _cfg.MaxDistance);
        }

        /// <summary>Xoay vườn. Dương = cảnh trên màn hình quay ngược chiều kim đồng hồ (theo 2 ngón xoay ngược chiều kim đồng hồ).</summary>
        public void Rotate(float degrees) => Yaw = Wrap(Yaw + degrees);

        /// <summary>Đổi góc nghiêng (dương = nhìn từ trên cao hơn).</summary>
        public void Tilt(float degrees) => Pitch = Clamp(Pitch + degrees, _cfg.MinPitch, _cfg.MaxPitch);

        /// <summary>Lăn chuột: dương (lăn lên) = lại gần.</summary>
        public void Wheel(float notches) => Zoom(1f + notches * _cfg.WheelZoomPerNotch);

        /// <summary>Giữ chuột phải kéo: ngang = xoay (cảnh chạy theo chuột), dọc = đổi góc nghiêng (kéo lên = nhìn thấp xuống).</summary>
        public void OrbitDrag(Vector2 deltaPx, Vector2 screenPx)
        {
            if (screenPx.X <= 0f || screenPx.Y <= 0f) return;
            Rotate(-deltaPx.X / screenPx.X * _cfg.OrbitDegreesPerScreen);
            Tilt(-deltaPx.Y / screenPx.Y * _cfg.TiltDegreesPerScreen);
        }

        // ------------------------------------------------------------------ 2 ngón

        public bool TwoFingerActive => _twoFinger;

        public void BeginTwoFinger(Vector2 a, Vector2 b)
        {
            _twoFinger = true;
            _startA = _prevA = a;
            _startB = _prevB = b;
            _pinchOn = _twistOn = false;
            Mode = TwoFingerMode.Undecided;
        }

        /// <summary>
        /// 2 ngón di chuyển. Lúc đầu chưa làm gì cho tới khi rõ ý (vượt ngưỡng): 2 ngón cùng đi lên / xuống mà khoảng cách + góc
        /// gần như không đổi = đổi góc nghiêng; còn lại = chụm-mở / xoay (mỗi cái tự bật khi vượt ngưỡng của nó, như bản đồ).
        /// </summary>
        public void MoveTwoFinger(Vector2 a, Vector2 b, float screenHeightPx)
        {
            if (!_twoFinger) BeginTwoFinger(a, b);
            float h = Math.Max(1f, screenHeightPx);
            float d0 = Math.Max(1f, Vector2.Distance(_startA, _startB));
            float ratio = Vector2.Distance(a, b) / d0;
            float twist = DeltaAngle(Angle(_startB - _startA), Angle(b - a));
            float dyA = (a.Y - _startA.Y) / h, dyB = (b.Y - _startB.Y) / h;

            if (Mode == TwoFingerMode.Undecided)
            {
                bool pinch = Math.Abs(ratio - 1f) >= _cfg.PinchStartRatio;
                bool turn = Math.Abs(twist) >= _cfg.TwistStartDegrees;
                bool sameWay = Math.Sign(dyA) == Math.Sign(dyB) && Math.Min(Math.Abs(dyA), Math.Abs(dyB)) >= 0.5f * Math.Max(Math.Abs(dyA), Math.Abs(dyB));
                bool tilt = sameWay && Math.Abs(dyA + dyB) * 0.5f >= _cfg.TiltStartScreen;
                if (tilt && !pinch && !turn) Mode = TwoFingerMode.Tilt;
                else if (pinch || turn) Mode = TwoFingerMode.PinchTwist;
                if (Mode == TwoFingerMode.Undecided) return;
                _prevA = a;   // bắt đầu tính từ đây: không nhảy góc 1 phát bằng cả đoạn dưới ngưỡng
                _prevB = b;
                return;
            }

            if (Mode == TwoFingerMode.Tilt)
            {
                float mid = ((a.Y - _prevA.Y) + (b.Y - _prevB.Y)) * 0.5f;
                Tilt(-mid / h * _cfg.TiltDegreesPerScreen);   // 2 ngón kéo lên = nhìn thấp xuống (thấy xa hơn)
            }
            else
            {
                if (!_pinchOn && Math.Abs(ratio - 1f) >= _cfg.PinchStartRatio) _pinchOn = true;
                if (!_twistOn && Math.Abs(twist) >= _cfg.TwistStartDegrees) _twistOn = true;
                float dPrev = Math.Max(1f, Vector2.Distance(_prevA, _prevB));
                if (_pinchOn) Zoom(Vector2.Distance(a, b) / dPrev);
                if (_twistOn) Rotate(DeltaAngle(Angle(_prevB - _prevA), Angle(b - a)));
            }
            _prevA = a;
            _prevB = b;
        }

        public void EndTwoFinger()
        {
            _twoFinger = false;
            Mode = TwoFingerMode.Undecided;
        }

        // ------------------------------------------------------------------

        private static float Angle(Vector2 v) => (float)(Math.Atan2(v.Y, v.X) * 180.0 / Math.PI);

        /// <summary>Chênh lệch góc từ `from` tới `to`, trong [-180, 180).</summary>
        public static float DeltaAngle(float from, float to)
        {
            float d = (to - from) % 360f;
            if (d >= 180f) d -= 360f;
            if (d < -180f) d += 360f;
            return d;
        }

        private static float Wrap(float deg)
        {
            float w = deg % 360f;
            return w < 0f ? w + 360f : w;
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }
}
