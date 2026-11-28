using System;
using System.Numerics;

namespace PawFit.Battle
{
    /// <summary>Cử chỉ khi Săn quái: chạm = đánh · vuốt lên = đập đất · vuốt xuống = đỡ · vuốt trái / phải = lướt.</summary>
    public enum HuntGestureKind { None = 0, Tap = 1, Up = 2, Down = 3, Left = 4, Right = 5 }

    public sealed class HuntGestureConfig
    {
        /// <summary>Chạm: nhấc tay trong bấy nhiêu giây và dịch chưa tới TapMaxPx (× mật độ điểm ảnh).</summary>
        public float TapMaxSeconds = 0.3f;
        public float TapMaxPx = 22f;

        /// <summary>Vuốt nhanh: dịch đủ FlickMinPx theo 1 hướng trong FlickMaxSeconds là nhận NGAY (không đợi nhấc tay) — đỡ / lướt kịp lúc.</summary>
        public float FlickMinPx = 55f;
        public float FlickMaxSeconds = 0.35f;

        /// <summary>Vuốt chậm: nhấc tay mà đã dịch ≥ SlowMinPx theo 1 hướng thì vẫn nhận.</summary>
        public float SlowMinPx = 70f;

        /// <summary>Hướng chính phải dài hơn hướng phụ bấy nhiêu lần (vuốt chéo quá thì không nhận).</summary>
        public float AxisRatio = 1.4f;
    }

    /// <summary>
    /// Nhận cử chỉ Săn quái từ ngón tay (C# thuần, pixel màn hình như Unity: Y hướng lên). Mỗi lần chạm ra tối đa 1 cử chỉ.
    /// Khác màn đấu 1-1 (vẽ hình chiêu): ở đây chỉ cần chạm + vuốt 4 hướng, nhận ngay khi vừa đủ dài nên phản xạ nhanh.
    /// </summary>
    public sealed class HuntGestures
    {
        private readonly HuntGestureConfig _cfg;
        private readonly float _dpi;
        private bool _down, _used;
        private Vector2 _start;
        private double _startTime;

        public HuntGestures(HuntGestureConfig cfg = null, float dpiScale = 1f)
        {
            _cfg = cfg ?? new HuntGestureConfig();
            _dpi = Math.Max(1f, dpiScale);
        }

        public bool IsDown => _down;

        /// <summary>Chỗ bắt đầu chạm (để biết chạm trúng quái nào).</summary>
        public Vector2 Start => _start;

        public void Down(Vector2 p, double time)
        {
            _down = true;
            _used = false;
            _start = p;
            _startTime = time;
        }

        /// <summary>Ngón tay di chuyển: trả hướng vuốt ngay khi vừa đủ dài + đủ nhanh (1 lần mỗi lần chạm), còn lại None.</summary>
        public HuntGestureKind Move(Vector2 p, double time)
        {
            if (!_down || _used) return HuntGestureKind.None;
            if (time - _startTime > _cfg.FlickMaxSeconds) return HuntGestureKind.None;
            HuntGestureKind k = Direction(p - _start, _cfg.FlickMinPx * _dpi);
            if (k != HuntGestureKind.None) _used = true;
            return k;
        }

        /// <summary>Nhấc tay: chạm nhanh tại chỗ = Tap; vuốt chậm đủ dài = hướng đó; còn lại (giữ lâu, vuốt chéo, đã nhận rồi) = None.</summary>
        public HuntGestureKind Up(Vector2 p, double time)
        {
            if (!_down) return HuntGestureKind.None;
            _down = false;
            if (_used) return HuntGestureKind.None;
            Vector2 d = p - _start;
            if (d.Length() < _cfg.TapMaxPx * _dpi) return time - _startTime <= _cfg.TapMaxSeconds ? HuntGestureKind.Tap : HuntGestureKind.None;
            return Direction(d, _cfg.SlowMinPx * _dpi);
        }

        public void Cancel() => _down = false;

        private HuntGestureKind Direction(Vector2 d, float minLen)
        {
            float ax = Math.Abs(d.X), ay = Math.Abs(d.Y);
            if (Math.Max(ax, ay) < minLen) return HuntGestureKind.None;
            if (ay >= ax * _cfg.AxisRatio) return d.Y > 0f ? HuntGestureKind.Up : HuntGestureKind.Down;
            if (ax >= ay * _cfg.AxisRatio) return d.X > 0f ? HuntGestureKind.Right : HuntGestureKind.Left;
            return HuntGestureKind.None;
        }
    }
}
