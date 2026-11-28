using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>1 loại cây: lớn bao lâu, hái được mấy quả, giá gói hạt, bé ăn 1 quả thì được gì.</summary>
    public sealed class CropDef
    {
        public CropKind Kind;
        public string Name;

        /// <summary>Số giờ lớn tới lúc chín (đất khô). Đất ướt lớn nhanh hơn (FarmConfig.WaterSpeed).</summary>
        public float GrowHours;
        public int Yield;
        public int PackPrice;
        public int PackSeeds;

        /// <summary>Bé ăn 1 quả: cộng no / vui / năng lượng; có loại còn cho điểm tập (bé ốm thì không tập).</summary>
        public float Satiety, Mood, Energy;
        public TrainingAxis? Train;
        public float TrainPoints;

        /// <summary>Mô tả ngắn tác dụng (hiện ở quầy hạt giống / giỏ quả).</summary>
        public string Effect;
    }

    public sealed class FarmConfig
    {
        public int PlotCount = 3;

        /// <summary>Đất còn ẩm thì cây lớn nhanh gấp bấy nhiêu lần; mỗi lần tưới ẩm bấy nhiêu giờ (gieo xong là tưới luôn).</summary>
        public float WaterSpeed = 2f;
        public float WaterHours = 2f;

        /// <summary>Mở vườn rau lần đầu được tặng (chỉ số = CropKind): 3 hạt dâu tây + 2 hạt cà rốt để gieo thử ngay.</summary>
        public int[] StarterSeeds = { 3, 2, 0, 0 };

        /// <summary>Giỏ quả chứa tối đa bấy nhiêu quả mỗi loại (đầy thì hái tiếp không vào giỏ được).</summary>
        public int BasketMaxPerKind = 20;

        public readonly CropDef[] Crops =
        {
            new CropDef
            {
                Kind = CropKind.Strawberry, Name = CropNames.Of(CropKind.Strawberry), GrowHours = 1.5f, Yield = 3, PackPrice = 15, PackSeeds = 3,
                Satiety = 15f, Mood = 12f, Effect = "ngọt lịm: bé vui hẳn",
            },
            new CropDef
            {
                Kind = CropKind.Carrot, Name = CropNames.Of(CropKind.Carrot), GrowHours = 3f, Yield = 3, PackPrice = 20, PackSeeds = 3,
                Satiety = 25f, Energy = 12f, Mood = 3f, Effect = "giòn rụm: bé khỏe khoắn (+năng lượng)",
            },
            new CropDef
            {
                Kind = CropKind.Blueberry, Name = CropNames.Of(CropKind.Blueberry), GrowHours = 6f, Yield = 4, PackPrice = 30, PackSeeds = 3,
                Satiety = 12f, Mood = 6f, Train = TrainingAxis.Speed, TrainPoints = 2f, Effect = "bé nhanh nhẹn (tập Tốc độ)",
            },
            new CropDef
            {
                Kind = CropKind.Pumpkin, Name = CropNames.Of(CropKind.Pumpkin), GrowHours = 10f, Yield = 2, PackPrice = 40, PackSeeds = 2,
                Satiety = 40f, Mood = 5f, Train = TrainingAxis.Strength, TrainPoints = 3f, Effect = "no lâu, bé khỏe mạnh (tập Sức mạnh)",
            },
        };
    }

    /// <summary>Luống đất đang ở giai đoạn nào (View dựng hình theo đây).</summary>
    public enum PlotStage { Empty = 0, Seeded = 1, Sprout = 2, Growing = 3, Ripe = 4 }

    public enum FarmResult { Ok = 0, BadPlot = 1, NotEmpty = 2, NoSeed = 3, Empty = 4, NotRipe = 5, AlreadyWet = 6, Ripe = 7, NotEnoughCoins = 8, BasketFull = 9 }

    /// <summary>Vườn rau vừa đổi (gieo / tưới / hái / mua hạt / ăn quả). Plot = luống vừa đổi, -1 = hạt / giỏ quả.</summary>
    public readonly struct FarmChanged
    {
        public readonly int Plot;
        public FarmChanged(int plot) { Plot = plot; }
    }

    /// <summary>Vừa hái 1 luống: được `Count` quả `Kind` vào giỏ.</summary>
    public readonly struct CropHarvested
    {
        public readonly CropKind Kind;
        public readonly int Count;

        public CropHarvested(CropKind kind, int count)
        {
            Kind = kind;
            Count = count;
        }
    }

    /// <summary>
    /// Vườn rau (Phase 10.2, C# thuần): gieo hạt vào luống, cây lớn theo giờ thật (cả lúc tắt app), tưới thì lớn nhanh gấp đôi
    /// trong 2 giờ, chín thì hái vào giỏ quả cạnh chòi ăn; bé ăn quả thì được no + tác dụng riêng của từng loại
    /// (dâu tây: vui, cà rốt: năng lượng, việt quất: tập Tốc độ, bí ngô: no lâu + tập Sức mạnh). Hạt mua ở quầy hàng,
    /// săn quái cũng rơi hạt (Phase 10.3). Không có héo / chết cây: để lâu quả vẫn chờ bạn hái.
    /// Lớn = tính lại từ mốc thời gian đã lưu nên không cần lưu mỗi giây; giờ máy lùi thì cây đứng yên (không tụt).
    /// </summary>
    public sealed class FarmService : ISeedBag
    {
        private readonly PetService _pets;
        private readonly CareActionService _care;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly ICoinWallet _wallet;
        private readonly FarmConfig _cfg;
        private readonly ILog _log;

        public FarmService(PetService pets, CareActionService care, ITimeProvider time, EventBus bus, ICoinWallet wallet = null,
                           FarmConfig cfg = null, ILog log = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _care = care ?? throw new ArgumentNullException(nameof(care));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _wallet = wallet;
            _cfg = cfg ?? new FarmConfig();
            _log = log ?? NullLog.Instance;
        }

        public FarmConfig Config => _cfg;
        public int PlotCount => _cfg.PlotCount;
        public static readonly CropKind[] Kinds = (CropKind[])Enum.GetValues(typeof(CropKind));

        public CropDef Def(CropKind kind)
        {
            foreach (CropDef d in _cfg.Crops)
                if (d.Kind == kind) return d;
            throw new ArgumentOutOfRangeException(nameof(kind), kind, "Chưa có thông số loại cây này");
        }

        /// <summary>Dữ liệu vườn rau (đủ số luống, đủ ô hạt / giỏ; lần đầu tặng gói hạt khởi đầu).</summary>
        private FarmData Data
        {
            get
            {
                FarmData f = _pets.Farm;
                if (f == null) return null;
                while (f.Plots.Count < _cfg.PlotCount) f.Plots.Add(new PlotState());
                int kinds = Kinds.Length;
                while (f.Seeds.Count < kinds) f.Seeds.Add(0);
                while (f.Basket.Count < kinds) f.Basket.Add(0);
                if (!f.StarterGiven)
                {
                    f.StarterGiven = true;
                    for (int i = 0; i < kinds && i < _cfg.StarterSeeds.Length; i++) f.Seeds[i] += Math.Max(0, _cfg.StarterSeeds[i]);
                    _pets.Save();
                }
                return f;
            }
        }

        public int Seeds(CropKind kind) => Data?.Seeds[(int)kind] ?? 0;
        public int InBasket(CropKind kind) => Data?.Basket[(int)kind] ?? 0;

        public int SeedTotal
        {
            get
            {
                int n = 0;
                foreach (CropKind k in Kinds) n += Seeds(k);
                return n;
            }
        }

        public int BasketTotal
        {
            get
            {
                int n = 0;
                foreach (CropKind k in Kinds) n += InBasket(k);
                return n;
            }
        }

        // ------------------------------------------------------------------ luống đất

        /// <summary>Luống thứ i, đã tính lớn tới bây giờ. null nếu sai số luống / chưa nạp file lưu.</summary>
        public PlotState Plot(int i)
        {
            FarmData f = Data;
            if (f == null || i < 0 || i >= _cfg.PlotCount) return null;
            PlotState p = f.Plots[i];
            Grow(p, _time.UtcNow);
            return p;
        }

        public bool IsRipe(int i)
        {
            PlotState p = Plot(i);
            return p != null && p.Planted && p.GrownHours >= Def(p.Crop).GrowHours - 1e-6;
        }

        public bool IsWet(int i)
        {
            PlotState p = Plot(i);
            return p != null && p.Planted && !IsRipe(i) && p.WetUntilUtc > _time.UtcNow;
        }

        /// <summary>0..1: cây lớn được bao nhiêu phần.</summary>
        public float Progress01(int i)
        {
            PlotState p = Plot(i);
            if (p == null || !p.Planted) return 0f;
            return (float)Math.Min(1.0, p.GrownHours / Math.Max(1e-6, Def(p.Crop).GrowHours));
        }

        public PlotStage Stage(int i)
        {
            PlotState p = Plot(i);
            if (p == null || !p.Planted) return PlotStage.Empty;
            float k = Progress01(i);
            if (k >= 1f - 1e-6f) return PlotStage.Ripe;
            return k < 0.15f ? PlotStage.Seeded : k < 0.5f ? PlotStage.Sprout : PlotStage.Growing;
        }

        public bool AnyRipe
        {
            get
            {
                for (int i = 0; i < _cfg.PlotCount; i++)
                    if (IsRipe(i)) return true;
                return false;
            }
        }

        /// <summary>Lúc luống này chín (UTC) nếu không tưới thêm: đất còn ẩm thì lớn nhanh tới lúc khô. null = luống trống.</summary>
        public DateTime? RipeAtUtc(int i)
        {
            PlotState p = Plot(i);
            if (p == null || !p.Planted) return null;
            DateTime now = _time.UtcNow;
            double left = Def(p.Crop).GrowHours - p.GrownHours;
            if (left <= 1e-9) return now;
            double wet = Math.Max(0.0, (p.WetUntilUtc - now).TotalHours);
            double speed = Math.Max(1.0, _cfg.WaterSpeed);
            if (left <= wet * speed) return now.AddHours(left / speed);
            return now.AddHours(wet + (left - wet * speed));
        }

        /// <summary>Còn bao lâu nữa luống này chín (theo giờ của game). null = luống trống.</summary>
        public TimeSpan? TimeToRipe(int i)
        {
            DateTime? at = RipeAtUtc(i);
            if (!at.HasValue) return null;
            TimeSpan left = at.Value - _time.UtcNow;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        /// <summary>Luống chín sớm nhất trong số luống đang lớn (để hẹn thông báo "quả chín rồi"). null = không có luống nào đang lớn.</summary>
        public DateTime? NextRipeUtc
        {
            get
            {
                DateTime? best = null;
                for (int i = 0; i < _cfg.PlotCount; i++)
                {
                    if (IsRipe(i)) continue;
                    DateTime? t = RipeAtUtc(i);
                    if (t.HasValue && (!best.HasValue || t.Value < best.Value)) best = t;
                }
                return best;
            }
        }

        /// <summary>Gieo 1 hạt vào luống trống (gieo xong tưới luôn: đất ẩm WaterHours giờ).</summary>
        public FarmResult Plant(int i, CropKind kind)
        {
            PlotState p = Plot(i);
            if (p == null) return FarmResult.BadPlot;
            if (p.Planted) return FarmResult.NotEmpty;
            FarmData f = Data;
            if (f.Seeds[(int)kind] <= 0) return FarmResult.NoSeed;
            DateTime now = _time.UtcNow;
            f.Seeds[(int)kind]--;
            p.Planted = true;
            p.Crop = kind;
            p.GrownHours = 0;
            p.UpdatedUtc = now;
            p.WetUntilUtc = now.AddHours(_cfg.WaterHours);
            Changed(i, "gieo " + Def(kind).Name + " ở luống " + (i + 1));
            return FarmResult.Ok;
        }

        /// <summary>Tưới luống đang lớn mà đất đã khô: lớn nhanh gấp WaterSpeed lần trong WaterHours giờ tới.</summary>
        public FarmResult Water(int i)
        {
            PlotState p = Plot(i);
            if (p == null) return FarmResult.BadPlot;
            if (!p.Planted) return FarmResult.Empty;
            if (IsRipe(i)) return FarmResult.Ripe;
            DateTime now = _time.UtcNow;
            if (p.WetUntilUtc > now) return FarmResult.AlreadyWet;
            p.WetUntilUtc = now.AddHours(_cfg.WaterHours);
            Changed(i, "tưới luống " + (i + 1));
            return FarmResult.Ok;
        }

        /// <summary>Hái luống đã chín: quả vào giỏ (giỏ có trần mỗi loại), luống trống lại để gieo tiếp.</summary>
        public FarmResult Harvest(int i, out int count)
        {
            count = 0;
            PlotState p = Plot(i);
            if (p == null) return FarmResult.BadPlot;
            if (!p.Planted) return FarmResult.Empty;
            if (!IsRipe(i)) return FarmResult.NotRipe;
            FarmData f = Data;
            int k = (int)p.Crop;
            if (f.Basket[k] >= _cfg.BasketMaxPerKind) return FarmResult.BasketFull;
            CropDef def = Def(p.Crop);
            count = Math.Min(def.Yield, _cfg.BasketMaxPerKind - f.Basket[k]);
            f.Basket[k] += count;
            f.Harvests++;
            p.Planted = false;
            p.GrownHours = 0;
            Changed(i, "hái " + count + " " + def.Name + " ở luống " + (i + 1));
            _bus.Publish(new CropHarvested(def.Kind, count));
            return FarmResult.Ok;
        }

        // ------------------------------------------------------------------ hạt giống + giỏ quả

        /// <summary>Mua 1 gói hạt ở quầy hàng.</summary>
        public FarmResult BuySeeds(CropKind kind)
        {
            FarmData f = Data;
            if (f == null || _wallet == null) return FarmResult.NotEnoughCoins;
            CropDef def = Def(kind);
            if (!_wallet.TrySpend(def.PackPrice, "gói hạt " + def.Name)) return FarmResult.NotEnoughCoins;
            f.Seeds[(int)kind] += def.PackSeeds;
            Changed(-1, "mua " + def.PackSeeds + " hạt " + def.Name);
            return FarmResult.Ok;
        }

        /// <summary>Thêm hạt (quái rừng rơi ra khi săn, Phase 10.3).</summary>
        public void AddSeeds(CropKind kind, int count)
        {
            FarmData f = Data;
            if (f == null || count <= 0) return;
            f.Seeds[(int)kind] += count;
            Changed(-1, "nhận " + count + " hạt " + Def(kind).Name);
        }

        /// <summary>Bé ăn được 1 quả loại này không (để báo lý do trước khi bé chạy ra giỏ). why = lý do không ăn được.</summary>
        public bool CanEat(CropKind kind, out string why)
        {
            why = null;
            if (InBasket(kind) <= 0) why = "giỏ hết " + Def(kind).Name + " rồi";
            else if (!_pets.HasPet) why = "chưa có bé";
            else if (_pets.Current.IsSleeping) why = "bé đang ngủ";
            else if (_pets.Current.Needs.Satiety >= _care.FullSatiety) why = "bé no rồi";
            return why == null;
        }

        /// <summary>Bé ăn 1 quả trong giỏ (tính là 1 lần cho ăn: nhiệm vụ ngày, mẹo, điểm Chăm sóc). Bé không chịu ăn thì quả vẫn còn.</summary>
        public CareResult Eat(CropKind kind)
        {
            FarmData f = Data;
            if (f == null || f.Basket[(int)kind] <= 0) return CareResult.Refused;
            CropDef d = Def(kind);
            CareResult r = _care.FeedFruit(d.Satiety, d.Mood, d.Energy, d.Train, d.TrainPoints, d.Name);
            if (r != CareResult.Ok) return r;
            f.Basket[(int)kind]--;
            Changed(-1, "bé ăn " + d.Name);
            return r;
        }

        // ------------------------------------------------------------------ bảng DEV

        /// <summary>Bảng DEV: mọi luống đang lớn chín ngay.</summary>
        public void RipenAllForDebug()
        {
            FarmData f = Data;
            if (f == null) return;
            for (int i = 0; i < _cfg.PlotCount; i++)
            {
                PlotState p = Plot(i);
                if (p == null || !p.Planted) continue;
                p.GrownHours = Def(p.Crop).GrowHours;
            }
            Changed(-1, "DEV: chín hết");
        }

        public void AddSeedsForDebug(int each)
        {
            FarmData f = Data;
            if (f == null) return;
            foreach (CropKind k in Kinds) f.Seeds[(int)k] += each;
            Changed(-1, "DEV: +" + each + " hạt mỗi loại");
        }

        // ------------------------------------------------------------------

        /// <summary>Tính lớn từ lần cuối tới `now`: phần thời gian đất còn ẩm lớn nhanh gấp WaterSpeed lần.</summary>
        private void Grow(PlotState p, DateTime now)
        {
            if (!p.Planted || now <= p.UpdatedUtc) return;   // giờ máy lùi: cây đứng yên, không tụt
            double dt = (now - p.UpdatedUtc).TotalHours;
            double wet = Math.Max(0.0, Math.Min(dt, (p.WetUntilUtc - p.UpdatedUtc).TotalHours));
            double grow = (dt - wet) + wet * Math.Max(1.0, _cfg.WaterSpeed);
            p.GrownHours = Math.Min(Def(p.Crop).GrowHours, p.GrownHours + grow);
            p.UpdatedUtc = now;
        }

        private void Changed(int plot, string what)
        {
            _pets.Save();
            _log.Info("[PawFit] Vườn rau: " + what);
            _bus.Publish(new FarmChanged(plot));
        }
    }
}
