using System.Collections.Generic;
using System.Numerics;

namespace PawFit.Garden
{
    /// <summary>Các khu nằm TRÊN MAP vườn (không phải nút UI).</summary>
    /// <summary>Khu trong vườn. Bench (Phase 8.4) = ghế gỗ mua ở quầy: không nằm trong GardenLayout, bộ não giữ chỗ ghế riêng.</summary>
    public enum ZoneType { None = 0, FoodHut = 1, Pond = 2, TrainingGround = 3, Bed = 4, Memorial = 5, Shop = 6, Adventure = 7, Bench = 8 }

    /// <summary>
    /// 1 khu trong vườn. Tọa độ mặt đất dùng Vector2: X = trái/phải, Y = sâu vào trong màn hình
    /// (bên Unity đổi thành Vector3(x, 0, y)).
    /// </summary>
    public sealed class GardenZone
    {
        public readonly ZoneType Type;
        public readonly Vector2 Center;
        public readonly float Radius;

        /// <summary>Chỗ thú đứng để dùng khu này (vd trước cửa chòi ăn).</summary>
        public readonly Vector2 UsePoint;

        public GardenZone(ZoneType type, Vector2 center, float radius, Vector2 usePoint)
        {
            Type = type;
            Center = center;
            Radius = radius;
            UsePoint = usePoint;
        }
    }

    /// <summary>Bố cục vườn cố định (rộng, camera đi theo thú). Chỉ là dữ liệu + phép tính thuần.</summary>
    public sealed class GardenLayout
    {
        public Vector2 Min;
        public Vector2 Max;
        public Vector2 PetSpawn;
        public readonly List<GardenZone> Zones = new List<GardenZone>();

        /// <summary>
        /// Chỗ có thể hiện điểm đào kho báu (Phase 7.3): bãi cỏ trống, đã chừa khu, cây, đồ trang trí, lối đá.
        /// Mỗi ngày chọn vài chỗ trong danh sách này.
        /// </summary>
        public readonly List<Vector2> DigSpots = new List<Vector2>();

        /// <summary>Vườn mặc định: chòi ăn trái, hồ phải, sân tập phía sau, ổ ngủ giữa, tưởng niệm ở góc, quầy hàng phía trước bên phải.</summary>
        public static GardenLayout CreateDefault()
        {
            var l = new GardenLayout
            {
                Min = new Vector2(-10f, -13f),
                Max = new Vector2(10f, 14f),
                PetSpawn = new Vector2(0f, -2.5f),
            };
            l.Zones.Add(new GardenZone(ZoneType.Bed, new Vector2(0f, 0.5f), 1.3f, new Vector2(0f, 0.5f)));
            l.Zones.Add(new GardenZone(ZoneType.FoodHut, new Vector2(-7f, 1f), 1.8f, new Vector2(-5.2f, 0.4f)));
            l.Zones.Add(new GardenZone(ZoneType.Pond, new Vector2(6.8f, 0.5f), 2.6f, new Vector2(6.8f, 0.5f)));
            l.Zones.Add(new GardenZone(ZoneType.TrainingGround, new Vector2(0f, 9.5f), 3.4f, new Vector2(0f, 6.8f)));
            l.Zones.Add(new GardenZone(ZoneType.Memorial, new Vector2(8f, 12f), 1.5f, new Vector2(6.9f, 10.9f)));
            // quầy hàng (Phase 5.1): chạm vào = mở quầy; UsePoint = chỗ đứng trước quầy (cuối lối đá)
            l.Zones.Add(new GardenZone(ZoneType.Shop, new Vector2(4.5f, -6.5f), 1.5f, new Vector2(3.85f, -8.3f)));
            // biển chỉ đường Thám hiểm (Phase 7.2) ở góc sau bên trái: lối mòn dẫn ra khe hở hàng rào trái (z 9..11)
            l.Zones.Add(new GardenZone(ZoneType.Adventure, new Vector2(-7.3f, 10.2f), 1f, new Vector2(-6.4f, 9.3f)));
            l.DigSpots.AddRange(new[]
            {
                new Vector2(-3.5f, -4.5f), new Vector2(2.5f, -9.5f), new Vector2(-6.5f, -9.5f), new Vector2(7.5f, -4.5f),
                new Vector2(-3f, 4f), new Vector2(3.2f, 3.5f), new Vector2(-7.5f, -2f), new Vector2(6.5f, 8f),
                new Vector2(-4f, 8f), new Vector2(-1.5f, -7.5f), new Vector2(8f, -8f), new Vector2(-1.5f, 5.5f),
            });
            return l;
        }

        public GardenZone Get(ZoneType type)
        {
            foreach (GardenZone z in Zones)
                if (z.Type == type) return z;
            return null;
        }

        /// <summary>Điểm p đang nằm trong khu nào (dùng để biết thả thú vào hồ / ổ).</summary>
        public ZoneType ZoneAt(Vector2 p)
        {
            foreach (GardenZone z in Zones)
                if (Vector2.Distance(p, z.Center) <= z.Radius) return z.Type;
            return ZoneType.None;
        }

        public bool Contains(Vector2 p, float margin = 0f) =>
            p.X >= Min.X + margin && p.X <= Max.X - margin && p.Y >= Min.Y + margin && p.Y <= Max.Y - margin;

        /// <summary>Kéo 1 điểm về trong biên vườn (cách mép `margin` mét).</summary>
        public Vector2 Clamp(Vector2 p, float margin = 0f) =>
            new Vector2(Clamp(p.X, Min.X + margin, Max.X - margin), Clamp(p.Y, Min.Y + margin, Max.Y - margin));

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>Số liệu điều khiển + chăm sóc trong vườn (chỉnh được, không phải sửa code logic).</summary>
    public sealed class GardenConfig
    {
        // ---- Cử chỉ ----
        /// <summary>Chạm nhanh hơn ngưỡng này (giây) mới tính là "chạm".</summary>
        public float TapMaxSeconds = 0.30f;

        /// <summary>Ngón tay dịch quá ngưỡng (pixel ở 160 dpi, nhân theo DPI thật) thì thành "kéo".</summary>
        public float DragThresholdPx = 14f;

        // ---- Di chuyển ----
        public float WalkSpeed = 1.6f;
        public float RunSpeed = 3.4f;
        public float LeadSpeed = 4.2f;
        public float ArriveDistance = 0.4f;
        public float WanderRadius = 5f;
        public float IdleMinSeconds = 2.5f;
        public float IdleMaxSeconds = 6f;

        /// <summary>Sau lệnh của người chơi, thú chưa tự quyết trong khoảng này (giây).</summary>
        public float PlayerHoldSeconds = 10f;

        // ---- Chăm sóc ----
        /// <summary>Thả đồ ăn cách thú tối đa bấy nhiêu mét thì thú ăn.</summary>
        public float FeedReach = 1.3f;
        public float FeedSatiety = 35f;
        public float FeedMood = 4f;

        /// <summary>No từ mức này trở lên thì từ chối ăn (lắc đầu).</summary>
        public float FullSatiety = 92f;
        public float BatheHygiene = 60f;
        public float BatheMood = 3f;
        public float PetMood = 3f;

        /// <summary>Vuốt ve liên tục chỉ cộng vui mỗi bấy nhiêu giây.</summary>
        public float PetMoodCooldownSeconds = 4f;

        /// <summary>
        /// Vuốt ve = xoa qua lại trên người thú (Phase 8.2): mỗi lượt xoa cộng bấy nhiêu vui + điểm Chăm sóc,
        /// 2 lượt được cộng cách nhau ≥ StrokeCooldownSeconds. Xoa 10 giây ≈ +20 vui (chạm đơn: +3 mỗi 4 giây).
        /// </summary>
        public float StrokeMood = 1.5f;
        public float StrokeCooldownSeconds = 0.6f;
        public float StrokeCareFactor = 0.5f;

        // ---- Tự quyết của thú ----
        public float SleepyBelowEnergy = 20f;
        public float NotSleepyAboveEnergy = 95f;
        public float WakeAboveEnergy = 95f;
        public float HungryBelowSatiety = 35f;
        public float EatSeconds = 1.6f;

        /// <summary>1 buổi tập ở sân tập kéo dài bấy nhiêu giây.</summary>
        public float WorkoutSeconds = 4f;
        public float BatheSeconds = 2.6f;
        public float ReactSeconds = 0.8f;

        /// <summary>Đi quá lâu chưa tới đích (bị kẹt) thì coi như đã tới.</summary>
        public float MoveTimeoutExtraSeconds = 4f;

        // ---- Thú mệt / ốm (Phase 2.1) ----
        /// <summary>Tốc độ đi so với lúc khỏe.</summary>
        public float TiredSpeedFactor = 0.85f;
        public float SickSpeedFactor = 0.65f;
        public float CriticalSpeedFactor = 0.45f;

        /// <summary>Thú ốm: năng lượng dưới mức này thì tự về ổ nằm nghỉ (nguy kịch: ngưỡng cao hơn => nằm gần như cả ngày).</summary>
        public float SickRestBelowEnergy = 60f;
        public float CriticalRestBelowEnergy = 90f;

        /// <summary>Chạm vào thú đang ốm = lau người bằng khăn ấm.</summary>
        public float WipeHygiene = 15f;
        public float WipeMood = 2f;
        public float WipeCooldownSeconds = 3f;

        // ---- Ghế gỗ (Phase 8.4) ----
        /// <summary>Bé rảnh mà năng lượng dưới mức này thì có lúc (xác suất mỗi lần tự quyết) tự ra ghế ngồi nghỉ bấy nhiêu giây.</summary>
        public float BenchRestBelowEnergy = 65f;
        public float BenchRestChance = 0.35f;
        public float BenchRestSeconds = 9f;

        /// <summary>Mỗi lần ngồi ghế: năng lượng + vui, cộng nhiều nhất mỗi bấy nhiêu phút (ngồi liên tục không cộng mãi).</summary>
        public float BenchRestEnergy = 4f;
        public float BenchRestMood = 2f;
        public float BenchRestCooldownMinutes = 10f;

        /// <summary>Thả bé (đang dắt) cách tâm ghế dưới bấy nhiêu mét = bé lên ghế ngồi.</summary>
        public float BenchDropRadius = 1.1f;

        // ---- Ban đêm (Phase 10.1) ----
        /// <summary>Giờ đi ngủ (DayCycle.IsBedtime): năng lượng dưới mức này là bé tự về ổ (ban ngày chỉ khi dưới SleepyBelowEnergy).</summary>
        public float NightSleepyBelowEnergy = 55f;

        /// <summary>Bạn đánh thức bé lúc nửa đêm (chạm đất / dắt / gọi đi chơi): bé thức thêm bấy nhiêu giây rồi mới buồn ngủ lại.</summary>
        public float NightWakeSnoozeSeconds = 180f;
    }
}
