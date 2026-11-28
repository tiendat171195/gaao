using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Đồ trang trí + phụ kiện có tác dụng thật, bé dùng được đồ trong vườn (Phase 8.4).</summary>
    public class PerksTests
    {
        private static CosmeticsData Owned(AccessoryKind wear, params DecorKind[] decor)
        {
            var c = new CosmeticsData { Equipped = wear };
            if (wear != AccessoryKind.None) c.Accessories.Add(wear);
            c.Decor.AddRange(decor);
            return c;
        }

        [Test]
        public void Perks_ComeFromDecorInGarden_AndWornAccessory()
        {
            var cfg = new PerksConfig();
            CarePerks none = CarePerks.From(new CosmeticsData(), cfg);
            Assert.IsFalse(none.Any, "chưa có gì thì không có tác dụng");
            Assert.IsFalse(default(CarePerks).Any);

            CarePerks p = CarePerks.From(Owned(AccessoryKind.Bow, DecorKind.Bench, DecorKind.FlowerArch, DecorKind.Lanterns), cfg);
            Assert.AreEqual(cfg.BenchEnergyCut, p.EnergyCut, 1e-5f, "ghế gỗ: năng lượng hao chậm");
            Assert.AreEqual(cfg.FlowerArchMoodCut + cfg.BowMoodCut, p.MoodCut, 1e-5f, "vòm hoa + nơ cộng dồn");
            Assert.AreEqual(cfg.LanternsSleepBoost, p.SleepBoost, 1e-5f, "đèn lồng: ngủ hồi nhanh");
            Assert.AreEqual(0f, p.SatietyCut, 1e-5f);

            Assert.AreEqual(cfg.ScarfSatietyCut, CarePerks.From(Owned(AccessoryKind.Scarf), cfg).SatietyCut, 1e-5f, "khăn quàng: đỡ đói");
            Assert.AreEqual(cfg.BeanieEnergyCut, CarePerks.From(Owned(AccessoryKind.Beanie), cfg).EnergyCut, 1e-5f, "mũ len: đỡ mệt");
            CarePerks crown = CarePerks.From(Owned(AccessoryKind.Crown), cfg);
            foreach (NeedType n in new[] { NeedType.Satiety, NeedType.Hygiene, NeedType.Energy, NeedType.Mood })
                Assert.AreEqual(cfg.CrownAllCut, crown.CutOf(n), 1e-5f, "vương miện: cả 4 nhu cầu");

            var big = new PerksConfig { FlowerArchMoodCut = 0.5f, BowMoodCut = 0.5f };
            Assert.AreEqual(big.MaxCut, CarePerks.From(Owned(AccessoryKind.Bow, DecorKind.FlowerArch), big).MoodCut, 1e-5f, "có trần cộng dồn");
        }

        [Test]
        public void Simulator_DecaysSlower_AndSleepsBetter_WithPerks()
        {
            var cfg = new NeedsConfig();
            var perks = new CarePerks { MoodCut = 0.25f, EnergyCut = 0.15f, SleepBoost = 0.25f };
            PetNeeds plain = PetNeeds.Filled(80f), lucky = PetNeeds.Filled(80f);
            NeedsSimulator.Advance(plain, 10, false, cfg);
            NeedsSimulator.Advance(lucky, 10, false, cfg, perks);
            Assert.AreEqual(80f - cfg.MoodDecayPerHour * 10f, plain.Mood, 0.01f);
            Assert.AreEqual(80f - cfg.MoodDecayPerHour * 0.75f * 10f, lucky.Mood, 0.01f);
            Assert.AreEqual(80f - cfg.EnergyDecayPerHour * 0.85f * 10f, lucky.Energy, 0.01f);
            Assert.AreEqual(plain.Satiety, lucky.Satiety, 0.01f, "không có đồ cho no thì như thường");

            PetNeeds a = PetNeeds.Filled(20f), b = PetNeeds.Filled(20f);
            NeedsSimulator.Advance(a, 2, true, cfg);
            NeedsSimulator.Advance(b, 2, true, cfg, perks);
            Assert.AreEqual(20f + cfg.EnergyRegenPerHour * 2f, a.Energy, 0.01f);
            Assert.AreEqual(20f + cfg.EnergyRegenPerHour * 1.25f * 2f, b.Energy, 0.01f);
        }

        [Test]
        public void AwayTime_UsesOwnedDecor()
        {
            var plain = new GardenFixture();
            var arch = new GardenFixture();
            arch.Pets.Cosmetics.Decor.Add(DecorKind.FlowerArch);
            Assert.Greater(arch.Pets.Perks.MoodCut, 0f);
            float m0 = plain.Pets.Current.Needs.Mood;
            Assert.AreEqual(m0, arch.Pets.Current.Needs.Mood, 0.01f);
            plain.Time.Advance(TimeSpan.FromHours(6));
            arch.Time.Advance(TimeSpan.FromHours(6));
            plain.Pets.Tick();
            arch.Pets.Tick();
            Assert.Greater(arch.Pets.Current.Needs.Mood, plain.Pets.Current.Needs.Mood + 2f, "có vòm hoa: vắng 6 giờ bé vẫn vui hơn");
        }

        [Test]
        public void Notifications_PredictWithSamePerks()
        {
            var cfg = GameConfig.CreateDefault();
            var raw = new NotificationConfig { QuietStartHour = 0, QuietEndHour = 0, MergeWithinMinutes = 0, MaxNotifications = 50, MinDelayMinutes = 0 };
            DateTime t0 = GardenFixture.T0;
            PetData Pet() => new PetData { Name = "Kem", Needs = PetNeeds.Filled(81f), Health = new PetHealth() };
            double HungryAt(List<PlannedNotification> plan)
            {
                foreach (PlannedNotification n in plan)
                    if (n.Kinds.Contains(PetAlertKind.Hungry)) return (n.FireUtc - t0).TotalHours;
                return -1;
            }
            double plain = HungryAt(NotificationPlanner.Plan(Pet(), null, t0, TimeSpan.Zero, cfg, new ExpressionConfig(), raw));
            CarePerks scarf = CarePerks.From(Owned(AccessoryKind.Scarf), cfg.Perks);
            double warm = HungryAt(NotificationPlanner.Plan(Pet(), null, t0, TimeSpan.Zero, cfg, new ExpressionConfig(), raw, scarf));
            Assert.AreEqual(11.5, plain, 0.26);
            Assert.Greater(warm, plain + 1.0, "đeo khăn quàng: đói muộn hơn => thông báo cũng muộn hơn (khớp lúc mở lại game)");
        }

        [Test]
        public void Windmill_GivesOneTreatPerDay()
        {
            var f = new GardenFixture();
            var daily = new DailyService(f.Pets, f.Time, f.Bus, null, f.Layout, toLocal: utc => utc);
            Assert.IsFalse(daily.WindmillReady, "chưa có cối xay gió");
            f.Pets.Cosmetics.Decor.Add(DecorKind.Windmill);
            Assert.IsTrue(daily.WindmillReady);
            f.Pets.SetNeedForDebug(NeedType.Satiety, 95f);
            Assert.IsFalse(daily.TakeWindmillTreat(out string full));
            StringAssert.Contains("no", full);
            Assert.IsTrue(daily.WindmillReady, "no quá thì để dành, chưa mất bánh");
            f.Pets.SetNeedForDebug(NeedType.Satiety, 40f);
            f.Pets.SetNeedForDebug(NeedType.Mood, 50f);
            Assert.IsTrue(daily.TakeWindmillTreat(out _));
            Assert.AreEqual(40f + daily.Config.WindmillSatiety, f.Pets.Current.Needs.Satiety, 0.05f);
            Assert.AreEqual(50f + daily.Config.WindmillMood, f.Pets.Current.Needs.Mood, 0.05f);
            Assert.IsFalse(daily.WindmillReady);
            Assert.IsFalse(daily.TakeWindmillTreat(out string again));
            StringAssert.Contains("mai", again);
            f.Time.Advance(TimeSpan.FromDays(1));
            Assert.IsTrue(daily.WindmillReady, "sang ngày mới có túi bánh mới");
            daily.Dispose();
        }

        [Test]
        public void Bench_Rest_GivesEnergy_WithCooldown()
        {
            var f = new GardenFixture();
            f.Pets.SetNeedForDebug(NeedType.Energy, 50f);
            f.Pets.SetNeedForDebug(NeedType.Mood, 50f);
            Assert.AreEqual(CareResult.Ok, f.Care.RestOnBench());
            Assert.AreEqual(50f + f.Cfg.BenchRestEnergy, f.Pets.Current.Needs.Energy, 0.05f);
            Assert.AreEqual(50f + f.Cfg.BenchRestMood, f.Pets.Current.Needs.Mood, 0.05f);
            float e = f.Pets.Current.Needs.Energy;
            Assert.AreEqual(CareResult.Ok, f.Care.RestOnBench(), "vẫn ngồi được");
            Assert.AreEqual(e, f.Pets.Current.Needs.Energy, 0.05f, "ngồi liền lần nữa không cộng thêm");
            f.Time.Advance(TimeSpan.FromMinutes(f.Cfg.BenchRestCooldownMinutes + 1));
            f.Care.RestOnBench();
            Assert.Greater(f.Pets.Current.Needs.Energy, e);
        }

        private static void PutBench(GardenFixture f)
        {
            f.Brain.BenchCenter = new Vector2(5.2f, -2.7f);
            f.Brain.BenchStand = new Vector2(4.9f, -3.3f);
        }

        [Test]
        public void Brain_TiredPet_GoesToSitOnBench_ThenGetsUp()
        {
            var f = new GardenFixture();
            PutBench(f);
            f.Pets.SetNeedForDebug(NeedType.Energy, 50f);
            f.Pets.SetNeedForDebug(NeedType.Satiety, 80f);
            f.Pets.SetNeedForDebug(NeedType.Mood, 80f);
            bool rested = false;
            for (int i = 0; i < 1200 && !rested; i++)
            {
                f.Simulate(0.1f);
                rested = f.Brain.Activity == PetActivity.Resting;
            }
            Assert.IsTrue(rested, "hơi mệt + có ghế: bé tự ra ghế ngồi nghỉ");
            Assert.Less(Vector2.Distance(f.Pos, f.Brain.BenchStand.Value), 0.6f, "ngồi đúng chỗ ghế");
            f.Simulate(f.Cfg.BenchRestSeconds + 0.5f);
            Assert.AreNotEqual(PetActivity.Resting, f.Brain.Activity, "ngồi 1 lúc rồi đứng dậy");
        }

        [Test]
        public void Brain_NoBench_NeverRests()
        {
            var f = new GardenFixture();
            f.Pets.SetNeedForDebug(NeedType.Energy, 50f);
            f.Pets.SetNeedForDebug(NeedType.Satiety, 80f);
            for (int i = 0; i < 600; i++)
            {
                f.Simulate(0.1f);
                Assert.AreNotEqual(PetActivity.Resting, f.Brain.Activity);
            }
        }

        [Test]
        public void Brain_DropOnBench_OrTapBench_SitsDown()
        {
            var f = new GardenFixture();
            PutBench(f);
            f.Pets.SetNeedForDebug(NeedType.Energy, 90f);   // không mệt vẫn ngồi khi bạn bảo
            f.Brain.BeginLead(f.Pos);
            f.Brain.EndLead(f.Brain.BenchCenter.Value, f.Pos);
            Assert.AreEqual(PetActivity.GoingToZone, f.Brain.Activity);
            Assert.AreEqual(ZoneType.Bench, f.Brain.ZoneGoal);
            f.Simulate(8f, 0.1f);
            Assert.AreEqual(PetActivity.Resting, f.Brain.Activity, "thả lên ghế: bé lên ngồi");

            var g = new GardenFixture();
            PutBench(g);
            g.Brain.CommandUseZone(ZoneType.Bench, g.Pos);
            g.Simulate(8f, 0.1f);
            Assert.AreEqual(PetActivity.Resting, g.Brain.Activity, "chạm ghế: bé ra ngồi");

            var h = new GardenFixture();   // chưa có ghế: lệnh ra ghế bị bỏ qua
            h.Brain.CommandUseZone(ZoneType.Bench, h.Pos);
            Assert.AreNotEqual(PetActivity.GoingToZone, h.Brain.Activity);
        }

        [Test]
        public void Butterfly_IsAPlayKind()
        {
            var f = new GardenFixture();
            var play = new PlayService(f.Pets, f.Health, f.Evo, f.Time, f.Bus);
            f.Pets.SetNeedForDebug(NeedType.Mood, 50f);
            Assert.IsTrue(play.Reward(PlayKind.Butterfly));
            Assert.AreEqual(50f + play.Config.Mood[(int)PlayKind.Butterfly], f.Pets.Current.Needs.Mood, 0.05f);
            Assert.IsFalse(play.Reward(PlayKind.Butterfly), "có thời gian chờ");
        }
    }
}
