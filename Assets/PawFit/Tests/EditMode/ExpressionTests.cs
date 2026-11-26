using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    public class PetExpressionTests
    {
        private sealed class Rig
        {
            public readonly GardenFixture F = new GardenFixture();
            public readonly ExpressionConfig Cfg = new ExpressionConfig();
            public readonly PetExpressionService Expr;

            public Rig() => Expr = new PetExpressionService(F.Pets, F.Brain, Cfg, new SeededRandom(3));

            public PetNeeds Needs => F.Pets.Current.Needs;

            /// <summary>Chạy biểu cảm `seconds` giây (không chạy bộ não), trả về các cue theo thứ tự + thời điểm.</summary>
            public List<(float t, ExpressionCue cue)> Run(float seconds, float dt = 0.05f)
            {
                var cues = new List<(float, ExpressionCue)>();
                for (float t = 0f; t < seconds; t += dt)
                {
                    ExpressionCue c = Expr.Update(dt);
                    if (c != ExpressionCue.None) cues.Add((t, c));
                }
                return cues;
            }
        }

        [Test]
        public void HealthyPet_ShowsNothing()
        {
            var r = new Rig();
            Assert.IsEmpty(r.Run(30f));
            PetExpression e = r.Expr.Current;
            Assert.AreEqual(PetCondition.None, e.Conditions);
            Assert.AreEqual(1f, e.EyeOpen);
            Assert.AreEqual(0, e.Flies);
            Assert.AreEqual(0f, e.Dirt);
            Assert.IsFalse(e.LookAtFood);
        }

        [Test]
        public void Hungry_LooksAtFoodHut_ThenCries_AndRepeats()
        {
            var r = new Rig();
            r.Needs.Satiety = 10f;
            List<(float t, ExpressionCue cue)> cues = r.Run(40f);
            Assert.IsTrue(r.Expr.Current.Has(PetCondition.Hungry));
            Assert.GreaterOrEqual(cues.Count, 4, "40 giây phải đòi ăn ít nhất 2 lần (nhìn + kêu)");
            Assert.AreEqual(ExpressionCue.LookAtFood, cues[0].cue, "nhìn chòi ăn trước");
            Assert.AreEqual(r.Cfg.FirstCryDelaySeconds, cues[0].t, 0.1f);
            Assert.AreEqual(ExpressionCue.Cry, cues[1].cue, "rồi mới kêu");
            Assert.AreEqual(r.Cfg.CryAfterLookSeconds, cues[1].t - cues[0].t, 0.1f);
            Assert.AreEqual(ExpressionCue.LookAtFood, cues[2].cue);
            float gap = cues[2].t - cues[0].t - r.Cfg.LookAtFoodSeconds;
            Assert.That(gap, Is.InRange(r.Cfg.CryIntervalSeconds * 0.69f, r.Cfg.CryIntervalSeconds * 1.31f));
        }

        [Test]
        public void Hungry_LookFlag_LastsWholeLook()
        {
            var r = new Rig();
            r.Needs.Satiety = 10f;
            r.Run(r.Cfg.FirstCryDelaySeconds + 0.1f);
            Assert.IsTrue(r.Expr.Current.LookAtFood);
            r.Run(r.Cfg.CryAfterLookSeconds + 0.1f);
            Assert.IsTrue(r.Expr.Current.LookAtFood, "vẫn nhìn chòi ăn trong lúc kêu");
            r.Run(r.Cfg.LookAtFoodSeconds);
            Assert.IsFalse(r.Expr.Current.LookAtFood);
        }

        [Test]
        public void Hungry_ButBusyEating_DoesNotCry()
        {
            var r = new Rig();
            r.Needs.Satiety = 10f;
            r.F.Brain.OnFed();                 // bộ não đứng yên ở trạng thái Eating vì test không chạy bộ não
            Assert.AreEqual(PetActivity.Eating, r.F.Brain.Activity);
            Assert.IsEmpty(r.Run(20f));
        }

        [Test]
        public void Dirty_ShowsFliesAndDirt_BathingCleans()
        {
            var r = new Rig();
            r.Needs.Hygiene = 30f;
            r.Run(0.1f);
            PetExpression a = r.Expr.Current;
            Assert.IsTrue(a.Has(PetCondition.Dirty));
            Assert.AreEqual(r.Cfg.FliesDirty, a.Flies);
            Assert.That(a.Dirt, Is.InRange(0.3f, 1f));
            r.Needs.Hygiene = 5f;
            r.Run(0.1f);
            Assert.AreEqual(r.Cfg.FliesVeryDirty, r.Expr.Current.Flies);
            Assert.Greater(r.Expr.Current.Dirt, a.Dirt);
            r.F.Care.Bathe();
            r.Run(0.1f);
            Assert.AreEqual(0, r.Expr.Current.Flies);
            Assert.AreEqual(0f, r.Expr.Current.Dirt);
        }

        [Test]
        public void Sleepy_DroopsEyes_AndYawns()
        {
            var r = new Rig();
            r.Needs.Energy = 25f;
            r.Run(0.1f);
            float mild = r.Expr.Current.EyeOpen;
            r.Needs.Energy = 5f;
            List<(float t, ExpressionCue cue)> cues = r.Run(r.Cfg.FirstYawnDelaySeconds + 0.5f);
            PetExpression e = r.Expr.Current;
            Assert.IsTrue(e.Has(PetCondition.Sleepy));
            Assert.That(e.EyeOpen, Is.InRange(r.Cfg.SleepyEyeOpenMin, r.Cfg.SleepyEyeOpenMax));
            Assert.Less(e.EyeOpen, mild, "càng mệt mắt càng díp");
            Assert.Greater(e.Droop, 0f);
            Assert.AreEqual(1, cues.Count);
            Assert.AreEqual(ExpressionCue.Yawn, cues[0].cue);
        }

        [Test]
        public void Sleeping_ClosesEyes_AndNeverCries()
        {
            var r = new Rig();
            Assert.AreEqual(CareResult.Ok, r.F.Care.Sleep());
            r.Needs.Satiety = 5f;
            Assert.IsEmpty(r.Run(30f));
            PetExpression e = r.Expr.Current;
            Assert.IsTrue(e.Has(PetCondition.Sleeping));
            Assert.IsFalse(e.Has(PetCondition.Hungry), "đang ngủ thì không đòi ăn");
            Assert.AreEqual(0f, e.EyeOpen);
            Assert.AreEqual(1f, e.Droop);
        }
    }
}
