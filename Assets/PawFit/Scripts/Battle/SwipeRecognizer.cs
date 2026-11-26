using System;
using System.Collections.Generic;

namespace PawFit.Battle
{
    /// <summary>
    /// Các kiểu vuốt khi đấu (GDD G9): ngang = chiêu hệ 1 (đánh nhanh), lên = chiêu hệ 2 (đánh mạnh / hất tung),
    /// V = chiêu riêng, Z = Ultimate, xuống = đỡ (đỡ ngay trước đòn = phản đòn). None = vuốt sai.
    /// </summary>
    public enum SwipeKind { None = 0, Horizontal = 1, Up = 2, Down = 3, V = 4, Z = 5 }

    /// <summary>1 điểm của nét vuốt: tọa độ màn hình (pixel, gốc dưới trái, y hướng lên) + thời điểm (giây).</summary>
    public struct SwipePoint
    {
        public float X, Y;
        public double T;

        public SwipePoint(float x, float y, double t)
        {
            X = x;
            Y = y;
            T = t;
        }
    }

    /// <summary>Kết quả nhận dạng 1 nét vuốt.</summary>
    public struct SwipeResult
    {
        public SwipeKind Kind;

        /// <summary>Hệ số vuốt 0.7..1.3 (nhanh + đúng hình thì cao) — nhân vào damage.</summary>
        public float Quality;

        /// <summary>Vuốt ngang: +1 sang phải, -1 sang trái.</summary>
        public int Direction;

        /// <summary>Vì sao không nhận ra (để hiện gợi ý ở Mode Huấn luyện).</summary>
        public string Reason;

        public bool IsValid => Kind != SwipeKind.None;

        public static SwipeResult Wrong(string reason) => new SwipeResult { Kind = SwipeKind.None, Quality = 0f, Reason = reason };
    }

    public sealed class SwipeConfig
    {
        /// <summary>Nét ngắn hơn bấy nhiêu pixel (ở 160 dpi) là chạm nhầm, không tính.</summary>
        public float MinLengthPx = 60f;

        /// <summary>Vuốt lâu hơn bấy nhiêu giây là quá chậm (vuốt sai).</summary>
        public float MaxDurationSeconds = 1.2f;

        /// <summary>Độ thẳng tối thiểu (khoảng cách đầu-cuối / quãng đường) để là vuốt thẳng.</summary>
        public float StraightMin = 0.86f;

        /// <summary>Tốc độ (pixel/giây ở 160 dpi) cho điểm tốc độ thấp nhất / cao nhất.</summary>
        public float SlowPxPerSec = 300f;
        public float FastPxPerSec = 1800f;

        /// <summary>Hệ số vuốt thấp nhất / cao nhất (G9: 0.7–1.3).</summary>
        public float MinQuality = 0.7f;
        public float MaxQuality = 1.3f;
    }

    /// <summary>
    /// Nhận dạng nét vuốt (C# thuần): thẳng (ngang / lên / xuống), V (xuống rồi lên), Z (ngang - chéo ngược - ngang).
    /// Làm gọn nét bằng Ramer–Douglas–Peucker rồi xét hướng từng đoạn. Không phụ thuộc Unity.
    /// </summary>
    public sealed class SwipeRecognizer
    {
        private readonly SwipeConfig _cfg;
        private readonly float _dpiScale;

        public SwipeRecognizer(SwipeConfig cfg = null, float dpiScale = 1f)
        {
            _cfg = cfg ?? new SwipeConfig();
            _dpiScale = dpiScale > 0f ? dpiScale : 1f;
        }

        public SwipeResult Recognize(IReadOnlyList<SwipePoint> pts)
        {
            if (pts == null || pts.Count < 2) return SwipeResult.Wrong("quá ngắn");
            double duration = pts[pts.Count - 1].T - pts[0].T;
            if (duration > _cfg.MaxDurationSeconds) return SwipeResult.Wrong("vuốt chậm quá");
            float length = 0f;
            float minX = pts[0].X, maxX = pts[0].X, minY = pts[0].Y, maxY = pts[0].Y;
            for (int i = 1; i < pts.Count; i++)
            {
                length += Dist(pts[i - 1], pts[i]);
                minX = Math.Min(minX, pts[i].X);
                maxX = Math.Max(maxX, pts[i].X);
                minY = Math.Min(minY, pts[i].Y);
                maxY = Math.Max(maxY, pts[i].Y);
            }
            if (length < _cfg.MinLengthPx * _dpiScale) return SwipeResult.Wrong("quá ngắn");
            float speed = (float)(length / Math.Max(duration, 1e-3)) / _dpiScale;
            float speedScore = Clamp01((speed - _cfg.SlowPxPerSec) / (_cfg.FastPxPerSec - _cfg.SlowPxPerSec));

            SwipePoint a = pts[0], b = pts[pts.Count - 1];
            float straightness = Dist(a, b) / length;
            if (straightness >= _cfg.StraightMin)
            {
                float dx = b.X - a.X, dy = b.Y - a.Y;
                float fidelity = Clamp01((straightness - _cfg.StraightMin) / (1f - _cfg.StraightMin));
                var r = new SwipeResult { Quality = Quality(speedScore, fidelity) };
                if (Math.Abs(dx) >= Math.Abs(dy))
                {
                    r.Kind = SwipeKind.Horizontal;
                    r.Direction = dx >= 0 ? 1 : -1;
                }
                else r.Kind = dy > 0 ? SwipeKind.Up : SwipeKind.Down;
                return r;
            }

            // nét gãy: làm gọn còn vài đỉnh rồi xét hình
            float size = Math.Max(maxX - minX, maxY - minY);
            List<SwipePoint> poly = Simplify(pts, size * 0.12f);
            if (poly.Count == 3)
            {
                Seg s1 = new Seg(poly[0], poly[1]), s2 = new Seg(poly[1], poly[2]);
                bool down = s1.Dy < 0 && Math.Abs(s1.Dy) >= 0.4f * s1.Len;
                bool up = s2.Dy > 0 && Math.Abs(s2.Dy) >= 0.4f * s2.Len;
                if (down && up)
                {
                    float sym = Math.Min(s1.Len, s2.Len) / Math.Max(s1.Len, s2.Len);   // 2 cạnh V dài gần bằng nhau
                    return new SwipeResult { Kind = SwipeKind.V, Quality = Quality(speedScore, sym) };
                }
            }
            if (poly.Count == 4)
            {
                Seg s1 = new Seg(poly[0], poly[1]), s2 = new Seg(poly[1], poly[2]), s3 = new Seg(poly[2], poly[3]);
                bool flat1 = Math.Abs(s1.Dx) > 1.5f * Math.Abs(s1.Dy);
                bool flat3 = Math.Abs(s3.Dx) > 1.5f * Math.Abs(s3.Dy);
                bool diag = s2.Dy < 0 && Math.Abs(s2.Dy) >= 0.35f * s2.Len && Math.Sign(s2.Dx) == -Math.Sign(s1.Dx);
                if (flat1 && flat3 && diag && Math.Sign(s1.Dx) == Math.Sign(s3.Dx))
                {
                    float flatness = Clamp01(1f - (Math.Abs(s1.Dy) / s1.Len + Math.Abs(s3.Dy) / s3.Len));
                    return new SwipeResult { Kind = SwipeKind.Z, Quality = Quality(speedScore, flatness), Direction = Math.Sign(s1.Dx) };
                }
            }
            return SwipeResult.Wrong("không rõ hình");
        }

        private float Quality(float speedScore, float fidelity)
        {
            float k = Clamp01(0.6f * speedScore + 0.4f * Clamp01(fidelity));
            return _cfg.MinQuality + (_cfg.MaxQuality - _cfg.MinQuality) * k;
        }

        private struct Seg
        {
            public readonly float Dx, Dy, Len;

            public Seg(SwipePoint a, SwipePoint b)
            {
                Dx = b.X - a.X;
                Dy = b.Y - a.Y;
                Len = Math.Max(1e-3f, (float)Math.Sqrt(Dx * Dx + Dy * Dy));
            }
        }

        /// <summary>Ramer–Douglas–Peucker: giữ lại các đỉnh gãy của nét.</summary>
        private static List<SwipePoint> Simplify(IReadOnlyList<SwipePoint> pts, float epsilon)
        {
            var keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
            var stack = new Stack<(int, int)>();
            stack.Push((0, pts.Count - 1));
            while (stack.Count > 0)
            {
                (int i0, int i1) = stack.Pop();
                float best = 0f;
                int idx = -1;
                for (int i = i0 + 1; i < i1; i++)
                {
                    float d = LineDist(pts[i], pts[i0], pts[i1]);
                    if (d > best)
                    {
                        best = d;
                        idx = i;
                    }
                }
                if (idx >= 0 && best > epsilon)
                {
                    keep[idx] = true;
                    stack.Push((i0, idx));
                    stack.Push((idx, i1));
                }
            }
            var outPts = new List<SwipePoint>();
            for (int i = 0; i < pts.Count; i++)
                if (keep[i]) outPts.Add(pts[i]);
            return outPts;
        }

        private static float LineDist(SwipePoint p, SwipePoint a, SwipePoint b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float len2 = dx * dx + dy * dy;
            if (len2 < 1e-6f) return Dist(p, a);
            float t = Clamp01(((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2);
            float qx = a.X + dx * t - p.X, qy = a.Y + dy * t - p.Y;
            return (float)Math.Sqrt(qx * qx + qy * qy);
        }

        private static float Dist(SwipePoint a, SwipePoint b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
