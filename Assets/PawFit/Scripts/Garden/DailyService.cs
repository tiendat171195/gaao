using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>Số liệu nhiệm vụ ngày / quà đăng nhập / điểm đào — GDD G16 (chỉnh ở đây, không phải sửa code).</summary>
    public sealed class DailyConfig
    {
        /// <summary>Xong cả 3 nhiệm vụ hôm nay thì được thêm bấy nhiêu Coin.</summary>
        public int AllDoneBonusCoins = 40;

        /// <summary>Quà mở game ngày 1..7 của chuỗi (sang ngày 8 quay lại ngày 1). Bỏ 1 ngày thì chuỗi về ngày 1.</summary>
        public int[] StreakCoins = { 20, 25, 30, 35, 40, 50, 80 };

        /// <summary>Mỗi ngày có bấy nhiêu điểm đào kho báu trong vườn; thú phải đứng cách điểm đào ≤ DigReach mét mới đào được.</summary>
        public int DigSpotsPerDay = 2;
        public float DigReach = 1.6f;

        /// <summary>Đào được: phần lớn là Coin; đôi khi là bánh (no + vui) hoặc nước tăng lực (năng lượng), kèm ít Coin.</summary>
        public int DigCoinsMin = 10;
        public int DigCoinsMax = 25;
        public float DigTreatChance = 0.15f;
        public float DigEnergyChance = 0.15f;
        public float DigTreatSatiety = 15f;
        public float DigTreatMood = 15f;
        public float DigEnergy = 15f;
        public int DigSideCoins = 5;

        /// <summary>Nhiệm vụ vuốt ve: 2 lần vuốt cách nhau ít nhất bấy nhiêu giây mới tính (như lúc bé được cộng vui).</summary>
        public float CuddleGapSeconds = 4f;
    }

    public enum DigPrize { Coins = 0, Treat = 1, Energy = 2 }

    /// <summary>Kết quả đào 1 điểm kho báu.</summary>
    public struct DigResult
    {
        public bool Ok;

        /// <summary>Vì sao chưa đào được (Ok = false).</summary>
        public string Reason;

        public DigPrize Prize;
        public int Coins;
    }

    /// <summary>Nhiệm vụ / quà / điểm đào vừa đổi (View cập nhật cờ hòm thư + ụ đất kho báu).</summary>
    public readonly struct DailyChanged
    {
    }

    /// <summary>
    /// Việc mỗi ngày (Phase 7.3, C# thuần) — lý do quay lại game mỗi ngày:
    ///  * Quà mở game theo chuỗi ngày liên tiếp (ngày 7 nhiều nhất), nhận ở hòm thư.
    ///  * 3 nhiệm vụ: 1 việc chăm bé + 1 việc đấu + 1 việc khám phá / chăm; đếm qua sự kiện của game; nhận Coin ở hòm thư,
    ///    xong cả 3 thì thêm thưởng.
    ///  * Vài điểm đào kho báu trong vườn (đổi chỗ mỗi ngày): dắt bé tới đào được Coin / bánh / nước tăng lực.
    /// Ngày tính theo giờ trên máy (như lượt quảng cáo). Bộ nhiệm vụ + điểm đào + phần thưởng đào cố định theo (ngày, người chơi):
    /// mở lại game không đổi được.
    /// </summary>
    public sealed class DailyService : IDisposable
    {
        private readonly struct QuestDef
        {
            public readonly QuestKind Kind;
            public readonly int Target;
            public readonly int Coins;

            public QuestDef(QuestKind kind, int target, int coins)
            {
                Kind = kind;
                Target = target;
                Coins = coins;
            }
        }

        private static readonly QuestDef[] CareQuests =
        {
            new QuestDef(QuestKind.Feed, 3, 15), new QuestDef(QuestKind.Bathe, 1, 15),
            new QuestDef(QuestKind.Cuddle, 5, 10), new QuestDef(QuestKind.Workout, 1, 15),
        };

        /// <summary>Có đồ chơi thì thêm vào nhóm việc chăm (Phase 8.3).</summary>
        private static readonly QuestDef PlayQuest = new QuestDef(QuestKind.Play, 3, 15);

        private static readonly QuestDef[] BattleQuests =
        {
            new QuestDef(QuestKind.WinAdventure, 2, 25), new QuestDef(QuestKind.AdventureStar, 1, 25),
            new QuestDef(QuestKind.WinBattle, 2, 20), new QuestDef(QuestKind.PlayPvp, 1, 20),
        };

        private readonly PetService _pets;
        private readonly ITimeProvider _time;
        private readonly EventBus _bus;
        private readonly ICoinWallet _wallet;
        private readonly GardenLayout _layout;
        private readonly DailyConfig _cfg;
        private readonly Func<DateTime, DateTime> _toLocal;
        private readonly Func<bool> _starsLeft;
        private readonly Func<bool> _hasToys;
        private readonly ILog _log;
        private readonly List<IDisposable> _subs = new List<IDisposable>();
        private readonly List<Vector2> _spots = new List<Vector2>();
        private string _spotsDay;
        private DateTime _lastCuddleUtc = DateTime.MinValue;

        /// <param name="toLocal">Đổi giờ UTC ra giờ trên máy để tính "ngày" (mặc định ToLocalTime).</param>
        /// <param name="starsLeft">Thám hiểm còn sao để kiếm không (hết sao thì không ra nhiệm vụ "kiếm sao mới"). null = còn.</param>
        /// <param name="hasToys">Người chơi có đồ chơi chưa (có thì có thể ra nhiệm vụ "chơi với bé"). null = chưa.</param>
        public DailyService(PetService pets, ITimeProvider time, EventBus bus, ICoinWallet wallet, GardenLayout layout,
                            DailyConfig cfg = null, Func<DateTime, DateTime> toLocal = null, Func<bool> starsLeft = null, ILog log = null,
                            Func<bool> hasToys = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _bus = bus ?? throw new ArgumentNullException(nameof(bus));
            _wallet = wallet;
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            _cfg = cfg ?? new DailyConfig();
            _toLocal = toLocal ?? (utc => utc.ToLocalTime());
            _starsLeft = starsLeft;
            _hasToys = hasToys;
            _log = log;
            _subs.Add(bus.Subscribe<PlayDone>(e => Count(QuestKind.Play, 1)));
            _subs.Add(bus.Subscribe<CareDone>(OnCare));
            _subs.Add(bus.Subscribe<AdventureWon>(e =>
            {
                Count(QuestKind.WinAdventure, 1);
                if (e.NewStars > 0) Count(QuestKind.AdventureStar, e.NewStars);
            }));
            _subs.Add(bus.Subscribe<BattleEnded>(e =>
            {
                if (e.Won && e.Mode != 0) Count(QuestKind.WinBattle, 1);   // 0 = Huấn luyện: không tính
                if (e.Mode == 2) Count(QuestKind.PlayPvp, 1);
            }));
        }

        public DailyConfig Config => _cfg;

        private DailyData Data => _pets.Daily;

        public string Today => DayKey(_time.UtcNow);

        public IReadOnlyList<QuestState> Quests
        {
            get
            {
                EnsureToday();
                DailyData d = Data;
                return d != null ? (IReadOnlyList<QuestState>)d.Quests : Array.Empty<QuestState>();
            }
        }

        // ------------------------------------------------------------------ quà mở game theo chuỗi ngày

        /// <summary>Đang ở ngày thứ mấy của chuỗi mở game liên tiếp (1..).</summary>
        public int Streak
        {
            get
            {
                EnsureToday();
                return Data?.Streak ?? 0;
            }
        }

        /// <summary>Ngày trong vòng 7 ngày quà (1..7).</summary>
        public int GiftDay => Streak <= 0 ? 1 : (Streak - 1) % Math.Max(1, _cfg.StreakCoins.Length) + 1;

        public int GiftCoins => _cfg.StreakCoins.Length == 0 ? 0 : _cfg.StreakCoins[GiftDay - 1];

        public bool GiftReady
        {
            get
            {
                EnsureToday();
                DailyData d = Data;
                return d != null && !d.GiftClaimed && GiftCoins > 0;
            }
        }

        /// <summary>Nhận quà hôm nay. Trả số Coin nhận (0 nếu đã nhận rồi).</summary>
        public int ClaimGift()
        {
            if (!GiftReady) return 0;
            int coins = GiftCoins;
            Data.GiftClaimed = true;
            Pay(coins, "quà ngày " + GiftDay);
            return coins;
        }

        // ------------------------------------------------------------------ nhiệm vụ

        public bool CanClaim(int index)
        {
            IReadOnlyList<QuestState> q = Quests;
            return index >= 0 && index < q.Count && q[index].Done && !q[index].Claimed;
        }

        /// <summary>Nhận thưởng nhiệm vụ đã xong. Trả số Coin nhận (0 nếu chưa xong / đã nhận).</summary>
        public int Claim(int index)
        {
            if (!CanClaim(index)) return 0;
            QuestState q = Data.Quests[index];
            q.Claimed = true;
            Pay(q.Coins, "nhiệm vụ " + q.Kind);
            return q.Coins;
        }

        public int DoneCount
        {
            get
            {
                int n = 0;
                foreach (QuestState q in Quests)
                    if (q.Done) n++;
                return n;
            }
        }

        /// <summary>Xong cả 3 nhiệm vụ mà chưa nhận thưởng thêm.</summary>
        public bool BonusReady
        {
            get
            {
                IReadOnlyList<QuestState> q = Quests;
                return q.Count > 0 && DoneCount == q.Count && !Data.BonusClaimed;
            }
        }

        public int ClaimBonus()
        {
            if (!BonusReady) return 0;
            Data.BonusClaimed = true;
            Pay(_cfg.AllDoneBonusCoins, "xong cả nhiệm vụ ngày");
            return _cfg.AllDoneBonusCoins;
        }

        /// <summary>Có quà / thưởng nào chờ nhận ở hòm thư không (cờ hòm thư dựng lên).</summary>
        public bool HasSomethingToClaim
        {
            get
            {
                if (GiftReady || BonusReady) return true;
                for (int i = 0; i < Quests.Count; i++)
                    if (CanClaim(i)) return true;
                return false;
            }
        }

        public static string QuestText(QuestState q)
        {
            int t = q.Target;
            switch (q.Kind)
            {
                case QuestKind.Feed: return "Cho bé ăn " + t + " lần";
                case QuestKind.Bathe: return "Tắm cho bé " + t + " lần";
                case QuestKind.Cuddle: return "Vuốt ve bé " + t + " lần";
                case QuestKind.Workout: return "Cho bé tập ở sân tập " + t + " lần";
                case QuestKind.WinAdventure: return "Thắng " + t + " trận Thám hiểm";
                case QuestKind.AdventureStar: return "Kiếm " + t + " sao mới ở Thám hiểm";
                case QuestKind.WinBattle: return "Thắng " + t + " trận bất kỳ (hình nộm, PvP, Thám hiểm)";
                case QuestKind.PlayPvp: return "Đấu " + t + " trận PvP ở đấu trường";
                case QuestKind.Play: return "Chơi với bé " + t + " lần (bóng, lông vũ, bong bóng)";
                default: return "Đào " + t + " kho báu trong vườn";
            }
        }

        // ------------------------------------------------------------------ điểm đào kho báu

        /// <summary>Điểm đào của hôm nay (cả điểm đã đào; xem <see cref="IsDug"/>).</summary>
        public IReadOnlyList<Vector2> DigSpots
        {
            get
            {
                EnsureToday();
                return _spots;
            }
        }

        public bool IsDug(int index)
        {
            EnsureToday();
            DailyData d = Data;
            return d != null && d.Dug.Contains(index);
        }

        public int DigsLeft
        {
            get
            {
                int n = 0;
                for (int i = 0; i < DigSpots.Count; i++)
                    if (!IsDug(i)) n++;
                return n;
            }
        }

        /// <summary>Bé (đang đứng ở `petPos`) đào điểm `index`. Phải đứng đủ gần, còn thức, chưa đào điểm đó hôm nay.</summary>
        public DigResult Dig(int index, Vector2 petPos)
        {
            EnsureToday();
            if (index < 0 || index >= _spots.Count) return Fail("không có kho báu ở đây");
            if (IsDug(index)) return Fail("đã đào rồi");
            if (!_pets.HasPet) return Fail("không có thú");
            if (_pets.Current.IsSleeping) return Fail("bé đang ngủ");
            if (Vector2.Distance(petPos, _spots[index]) > _cfg.DigReach) return Fail("bé đứng xa quá");
            Data.Dug.Add(index);
            var rng = new SeededRandom(Hash(Today) ^ Data.Salt ^ (0x9E37U * (uint)(index + 1)), 23UL);
            double roll = rng.NextDouble();
            var r = new DigResult { Ok = true };
            if (roll < _cfg.DigTreatChance)
            {
                r.Prize = DigPrize.Treat;
                r.Coins = _cfg.DigSideCoins;
                _pets.Care(NeedType.Satiety, _cfg.DigTreatSatiety);
                _pets.Care(NeedType.Mood, _cfg.DigTreatMood);
            }
            else if (roll < _cfg.DigTreatChance + _cfg.DigEnergyChance)
            {
                r.Prize = DigPrize.Energy;
                r.Coins = _cfg.DigSideCoins;
                _pets.Care(NeedType.Energy, _cfg.DigEnergy);
            }
            else
            {
                r.Prize = DigPrize.Coins;
                r.Coins = rng.Range(_cfg.DigCoinsMin, _cfg.DigCoinsMax + 1);
            }
            Count(QuestKind.Dig, 1);   // lưu + báo đổi trong Count / Pay
            Pay(r.Coins, "đào kho báu");
            return r;
        }

        // ------------------------------------------------------------------ công cụ test (bảng DEV)

        /// <summary>Coi như hôm qua mới mở game (để thử sang ngày mới: chuỗi +1, nhiệm vụ + điểm đào mới).</summary>
        public void StartNewDayForDebug()
        {
            DailyData d = Data;
            if (d == null) return;
            string yesterday = DayKey(_time.UtcNow.AddDays(-1));
            d.Day = yesterday;
            d.LastLoginDay = yesterday;
            _spotsDay = null;
            EnsureToday();
        }

        /// <summary>Đánh dấu xong hết nhiệm vụ hôm nay (chưa nhận thưởng).</summary>
        public void CompleteAllForDebug()
        {
            foreach (QuestState q in Quests) q.Progress = q.Target;
            Changed();
        }

        public void Dispose()
        {
            foreach (IDisposable s in _subs) s.Dispose();
            _subs.Clear();
        }

        // ------------------------------------------------------------------

        private void OnCare(CareDone e)
        {
            if (e.Result != CareResult.Ok) return;
            switch (e.Kind)
            {
                case CareKind.Feed: Count(QuestKind.Feed, 1); break;
                case CareKind.Bathe: Count(QuestKind.Bathe, 1); break;
                case CareKind.Workout: Count(QuestKind.Workout, 1); break;
                case CareKind.Pet:
                    DateTime now = _time.UtcNow;
                    if ((now - _lastCuddleUtc).TotalSeconds < _cfg.CuddleGapSeconds) return;
                    _lastCuddleUtc = now;
                    Count(QuestKind.Cuddle, 1);
                    break;
            }
        }

        /// <summary>Cộng tiến độ cho nhiệm vụ loại `kind` của hôm nay (nếu có).</summary>
        private void Count(QuestKind kind, int amount)
        {
            EnsureToday();
            DailyData d = Data;
            if (d == null || amount <= 0) return;
            bool changed = false;
            foreach (QuestState q in d.Quests)
            {
                if (q.Kind != kind || q.Done) continue;
                q.Progress = Math.Min(q.Target, q.Progress + amount);
                changed = true;
                if (q.Done) _log?.Info("[PawFit] Xong nhiệm vụ ngày: " + QuestText(q));
            }
            if (changed) Changed();
        }

        private void Pay(int coins, string reason)
        {
            if (coins > 0) _wallet?.Add(coins, reason);
            Changed();
        }

        private void Changed()
        {
            _pets.Save();
            _bus.Publish(new DailyChanged());
        }

        private static DigResult Fail(string reason) => new DigResult { Ok = false, Reason = reason };

        /// <summary>Sang ngày mới (hoặc lần đầu): cập nhật chuỗi mở game, ra bộ nhiệm vụ + điểm đào mới.</summary>
        private void EnsureToday()
        {
            DailyData d = Data;
            if (d == null) return;
            string today = DayKey(_time.UtcNow);
            bool changed = false;
            if (d.Salt == 0)
            {
                d.Salt = (uint)Guid.NewGuid().GetHashCode() | 1U;   // riêng mỗi người chơi; khác 0
                changed = true;
            }
            if (d.LastLoginDay != today)
            {
                string yesterday = DayKey(_time.UtcNow.AddDays(-1));
                d.Streak = d.LastLoginDay == yesterday ? d.Streak + 1 : 1;
                d.LastLoginDay = today;
                d.GiftClaimed = false;
                changed = true;
            }
            if (d.Day != today)
            {
                d.Day = today;
                d.Quests.Clear();
                d.Quests.AddRange(RollQuests(today, d.Salt));
                d.BonusClaimed = false;
                d.Dug.Clear();
                changed = true;
            }
            if (_spotsDay != today)
            {
                _spotsDay = today;
                RollSpots(today, d.Salt);
            }
            if (!changed) return;
            _pets.Save();
            _bus.Publish(new DailyChanged());
        }

        /// <summary>1 việc chăm + 1 việc đấu + (đào kho báu hoặc 1 việc chăm khác), cố định theo ngày + người chơi.</summary>
        private List<QuestState> RollQuests(string day, uint salt)
        {
            var rng = new SeededRandom(Hash(day) ^ salt, 11UL);
            var list = new List<QuestState>();
            var cares = new List<QuestDef>(CareQuests);
            if (_hasToys != null && _hasToys()) cares.Add(PlayQuest);
            int care = rng.Next(cares.Count);
            list.Add(New(cares[care]));
            var battles = new List<QuestDef>(BattleQuests);
            if (_starsLeft != null && !_starsLeft()) battles.RemoveAll(q => q.Kind == QuestKind.AdventureStar);
            list.Add(New(battles[rng.Next(battles.Count)]));
            if (rng.NextDouble() < 0.6) list.Add(New(new QuestDef(QuestKind.Dig, Math.Max(1, _cfg.DigSpotsPerDay), 15)));
            else list.Add(New(cares[(care + 1 + rng.Next(cares.Count - 1)) % cares.Count]));
            return list;
        }

        private static QuestState New(QuestDef d) => new QuestState { Kind = d.Kind, Target = d.Target, Coins = d.Coins };

        private void RollSpots(string day, uint salt)
        {
            _spots.Clear();
            var pool = new List<Vector2>(_layout.DigSpots);
            var rng = new SeededRandom(Hash(day) ^ salt, 17UL);
            for (int i = 0; i < _cfg.DigSpotsPerDay && pool.Count > 0; i++)
            {
                int k = rng.Next(pool.Count);
                _spots.Add(pool[k]);
                pool.RemoveAt(k);
            }
        }

        private string DayKey(DateTime utc) => _toLocal(utc).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        /// <summary>FNV-1a 32 bit (cố định trên mọi máy, khác string.GetHashCode).</summary>
        private static uint Hash(string s)
        {
            uint h = 2166136261U;
            foreach (char c in s)
            {
                h ^= c;
                h *= 16777619U;
            }
            return h;
        }
    }
}
