using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Battle
{
    /// <summary>
    /// 1 lần đi Săn quái (Thám hiểm, Phase 9.2, C# thuần): chạy HuntSimulation theo bước cố định, nhận lệnh từ View,
    /// hết trận thì: cộng điểm tập (hạ quái = Sức mạnh, đỡ đẹp / né = Tốc độ), trừ năng lượng + no như 1 trận đấu,
    /// tính sao + Coin qua AdventureService, báo BattleEnded (nhiệm vụ ngày).
    /// </summary>
    public sealed class HuntSession
    {
        private readonly PetService _pets;
        private readonly EvolutionService _evo;
        private readonly AdventureService _adv;
        private readonly BattleSessionConfig _costs;
        private readonly List<HuntEvent> _out = new List<HuntEvent>();
        private float _acc;
        private bool _quit;

        public HuntSession(PetService pets, EvolutionService evo, AdventureService adv, int stage, ulong seed,
                           HuntConfig cfg = null, BattleSessionConfig costs = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _adv = adv ?? throw new ArgumentNullException(nameof(adv));
            _costs = costs ?? new BattleSessionConfig();
            Stage = stage;
            Plan = adv.PlanFor(stage);
            Sim = new HuntSimulation(Plan, HunterStats.From(evo.Stats), seed, cfg);
        }

        public int Stage { get; }
        public HuntPlan Plan { get; }
        public HuntSimulation Sim { get; }

        /// <summary>Có thì hết trận phát <see cref="BattleEnded"/> (nhiệm vụ ngày "thắng N trận").</summary>
        public EventBus Events { get; set; }

        public bool Finished { get; private set; }
        public bool Won => Sim.Outcome == HuntOutcome.Won && !_quit;

        /// <summary>Sao + Coin (có sau khi hết trận).</summary>
        public AdventureResult Result { get; private set; }

        public float StrengthGained { get; private set; }
        public float SpeedGained { get; private set; }

        /// <summary>Chạy mô phỏng theo thời gian thật (View gọi mỗi frame). Việc xảy ra gom vào hàng chờ, lấy bằng <see cref="TakeEvents"/>.</summary>
        public void Update(float dt)
        {
            if (Finished) return;
            float tick = Sim.Config.TickSeconds;
            _acc = Math.Min(_acc + Math.Max(0f, dt), 0.25f);   // máy giật 1 lúc thì chạy dồn tối đa 0,25 giây
            while (_acc >= tick)
            {
                _acc -= tick;
                Sim.Step();
                _out.AddRange(Sim.Events);
                if (Sim.Outcome != HuntOutcome.None)
                {
                    Finish();
                    return;
                }
            }
        }

        /// <summary>Lấy các việc đã xảy ra từ lần lấy trước (rồi xóa hàng chờ).</summary>
        public void TakeEvents(List<HuntEvent> into)
        {
            into.AddRange(_out);
            _out.Clear();
        }

        public void Tap(int monsterId = -1)
        {
            if (!Finished) Sim.Tap(monsterId);
        }

        public void Slam()
        {
            if (!Finished) Sim.Slam();
        }

        public void Guard()
        {
            if (!Finished) Sim.Guard();
        }

        public void Dash(int dir)
        {
            if (!Finished) Sim.Dash(dir);
        }

        public void Ultimate()
        {
            if (!Finished) Sim.Ultimate();
        }

        /// <summary>Bỏ cuộc = thua (vẫn tốn sức như đã đi săn).</summary>
        public void Quit()
        {
            if (Finished) return;
            _quit = true;
            Finish();
        }

        private void Finish()
        {
            if (Finished) return;
            Finished = true;
            bool won = Won;
            HuntConfig c = Sim.Config;
            StrengthGained = _evo.AddTraining(TrainingAxis.Strength, Math.Min(c.MaxStrengthPoints, Sim.Kills * c.KillStrength));
            SpeedGained = _evo.AddTraining(TrainingAxis.Speed, Math.Min(c.MaxSpeedPoints, (Sim.PerfectGuards + Sim.Dodges) * c.DodgeSpeed));
            if (_pets.HasPet)
            {
                _pets.Spend(NeedType.Energy, _costs.FightEnergyCost);
                _pets.Spend(NeedType.Satiety, _costs.FightSatietyCost);
            }
            Result = _adv.Complete(Stage, won, won ? Sim.Hunter.Hp01 : 0f);
            Events?.Publish(new BattleEnded((int)BattleMode.Adventure, won));
        }
    }

    /// <summary>
    /// Người chơi máy cho Săn quái (C# thuần): dùng trong test + đo cân bằng. Tay nghề 0..1: phản xạ đỡ / né khi quái lấy đà,
    /// canh đỡ sát lúc (phản đòn), nhịp chạm nhanh hay chậm, biết đập đất khi bị vây, xả Nộ.
    /// </summary>
    public sealed class HuntBot
    {
        private readonly float _skill;
        private readonly IRandom _rng;
        private readonly Dictionary<int, float> _windupLead = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _chargeLead = new Dictionary<int, float>();
        private readonly HashSet<int> _shotSeen = new HashSet<int>();
        private float _tapT;

        public HuntBot(float skill, ulong seed)
        {
            _skill = Math.Max(0f, Math.Min(1f, skill));
            _rng = new SeededRandom(seed, 77UL);
        }

        public void Act(HuntSimulation sim, float dt)
        {
            Hunter h = sim.Hunter;
            if (h.Action == HunterAction.Dead || sim.Outcome != HuntOutcome.None) return;
            HuntConfig c = sim.Config;
            _tapT -= dt;
            // 1a) heo đang lao tới: đợi nó tới sát rồi lướt XUYÊN qua (không bị trúng lúc lướt) hoặc đỡ sát lúc để phản đòn
            foreach (HuntMonster m in sim.Monsters)
            {
                bool charging = m.Phase == MonsterPhase.Act && m.Attack != null && m.Attack.Move == MonsterMove.Charge && !m.HitThisAttack &&
                                Math.Sign(h.X - m.X) == m.ChargeDir;
                if (!charging)
                {
                    _chargeLead.Remove(m.Id);
                    continue;
                }
                if (!_chargeLead.TryGetValue(m.Id, out float cl))
                {
                    cl = _rng.NextDouble() < 0.35 + 0.6 * _skill ? 0.04f + (float)_rng.NextDouble() * 0.25f * (1f - _skill) : -1f;
                    _chargeLead[m.Id] = cl;
                }
                if (cl < 0f) continue;
                float arrive = (Math.Abs(h.X - m.X) - (c.HunterSize + m.Def.Size)) / Math.Max(0.1f, m.Attack.ChargeSpeed);
                if (arrive > cl) continue;
                _chargeLead[m.Id] = -1f;
                if (_skill >= 0.5f && h.DashCd <= 0f && _rng.NextDouble() < 0.6) sim.Dash(m.X >= h.X ? 1 : -1);   // lướt xuyên qua heo
                else sim.Guard();
                return;
            }
            // 1b) quái sắp đánh (cắn / đập đất): đỡ (hoặc lướt né đập đất) — tay nghề quyết định có kịp không + canh có sát không
            foreach (HuntMonster m in sim.Monsters)
            {
                if (m.Phase != MonsterPhase.Windup)
                {
                    _windupLead.Remove(m.Id);
                    continue;
                }
                if (!_windupLead.TryGetValue(m.Id, out float lead))
                {
                    bool notice = _rng.NextDouble() < 0.35 + 0.6 * _skill;
                    lead = notice ? 0.05f + (float)_rng.NextDouble() * 0.45f * (1f - _skill) : -1f;
                    _windupLead[m.Id] = lead;
                }
                if (lead < 0f || m.Attack == null) continue;
                float wu = m.Attack.Windup * (m.Enraged ? c.EnrageWindup : 1f);
                float left = m.WindupLeft01 * wu;
                bool reaches = m.Attack.Move == MonsterMove.Charge || m.Attack.Move == MonsterMove.Ranged ||
                               Math.Abs(m.X - h.X) - (c.HunterSize + m.Def.Size) <= (m.Attack.Move == MonsterMove.Smash ? m.Attack.Radius : m.Attack.Range + 0.35f);
                if (!reaches || m.Attack.Move == MonsterMove.Ranged || m.Attack.Move == MonsterMove.Charge || left > lead) continue;
                _windupLead[m.Id] = -1f;   // đã phản ứng cho đòn này
                bool dodge = m.Attack.Move == MonsterMove.Smash && _skill >= 0.5f && h.DashCd <= 0f && _rng.NextDouble() < 0.5;
                if (dodge) sim.Dash(m.X >= h.X ? -1 : 1);
                else sim.Guard();
                return;
            }
            foreach (HuntProjectile s in sim.Projectiles)   // đạn bay tới: đỡ
            {
                if (_shotSeen.Contains(s.Id) || Math.Sign(h.X - s.X) != s.Dir) continue;
                if (Math.Abs(h.X - s.X) > 0.6f + s.Speed * (0.05f + 0.2f * (1f - _skill))) continue;
                _shotSeen.Add(s.Id);
                if (_rng.NextDouble() < 0.3 + 0.65 * _skill)
                {
                    sim.Guard();
                    return;
                }
            }
            // 2) đầy Nộ + đông quái: tuyệt chiêu
            if (sim.RageFull && sim.AliveCount >= 2)
            {
                sim.Ultimate();
                return;
            }
            // 3) bị vây (≥ 2 quái sát bên): đập đất
            if (h.SlamCd <= 0f)
            {
                int near = 0;
                foreach (HuntMonster m in sim.Monsters)
                    if (m.IsAlive && Math.Abs(m.X - h.X) <= c.SlamRadius) near++;
                if (near >= 2 && _rng.NextDouble() < 0.4 + 0.5 * _skill)
                {
                    sim.Slam();
                    return;
                }
            }
            // 4) chạm đánh con gần nhất theo nhịp tay
            if (_tapT <= 0f && sim.AliveCount > 0)
            {
                sim.Tap();
                _tapT = (0.4f - 0.22f * _skill) * (0.8f + (float)_rng.NextDouble() * 0.4f);
            }
        }

        /// <summary>Đánh trọn 1 màn bằng bot (test / đo cân bằng): trả kết quả + %HP còn lại.</summary>
        public static HuntOutcome Play(HuntSimulation sim, HuntBot bot, out float hpLeft01)
        {
            float dt = sim.Config.TickSeconds;
            while (sim.Outcome == HuntOutcome.None)
            {
                bot.Act(sim, dt);
                sim.Step();
            }
            hpLeft01 = sim.Hunter.Hp01;
            return sim.Outcome;
        }
    }
}
