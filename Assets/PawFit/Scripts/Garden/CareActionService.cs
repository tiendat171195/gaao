using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    public enum CareKind { Feed, Bathe, Pet, Sleep, Wake }

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
    /// Luật chăm sóc trong vườn: cho ăn, tắm, vuốt ve, ngủ / dậy.
    /// Quyết định được hay không (vd no rồi thì từ chối) nằm ở đây, View chỉ chạy hiệu ứng theo kết quả.
    /// </summary>
    public sealed class CareActionService
    {
        private readonly PetService _pets;
        private readonly GardenConfig _cfg;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly ILog _log;
        private DateTime _lastPetMoodUtc = DateTime.MinValue;

        public CareActionService(PetService pets, GardenConfig cfg, ITimeProvider time, EventBus bus, ILog log = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
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

        /// <summary>Thả đồ ăn trúng thú. Đang ngủ hoặc đã no thì từ chối.</summary>
        public CareResult Feed()
        {
            if (_pets.Current.IsSleeping) return Done(CareKind.Feed, CareResult.Refused, "đang ngủ");
            if (Needs.Satiety >= _cfg.FullSatiety) return Done(CareKind.Feed, CareResult.Refused, "no rồi");
            _pets.Care(NeedType.Satiety, _cfg.FeedSatiety);
            _pets.Care(NeedType.Mood, _cfg.FeedMood);
            return Done(CareKind.Feed, CareResult.Ok, null);
        }

        /// <summary>Thú xuống hồ tắm.</summary>
        public CareResult Bathe()
        {
            if (_pets.Current.IsSleeping) _pets.SetSleeping(false);
            _pets.Care(NeedType.Hygiene, _cfg.BatheHygiene);
            _pets.Care(NeedType.Mood, _cfg.BatheMood);
            return Done(CareKind.Bathe, CareResult.Ok, null);
        }

        /// <summary>Chạm vào thú = vuốt ve. Luôn phản ứng, nhưng chỉ cộng vui sau mỗi khoảng hồi.</summary>
        public CareResult Pet()
        {
            DateTime now = _time.UtcNow;
            if ((now - _lastPetMoodUtc).TotalSeconds >= _cfg.PetMoodCooldownSeconds)
            {
                _lastPetMoodUtc = now;
                _pets.Care(NeedType.Mood, _cfg.PetMood);
            }
            return Done(CareKind.Pet, CareResult.Ok, null);
        }

        /// <summary>Thú vào ổ ngủ. Còn tỉnh táo (năng lượng cao) thì không chịu ngủ.</summary>
        public CareResult Sleep()
        {
            if (Needs.Energy >= _cfg.NotSleepyAboveEnergy) return Done(CareKind.Sleep, CareResult.Refused, "chưa buồn ngủ");
            _pets.SetSleeping(true);
            return Done(CareKind.Sleep, CareResult.Ok, null);
        }

        public void Wake()
        {
            if (!_pets.Current.IsSleeping) return;
            _pets.SetSleeping(false);
            Done(CareKind.Wake, CareResult.Ok, null);
        }

        private CareResult Done(CareKind kind, CareResult result, string reason)
        {
            PetNeeds n = _pets.Current.Needs;
            _log.Info("[PawFit] " + kind + (result == CareResult.Ok ? "" : " bị từ chối (" + reason + ")") +
                      " · no " + n.Satiety.ToString("0") + " / sạch " + n.Hygiene.ToString("0") +
                      " / năng lượng " + n.Energy.ToString("0") + " / vui " + n.Mood.ToString("0"));
            _bus.Publish(new CareDone(kind, result, reason));
            return result;
        }
    }
}
