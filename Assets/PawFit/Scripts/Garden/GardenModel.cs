using System.Collections.Generic;
using System.Numerics;

namespace PawFit.Garden
{
    /// <summary>Các khu nằm TRÊN MAP vườn (không phải nút UI).</summary>
    public enum ZoneType { None = 0, FoodHut = 1, Pond = 2, TrainingGround = 3, Bed = 4, Memorial = 5, Shop = 6, Adventure = 7 }

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
    }
}
