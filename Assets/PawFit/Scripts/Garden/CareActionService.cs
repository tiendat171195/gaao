using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>Wipe = chạm vào thú đang ốm (lau người). Workout = tập sức mạnh ở sân tập.</summary>
    public enum CareKind { Feed, Bathe, Pet, Sleep, Wake, Wipe, Workout }

    public enum CareResult { Ok, Refused }

    /// <summary>Sự kiện: vừa chăm sóc xong (View dùng để chạy hiệu ứng, log).</summary>
    public readonly struct CareDone
    {
        public readonly CareKind Kind;
        public readonly CareResult Result;
        public readonly string Reason;

        public CareDone(CareKind kind, CareResult result, string reason)
        {
            Kind = kind;
            Result = result;
            Reason = reason;
        }
    }

    /// <summary>
    /// Luật chăm sóc trong vườn: cho ăn, tắm, vuốt ve, ngủ / dậy. Thú ốm (Phase 2.1): cho ăn = cháo, ngủ ổ = đắp chăn,
    /// chạm = lau người (3 việc của 1 lượt chăm kỹ), không chịu xuống hồ tắm.
    /// Quyết định được hay không (vd no rồi thì từ chối) nằm ở đây, View chỉ chạy hiệu ứng theo kết quả.
    /// </summary>
    public sealed class CareActionService
    {
        private readonly PetService _pets;
        private readonly HealthService _health;
        private readonly EvolutionService _evo;
        private readonly GardenConfig _cfg;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly ILog _log;
        private DateTime _lastPetMoodUtc = DateTime.MinValue;
        private DateTime _lastWipeUtc = DateTime.MinValue;

        public CareActionService(PetService pets, HealthService health, EvolutionService evo, GardenConfig cfg, ITimeProvider time,
                                 EventBus bus, ILog log = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _log = log ?? NullLog.Instance;
        }

        private PetNeeds Needs
        {
            get
            {
                _pets.Tick();
                return _pets.Current.Needs;
            }
        }

        /// <summary>Thả đồ ăn trúng thú. Đang ngủ hoặc đã no thì từ chối. Thú ốm: đồ ăn là bát cháo (1 việc chăm kỹ).</summary>
        public CareResult Feed()
        {
            if (!_pets.HasPet) return Done(CareKind.Feed, CareResult.Refused, "không có thú");
            if (_pets.Current.IsSleeping) return Done(CareKind.Feed, CareResult.Refused, "đang ngủ");
            if (Needs.Satiety >= _cfg.FullSatiety) return Done(CareKind.Feed, CareResult.Refused, "no rồi");
            bool ill = _health.IsIll;
            _pets.Care(NeedType.Satiety, _cfg.FeedSatiety);
            _pets.Care(NeedType.Mood, _cfg.FeedMood);
            if (ill) _health.MarkCarefulCare(CarefulCareKind.Porridge);
            _evo.AddTraining(TrainingAxis.Care, _pets.Config.Evolution.CareFeed);
            return Done(CareKind.Feed, CareResult.Ok, ill ? "cháo" : null);
        }

        /// <summary>Thú xuống hồ tắm. Thú ốm thì không chịu xuống nước (GDD G4).</summary>
        public CareResult Bathe()
        {
            if (!_pets.HasPet) return Done(CareKind.Bathe, CareResult.Refused, "không có thú");
            if (_health.IsIll) return Done(CareKind.Bathe, CareResult.Refused, "đang ốm, không chịu xuống nước");
            if (_pets.Current.IsSleeping) _pets.SetSleeping(false);
            _pets.Care(NeedType.Hygiene, _cfg.BatheHygiene);
            _pets.Care(NeedType.Mood, _cfg.BatheMood);
            _evo.AddTraining(TrainingAxis.Care, _pets.Config.Evolution.CareBathe);
            return Done(CareKind.Bathe, CareResult.Ok, null);
        }

        /// <summary>
        /// Chạm vào thú = vuốt ve (chỉ cộng vui sau mỗi khoảng hồi). Thú ốm: chạm = lau người bằng khăn ấm
        /// (cộng sạch, là 1 việc chăm kỹ), không đánh thức thú.
        /// </summary>
        public CareResult Pet()
        {
            if (!_pets.HasPet) return Done(CareKind.Pet, CareResult.Refused, "không có thú");
            DateTime now = _time.UtcNow;
            if (_health.IsIll)
            {
                if ((now - _lastWipeUtc).TotalSeconds >= _cfg.WipeCooldownSeconds)
                {
                    _lastWipeUtc = now;
                    _pets.Care(NeedType.Hygiene, _cfg.WipeHygiene);
                    _pets.Care(NeedType.Mood, _cfg.WipeMood);
                    _health.MarkCarefulCare(CarefulCareKind.Wipe);
                    _evo.AddTraining(TrainingAxis.Care, _pets.Config.Evolution.CareWipe);
                }
                return Done(CareKind.Wipe, CareResult.Ok, null);
            }
            if ((now - _lastPetMoodUtc).TotalSeconds >= _cfg.PetMoodCooldownSeconds)
            {
                _lastPetMoodUtc = now;
                _pets.Care(NeedType.Mood, _cfg.PetMood);
                _evo.AddTraining(TrainingAxis.Care, _pets.Config.Evolution.CarePet);
            }
            return Done(CareKind.Pet, CareResult.Ok, null);
        }

        /// <summary>
        /// Thú vào ổ ngủ. Còn tỉnh táo (năng lượng cao) thì không chịu ngủ. Thú ốm thì nằm nghỉ lúc nào cũng được
        /// và được đắp chăn (1 việc chăm kỹ).
        /// </summary>
        public CareResult Sleep()
        {
            if (!_pets.HasPet) return Done(CareKind.Sleep, CareResult.Refused, "không có thú");
            bool ill = _health.IsIll;
            if (!ill && Needs.Energy >= _cfg.NotSleepyAboveEnergy) return Done(CareKind.Sleep, CareResult.Refused, "chưa buồn ngủ");
            _pets.SetSleeping(true);
            if (ill) _health.MarkCarefulCare(CarefulCareKind.Blanket);
            _evo.AddTraining(TrainingAxis.Care, _pets.Config.Evolution.CareSleep);
            return Done(CareKind.Sleep, CareResult.Ok, ill ? "đắp chăn" : null);
        }

        /// <summary>Thả thú vào sân tập: đấm hình nộm (Sức mạnh). Ốm / quá mệt / hết lượt hôm nay thì từ chối.</summary>
        public CareResult Workout()
        {
            WorkoutResult r = _evo.TryWorkout();
            switch (r)
            {
                case WorkoutResult.Ok: return Done(CareKind.Workout, CareResult.Ok, null);
                case WorkoutResult.Sick: return Done(CareKind.Workout, CareResult.Refused, "đang ốm, không tập được");
                case WorkoutResult.TooTired: return Done(CareKind.Workout, CareResult.Refused, "mệt quá");
                case WorkoutResult.DailyLimit: return Done(CareKind.Workout, CareResult.Refused, "hôm nay tập đủ rồi");
                default: return Done(CareKind.Workout, CareResult.Refused, "không có thú");
            }
        }

        /// <summary>Thú chạy theo ngón tay được `meters` mét: cộng điểm Tốc độ (không bắn sự kiện, gọi thường xuyên).</summary>
        public void Run(float meters)
        {
            if (!_pets.HasPet || meters <= 0f || _health.IsIll) return;
            _evo.AddTraining(TrainingAxis.Speed, meters * _pets.Config.Evolution.SpeedPerMeterLed);
        }

        public void Wake()
        {
            if (!_pets.HasPet || !_pets.Current.IsSleeping) return;
            _pets.SetSleeping(false);
            Done(CareKind.Wake, CareResult.Ok, null);
        }

        private CareResult Done(CareKind kind, CareResult result, string reason)
        {
            string needs = "";
            if (_pets.HasPet)
            {
                PetNeeds n = _pets.Current.Needs;
                needs = " · no " + n.Satiety.ToString("0") + " / sạch " + n.Hygiene.ToString("0") +
                        " / năng lượng " + n.Energy.ToString("0") + " / vui " + n.Mood.ToString("0");
            }
            _log.Info("[PawFit] " + kind + (result == CareResult.Ok ? (reason != null ? " (" + reason + ")" : "") : " bị từ chối (" + reason + ")") +
                      needs);
            _bus.Publish(new CareDone(kind, result, reason));
            return result;
        }
    }
}
