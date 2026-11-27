using System.Numerics;

namespace PawFit.Garden
{
    /// <summary>Ngón tay chạm xuống trúng cái gì.</summary>
    public enum PointerTarget { Ground = 0, Pet = 1, FoodSource = 2, Egg = 3, Memorial = 4, Dummy = 5, Shop = 6, Mailbox = 7 }

    public enum GestureKind { Tap, DragStart, DragMove, DragEnd }

    /// <summary>1 cử chỉ đã nhận diện. Position / StartPosition tính bằng pixel màn hình.</summary>
    public readonly struct GestureEvent
    {
        public readonly GestureKind Kind;
        public readonly PointerTarget Target;
        public readonly Vector2 Position;
        public readonly Vector2 StartPosition;

        public GestureEvent(GestureKind kind, PointerTarget target, Vector2 position, Vector2 startPosition)
        {
            Kind = kind;
            Target = target;
            Position = position;
            StartPosition = startPosition;
        }
    }

    /// <summary>
    /// Phân biệt "chạm" và "kéo" từ dữ liệu con trỏ thô (pixel + thời gian). Không phụ thuộc Unity.
    /// Luật: chạm = nhả nhanh (≤ TapMaxSeconds) và gần như không dịch; dịch quá ngưỡng = kéo.
    /// Mục tiêu (đất / thú / đồ ăn) chốt lúc chạm xuống, kéo đi đâu cũng không đổi.
    /// </summary>
    public sealed class PointerGestureClassifier
    {
        private readonly float _tapMaxSeconds;
        private readonly float _dragThresholdPx;
        private bool _down;
        private bool _dragging;
        private Vector2 _start;
        private double _startTime;
        private PointerTarget _target;

        public PointerGestureClassifier(float tapMaxSeconds, float dragThresholdPx)
        {
            _tapMaxSeconds = tapMaxSeconds;
            _dragThresholdPx = dragThresholdPx;
        }

        public bool IsDown => _down;
        public bool IsDragging => _dragging;
        public PointerTarget Target => _target;

        public void Down(Vector2 position, double time, PointerTarget target)
        {
            _down = true;
            _dragging = false;
            _start = position;
            _startTime = time;
            _target = target;
        }

        /// <summary>Ngón tay di chuyển: trả DragStart (lần đầu vượt ngưỡng) rồi DragMove; chưa vượt ngưỡng thì null.</summary>
        public GestureEvent? Move(Vector2 position, double time)
        {
            if (!_down) return null;
            if (!_dragging)
            {
                if (Vector2.Distance(position, _start) < _dragThresholdPx) return null;
                _dragging = true;
                return new GestureEvent(GestureKind.DragStart, _target, position, _start);
            }
            return new GestureEvent(GestureKind.DragMove, _target, position, _start);
        }

        /// <summary>Nhấc tay: trả DragEnd nếu đang kéo, Tap nếu nhanh + không dịch, ngược lại null (giữ lâu).</summary>
        public GestureEvent? Up(Vector2 position, double time)
        {
            if (!_down) return null;
            _down = false;
            if (_dragging)
            {
                _dragging = false;
                return new GestureEvent(GestureKind.DragEnd, _target, position, _start);
            }
            bool quick = time - _startTime <= _tapMaxSeconds;
            bool still = Vector2.Distance(position, _start) < _dragThresholdPx;
            if (quick && still) return new GestureEvent(GestureKind.Tap, _target, position, _start);
            return null;
        }

        public void Cancel()
        {
            _down = false;
            _dragging = false;
        }
    }
}
