using System;
using System.Collections.Generic;

namespace PawFit.Garden
{
    /// <summary>Buổi trong ngày theo giờ trên máy (Phase 10.1).</summary>
    public enum DayPhase { Dawn = 0, Day = 1, Dusk = 2, Night = 3 }

    /// <summary>Màu RGB 0..1 (C# thuần, bên Unity đổi thành Color).</summary>
    public readonly struct Rgb
    {
        public readonly float R, G, B;

        public Rgb(float r, float g, float b)
        {
            R = r;
            G = g;
            B = b;
        }

        /// <summary>Độ sáng cảm nhận (để test "đêm tối hơn ngày").</summary>
        public float Luma => 0.2126f * R + 0.7152f * G + 0.0722f * B;

        public static Rgb Lerp(Rgb a, Rgb b, float t) =>
            new Rgb(a.R + (b.R - a.R) * t, a.G + (b.G - a.G) * t, a.B + (b.B - a.B) * t);

        public static float Distance(Rgb a, Rgb b) =>
            Math.Max(Math.Abs(a.R - b.R), Math.Max(Math.Abs(a.G - b.G), Math.Abs(a.B - b.B)));

        public override string ToString() => $"({R:0.00}, {G:0.00}, {B:0.00})";
    }

    /// <summary>
    /// Ánh sáng vườn ở 1 giờ trong ngày (1 mốc trong bảng, hoặc trộn giữa 2 mốc).
    /// Mặt trời (đêm là mặt trăng) luôn chiếu từ sau lưng camera để mặt bé không bị tối:
    /// SunPitch = độ cao (độ), SunYaw = hướng (độ, 0 = chiếu thẳng vào trong màn hình).
    /// </summary>
    public sealed class DayLight
    {
        public float Hour;
        public Rgb SunColor;
        public float SunIntensity;
        public float SunPitch;
        public float SunYaw;
        public float ShadowStrength;
        public Rgb AmbientSky;
        public Rgb AmbientEquator;
        public Rgb AmbientGround;

        /// <summary>Màu sương = màu nền camera (chân trời).</summary>
        public Rgb Fog;

        /// <summary>0 = ban ngày, 1 = đêm hẳn: đom đóm, sao trên mặt hồ, tiếng dế, đèn lồng, đèn ngủ.</summary>
        public float Night;

        /// <summary>Chim hót nhiều hay ít (0..1): rộn ràng nhất lúc bình minh, đêm im hẳn.</summary>
        public float Birds;

        public DayLight Clone() => (DayLight)MemberwiseClone();

        /// <summary>Đổi dần mọi giá trị về phía `goal` (t = 0: giữ nguyên, 1: bằng goal). View gọi mỗi frame để trời chuyển mượt.</summary>
        public void MoveToward(DayLight goal, float t)
        {
            SunColor = Rgb.Lerp(SunColor, goal.SunColor, t);
            SunIntensity = Lerp(SunIntensity, goal.SunIntensity, t);
            SunPitch = Lerp(SunPitch, goal.SunPitch, t);
            SunYaw = Lerp(SunYaw, goal.SunYaw, t);
            ShadowStrength = Lerp(ShadowStrength, goal.ShadowStrength, t);
            AmbientSky = Rgb.Lerp(AmbientSky, goal.AmbientSky, t);
            AmbientEquator = Rgb.Lerp(AmbientEquator, goal.AmbientEquator, t);
            AmbientGround = Rgb.Lerp(AmbientGround, goal.AmbientGround, t);
            Fog = Rgb.Lerp(Fog, goal.Fog, t);
            Night = Lerp(Night, goal.Night, t);
            Birds = Lerp(Birds, goal.Birds, t);
            Hour = t >= 1f ? goal.Hour : Hour;
        }

        internal static DayLight Mix(DayLight a, DayLight b, float t, float hour)
        {
            DayLight m = a.Clone();
            m.MoveToward(b, t);
            m.Hour = hour;
            return m;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    public sealed class DayCycleConfig
    {
        /// <summary>
        /// Giờ đi ngủ của bé: từ BedtimeStartHour tới BedtimeEndHour (qua nửa đêm). Trong giờ này bé buồn ngủ sớm hơn
        /// (GardenConfig.NightSleepyBelowEnergy) và đã ngủ thì ngủ một mạch tới sáng (trừ khi bạn đánh thức).
        /// </summary>
        public float BedtimeStartHour = 21.5f;
        public float BedtimeEndHour = 6f;

        /// <summary>Tắt ngày / đêm (hòm thư) thì vườn luôn ở giờ này (trưa — sáng đẹp nhất).</summary>
        public float FixedHour = 12f;

        /// <summary>Ranh giới các buổi (giờ): Dawn [DawnStart, DayStart), Day [DayStart, DuskStart), Dusk [DuskStart, NightStart).</summary>
        public float DawnStart = 4.5f;
        public float DayStart = 7.5f;
        public float DuskStart = 17f;
        public float NightStart = 20f;

        /// <summary>Bảng mốc ánh sáng theo giờ (xếp tăng dần, 0 ≤ giờ &lt; 24); giữa 2 mốc trộn mượt, qua nửa đêm vòng lại.</summary>
        public readonly List<DayLight> Keys = DefaultKeys();

        /// <summary>Mốc trưa = đúng ánh sáng vườn trước Phase 10.1 (đã chỉnh cho đẹp), các mốc khác dựa theo nó.</summary>
        public static List<DayLight> DefaultKeys()
        {
            DayLight night = Key(0f, new Rgb(0.62f, 0.70f, 1.00f), 0.42f, 58f, 15f, 0.45f,
                                 new Rgb(0.24f, 0.30f, 0.50f), new Rgb(0.20f, 0.25f, 0.38f), new Rgb(0.10f, 0.11f, 0.16f),
                                 new Rgb(0.11f, 0.15f, 0.28f), 1f, 0f);
            DayLight lateNight = night.Clone();
            lateNight.Hour = 4.5f;
            DayLight nightStart = night.Clone();
            nightStart.Hour = 20.5f;
            return new List<DayLight>
            {
                night,
                lateNight,
                // bình minh: nắng cam hồng chiếu xiên từ bên trái, chim hót rộn ràng
                Key(6f, new Rgb(1.00f, 0.70f, 0.52f), 0.80f, 16f, -72f, 0.55f,
                    new Rgb(0.62f, 0.64f, 0.82f), new Rgb(0.86f, 0.68f, 0.62f), new Rgb(0.36f, 0.31f, 0.30f),
                    new Rgb(0.90f, 0.76f, 0.74f), 0.15f, 1f),
                Key(8f, new Rgb(1.00f, 0.93f, 0.82f), 1.08f, 36f, -58f, 0.65f,
                    new Rgb(0.70f, 0.80f, 0.92f), new Rgb(0.76f, 0.79f, 0.68f), new Rgb(0.41f, 0.39f, 0.33f),
                    new Rgb(0.80f, 0.88f, 0.94f), 0f, 0.8f),
                // trưa: ánh sáng gốc của vườn
                Key(12f, new Rgb(1.00f, 0.95f, 0.86f), 1.15f, 50f, -35f, 0.70f,
                    new Rgb(0.72f, 0.82f, 0.92f), new Rgb(0.74f, 0.80f, 0.68f), new Rgb(0.42f, 0.40f, 0.34f),
                    new Rgb(0.78f, 0.88f, 0.94f), 0f, 0.5f),
                Key(15.5f, new Rgb(1.00f, 0.92f, 0.80f), 1.12f, 42f, -15f, 0.70f,
                    new Rgb(0.72f, 0.80f, 0.90f), new Rgb(0.78f, 0.79f, 0.66f), new Rgb(0.42f, 0.39f, 0.33f),
                    new Rgb(0.82f, 0.87f, 0.92f), 0f, 0.5f),
                // chiều tà: nắng vàng cam, bóng đổ dài
                Key(17.75f, new Rgb(1.00f, 0.74f, 0.50f), 0.98f, 20f, 18f, 0.60f,
                    new Rgb(0.70f, 0.66f, 0.76f), new Rgb(0.90f, 0.70f, 0.56f), new Rgb(0.40f, 0.33f, 0.28f),
                    new Rgb(0.96f, 0.78f, 0.64f), 0.05f, 0.7f),
                // chạng vạng: tím hồng, đom đóm bắt đầu ra
                Key(19f, new Rgb(0.86f, 0.56f, 0.64f), 0.60f, 12f, 32f, 0.50f,
                    new Rgb(0.40f, 0.38f, 0.60f), new Rgb(0.52f, 0.42f, 0.50f), new Rgb(0.20f, 0.18f, 0.22f),
                    new Rgb(0.42f, 0.36f, 0.56f), 0.55f, 0.2f),
                nightStart,
            };
        }

        private static DayLight Key(float hour, Rgb sun, float intensity, float pitch, float yaw, float shadow,
                                    Rgb sky, Rgb equator, Rgb ground, Rgb fog, float night, float birds) =>
            new DayLight
            {
                Hour = hour, SunColor = sun, SunIntensity = intensity, SunPitch = pitch, SunYaw = yaw, ShadowStrength = shadow,
                AmbientSky = sky, AmbientEquator = equator, AmbientGround = ground, Fog = fog, Night = night, Birds = birds,
            };
    }

    /// <summary>
    /// Ngày / đêm trong vườn theo giờ trên máy (Phase 10.1, C# thuần + test).
    /// View đọc <see cref="Light"/> để chỉnh nắng / ánh sáng môi trường / sương, đọc <see cref="Night"/> để bật đom đóm,
    /// tiếng dế, đèn lồng; bộ não đọc <see cref="IsBedtime"/> để bé đi ngủ sớm hơn ban đêm.
    /// Giờ lấy qua hàm (mặc định giờ của game đổi ra giờ máy) nên bảng DEV tua giờ thì trời đổi theo.
    /// </summary>
    public sealed class DayCycle
    {
        private readonly Func<DateTime> _localNow;
        private readonly DayCycleConfig _cfg;

        public DayCycle(Func<DateTime> localNow, DayCycleConfig cfg = null)
        {
            _localNow = localNow ?? throw new ArgumentNullException(nameof(localNow));
            _cfg = cfg ?? new DayCycleConfig();
            if (_cfg.Keys.Count == 0) throw new ArgumentException("Bảng ánh sáng trống", nameof(cfg));
        }

        public DayCycleConfig Config => _cfg;

        /// <summary>false (hòm thư tắt ngày / đêm) = vườn luôn sáng như buổi trưa, bé không có giờ đi ngủ.</summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Bảng DEV: ép giờ trong vườn (null = theo đồng hồ).</summary>
        public float? DebugHour { get; set; }

        /// <summary>Giờ trên đồng hồ (0..24, có phần lẻ), bỏ qua Enabled.</summary>
        public float ClockHour => DebugHour.HasValue ? Wrap(DebugHour.Value) : HourOf(_localNow());

        /// <summary>Giờ vườn đang hiển thị (tắt ngày / đêm thì luôn là FixedHour).</summary>
        public float Hour => Enabled ? ClockHour : _cfg.FixedHour;

        public DayPhase Phase => PhaseAt(Hour, _cfg);
        public DayLight Light => Sample(Hour, _cfg);
        public float Night => Sample(Hour, _cfg).Night;
        public bool IsNight => Night >= 0.5f;
        public bool IsBedtime => Enabled && InBedtime(Hour, _cfg);

        public static float HourOf(DateTime local) => (float)local.TimeOfDay.TotalHours;

        public static DayPhase PhaseAt(float hour, DayCycleConfig cfg)
        {
            hour = Wrap(hour);
            if (hour >= cfg.DawnStart && hour < cfg.DayStart) return DayPhase.Dawn;
            if (hour >= cfg.DayStart && hour < cfg.DuskStart) return DayPhase.Day;
            if (hour >= cfg.DuskStart && hour < cfg.NightStart) return DayPhase.Dusk;
            return DayPhase.Night;
        }

        public static bool InBedtime(float hour, DayCycleConfig cfg)
        {
            hour = Wrap(hour);
            float a = cfg.BedtimeStartHour, b = cfg.BedtimeEndHour;
            return a <= b ? hour >= a && hour < b : hour >= a || hour < b;
        }

        /// <summary>Ánh sáng ở giờ `hour`: trộn mượt (smoothstep) giữa 2 mốc kề nhau, qua 24 giờ vòng về 0.</summary>
        public static DayLight Sample(float hour, DayCycleConfig cfg)
        {
            List<DayLight> keys = cfg.Keys;
            hour = Wrap(hour);
            int n = keys.Count;
            if (n == 1)
            {
                DayLight only = keys[0].Clone();
                only.Hour = hour;
                return only;
            }
            // mốc cuối cùng có giờ ≤ hour (trước mốc đầu thì là mốc cuối của hôm trước)
            int i = n - 1;
            for (int k = 0; k < n; k++)
                if (keys[k].Hour <= hour) i = k;
            DayLight a = keys[i], b = keys[(i + 1) % n];
            float start = a.Hour, end = b.Hour, h = hour;
            if (end <= start) end += 24f;   // đoạn qua nửa đêm
            if (h < start) h += 24f;
            float t = end - start > 1e-4f ? (h - start) / (end - start) : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            t = t * t * (3f - 2f * t);
            return DayLight.Mix(a, b, t, hour);
        }

        private static float Wrap(float hour)
        {
            float h = hour % 24f;
            return h < 0f ? h + 24f : h;
        }
    }
}
