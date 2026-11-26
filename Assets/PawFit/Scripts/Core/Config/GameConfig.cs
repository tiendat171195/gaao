namespace PawFit.Core
{
    /// <summary>
    /// Mọi con số cân bằng game ở 1 chỗ. Hiện lấy giá trị mặc định trong code;
    /// sau này đọc từ file JSON để chỉnh số mà không phải sửa code.
    /// </summary>
    public sealed class GameConfig
    {
        public NeedsConfig Needs = new NeedsConfig();

        public static GameConfig CreateDefault() => new GameConfig();
    }

    /// <summary>4 nhu cầu ẩn (thang 0–100), giảm theo giờ thực — GDD mục G1, G2.</summary>
    public sealed class NeedsConfig
    {
        /// <summary>No bụng giảm mỗi giờ (~25 giờ từ đầy tới cạn).</summary>
        public float SatietyDecayPerHour = 4f;

        /// <summary>Sạch sẽ giảm mỗi giờ (~33 giờ).</summary>
        public float HygieneDecayPerHour = 3f;

        /// <summary>Năng lượng giảm mỗi giờ khi thức.</summary>
        public float EnergyDecayPerHour = 4f;

        /// <summary>Năng lượng hồi mỗi giờ khi ngủ.</summary>
        public float EnergyRegenPerHour = 15f;

        /// <summary>Vui vẻ giảm mỗi giờ.</summary>
        public float MoodDecayPerHour = 3.5f;

        /// <summary>"Chăm đủ" = cả 4 nhu cầu >= ngưỡng này cùng lúc (dùng cho đồng hồ bỏ bê ở Phase 2.1).</summary>
        public float FullCareThreshold = 60f;

        /// <summary>Giá trị 4 nhu cầu lúc thú mới nở.</summary>
        public float StartValue = 80f;
    }
}
