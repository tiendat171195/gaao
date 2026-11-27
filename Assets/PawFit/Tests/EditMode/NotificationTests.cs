using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Thông báo nhắc chăm thú (Phase 4.5): dự đoán đúng lúc bằng luật thật, giờ yên tĩnh, gộp, hỏi quyền.</summary>
    public class NotificationTests
    {
        // 07:00 UTC = 14:00 giờ Việt Nam
        private static readonly DateTime T0 = new DateTime(2026, 11, 27, 7, 0, 0, DateTimeKind.Utc);
        private static readonly TimeSpan Vn = TimeSpan.FromHours(7);

        private GameConfig _cfg;
        private ExpressionConfig _expr;

        [SetUp]
        public void SetUp()
        {
            _cfg = GameConfig.CreateDefault();
            _expr = new ExpressionConfig();
        }

        private static PetData Pet(float needs, bool sleeping = false) => new PetData
        {
            Name = "Kem",
            Needs = PetNeeds.Filled(needs),
            IsSleeping = sleeping,
            Health = new PetHealth(),
        };

        /// <summary>Không giờ yên tĩnh, không gộp, không giới hạn: xem đúng giờ từng chuyện.</summary>
        private static NotificationConfig Raw() => new NotificationConfig
        {
            QuietStartHour = 0, QuietEndHour = 0, MergeWithinMinutes = 0, MaxNotifications = 50, MinDelayMinutes = 0,
        };

        private static double HoursOf(List<PlannedNotification> plan, PetAlertKind k)
        {
            foreach (PlannedNotification n in plan)
                if (n.Kinds.Contains(k)) return (n.FireUtc - T0).TotalHours;
            return -1;
        }

        [Test]
        public void HealthyPet_PredictsNeedsThenNeglect_WithRealRules()
        {
            List<PlannedNotification> plan = NotificationPlanner.Plan(Pet(81f), null, T0, Vn, _cfg, _expr, Raw());
            // no 81 giảm 4/giờ, đói khi < 35 => 11,5 giờ; năng lượng 81 giảm 4/giờ, buồn ngủ khi < 30 => 12,75 giờ;
            // sạch 81 giảm 3/giờ, bẩn khi < 40 => 13,67 giờ (bước 15 phút => 13,75)
            Assert.AreEqual(11.5, HoursOf(plan, PetAlertKind.Hungry), 0.26);
            Assert.AreEqual(12.75, HoursOf(plan, PetAlertKind.Sleepy), 0.26);
            Assert.AreEqual(13.75, HoursOf(plan, PetAlertKind.Dirty), 0.26);
            // bỏ bê bắt đầu khi 1 nhu cầu < 60 (no / năng lượng: sau 5,25 giờ) => mệt +24, ốm +72, ốm nặng +120, mất +168
            double start = HoursOf(plan, PetAlertKind.Tired) - _cfg.Health.TiredAfterHours;
            Assert.AreEqual(5.25, start, 0.26);
            Assert.AreEqual(start + _cfg.Health.SickAfterHours, HoursOf(plan, PetAlertKind.Sick), 0.26);
            Assert.AreEqual(start + _cfg.Health.CriticalAfterHours, HoursOf(plan, PetAlertKind.Critical), 0.26);
            Assert.AreEqual(start + _cfg.Health.DeathAfterHours - 24, HoursOf(plan, PetAlertKind.LastChance), 0.26, "còn 1 ngày để cứu bé");
            Assert.AreEqual(7, plan.Count, "đói, buồn ngủ, bẩn, mệt, ốm, ốm nặng, còn 1 ngày — không chuyện nào lặp");
            for (int i = 1; i < plan.Count; i++) Assert.LessOrEqual(plan[i - 1].FireUtc, plan[i].FireUtc, "theo thứ tự thời gian");
            StringAssert.Contains("Kem", plan[0].Title);
        }

        [Test]
        public void SleepingPet_NoSleepyAlert_AlreadyHungry_NotRepeated()
        {
            PetData pet = Pet(80f, sleeping: true);
            pet.Needs.Satiety = 20f;   // đã đói sẵn lúc rời game: người chơi biết rồi
            List<PlannedNotification> plan = NotificationPlanner.Plan(pet, null, T0, Vn, _cfg, _expr, Raw());
            Assert.AreEqual(-1, HoursOf(plan, PetAlertKind.Sleepy), "đang ngủ: năng lượng hồi, không buồn ngủ");
            Assert.AreEqual(-1, HoursOf(plan, PetAlertKind.Hungry), "đói từ trước: không báo lại");
            Assert.Greater(HoursOf(plan, PetAlertKind.Tired), 0, "vẫn báo mệt / ốm vì bỏ bê");
        }

        [Test]
        public void QuietHours_MoveToMorning_AndMergeIntoOne()
        {
            var nc = new NotificationConfig { MaxNotifications = 50 };
            DateTime evening = new DateTime(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);   // 19:00 giờ VN
            List<PlannedNotification> plan = NotificationPlanner.Plan(Pet(81f), null, evening, Vn, _cfg, _expr, nc);
            PlannedNotification first = plan[0];
            DateTime local = first.FireUtc + Vn;
            // đói ~06:45, buồn ngủ ~08:00 (đêm => dời 8:00), bẩn ~08:45 (cách 8:00 dưới 60 phút) => 1 thông báo lúc 8:00
            Assert.AreEqual(8, local.Hour, "rơi vào đêm thì dời tới 8:00");
            Assert.AreEqual(0, local.Minute);
            Assert.AreEqual(28, local.Day, "sáng hôm sau");
            CollectionAssert.IsSupersetOf(first.Kinds, new[] { PetAlertKind.Hungry, PetAlertKind.Sleepy, PetAlertKind.Dirty }, "gộp 1 thông báo");
            foreach (PlannedNotification n in plan)
            {
                int h = (n.FireUtc + Vn).Hour;
                Assert.IsTrue(h >= 8 && h < 22, "không thông báo nào trong 22:00–08:00, lúc " + (n.FireUtc + Vn));
            }
        }

        [Test]
        public void OutOfQuietHours_Rules()
        {
            var nc = new NotificationConfig();
            DateTime At(int hour, int minute = 0) => new DateTime(2026, 11, 27, hour, minute, 0, DateTimeKind.Utc) - Vn;   // giờ VN → UTC
            Assert.AreEqual(At(15), NotificationPlanner.OutOfQuietHours(At(15), Vn, nc), "ban ngày giữ nguyên");
            Assert.AreEqual(At(8).AddDays(1), NotificationPlanner.OutOfQuietHours(At(23, 30), Vn, nc), "23:30 → 8:00 hôm sau");
            Assert.AreEqual(At(8), NotificationPlanner.OutOfQuietHours(At(3), Vn, nc), "3:00 → 8:00 cùng ngày");
            Assert.AreEqual(At(8), NotificationPlanner.OutOfQuietHours(At(8), Vn, nc), "đúng 8:00 thì giữ");
            nc.QuietStartHour = nc.QuietEndHour = 0;
            Assert.AreEqual(At(3), NotificationPlanner.OutOfQuietHours(At(3), Vn, nc), "tắt giờ yên tĩnh");
        }

        [Test]
        public void MergedNeeds_OneSentence_HealthAlertKeepsTitle()
        {
            var nc = new NotificationConfig { QuietStartHour = 0, QuietEndHour = 0, MergeWithinMinutes = 180 };
            List<PlannedNotification> plan = NotificationPlanner.Plan(Pet(81f), null, T0, Vn, _cfg, _expr, nc);
            PlannedNotification needs = plan[0];
            Assert.AreEqual("Kem cần bạn!", needs.Title);
            Assert.AreEqual("Bé đang đói, buồn ngủ và lấm lem. Vào chăm bé nhé.", needs.Body);
            PlannedNotification sick = plan.Find(n => n.Kind == PetAlertKind.Sick);
            Assert.NotNull(sick);
            Assert.AreEqual("Kem bị ốm rồi!", sick.Title);
        }

        [Test]
        public void SickPet_CriticalAndLastChance()
        {
            PetData pet = Pet(10f);
            pet.Health.Stage = HealthStage.Sick;
            pet.Health.NeglectHours = 100;
            List<PlannedNotification> plan = NotificationPlanner.Plan(pet, null, T0, Vn, _cfg, _expr, Raw());
            Assert.AreEqual(_cfg.Health.CriticalAfterHours - 100, HoursOf(plan, PetAlertKind.Critical), 0.26);
            Assert.AreEqual(_cfg.Health.DeathAfterHours - 100 - 24, HoursOf(plan, PetAlertKind.LastChance), 0.26);
            Assert.AreEqual(-1, HoursOf(plan, PetAlertKind.Sick), "đang ốm sẵn: không báo ốm lại");
            Assert.AreEqual("Còn 1 ngày để cứu Kem", plan.Find(n => n.Kind == PetAlertKind.LastChance).Title);
        }

        [Test]
        public void Egg_ReadyAlert_AndMinDelay_AndMax()
        {
            var egg = new EggData { ReadyAtUtc = T0.AddHours(10) };
            List<PlannedNotification> plan = NotificationPlanner.Plan(null, egg, T0, Vn, _cfg, _expr, Raw());
            Assert.AreEqual(1, plan.Count);
            Assert.AreEqual(PetAlertKind.EggReady, plan[0].Kind);
            Assert.AreEqual(10.0, (plan[0].FireUtc - T0).TotalHours, 1e-6);
            Assert.AreEqual("Trứng đã sẵn sàng nở!", plan[0].Title);

            var nc = new NotificationConfig { QuietStartHour = 0, QuietEndHour = 0, MaxNotifications = 2, MinDelayMinutes = 40 };
            PetData pet = Pet(36f);   // đói ngay bước đầu sau khi rời game
            plan = NotificationPlanner.Plan(pet, null, T0, Vn, _cfg, _expr, nc);
            Assert.AreEqual(2, plan.Count, "tối đa 2");
            Assert.AreEqual(40.0, (plan[0].FireUtc - T0).TotalMinutes, 1e-6, "không kêu ngay khi vừa tắt app: dời tới phút 40");
            Assert.IsEmpty(NotificationPlanner.Plan(null, null, T0, Vn, _cfg, _expr, nc), "không thú không trứng: không báo");
        }

        // ------------------------------------------------------------------
        // NotificationService + cài đặt
        // ------------------------------------------------------------------

        private sealed class FakeOs : INotificationScheduler
        {
            public bool Allow = true;
            public readonly List<PlannedNotification> Scheduled = new List<PlannedNotification>();
            public int Clears, Asks;
            public bool Supported => true;

            public void RequestPermission(Action<bool> done)
            {
                Asks++;
                done(Allow);
            }

            public void Replace(IReadOnlyList<PlannedNotification> plan, DateTime nowUtc)
            {
                Scheduled.Clear();
                Scheduled.AddRange(plan);
            }

            public void ClearAll()
            {
                Clears++;
                Scheduled.Clear();
            }

            public void OpenSystemSettings() { }
        }

        [Test]
        public void Service_AsksOnce_SchedulesOnBackground_ClearsOnForeground()
        {
            var time = new ManualTimeProvider(T0);
            var bus = new EventBus();
            var save = new InMemorySaveService(new NewtonsoftSerializer());
            var pets = new PetService(time, new SeededRandom(5), save, bus, _cfg);
            pets.LoadOrCreate("Kem");
            var settings = new SettingsService(save, bus);
            var os = new FakeOs();
            var svc = new NotificationService(settings, pets, time, _cfg, _expr, os, () => Vn);

            Assert.IsFalse(svc.ShouldAsk, "chưa chơi đủ lâu");
            svc.Update(svc.Config.AskAfterSeconds + 1f);
            Assert.IsTrue(svc.ShouldAsk);
            svc.OnBackground();
            Assert.IsEmpty(os.Scheduled, "chưa đồng ý thì chưa hẹn");

            bool? enabled = null;
            svc.Enable(ok => enabled = ok);
            Assert.IsTrue(enabled.Value);
            Assert.IsTrue(svc.Enabled);
            Assert.IsFalse(svc.ShouldAsk, "hỏi 1 lần thôi");
            Assert.AreEqual(true, new SettingsService(save, bus).Current.NotificationsOn, "lưu vào cài đặt của máy");

            svc.OnBackground();
            Assert.IsNotEmpty(os.Scheduled, "ẩn app: có lịch");
            StringAssert.Contains("Kem", os.Scheduled[0].Title);
            svc.OnForeground();
            Assert.IsEmpty(os.Scheduled, "vào lại game: xóa lịch");

            svc.Disable();
            svc.OnBackground();
            Assert.IsEmpty(os.Scheduled, "tắt ở hòm thư: không hẹn nữa");
            Assert.AreEqual(false, settings.Current.NotificationsOn);

            os.Allow = false;   // máy chặn quyền
            svc.Enable(ok => enabled = ok);
            Assert.IsFalse(enabled.Value);
            Assert.IsFalse(svc.Enabled);
        }
    }
}
