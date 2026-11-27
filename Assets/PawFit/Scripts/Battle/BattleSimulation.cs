using System;
using System.Collections.Generic;
using PawFit.Core;

namespace PawFit.Battle
{
    /// <summary>4 chiêu: ngang = Nhanh, lên = Mạnh (hất tung), V = Chiêu riêng, Z = Tuyệt chiêu (Ultimate, cần đầy Nộ).</summary>
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

        /// <summary>
        /// Máu trong trận = HP của thú × hệ số này (chỉ trong trận; chỉ số ở vườn / lực chiến không đổi).
        /// ×2,5 => 2 bên ngang sức đấu khoảng 25–30 giây: đủ dài để nối combo, tích Nộ, lật kèo, mà nhịp ra đòn vẫn nhanh.
        /// </summary>
        public float HpScale = 2.5f;

        /// <summary>Hồi chiêu sau mỗi đòn (tính từ lúc bắt đầu vung đòn).</summary>
        public float CooldownSeconds = 0.6f;

        /// <summary>Vuốt sai thì bị hở (không làm gì được) bấy nhiêu giây.</summary>
        public float WrongSwipeOpenSeconds = 0.5f;

        /// <summary>
        /// Bộ đệm vuốt: vuốt lúc còn bận (hồi chiêu / đang vung đòn / đang hở) mà chỉ còn ≤ bấy nhiêu giây là rảnh
        /// thì giữ lại, rảnh ra là làm ngay (không bị "nuốt" nét). Nét sau thay nét trước.
        /// Vuốt sai lúc đang bận thì bỏ qua (không phạt, không giữ).
        /// </summary>
        public float InputBufferSeconds = 0.35f;

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
        public float FirstMoveDelaySeconds = 0.3f;

        /// <summary>Hệ số damage + thời gian vung đòn (trước khi trúng) của 4 chiêu.</summary>
        public float[] MoveMultiplier = { 1f, 1.6f, 2.2f, 3.2f };
        public float[] WindupSeconds = { 0.15f, 0.35f, 0.45f, 0.7f };

        /// <summary>
        /// Combo: đòn trúng cách đòn trúng trước ≤ bấy nhiêu giây thì nối combo; mỗi bậc +ComboStep damage, tối đa ComboMaxSteps bậc
        /// (đòn 1 ×1, đòn 2 ×1,1 ... từ đòn 5 ×1,4). Bị trúng đòn, bị đỡ, bị phản đòn hoặc để quá giờ thì mất combo.
        /// Phản đòn cũng tính là 1 đòn trúng của combo (đánh tiếp ngay lúc đối thủ đang choáng).
        /// </summary>
        public float ComboWindowSeconds = 1.2f;
        public float ComboStep = 0.1f;
        public int ComboMaxSteps = 4;

        /// <summary>Thanh Nộ 0..RageMax: đầy mới ra được Tuyệt chiêu (vuốt Z), ra là dùng hết.</summary>
        public float RageMax = 100f;

        /// <summary>Nộ cộng thêm khi: đánh trúng / bị trúng đòn (cả khi bị phản đòn) / đỡ được / phản đòn.</summary>
        public float RageOnHit = 12f;
        public float RageOnHurt = 8f;
        public float RageOnBlock = 8f;
        public float RageOnCounter = 25f;

        /// <summary>false = Tuyệt chiêu không cần Nộ, thanh Nộ không tích (Mode Huấn luyện: học vuốt Z không phải chờ).</summary>
        public bool UltimateNeedsRage = true;

        /// <summary>Tuyệt chiêu không bị đòn Mạnh / Chiêu riêng cắt ngang (vẫn bị đỡ / phản đòn được).</summary>
        public bool UltimateArmor = true;
    }

    /// <summary>
    /// Queued = nét vuốt được giữ trong bộ đệm (sắp ra đòn); RageFull = thanh Nộ vừa đầy;
    /// RageLow = vuốt Z lúc chưa đầy Nộ (không ra đòn, không bị phạt).
    /// </summary>
    public enum BattleEventKind { AttackStarted, Hit, Blocked, Countered, Interrupted, WrongSwipe, Guard, KO, TimeUp, Queued, RageFull, RageLow }

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

        /// <summary>Hit / Countered: combo của bên gây ra sau đòn này (1 = đòn đầu, từ 2 là đang nối combo).</summary>
        public int Combo;
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

        /// <summary>Thanh Nộ 0..RageMax (đầy = vuốt Z ra Tuyệt chiêu được).</summary>
        public float Rage;

        /// <summary>Số đòn trúng liên tiếp + còn bao lâu để nối đòn kế (hết giờ = mất combo).</summary>
        public int Combo;
        public float ComboLeft;

        /// <summary>Nét vuốt đang giữ trong bộ đệm (rảnh ra là làm) + còn giữ bao lâu.</summary>
        public bool HasBuffered;
        public SwipeResult Buffered;
        public float BufferLeft;

        public float HpPercent => Stats.Hp > 0 ? Math.Max(0f, Hp) / Stats.Hp : 0f;
        public bool CanAct => Open <= 0f && !WindingUp && Hp > 0f;
    }

    /// <summary>
    /// Mô phỏng trận đấu (C# thuần, có seed => chạy lại ra đúng kết quả; máy chủ PvP chạy đúng code này):
    /// vuốt -> vung đòn -> trúng / bị đỡ / bị phản đòn; hồi chiêu 0,6s; vuốt lúc còn bận thì giữ lại (bộ đệm 0,35s);
    /// vuốt sai hở 0,5s; trúng liên tiếp thành combo (+10% mỗi bậc); Tuyệt chiêu cần đầy Nộ; hết 90 giây so %HP.
    /// Damage = ATK × hệ số chiêu × hệ số vuốt × hệ số combo × 100 / (100 + DEF); chí mạng ×1.5 (G9).
    /// </summary>
    public sealed class BattleSimulation
    {
        private readonly BattleConfig _cfg;
        private readonly IRandom _rng;
        private readonly int _tieFirst;

        /// <param name="tieFirst">
        /// 2 bên SPD bằng nhau mà đòn trúng cùng 1 bước thì bên này tính trước. Mặc định bên 0.
        /// PvP qua máy chủ: máy nào cũng đứng bên 0 trên màn hình của mình, nên máy của bên 1 (theo máy chủ) đặt 1
        /// để thứ tự tính giống hệt máy chủ.
        /// </param>
        public BattleSimulation(FighterStats a, FighterStats b, BattleConfig cfg = null, ulong seed = 1,
                                bool[] learnedA = null, bool[] learnedB = null, int tieFirst = 0)
        {
            _cfg = cfg ?? new BattleConfig();
            _rng = new SeededRandom(seed, 91UL);
            _tieFirst = tieFirst == 1 ? 1 : 0;
            Fighters = new[] { NewFighter(a, learnedA, _cfg.HpScale), NewFighter(b, learnedB, _cfg.HpScale) };
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

        private static Fighter NewFighter(FighterStats s, bool[] learned, float hpScale)
        {
            s.Hp *= hpScale > 0f ? hpScale : 1f;   // máu trong trận (Stats.Hp cũng đổi theo => %HP đúng)
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
        /// 1 bên vuốt. Trả true nếu được nhận: làm ngay, hoặc giữ trong bộ đệm nếu chỉ còn bận ≤ InputBufferSeconds.
        /// Vuốt sai lúc rảnh => hở 0,5 giây. Vuốt xuống => đỡ. Vuốt Z lúc chưa đầy Nộ => không nhận (báo RageLow).
        /// Còn lại => vung đòn.
        /// </summary>
        public bool Submit(int side, SwipeResult swipe, List<BattleEvent> events = null)
        {
            if (IsOver || side < 0 || side > 1) return false;
            Fighter f = Fighters[side];
            if (f.Hp <= 0f) return false;
            if (!swipe.IsValid)
            {
                if (!f.CanAct || f.Cooldown > 0f) return false;   // đang bận (cả lúc hồi chiêu): vuốt sai không tính (không phạt, không giữ)
                f.Open = _cfg.WrongSwipeOpenSeconds;
                f.HasBuffered = false;
                Emit(events, BattleEventKind.WrongSwipe, side, MoveKind.Quick, 0f, false);
                return true;
            }
            if (swipe.Kind == SwipeKind.Z && !UltimateReady(side))
            {
                Emit(events, BattleEventKind.RageLow, side, MoveKind.Ultimate, 0f, false);
                return false;
            }
            if (TryStart(side, swipe, events)) return true;
            if (BusyLeft(f, swipe.Kind == SwipeKind.Down) > _cfg.InputBufferSeconds + 1e-4f) return false;   // còn bận lâu: bỏ qua
            f.HasBuffered = true;   // sắp rảnh: giữ lại, nét sau thay nét trước
            f.Buffered = swipe;
            f.BufferLeft = _cfg.InputBufferSeconds + _cfg.TickSeconds;
            Emit(events, BattleEventKind.Queued, side, MoveOf(swipe.Kind) ?? MoveKind.Quick, 0f, false);
            return true;
        }

        /// <summary>Thanh Nộ của bên này đủ để ra Tuyệt chiêu chưa (Mode Huấn luyện: luôn đủ).</summary>
        public bool UltimateReady(int side) => !_cfg.UltimateNeedsRage || Fighters[side].Rage >= _cfg.RageMax - 1e-3f;

        /// <summary>Còn bao lâu nữa thì bên này ra đòn được (0 = ngay). View vẽ vạch hồi chiêu theo đây.</summary>
        public float AttackReadyIn(int side) => BusyLeft(Fighters[side], false);

        /// <summary>Còn bận bao lâu nữa mới đánh được (guard = false) / đỡ được (guard = true).</summary>
        private static float BusyLeft(Fighter f, bool guard)
        {
            float busy = Math.Max(f.Open, f.WindingUp ? Math.Max(0f, f.WindupLeft) : 0f);
            return Math.Max(busy, guard ? f.GuardCooldown : f.Cooldown);
        }

        /// <summary>Làm ngay 1 nét vuốt hợp lệ nếu đang rảnh. Trả false nếu còn bận.</summary>
        private bool TryStart(int side, SwipeResult swipe, List<BattleEvent> events)
        {
            Fighter f = Fighters[side];
            if (!f.CanAct) return false;
            if (swipe.Kind == SwipeKind.Down)   // đỡ: được cả khi đang hồi chiêu đánh, chỉ không được lúc đang vung đòn
            {
                if (f.GuardCooldown > 0f) return false;
                f.GuardLeft = _cfg.GuardSeconds;
                f.GuardAge = 0f;
                f.GuardCooldown = _cfg.GuardCooldownSeconds;
                f.HasBuffered = false;
                Emit(events, BattleEventKind.Guard, side, MoveKind.Quick, 0f, false);
                return true;
            }
            if (f.Cooldown > 0f) return false;   // vừa ra đòn: chưa đánh tiếp được
            MoveKind move = MoveOf(swipe.Kind).Value;
            if (move == MoveKind.Ultimate)
            {
                if (!UltimateReady(side)) return false;
                if (_cfg.UltimateNeedsRage) f.Rage = 0f;   // ra Tuyệt chiêu là dùng hết Nộ
            }
            f.WindingUp = true;
            f.WindupMove = move;
            f.WindupQuality = Math.Max(0.7f, Math.Min(1.3f, swipe.Quality));
            f.WindupLeft = _cfg.WindupSeconds[(int)move];
            f.Cooldown = _cfg.CooldownSeconds;
            f.GuardLeft = 0f;
            f.HasBuffered = false;
            Emit(events, BattleEventKind.AttackStarted, side, move, 0f, false);
            return true;
        }

        /// <summary>Chạy 1 bước cố định. Sự kiện trong bước được thêm vào `events`.</summary>
        public void Tick(List<BattleEvent> events = null)
        {
            if (IsOver) return;
            float dt = _cfg.TickSeconds;
            // nét đang giữ: thử lại ngay trước bước, như thể người chơi vừa vuốt (mỗi bên chỉ đổi trạng thái của mình => thứ tự không ảnh hưởng)
            for (int side = 0; side < 2; side++)
            {
                Fighter f = Fighters[side];
                if (!f.HasBuffered || TryStart(side, f.Buffered, events)) continue;
                if ((f.BufferLeft -= dt) <= 0f) f.HasBuffered = false;
            }
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
                if (f.ComboLeft > 0f && (f.ComboLeft -= dt) <= 0f) BreakCombo(f);   // quá giờ nối đòn
            }
            // đòn trúng cùng lúc: bên SPD cao hơn tính trước (có thể hạ gục trước khi đòn kia kịp trúng)
            float s0 = Fighters[0].Stats.Spd, s1 = Fighters[1].Stats.Spd;
            int first = s1 > s0 ? 1 : s0 > s1 ? 0 : _tieFirst;
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

        /// <summary>Hệ số damage theo combo: đòn thứ n của combo được +ComboStep × (n − 1), tối đa ComboMaxSteps bậc.</summary>
        public float ComboFactor(int combo) => 1f + _cfg.ComboStep * Math.Min(Math.Max(0, combo - 1), Math.Max(0, _cfg.ComboMaxSteps));

        private void Land(int side, List<BattleEvent> events)
        {
            Fighter a = Fighters[side], d = Fighters[1 - side];
            a.WindingUp = false;
            MoveKind move = a.WindupMove;
            if (d.GuardLeft > 0f && d.Open <= 0f)
            {
                BreakCombo(a);   // bị đỡ / bị phản đòn: mất combo
                if (d.GuardAge <= _cfg.ParryWindowSeconds + 1e-4f)   // đỡ đúng lúc => phản đòn
                {
                    float counter = d.Stats.Atk * _cfg.CounterAtkFactor * 100f / (100f + Math.Max(0f, a.Stats.Def));
                    a.Hp -= counter;
                    a.Open = _cfg.CounterStunSeconds;
                    a.HasBuffered = false;   // bị choáng: bỏ nét đang giữ
                    d.GuardLeft = 0f;
                    int chain = NextCombo(d);   // phản đòn = 1 đòn trúng => đánh tiếp ngay là nối combo
                    Emit(events, BattleEventKind.Countered, 1 - side, move, counter, false, chain);
                    AddRage(1 - side, _cfg.RageOnCounter, events);
                    AddRage(side, _cfg.RageOnHurt, events);
                    CheckKo(1 - side, events);
                    return;
                }
                float blocked = BaseDamage(side, move, a.WindupQuality) * _cfg.GuardDamageFactor;
                d.Hp -= blocked;
                Emit(events, BattleEventKind.Blocked, side, move, blocked, false);
                AddRage(1 - side, _cfg.RageOnBlock, events);
                CheckKo(side, events);
                return;
            }
            bool crit = _rng.NextDouble() * 100.0 < a.Stats.Crit;
            int combo = NextCombo(a);
            float dmg = BaseDamage(side, move, a.WindupQuality) * ComboFactor(combo) * (crit ? _cfg.CritMultiplier : 1f);
            d.Hp -= dmg;
            BreakCombo(d);   // bị trúng đòn: mất combo
            Emit(events, BattleEventKind.Hit, side, move, dmg, crit, combo);
            AddRage(side, _cfg.RageOnHit, events);
            AddRage(1 - side, _cfg.RageOnHurt, events);
            bool armored = _cfg.UltimateArmor && d.WindupMove == MoveKind.Ultimate;
            if (move != MoveKind.Quick && d.WindingUp && !armored)   // đòn mạnh hất tung => cắt đòn đang vung của đối thủ (trừ Tuyệt chiêu)
            {
                d.WindingUp = false;
                Emit(events, BattleEventKind.Interrupted, 1 - side, d.WindupMove, 0f, false);
            }
            CheckKo(side, events);
        }

        /// <summary>Thêm 1 đòn vào combo của `f` (hoặc bắt đầu combo mới nếu đã quá giờ). Trả số đòn của combo.</summary>
        private int NextCombo(Fighter f)
        {
            f.Combo = f.ComboLeft > 0f ? f.Combo + 1 : 1;
            f.ComboLeft = _cfg.ComboWindowSeconds;
            return f.Combo;
        }

        private static void BreakCombo(Fighter f)
        {
            f.Combo = 0;
            f.ComboLeft = 0f;
        }

        private void AddRage(int side, float amount, List<BattleEvent> events)
        {
            if (!_cfg.UltimateNeedsRage || amount <= 0f) return;
            Fighter f = Fighters[side];
            bool wasFull = f.Rage >= _cfg.RageMax - 1e-3f;
            f.Rage = Math.Min(_cfg.RageMax, f.Rage + amount);
            if (!wasFull && f.Rage >= _cfg.RageMax - 1e-3f) Emit(events, BattleEventKind.RageFull, side, MoveKind.Ultimate, 0f, false);
        }

        private void CheckKo(int winner, List<BattleEvent> events)
        {
            Fighter loser = Fighters[1 - winner];
            if (loser.Hp > 0f) return;
            loser.Hp = 0f;
            Outcome = winner == 0 ? BattleOutcome.Win0 : BattleOutcome.Win1;
            Emit(events, BattleEventKind.KO, winner, MoveKind.Quick, 0f, false);
        }

        private void Emit(List<BattleEvent> events, BattleEventKind kind, int side, MoveKind move, float dmg, bool crit, int combo = 0)
        {
            events?.Add(new BattleEvent { Kind = kind, Side = side, Move = move, Damage = dmg, Crit = crit, Time = Time, Combo = combo });
        }
    }
}
