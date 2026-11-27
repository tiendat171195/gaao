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

    /// <summary>
    /// Vì sao vuốt không ra chiêu. TooShort / TooSlow: màn đấu chỉ nhắc, không phạt (chạm nhầm, kéo chậm);
    /// Unclear (vẽ nguệch ngoạc / vòng tròn): tính là vuốt sai, bị hở.
    /// </summary>
    public enum SwipeFail { None = 0, TooShort = 1, TooSlow = 2, Unclear = 3 }

    /// <summary>Kết quả nhận dạng 1 nét vuốt.</summary>
    public struct SwipeResult
    {
        public SwipeKind Kind;

        /// <summary>Hệ số vuốt 0.7..1.3 (nhanh + đúng hình thì cao) — nhân vào damage.</summary>
        public float Quality;

        /// <summary>Vuốt ngang: +1 sang phải, -1 sang trái.</summary>
        public int Direction;

        /// <summary>Vì sao không nhận ra (để hiện gợi ý).</summary>
        public string Reason;

        public SwipeFail Fail;

        public bool IsValid => Kind != SwipeKind.None;

        public static SwipeResult Wrong(string reason, SwipeFail fail = SwipeFail.Unclear) =>
            new SwipeResult { Kind = SwipeKind.None, Quality = 0f, Reason = reason, Fail = fail };
    }

    public sealed class SwipeConfig
    {
        /// <summary>Nét ngắn hơn bấy nhiêu pixel (ở 160 dpi, ~7 mm) là chạm nhầm, không tính.</summary>
        public float MinLengthPx = 45f;

        /// <summary>Vuốt lâu hơn bấy nhiêu giây là quá chậm.</summary>
        public float MaxDurationSeconds = 1.5f;

        /// <summary>
        /// Độ thẳng tối thiểu (khoảng cách đầu-cuối / quãng đường) để là vuốt thẳng. 0,78 nhận cả nét cong theo ngón cái
        /// (cung tròn tới ~130°).
        /// </summary>
        public float StraightMin = 0.78f;

        /// <summary>Móc ở đầu / cuối nét (lúc chạm xuống, lúc nhấc tay) dài tới bấy nhiêu phần quãng đường thì bỏ đi.</summary>
        public float HookMaxFraction = 0.25f;

        /// <summary>V: mỗi cạnh ≥ bấy nhiêu phần quãng đường; 2 đầu cao hơn đáy ≥ VRiseMin × cỡ nét; 2 đầu cách nhau ≥ VSpreadMin × cỡ.</summary>
        public float VArmMinFraction = 0.2f;
        public float VRiseMin = 0.3f;
        public float VSpreadMin = 0.2f;

        /// <summary>Z: 2 nét ngang phải có |dx| > bấy nhiêu × |dy|; nét chéo phải đi xuống ≥ ZDiagMin × độ dài.</summary>
        public float ZFlatRatio = 1.2f;
        public float ZDiagMin = 0.3f;

        /// <summary>Tốc độ (pixel/giây ở 160 dpi) cho điểm tốc độ thấp nhất / cao nhất.</summary>
        public float SlowPxPerSec = 300f;
        public float FastPxPerSec = 1800f;

        /// <summary>Hệ số vuốt thấp nhất / cao nhất (G9: 0.7–1.3).</summary>
        public float MinQuality = 0.7f;
        public float MaxQuality = 1.3f;
    }

    /// <summary>
    /// Nhận dạng nét vuốt (C# thuần): thẳng (ngang / lên / xuống), V (xuống rồi lên), Z (ngang - chéo ngược - ngang).
    /// Dễ tính với ngón tay thật: nét cong vẫn là thẳng, móc nhỏ lúc chạm / nhấc tay bị bỏ, V nhận theo điểm thấp nhất
    /// (đáy tròn hay lệch vẫn được), Z thử nhiều mức làm gọn (góc bo tròn vẫn được). Không phụ thuộc Unity.
    /// </summary>
    public sealed class SwipeRecognizer
    {
        private static readonly float[] ZEpsilons = { 0.1f, 0.15f, 0.22f };
        private static readonly float MergeCos = (float)Math.Cos(30.0 * Math.PI / 180.0);

        private readonly SwipeConfig _cfg;
        private readonly float _dpiScale;

        public SwipeRecognizer(SwipeConfig cfg = null, float dpiScale = 1f)
        {
            _cfg = cfg ?? new SwipeConfig();
            _dpiScale = dpiScale > 0f ? dpiScale : 1f;
        }

        public SwipeResult Recognize(IReadOnlyList<SwipePoint> pts)
        {
            if (pts == null || pts.Count < 2) return SwipeResult.Wrong("quá ngắn", SwipeFail.TooShort);
            int last = pts.Count - 1;
            double duration = pts[last].T - pts[0].T;
            if (duration > _cfg.MaxDurationSeconds) return SwipeResult.Wrong("vuốt chậm quá", SwipeFail.TooSlow);
            float length = PathLength(pts, 0, last);
            float minX = pts[0].X, maxX = pts[0].X, minY = pts[0].Y, maxY = pts[0].Y;
            for (int i = 1; i < pts.Count; i++)
            {
                minX = Math.Min(minX, pts[i].X);
                maxX = Math.Max(maxX, pts[i].X);
                minY = Math.Min(minY, pts[i].Y);
                maxY = Math.Max(maxY, pts[i].Y);
            }
            if (length < _cfg.MinLengthPx * _dpiScale) return SwipeResult.Wrong("quá ngắn", SwipeFail.TooShort);
            float speed = (float)(length / Math.Max(duration, 1e-3)) / _dpiScale;
            float speedScore = Clamp01((speed - _cfg.SlowPxPerSec) / (_cfg.FastPxPerSec - _cfg.SlowPxPerSec));
            float size = Math.Max(maxX - minX, maxY - minY);

            if (TryV(pts, length, size, speedScore, out SwipeResult v)) return v;   // xét V trước: V rộng + nông vẫn là ngang
            float straightness = Dist(pts[0], pts[last]) / length;
            if (straightness >= _cfg.StraightMin) return Straight(pts[0], pts[last], straightness, speedScore, 0f);
            if (TryZ(pts, length, size, speedScore, out SwipeResult z)) return z;
            if (TryHooked(pts, length, speedScore, out SwipeResult h)) return h;
            return SwipeResult.Wrong("không rõ hình", SwipeFail.Unclear);
        }

        private SwipeResult Straight(SwipePoint a, SwipePoint b, float straightness, float speedScore, float penalty)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y;
            float fidelity = Clamp01((straightness - _cfg.StraightMin) / (1f - _cfg.StraightMin)) - penalty;
            var r = new SwipeResult { Quality = Quality(speedScore, fidelity) };
            if (Math.Abs(dx) >= Math.Abs(dy))
            {
                r.Kind = SwipeKind.Horizontal;
                r.Direction = dx >= 0 ? 1 : -1;
            }
            else r.Kind = dy > 0 ? SwipeKind.Up : SwipeKind.Down;
            return r;
        }

        /// <summary>V = đi xuống tới điểm thấp nhất rồi đi lên; 2 cạnh khá thẳng, đáy nằm giữa 2 đầu.</summary>
        private bool TryV(IReadOnlyList<SwipePoint> pts, float length, float size, float speedScore, out SwipeResult r)
        {
            r = default;
            int last = pts.Count - 1, m = 0;
            for (int i = 1; i <= last; i++)
                if (pts[i].Y < pts[m].Y) m = i;
            if (m == 0 || m == last) return false;
            SwipePoint s = pts[0], b = pts[m], e = pts[last];
            float arm1 = PathLength(pts, 0, m), arm2 = PathLength(pts, m, last);
            if (arm1 < _cfg.VArmMinFraction * length || arm2 < _cfg.VArmMinFraction * length) return false;
            if (s.Y - b.Y < _cfg.VRiseMin * size || e.Y - b.Y < _cfg.VRiseMin * size) return false;
            if (Math.Abs(e.X - s.X) < _cfg.VSpreadMin * size) return false;   // lên xuống trên 1 đường thẳng: không phải V
            float pad = 0.15f * size;
            if (b.X < Math.Min(s.X, e.X) - pad || b.X > Math.Max(s.X, e.X) + pad) return false;
            if (Dist(s, b) < 0.75f * arm1 || Dist(b, e) < 0.75f * arm2) return false;   // cạnh cong queo: nguệch ngoạc
            float sym = Math.Min(arm1, arm2) / Math.Max(arm1, arm2);   // 2 cạnh V dài gần bằng nhau
            r = new SwipeResult { Kind = SwipeKind.V, Quality = Quality(speedScore, sym) };
            return true;
        }

        /// <summary>Z = ngang - chéo ngược xuống - ngang (cùng chiều nét đầu). Thử vài mức làm gọn để góc bo tròn vẫn nhận.</summary>
        private bool TryZ(IReadOnlyList<SwipePoint> pts, float length, float size, float speedScore, out SwipeResult r)
        {
            r = default;
            foreach (float k in ZEpsilons)
            {
                List<SwipePoint> poly = Simplify(pts, size * k);
                TrimHooks(poly, 0.12f * length);
                MergeStraight(poly);
                if (poly.Count != 4) continue;
                Seg s1 = new Seg(poly[0], poly[1]), s2 = new Seg(poly[1], poly[2]), s3 = new Seg(poly[2], poly[3]);
                bool flat1 = Math.Abs(s1.Dx) > _cfg.ZFlatRatio * Math.Abs(s1.Dy);
                bool flat3 = Math.Abs(s3.Dx) > _cfg.ZFlatRatio * Math.Abs(s3.Dy);
                bool diag = s2.Dy < 0 && Math.Abs(s2.Dy) >= _cfg.ZDiagMin * s2.Len && Math.Sign(s2.Dx) == -Math.Sign(s1.Dx);
                if (!flat1 || !flat3 || !diag || Math.Sign(s1.Dx) != Math.Sign(s3.Dx)) continue;
                float flatness = Clamp01(1f - (Math.Abs(s1.Dy) / s1.Len + Math.Abs(s3.Dy) / s3.Len));
                r = new SwipeResult { Kind = SwipeKind.Z, Quality = Quality(speedScore, flatness), Direction = Math.Sign(s1.Dx) };
                return true;
            }
            return false;
        }

        /// <summary>
        /// Nét thẳng có móc ở đầu / cuối (ngón tay giật lúc chạm xuống / nhấc lên): bỏ móc rồi xét phần chính.
        /// Hệ số vuốt bị trừ chút cho nét không gọn.
        /// </summary>
        private bool TryHooked(IReadOnlyList<SwipePoint> pts, float length, float speedScore, out SwipeResult r)
        {
            r = default;
            int last = pts.Count - 1, start = 0, end = last;
            float hook = _cfg.HookMaxFraction * length;
            int f = Farthest(pts, pts[0], 0, last);   // xa điểm đầu nhất: sau đó là móc cuối
            if (PathLength(pts, f, last) <= hook) end = f;
            int g = Farthest(pts, pts[end], 0, end);   // xa điểm cuối (mới) nhất: trước đó là móc đầu
            if (PathLength(pts, 0, g) <= hook) start = g;
            if ((start == 0 && end == last) || end <= start) return false;
            float main = PathLength(pts, start, end);
            if (main < _cfg.MinLengthPx * _dpiScale) return false;
            float straightness = Dist(pts[start], pts[end]) / main;
            if (straightness < _cfg.StraightMin) return false;
            r = Straight(pts[start], pts[end], straightness, speedScore, 0.2f);
            return true;
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

        private static float PathLength(IReadOnlyList<SwipePoint> pts, int from, int to)
        {
            float len = 0f;
            for (int i = from + 1; i <= to; i++) len += Dist(pts[i - 1], pts[i]);
            return len;
        }

        private static int Farthest(IReadOnlyList<SwipePoint> pts, SwipePoint from, int i0, int i1)
        {
            int best = i0;
            float bestD = -1f;
            for (int i = i0; i <= i1; i++)
            {
                float d = Dist(from, pts[i]);
                if (d > bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Bỏ đoạn đầu / cuối ngắn hơn `minLen` (móc lúc chạm / nhấc tay).</summary>
        private static void TrimHooks(List<SwipePoint> poly, float minLen)
        {
            while (poly.Count > 2 && Dist(poly[0], poly[1]) < minLen) poly.RemoveAt(0);
            while (poly.Count > 2 && Dist(poly[poly.Count - 2], poly[poly.Count - 1]) < minLen) poly.RemoveAt(poly.Count - 1);
        }

        /// <summary>Gộp 2 đoạn liền nhau gần như cùng hướng (đổi hướng dưới 30°).</summary>
        private static void MergeStraight(List<SwipePoint> poly)
        {
            for (int i = poly.Count - 2; i >= 1; i--)
            {
                var a = new Seg(poly[i - 1], poly[i]);
                var b = new Seg(poly[i], poly[i + 1]);
                if ((a.Dx * b.Dx + a.Dy * b.Dy) / (a.Len * b.Len) > MergeCos) poly.RemoveAt(i);
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
