using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>Trò chơi với bé trong vườn (Phase 8.3).</summary>
    public enum PlayKind { Fetch = 0, Pounce = 1, BubblePop = 2, SelfPlay = 3 }

    /// <summary>Số liệu trò chơi — GDD G17 (chỉnh ở đây, không phải sửa code).</summary>
    public sealed class PlayConfig
    {
        /// <summary>Mỗi lần: vui +, điểm Tốc độ +, năng lượng −, no − (theo PlayKind: nhặt bóng / vồ lông vũ / đớp bong bóng / tự chơi).</summary>
        public float[] Mood = { 6f, 4f, 2.5f, 3f };
        public float[] SpeedPoints = { 1.5f, 1f, 0.5f, 0.3f };
        public float[] Energy = { 1.5f, 1f, 0.5f, 0.5f };
        public float[] Satiety = { 0.6f, 0.4f, 0.2f, 0.2f };

        /// <summary>2 lần thưởng cùng kiểu cách nhau ít nhất bấy nhiêu giây (vồ liên tục / đớp bong bóng dồn không cộng mãi).</summary>
        public float[] CooldownSeconds = { 1f, 1.2f, 0.5f, 8f };

        /// <summary>Năng lượng dưới mức này thì bé không chịu chơi (mệt, cần ngủ).</summary>
        public float MinEnergy = 10f;

        /// <summary>Tự chơi bóng khi rảnh: vui dưới mức này + năng lượng trên mức kia, xác suất mỗi lần bé tự quyết.</summary>
        public float SelfPlayBelowMood = 75f;
        public float SelfPlayAboveEnergy = 25f;
        public float SelfPlayChance = 0.2f;
    }

    /// <summary>Vừa chơi xong 1 lượt (nhiệm vụ ngày "chơi với bé" nghe sự kiện này).</summary>
    public readonly struct PlayDone
    {
        public readonly PlayKind Kind;
        public PlayDone(PlayKind kind) { Kind = kind; }
    }

    /// <summary>
    /// Luật chơi với bé (C# thuần, Phase 8.3): bé nhặt bóng mang về, vồ lông vũ, nhảy đớp bong bóng, tự đẩy bóng chơi.
    /// Mỗi lượt: vui lên, thêm điểm Tốc độ (có trần mỗi ngày của EvolutionService), tốn chút năng lượng + no.
    /// Bé ốm / đang ngủ / quá mệt thì không chơi. View (đồ chơi trong vườn) quyết định lúc nào "trúng" rồi gọi Reward.
    /// </summary>
    public sealed class PlayService
    {
        private readonly PetService _pets;
        private readonly HealthService _health;
        private readonly EvolutionService _evo;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly PlayConfig _cfg;
        private readonly DateTime[] _last = new DateTime[4];

        public PlayService(PetService pets, HealthService health, EvolutionService evo, ITimeProvider time, EventBus bus, PlayConfig cfg = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? new PlayConfig();
            for (int i = 0; i < _last.Length; i++) _last[i] = DateTime.MinValue;
        }

        public PlayConfig Config => _cfg;

        /// <summary>Bé có chơi được không (đang khỏe, thức, còn sức).</summary>
        public bool CanPlay(out string reason)
        {
            reason = null;
            if (!_pets.HasPet) reason = "không có thú";
            else if (_pets.Current.IsSleeping) reason = "bé đang ngủ";
            else if (_health.IsIll) reason = "bé đang ốm, chữa khỏi rồi chơi nhé";
            else if (_pets.Current.Needs.Energy < _cfg.MinEnergy) reason = "bé mệt rồi, cho bé ngủ đã";
            return reason == null;
        }

        public bool CanPlay() => CanPlay(out _);

        /// <summary>Bé có muốn tự đẩy bóng chơi lúc rảnh không (chán + còn khỏe).</summary>
        public bool WantsSelfPlay(double roll)
        {
            if (!CanPlay()) return false;
            PetNeeds n = _pets.Current.Needs;
            return n.Mood < _cfg.SelfPlayBelowMood && n.Energy > _cfg.SelfPlayAboveEnergy && roll < _cfg.SelfPlayChance;
        }

        /// <summary>1 lượt chơi thành công: cộng vui + điểm Tốc độ, trừ sức. Trả false nếu bé không chơi được / chưa hết thời gian chờ.</summary>
        public bool Reward(PlayKind kind)
        {
            if (!CanPlay()) return false;
            int k = (int)kind;
            DateTime now = _time.UtcNow;
            if ((now - _last[k]).TotalSeconds < _cfg.CooldownSeconds[k]) return false;
            _last[k] = now;
            _pets.Care(NeedType.Mood, _cfg.Mood[k]);
            _pets.Spend(NeedType.Energy, _cfg.Energy[k]);
            _pets.Spend(NeedType.Satiety, _cfg.Satiety[k]);
            _evo.AddTraining(TrainingAxis.Speed, _cfg.SpeedPoints[k]);
            _bus.Publish(new PlayDone(kind));
            return true;
        }
    }
}
