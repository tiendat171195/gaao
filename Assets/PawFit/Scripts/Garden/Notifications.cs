using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>
    /// Thú cần mình vì chuyện gì. Số càng lớn càng gấp (gộp nhiều chuyện thì lấy chuyện gấp nhất làm tiêu đề).
    /// CropRipe (Phase 10.2) = vườn rau có quả chín: ít gấp nhất.
    /// </summary>
    public enum PetAlertKind { CropRipe = -1, EggReady = 0, Dirty = 1, Sleepy = 2, Hungry = 3, Tired = 4, Relapsed = 5, Sick = 6, Critical = 7, LastChance = 8 }

    /// <summary>1 thông báo sẽ hẹn với hệ điều hành.</summary>
    public sealed class PlannedNotification
    {
        public DateTime FireUtc;
        public string Title;
        public string Body;

        /// <summary>Chuyện gấp nhất + mọi chuyện đã gộp vào thông báo này.</summary>
        public PetAlertKind Kind;
        public readonly List<PetAlertKind> Kinds = new List<PetAlertKind>();
    }

    public sealed class NotificationConfig
    {
        /// <summary>Giờ yên tĩnh theo giờ trên máy: thông báo rơi vào 22:00–08:00 dời tới 08:00. Đặt 2 số bằng nhau = tắt.</summary>
        public int QuietStartHour = 22;
        public int QuietEndHour = 8;

        /// <summary>2 chuyện cách nhau dưới bấy nhiêu phút thì gộp 1 thông báo.</summary>
        public double MergeWithinMinutes = 60;

        /// <summary>Không báo sớm hơn bấy nhiêu phút sau khi rời game (vừa tắt app đã kêu thì phiền).</summary>
        public double MinDelayMinutes = 15;

        /// <summary>Nhìn trước bấy nhiêu giờ (8 ngày: đủ thấy cả lúc thú bị bỏ bê tới chết).</summary>
        public double HorizonHours = 192;

        /// <summary>Hẹn tối đa bấy nhiêu thông báo.</summary>
        public int MaxNotifications = 8;

        /// <summary>Báo "còn ít thời gian để cứu bé" trước lúc thú mất bấy nhiêu giờ.</summary>
        public double LastChanceHours = 24;

        /// <summary>Chơi bấy nhiêu giây trong vườn thì hỏi có bật thông báo không (lần đầu).</summary>
        public float AskAfterSeconds = 90f;
    }

    /// <summary>
    /// Dự đoán lúc thú cần mình khi tắt app (C# thuần): mô phỏng tới trước bằng đúng NeedsSimulator + HealthRules
    /// (y như lúc mở lại game tính bù thời gian vắng mặt) trên BẢN SAO dữ liệu, rồi lấy lần đầu mỗi chuyện xảy ra:
    /// đói / buồn ngủ / lấm lem (đúng ngưỡng biểu cảm ở vườn), mệt / ốm / ốm lại / ốm nặng, "còn 1 ngày để cứu bé", trứng nở được.
    /// Chuyện đã có sẵn lúc rời game thì không báo lại. Giờ yên tĩnh dời sang sáng, chuyện gần nhau gộp 1 thông báo.
    /// </summary>
    public static class NotificationPlanner
    {
        /// <param name="perks">Tác dụng của đồ đã mua (Phase 8.4): phải giống lúc tính bù khi mở lại game, không thì báo sai giờ.</param>
        /// <param name="cropRipeUtc">Lúc luống rau chín sớm nhất (Phase 10.2), null = không có luống nào đang lớn.</param>
        public static List<PlannedNotification> Plan(PetData pet, EggData egg, DateTime nowUtc, TimeSpan localOffset,
                                                     GameConfig cfg, ExpressionConfig expr, NotificationConfig nc = null,
                                                     CarePerks perks = default, DateTime? cropRipeUtc = null)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            expr = expr ?? new ExpressionConfig();
            nc = nc ?? new NotificationConfig();
            var events = new List<KeyValuePair<DateTime, PetAlertKind>>();
            string name = pet?.Name;

            if (pet != null && pet.Health != null && pet.Health.Stage != HealthStage.Dead && pet.Needs != null)
                Simulate(pet, nowUtc, cfg, expr, nc, perks, events);
            else if (egg != null && egg.ReadyAtUtc > nowUtc)
                events.Add(new KeyValuePair<DateTime, PetAlertKind>(egg.ReadyAtUtc, PetAlertKind.EggReady));
            if (cropRipeUtc.HasValue && cropRipeUtc.Value > nowUtc && cropRipeUtc.Value <= nowUtc.AddHours(nc.HorizonHours))
                events.Add(new KeyValuePair<DateTime, PetAlertKind>(cropRipeUtc.Value, PetAlertKind.CropRipe));

            DateTime earliest = nowUtc.AddMinutes(Math.Max(0.0, nc.MinDelayMinutes));
            var shifted = new List<KeyValuePair<DateTime, PetAlertKind>>();
            foreach (KeyValuePair<DateTime, PetAlertKind> e in events)
            {
                DateTime t = e.Key < earliest ? earliest : e.Key;
                shifted.Add(new KeyValuePair<DateTime, PetAlertKind>(OutOfQuietHours(t, localOffset, nc), e.Value));
            }
            shifted.Sort((a, b) => a.Key != b.Key ? a.Key.CompareTo(b.Key) : b.Value.CompareTo(a.Value));

            var plan = new List<PlannedNotification>();
            PlannedNotification current = null;
            foreach (KeyValuePair<DateTime, PetAlertKind> e in shifted)
            {
                if (current != null && (e.Key - current.FireUtc).TotalMinutes <= nc.MergeWithinMinutes)
                {
                    if (!current.Kinds.Contains(e.Value)) current.Kinds.Add(e.Value);
                    continue;
                }
                if (plan.Count >= Math.Max(0, nc.MaxNotifications)) break;
                current = new PlannedNotification { FireUtc = e.Key };
                current.Kinds.Add(e.Value);
                plan.Add(current);
            }
            foreach (PlannedNotification n in plan) Write(n, name, nc);
            return plan;
        }

        /// <summary>Chạy tới trước từng bước nhỏ (bước của luật sức khỏe) trên bản sao, ghi lần đầu mỗi chuyện xảy ra.</summary>
        private static void Simulate(PetData pet, DateTime nowUtc, GameConfig cfg, ExpressionConfig expr, NotificationConfig nc,
                                     CarePerks perks, List<KeyValuePair<DateTime, PetAlertKind>> events)
        {
            PetNeeds needs = pet.Needs.Clone();
            PetHealth health = pet.Health.Clone();
            bool sleeping = pet.IsSleeping;   // vắng mặt thì thú giữ nguyên ngủ / thức (như lúc mở game tính bù)
            bool hungry = needs.Satiety < expr.HungryBelowSatiety;
            bool dirty = needs.Hygiene < expr.DirtyBelowHygiene;
            bool sleepy = !sleeping && needs.Energy < expr.SleepyBelowEnergy;
            var seen = new HashSet<PetAlertKind>();
            double step = Math.Max(0.01, cfg.Health.SimulationStepHours);
            double horizon = Math.Max(step, nc.HorizonHours);

            void Add(PetAlertKind k, DateTime at)
            {
                if (seen.Add(k)) events.Add(new KeyValuePair<DateTime, PetAlertKind>(at, k));
            }

            for (double h = step; h <= horizon + 1e-9; h += step)
            {
                DateTime at = nowUtc.AddHours(h);
                NeedsSimulator.Advance(needs, step, sleeping, cfg.Needs, perks);
                HealthChange change = HealthRules.Step(health, needs, step, at, cfg);
                if (!hungry && needs.Satiety < expr.HungryBelowSatiety)
                {
                    hungry = true;
                    Add(PetAlertKind.Hungry, at);
                }
                if (!dirty && needs.Hygiene < expr.DirtyBelowHygiene)
                {
                    dirty = true;
                    Add(PetAlertKind.Dirty, at);
                }
                if (!sleepy && !sleeping && needs.Energy < expr.SleepyBelowEnergy)
                {
                    sleepy = true;
                    Add(PetAlertKind.Sleepy, at);
                }
                switch (change)
                {
                    case HealthChange.BecameTired: Add(PetAlertKind.Tired, at); break;
                    case HealthChange.FellSick: Add(PetAlertKind.Sick, at); break;
                    case HealthChange.Relapsed: Add(PetAlertKind.Relapsed, at); break;
                    case HealthChange.Worsened: Add(PetAlertKind.Critical, at); break;
                    case HealthChange.Died:
                        DateTime died = health.DiedAtUtc ?? at;
                        DateTime warn = died.AddHours(-Math.Max(1.0, nc.LastChanceHours));
                        if (warn > nowUtc) Add(PetAlertKind.LastChance, warn);
                        return;   // thú mất: không báo gì sau đó nữa
                }
            }
        }

        /// <summary>Rơi vào giờ yên tĩnh (theo giờ trên máy) thì dời tới giờ hết yên tĩnh gần nhất.</summary>
        public static DateTime OutOfQuietHours(DateTime utc, TimeSpan localOffset, NotificationConfig nc)
        {
            int start = ((nc.QuietStartHour % 24) + 24) % 24, end = ((nc.QuietEndHour % 24) + 24) % 24;
            if (start == end) return utc;
            DateTime local = utc + localOffset;
            int hour = local.Hour;
            bool quiet = start > end ? hour >= start || hour < end : hour >= start && hour < end;
            if (!quiet) return utc;
            DateTime wake = local.Date.AddHours(end);
            if (wake <= local) wake = wake.AddDays(1);
            return wake - localOffset;
        }

        // ------------------------------------------------------------------
        // Chữ (tiếng Việt)
        // ------------------------------------------------------------------

        private static void Write(PlannedNotification n, string petName, NotificationConfig nc)
        {
            string pet = string.IsNullOrEmpty(petName) ? "Bé" : petName;
            PetAlertKind top = n.Kinds[0];
            foreach (PetAlertKind k in n.Kinds)
                if (k > top) top = k;
            n.Kind = top;
            var needs = new List<string>();
            foreach (PetAlertKind k in new[] { PetAlertKind.Hungry, PetAlertKind.Sleepy, PetAlertKind.Dirty })
                if (n.Kinds.Contains(k)) needs.Add(NeedWord(k));
            if (top <= PetAlertKind.Hungry && needs.Count >= 2)   // chỉ toàn nhu cầu: gộp 1 câu
            {
                n.Title = pet + " cần bạn!";
                n.Body = "Bé đang " + JoinVi(needs) + ". Vào chăm bé nhé.";
                return;
            }
            n.Title = Title(top, pet, nc);
            n.Body = Body(top, nc);
            if (top > PetAlertKind.Hungry && needs.Count > 0) n.Body += " Bé cũng đang " + JoinVi(needs) + ".";
        }

        public static string Title(PetAlertKind k, string pet, NotificationConfig nc = null)
        {
            switch (k)
            {
                case PetAlertKind.Hungry: return pet + " đói rồi!";
                case PetAlertKind.Sleepy: return pet + " buồn ngủ";
                case PetAlertKind.Dirty: return pet + " lấm lem quá";
                case PetAlertKind.Tired: return pet + " mệt lắm rồi";
                case PetAlertKind.Sick: return pet + " bị ốm rồi!";
                case PetAlertKind.Relapsed: return pet + " ốm lại rồi";
                case PetAlertKind.Critical: return pet + " ốm nặng!";
                case PetAlertKind.LastChance: return "Còn " + Span((nc ?? new NotificationConfig()).LastChanceHours) + " để cứu " + pet;
                case PetAlertKind.CropRipe: return "Vườn rau có quả chín rồi!";
                default: return "Trứng đã sẵn sàng nở!";
            }
        }

        public static string Body(PetAlertKind k, NotificationConfig nc = null)
        {
            switch (k)
            {
                case PetAlertKind.Hungry: return "Bé đang ngóng chòi ăn. Vào cho bé ăn nhé.";
                case PetAlertKind.Sleepy: return "Bé díp mắt rồi. Dắt bé vào ổ ngủ nhé.";
                case PetAlertKind.Dirty: return "Ruồi bay quanh bé rồi. Dắt bé ra hồ tắm nhé.";
                case PetAlertKind.Tired: return "Lâu rồi bé chưa được chăm đủ. Vào với bé kẻo bé ốm nhé!";
                case PetAlertKind.Sick: return "Bé cần cháo, chăn ấm và được lau người. Vào chăm bé nhé.";
                case PetAlertKind.Relapsed: return "Bé chưa khỏi hẳn mà lâu chưa được chăm. Vào với bé nhé.";
                case PetAlertKind.Critical: return "Không chữa kịp bé sẽ mất. Vào chữa cho bé ngay nhé!";
                case PetAlertKind.LastChance: return "Bé ốm rất nặng. Vào chữa ngay kẻo mất bé mãi mãi.";
                case PetAlertKind.CropRipe: return "Vào hái quả cho bé ăn, gieo hạt mới nhé.";
                default: return "Vào vườn đón bé mới nhé.";
            }
        }

        private static string NeedWord(PetAlertKind k) =>
            k == PetAlertKind.Hungry ? "đói" : k == PetAlertKind.Sleepy ? "buồn ngủ" : "lấm lem";

        private static string JoinVi(List<string> parts)
        {
            if (parts.Count <= 1) return parts.Count == 1 ? parts[0] : "";
            return string.Join(", ", parts.GetRange(0, parts.Count - 1)) + " và " + parts[parts.Count - 1];
        }

        private static string Span(double hours) =>
            hours >= 24 ? Math.Max(1, (int)Math.Round(hours / 24.0)) + " ngày" : Math.Max(1, (int)Math.Round(hours)) + " giờ";
    }

    public enum NotificationPermission { Unknown = 0, Granted = 1, Denied = 2 }

    /// <summary>
    /// Cổng hẹn thông báo của hệ điều hành (bản Unity: package Mobile Notifications ở PawFit.App; Editor / test: bản giả).
    /// Mọi callback chạy trên luồng chính.
    /// </summary>
    public interface INotificationScheduler
    {
        /// <summary>Máy này hẹn được thông báo không (Editor / máy tính: không).</summary>
        bool Supported { get; }

        /// <summary>Xin quyền hiện thông báo (Android 13+ / iOS hiện hộp thoại của máy, đã quyết rồi thì trả ngay).</summary>
        void RequestPermission(Action<bool> done);

        /// <summary>Hủy lịch cũ, hẹn đúng danh sách này (giờ hẹn tính từ `nowUtc`).</summary>
        void Replace(IReadOnlyList<PlannedNotification> plan, DateTime nowUtc);

        /// <summary>Hủy lịch + xóa thông báo đang hiện (người chơi đã vào game).</summary>
        void ClearAll();

        /// <summary>Mở cài đặt thông báo của app trong máy (khi người chơi lỡ chặn).</summary>
        void OpenSystemSettings();
    }

    /// <summary>
    /// Thông báo nhắc chăm thú (Phase 4.5, C# thuần): ẩn / tắt app thì hẹn lịch theo <see cref="NotificationPlanner"/>,
    /// mở lại app thì xóa hết (người chơi đang ở đây rồi). Lần đầu chơi đủ lâu thì hỏi có bật không (rồi mới xin quyền
    /// của máy). Bật / tắt ở hòm thư, lưu ở cài đặt của máy.
    /// </summary>
    public sealed class NotificationService
    {
        private readonly SettingsService _settings;
        private readonly PetService _pets;
        private readonly ITimeProvider _time;
        private readonly GameConfig _cfg;
        private readonly ExpressionConfig _expr;
        private readonly INotificationScheduler _os;
        private readonly Func<TimeSpan> _localOffset;
        private readonly NotificationConfig _nc;
        private readonly ILog _log;
        private float _played;

        /// <param name="localOffset">Giờ trên máy − UTC (để tính giờ yên tĩnh).</param>
        public NotificationService(SettingsService settings, PetService pets, ITimeProvider time, GameConfig cfg, ExpressionConfig expr,
                                   INotificationScheduler os, Func<TimeSpan> localOffset, NotificationConfig nc = null, ILog log = null)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _time = time ?? throw new ArgumentNullException(nameof(time));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _expr = expr ?? new ExpressionConfig();
            _os = os ?? throw new ArgumentNullException(nameof(os));
            _localOffset = localOffset ?? (() => TimeSpan.Zero);
            _nc = nc ?? new NotificationConfig();
            _log = log ?? NullLog.Instance;
        }

        public NotificationConfig Config => _nc;
        public bool Supported => _os.Supported;

        /// <summary>Lúc luống rau chín sớm nhất (Phase 10.2, App nối vào FarmService.NextRipeUtc). null = không báo quả chín.</summary>
        public Func<DateTime?> CropRipeAt { get; set; }

        /// <summary>null = chưa hỏi, true = bật, false = tắt.</summary>
        public bool? Setting => _settings.Current.NotificationsOn;

        public bool Enabled => Setting == true;

        /// <summary>Chơi trong vườn (gọi khi đang ở vườn, không có thẻ mở) — để biết lúc nào hỏi bật thông báo.</summary>
        public void Update(float seconds) => _played += Math.Max(0f, seconds);

        /// <summary>Tới lúc hỏi "bật thông báo không?" (1 lần): máy hẹn được, chưa hỏi, đang có thú, đã chơi đủ lâu.</summary>
        public bool ShouldAsk => _os.Supported && Setting == null && _pets.HasPet && _played >= _nc.AskAfterSeconds;

        /// <summary>Người chơi đồng ý ở thẻ hỏi / bật ở hòm thư: xin quyền của máy. done(true) = đã bật.</summary>
        public void Enable(Action<bool> done)
        {
            _os.RequestPermission(ok =>
            {
                _settings.SetNotifications(ok);
                _log.Info("[PawFit] Thông báo: " + (ok ? "đã bật" : "máy chặn"));
                done?.Invoke(ok);
            });
        }

        /// <summary>Người chơi chọn "Để sau" / tắt ở hòm thư.</summary>
        public void Disable()
        {
            _settings.SetNotifications(false);
            _os.ClearAll();
        }

        /// <summary>Lịch sẽ hẹn nếu ẩn app ngay bây giờ (bảng DEV xem trước).</summary>
        public List<PlannedNotification> Preview() =>
            NotificationPlanner.Plan(_pets.Current, _pets.Egg, _time.UtcNow, _localOffset(), _cfg, _expr, _nc, _pets.Perks, CropRipeAt?.Invoke());

        /// <summary>Ẩn / tắt app: hẹn lịch mới (tắt thông báo thì thôi).</summary>
        public void OnBackground()
        {
            if (!_os.Supported || !Enabled) return;
            List<PlannedNotification> plan = Preview();
            _os.Replace(plan, _time.UtcNow);
            _log.Info("[PawFit] Hẹn " + plan.Count + " thông báo" + (plan.Count > 0 ? ", đầu tiên: " + plan[0].Title : ""));
        }

        /// <summary>Mở / quay lại app: xóa lịch + thông báo đang hiện.</summary>
        public void OnForeground()
        {
            if (_os.Supported) _os.ClearAll();
        }

        /// <summary>Bảng DEV: thử 1 thông báo sau `seconds` giây (ẩn app đi để thấy).</summary>
        public void SendTest(double seconds)
        {
            if (!_os.Supported) return;
            string pet = _pets.HasPet ? _pets.Current.Name : "Bé";
            var n = new PlannedNotification
            {
                FireUtc = _time.UtcNow.AddSeconds(Math.Max(1.0, seconds)),
                Kind = PetAlertKind.Hungry,
                Title = NotificationPlanner.Title(PetAlertKind.Hungry, pet, _nc),
                Body = "Thông báo thử từ bảng DEV.",
            };
            n.Kinds.Add(PetAlertKind.Hungry);
            _os.Replace(new[] { n }, _time.UtcNow);
        }

        public void OpenSystemSettings() => _os.OpenSystemSettings();

        /// <summary>Bảng DEV: quên câu trả lời để game hỏi lại (sau khi chơi đủ lâu).</summary>
        public void ResetAskForDebug()
        {
            _settings.ResetNotificationsForDebug();
            _played = 0f;
        }
    }

    /// <summary>Không hẹn được thông báo (Editor / máy tính): chỉ ghi log để test luồng.</summary>
    public sealed class NoNotificationScheduler : INotificationScheduler
    {
        public bool Supported => false;
        public void RequestPermission(Action<bool> done) => done?.Invoke(false);
        public void Replace(IReadOnlyList<PlannedNotification> plan, DateTime nowUtc) { }
        public void ClearAll() { }
        public void OpenSystemSettings() { }
    }
}
