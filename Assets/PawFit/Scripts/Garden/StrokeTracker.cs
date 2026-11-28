using System;
using System.Numerics;

namespace PawFit.Garden
{
    /// <summary>Kéo bắt đầu trên người thú đang là: chưa rõ / vuốt ve / dắt đi.</summary>
    public enum DragIntent { Undecided = 0, Stroke = 1, Lead = 2 }

    /// <summary>Số liệu phân biệt vuốt ve với dắt đi (tính theo bán kính thú trên màn hình, nên đúng ở mọi cỡ màn hình / zoom).</summary>
    public sealed class StrokeConfig
    {
        /// <summary>Ngón tay ra khỏi bán kính × bấy nhiêu khi chưa rõ ý = dắt đi.</summary>
        public float LeadExit = 1.15f;

        /// <summary>Đã quay đầu (đổi hướng &gt; ~110°) ít nhất 1 lần và đi được ≥ bán kính × bấy nhiêu = vuốt ve (xoa qua lại).</summary>
        public float StrokeAfterReversal = 0.5f;

        /// <summary>Hoặc xoa vòng vòng trên người thú được ≥ bán kính × bấy nhiêu = vuốt ve.</summary>
        public float StrokeByPath = 1.6f;

        /// <summary>Đang vuốt mà kéo xa quá bán kính × bấy nhiêu = chuyển sang dắt đi.</summary>
        public float StrokeToLead = 2.4f;

        /// <summary>Mỗi lần xoa được bán kính × bấy nhiêu quãng đường = 1 lượt vuốt (cộng vui).</summary>
        public float PassLength = 0.8f;

        /// <summary>Đoạn di chuyển ngắn hơn bấy nhiêu pixel thì chưa xét hướng (lọc rung tay).</summary>
        public float MinSegmentPx = 8f;
    }

    /// <summary>
    /// Phân biệt "xoa trên người thú" (vuốt ve) với "kéo thú đi" (dắt) cho 1 lần kéo bắt đầu trên người thú (C# thuần):
    ///  * ngón tay đi ra khỏi người thú khi chưa rõ ý → dắt đi (như trước);
    ///  * ngón tay xoa qua lại (có quay đầu) hoặc xoa vòng vòng trên người thú → vuốt ve; đếm từng lượt vuốt;
    ///  * đang vuốt mà kéo hẳn ra xa → chuyển sang dắt đi.
    /// Tọa độ màn hình (pixel); tâm + bán kính thú trên màn hình truyền vào mỗi lần (thú / camera có thể đang di chuyển).
    /// </summary>
    public sealed class StrokeTracker
    {
        private readonly StrokeConfig _cfg;
        private Vector2 _last, _segStart, _dir;
        private bool _hasDir;
        private float _path, _passPath;
        private int _reversals, _passes;

        public StrokeTracker(StrokeConfig cfg = null) => _cfg = cfg ?? new StrokeConfig();

        public DragIntent Intent { get; private set; }

        /// <summary>Số lần đổi hướng (xoa qua lại) từ lúc bắt đầu kéo.</summary>
        public int Reversals => _reversals;

        public void Begin(Vector2 screen)
        {
            Intent = DragIntent.Undecided;
            _last = _segStart = screen;
            _hasDir = false;
            _path = _passPath = 0f;
            _reversals = _passes = 0;
        }

        /// <summary>Ngón tay tới `screen`; thú đang ở `petCenter`, bán kính `petRadius` (pixel). Trả ý hiện tại.</summary>
        public DragIntent Move(Vector2 screen, Vector2 petCenter, float petRadius)
        {
            float r = Math.Max(1f, petRadius);
            float step = Vector2.Distance(screen, _last);
            _last = screen;
            if (Intent == DragIntent.Lead) return Intent;
            _path += step;
            Vector2 seg = screen - _segStart;
            if (seg.Length() >= _cfg.MinSegmentPx)
            {
                Vector2 d = Vector2.Normalize(seg);
                if (_hasDir && Vector2.Dot(d, _dir) < -0.35f) _reversals++;
                _dir = d;
                _hasDir = true;
                _segStart = screen;
            }
            float dist = Vector2.Distance(screen, petCenter);
            if (Intent == DragIntent.Undecided)
            {
                if (dist > r * _cfg.LeadExit) Intent = DragIntent.Lead;
                else if ((_reversals >= 1 && _path >= r * _cfg.StrokeAfterReversal) || _path >= r * _cfg.StrokeByPath)
                {
                    Intent = DragIntent.Stroke;
                    _passPath = _path;   // quãng đã xoa tính luôn vào lượt vuốt đầu
                }
            }
            else if (Intent == DragIntent.Stroke)
            {
                if (dist > r * _cfg.StrokeToLead)
                {
                    Intent = DragIntent.Lead;
                    return Intent;
                }
                _passPath += step;
            }
            while (Intent == DragIntent.Stroke && _passPath >= r * _cfg.PassLength)
            {
                _passPath -= r * _cfg.PassLength;
                _passes++;
            }
            return Intent;
        }

        /// <summary>Số lượt vuốt mới từ lần lấy trước.</summary>
        public int TakePasses()
        {
            int n = _passes;
            _passes = 0;
            return n;
        }
    }
}
