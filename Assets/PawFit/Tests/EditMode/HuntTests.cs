using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Săn quái — Thám hiểm kiểu mới, khác trận 1-1 PvP (Phase 9.2).</summary>
    public class HuntTests
    {
        private static readonly HunterStats Baby = new HunterStats { Hp = 100f, Atk = 10f, Def = 10f, Spd = 10f, Crit = 5f };

        /// <summary>Màn tự đặt: 1 đợt gồm các quái cho trước (bên phải), chỉ số chuẩn bằng thú Baby.</summary>
        private static HuntPlan Plan(params MonsterKind[] kinds)
        {
            var p = new HuntPlan { RefHp = 100f, RefAtk = 10f, RefDef = 10f, RefSpd = 10f, Title = "test" };
            var w = new HuntWave();
            foreach (MonsterKind k in kinds) w.Spawns.Add(new HuntSpawn { Kind = k, Side = 1, Delay = 0.3f });
            p.Waves.Add(w);
            return p;
        }

        private static HuntSimulation Sim(HuntPlan plan, ulong seed = 7) => new HuntSimulation(plan, Baby, seed);

        private static List<HuntEvent> Run(HuntSimulation s, Func<HuntSimulation, bool> until, int maxSteps = 6000)
        {
            var all = new List<HuntEvent>();
            for (int i = 0; i < maxSteps && s.Outcome == HuntOutcome.None; i++)
            {
                s.Step();
                all.AddRange(s.Events);
                if (until(s)) break;
            }
            return all;
        }

        private static HuntMonster First(HuntSimulation s) => s.Monsters.Count > 0 ? s.Monsters[0] : null;

        /// <summary>Chạy tới lúc quái đầu tiên sắp ra đòn (còn ≤ `left01` thời gian lấy đà).</summary>
        private static HuntMonster UntilWindup(HuntSimulation s, float left01)
        {
            Run(s, x => First(x) != null && First(x).Phase == MonsterPhase.Windup && First(x).WindupLeft01 <= left01);
            HuntMonster m = First(s);
            Assert.IsNotNull(m);
            Assert.AreEqual(MonsterPhase.Windup, m.Phase, "quái lấy đà trước khi đánh (để kịp đỡ)");
            return m;
        }

        [Test]
        public void SameSeed_SameInputs_SameResult()
        {
            HuntPlan plan = HuntPlan.Create(1, 3, false, StatsRules.Compute(EvolutionStage.Child, AdultForm.None, 1f, new StatsConfig()));
            var stats = HunterStats.From(StatsRules.Compute(EvolutionStage.Child, AdultForm.None, 1f, new StatsConfig()));
            HuntOutcome a = HuntBot.Play(new HuntSimulation(plan, stats, 99), new HuntBot(0.5f, 3), out float hpA);
            HuntOutcome b = HuntBot.Play(new HuntSimulation(plan, stats, 99), new HuntBot(0.5f, 3), out float hpB);
            Assert.AreEqual(a, b);
            Assert.AreEqual(hpA, hpB, 1e-6f, "bước cố định + seed: chạy lại y hệt");
        }

        [Test]
        public void Tap_DashesToNearest_HitsBuildComboAndRage_KillsThenWins()
        {
            HuntSimulation s = Sim(Plan(MonsterKind.Slime));
            Run(s, x => First(x) != null && Math.Abs(First(x).X) < 5f);
            HuntMonster m = First(s);
            float x0 = s.Hunter.X, hp0 = m.Hp;
            s.Tap();
            List<HuntEvent> ev = Run(s, x => x.Hunter.Action == HunterAction.Strike && x.Hunter.ActionT > 0.15f);
            Assert.Greater(s.Hunter.X, x0 + 1f, "chạm = bé lao tới quái");
            Assert.Less(m.Hp, hp0, "đánh trúng");
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.Hit && e.Monster == m.Id));
            Assert.Greater(s.Hunter.Rage, 0f, "đánh trúng thì lên Nộ");
            for (int i = 0; i < 40 && s.Outcome == HuntOutcome.None; i++)
            {
                s.Tap();
                Run(s, x => false, 12);
            }
            Assert.AreEqual(HuntOutcome.Won, s.Outcome, "hạ hết quái đợt cuối = thắng");
            Assert.AreEqual(1, s.Kills);
        }

        [Test]
        public void Guard_PerfectBlocksAndCounters_LateGuardReduces_NoGuardFull()
        {
            // đỡ sát lúc: không mất máu + phản đòn làm quái choáng
            HuntSimulation s = Sim(Plan(MonsterKind.Slime));
            HuntMonster m = UntilWindup(s, 0.15f);
            float hp = s.Hunter.Hp;
            s.Guard();
            List<HuntEvent> ev = Run(s, x => m.Phase != MonsterPhase.Windup);
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.PerfectGuard), "đỡ sát lúc = phản đòn");
            Assert.AreEqual(hp, s.Hunter.Hp, 1e-4f, "không mất máu");
            Assert.AreEqual(MonsterPhase.Stunned, m.Phase, "quái bị phản đòn choáng");
            Assert.AreEqual(1, s.PerfectGuards);

            // không đỡ: mất máu đủ, bé khựng
            HuntSimulation n = Sim(Plan(MonsterKind.Slime));
            UntilWindup(n, 0.15f);
            float before = n.Hunter.Hp;
            List<HuntEvent> ev2 = Run(n, x => x.Hunter.Hp < before);
            float full = before - n.Hunter.Hp;
            Assert.Greater(full, 0f);
            Assert.IsTrue(ev2.Exists(e => e.Kind == HuntEventKind.PetHurt));

            // đỡ sớm (quá cửa sổ đỡ sát): vẫn đỡ được nhưng mất 1 phần máu
            HuntSimulation g = Sim(Plan(MonsterKind.Slime));
            UntilWindup(g, 0.65f);
            float b2 = g.Hunter.Hp;
            g.Guard();
            List<HuntEvent> ev3 = Run(g, x => x.Hunter.Hp < b2);
            Assert.IsTrue(ev3.Exists(e => e.Kind == HuntEventKind.Guarded), "đỡ thường");
            Assert.Less(b2 - g.Hunter.Hp, full * 0.5f, "đỡ thường chỉ mất ít máu");
        }

        [Test]
        public void Dash_ThroughChargingBoar_Dodges_BoarGetsDizzy()
        {
            HuntSimulation s = Sim(Plan(MonsterKind.Boar));
            HuntMonster boar = UntilWindup(s, 0.5f);
            Assert.AreEqual(MonsterMove.Charge, boar.Attack.Move, "heo rừng lấy đà rồi lao húc");
            // đợi heo lao tới sát rồi lướt xuyên qua nó
            Run(s, x => boar.Phase == MonsterPhase.Act && Math.Abs(boar.X - x.Hunter.X) < 1.8f);
            float hp = s.Hunter.Hp, boarHp = boar.Hp;
            s.Dash(boar.X >= s.Hunter.X ? 1 : -1);
            List<HuntEvent> ev = Run(s, x => boar.Phase == MonsterPhase.Recover || x.Hunter.Hp < hp);
            Assert.AreEqual(hp, s.Hunter.Hp, 1e-4f, "lướt xuyên qua cú húc: không mất máu");
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.Dodged), "né được");
            Assert.Less(boar.Hp, boarHp, "lướt xuyên quái thì gây sát thương");
            Assert.IsTrue(boar.Dizzy, "húc hụt xong heo choáng: lúc này đánh dễ");

            // đứng yên thì bị húc trúng (đau)
            HuntSimulation still = Sim(Plan(MonsterKind.Boar));
            HuntMonster b2 = UntilWindup(still, 0.5f);
            float hp2 = still.Hunter.Hp;
            Run(still, x => b2.Phase == MonsterPhase.Recover);
            Assert.Less(still.Hunter.Hp, hp2 - 10f, "không né / đỡ thì bị húc trúng, mất nhiều máu");
        }

        [Test]
        public void Slam_HitsTheWholeGroup_KnocksBack_Stuns()
        {
            HuntSimulation s = Sim(Plan(MonsterKind.Slime, MonsterKind.Slime, MonsterKind.Slime));
            Run(s, x => x.Monsters.Count == 3 && Math.Abs(x.Monsters[2].X) < 2.2f, 8000);
            var hp = new List<float>();
            foreach (HuntMonster m in s.Monsters) hp.Add(m.Hp);
            s.Slam();
            List<HuntEvent> ev = Run(s, x => x.Hunter.Action == HunterAction.Idle && x.Hunter.SlamCd > 0f);
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.Slam));
            int hit = 0;
            for (int i = 0; i < s.Monsters.Count; i++)
                if (s.Monsters[i].Hp < hp[i]) hit++;
            Assert.GreaterOrEqual(hit, 2, "đập đất trúng nhiều con 1 lúc");
            Assert.Greater(s.Hunter.SlamCd, 0f, "đập đất có hồi chiêu");
        }

        [Test]
        public void Ultimate_NeedsFullRage_HitsAll()
        {
            HuntSimulation s = Sim(Plan(MonsterKind.Golem, MonsterKind.Golem));
            Run(s, x => x.Monsters.Count == 2 && Math.Abs(x.Monsters[1].X) < 6f, 8000);
            s.Ultimate();
            List<HuntEvent> none = Run(s, x => false, 30);
            Assert.IsFalse(none.Exists(e => e.Kind == HuntEventKind.Ultimate), "chưa đầy Nộ thì không ra tuyệt chiêu");
            s.Hunter.Rage = s.Config.RageMax;
            s.Ultimate();
            List<HuntEvent> ev = Run(s, x => x.Hunter.Action == HunterAction.Idle && x.Hunter.Rage < 1f, 120);
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.Ultimate));
            Assert.AreEqual(2, ev.FindAll(e => e.Kind == HuntEventKind.Hit).Count, "trúng mọi quái");
        }

        [Test]
        public void Mushroom_ShootsSpores_FromRange()
        {
            HuntSimulation s = Sim(Plan(MonsterKind.Mushroom));
            float hp = s.Hunter.Hp;
            List<HuntEvent> ev = Run(s, x => x.Hunter.Hp < hp, 3000);
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.Shot), "nấm độc phun bào tử");
            Assert.Greater(Math.Abs(First(s).X - s.Hunter.X), 2f, "bắn từ xa, phải lao tới đánh");
        }

        [Test]
        public void Boss_SummonsAndEnrages()
        {
            var p = new HuntPlan { RefHp = 100f, RefAtk = 10f, RefDef = 10f, RefSpd = 10f, Boss = true };
            var w = new HuntWave();
            w.Spawns.Add(new HuntSpawn { Kind = MonsterKind.KingSlime, Side = 1 });
            p.Waves.Add(w);
            HuntSimulation s = Sim(p);
            List<HuntEvent> ev = Run(s, x => x.Monsters.Count > 1, 3000);
            Assert.IsTrue(ev.Exists(e => e.Kind == HuntEventKind.Summon), "Vua Chất Nhờn gọi thêm chất nhờn");
            HuntMonster boss = s.Monsters[0];
            Assert.IsTrue(boss.Def.Boss);
            s.Hunter.Hp = s.Hunter.MaxHp = 1e6f;   // chỉ xem trùm nổi giận
            for (int i = 0; i < 400 && !boss.Enraged && boss.IsAlive; i++)
            {
                s.Tap(boss.Id);
                Run(s, x => false, 10);
            }
            Assert.IsTrue(boss.Enraged, "còn nửa máu thì nổi giận");
        }

        [Test]
        public void Waves_ComeOneAfterAnother_ToTheEnd()
        {
            HuntPlan plan = HuntPlan.Create(0, 1, false, StatsRules.Compute(EvolutionStage.Baby, AdultForm.None, 1f, new StatsConfig()));
            var s = new HuntSimulation(plan, Baby, 5);
            var bot = new HuntBot(0.7f, 1);
            var waves = new List<int>();
            int spawned = 0;
            while (s.Outcome == HuntOutcome.None)
            {
                bot.Act(s, s.Config.TickSeconds);
                s.Step();
                foreach (HuntEvent e in s.Events)
                {
                    if (e.Kind == HuntEventKind.WaveStart) waves.Add(e.Value);
                    if (e.Kind == HuntEventKind.Spawn) spawned++;
                }
            }
            Assert.AreEqual(HuntOutcome.Won, s.Outcome);
            CollectionAssert.AreEqual(new[] { 0, 1, 2 }, waves, "3 đợt lần lượt");
            Assert.AreEqual(plan.TotalMonsters, spawned);
            Assert.AreEqual(plan.TotalMonsters, s.Kills);
        }

        [Test]
        public void Balance_GrowingUpOpensTheMap()
        {
            var cfg = new StatsConfig();
            var adv = new AdventureConfig();
            PetStats Ref(EvolutionStage st, float f)
            {
                PetStats p = StatsRules.Compute(st, AdultForm.None, 1f, cfg);
                p.Hp *= f;
                p.Atk *= f;
                p.Def *= f;
                p.Spd *= f;
                return p;
            }
            int Wins(int region, int number, EvolutionStage pet, float skill, int runs)
            {
                HuntPlan plan = HuntPlan.Create(region, number, number == 5, Ref((EvolutionStage)region, adv.StageFactors[number - 1]));
                var hs = HunterStats.From(StatsRules.Compute(pet, AdultForm.None, 1f, cfg));
                int w = 0;
                for (int i = 0; i < runs; i++)
                    if (HuntBot.Play(new HuntSimulation(plan, hs, (ulong)(100 + i)), new HuntBot(skill, (ulong)i), out _) == HuntOutcome.Won) w++;
                return w;
            }
            Assert.AreEqual(8, Wins(0, 1, EvolutionStage.Baby, 0.4f, 8), "bé mới nở đánh vụng vẫn qua 1-1");
            Assert.GreaterOrEqual(Wins(1, 5, EvolutionStage.Child, 0.5f, 10), 6, "đúng giai đoạn, đánh khá: hạ được trùm");
            Assert.LessOrEqual(Wins(3, 5, EvolutionStage.Teen, 0.5f, 8), 1, "nhỏ hơn 1 giai đoạn: gần như không qua trùm");
            Assert.AreEqual(8, Wins(2, 5, EvolutionStage.Adult, 0.5f, 8), "lớn rồi quay lại vùng cũ: dễ");
        }

        [Test]
        public void NextAfter_OnlyWhenUnlocked()
        {
            var time = new ManualTimeProvider(GardenFixture.T0);
            var c = GameConfig.CreateDefault();
            var pets = new PetService(time, new SeededRandom(3), new InMemorySaveService(new NewtonsoftSerializer()), new EventBus(), c);
            pets.LoadOrCreate("Kem");
            var health = new HealthService(pets, time, c);
            var adv = new AdventureService(pets, new EvolutionService(pets, health, time, new EventBus(), c), health);
            Assert.AreEqual(-1, adv.NextAfter(0), "màn 1-2 chưa mở");
            adv.Complete(0, true, 0.8f);
            Assert.AreEqual(1, adv.NextAfter(0), "thắng 1-1 thì có nút Đánh tiếp 1-2");
            Assert.AreEqual(-1, adv.NextAfter(24), "màn cuối không có màn tiếp");
        }

        [Test]
        public void Session_Quit_IsALoss_CostsEnergy_ReportsBattleEnded()
        {
            var time = new ManualTimeProvider(GardenFixture.T0);
            var c = GameConfig.CreateDefault();
            var bus = new EventBus();
            var pets = new PetService(time, new SeededRandom(3), new InMemorySaveService(new NewtonsoftSerializer()), bus, c);
            pets.LoadOrCreate("Kem");
            var health = new HealthService(pets, time, c);
            var evo = new EvolutionService(pets, health, time, bus, c);
            var adv = new AdventureService(pets, evo, health, null, bus);
            var ended = new List<BattleEnded>();
            bus.Subscribe<BattleEnded>(e => ended.Add(e));
            float energy = pets.Current.Needs.Energy;
            HuntSession s = adv.StartHunt(0, 1);
            s.Events = bus;
            s.Update(2f);
            s.Quit();
            Assert.IsTrue(s.Finished);
            Assert.IsFalse(s.Won);
            Assert.AreEqual(0, s.Result.Stars);
            Assert.Less(pets.Current.Needs.Energy, energy, "đi săn tốn sức");
            Assert.AreEqual(1, ended.Count);
            Assert.IsFalse(ended[0].Won);
        }
    }
}

namespace PawFit.Tests
{
    /// <summary>Cử chỉ Săn quái: chạm / vuốt 4 hướng, nhận ngay khi vuốt đủ dài (Phase 9.3).</summary>
    public class HuntGestureTests
    {
        private static readonly System.Numerics.Vector2 C = new System.Numerics.Vector2(500f, 500f);

        private static System.Numerics.Vector2 P(float dx, float dy) => C + new System.Numerics.Vector2(dx, dy);

        [Test]
        public void QuickTap_IsTap_LongPress_IsNothing()
        {
            var g = new HuntGestures();
            g.Down(C, 0);
            Assert.AreEqual(HuntGestureKind.Tap, g.Up(P(5f, -4f), 0.12));
            g.Down(C, 1);
            Assert.AreEqual(HuntGestureKind.None, g.Up(P(3f, 0f), 2.0), "giữ lâu không phải chạm");
        }

        [Test]
        public void Flick_IsRecognized_BeforeRelease_OnlyOnce()
        {
            var g = new HuntGestures();
            g.Down(C, 0);
            Assert.AreEqual(HuntGestureKind.None, g.Move(P(0f, -20f), 0.03), "chưa đủ dài");
            Assert.AreEqual(HuntGestureKind.Down, g.Move(P(4f, -70f), 0.08), "vuốt xuống = đỡ, nhận ngay");
            Assert.AreEqual(HuntGestureKind.None, g.Move(P(4f, -160f), 0.12), "1 lần chạm 1 cử chỉ");
            Assert.AreEqual(HuntGestureKind.None, g.Up(P(4f, -170f), 0.15));
            g.Down(C, 1);
            Assert.AreEqual(HuntGestureKind.Up, g.Move(P(-5f, 80f), 1.1));
            g.Up(P(-5f, 80f), 1.2);
            g.Down(C, 2);
            Assert.AreEqual(HuntGestureKind.Left, g.Move(P(-90f, 10f), 2.1));
            g.Up(P(-90f, 10f), 2.2);
            g.Down(C, 3);
            Assert.AreEqual(HuntGestureKind.Right, g.Move(P(90f, -10f), 3.1));
        }

        [Test]
        public void SlowSwipe_OnRelease_DiagonalIsNothing()
        {
            var g = new HuntGestures();
            g.Down(C, 0);
            Assert.AreEqual(HuntGestureKind.None, g.Move(P(0f, 90f), 0.6), "chậm quá: chưa nhận lúc kéo");
            Assert.AreEqual(HuntGestureKind.Up, g.Up(P(0f, 100f), 0.8), "nhấc tay: vẫn nhận vuốt chậm");
            g.Down(C, 1);
            Assert.AreEqual(HuntGestureKind.None, g.Up(P(90f, 85f), 1.3), "vuốt chéo không rõ hướng");
        }

        [Test]
        public void Thresholds_ScaleWithScreenDensity()
        {
            var g = new HuntGestures(null, 3f);   // máy 480 dpi: ngón tay dịch nhiều pixel hơn
            g.Down(C, 0);
            Assert.AreEqual(HuntGestureKind.None, g.Move(P(0f, -70f), 0.05), "70 px trên máy dày điểm ảnh chưa là vuốt");
            Assert.AreEqual(HuntGestureKind.Down, g.Move(P(0f, -180f), 0.1));
        }
    }
}
