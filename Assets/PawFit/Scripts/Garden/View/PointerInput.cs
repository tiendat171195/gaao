using System;
using UnityEngine;

namespace PawFit.Garden.View
{
    public enum PointerPhase { None, Began, Moved, Ended }

    /// <summary>1 mẫu con trỏ trong frame này (pixel màn hình + thời gian).</summary>
    public struct PointerSample
    {
        public PointerPhase Phase;
        public Vector2 Position;
        public double Time;
    }

    /// <summary>Nguồn con trỏ (1 ngón tay / chuột trái). Bọc interface để sau đổi sang Input System không ảnh hưởng chỗ khác.</summary>
    public interface IPointerInput
    {
        PointerSample Read();
    }

    /// <summary>Input Manager cũ: cảm ứng trên điện thoại, chuột trong Editor.</summary>
    public sealed class LegacyPointerInput : IPointerInput
    {
        private bool _failed;

        public LegacyPointerInput()
        {
            try
            {
                Input.simulateMouseWithTouches = false;   // tránh 1 lần chạm bị tính 2 lần (chạm + chuột giả)
            }
            catch (InvalidOperationException)
            {
                // Input cũ đang tắt: báo lỗi rõ ở Read()
            }
        }

        public PointerSample Read()
        {
            if (_failed) return default;
            try
            {
                var s = new PointerSample { Time = UnityEngine.Time.unscaledTimeAsDouble };
                if (Input.touchCount > 0)
                {
                    Touch t = Input.GetTouch(0);
                    s.Position = t.position;
                    switch (t.phase)
                    {
                        case TouchPhase.Began: s.Phase = PointerPhase.Began; break;
                        case TouchPhase.Ended:
                        case TouchPhase.Canceled: s.Phase = PointerPhase.Ended; break;
                        default: s.Phase = PointerPhase.Moved; break;
                    }
                    return s;
                }
                s.Position = Input.mousePosition;
                if (Input.GetMouseButtonDown(0)) s.Phase = PointerPhase.Began;
                else if (Input.GetMouseButtonUp(0)) s.Phase = PointerPhase.Ended;
                else if (Input.GetMouseButton(0)) s.Phase = PointerPhase.Moved;
                return s;
            }
            catch (InvalidOperationException)
            {
                _failed = true;
                Debug.LogError("[PawFit] Input cũ đang tắt. Chạy menu PawFit ▸ Setup Project (tự đặt Active Input Handling = Both) rồi khởi động lại Unity.");
                return default;
            }
        }
    }
}
