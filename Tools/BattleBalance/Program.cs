using System;
using System.Collections.Generic;
using PawFit.Battle;

namespace PawFit.Tools
{
    /// <summary>
    /// Đo nhịp trận bằng bot đấu bot (cùng luật BattleSimulation trong game + máy chủ).
    /// Dùng khi chỉnh số trong BattleConfig / BotConfig: trận ngang sức nên khoảng 25–30 giây, mỗi bên 2–3 Tuyệt chiêu.
    /// </summary>
    public static class Program
    {
        private static void Run(string label, BotLevel a, BotLevel b, FighterStats sa, FighterStats sb, int battles)
        {
            int winsB = 0, ko = 0, ults = 0, hits = 0, maxCombo = 0;
            double time = 0;
            for (ulong seed = 1; seed <= (ulong)battles; seed++)
            {
                var sim = new BattleSimulation(sa, sb, new BattleConfig(), seed);
                var ba = new BattleBot(0, a, seed * 3);
                var bb = new BattleBot(1, b, seed * 5);
                var ev = new List<BattleEvent>();
                while (!sim.IsOver)
                {
                    SwipeResult? x = ba.Think(sim);
                    if (x.HasValue) sim.Submit(0, x.Value, ev);
                    SwipeResult? y = bb.Think(sim);
                    if (y.HasValue) sim.Submit(1, y.Value, ev);
                    sim.Tick(ev);
                }
                if (sim.Outcome == BattleOutcome.Win1) winsB++;
                if (sim.Fighters[0].Hp <= 0f || sim.Fighters[1].Hp <= 0f) ko++;
                time += sim.Time;
                foreach (BattleEvent e in ev)
                {
                    if (e.Kind == BattleEventKind.AttackStarted && e.Move == MoveKind.Ultimate) ults++;
                    if (e.Kind != BattleEventKind.Hit) continue;
                    hits++;
                    maxCombo = Math.Max(maxCombo, e.Combo);
                }
            }
            double n = battles;
            Console.WriteLine($"{label,-22} {a,-6} vs {b,-6}  {time / n,5:0.0} s  KO {100 * ko / n,3:0}%  bên phải thắng {100 * winsB / n,3:0}%  " +
                              $"đòn trúng {hits / n,4:0.0}  Tuyệt chiêu {ults / n,3:0.0}  combo dài nhất {maxCombo}");
        }

        public static void Main(string[] args)
        {
            int battles = args.Length > 0 && int.TryParse(args[0], out int k) ? k : 400;
            var even = new FighterStats(320, 32, 32, 32, 5);
            var adult = new FighterStats(900, 60, 55, 50, 8);
            Console.WriteLine($"Mỗi cặp {battles} trận · máu trong trận ×{new BattleConfig().HpScale}");
            Run("Ngang sức (Child)", BotLevel.Normal, BotLevel.Normal, even, even, battles);
            Run("Ngang sức (Child)", BotLevel.Easy, BotLevel.Normal, even, even, battles);
            Run("Ngang sức (Child)", BotLevel.Easy, BotLevel.Hard, even, even, battles);
            Run("Ngang sức (Adult)", BotLevel.Normal, BotLevel.Hard, adult, adult, battles);
        }
    }
}
