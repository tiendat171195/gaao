// Đo cân bằng Săn quái (Phase 9.2): bot chơi từng màn ở 3 mức tay nghề (vụng 0,3 / khá 0,5 / giỏi 0,8).
// Mỗi ô: % thắng / % máu còn lại khi thắng / số giây trung bình 1 lần săn.
//   cd Tools/HuntBalance && dotnet run -c Release -- [số lần mỗi màn, mặc định 40] [bé lớn hơn / nhỏ hơn giai đoạn vùng: 1 / -1]
using System;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

static class Program
{
    static void Main(string[] args)
    {
        var cfg = GameConfig.CreateDefault();
        var adv = new AdventureConfig();
        float[] skills = { 0.3f, 0.5f, 0.8f };
        int runs = args.Length > 0 ? int.Parse(args[0]) : 40;
        int petShift = args.Length > 1 ? int.Parse(args[1]) : 0;   // bé lớn hơn / nhỏ hơn giai đoạn vùng
        for (int r = 0; r < 5; r++)
        {
            int ps = Math.Max(0, Math.Min(4, r + petShift));
            PetStats pet = StatsRules.Compute((EvolutionStage)ps, ps >= 3 ? AdultForm.Fire : AdultForm.None, 1f, cfg.Stats);
            Console.Write("vùng " + (r + 1) + " (bé " + (EvolutionStage)ps + "):");
            for (int n = 1; n <= 5; n++)
            {
                PetStats m = StatsRules.Compute((EvolutionStage)r, AdultForm.None, 1f, cfg.Stats);
                float f = adv.StageFactors[n - 1];
                m.Hp *= f; m.Atk *= f; m.Def *= f; m.Spd *= f;
                HuntPlan plan = HuntPlan.Create(r, n, n == 5, m);
                Console.Write("  " + (r + 1) + "-" + n + ":");
                foreach (float sk in skills)
                {
                    int wins = 0; float hp = 0f, time = 0f; int stars3 = 0;
                    for (int i = 0; i < runs; i++)
                    {
                        var sim = new HuntSimulation(plan, HunterStats.From(pet), (ulong)(1000 + i * 7919 + n * 31 + r * 101));
                        var bot = new HuntBot(sk, (ulong)(77 + i));
                        HuntOutcome o = HuntBot.Play(sim, bot, out float left);
                        time += sim.Time;
                        if (o == HuntOutcome.Won) { wins++; hp += left; if (left >= 0.7f) stars3++; }
                    }
                    Console.Write(" " + (100 * wins / runs).ToString().PadLeft(3) + "%/" + (wins > 0 ? (hp / wins * 100).ToString("0") : "-").PadLeft(2) + "hp/" + (time / runs).ToString("0") + "s");
                }
            }
            Console.WriteLine();
        }
    }
}
