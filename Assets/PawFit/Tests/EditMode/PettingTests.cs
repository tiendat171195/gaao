using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Vuốt ve (Phase 8.2): phân biệt xoa trên người thú với kéo thú đi; vuốt ve cộng vui, thú đứng yên tận hưởng.</summary>
    public class PettingTests
    {
        private static readonly Vector2 Pet = new Vector2(500, 800);
        private const float R = 120f;

        /// <summary>Kéo qua các điểm, mỗi đoạn chia nhỏ 6 pixel (như ngón tay thật gửi nhiều điểm).</summary>
        private static DragIntent Drag(StrokeTracker t, params Vector2[] path)
        {
            t.Begin(path[0]);
            DragIntent last = DragIntent.Undecided;
            for (int i = 1; i < path.Length; i++)
            {
                Vector2 a = path[i - 1], b = path[i];
                int n = Math.Max(1, (int)(Vector2.Distance(a, b) / 6f));
                for (int k = 1; k <= n; k++) last = t.Move(Vector2.Lerp(a, b, k / (float)n), Pet, R);
            }
            return last;
        }

        [Test]
        public void DragOutOfPet_IsLead()
        {
            var t = new StrokeTracker();
            Assert.AreEqual(DragIntent.Lead, Drag(t, Pet, Pet + new Vector2(400, 0)), "kéo thẳng ra khỏi người thú = dắt đi");
            Assert.AreEqual(0, t.TakePasses());
        }

        [Test]
        public void RubBackAndForth_IsStroke_CountsPasses()
        {
            var t = new StrokeTracker();
            Vector2 left = Pet + new Vector2(-70, 0), right = Pet + new Vector2(70, 0);
            DragIntent i = Drag(t, left, right, left, right, left, right);
            Assert.AreEqual(DragIntent.Stroke, i, "xoa qua lại trên lưng = vuốt ve");
            Assert.GreaterOrEqual(t.Reversals, 4);
            int passes = t.TakePasses();
            Assert.That(passes, Is.InRange(5, 8), "5 lần xoa 140 px ≈ 700 px / (0,8 × 120) lượt");
            Assert.AreEqual(0, t.TakePasses(), "lấy rồi thì về 0");
        }

        [Test]
        public void CircleRub_IsStroke_ThenDragFarAway_BecomesLead()
        {
            var t = new StrokeTracker();
            var circle = new List<Vector2>();
            for (int k = 0; k <= 24; k++)
            {
                double a = k / 24.0 * Math.PI * 2;
                circle.Add(Pet + new Vector2((float)Math.Cos(a) * 50, (float)Math.Sin(a) * 50));
            }
            Assert.AreEqual(DragIntent.Stroke, Drag(t, circle.ToArray()), "xoa vòng vòng = vuốt ve");
            DragIntent after = DragIntent.Stroke;
            for (int k = 1; k <= 80; k++) after = t.Move(circle[circle.Count - 1] + new Vector2(k * 6, 0), Pet, R);
            Assert.AreEqual(DragIntent.Lead, after, "đang vuốt mà kéo hẳn ra xa = chuyển sang dắt");
        }

        [Test]
        public void SmallWiggle_StaysUndecided()
        {
            var t = new StrokeTracker();
            Assert.AreEqual(DragIntent.Undecided, Drag(t, Pet, Pet + new Vector2(20, 5)), "rung tay chút xíu: chưa tính gì (nhấc tay = chạm)");
        }

        [Test]
        public void Stroking_RaisesMood_WithCooldown_PetStaysStill()
        {
            var f = new GardenFixture();
            f.Pets.SetNeedForDebug(NeedType.Mood, 40f);
            int cuddles = 0;
            f.Bus.Subscribe<CareDone>(e =>
            {
                if (e.Kind == CareKind.Pet) cuddles++;
            });
            f.Brain.BeginPetting();
            Assert.AreEqual(PetActivity.Petted, f.Brain.Activity);
            Assert.IsFalse(f.Brain.IsMoving, "đang được vuốt thì đứng yên");
            f.Brain.Stroke();
            f.Brain.Stroke();   // cùng lúc: chỉ cộng 1 lần (chờ 0,6 giây)
            Assert.AreEqual(40f + f.Cfg.StrokeMood, f.Pets.Current.Needs.Mood, 0.01f);
            for (int i = 0; i < 10; i++)
            {
                f.Time.Advance(TimeSpan.FromSeconds(1));
                f.Brain.Stroke();
            }
            Assert.AreEqual(40f + 11 * f.Cfg.StrokeMood, f.Pets.Current.Needs.Mood, 0.5f, "xoa 10 giây ≈ +16 vui");
            Assert.AreEqual(11, cuddles, "mỗi lượt được cộng báo CareDone (nhiệm vụ vuốt ve đếm được)");
            f.Simulate(5f);
            Assert.AreEqual(PetActivity.Petted, f.Brain.Activity, "chưa thả tay thì vẫn đứng tận hưởng");
            f.Brain.EndPetting();
            Assert.AreEqual(PetActivity.Reacting, f.Brain.Activity, "thả tay: phản ứng vui");
        }

        [Test]
        public void SleepingPet_CanBeStroked_WithoutWaking()
        {
            var f = new GardenFixture();
            f.Pets.SetNeedForDebug(NeedType.Mood, 30f);
            f.Pets.SetSleeping(true);
            f.Brain.Update(0.1f, f.Pos);
            f.Brain.BeginPetting();
            f.Brain.Stroke();
            Assert.IsTrue(f.Pets.Current.IsSleeping, "vuốt nhẹ không đánh thức");
            Assert.AreEqual(PetActivity.Sleeping, f.Brain.Activity);
            Assert.Greater(f.Pets.Current.Needs.Mood, 30f, "vẫn vui lên");
        }
    }
}
