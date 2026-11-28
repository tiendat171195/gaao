using System;
using System.Collections.Generic;
using PawFit.Core;

namespace PawFit.Pet
{
    /// <summary>Dữ liệu 1 con thú (lưu trên máy; có máy chủ riêng thì sao lưu thêm lên máy chủ).</summary>
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

        /// <summary>Sức khỏe: đồng hồ bỏ bê, giai đoạn bệnh, hồi phục (Phase 2.1).</summary>
        public PetHealth Health = new PetHealth();

        /// <summary>Lớn lên: giai đoạn tiến hóa, dạng Adult, điểm tập, CareScore (Phase 2.2).</summary>
        public PetGrowth Growth = new PetGrowth();

        /// <summary>Bậc rank cao nhất con thú này từng đạt khi đấu PvP (ghi lên bia mộ). None = chưa đấu.</summary>
        public RankTier BestTier;

        public double AgeDays(DateTime nowUtc) => (nowUtc - BornAtUtc).TotalDays;
    }

    /// <summary>File lưu game: 1 người = 1 thú (hoặc 1 quả trứng chờ nở) + các bia mộ.</summary>
    public sealed class GameSave
    {
        /// <summary>
        /// 2 = Phase 2.1 (thêm sức khỏe, trứng, bia mộ). 3 = Phase 5.1 (thêm ví Coin). 4 = Phase 5.2 (thêm phụ kiện + trang trí).
        /// 5 = Phase 4.3 (thêm rank Elo + mùa giải). 6 = Phase 7.2 (thêm Thám hiểm).
        /// 7 = Phase 7.3 (nhiệm vụ ngày, quà đăng nhập, điểm đào). File bản cũ vẫn đọc được.
        /// </summary>
        public const int CurrentVersion = 7;
        public int Version = CurrentVersion;

        /// <summary>Thú đang sống. null khi thú đã mất và trứng chưa nở.</summary>
        public PetData Pet;

        /// <summary>Trứng chờ nở (sau khi thú mất).</summary>
        public EggData Egg;

        public List<MemorialEntry> Memorials = new List<MemorialEntry>();

        /// <summary>
        /// Ví Coin của người chơi (còn nguyên khi thú mất). Cố ý không khởi tạo sẵn: file cũ chưa có ví
        /// đọc ra null, lúc đó mới cấp số Coin khởi đầu (xem <see cref="PetService.Wallet"/>).
        /// </summary>
        public WalletData Wallet;

        /// <summary>Phụ kiện + đồ trang trí đã mua (còn nguyên khi thú mất). File cũ chưa có thì tạo rỗng khi cần.</summary>
        public CosmeticsData Cosmetics;

        /// <summary>Rank Elo + mùa giải của người chơi (thú mất vẫn giữ). File cũ chưa có thì tạo khi cần (Elo khởi đầu).</summary>
        public RankData Rank;

        /// <summary>Tiến độ Thám hiểm (sao từng màn). File cũ chưa có thì tạo rỗng khi cần.</summary>
        public AdventureData Adventure;

        /// <summary>Nhiệm vụ ngày + chuỗi ngày đăng nhập + điểm đào hôm nay. File cũ chưa có thì tạo khi cần.</summary>
        public DailyData Daily;
    }

    // ---- Sự kiện (View lắng nghe qua EventBus) ----

    /// <summary>Có thú mới (nở lần đầu, hoặc nở từ trứng).</summary>
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
    /// Quản lý vòng đời thú: nở / nạp / lưu, nhu cầu chạy theo giờ thực, sức khỏe (bỏ bê -> bệnh -> chết),
    /// bia mộ + trứng thừa hưởng 1 trait, nở trứng. Logic C# thuần: không dùng UnityEngine, phụ thuộc nhận qua constructor.
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
        private bool _eggAnnounced;
        private readonly List<IPetStepper> _steppers = new List<IPetStepper>();

        public PetService(ITimeProvider time, IRandom rng, ISaveService save, EventBus bus, GameConfig cfg, ILog log = null)
        {
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _save = save ?? throw new ArgumentNullException(nameof(save));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _log = log ?? NullLog.Instance;
        }

        /// <summary>Gắn thêm 1 bước tính theo thời gian (vd tiến hóa) — chạy sau nhu cầu + sức khỏe ở mỗi bước.</summary>
        public void AddStepper(IPetStepper stepper)
        {
            if (stepper != null && !_steppers.Contains(stepper)) _steppers.Add(stepper);
        }

        /// <summary>Lần mở game này, thú đã vắng người chơi bao nhiêu giờ (0 nếu thú mới / không có thú).</summary>
        public double LastAwayHours { get; private set; }

        /// <summary>Thú đang sống (null nếu thú đã mất và trứng chưa nở).</summary>
        public PetData Current => _game?.Pet;
        public bool HasPet => Current != null;

        public GameConfig Config => _cfg;

        /// <summary>Trứng chờ nở (null nếu không có).</summary>
        public EggData Egg => _game?.Egg;

        /// <summary>Có trứng và đã tới lúc nở.</summary>
        public bool IsEggReady => Egg != null && _time.UtcNow >= Egg.ReadyAtUtc;

        /// <summary>Các bia mộ, cũ trước mới sau.</summary>
        public IReadOnlyList<MemorialEntry> Memorials =>
            _game?.Memorials != null ? (IReadOnlyList<MemorialEntry>)_game.Memorials : Array.Empty<MemorialEntry>();

        /// <summary>
        /// Ví Coin trong file lưu (EconomyService đọc / ghi qua đây, mỗi lần đổi thì gọi <see cref="Save"/>).
        /// Chưa có ví (người mới, hoặc file từ trước Phase 5.1) thì tạo ví với số Coin khởi đầu.
        /// null nếu chưa nạp file lưu (chưa gọi LoadOrCreate).
        /// </summary>
        public WalletData Wallet
        {
            get
            {
                if (_game == null) return null;
                return _game.Wallet ?? (_game.Wallet = new WalletData { Coins = _cfg.Economy.StartCoins });
            }
        }

        /// <summary>Rank của người chơi (RankService đọc / ghi qua đây). null nếu chưa nạp file lưu.</summary>
        public RankData Rank
        {
            get
            {
                if (_game == null) return null;
                return _game.Rank ?? (_game.Rank = new RankData { Elo = _cfg.Rank.StartElo, SeasonPeakElo = _cfg.Rank.StartElo });
            }
        }

        /// <summary>Phụ kiện + đồ trang trí đã mua (CosmeticsService đọc / ghi qua đây). null nếu chưa nạp file lưu.</summary>
        public CosmeticsData Cosmetics
        {
            get
            {
                if (_game == null) return null;
                CosmeticsData c = _game.Cosmetics ?? (_game.Cosmetics = new CosmeticsData());
                if (c.Accessories == null) c.Accessories = new List<AccessoryKind>();   // file sửa tay / lỗi: không để null
                if (c.Decor == null) c.Decor = new List<DecorKind>();
                return c;
            }
        }

        /// <summary>Tiến độ Thám hiểm (AdventureService đọc / ghi qua đây). null nếu chưa nạp file lưu.</summary>
        public AdventureData Adventure
        {
            get
            {
                if (_game == null) return null;
                AdventureData a = _game.Adventure ?? (_game.Adventure = new AdventureData());
                if (a.Stars == null) a.Stars = new List<int>();   // file sửa tay / lỗi: không để null
                return a;
            }
        }

        /// <summary>Nhiệm vụ ngày / quà đăng nhập / điểm đào (DailyService đọc / ghi qua đây). null nếu chưa nạp file lưu.</summary>
        public DailyData Daily
        {
            get
            {
                if (_game == null) return null;
                DailyData d = _game.Daily ?? (_game.Daily = new DailyData());
                if (d.Quests == null) d.Quests = new List<QuestState>();   // file sửa tay / lỗi: không để null
                if (d.Dug == null) d.Dug = new List<int>();
                return d;
            }
        }

        /// <summary>Mã DNA để khoe (PF-XXXX-XXXX-XXXX).</summary>
        public string DnaCode => HasPet ? DnaCodec.Encode(Current.Dna) : null;

        /// <summary>"Chăm đủ": cả 4 nhu cầu đạt ngưỡng cùng lúc (GDD G2).</summary>
        public bool IsFullyCared => HasPet && Current.Needs.AllAtLeast(_cfg.Needs.FullCareThreshold);

        /// <summary>Nạp file lưu; chưa có thì cho nở thú mới. Tự tính bù thời gian đã trôi qua khi tắt app.</summary>
        public void LoadOrCreate(string defaultName)
        {
            if (_save.TryLoad(SaveKey, out GameSave loaded) && loaded != null)
            {
                _game = loaded;
                Upgrade(_game);
                if (!HasPet && Egg == null)   // file không có thú lẫn trứng: nở thú mới, vẫn giữ bia mộ cũ
                {
                    CreateNewPet(defaultName);
                    return;
                }
                if (!HasPet)
                {
                    _log.Info("[PawFit] Nạp file: thú đã mất, có trứng chờ nở");
                    Tick();
                    return;
                }
                PetData pet = Current;
                double hoursAway = Math.Max(0.0, (_time.UtcNow - pet.LastSimulatedUtc).TotalHours);
                LastAwayHours = hoursAway;
                Tick();
                _log.Info("[PawFit] Nạp thú '" + pet.Name + "', vắng " + hoursAway.ToString("0.0") + " giờ");
                if (HasPet) _bus.Publish(new PetLoaded(Current, hoursAway));
                return;
            }
            CreateNewPet(defaultName);
        }

        /// <summary>Cho nở 1 con thú mới với DNA ngẫu nhiên (lần đầu chơi / công cụ test). Bia mộ cũ vẫn giữ.</summary>
        public PetData CreateNewPet(string name)
        {
            PetData pet = NewPet(name, DnaGenerator.Random(_rng), DnaGenerator.RandomHidden(_rng));
            if (_game == null) _game = new GameSave();
            _game.Pet = pet;
            _game.Egg = null;
            Save();
            _log.Info("[PawFit] Thú mới '" + pet.Name + "' DNA " + DnaCodec.Encode(pet.Dna));
            _bus.Publish(new PetCreated(pet));
            return pet;
        }

        /// <summary>Chạm trứng đã tới lúc: nở ra thú mới mang DNA của trứng. Chưa tới lúc thì trả null.</summary>
        public PetData HatchEgg(string name)
        {
            EggData egg = Egg;
            if (egg == null || HasPet || !IsEggReady) return null;
            PetData pet = NewPet(name, egg.Dna, egg.Hidden ?? DnaGenerator.RandomHidden(_rng));
            _game.Pet = pet;
            _game.Egg = null;
            Save();
            _log.Info("[PawFit] Trứng nở: '" + pet.Name + "' DNA " + DnaCodec.Encode(pet.Dna) +
                      " · thừa hưởng " + egg.Trait + " từ '" + egg.ParentName + "'");
            _bus.Publish(new PetCreated(pet));
            return pet;
        }

        /// <summary>
        /// Áp dụng thời gian trôi qua tới hiện tại: nhu cầu + sức khỏe, theo từng bước nhỏ (tắt app lâu vẫn tính đúng
        /// lúc thú bắt đầu bị bỏ bê). Gọi đều đặn (mỗi giây) và khi app quay lại. Đồng hồ chỉnh lùi thì bỏ qua.
        /// </summary>
        public void Tick()
        {
            if (_game == null) return;
            DateTime now = _time.UtcNow;
            PetData pet = Current;
            if (pet != null)
            {
                double hours = (now - pet.LastSimulatedUtc).TotalHours;
                if (hours > 0) Simulate(pet, hours, now);
            }
            if (Egg != null && !_eggAnnounced && now >= Egg.ReadyAtUtc)
            {
                _eggAnnounced = true;
                _bus.Publish(new EggReady(Egg));
            }
        }

        private void Simulate(PetData pet, double hours, DateTime now)
        {
            double step = Math.Max(0.001, _cfg.Health.SimulationStepHours);
            double left = hours;
            while (left > 1e-9)
            {
                double dt = Math.Min(step, left);
                left -= dt;
                DateTime end = left > 1e-9 ? now.AddHours(-left) : now;
                NeedsSimulator.Advance(pet.Needs, dt, pet.IsSleeping, _cfg.Needs);
                HealthStage before = pet.Health.Stage;
                HealthChange change = HealthRules.Step(pet.Health, pet.Needs, dt, end, _cfg);
                if (change == HealthChange.Died)
                {
                    pet.LastSimulatedUtc = end;
                    Die(pet);
                    return;
                }
                if (change != HealthChange.None) PublishHealth(pet, before, change);
                foreach (IPetStepper s in _steppers) s.Step(pet, dt, end);
            }
            pet.LastSimulatedUtc = now;
            _bus.Publish(new PetNeedsChanged(pet));
        }

        /// <summary>Chết vĩnh viễn: thêm bia mộ, đẻ trứng thừa hưởng 1 trait (nở sau 24h), thú biến mất khỏi file lưu.</summary>
        private void Die(PetData pet)
        {
            DateTime died = pet.Health.DiedAtUtc ?? _time.UtcNow;
            pet.Health.Stage = HealthStage.Dead;
            pet.Health.DiedAtUtc = died;
            var memorial = new MemorialEntry
            {
                PetId = pet.Id,
                Name = pet.Name,
                Dna = pet.Dna.Clone(),
                DnaCode = DnaCodec.Encode(pet.Dna),
                BornAtUtc = pet.BornAtUtc,
                DiedAtUtc = died,
                Stage = pet.Growth?.Stage ?? EvolutionStage.Baby,
                Form = pet.Growth?.Form ?? AdultForm.None,
                BestRank = pet.BestTier == RankTier.None ? null : pet.BestTier.ToString(),
            };
            _game.Memorials.Add(memorial);
            _game.Egg = EggFactory.Lay(pet, died, _cfg.Health.EggAfterDeathHours, _rng);
            _game.Pet = null;
            _eggAnnounced = false;
            Save();
            _log.Info("[PawFit] '" + pet.Name + "' đã mất sau " + memorial.AgeDaysAtDeath().ToString("0.0") +
                      " ngày. Trứng mới nở sau " + _cfg.Health.EggAfterDeathHours.ToString("0") + " giờ.");
            _bus.Publish(new PetDied(pet, memorial, _game.Egg));
        }

        /// <summary>Hành động chăm sóc (cho ăn, tắm, vuốt ve...): tăng 1 nhu cầu, tối đa 100.</summary>
        public void Care(NeedType need, float amount)
        {
            RequirePet();
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Chăm sóc chỉ làm tăng nhu cầu");
            Tick();
            if (!HasPet) return;   // vừa mất trong lúc tính bù
            PetData pet = Current;
            pet.Needs.Add(need, amount);
            HealthStage before = pet.Health.Stage;
            HealthChange change = HealthRules.AfterCare(pet.Health, pet.Needs, _time.UtcNow, _cfg);
            if (change != HealthChange.None) PublishHealth(pet, before, change);
            _bus.Publish(new PetNeedsChanged(pet));
        }

        /// <summary>Tiêu hao nhu cầu (vd tập luyện tốn năng lượng + no bụng). amount >= 0.</summary>
        public void Spend(NeedType need, float amount)
        {
            RequirePet();
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount), "Tiêu hao phải >= 0");
            Tick();
            if (!HasPet) return;
            Current.Needs.Add(need, -amount);
            _bus.Publish(new PetNeedsChanged(Current));
        }

        /// <summary>Cho thú ngủ / thức. Khi ngủ, năng lượng hồi thay vì giảm.</summary>
        public void SetSleeping(bool sleeping)
        {
            RequirePet();
            Tick();
            if (!HasPet || Current.IsSleeping == sleeping) return;
            Current.IsSleeping = sleeping;
            _bus.Publish(new PetNeedsChanged(Current));
        }

        /// <summary>Báo 1 thay đổi sức khỏe (dùng chung cho HealthService, vd khi bắt đầu hồi phục).</summary>
        public void PublishHealth(PetData pet, HealthStage before, HealthChange change)
        {
            _log.Info("[PawFit] Sức khỏe '" + pet.Name + "': " + before + " -> " + pet.Health.Stage + " (" + change + ")");
            _bus.Publish(new PetHealthChanged(pet, before, pet.Health.Stage, change));
        }

        // ------------------------------------------------------------------
        // CHỈ dùng cho công cụ test (Debug overlay) — vẫn đi qua đúng luật
        // ------------------------------------------------------------------

        /// <summary>Đặt thẳng 1 nhu cầu về giá trị bất kỳ 0..100.</summary>
        public void SetNeedForDebug(NeedType need, float value)
        {
            RequirePet();
            Tick();
            if (!HasPet) return;
            PetData pet = Current;
            pet.Needs.Set(need, value);
            HealthStage before = pet.Health.Stage;
            HealthChange change = HealthRules.CheckFullCare(pet.Health, pet.Needs, _cfg);
            if (change != HealthChange.None) PublishHealth(pet, before, change);
            _bus.Publish(new PetNeedsChanged(pet));
        }

        /// <summary>Đặt số giờ bị bỏ bê (để thử nhanh Mệt / Ốm / Nguy kịch / Chết). Bỏ dở hồi phục nếu có.</summary>
        public void SetNeglectForDebug(double hours)
        {
            RequirePet();
            Tick();
            if (!HasPet) return;
            PetData pet = Current;
            HealthConfig c = _cfg.Health;
            HealthStage before = pet.Health.Stage;
            pet.Health.NeglectHours = Math.Max(0.0, hours);
            pet.Health.Recovery = null;
            pet.Health.Marks = new CarefulCareMarks();
            if (hours >= c.DeathAfterHours)
            {
                pet.Health.DiedAtUtc = _time.UtcNow;
                Die(pet);
                return;
            }
            pet.Health.Stage = hours >= c.CriticalAfterHours ? HealthStage.Critical
                             : hours >= c.SickAfterHours ? HealthStage.Sick
                             : hours >= c.TiredAfterHours ? HealthStage.Tired
                             : HealthStage.Healthy;
            if (pet.Health.Stage != before) PublishHealth(pet, before, HealthChange.None);
        }

        /// <summary>Cho thú già thêm `days` ngày (thử tiến hóa nhanh). Giai đoạn mới được xét ở lần tính tiếp theo.</summary>
        public void AgeForDebug(double days)
        {
            RequirePet();
            Tick();
            if (!HasPet || days <= 0) return;
            Current.BornAtUtc = Current.BornAtUtc.AddDays(-days);
        }

        /// <summary>Cho trứng tới lúc nở ngay.</summary>
        public void MakeEggReadyForDebug()
        {
            if (Egg == null) return;
            Egg.ReadyAtUtc = _time.UtcNow;
            Tick();
        }

        public void Save()
        {
            if (_game != null) _save.Save(SaveKey, _game);
        }

        private PetData NewPet(string name, PetDna dna, HiddenGenes hidden)
        {
            DateTime now = _time.UtcNow;
            return new PetData
            {
                Id = Guid.NewGuid().ToString("N"),
                Name = PetNames.Normalize(name),
                Dna = dna,
                Hidden = hidden,
                BornAtUtc = now,
                LastSimulatedUtc = now,
                Needs = PetNeeds.Filled(_cfg.Needs.StartValue),
                Health = new PetHealth(),
                Growth = new PetGrowth(),
            };
        }

        /// <summary>Đọc file lưu bản cũ: bổ sung phần còn thiếu (bản 1 chưa có sức khỏe / trứng / bia mộ; bản 2 chưa có ví, bản 3 chưa có đồ trang trí — tạo khi cần).</summary>
        private static void Upgrade(GameSave g)
        {
            if (g.Memorials == null) g.Memorials = new List<MemorialEntry>();
            if (g.Pet != null)
            {
                if (g.Pet.Health == null) g.Pet.Health = new PetHealth();
                if (g.Pet.Health.Marks == null) g.Pet.Health.Marks = new CarefulCareMarks();
                if (g.Pet.Needs == null) g.Pet.Needs = PetNeeds.Filled(PetNeeds.Max);
                PetGrowth gr = g.Pet.Growth ?? (g.Pet.Growth = new PetGrowth());
                if (gr.Training == null) gr.Training = new TrainingVector();
                if (gr.TrainedToday == null) gr.TrainedToday = new TrainingVector();
                if (gr.CareHours == null) gr.CareHours = new List<float>();
                if (gr.SkillProgress == null || gr.SkillProgress.Length < 4)
                {
                    var sp = new int[4];   // file Phase 2.2 chỉ có số chiêu: coi như thuộc các chiêu đầu
                    for (int i = 0; i < Math.Min(4, gr.LearnedSkills); i++) sp[i] = 10;
                    gr.SkillProgress = sp;
                }
            }
            g.Version = GameSave.CurrentVersion;
        }

        private void RequirePet()
        {
            if (!HasPet) throw new InvalidOperationException("Chưa có thú");
        }
    }
}
