using System;
using System.Numerics;
using NUnit.Framework;
using PawFit.Garden;

namespace PawFit.Tests
{
    /// <summary>Camera vườn zoom / xoay / đổi góc (Phase 9.1).</summary>
    public class CameraOrbitTests
    {
        private const float H = 1920f;

        /// <summary>2 ngón đặt ngang giữa màn hình, cách nhau `gap` pixel, xoay `deg` độ quanh tâm.</summary>
        private static void Fingers(float gap, float deg, float dy, out Vector2 a, out Vector2 b)
        {
            var c = new Vector2(540f, 960f + dy);
            double r = deg * Math.PI / 180.0;
            var half = new Vector2((float)Math.Cos(r), (float)Math.Sin(r)) * (gap * 0.5f);
            a = c - half;
            b = c + half;
        }

        private static void Gesture(CameraOrbit o, Func<int, (float gap, float deg, float dy)> at, int steps)
        {
            var s = at(0);
            Fingers(s.gap, s.deg, s.dy, out Vector2 a, out Vector2 b);
            o.BeginTwoFinger(a, b);
            for (int i = 1; i <= steps; i++)
            {
                s = at(i);
                Fingers(s.gap, s.deg, s.dy, out a, out b);
                o.MoveTwoFinger(a, b, H);
            }
            o.EndTwoFinger();
        }

        [Test]
        public void Defaults_AndLimits()
        {
            var o = new CameraOrbit();
            Assert.IsTrue(o.IsDefault);
            o.Set(400f, 5f, 100f);
            Assert.AreEqual(40f, o.Yaw, 0.01f, "xoay quấn vòng 360°");
            Assert.AreEqual(o.Config.MinPitch, o.Pitch, 0.01f);
            Assert.AreEqual(o.Config.MaxDistance, o.Distance, 0.01f);
            Assert.IsFalse(o.IsDefault);
            o.Reset();
            Assert.IsTrue(o.IsDefault);
        }

        [Test]
        public void PinchOut_ZoomsIn_PinchIn_ZoomsOut_WithLimits()
        {
            var o = new CameraOrbit();
            float d0 = o.Distance;
            Gesture(o, i => (300f + i * 10f, 0f, 0f), 15);   // mở 2 ngón 300 → 450 px
            Assert.Less(o.Distance, d0, "mở 2 ngón = lại gần");
            Assert.AreEqual(0f, CameraOrbit.DeltaAngle(0f, o.Yaw), 0.01f, "chỉ zoom, không xoay");
            Gesture(o, i => (600f - i * 20f, 0f, 0f), 25);   // chụm mạnh
            Assert.Greater(o.Distance, d0);
            for (int k = 0; k < 6; k++) Gesture(o, i => (600f - i * 20f, 0f, 0f), 25);
            Assert.AreEqual(o.Config.MaxDistance, o.Distance, 0.01f, "không ra xa quá giới hạn");
        }

        [Test]
        public void SmallJitter_DoesNothing()
        {
            var o = new CameraOrbit();
            Gesture(o, i => (300f + (i % 2) * 6f, (i % 2) * 3f, (i % 2) * 20f), 20);   // rung tay nhẹ
            Assert.IsTrue(o.IsDefault, "rung tay dưới ngưỡng: không đổi góc");
        }

        [Test]
        public void Twist_RotatesGarden_SameWayAsFingers()
        {
            var o = new CameraOrbit();
            Gesture(o, i => (300f, i * 2f, 0f), 20);   // xoay ngược chiều kim đồng hồ 40°
            float yaw = CameraOrbit.DeltaAngle(0f, o.Yaw);
            Assert.Greater(yaw, 20f, "xoay theo 2 ngón (trừ phần dưới ngưỡng lúc đầu)");
            Assert.Less(yaw, 41f);
            Assert.AreEqual(o.Config.DefaultDistance, o.Distance, 0.2f, "xoay giữ khoảng cách 2 ngón thì không zoom");
            Gesture(o, i => (300f, -i * 2f, 0f), 20);
            Assert.Less(Math.Abs(CameraOrbit.DeltaAngle(0f, o.Yaw)), 1f, "xoay ngược lại thì về chỗ cũ");
        }

        [Test]
        public void TwoFingerVerticalDrag_Tilts()
        {
            var o = new CameraOrbit();
            float p0 = o.Pitch;
            Gesture(o, i => (300f, 0f, i * 20f), 15);   // 2 ngón cùng kéo lên 300 px
            Assert.Less(o.Pitch, p0, "kéo lên = nhìn thấp xuống (thấy xa hơn)");
            Assert.AreEqual(o.Config.DefaultDistance, o.Distance, 0.01f, "đổi góc không zoom");
            Assert.AreEqual(0f, CameraOrbit.DeltaAngle(0f, o.Yaw), 0.01f, "đổi góc không xoay");
            Gesture(o, i => (300f, 0f, -i * 40f), 30);
            Assert.AreEqual(o.Config.MaxPitch, o.Pitch, 0.01f, "kéo xuống nhiều = nhìn từ trên cao, có giới hạn");
        }

        [Test]
        public void Mouse_WheelZooms_RightDragOrbits()
        {
            var o = new CameraOrbit();
            float d0 = o.Distance;
            o.Wheel(1f);
            Assert.AreEqual(d0 / (1f + o.Config.WheelZoomPerNotch), o.Distance, 0.01f, "lăn lên = lại gần");
            o.OrbitDrag(new Vector2(108f, 0f), new Vector2(1080f, 1920f));   // kéo phải 10% màn hình
            Assert.AreEqual(-0.1f * o.Config.OrbitDegreesPerScreen, CameraOrbit.DeltaAngle(0f, o.Yaw), 0.01f);
            float p = o.Pitch;
            o.OrbitDrag(new Vector2(0f, 192f), new Vector2(1080f, 1920f));
            Assert.AreEqual(p - 0.1f * o.Config.TiltDegreesPerScreen, o.Pitch, 0.01f, "kéo lên = nhìn thấp xuống");
        }
    }
}
