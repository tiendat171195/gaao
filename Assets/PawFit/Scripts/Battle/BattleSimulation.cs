using System;
using System.Collections.Generic;
using PawFit.Core;

namespace PawFit.Battle
{
    /// <summary>4 chiêu: ngang = Nhanh, lên = Mạnh (hất tung), V = Chiêu riêng, Z = Ultimate.</summary>
    public enum MoveKind { Quick = 0, Heavy = 1, Special = 2, Ultimate = 3 }

    /// <summary>Chỉ số 1 bên khi đấu (lấy từ PetStats ở View / App, Battle không phụ thuộc Pet).</summary>
    public struct FighterStats
    {
        public float Hp, Atk, Def, Spd;

        /// <summary>Tỉ lệ chí mạng (%).</summary>
        public float Crit;

        public FighterStats(float hp, float atk, float def, float spd, float crit)
        {
            Hp = hp;
            Atk = atk;
            Def = def;
            Spd = spd;
            Crit = crit;
        }
    }

    /// <summary>Số liệu trận đấu — GDD G9 (chỉnh ở đây, không phải sửa code).</summary>
    public sealed class BattleConfig
    {
        /// <summary>PvP 90 giây; hết giờ bên còn nhiều %HP hơn thắng.</summary>
        public float DurationSeconds = 90f;

        /// <summary>Mô phỏng theo bước cố định (30 lần / giây) => mọi máy ra cùng kết quả.</summary>
        public float TickSeconds = 1f / 30f;

        /// <summary>Hồi chiêu sau mỗi đòn.</summary>
        public float CooldownSeconds = 0.8f;

        /// <summary>Vuốt sai thì bị hở (không làm gì được) bấy nhiêu giây.</summary>
        public float WrongSwipeOpenSeconds = 1f;

        /// <summary>Thế đỡ (vuốt xuống) kéo dài bấy nhiêu giây; đỡ trong bấy nhiêu giây cuối trước đòn = phản đòn.</summary>
        public float GuardSeconds = 0.45f;
        public float ParryWindowSeconds = 0.2f;

        /// <summary>Đỡ xong phải chờ bấy nhiêu giây mới đỡ tiếp được (không đỡ liên tục). Đỡ không cần chờ hồi chiêu đánh.</summary>
        public float GuardCooldownSeconds = 0.6f;
        public float GuardDamageFactor = 0.2f;

        /// <summary>Phản đòn: gây ATK × hệ số này lên bên tấn công, bên đó bị hở bấy nhiêu giây.</summary>
        public float CounterAtkFactor = 0.8f;
        public float CounterStunSeconds = 1f;

        public float CritMultiplier = 1.5f;

        /// <summary>Chiêu chưa thuộc (Mode Huấn luyện chưa đủ 10 lần) vẫn dùng được nhưng yếu hơn.</summary>
        public float UnlearnedFactor = 0.8f;

        /// <summary>Bên SPD thấp hơn phải chờ bấy nhiêu giây mới được ra đòn đầu (SPD quyết định ai đánh trước).</summary>
        public float FirstMoveDelaySeconds = 0.5f;

        /// <summary>Hệ số damage + thời gian vung đòn (trước khi trúng) của 4 chiêu.</summary>
        public float[] MoveMultiplier = { 1f, 1.6f, 2.2f, 3.2f };
        public float[] WindupSeconds = { 0.25f, 0.5f, 0.6f, 0.9f };
    }

    public enum BattleEventKind { AttackStarted, Hit, Blocked, Countered, Interrupted, WrongSwipe, Guard, KO, TimeUp }

    /// <summary>Chuyện vừa xảy ra trong trận (View chạy hoạt cảnh theo đây).</summary>
    public struct BattleEvent
    {
        public BattleEventKind Kind;

        /// <summary>Bên gây ra sự kiện (0 hoặc 1). Hit / Blocked: bên tấn công. Countered: bên đỡ phản đòn.</summary>
        public int Side;

        public MoveKind Move;
        public float Damage;
        public bool Crit;
        public float Time;
    }

    public enum BattleOutcome { Ongoing, Win0, Win1, Draw }

    /// <summary>Trạng thái 1 bên trong trận.</summary>
    public sealed class Fighter
    {
        public FighterStats Stats;
        public float Hp;
        public readonly bool[] Learned = new bool[4];
        public float Cooldown;

        /// <summary>Đang bị hở (vuốt sai / bị phản đòn): không ra đòn, không đỡ.</summary>
        public float Open;

        public float GuardLeft;
        public float GuardAge;
        public float GuardCooldown;

        public bool WindingUp;
        public MoveKind WindupMove;
        public float WindupQuality;
        public float WindupLeft;

        public float HpPercent => Stats.Hp > 0 ? Math.Max(0f, Hp) / Stats.Hp : 0f;
        public bool CanAct => Open <= 0f && !WindingUp && Hp > 0f;
    }

    /// <summary>
    /// Mô phỏng trận đấu (C# thuần, có seed => chạy lại ra đúng kết quả, dùng được cho PvP sau này):
    /// vuốt -> vung đòn -> trúng / bị đỡ / bị phản đòn; hồi chiêu 0.8s; vuốt sai hở 1s; hết 90 giây so %HP.
    /// Damage = ATK × hệ số chiêu × hệ số vuốt × 100 / (100 + DEF); chí mạng ×1.5 (G9).
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly BattleConfig _cfg;
        private readonly IRandom _rng;

        public BattleSimulation(FighterStats a, FighterStats b, BattleConfig cfg = null, ulong seed = 1,
                                bool[] learnedA = null, bool[] learnedB = null)
        {
            _cfg = cfg ?? new BattleConfig();
            _rng = new SeededRandom(seed, 91UL);
            Fighters = new[] { NewFighter(a, learnedA), NewFighter(b, learnedB) };
            // SPD quyết định ai đánh trước: bên chậm hơn chờ 1 chút mới được ra đòn đầu
            if (a.Spd > b.Spd) Fighters[1].Cooldown = _cfg.FirstMoveDelaySeconds;
            else if (b.Spd > a.Spd) Fighters[0].Cooldown = _cfg.FirstMoveDelaySeconds;
        }

        public BattleConfig Config => _cfg;
        public Fighter[] Fighters { get; }
        public float Time { get; private set; }
        public float TimeLeft => Math.Max(0f, _cfg.DurationSeconds - Time);
        public BattleOutcome Outcome { get; private set; } = BattleOutcome.Ongoing;
        public bool IsOver => Outcome != BattleOutcome.Ongoing;

        private static Fighter NewFighter(FighterStats s, bool[] learned)
        {
            var f = new Fighter { Stats = s, Hp = s.Hp };
            for (int i = 0; i < 4; i++) f.Learned[i] = learned == null || (i < learned.Length && learned[i]);
            return f;
        }

        public static MoveKind? MoveOf(SwipeKind k)
        {
            switch (k)
            {
                case SwipeKind.Horizontal: return MoveKind.Quick;
                case SwipeKind.Up: return MoveKind.Heavy;
                case SwipeKind.V: return MoveKind.Special;
                case SwipeKind.Z: return MoveKind.Ultimate;
                default: return null;
            }
        }

        /// <summary>
        /// 1 bên vuốt. Trả true nếu được nhận (đang hồi chiêu / bị hở / đang vung đòn thì bỏ qua).
        /// Vuốt sai => hở 1 giây. Vuốt xuống => đỡ. Còn lại => vung đòn.
        /// </summary>
        public bool Submit(int side, SwipeResult swipe, List<BattleEvent> events = null)
        {
            if (IsOver || side < 0 || side > 1) return false;
            Fighter f = Fighters[side];
            if (!f.CanAct) return false;
            if (!swipe.IsValid)
            {
                f.Open = _cfg.WrongSwipeOpenSeconds;
                Emit(events, BattleEventKind.WrongSwipe, side, MoveKind.Quick, 0f, false);
                return true;
            }
            if (swipe.Kind == SwipeKind.Down)   // đỡ: được cả khi đang hồi chiêu đánh, chỉ không được lúc đang vung đòn
            {
                if (f.GuardCooldown > 0f) return false;
                f.GuardLeft = _cfg.GuardSeconds;
                f.GuardAge = 0f;
                f.GuardCooldown = _cfg.GuardCooldownSeconds;
                Emit(events, BattleEventKind.Guard, side, MoveKind.Quick, 0f, false);
                return true;
            }
            if (f.Cooldown > 0f) return false;   // vừa ra đòn: chưa đánh tiếp được
            MoveKind move = MoveOf(swipe.Kind).Value;
            f.WindingUp = true;
            f.WindupMove = move;
            f.WindupQuality = Math.Max(0.7f, Math.Min(1.3f, swipe.Quality));
            f.WindupLeft = _cfg.WindupSeconds[(int)move];
            f.Cooldown = _cfg.CooldownSeconds;
            f.GuardLeft = 0f;
            Emit(events, BattleEventKind.AttackStarted, side, move, 0f, false);
            return true;
        }

        /// <summary>Chạy 1 bước cố định. Sự kiện trong bước được thêm vào `events`.</summary>
        public void Tick(List<BattleEvent> events = null)
        {
            if (IsOver) return;
            float dt = _cfg.TickSeconds;
            Time += dt;
            foreach (Fighter f in Fighters)
            {
                f.Cooldown = Math.Max(0f, f.Cooldown - dt);
                f.GuardCooldown = Math.Max(0f, f.GuardCooldown - dt);
                f.Open = Math.Max(0f, f.Open - dt);
                if (f.GuardLeft > 0f)
                {
                    f.GuardLeft = Math.Max(0f, f.GuardLeft - dt);
                    f.GuardAge += dt;
                }
            }
            // đòn trúng cùng lúc: bên SPD cao hơn tính trước (có thể hạ gục trước khi đòn kia kịp trúng)
            int first = Fighters[1].Stats.Spd > Fighters[0].Stats.Spd ? 1 : 0;
            for (int k = 0; k < 2 && !IsOver; k++)
            {
                int side = k == 0 ? first : 1 - first;
                Fighter f = Fighters[side];
                if (!f.WindingUp) continue;
                f.WindupLeft -= dt;
                if (f.WindupLeft <= 0f) Land(side, events);
            }
            if (!IsOver && Time >= _cfg.DurationSeconds - 1e-4f)
            {
                float p0 = Fighters[0].HpPercent, p1 = Fighters[1].HpPercent;
                Outcome = Math.Abs(p0 - p1) < 1e-4f ? BattleOutcome.Draw : (p0 > p1 ? BattleOutcome.Win0 : BattleOutcome.Win1);
                Emit(events, BattleEventKind.TimeUp, p0 >= p1 ? 0 : 1, MoveKind.Quick, 0f, false);
            }
        }

        /// <summary>Tính damage 1 đòn (không chí mạng) — public để test + hiện số ở Mode Huấn luyện.</summary>
        public float BaseDamage(int attacker, MoveKind move, float quality)
        {
            Fighter a = Fighters[attacker], d = Fighters[1 - attacker];
            float learn = a.Learned[(int)move] ? 1f : _cfg.UnlearnedFactor;
            return a.Stats.Atk * _cfg.MoveMultiplier[(int)move] * quality * learn * 100f / (100f + Math.Max(0f, d.Stats.Def));
        }

        private void Land(int side, List<BattleEvent> events)
        {
            Fighter a = Fighters[side], d = Fighters[1 - side];
            a.WindingUp = false;
            MoveKind move = a.WindupMove;
            if (d.GuardLeft > 0f && d.Open <= 0f)
            {
                if (d.GuardAge <= _cfg.ParryWindowSeconds + 1e-4f)   // đỡ đúng lúc => phản đòn
                {
                    float counter = d.Stats.Atk * _cfg.CounterAtkFactor * 100f / (100f + Math.Max(0f, a.Stats.Def));
                    a.Hp -= counter;
                    a.Open = _cfg.CounterStunSeconds;
                    d.GuardLeft = 0f;
                    Emit(events, BattleEventKind.Countered, 1 - side, move, counter, false);
                    CheckKo(1 - side, events);
                    return;
                }
                float blocked = BaseDamage(side, move, a.WindupQuality) * _cfg.GuardDamageFactor;
                d.Hp -= blocked;
                Emit(events, BattleEventKind.Blocked, side, move, blocked, false);
                CheckKo(side, events);
                return;
            }
            bool crit = _rng.NextDouble() * 100.0 < a.Stats.Crit;
            float dmg = BaseDamage(side, move, a.WindupQuality) * (crit ? _cfg.CritMultiplier : 1f);
            d.Hp -= dmg;
            Emit(events, BattleEventKind.Hit, side, move, dmg, crit);
            if (move != MoveKind.Quick && d.WindingUp)   // đòn mạnh hất tung => cắt đòn đang vung của đối thủ
            {
                d.WindingUp = false;
                Emit(events, BattleEventKind.Interrupted, 1 - side, d.WindupMove, 0f, false);
            }
            CheckKo(side, events);
        }

        private void CheckKo(int winner, List<BattleEvent> events)
        {
            Fighter loser = Fighters[1 - winner];
            if (loser.Hp > 0f) return;
            loser.Hp = 0f;
            Outcome = winner == 0 ? BattleOutcome.Win0 : BattleOutcome.Win1;
            Emit(events, BattleEventKind.KO, winner, MoveKind.Quick, 0f, false);
        }

        private void Emit(List<BattleEvent> events, BattleEventKind kind, int side, MoveKind move, float dmg, bool crit)
        {
            events?.Add(new BattleEvent { Kind = kind, Side = side, Move = move, Damage = dmg, Crit = crit, Time = Time });
        }
    }
}
