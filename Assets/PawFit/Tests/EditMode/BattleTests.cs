using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Battle;

namespace PawFit.Tests
{
    public class SwipeRecognizerTests
    {
        private readonly SwipeRecognizer _rec = new SwipeRecognizer();

        /// <summary>Tạo nét vuốt đi qua các đỉnh với tốc độ đều `speed` pixel/giây, lấy mẫu mỗi ~8 pixel.</summary>
        private static List<SwipePoint> Stroke(float speed, params float[] xy)
        {
            var pts = new List<SwipePoint> { new SwipePoint(xy[0], xy[1], 0) };
            double t = 0;
            for (int i = 2; i + 1 < xy.Length; i += 2)
            {
                float x0 = xy[i - 2], y0 = xy[i - 1], x1 = xy[i], y1 = xy[i + 1];
                float len = (float)Math.Sqrt((x1 - x0) * (x1 - x0) + (y1 - y0) * (y1 - y0));
                int n = Math.Max(1, (int)(len / 8f));
                for (int k = 1; k <= n; k++)
                {
                    float u = k / (float)n;
                    t += len / n / speed;
                    pts.Add(new SwipePoint(x0 + (x1 - x0) * u, y0 + (y1 - y0) * u, t));
                }
            }
            return pts;
        }

        /// <summary>Nét theo cung tròn tâm (cx, cy), bán kính r, từ góc `fromDeg` tới `toDeg` (như ngón cái quét).</summary>
        private static List<SwipePoint> Arc(float speed, float cx, float cy, float r, float fromDeg, float toDeg)
        {
            var xy = new List<float>();
            for (int i = 0; i <= 24; i++)
            {
                double a = (fromDeg + (toDeg - fromDeg) * i / 24.0) * Math.PI / 180.0;
                xy.Add(cx + (float)(Math.Cos(a) * r));
                xy.Add(cy + (float)(Math.Sin(a) * r));
            }
            return Stroke(speed, xy.ToArray());
        }

        [Test]
        public void StraightSwipes()
        {
            SwipeResult r = _rec.Recognize(Stroke(1500, 0, 0, 300, 12));
            Assert.AreEqual(SwipeKind.Horizontal, r.Kind);
            Assert.AreEqual(1, r.Direction);
            Assert.That(r.Quality, Is.InRange(0.7f, 1.3f));
            Assert.AreEqual(-1, _rec.Recognize(Stroke(1500, 300, 0, 0, -10)).Direction);
            Assert.AreEqual(SwipeKind.Up, _rec.Recognize(Stroke(1500, 0, 0, 15, 320)).Kind);
            Assert.AreEqual(SwipeKind.Down, _rec.Recognize(Stroke(1500, 0, 320, -10, 0)).Kind);
        }

        [Test]
        public void VShape()
        {
            Assert.AreEqual(SwipeKind.V, _rec.Recognize(Stroke(1500, 0, 220, 110, 0, 220, 220)).Kind);
        }

        [Test]
        public void ZShape_BothDirections()
        {
            Assert.AreEqual(SwipeKind.Z, _rec.Recognize(Stroke(1500, 0, 220, 220, 220, 0, 0, 220, 0)).Kind);
            Assert.AreEqual(SwipeKind.Z, _rec.Recognize(Stroke(1500, 220, 220, 0, 220, 220, 0, 0, 0)).Kind, "Z vẽ ngược chiều");
        }

        [Test]
        public void WrongSwipes()
        {
            Assert.AreEqual(SwipeKind.None, _rec.Recognize(Stroke(1500, 0, 0, 25, 0)).Kind, "quá ngắn");
            Assert.AreEqual(SwipeKind.None, _rec.Recognize(Stroke(100, 0, 0, 300, 0)).Kind, "quá chậm (3 giây)");
            var circle = new List<float>();
            for (int i = 0; i <= 24; i++)
            {
                double a = i / 24.0 * Math.PI * 2;
                circle.Add((float)(Math.Cos(a) * 120));
                circle.Add((float)(Math.Sin(a) * 120));
            }
            SwipeResult round = _rec.Recognize(Stroke(1500, circle.ToArray()));
            Assert.AreEqual(SwipeKind.None, round.Kind, "vẽ vòng tròn");
            Assert.AreEqual(SwipeFail.Unclear, round.Fail);
            Assert.AreEqual(SwipeKind.None, _rec.Recognize(null).Kind);
        }

        [Test]
        public void CurvedThumbSwipe_IsStillStraight()
        {
            Assert.AreEqual(SwipeKind.Horizontal, _rec.Recognize(Arc(1500, 0, -200, 260, 150, 30)).Kind, "cung 120° quét ngang");
            Assert.AreEqual(SwipeKind.Up, _rec.Recognize(Arc(1500, 200, 0, 260, 240, 120)).Kind, "cung 120° vuốt lên");
            Assert.AreEqual(SwipeKind.Down, _rec.Recognize(Arc(1500, -200, 0, 260, 60, -60)).Kind, "cung 120° vuốt xuống");
        }

        [Test]
        public void HookAtStartOrEnd_IsIgnored()
        {
            Assert.AreEqual(SwipeKind.Horizontal, _rec.Recognize(Stroke(1500, 0, 0, 300, 0, 260, 50)).Kind, "móc lúc nhấc tay");
            Assert.AreEqual(SwipeKind.Down, _rec.Recognize(Stroke(1500, 0, 300, 0, 0, 25, 60)).Kind, "vuốt xuống, hất lên lúc nhấc tay: vẫn là đỡ");
            Assert.AreEqual(SwipeKind.Up, _rec.Recognize(Stroke(1500, 60, 30, 0, 0, 0, 300)).Kind, "móc lúc chạm xuống");
        }

        [Test]
        public void SloppyV_RoundedOrLopsided_IsStillV()
        {
            var xy = new List<float> { 0, 220 };
            for (int i = 0; i <= 10; i++)   // đáy tròn bán kính 40
            {
                double a = Math.PI + i / 10.0 * Math.PI;
                xy.Add(110 + (float)(Math.Cos(a) * 40));
                xy.Add(40 + (float)(Math.Sin(a) * 40));
            }
            xy.Add(220);
            xy.Add(220);
            Assert.AreEqual(SwipeKind.V, _rec.Recognize(Stroke(1500, xy.ToArray())).Kind, "đáy tròn");
            Assert.AreEqual(SwipeKind.V, _rec.Recognize(Stroke(1500, 0, 120, 60, 0, 260, 300)).Kind, "lệch như dấu ✓");
            Assert.AreEqual(SwipeKind.Horizontal, _rec.Recognize(Stroke(1500, 0, 60, 150, 0, 300, 60)).Kind, "V nông quá: tính là vuốt ngang");
        }

        [Test]
        public void SloppyZ_RoundedCornersOrHook_IsStillZ()
        {
            Assert.AreEqual(SwipeKind.Z, _rec.Recognize(Stroke(1500, 0, 220, 200, 215, 220, 200, 20, 15, 0, 0, 220, 5)).Kind, "góc bo tròn");
            Assert.AreEqual(SwipeKind.Z, _rec.Recognize(Stroke(1500, 0, 220, 220, 220, 0, 0, 220, 0, 200, 30)).Kind, "móc lúc nhấc tay");
        }

        [Test]
        public void ShortOrSlowStroke_IsNotPunished_ScribbleIs()
        {
            Assert.AreEqual(SwipeFail.TooShort, _rec.Recognize(Stroke(1500, 0, 0, 40, 0)).Fail);
            Assert.AreEqual(SwipeKind.Horizontal, _rec.Recognize(Stroke(1500, 0, 0, 55, 0)).Kind, "vuốt ngắn ~9 mm vẫn nhận");
            Assert.AreEqual(SwipeFail.TooSlow, _rec.Recognize(Stroke(100, 0, 0, 300, 0)).Fail);
            Assert.AreEqual(SwipeKind.Horizontal, _rec.Recognize(Stroke(250, 0, 0, 300, 0)).Kind, "kéo chậm 1,2 giây vẫn nhận");
            SwipeResult scribble = _rec.Recognize(Stroke(1500, 0, 0, 100, 100, 0, 100, 100, 0, 0, 50, 100, 50));
            Assert.AreEqual(SwipeKind.None, scribble.Kind);
            Assert.AreEqual(SwipeFail.Unclear, scribble.Fail, "nguệch ngoạc = vuốt sai (bị hở)");
        }

        [Test]
        public void FasterSwipe_HasHigherQuality()
        {
            float slow = _rec.Recognize(Stroke(400, 0, 0, 300, 0)).Quality;
            float fast = _rec.Recognize(Stroke(2000, 0, 0, 300, 0)).Quality;
            Assert.Greater(fast, slow);
            Assert.AreEqual(1.3f, fast, 1e-3f, "vuốt nhanh + thẳng = hệ số tối đa");
        }

        [Test]
        public void MinLength_ScalesWithDpi()
        {
            var hiDpi = new SwipeRecognizer(null, 3f);   // máy 480 dpi: 60px ở 160dpi = 180px thật
            Assert.AreEqual(SwipeKind.None, hiDpi.Recognize(Stroke(3000, 0, 0, 120, 0)).Kind);
            Assert.AreEqual(SwipeKind.Horizontal, hiDpi.Recognize(Stroke(3000, 0, 0, 400, 0)).Kind);
        }
    }

    public class BattleSimulationTests
    {
        private static readonly FighterStats Plain = new FighterStats(100, 10, 0, 10, 0);

        private static SwipeResult Swipe(SwipeKind k, float q = 1f) => new SwipeResult { Kind = k, Quality = q };

        private static void Run(BattleSimulation sim, float seconds, List<BattleEvent> ev = null)
        {
            int n = (int)Math.Round(seconds / sim.Config.TickSeconds);
            for (int i = 0; i < n && !sim.IsOver; i++) sim.Tick(ev);
        }

        [Test]
        public void Damage_FollowsFormula()
        {
            var sim = new BattleSimulation(Plain, new FighterStats(100, 10, 100, 10, 0));
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.Horizontal)));
            Run(sim, 0.3f);
            Assert.AreEqual(100f * sim.Config.HpScale, sim.Fighters[1].Stats.Hp, 1e-3f, "máu trong trận = HP × HpScale");
            Assert.AreEqual(sim.Fighters[1].Stats.Hp - 10f * 1f * 1f * 100f / 200f, sim.Fighters[1].Hp, 1e-3f, "ATK × hệ số chiêu × hệ số vuốt × 100/(100+DEF)");
            Assert.AreEqual(10f * 1.6f * 1.2f * 100f / 200f, sim.BaseDamage(0, MoveKind.Heavy, 1.2f), 1e-3f);
        }

        [Test]
        public void Crit_Is150Percent()
        {
            var sim = new BattleSimulation(new FighterStats(100, 10, 0, 10, 100), Plain);
            var ev = new List<BattleEvent>();
            sim.Submit(0, Swipe(SwipeKind.Horizontal));
            Run(sim, 0.3f, ev);
            BattleEvent hit = ev.Find(e => e.Kind == BattleEventKind.Hit);
            Assert.IsTrue(hit.Crit);
            Assert.AreEqual(15f, hit.Damage, 1e-3f);
        }

        [Test]
        public void Cooldown_SwipeNearEnd_IsBuffered_ThenFiresOnTime()
        {
            var sim = new BattleSimulation(Plain, Plain);
            var ev = new List<BattleEvent>();
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.Horizontal)));
            Run(sim, 0.1f);
            Assert.IsFalse(sim.Submit(0, Swipe(SwipeKind.Horizontal)), "còn 0,5 giây hồi chiêu: quá lâu để giữ");
            Run(sim, 0.2f);   // còn ~0,3 giây
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.Up), ev), "sắp hết hồi chiêu: giữ lại");
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.Queued && e.Side == 0));
            Assert.IsFalse(ev.Exists(e => e.Kind == BattleEventKind.AttackStarted), "chưa ra đòn ngay");
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.V), ev), "nét sau thay nét trước");
            Run(sim, 0.4f, ev);
            List<BattleEvent> starts = ev.FindAll(e => e.Kind == BattleEventKind.AttackStarted);
            Assert.AreEqual(1, starts.Count, "chỉ ra 1 đòn: nét cuối cùng");
            Assert.AreEqual(MoveKind.Special, starts[0].Move);
            Assert.AreEqual(new BattleConfig().CooldownSeconds, starts[0].Time, 0.04f, "ra đòn ngay lúc hết hồi chiêu");
        }

        [Test]
        public void Guard_DuringGuardCooldown_IsBufferedToo()
        {
            var sim = new BattleSimulation(Plain, Plain);
            var ev = new List<BattleEvent>();
            Assert.IsTrue(sim.Submit(1, Swipe(SwipeKind.Down), ev));
            Run(sim, 0.3f, ev);
            Assert.IsTrue(sim.Submit(1, Swipe(SwipeKind.Down), ev), "còn 0,3 giây chờ đỡ: giữ lại");
            Run(sim, 0.4f, ev);
            Assert.AreEqual(2, ev.FindAll(e => e.Kind == BattleEventKind.Guard).Count);
        }

        [Test]
        public void WrongSwipe_LeavesOpenForHalfSecond_NoExtraPenaltyWhileBusy()
        {
            var sim = new BattleSimulation(Plain, Plain);
            Assert.IsTrue(sim.Submit(1, SwipeResult.Wrong("test")));
            Run(sim, 0.1f);
            Assert.IsFalse(sim.Submit(1, Swipe(SwipeKind.Down)), "đang hở thì không đỡ được");
            float open = sim.Fighters[1].Open;
            Assert.IsFalse(sim.Submit(1, SwipeResult.Wrong("test")), "đang bận: vuốt sai không tính");
            Assert.AreEqual(open, sim.Fighters[1].Open, 1e-6f, "không bị phạt thêm");
            Run(sim, 0.45f);
            Assert.IsTrue(sim.Submit(1, Swipe(SwipeKind.Horizontal)));
            Run(sim, 0.3f);
            Assert.IsFalse(sim.Submit(1, SwipeResult.Wrong("test")), "đang hồi chiêu: vuốt sai cũng bỏ qua");
            Assert.AreEqual(0f, sim.Fighters[1].Open);
        }

        [Test]
        public void Guard_Blocks_ParryCounters()
        {
            // đỡ sớm (~0,3 giây trước đòn): chỉ chặn, ăn 20% damage
            var sim = new BattleSimulation(Plain, Plain);
            var ev = new List<BattleEvent>();
            sim.Submit(0, Swipe(SwipeKind.Up));
            Run(sim, 0.05f, ev);
            sim.Submit(1, Swipe(SwipeKind.Down));   // đòn Mạnh vung 0,35s => còn ~0,28s
            Run(sim, 0.5f, ev);
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.Blocked));
            Assert.AreEqual(sim.Fighters[1].Stats.Hp - 10f * 1.6f * 0.2f, sim.Fighters[1].Hp, 1e-3f);

            // đỡ sát đòn (≤ 0.2 giây): phản đòn, bên tấn công mất máu + bị hở
            sim = new BattleSimulation(Plain, Plain);
            ev.Clear();
            sim.Submit(0, Swipe(SwipeKind.Up));
            Run(sim, 0.25f, ev);
            sim.Submit(1, Swipe(SwipeKind.Down));   // còn ~0,08s
            Run(sim, 0.3f, ev);
            BattleEvent counter = ev.Find(e => e.Kind == BattleEventKind.Countered && e.Side == 1);
            Assert.AreEqual(BattleEventKind.Countered, counter.Kind);
            Assert.AreEqual(1, counter.Combo, "phản đòn tính là đòn đầu của combo");
            Assert.AreEqual(sim.Fighters[1].Stats.Hp, sim.Fighters[1].Hp, 1e-3f);
            Assert.AreEqual(sim.Fighters[0].Stats.Hp - 10f * 0.8f, sim.Fighters[0].Hp, 1e-3f);
            Assert.Greater(sim.Fighters[0].Open, 0f, "bị phản đòn thì hở");
            BattleConfig c = sim.Config;
            Assert.AreEqual(c.RageOnCounter, sim.Fighters[1].Rage, 1e-3f, "phản đòn được nhiều Nộ");
            Assert.AreEqual(c.RageOnHurt, sim.Fighters[0].Rage, 1e-3f);
        }

        [Test]
        public void HeavyHit_InterruptsSlowerWindup()
        {
            var sim = new BattleSimulation(Plain, Plain);
            var ev = new List<BattleEvent>();
            sim.Submit(0, Swipe(SwipeKind.V));    // chiêu riêng vung 0,45s
            sim.Submit(1, Swipe(SwipeKind.Up));   // đòn mạnh vung 0,35s => trúng trước, cắt đòn kia
            Run(sim, 1f, ev);
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.Interrupted && e.Side == 0));
            Assert.AreEqual(sim.Fighters[1].Stats.Hp, sim.Fighters[1].Hp, 1e-3f, "đòn bị cắt thì không trúng");
        }

        [Test]
        public void KO_EndsBattle()
        {
            var sim = new BattleSimulation(new FighterStats(100, 1000, 0, 10, 0), Plain);
            var ev = new List<BattleEvent>();
            sim.Submit(0, Swipe(SwipeKind.Horizontal));
            Run(sim, 0.5f, ev);
            Assert.AreEqual(BattleOutcome.Win0, sim.Outcome);
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.KO));
            Assert.IsFalse(sim.Submit(1, Swipe(SwipeKind.Horizontal)), "hết trận thì không nhận vuốt nữa");
        }

        [Test]
        public void TimeUp_HigherHpPercentWins_OrDraw()
        {
            var cfg = new BattleConfig { DurationSeconds = 2f };
            var sim = new BattleSimulation(Plain, new FighterStats(500, 10, 0, 10, 0), cfg);
            sim.Submit(0, Swipe(SwipeKind.Horizontal));   // bên 1 mất 10/500 = 2% HP
            Run(sim, 3f);
            Assert.AreEqual(BattleOutcome.Win0, sim.Outcome, "so %HP, không so HP");
            var draw = new BattleSimulation(Plain, Plain, cfg);
            Run(draw, 3f);
            Assert.AreEqual(BattleOutcome.Draw, draw.Outcome);
        }

        [Test]
        public void FasterPet_MovesFirst()
        {
            var sim = new BattleSimulation(new FighterStats(100, 10, 0, 20, 0), Plain);
            var ev = new List<BattleEvent>();
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.Horizontal), ev), "SPD cao ra đòn ngay");
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.AttackStarted && e.Side == 0));
            Assert.IsTrue(sim.Submit(1, Swipe(SwipeKind.Horizontal), ev), "SPD thấp: nét được giữ lại");
            Assert.IsFalse(ev.Exists(e => e.Kind == BattleEventKind.AttackStarted && e.Side == 1), "nhưng phải chờ mới ra đòn");
            Run(sim, 0.5f, ev);
            BattleEvent late = ev.Find(e => e.Kind == BattleEventKind.AttackStarted && e.Side == 1);
            Assert.AreEqual(BattleEventKind.AttackStarted, late.Kind);
            Assert.AreEqual(new BattleConfig().FirstMoveDelaySeconds, late.Time, 0.04f);
        }

        private static readonly FighterStats Tough = new FighterStats(1000, 10, 0, 10, 0);

        [Test]
        public void Combo_ChainedHits_HitHarder_UpTo40Percent()
        {
            var sim = new BattleSimulation(Tough, Tough);
            var ev = new List<BattleEvent>();
            for (int i = 0; i < 6; i++)
            {
                sim.Submit(0, Swipe(SwipeKind.Horizontal));
                Run(sim, 0.7f, ev);
            }
            List<BattleEvent> hits = ev.FindAll(e => e.Kind == BattleEventKind.Hit);
            Assert.AreEqual(6, hits.Count);
            for (int i = 0; i < 6; i++)
            {
                Assert.AreEqual(i + 1, hits[i].Combo);
                Assert.AreEqual(10f * (1f + 0.1f * Math.Min(i, 4)), hits[i].Damage, 1e-3f, "đòn " + (i + 1));
            }
        }

        [Test]
        public void Combo_Breaks_WhenHit_TooSlow_OrBlocked()
        {
            var sim = new BattleSimulation(Tough, Tough);
            sim.Submit(0, Swipe(SwipeKind.Horizontal));
            Run(sim, 0.7f);
            sim.Submit(0, Swipe(SwipeKind.Horizontal));
            Run(sim, 0.3f);
            Assert.AreEqual(2, sim.Fighters[0].Combo);
            sim.Submit(1, Swipe(SwipeKind.Horizontal));
            Run(sim, 0.3f);
            Assert.AreEqual(0, sim.Fighters[0].Combo, "bị trúng đòn: mất combo");
            Assert.AreEqual(1, sim.Fighters[1].Combo);
            Run(sim, 1.5f);
            Assert.AreEqual(0, sim.Fighters[1].Combo, "quá 1,2 giây chưa trúng tiếp: mất combo");

            sim.Submit(1, Swipe(SwipeKind.Horizontal));
            Run(sim, 0.7f);
            Assert.AreEqual(1, sim.Fighters[1].Combo);
            sim.Submit(0, Swipe(SwipeKind.Down));
            sim.Submit(1, Swipe(SwipeKind.Up));   // trúng lúc đang đỡ (đỡ sớm => chặn)
            Run(sim, 0.5f);
            Assert.AreEqual(0, sim.Fighters[1].Combo, "bị đỡ: mất combo");
        }

        [Test]
        public void Rage_FillsFromFighting_UltimateNeedsFullRage()
        {
            var sim = new BattleSimulation(Tough, Tough);
            var ev = new List<BattleEvent>();
            Assert.IsFalse(sim.Submit(0, Swipe(SwipeKind.Z), ev), "chưa có Nộ: không ra được Tuyệt chiêu");
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.RageLow && e.Side == 0));
            Assert.AreEqual(0f, sim.Fighters[0].Open, "vuốt Z lúc chưa đầy Nộ không bị phạt");
            int hits = 0;
            for (; hits < 10 && !sim.UltimateReady(0); hits++)
            {
                sim.Submit(0, Swipe(SwipeKind.Horizontal));
                Run(sim, 0.7f, ev);
            }
            Assert.AreEqual((int)Math.Ceiling(sim.Config.RageMax / sim.Config.RageOnHit), hits, "12 Nộ mỗi đòn trúng => 9 đòn là đầy 100");
            Assert.AreEqual(1, ev.FindAll(e => e.Kind == BattleEventKind.RageFull && e.Side == 0).Count);
            Assert.AreEqual(hits * sim.Config.RageOnHurt, sim.Fighters[1].Rage, 1e-3f, "bị đánh cũng tích Nộ");
            ev.Clear();
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.Z), ev));
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.AttackStarted && e.Move == MoveKind.Ultimate));
            Assert.AreEqual(0f, sim.Fighters[0].Rage, "ra Tuyệt chiêu là dùng hết Nộ");
        }

        [Test]
        public void Ultimate_CannotBeInterrupted_ButCanBeParried()
        {
            var sim = new BattleSimulation(Tough, Tough);
            var ev = new List<BattleEvent>();
            sim.Fighters[0].Rage = sim.Config.RageMax;
            sim.Submit(0, Swipe(SwipeKind.Z));    // vung 0,7s
            sim.Submit(1, Swipe(SwipeKind.Up));   // đòn Mạnh trúng trước nhưng không cắt được Tuyệt chiêu
            Run(sim, 1f, ev);
            Assert.IsFalse(ev.Exists(e => e.Kind == BattleEventKind.Interrupted));
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.Hit && e.Side == 0 && e.Move == MoveKind.Ultimate));

            sim = new BattleSimulation(Tough, Tough);
            ev.Clear();
            sim.Fighters[0].Rage = sim.Config.RageMax;
            sim.Submit(0, Swipe(SwipeKind.Z));
            Run(sim, 0.6f);
            sim.Submit(1, Swipe(SwipeKind.Down));   // đỡ sát đòn
            Run(sim, 0.3f, ev);
            Assert.IsTrue(ev.Exists(e => e.Kind == BattleEventKind.Countered && e.Side == 1), "vẫn bị phản đòn được");
        }

        [Test]
        public void UltimateWithoutRage_WhenConfigSaysSo()
        {
            var sim = new BattleSimulation(Tough, Tough, new BattleConfig { UltimateNeedsRage = false });
            Assert.IsTrue(sim.Submit(0, Swipe(SwipeKind.Z)), "Huấn luyện: vuốt Z ra Tuyệt chiêu luôn");
            Run(sim, 1f);
            Assert.AreEqual(0f, sim.Fighters[0].Rage, "không tích Nộ");
            Assert.Less(sim.Fighters[1].Hp, sim.Fighters[1].Stats.Hp);
        }

        [Test]
        public void UnlearnedMove_IsWeaker()
        {
            var sim = new BattleSimulation(Plain, Plain, null, 1, new[] { true, false, true, true });
            Assert.AreEqual(10f * 1.6f * 0.8f, sim.BaseDamage(0, MoveKind.Heavy, 1f), 1e-3f);
            Assert.AreEqual(10f, sim.BaseDamage(0, MoveKind.Quick, 1f), 1e-3f);
        }

        [Test]
        public void SameSeed_SameBattle()
        {
            var a = new BattleSimulation(new FighterStats(300, 30, 20, 25, 10), new FighterStats(320, 28, 25, 22, 10), null, 42);
            var b = new BattleSimulation(new FighterStats(300, 30, 20, 25, 10), new FighterStats(320, 28, 25, 22, 10), null, 42);
            BattleBot.Simulate(a, new BattleBot(0, BotLevel.Normal, 7), new BattleBot(1, BotLevel.Hard, 8));
            BattleBot.Simulate(b, new BattleBot(0, BotLevel.Normal, 7), new BattleBot(1, BotLevel.Hard, 8));
            Assert.AreEqual(a.Outcome, b.Outcome);
            Assert.AreEqual(a.Fighters[0].Hp, b.Fighters[0].Hp);
            Assert.AreEqual(a.Time, b.Time);
        }
    }

    public class BattleBotTests
    {
        private static readonly FighterStats Even = new FighterStats(320, 32, 32, 32, 5);

        [Test]
        public void HardBot_BeatsEasyBot_MostOfTheTime()
        {
            int hardWins = 0;
            for (ulong seed = 1; seed <= 40; seed++)
            {
                var sim = new BattleSimulation(Even, Even, null, seed);
                BattleOutcome o = BattleBot.Simulate(sim, new BattleBot(0, BotLevel.Easy, seed * 3), new BattleBot(1, BotLevel.Hard, seed * 5));
                if (o == BattleOutcome.Win1) hardWins++;
            }
            Assert.GreaterOrEqual(hardWins, 30, "Bot Khó thắng Bot Dễ ít nhất 75% số trận");
        }

        [Test]
        public void BotBattle_AlwaysEndsWithin90Seconds()
        {
            var sim = new BattleSimulation(new FighterStats(5000, 5, 200, 10, 0), new FighterStats(5000, 5, 200, 10, 0), null, 3);
            BattleBot.Simulate(sim, new BattleBot(0, BotLevel.Normal, 1), new BattleBot(1, BotLevel.Normal, 2));
            Assert.IsTrue(sim.IsOver);
            Assert.LessOrEqual(sim.Time, 90.1f);
        }

        [Test]
        public void HardBot_ParriesSometimes()
        {
            var tank = new FighterStats(100000, 32, 32, 32, 5);   // 2 bên nhiều máu để trận kéo đủ 90 giây
            var sim = new BattleSimulation(tank, tank, null, 9);
            var defendOnly = new BotConfig
            {
                AttackGapMin = new[] { 999f, 999f, 999f },   // chỉ đỡ, không đánh: để đo riêng khả năng canh phản đòn
                AttackGapMax = new[] { 999f, 999f, 999f },
            };
            var bot = new BattleBot(1, BotLevel.Hard, 11, defendOnly);
            var ev = new List<BattleEvent>();
            for (int i = 0; i < 90 * 30 && !sim.IsOver; i++)
            {
                if (i % 60 == 0) sim.Submit(0, new SwipeResult { Kind = SwipeKind.Up, Quality = 1f });   // đánh Mạnh mỗi 2 giây (45 đòn)
                SwipeResult? s = bot.Think(sim);
                if (s.HasValue) sim.Submit(1, s.Value);
                sim.Tick(ev);
            }
            Assert.Greater(ev.FindAll(e => e.Kind == BattleEventKind.Countered && e.Side == 1).Count, 22, "Bot Khó canh phản đòn được quá nửa số đòn (đỡ 70%)");
        }
    }
}
