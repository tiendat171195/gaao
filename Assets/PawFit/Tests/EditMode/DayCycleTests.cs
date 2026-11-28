using System;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Ngày / đêm theo giờ máy (Phase 10.1): ánh sáng theo giờ, chuyển mượt, giờ đi ngủ, tắt được, ép giờ (DEV).</summary>
    public class DayCycleTests
    {
        private static DayCycle At(float hour) => new DayCycle(() => new DateTime(2026, 11, 28, 0, 0, 0).AddHours(hour));

        [Test]
        public void Noon_KeepsTheOriginalGardenLook()
        {
            DayCycle c = At(12f);
            DayLight l = c.Light;
            Assert.AreEqual(DayPhase.Day, c.Phase);
            Assert.AreEqual(1.15f, l.SunIntensity, 1e-4f);
            Assert.AreEqual(50f, l.SunPitch, 1e-4f);
            Assert.AreEqual(-35f, l.SunYaw, 1e-4f);
            Assert.AreEqual(0.7f, l.ShadowStrength, 1e-4f);
            Assert.AreEqual(0f, Rgb.Distance(l.Fog, new Rgb(0.78f, 0.88f, 0.94f)), 1e-4f, "sương + nền trưa như cũ");
            Assert.AreEqual(0f, Rgb.Distance(l.AmbientSky, new Rgb(0.72f, 0.82f, 0.92f)), 1e-4f);
            Assert.AreEqual(0f, l.Night);
            Assert.IsFalse(c.IsNight);
        }

        [Test]
        public void Midnight_IsDarkBlueMoonlight_WithoutBirds()
        {
            DayCycle c = At(0.5f);
            DayLight night = c.Light, noon = At(12f).Light;
            Assert.AreEqual(DayPhase.Night, c.Phase);
            Assert.IsTrue(c.IsNight);
            Assert.AreEqual(1f, night.Night, 1e-4f);
            Assert.Less(night.SunIntensity, noon.SunIntensity * 0.5f, "trăng yếu hơn nắng nhiều");
            Assert.Less(night.Fog.Luma, noon.Fog.Luma * 0.4f, "nền đêm tối");
            Assert.Less(night.AmbientSky.Luma, noon.AmbientSky.Luma);
            Assert.Greater(night.SunColor.B, night.SunColor.R, "ánh trăng xanh");
            Assert.Greater(night.SunIntensity, 0.3f, "vẫn đủ sáng để thấy bé");
            Assert.AreEqual(0f, night.Birds, "đêm không có chim");
        }

        [Test]
        public void DawnAndDusk_LookLikeDawnAndDusk()
        {
            DayLight dawn = At(6f).Light, noon = At(12f).Light, golden = At(18f).Light;
            Assert.AreEqual(DayPhase.Dawn, At(6f).Phase);
            Assert.AreEqual(DayPhase.Dusk, At(18.5f).Phase);
            Assert.Greater(dawn.Birds, noon.Birds, "bình minh chim hót nhiều nhất");
            Assert.Less(dawn.SunPitch, noon.SunPitch, "sáng sớm nắng chiếu xiên");
            Assert.Less(golden.SunPitch, noon.SunPitch, "chiều tà bóng đổ dài");
            Assert.Greater(golden.SunColor.R - golden.SunColor.B, noon.SunColor.R - noon.SunColor.B, "chiều tà nắng vàng cam");
            Assert.Greater(At(19.5f).Light.Night, 0.5f, "chạng vạng đã tối, đom đóm ra");
            Assert.AreEqual(DayPhase.Night, At(23f).Phase);
            Assert.AreEqual(DayPhase.Night, At(3f).Phase);
            Assert.AreEqual(DayPhase.Day, At(9f).Phase);
        }

        [Test]
        public void Light_ChangesSmoothly_AllDay_AcrossMidnight()
        {
            var cfg = new DayCycleConfig();
            DayLight prev = DayCycle.Sample(0f, cfg);
            for (int m = 1; m <= 24 * 60; m++)   // mỗi phút; phút thứ 1440 = 24:00 = 0:00
            {
                DayLight l = DayCycle.Sample(m / 60f, cfg);
                string at = (m / 60) + ":" + (m % 60).ToString("00");
                Assert.Less(Math.Abs(l.SunIntensity - prev.SunIntensity), 0.02f, "nắng nhảy lúc " + at);
                Assert.Less(Math.Abs(l.SunYaw - prev.SunYaw), 2.5f, "hướng nắng nhảy lúc " + at);
                Assert.Less(Math.Abs(l.SunPitch - prev.SunPitch), 1.5f, "độ cao nắng nhảy lúc " + at);
                Assert.Less(Rgb.Distance(l.Fog, prev.Fog), 0.02f, "màu nền nhảy lúc " + at);
                Assert.Less(Rgb.Distance(l.AmbientSky, prev.AmbientSky), 0.02f, "ánh sáng trời nhảy lúc " + at);
                Assert.Less(Math.Abs(l.Night - prev.Night), 0.02f, "độ tối nhảy lúc " + at);
                Assert.Greater(l.SunYaw, -90f, "nắng luôn chiếu từ sau lưng camera (mặt bé không tối) lúc " + at);
                Assert.Less(l.SunYaw, 90f, at);
                prev = l;
            }
        }

        [Test]
        public void Bedtime_CrossesMidnight()
        {
            Assert.IsFalse(At(21f).IsBedtime);
            Assert.IsTrue(At(22f).IsBedtime);
            Assert.IsTrue(At(2f).IsBedtime);
            Assert.IsTrue(At(5.9f).IsBedtime);
            Assert.IsFalse(At(6.5f).IsBedtime);
            Assert.IsFalse(At(14f).IsBedtime);
            Assert.AreEqual(18.5f, At(18.5f).Hour, 1e-3f, "giờ có phần lẻ (18:30)");
        }

        [Test]
        public void Disabled_IsAlwaysNoon_NoBedtime_DebugHourOverridesClock()
        {
            DayCycle c = At(23f);
            c.Enabled = false;
            Assert.AreEqual(12f, c.Hour, 1e-4f);
            Assert.IsFalse(c.IsNight);
            Assert.IsFalse(c.IsBedtime);
            Assert.AreEqual(23f, c.ClockHour, 1e-3f, "đồng hồ vẫn chạy");
            c.Enabled = true;
            Assert.IsTrue(c.IsBedtime);
            c.DebugHour = 6f;
            Assert.AreEqual(DayPhase.Dawn, c.Phase);
            c.DebugHour = 26f;   // vòng qua 24 giờ
            Assert.AreEqual(2f, c.Hour, 1e-3f);
            c.DebugHour = null;
            Assert.AreEqual(23f, c.Hour, 1e-3f);
        }

        [Test]
        public void MoveToward_BlendsThenReachesGoal()
        {
            var cfg = new DayCycleConfig();
            DayLight a = DayCycle.Sample(12f, cfg), b = DayCycle.Sample(0f, cfg);
            DayLight s = a.Clone();
            s.MoveToward(b, 0.5f);
            Assert.AreEqual((a.SunIntensity + b.SunIntensity) / 2f, s.SunIntensity, 1e-4f);
            Assert.AreEqual(1.15f, a.SunIntensity, 1e-4f, "Clone: bản gốc không đổi");
            s.MoveToward(b, 1f);
            Assert.AreEqual(b.Night, s.Night, 1e-5f);
            Assert.AreEqual(0f, Rgb.Distance(s.Fog, b.Fog), 1e-5f);
        }

        [Test]
        public void Setting_TogglesAndPersists()
        {
            var save = new InMemorySaveService(new NewtonsoftSerializer());
            var bus = new EventBus();
            int changed = 0;
            bus.Subscribe<SettingsChanged>(_ => changed++);
            var s = new SettingsService(save, bus);
            Assert.IsTrue(s.DayNightOn, "mặc định bật");
            s.SetDayNight(false);
            Assert.AreEqual(1, changed);
            Assert.IsFalse(new SettingsService(save, new EventBus()).DayNightOn, "mở lại app vẫn tắt");
        }
    }

    /// <summary>Bé ban đêm (Phase 10.1): buồn ngủ sớm hơn, ngủ một mạch tới sáng, bị đánh thức thì thức chơi thêm 1 lúc.</summary>
    public class NightBrainTests
    {
        private static GardenFixture Rested(ulong seed, float energy, Func<bool> bedtime)
        {
            var f = new GardenFixture(seed);
            f.Brain.Bedtime = bedtime;
            f.Pets.SetNeedForDebug(NeedType.Energy, energy);
            f.Pets.SetNeedForDebug(NeedType.Satiety, 90f);   // không đói (đói thì ra chòi ăn đứng chờ)
            return f;
        }

        [Test]
        public void Bedtime_PetGoesToBedEarlier()
        {
            GardenFixture day = Rested(3, 45f, () => false);
            day.Simulate(30f);
            Assert.IsFalse(day.Pets.Current.IsSleeping, "ban ngày 45 năng lượng chưa buồn ngủ");
            GardenFixture night = Rested(3, 45f, () => true);
            Assert.AreEqual(night.Cfg.NightSleepyBelowEnergy, night.Brain.SleepyThreshold);
            night.Simulate(30f);
            Assert.IsTrue(night.Pets.Current.IsSleeping, "đêm: tự về ổ ngủ");
            GardenFixture fresh = Rested(3, 80f, () => true);
            fresh.Simulate(30f);
            Assert.IsFalse(fresh.Pets.Current.IsSleeping, "đêm mà còn tỉnh táo thì chưa ngủ");
        }

        [Test]
        public void Bedtime_SleepsThroughTheNight_WakesInTheMorning()
        {
            bool bedtime = true;
            GardenFixture f = Rested(4, 40f, () => bedtime);
            f.Simulate(30f);
            Assert.IsTrue(f.Pets.Current.IsSleeping);
            f.Pets.SetNeedForDebug(NeedType.Energy, 100f);   // ngủ đủ giấc
            f.Simulate(10f);
            Assert.IsTrue(f.Pets.Current.IsSleeping, "giữa đêm đủ năng lượng vẫn ngủ tiếp tới sáng");
            bedtime = false;   // trời sáng
            f.Simulate(2f);
            Assert.IsFalse(f.Pets.Current.IsSleeping, "sáng ra tự dậy");
        }

        [Test]
        public void WokenAtNight_StaysUpForAWhile_ThenSleepyAgain()
        {
            GardenFixture f = Rested(5, 40f, () => true);
            f.Simulate(30f);
            Assert.IsTrue(f.Pets.Current.IsSleeping);
            f.Brain.OnTapped();   // bạn chạm đánh thức
            Assert.IsFalse(f.Pets.Current.IsSleeping);
            Assert.AreEqual(f.Cfg.SleepyBelowEnergy, f.Brain.SleepyThreshold, "vừa bị đánh thức: chưa buồn ngủ lại");
            f.Simulate(60f);
            Assert.IsFalse(f.Pets.Current.IsSleeping, "thức chơi thêm 1 lúc");
            f.Simulate(f.Cfg.NightWakeSnoozeSeconds + 20f);
            Assert.IsTrue(f.Pets.Current.IsSleeping, "rồi lại buồn ngủ, tự về ổ");
        }

        [Test]
        public void NoBedtimeProvider_BehavesLikeDay()
        {
            GardenFixture f = Rested(6, 45f, null);
            Assert.AreEqual(f.Cfg.SleepyBelowEnergy, f.Brain.SleepyThreshold);
            f.Simulate(20f);
            Assert.IsFalse(f.Pets.Current.IsSleeping);
        }
    }
}
