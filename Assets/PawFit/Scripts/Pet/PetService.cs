using System;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Dữ liệu 1 con thú (lưu trên máy; Phase 4.1 đồng bộ thêm lên Firestore).</summary>
    public sealed class PetData
    {
        public string Id;
        public string Name;
        public PetDna Dna;
        public HiddenGenes Hidden;
        public DateTime BornAtUtc;

        /// <summary>Thời điểm đã tính nhu cầu tới (để tính bù khi tắt app quay lại).</summary>
        public DateTime LastSimulatedUtc;

        public PetNeeds Needs;
        public bool IsSleeping;

        public double AgeDays(DateTime nowUtc) => (nowUtc - BornAtUtc).TotalDays;
    }

    /// <summary>File lưu game: 1 người = 1 thú.</summary>
    public sealed class GameSave
    {
        public const int CurrentVersion = 1;
        public int Version = CurrentVersion;
        public PetData Pet;
    }

    // ---- Sự kiện (View lắng nghe qua EventBus) ----

    /// <summary>Có thú mới (nở lần đầu).</summary>
    public readonly struct PetCreated
    {
        public readonly PetData Pet;
        public PetCreated(PetData pet) { Pet = pet; }
    }

    /// <summary>Đã nạp thú từ file lưu (đã tính bù thời gian vắng mặt).</summary>
    public readonly struct PetLoaded
    {
        public readonly PetData Pet;
        public readonly double HoursAway;
        public PetLoaded(PetData pet, double hoursAway) { Pet = pet; HoursAway = hoursAway; }
    }

    /// <summary>Nhu cầu hoặc trạng thái ngủ thay đổi.</summary>
    public readonly struct PetNeedsChanged
    {
        public readonly PetData Pet;
        public PetNeedsChanged(PetData pet) { Pet = pet; }
    }

    /// <summary>
    /// Quản lý con thú hiện tại: tạo / nạp / lưu, nhu cầu chạy theo giờ thực, hành động chăm sóc.
    /// Logic C# thuần: không dùng UnityEngine, mọi phụ thuộc nhận qua constructor.
    /// </summary>
    public sealed class PetService
    {
        public const string SaveKey = "game";

        private readonly ITimeProvider _time;
        private readonly IRandom _rng;
        private readonly ISaveService _save;
        private readonly EventBus _bus;
        private readonly GameConfig _cfg;
        private readonly ILog _log;
        private GameSave _game;

        public PetService(ITimeProvider time, IRandom rng, ISaveService save, EventBus bus, GameConfig cfg, ILog log = null)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _log = log ?? NullLog.Instance;
        }

        public PetData Current => _game?.Pet;
        public bool HasPet => Current != null;

        /// <summary>Mã DNA để khoe (PF-XXXX-XXXX-XXXX).</summary>
        public string DnaCode => HasPet ? DnaCodec.Encode(Current.Dna) : null;

        /// <summary>"Chăm đủ": cả 4 nhu cầu đạt ngưỡng cùng lúc (GDD G2).</summary>
        public bool IsFullyCared => HasPet && Current.Needs.AllAtLeast(_cfg.Needs.FullCareThreshold);

        /// <summary>Nạp file lưu; chưa có thì cho nở thú mới. Tự tính bù thời gian đã trôi qua khi tắt app.</summary>
        public void LoadOrCreate(string defaultName)
        {
            if (_save.TryLoad(SaveKey, out GameSave loaded) && loaded?.Pet != null)
            {
                _game = loaded;
                double hoursAway = Math.Max(0.0, (_time.UtcNow - Current.LastSimulatedUtc).TotalHours);
                Tick();
                _log.Info("[PawFit] Nạp thú '" + Current.Name + "', vắng " + hoursAway.ToString("0.0") + " giờ");
                _bus.Publish(new PetLoaded(Current, hoursAway));
                return;
            }
            CreateNewPet(defaultName);
        }

        /// <summary>Cho nở 1 con thú mới với DNA ngẫu nhiên (Gen 1).</summary>
        public PetData CreateNewPet(string name)
        {
            DateTime now = _time.UtcNow;
            var pet = new PetData
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = string.IsNullOrWhiteSpace(name) ? "Bé" : name.Trim(),
                Dna = DnaGenerator.Random(_rng),
                Hidden = DnaGenerator.RandomHidden(_rng),
                BornAtUtc = now,
                LastSimulatedUtc = now,
                Needs = PetNeeds.Filled(_cfg.Needs.StartValue),
            };
            _game = new GameSave { Pet = pet };
            Save();
            _log.Info("[PawFit] Thú mới '" + pet.Name + "' DNA " + DnaCodec.Encode(pet.Dna));
            _bus.Publish(new PetCreated(pet));
            return pet;
        }

        /// <summary>
        /// Áp dụng thời gian trôi qua tới hiện tại. Gọi đều đặn (mỗi giây) và khi app quay lại.
        /// Nếu đồng hồ máy bị chỉnh lùi thì bỏ qua (thú không "trẻ lại").
        /// </summary>
        public void Tick()
        {
            if (!HasPet) return;
            DateTime now = _time.UtcNow;
            PetData pet = Current;
            double hours = (now - pet.LastSimulatedUtc).TotalHours;
            if (hours <= 0) return;
            NeedsSimulator.Advance(pet.Needs, hours, pet.IsSleeping, _cfg.Needs);
            pet.LastSimulatedUtc = now;
            _bus.Publish(new PetNeedsChanged(pet));
        }

        /// <summary>Hành động chăm sóc (cho ăn, tắm, vuốt ve...): tăng 1 nhu cầu, tối đa 100.</summary>
        public void Care(NeedType need, float amount)
        {
            RequirePet();
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Chăm sóc chỉ làm tăng nhu cầu");
            Tick();
            Current.Needs.Add(need, amount);
            _bus.Publish(new PetNeedsChanged(Current));
        }

        /// <summary>Cho thú ngủ / thức. Khi ngủ, năng lượng hồi thay vì giảm.</summary>
        public void SetSleeping(bool sleeping)
        {
            RequirePet();
            Tick();
            if (Current.IsSleeping == sleeping) return;
            Current.IsSleeping = sleeping;
            _bus.Publish(new PetNeedsChanged(Current));
        }

        /// <summary>CHỈ dùng cho công cụ test (Debug overlay): đặt thẳng 1 nhu cầu về giá trị bất kỳ 0..100.</summary>
        public void SetNeedForDebug(NeedType need, float value)
        {
            RequirePet();
            Tick();
            Current.Needs.Set(need, value);
            _bus.Publish(new PetNeedsChanged(Current));
        }

        public void Save()
        {
            if (_game != null) _save.Save(SaveKey, _game);
        }

        private void RequirePet()
        {
            if (!HasPet) throw new InvalidOperationException("Chưa có thú");
        }
    }
}
