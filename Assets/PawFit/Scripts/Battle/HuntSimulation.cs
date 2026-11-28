using System;
using System.Collections.Generic;
using PawFit.Core;

namespace PawFit.Battle
{
    public enum HuntOutcome { None = 0, Won = 1, Lost = 2 }

    /// <summary>Bé đang làm gì: đứng / lao tới quái / đánh / đập đất / đỡ / lướt / tuyệt chiêu / bị đánh khựng / gục.</summary>
    public enum HunterAction { Idle = 0, Approach = 1, Strike = 2, Slam = 3, Guard = 4, Dash = 5, Ultimate = 6, Hurt = 7, Dead = 8 }

    /// <summary>Quái đang làm gì: đi tới / lấy đà (báo trước) / ra đòn / nghỉ sau đòn (lao húc xong thì choáng) / bị choáng / đang chết / đã biến mất.</summary>
    public enum MonsterPhase { Approach = 0, Windup = 1, Act = 2, Recover = 3, Stunned = 4, Dying = 5, Gone = 6 }

    public enum HuntEventKind
    {
        WaveStart, Spawn, Windup, Interrupted, Hit, Kill, PetHurt, Guarded, PerfectGuard, Dodged, Whiff,
        Strike, Slam, Dash, Ultimate, Shot, ShotGone, Smash, Charge, Summon, Enrage, Combo, RageFull, Won, Lost,

        /// <summary>Quái vừa hạ rơi hạt giống (Phase 10.3, HuntSession thêm vào): Value = CropKind, Amount = số hạt, X = chỗ rơi.</summary>
        Drop,
    }

    /// <summary>1 việc vừa xảy ra trong bước mô phỏng (View chạy hoạt cảnh + tiếng theo).</summary>
    public struct HuntEvent
    {
        public HuntEventKind Kind;

        /// <summary>Id quái liên quan (-1 = không có).</summary>
        public int Monster;

        /// <summary>Sát thương (Hit / PetHurt / Guarded).</summary>
        public float Amount;

        public bool Crit;

        /// <summary>Vị trí ngang (m) chỗ xảy ra.</summary>
        public float X;

        /// <summary>Số kèm theo: đợt quái (WaveStart), bậc combo (Combo), id đạn (Shot / ShotGone).</summary>
        public int Value;
    }

    /// <summary>1 con quái trong trận.</summary>
    public sealed class HuntMonster
    {
        public int Id;
        public MonsterKind Kind;
        public MonsterDef Def;
        public float X;
        public int Facing = -1;
        public float Hp, MaxHp, Atk, DefStat;
        public MonsterPhase Phase;
        public float PhaseT;
        public float Cooldown;
        public float StunLeft;

        /// <summary>Đòn đang lấy đà / đang ra.</summary>
        public MonsterAttackDef Attack;

        public bool Enraged;

        /// <summary>Quái do trùm gọi ra (Owner = id trùm).</summary>
        public int Owner = -1;

        public float SummonLeft;
        public int ShotsLeft;
        public float ShotT;
        public int ChargeDir;
        public float ChargeLeft;
        public bool HitThisAttack;
        public bool Entered;

        public bool IsAlive => Phase != MonsterPhase.Dying && Phase != MonsterPhase.Gone;
        public float Hp01 => MaxHp > 0f ? Math.Max(0f, Hp) / MaxHp : 0f;
        public bool Flying => Def.Flying > 0f;

        /// <summary>Đang lấy đà: còn bao nhiêu phần (1 → 0) nữa thì ra đòn (View vẽ vòng đỏ thu nhỏ). -1 nếu không lấy đà.</summary>
        public float WindupLeft01 { get; internal set; } = -1f;

        /// <summary>Lao húc xong đang choáng (dễ đánh).</summary>
        public bool Dizzy => Phase == MonsterPhase.Recover && Attack != null && Attack.Move == MonsterMove.Charge;
    }

    /// <summary>Viên đạn (bào tử nấm, sóng đất, mảnh pha lê) bay ngang về phía bé.</summary>
    public sealed class HuntProjectile
    {
        public int Id;
        public int Owner;
        public MonsterKind OwnerKind;
        public float X;
        public int Dir;
        public float Speed;
        public float Power, Atk;
        public bool Low;
        public bool Alive = true;
    }

    /// <summary>Bé khi đi săn.</summary>
    public sealed class Hunter
    {
        public float X;
        public int Facing = 1;
        public float Hp, MaxHp, Atk, Def, Spd, Crit;
        public HunterAction Action;
        public float ActionT;
        public int Target = -1;
        public float StrikeCd, SlamCd, GuardCd, DashCd;
        public float Rage;
        public int Combo;
        public float ComboT;
        public int DashDir = 1;
        internal bool Resolved;
        internal readonly HashSet<int> DashHits = new HashSet<int>();

        public float Hp01 => MaxHp > 0f ? Math.Max(0f, Hp) / MaxHp : 0f;
        public bool Invulnerable => Action == HunterAction.Dash;
    }

    /// <summary>
    /// Mô phỏng 1 trận Săn quái (C# thuần, Phase 9.2), bước cố định + seed => cùng lệnh thì cùng kết quả.
    /// Dải ngang: bé ở giữa, quái rừng đi vào từ 2 mép theo từng đợt. Khác hẳn trận 1-1 PvP:
    ///  * chạm = bé lao tới quái gần nhất (hoặc con được chạm) đánh, trúng liên tục thành combo;
    ///  * vuốt lên = đập đất trúng cả đám quanh bé; vuốt xuống = đỡ (đỡ sát lúc bị đánh = phản đòn);
    ///  * vuốt ngang = lướt né (không bị đánh trúng lúc lướt, lướt xuyên quái thì gây sát thương);
    ///  * đầy Nộ = tuyệt chiêu trúng mọi quái.
    /// Quái lấy đà trước khi đánh (View hiện dấu !) để người chơi kịp đỡ / lướt. Hết các đợt quái = thắng; bé hết máu = thua.
    /// </summary>
    public sealed class HuntSimulation
    {
        private enum WaveState { Waiting, Spawning, Fighting, Done }

        private readonly HuntConfig _cfg;
        private readonly HuntPlan _plan;
        private readonly IRandom _rng;
        private readonly List<HuntMonster> _monsters = new List<HuntMonster>();
        private readonly List<HuntProjectile> _shots = new List<HuntProjectile>();
        private readonly List<HuntEvent> _events = new List<HuntEvent>();
        private readonly float _strikeCooldown;
        private int _nextId = 1;
        private int _wave = -1;
        private WaveState _waveState = WaveState.Waiting;
        private float _waveTimer;
        private int _spawnIdx;
        private float _spawnTimer;

        // lệnh đang chờ (giữ InputBuffer giây, làm ngay khi bé rảnh)
        private HuntCommand _pending;
        private int _pendingTarget = -1;
        private int _pendingDir;
        private float _pendingLeft;

        public HuntSimulation(HuntPlan plan, HunterStats stats, ulong seed, HuntConfig cfg = null)
        {
            _plan = plan ?? throw new ArgumentNullException(nameof(plan));
            _cfg = cfg ?? new HuntConfig();
            _rng = new SeededRandom(seed, 21UL);
            float hp = Math.Max(1f, stats.Hp * _cfg.HunterHpScale);
            Hunter = new Hunter
            {
                X = 0f, Hp = hp, MaxHp = hp, Atk = Math.Max(1f, stats.Atk), Def = Math.Max(0f, stats.Def),
                Spd = Math.Max(1f, stats.Spd), Crit = Math.Max(0f, stats.Crit),
            };
            // SPD cao hơn quái chuẩn của vùng thì đánh nhanh hơn chút (và ngược lại), có giới hạn
            float ratio = _plan.RefSpd > 0f ? (float)Math.Pow(_plan.RefSpd / Hunter.Spd, 0.3) : 1f;
            _strikeCooldown = _cfg.StrikeCooldown * Clamp(ratio, 0.75f, 1.3f);
            _waveTimer = _cfg.StartDelay;
        }

        public HuntConfig Config => _cfg;
        public HuntPlan Plan => _plan;
        public Hunter Hunter { get; }
        public IReadOnlyList<HuntMonster> Monsters => _monsters;
        public IReadOnlyList<HuntProjectile> Projectiles => _shots;

        /// <summary>Việc xảy ra ở bước vừa chạy (xóa đầu mỗi bước).</summary>
        public IReadOnlyList<HuntEvent> Events => _events;

        public float Time { get; private set; }
        public HuntOutcome Outcome { get; private set; }
        public int Kills { get; private set; }

        /// <summary>Số con đã hạ theo loại quái (chỉ số = MonsterKind) — sổ tay quái rừng (Phase 10.3).</summary>
        public int[] KillsByKind { get; } = new int[Enum.GetValues(typeof(MonsterKind)).Length];
        public int PerfectGuards { get; private set; }
        public int Dodges { get; private set; }

        /// <summary>Đợt quái hiện tại (0..), -1 = chưa bắt đầu.</summary>
        public int Wave => _wave;
        public int WaveCount => _plan.Waves.Count;
        public float StrikeCooldown => _strikeCooldown;

        public int AliveCount
        {
            get
            {
                int n = 0;
                foreach (HuntMonster m in _monsters)
                    if (m.IsAlive) n++;
                return n;
            }
        }

        public HuntMonster Find(int id)
        {
            foreach (HuntMonster m in _monsters)
                if (m.Id == id) return m;
            return null;
        }

        // ------------------------------------------------------------------ lệnh của người chơi

        /// <summary>Chạm: đánh quái `monsterId` (-1 = con gần nhất).</summary>
        public void Tap(int monsterId = -1) => Queue(HuntCommand.Strike, monsterId, 0);

        /// <summary>Vuốt lên: đập đất.</summary>
        public void Slam() => Queue(HuntCommand.Slam, -1, 0);

        /// <summary>Vuốt xuống: đỡ.</summary>
        public void Guard() => Queue(HuntCommand.Guard, -1, 0);

        /// <summary>Vuốt ngang: lướt sang trái (-1) / phải (+1).</summary>
        public void Dash(int dir) => Queue(HuntCommand.Dash, -1, dir >= 0 ? 1 : -1);

        /// <summary>Vuốt Z / nút Nộ (Nộ phải đầy).</summary>
        public void Ultimate() => Queue(HuntCommand.Ultimate, -1, 0);

        public bool RageFull => Hunter.Rage >= _cfg.RageMax - 1e-3f;

        private void Queue(HuntCommand c, int target, int dir)
        {
            if (Outcome != HuntOutcome.None) return;
            _pending = c;
            _pendingTarget = target;
            _pendingDir = dir;
            _pendingLeft = _cfg.InputBuffer;
        }

        // ------------------------------------------------------------------ mỗi bước

        public void Step()
        {
            if (Outcome != HuntOutcome.None) return;
            _events.Clear();
            float dt = _cfg.TickSeconds;
            Time += dt;
            UpdateWaves(dt);
            TryPending(dt);
            UpdateHunter(dt);
            for (int i = 0; i < _monsters.Count; i++) UpdateMonster(_monsters[i], dt);
            UpdateShots(dt);
            Separate();
            _monsters.RemoveAll(m => m.Phase == MonsterPhase.Gone);
            _shots.RemoveAll(s => !s.Alive);
            if (Outcome == HuntOutcome.None && Time >= _cfg.MaxSeconds) Lose();
        }

        // ------------------------------------------------------------------ đợt quái

        private void UpdateWaves(float dt)
        {
            switch (_waveState)
            {
                case WaveState.Waiting:
                    _waveTimer -= dt;
                    if (_waveTimer > 0f) return;
                    _wave++;
                    _spawnIdx = 0;
                    _spawnTimer = 0f;
                    _waveState = WaveState.Spawning;
                    Emit(HuntEventKind.WaveStart, -1, 0f, false, 0f, _wave);
                    break;
                case WaveState.Spawning:
                    List<HuntSpawn> spawns = _plan.Waves[_wave].Spawns;
                    _spawnTimer -= dt;
                    while (_spawnIdx < spawns.Count && _spawnTimer <= 0f)
                    {
                        HuntSpawn s = spawns[_spawnIdx++];
                        Spawn(s.Kind, s.Side, -1);
                        if (_spawnIdx < spawns.Count) _spawnTimer += spawns[_spawnIdx].Delay;
                    }
                    if (_spawnIdx >= spawns.Count) _waveState = WaveState.Fighting;
                    break;
                case WaveState.Fighting:
                    if (AliveCount > 0) return;
                    if (_wave >= _plan.Waves.Count - 1)
                    {
                        _waveState = WaveState.Done;
                        Outcome = HuntOutcome.Won;
                        Emit(HuntEventKind.Won, -1, 0f, false, Hunter.X, 0);
                        return;
                    }
                    _waveState = WaveState.Waiting;
                    _waveTimer = _cfg.WaveBreak;
                    break;
            }
        }

        private HuntMonster Spawn(MonsterKind kind, int side, int owner)
        {
            MonsterDef def = MonsterBook.Get(kind);
            float hp = Math.Max(1f, _plan.RefHp * def.Hp);
            var m = new HuntMonster
            {
                Id = _nextId++, Kind = kind, Def = def, X = (side >= 0 ? 1f : -1f) * (_cfg.ArenaHalf + 0.5f), Facing = side >= 0 ? -1 : 1,
                Hp = hp, MaxHp = hp, Atk = Math.Max(1f, _plan.RefAtk * def.Atk), DefStat = Math.Max(0f, _plan.RefDef * def.Def),
                Phase = MonsterPhase.Approach, Cooldown = 0.5f + (float)_rng.NextDouble() * 0.6f, Owner = owner,
                SummonLeft = def.SummonEvery * 0.6f,
            };
            _monsters.Add(m);
            Emit(HuntEventKind.Spawn, m.Id, 0f, false, m.X, 0);
            return m;
        }

        // ------------------------------------------------------------------ bé

        private bool CanAct(HuntCommand c)
        {
            Hunter h = Hunter;
            switch (h.Action)
            {
                case HunterAction.Idle:
                case HunterAction.Approach:
                    return true;
                case HunterAction.Strike:   // đánh xong nhát rồi thì đỡ / lướt / đánh tiếp được ngay (bắt nhịp nhanh)
                    return h.Resolved;
                default:
                    return false;
            }
        }

        private void TryPending(float dt)
        {
            if (_pending == HuntCommand.None) return;
            _pendingLeft -= dt;
            if (_pendingLeft < 0f)
            {
                _pending = HuntCommand.None;
                return;
            }
            if (!CanAct(_pending)) return;
            Hunter h = Hunter;
            switch (_pending)
            {
                case HuntCommand.Strike:
                    if (h.StrikeCd > 0f) return;   // chờ hồi đòn (vẫn giữ lệnh)
                    HuntMonster t = PickTarget(_pendingTarget);
                    if (t == null)
                    {
                        _pending = HuntCommand.None;
                        return;
                    }
                    h.Target = t.Id;
                    h.Facing = Math.Sign(t.X - h.X) >= 0 ? 1 : -1;
                    if (Gap(t) <= _cfg.StrikeReach) BeginStrike();
                    else Begin(HunterAction.Approach);
                    break;
                case HuntCommand.Slam:
                    if (h.SlamCd > 0f)
                    {
                        _pending = HuntCommand.None;
                        return;
                    }
                    Begin(HunterAction.Slam);
                    h.SlamCd = _cfg.SlamCooldown;
                    break;
                case HuntCommand.Guard:
                    if (h.GuardCd > 0f) return;
                    Begin(HunterAction.Guard);
                    break;
                case HuntCommand.Dash:
                    if (h.DashCd > 0f) return;
                    h.DashDir = _pendingDir == 0 ? -h.Facing : _pendingDir;
                    h.Facing = h.DashDir;
                    h.DashHits.Clear();
                    Begin(HunterAction.Dash);
                    h.DashCd = _cfg.DashCooldown;
                    Emit(HuntEventKind.Dash, -1, 0f, false, h.X, h.DashDir);
                    break;
                case HuntCommand.Ultimate:
                    if (!RageFull)
                    {
                        _pending = HuntCommand.None;
                        return;
                    }
                    h.Rage = 0f;
                    Begin(HunterAction.Ultimate);
                    break;
            }
            _pending = HuntCommand.None;
        }

        private void Begin(HunterAction a)
        {
            Hunter.Action = a;
            Hunter.ActionT = 0f;
            Hunter.Resolved = false;
        }

        private void BeginStrike()
        {
            Begin(HunterAction.Strike);
            Hunter.StrikeCd = _strikeCooldown;
            Emit(HuntEventKind.Strike, Hunter.Target, 0f, false, Hunter.X, 0);
        }

        /// <summary>Con được chạm (còn sống) hoặc con gần nhất; ưu tiên con đang đứng trước mặt nếu gần ngang nhau.</summary>
        private HuntMonster PickTarget(int id)
        {
            if (id >= 0)
            {
                HuntMonster m = Find(id);
                if (m != null && m.IsAlive) return m;
            }
            HuntMonster best = null;
            float bd = float.MaxValue;
            foreach (HuntMonster m in _monsters)
            {
                if (!m.IsAlive) continue;
                float d = Math.Abs(m.X - Hunter.X);
                if (Math.Sign(m.X - Hunter.X) != Hunter.Facing) d += 0.8f;
                if (d < bd)
                {
                    bd = d;
                    best = m;
                }
            }
            return best;
        }

        private float Gap(HuntMonster m) => Math.Abs(m.X - Hunter.X) - (_cfg.HunterSize + m.Def.Size);

        private void UpdateHunter(float dt)
        {
            Hunter h = Hunter;
            h.StrikeCd = Math.Max(0f, h.StrikeCd - dt);
            h.SlamCd = Math.Max(0f, h.SlamCd - dt);
            h.GuardCd = Math.Max(0f, h.GuardCd - dt);
            h.DashCd = Math.Max(0f, h.DashCd - dt);
            if (h.Combo > 0)
            {
                h.ComboT -= dt;
                if (h.ComboT <= 0f) h.Combo = 0;
            }
            h.ActionT += dt;
            switch (h.Action)
            {
                case HunterAction.Idle:
                    HuntMonster near = PickTarget(-1);
                    if (near != null && Math.Abs(near.X - h.X) > 0.05f) h.Facing = near.X > h.X ? 1 : -1;
                    break;

                case HunterAction.Approach:
                {
                    HuntMonster t = Find(h.Target);
                    if (t == null || !t.IsAlive || h.ActionT > 1.5f)
                    {
                        Begin(HunterAction.Idle);
                        break;
                    }
                    h.Facing = t.X >= h.X ? 1 : -1;
                    float gap = Gap(t);
                    if (gap <= _cfg.StrikeReach)
                    {
                        BeginStrike();
                        break;
                    }
                    h.X = ClampHunter(h.X + h.Facing * Math.Min(_cfg.ApproachSpeed * dt, gap - _cfg.StrikeReach * 0.5f));
                    break;
                }

                case HunterAction.Strike:
                    if (!h.Resolved && h.ActionT >= _cfg.StrikeHitTime)
                    {
                        h.Resolved = true;
                        HuntMonster t = Find(h.Target);
                        if (t != null && t.IsAlive && Gap(t) <= _cfg.StrikeReach + 0.35f) DamageMonster(t, _cfg.StrikePower, true);
                        else Emit(HuntEventKind.Whiff, -1, 0f, false, h.X, 0);
                    }
                    if (h.ActionT >= _cfg.StrikeTime) Begin(HunterAction.Idle);
                    break;

                case HunterAction.Slam:
                    if (!h.Resolved && h.ActionT >= _cfg.SlamHitTime)
                    {
                        h.Resolved = true;
                        Emit(HuntEventKind.Slam, -1, 0f, false, h.X, 0);
                        for (int i = 0; i < _monsters.Count; i++)
                        {
                            HuntMonster m = _monsters[i];
                            if (!m.IsAlive || Math.Abs(m.X - h.X) > _cfg.SlamRadius + m.Def.Size) continue;
                            DamageMonster(m, _cfg.SlamPower, true, heavy: true);
                            if (!m.IsAlive) continue;
                            float knock = m.Def.Boss ? _cfg.SlamKnockback * 0.3f : _cfg.SlamKnockback;
                            m.X += (m.X >= h.X ? 1f : -1f) * knock;
                            Stun(m, m.Def.Boss ? _cfg.SlamStun * 0.4f : _cfg.SlamStun);
                        }
                    }
                    if (h.ActionT >= _cfg.SlamTime) Begin(HunterAction.Idle);
                    break;

                case HunterAction.Guard:
                    if (h.ActionT >= _cfg.GuardTime)
                    {
                        Begin(HunterAction.Idle);
                        h.GuardCd = _cfg.GuardCooldown;
                    }
                    break;

                case HunterAction.Dash:
                {
                    float speed = _cfg.DashDistance / Math.Max(0.01f, _cfg.DashTime);
                    h.X = ClampHunter(h.X + h.DashDir * speed * dt);
                    for (int i = 0; i < _monsters.Count; i++)
                    {
                        HuntMonster m = _monsters[i];
                        if (!m.IsAlive || h.DashHits.Contains(m.Id) || Math.Abs(m.X - h.X) > m.Def.Size + _cfg.HunterSize) continue;
                        h.DashHits.Add(m.Id);
                        DamageMonster(m, _cfg.DashPower, false);
                    }
                    if (h.ActionT >= _cfg.DashTime) Begin(HunterAction.Idle);
                    break;
                }

                case HunterAction.Ultimate:
                    if (!h.Resolved && h.ActionT >= _cfg.UltimateHitTime)
                    {
                        h.Resolved = true;
                        Emit(HuntEventKind.Ultimate, -1, 0f, false, h.X, 0);
                        for (int i = 0; i < _monsters.Count; i++)
                        {
                            HuntMonster m = _monsters[i];
                            if (!m.IsAlive) continue;
                            DamageMonster(m, _cfg.UltimatePower, true, rage: false, heavy: true);
                            if (m.IsAlive) Stun(m, _cfg.UltimateStun);
                        }
                    }
                    if (h.ActionT >= _cfg.UltimateTime) Begin(HunterAction.Idle);
                    break;

                case HunterAction.Hurt:
                    if (h.ActionT >= _cfg.HurtTime) Begin(HunterAction.Idle);
                    break;
            }
        }

        private float ClampHunter(float x) => Clamp(x, -_cfg.HunterHalf, _cfg.HunterHalf);

        // ------------------------------------------------------------------ sát thương

        private static float Damage(float power, float atk, float def) => power * atk * atk / Math.Max(1f, atk + def);

        private float Vary() => 0.92f + (float)_rng.NextDouble() * 0.16f;

        private void AddRage(float amount)
        {
            Hunter h = Hunter;
            bool was = RageFull;
            h.Rage = Math.Min(_cfg.RageMax, h.Rage + amount);
            if (!was && RageFull) Emit(HuntEventKind.RageFull, -1, 0f, false, h.X, 0);
        }

        private void DamageMonster(HuntMonster m, float power, bool canCrit, bool rage = true, bool heavy = false)
        {
            Hunter h = Hunter;
            bool crit = canCrit && _rng.NextDouble() < h.Crit / 100.0;
            float combo = 1f + Math.Min(h.Combo, _cfg.ComboMax) * _cfg.ComboStep;
            float dmg = Damage(power, h.Atk, m.DefStat) * combo * (crit ? _cfg.CritMultiplier : 1f) * Vary();
            m.Hp -= dmg;
            h.Combo++;
            h.ComboT = _cfg.ComboWindow;
            if (rage) AddRage(_cfg.RageOnHit);
            Emit(HuntEventKind.Hit, m.Id, dmg, crit, m.X, 0);
            if (h.Combo >= 2) Emit(HuntEventKind.Combo, m.Id, 0f, false, m.X, h.Combo);
            if (m.Hp <= 0f)
            {
                Kill(m);
                return;
            }
            // quái nhỏ bị đánh lúc mới lấy đà (nửa đầu) thì khựng, mất đòn; sát lúc ra đòn thì phải đỡ / lướt (đòn nặng thì lúc nào cũng cắt được)
            if (m.Phase == MonsterPhase.Windup && m.Def.Interruptible && (heavy || m.WindupLeft01 > _cfg.InterruptBefore))
            {
                m.Phase = MonsterPhase.Approach;
                m.WindupLeft01 = -1f;
                m.Cooldown = _cfg.InterruptCooldown;
                Emit(HuntEventKind.Interrupted, m.Id, 0f, false, m.X, 0);
            }
            if (!m.Def.Boss && m.Phase != MonsterPhase.Act) m.X += (m.X >= h.X ? 1f : -1f) * 0.25f;   // giật lùi nhẹ
            if (m.Def.Boss && !m.Enraged && m.Hp01 <= _cfg.EnrageBelow)
            {
                m.Enraged = true;
                Emit(HuntEventKind.Enrage, m.Id, 0f, false, m.X, 0);
            }
        }

        private void Kill(HuntMonster m)
        {
            m.Hp = 0f;
            m.Phase = MonsterPhase.Dying;
            m.PhaseT = 0f;
            m.WindupLeft01 = -1f;
            Kills++;
            KillsByKind[(int)m.Kind]++;
            AddRage(_cfg.RageOnKill);
            Emit(HuntEventKind.Kill, m.Id, 0f, false, m.X, 0);
        }

        private void Stun(HuntMonster m, float seconds)
        {
            if (!m.IsAlive) return;
            m.Phase = MonsterPhase.Stunned;
            m.StunLeft = Math.Max(m.StunLeft, seconds);
            m.WindupLeft01 = -1f;
        }

        /// <summary>Bé bị đánh: đang lướt = né; đang đỡ = giảm sát thương (đỡ sát lúc = phản đòn, không mất máu).</summary>
        private void HurtHunter(float power, float atk, HuntMonster attacker, bool ranged)
        {
            Hunter h = Hunter;
            if (Outcome != HuntOutcome.None) return;
            int id = attacker != null ? attacker.Id : -1;
            if (h.Invulnerable)
            {
                Dodges++;
                Emit(HuntEventKind.Dodged, id, 0f, false, h.X, 0);
                return;
            }
            float dmg = Damage(power, atk, h.Def) * Vary();
            if (h.Action == HunterAction.Guard)
            {
                if (h.ActionT <= _cfg.PerfectWindow)
                {
                    PerfectGuards++;
                    AddRage(_cfg.RagePerfectGuard);
                    Emit(HuntEventKind.PerfectGuard, id, 0f, false, h.X, 0);
                    if (attacker != null && attacker.IsAlive && (!ranged || Math.Abs(attacker.X - h.X) <= 2.5f))
                    {
                        DamageMonster(attacker, _cfg.CounterPower, true, rage: false, heavy: true);
                        if (attacker.IsAlive) Stun(attacker, attacker.Def.Boss ? _cfg.CounterStun * 0.5f : _cfg.CounterStun);
                    }
                    return;
                }
                dmg *= _cfg.GuardTakes;
                Emit(HuntEventKind.Guarded, id, dmg, false, h.X, 0);
            }
            else if (h.Action != HunterAction.Ultimate)
            {
                Begin(HunterAction.Hurt);
                h.Combo = 0;
            }
            h.Hp -= dmg;
            AddRage(_cfg.RageOnHurt);
            Emit(HuntEventKind.PetHurt, id, dmg, false, h.X, 0);
            if (h.Hp <= 0f) Lose();
        }

        private void Lose()
        {
            Hunter.Hp = Math.Max(0f, Hunter.Hp);
            Hunter.Action = HunterAction.Dead;
            Outcome = HuntOutcome.Lost;
            Emit(HuntEventKind.Lost, -1, 0f, false, Hunter.X, 0);
        }

        // ------------------------------------------------------------------ quái

        /// <summary>Chọn đòn hợp khoảng cách: bé đứng sát thì ưu tiên đòn gần (cắn / đập đất), xa thì đòn chính (bắn / lao húc).</summary>
        private static MonsterAttackDef Choose(HuntMonster m, float gap)
        {
            MonsterAttackDef a = m.Def.Primary, b = m.Def.Secondary;
            if (b != null && (b.Move == MonsterMove.Melee || b.Move == MonsterMove.Smash) && Usable(b, gap)) return b;
            if (Usable(a, gap)) return a;
            return b != null && Usable(b, gap) ? b : null;
        }

        private static bool Usable(MonsterAttackDef a, float gap)
        {
            switch (a.Move)
            {
                case MonsterMove.Ranged: return gap <= a.Range && gap >= 0.6f;
                case MonsterMove.Charge: return gap <= a.Range && gap >= 1.2f;
                default: return gap <= a.Range;
            }
        }

        private void UpdateMonster(HuntMonster m, float dt)
        {
            if (m.Phase == MonsterPhase.Gone) return;
            if (m.Phase == MonsterPhase.Dying)
            {
                m.PhaseT += dt;
                if (m.PhaseT >= 0.6f) m.Phase = MonsterPhase.Gone;
                return;
            }
            if (m.Phase == MonsterPhase.Stunned)
            {
                m.StunLeft -= dt;
                if (m.StunLeft <= 0f) m.Phase = MonsterPhase.Approach;
                return;
            }
            Hunter h = Hunter;
            m.Cooldown -= dt;
            if (m.Def.Boss && m.Def.SummonEvery > 0f)
            {
                m.SummonLeft -= dt;
                if (m.SummonLeft <= 0f)
                {
                    m.SummonLeft = m.Def.SummonEvery * (m.Enraged ? 0.8f : 1f);
                    SummonFor(m);
                }
            }
            float dx = h.X - m.X;
            int dir = dx >= 0f ? 1 : -1;
            float gap = Math.Abs(dx) - (_cfg.HunterSize + m.Def.Size);
            switch (m.Phase)
            {
                case MonsterPhase.Approach:
                {
                    m.Facing = dir;
                    MonsterAttackDef a = Choose(m, gap);
                    if (a != null && m.Cooldown <= 0f && h.Action != HunterAction.Dead)
                    {
                        m.Attack = a;
                        m.Phase = MonsterPhase.Windup;
                        m.PhaseT = 0f;
                        m.WindupLeft01 = 1f;
                        Emit(HuntEventKind.Windup, m.Id, 0f, false, m.X, (int)a.Move);
                        break;
                    }
                    float speed = m.Def.Speed * (m.Enraged ? _cfg.EnrageSpeed : 1f);
                    float move = 0f;
                    MonsterAttackDef p = m.Def.Primary;
                    if (p.Move == MonsterMove.Ranged && m.Def.KeepDistance > 0f)
                    {
                        if (gap > m.Def.KeepDistance + 0.3f) move = dir;
                        else if (gap < m.Def.KeepDistance - 1.2f && Math.Abs(m.X) < _cfg.ArenaHalf - 0.5f) move = -dir * 0.7f;   // lùi giữ khoảng cách
                    }
                    else if (p.Move == MonsterMove.Charge)
                    {
                        MonsterAttackDef close = m.Def.Secondary;
                        if (gap > p.Range * 0.85f) move = dir;   // vào tầm lao
                        else if (gap < 1.2f) move = close != null && gap > close.Range * 0.7f ? dir : 0f;   // gần quá để lao: áp sát cắn / đập
                        else if (m.Cooldown > 0f) move = dir * 0.35f;   // chờ hồi đòn: nhích dần
                    }
                    else if (gap > p.Range * 0.7f) move = dir;
                    float step = move * speed * dt;
                    if (move * dir > 0f) step = dir * Math.Min(Math.Abs(step), Math.Max(0f, gap - 0.05f));   // không đi xuyên qua bé
                    m.X += step;
                    break;
                }

                case MonsterPhase.Windup:
                {
                    m.PhaseT += dt;
                    float wu = m.Attack.Windup * (m.Enraged ? _cfg.EnrageWindup : 1f);
                    m.WindupLeft01 = Math.Max(0f, 1f - m.PhaseT / Math.Max(0.01f, wu));
                    if (m.PhaseT < wu) break;
                    m.WindupLeft01 = -1f;
                    m.Phase = MonsterPhase.Act;
                    m.PhaseT = 0f;
                    m.HitThisAttack = false;
                    switch (m.Attack.Move)
                    {
                        case MonsterMove.Melee:
                            if (gap <= m.Attack.Range + 0.3f) HurtHunter(m.Attack.Power, m.Atk, m, false);
                            ToRecover(m);
                            break;
                        case MonsterMove.Smash:
                            Emit(HuntEventKind.Smash, m.Id, 0f, false, m.X, 0);
                            if (Math.Abs(dx) <= m.Attack.Radius + _cfg.HunterSize) HurtHunter(m.Attack.Power, m.Atk, m, false);
                            ToRecover(m);
                            break;
                        case MonsterMove.Ranged:
                            m.ShotsLeft = Math.Max(1, m.Attack.Shots);
                            m.ShotT = 0f;
                            break;
                        case MonsterMove.Charge:
                            m.ChargeDir = dir;
                            m.ChargeLeft = Math.Min(Math.Abs(dx) + 2f, 8f);
                            Emit(HuntEventKind.Charge, m.Id, 0f, false, m.X, dir);
                            break;
                    }
                    break;
                }

                case MonsterPhase.Act:
                    if (m.Attack.Move == MonsterMove.Ranged)
                    {
                        m.ShotT -= dt;
                        if (m.ShotT <= 0f && m.ShotsLeft > 0)
                        {
                            Fire(m, dir);
                            m.ShotsLeft--;
                            m.ShotT = m.Attack.ShotGap;
                        }
                        if (m.ShotsLeft <= 0) ToRecover(m);
                    }
                    else if (m.Attack.Move == MonsterMove.Charge)
                    {
                        float step = m.Attack.ChargeSpeed * dt;
                        m.X += m.ChargeDir * step;
                        m.ChargeLeft -= step;
                        if (!m.HitThisAttack && Math.Abs(h.X - m.X) <= m.Def.Size + _cfg.HunterSize)
                        {
                            m.HitThisAttack = true;
                            HurtHunter(m.Attack.Power, m.Atk, m, false);
                        }
                        if (m.Phase == MonsterPhase.Act && (m.ChargeLeft <= 0f || Math.Abs(m.X) >= _cfg.ArenaHalf))
                        {
                            m.X = Clamp(m.X, -_cfg.ArenaHalf, _cfg.ArenaHalf);
                            ToRecover(m);
                        }
                    }
                    else ToRecover(m);
                    break;

                case MonsterPhase.Recover:
                    m.PhaseT += dt;
                    if (m.PhaseT >= m.Attack.Recover)
                    {
                        m.Phase = MonsterPhase.Approach;
                        m.Cooldown = m.Attack.Cooldown * (m.Enraged ? _cfg.EnrageCooldown : 1f);
                    }
                    break;
            }
            if (Math.Abs(m.X) <= _cfg.ArenaHalf) m.Entered = true;
            float lim = m.Entered ? _cfg.ArenaHalf : _cfg.ArenaHalf + 1f;   // đã vào sân thì không ra ngoài tầm bé với tới
            m.X = Clamp(m.X, -lim, lim);
        }

        private static void ToRecover(HuntMonster m)
        {
            if (!m.IsAlive || m.Phase == MonsterPhase.Stunned) return;
            m.Phase = MonsterPhase.Recover;
            m.PhaseT = 0f;
        }

        private void SummonFor(HuntMonster boss)
        {
            int alive = 0;
            foreach (HuntMonster m in _monsters)
                if (m.IsAlive && m.Owner == boss.Id) alive++;
            int n = Math.Min(boss.Def.SummonCount, boss.Def.SummonCap - alive);
            if (n <= 0) return;
            Emit(HuntEventKind.Summon, boss.Id, 0f, false, boss.X, n);
            int side = boss.X >= Hunter.X ? -1 : 1;   // gọi ra phía sau lưng bé: phải quay lại đánh
            for (int i = 0; i < n; i++) Spawn(boss.Def.Summon, i % 2 == 0 ? side : -side, boss.Id);
        }

        // ------------------------------------------------------------------ đạn

        private void Fire(HuntMonster m, int dir)
        {
            var s = new HuntProjectile
            {
                Id = _nextId++, Owner = m.Id, OwnerKind = m.Kind, X = m.X + dir * m.Def.Size, Dir = dir,
                Speed = m.Attack.ShotSpeed, Power = m.Attack.Power, Atk = m.Atk, Low = m.Attack.LowShot,
            };
            _shots.Add(s);
            Emit(HuntEventKind.Shot, m.Id, 0f, false, s.X, s.Id);
        }

        private void UpdateShots(float dt)
        {
            Hunter h = Hunter;
            foreach (HuntProjectile s in _shots)
            {
                if (!s.Alive) continue;
                float before = s.X - h.X;
                s.X += s.Dir * s.Speed * dt;
                float after = s.X - h.X;
                bool crossed = Math.Sign(before) != Math.Sign(after);
                if (Math.Abs(after) <= _cfg.HunterSize + 0.2f || crossed)
                {
                    s.Alive = false;
                    HurtHunter(s.Power, s.Atk, Find(s.Owner), true);
                    Emit(HuntEventKind.ShotGone, s.Owner, 0f, false, s.X, s.Id);
                    continue;
                }
                if (Math.Abs(s.X) > _cfg.ArenaHalf + 1.5f)
                {
                    s.Alive = false;
                    Emit(HuntEventKind.ShotGone, s.Owner, 0f, false, s.X, s.Id);
                }
            }
        }

        // ------------------------------------------------------------------

        /// <summary>Quái đi đất không chồng lên nhau và không đứng lẫn vào người bé (đẩy nhẹ cho cách ra; đang lao húc thì xuyên qua).</summary>
        private void Separate()
        {
            Hunter h = Hunter;
            foreach (HuntMonster m in _monsters)
            {
                if (!m.IsAlive || m.Flying || m.Phase == MonsterPhase.Act) continue;
                float min = (m.Def.Size + _cfg.HunterSize) * 0.85f, d = m.X - h.X;
                if (Math.Abs(d) >= min) continue;
                m.X = h.X + (d >= 0f ? 1f : -1f) * min;
            }
            for (int i = 0; i < _monsters.Count; i++)
            {
                HuntMonster a = _monsters[i];
                if (!a.IsAlive || a.Flying || a.Phase == MonsterPhase.Act) continue;
                for (int j = i + 1; j < _monsters.Count; j++)
                {
                    HuntMonster b = _monsters[j];
                    if (!b.IsAlive || b.Flying || b.Phase == MonsterPhase.Act) continue;
                    float min = (a.Def.Size + b.Def.Size) * 0.9f, d = b.X - a.X;
                    if (Math.Abs(d) >= min) continue;
                    float push = (min - Math.Abs(d)) * 0.5f * (d >= 0f ? 1f : -1f);
                    if (Math.Abs(d) < 1e-4f) push = (a.Id < b.Id ? 1f : -1f) * min * 0.5f;
                    a.X -= push;
                    b.X += push;
                }
            }
        }

        private void Emit(HuntEventKind kind, int monster, float amount, bool crit, float x, int value) =>
            _events.Add(new HuntEvent { Kind = kind, Monster = monster, Amount = amount, Crit = crit, X = x, Value = value });

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
    }

    /// <summary>Lệnh của người chơi cho bé khi đi săn.</summary>
    public enum HuntCommand { None = 0, Strike = 1, Slam = 2, Guard = 3, Dash = 4, Ultimate = 5 }
}
