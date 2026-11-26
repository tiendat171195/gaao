using System;
using System.Numerics;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    public class GardenLayoutTests
    {
        [Test]
        public void DefaultLayout_PlacesZonesAsDesigned()
        {
            GardenLayout l = GardenLayout.CreateDefault();
            Assert.Less(l.Get(ZoneType.FoodHut).Center.X, -3f, "chòi ăn bên trái");
            Assert.Greater(l.Get(ZoneType.Pond).Center.X, 3f, "hồ tắm bên phải");
            Assert.Greater(l.Get(ZoneType.TrainingGround).Center.Y, 5f, "sân tập phía sau");
            Assert.Less(l.Get(ZoneType.Bed).Center.Length(), 1.5f, "ổ ngủ ở giữa");
            GardenZone m = l.Get(ZoneType.Memorial);
            Assert.IsTrue(Math.Abs(m.Center.X) > 6f && m.Center.Y > 9f, "tưởng niệm ở góc");
            foreach (GardenZone z in l.Zones) Assert.IsTrue(l.Contains(z.Center, 1f), z.Type + " phải nằm trong vườn");
        }

        [Test]
        public void ZoneAt_AndClamp()
        {
            GardenLayout l = GardenLayout.CreateDefault();
            Assert.AreEqual(ZoneType.Pond, l.ZoneAt(l.Get(ZoneType.Pond).Center));
            Assert.AreEqual(ZoneType.None, l.ZoneAt(l.PetSpawn));
            Vector2 c = l.Clamp(new Vector2(999f, -999f), 1f);
            Assert.AreEqual(l.Max.X - 1f, c.X, 1e-4);
            Assert.AreEqual(l.Min.Y + 1f, c.Y, 1e-4);
        }
    }

    public class GestureTests
    {
        private PointerGestureClassifier New() => new PointerGestureClassifier(0.3f, 14f);

        [Test]
        public void QuickStillPress_IsTap()
        {
            PointerGestureClassifier g = New();
            g.Down(new Vector2(100, 100), 0.0, PointerTarget.Ground);
            Assert.IsNull(g.Move(new Vector2(105, 102), 0.05));
            GestureEvent? e = g.Up(new Vector2(105, 102), 0.12);
            Assert.IsTrue(e.HasValue);
            Assert.AreEqual(GestureKind.Tap, e.Value.Kind);
            Assert.AreEqual(PointerTarget.Ground, e.Value.Target);
        }

        [Test]
        public void MovingPastThreshold_StartsDrag_KeepsTarget_EndsDrag()
        {
            PointerGestureClassifier g = New();
            g.Down(new Vector2(0, 0), 0.0, PointerTarget.Pet);
            GestureEvent? s = g.Move(new Vector2(20, 0), 0.05);
            Assert.AreEqual(GestureKind.DragStart, s.Value.Kind);
            Assert.AreEqual(PointerTarget.Pet, s.Value.Target);
            Assert.AreEqual(new Vector2(0, 0), s.Value.StartPosition);
            Assert.AreEqual(GestureKind.DragMove, g.Move(new Vector2(60, 10), 0.2).Value.Kind);
            GestureEvent? end = g.Up(new Vector2(80, 10), 1.5);
            Assert.AreEqual(GestureKind.DragEnd, end.Value.Kind);
            Assert.IsFalse(g.IsDown);
        }

        [Test]
        public void LongStillPress_IsNothing()
        {
            PointerGestureClassifier g = New();
            g.Down(new Vector2(0, 0), 0.0, PointerTarget.Ground);
            Assert.IsNull(g.Up(new Vector2(2, 2), 1.0));
            Assert.IsNull(g.Up(new Vector2(2, 2), 1.1), "nhấc tay khi chưa chạm xuống: bỏ qua");
            Assert.IsNull(g.Move(new Vector2(50, 50), 1.2));
        }
    }

    internal sealed class GardenFixture
    {
        public static readonly DateTime T0 = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);
        public readonly ManualTimeProvider Time = new ManualTimeProvider(T0);
        public readonly GardenConfig Cfg = new GardenConfig();
        public readonly GardenLayout Layout = GardenLayout.CreateDefault();
        public readonly PetService Pets;
        public readonly HealthService Health;
        public readonly EventBus Bus = new EventBus();
        public readonly CareActionService Care;
        public readonly PetBrain Brain;
        public Vector2 Pos;

        public GardenFixture(ulong seed = 1)
        {
            EventBus bus = Bus;
            var gameCfg = GameConfig.CreateDefault();
            Pets = new PetService(Time, new SeededRandom(seed), new InMemorySaveService(new NewtonsoftSerializer()),
                                  bus, gameCfg);
            Pets.LoadOrCreate("Kem");
            Health = new HealthService(Pets, Time, gameCfg);
            Care = new CareActionService(Pets, Health, Cfg, Time, bus);
            Brain = new PetBrain(Layout, Cfg, new SeededRandom(seed + 7), Pets, Care);
            Pos = Layout.PetSpawn;
        }

        /// <summary>Mô phỏng: thú đi thẳng tới đích của bộ não với tốc độ đi / chạy.</summary>
        public void Simulate(float seconds, float dt = 0.1f)
        {
            for (float t = 0; t < seconds; t += dt)
            {
                Brain.Update(dt, Pos);
                if (!Brain.IsMoving) continue;
                float speed = Brain.Run ? Cfg.RunSpeed : Cfg.WalkSpeed;
                Vector2 d = Brain.Target - Pos;
                float len = d.Length();
                Pos = len <= speed * dt ? Brain.Target : Pos + d / len * speed * dt;
            }
        }
    }

    public class CareActionTests
    {
        [Test]
        public void Feed_RaisesSatiety_AndRefusesWhenFull()
        {
            var f = new GardenFixture();
            float before = f.Pets.Current.Needs.Satiety;
            Assert.AreEqual(CareResult.Ok, f.Care.Feed());
            Assert.AreEqual(Math.Min(100f, before + f.Cfg.FeedSatiety), f.Pets.Current.Needs.Satiety, 0.01f);
            f.Pets.Care(NeedType.Satiety, 100f);
            Assert.AreEqual(CareResult.Refused, f.Care.Feed(), "no rồi thì lắc đầu");
        }

        [Test]
        public void Feed_RefusedWhileSleeping()
        {
            var f = new GardenFixture();
            Assert.AreEqual(CareResult.Ok, f.Care.Sleep());
            Assert.AreEqual(CareResult.Refused, f.Care.Feed());
        }

        [Test]
        public void Bathe_RaisesHygiene_AndWakes()
        {
            var f = new GardenFixture();
            f.Time.Advance(TimeSpan.FromHours(10));
            f.Care.Sleep();
            float before = f.Pets.Current.Needs.Hygiene;
            f.Care.Bathe();
            Assert.Greater(f.Pets.Current.Needs.Hygiene, before);
            Assert.IsFalse(f.Pets.Current.IsSleeping);
        }

        [Test]
        public void Pet_AddsMood_OnlyOncePerCooldown()
        {
            var f = new GardenFixture();
            f.Time.Advance(TimeSpan.FromHours(4));
            f.Pets.Tick();
            float before = f.Pets.Current.Needs.Mood;
            f.Care.Pet();
            f.Care.Pet();
            f.Care.Pet();
            Assert.AreEqual(before + f.Cfg.PetMood, f.Pets.Current.Needs.Mood, 0.01f);
            f.Time.Advance(TimeSpan.FromSeconds(f.Cfg.PetMoodCooldownSeconds + 1));
            f.Care.Pet();
            Assert.Greater(f.Pets.Current.Needs.Mood, before + f.Cfg.PetMood);
        }

        [Test]
        public void Sleep_RefusedWhenEnergyHigh()
        {
            var f = new GardenFixture();
            f.Pets.Care(NeedType.Energy, 100f);
            Assert.AreEqual(CareResult.Refused, f.Care.Sleep());
            Assert.IsFalse(f.Pets.Current.IsSleeping);
        }
    }

    public class PetBrainTests
    {
        [Test]
        public void Idle_ThenWanders_InsideGarden_OutsideZones()
        {
            var f = new GardenFixture();
            bool wandered = false;
            for (int i = 0; i < 300 && !wandered; i++)
            {
                f.Simulate(0.1f);
                if (f.Brain.Activity == PetActivity.Wander)
                {
                    wandered = true;
                    Assert.IsTrue(f.Layout.Contains(f.Brain.Target, 0.9f));
                    Assert.AreEqual(ZoneType.None, f.Layout.ZoneAt(f.Brain.Target));
                }
            }
            Assert.IsTrue(wandered);
        }

        [Test]
        public void TapGround_RunsThere_ThenWaitsForPlayer()
        {
            var f = new GardenFixture();
            var dest = new Vector2(3f, 4f);
            f.Brain.CommandMoveTo(dest, f.Pos);
            Assert.AreEqual(PetActivity.MoveTo, f.Brain.Activity);
            Assert.IsTrue(f.Brain.Run);
            f.Simulate(5f);
            Assert.Less(Vector2.Distance(f.Pos, dest), f.Cfg.ArriveDistance + 0.01f);
            Assert.AreEqual(PetActivity.Idle, f.Brain.Activity);
            f.Simulate(3f);
            Assert.AreEqual(PetActivity.Idle, f.Brain.Activity, "vừa nghe lệnh xong: chưa tự đi lang thang ngay");
        }

        [Test]
        public void Sleepy_GoesToBed_AndSleeps()
        {
            var f = new GardenFixture();
            f.Pets.Current.Needs.Energy = 10f;
            f.Simulate(20f);
            Assert.AreEqual(PetActivity.Sleeping, f.Brain.Activity);
            Assert.IsTrue(f.Pets.Current.IsSleeping);
            Assert.Less(Vector2.Distance(f.Pos, f.Layout.Get(ZoneType.Bed).UsePoint), 0.5f);
        }

        [Test]
        public void Hungry_GoesToFoodHut()
        {
            var f = new GardenFixture();
            f.Pets.Current.Needs.Satiety = 10f;
            f.Simulate(2f);
            Assert.AreEqual(PetActivity.GoingToZone, f.Brain.Activity);
            Assert.AreEqual(ZoneType.FoodHut, f.Brain.ZoneGoal);
            Assert.AreEqual(f.Layout.Get(ZoneType.FoodHut).UsePoint, f.Brain.Target);
        }

        [Test]
        public void LeadAndDropIntoPond_Bathes()
        {
            var f = new GardenFixture();
            f.Time.Advance(TimeSpan.FromHours(12));
            f.Pets.Tick();
            float before = f.Pets.Current.Needs.Hygiene;
            Vector2 pond = f.Layout.Get(ZoneType.Pond).Center;
            f.Brain.BeginLead(f.Pos);
            f.Brain.Lead(pond);
            f.Simulate(1f);
            f.Brain.EndLead(pond, f.Pos);
            Assert.AreEqual(PetActivity.GoingToZone, f.Brain.Activity);
            f.Simulate(8f);
            Assert.Greater(f.Pets.Current.Needs.Hygiene, before, "đã tắm");
        }

        [Test]
        public void TapWhileSleeping_WakesUp_TapAwake_Reacts()
        {
            var f = new GardenFixture();
            f.Care.Sleep();
            f.Simulate(0.2f);
            Assert.AreEqual(PetActivity.Sleeping, f.Brain.Activity);
            f.Brain.OnTapped();
            Assert.IsFalse(f.Pets.Current.IsSleeping);
            f.Brain.OnTapped();
            Assert.AreEqual(PetActivity.Reacting, f.Brain.Activity);
            f.Simulate(f.Cfg.ReactSeconds + 0.2f);
            Assert.AreEqual(PetActivity.Idle, f.Brain.Activity);
        }

        [Test]
        public void SleepingPet_WakesByItself_WhenRested()
        {
            var f = new GardenFixture();
            f.Care.Sleep();
            f.Time.Advance(TimeSpan.FromHours(3));    // 80 + 3 x 15 > 95
            f.Pets.Tick();
            f.Simulate(0.3f);
            Assert.IsFalse(f.Pets.Current.IsSleeping);
            Assert.AreNotEqual(PetActivity.Sleeping, f.Brain.Activity);
        }
    }

    /// <summary>Thú ốm trong vườn (Phase 2.1).</summary>
    public class SickPetGardenTests
    {
        private static GardenFixture SickFixture(double neglectHours = 80)
        {
            var f = new GardenFixture();
            f.Pets.SetNeglectForDebug(neglectHours);
            Assert.IsTrue(f.Health.IsIll);
            return f;
        }

        [Test]
        public void SickPet_RefusesPond_EvenWhenDroppedThere()
        {
            GardenFixture f = SickFixture();
            float hygiene = f.Pets.Current.Needs.Hygiene;
            Assert.AreEqual(CareResult.Refused, f.Care.Bathe());
            Vector2 pond = f.Layout.Get(ZoneType.Pond).Center;
            f.Brain.BeginLead(f.Pos);
            f.Brain.Lead(pond);
            f.Simulate(1f);
            f.Brain.EndLead(pond, f.Pos);
            f.Simulate(12f);
            Assert.AreNotEqual(PetActivity.Bathing, f.Brain.Activity);
            Assert.AreEqual(hygiene, f.Pets.Current.Needs.Hygiene, 0.01f, "không tắm được");
        }

        [Test]
        public void SickPet_PorridgeBlanketWipe_IsOneCarefulRound()
        {
            GardenFixture f = SickFixture();
            Assert.AreEqual(CareResult.Ok, f.Care.Feed(), "cho ăn = bát cháo");
            f.Pets.Care(NeedType.Energy, 100f);
            Assert.AreEqual(CareResult.Ok, f.Care.Sleep(), "ốm thì nằm nghỉ được dù chưa buồn ngủ (đắp chăn)");
            Assert.IsFalse(f.Health.IsRecovering);
            float hygiene = f.Pets.Current.Needs.Hygiene;
            f.Brain.OnTapped();                                   // chạm = lau người
            Assert.Greater(f.Pets.Current.Needs.Hygiene, hygiene);
            Assert.IsTrue(f.Pets.Current.IsSleeping, "lau người không đánh thức thú");
            Assert.IsTrue(f.Health.IsRecovering, "đủ cháo + chăn + lau: bắt đầu hồi phục");
        }

        [Test]
        public void Wipe_PublishesWipe_AndHasCooldown()
        {
            GardenFixture f = SickFixture();
            var kinds = new System.Collections.Generic.List<CareKind>();
            f.Bus.Subscribe<CareDone>(e => kinds.Add(e.Kind));
            float h0 = f.Pets.Current.Needs.Hygiene;
            f.Care.Pet();
            f.Care.Pet();
            Assert.AreEqual(h0 + f.Cfg.WipeHygiene, f.Pets.Current.Needs.Hygiene, 0.01f, "lau liên tục chỉ tính 1 lần mỗi khoảng hồi");
            Assert.AreEqual(CareKind.Wipe, kinds[0]);
        }

        [Test]
        public void SickPet_WalksSlower_NeverRuns_AndRests()
        {
            GardenFixture f = SickFixture();
            Assert.AreEqual(f.Cfg.SickSpeedFactor, f.Brain.SpeedFactor);
            f.Brain.CommandMoveTo(new Vector2(3f, 4f), f.Pos);
            Assert.IsFalse(f.Brain.Run, "ốm không chạy");
            f.Pets.Current.Needs.Energy = 30f;
            f.Pets.Current.Needs.Satiety = 90f;
            f.Simulate(25f);
            Assert.AreEqual(PetActivity.Sleeping, f.Brain.Activity, "mệt là về ổ nằm nghỉ");
            GardenFixture c = SickFixture(130);
            Assert.AreEqual(f.Cfg.CriticalSpeedFactor, c.Brain.SpeedFactor);
        }

        [Test]
        public void HealthyAgain_AfterTiredIsCared_RunsNormally()
        {
            var f = new GardenFixture();
            f.Pets.SetNeglectForDebug(30);
            Assert.AreEqual(f.Cfg.TiredSpeedFactor, f.Brain.SpeedFactor);
            foreach (NeedType t in new[] { NeedType.Satiety, NeedType.Hygiene, NeedType.Energy, NeedType.Mood })
                f.Pets.Care(t, 100f);
            Assert.AreEqual(1f, f.Brain.SpeedFactor);
            f.Brain.CommandMoveTo(new Vector2(3f, 4f), f.Pos);
            Assert.IsTrue(f.Brain.Run);
        }

        [Test]
        public void Expression_ShowsSickness()
        {
            GardenFixture f = SickFixture(130);
            var expr = new PetExpressionService(f.Pets, f.Brain, new ExpressionConfig(), new SeededRandom(1));
            expr.Update(0.1f);
            PetExpression e = expr.Current;
            Assert.IsTrue(e.Has(PetCondition.Critical));
            Assert.AreEqual(1f, e.Sickness);
            Assert.LessOrEqual(e.EyeOpen, new ExpressionConfig().CriticalEyeOpen);
            Assert.GreaterOrEqual(e.Droop, new ExpressionConfig().CriticalDroop);
            f.Health.GiveMedicine();
            expr.Update(0.1f);
            Assert.IsTrue(expr.Current.Has(PetCondition.Recovering));
        }

        [Test]
        public void DeadPet_BrainAndCareDoNothing()
        {
            GardenFixture f = SickFixture();
            f.Pets.SetNeglectForDebug(500);
            Assert.IsFalse(f.Pets.HasPet);
            Assert.DoesNotThrow(() => f.Simulate(1f));
            Assert.AreEqual(CareResult.Refused, f.Care.Feed());
            Assert.DoesNotThrow(() => f.Brain.OnTapped());
            var expr = new PetExpressionService(f.Pets, f.Brain, new ExpressionConfig(), new SeededRandom(1));
            Assert.AreEqual(ExpressionCue.None, expr.Update(0.1f));
        }
    }
}
