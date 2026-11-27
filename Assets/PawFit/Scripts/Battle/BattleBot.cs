using System;
using PawFit.Core;

namespace PawFit.Battle
{
    /// <summary>3 mức Bot = 3 hình nộm ở sân tập (GDD G11).</summary>
    public enum BotLevel { Easy = 0, Normal = 1, Hard = 2 }

    /// <summary>Lối đánh của người chơi ảo (PvP giả lập): Cân bằng / Hăng (đánh dồn, ít đỡ) / Thủ (hay đỡ, canh phản đòn) / Kỹ thuật (hay ra chiêu riêng).</summary>
    public enum BotStyle { Balanced = 0, Aggressive = 1, Defensive = 2, Technical = 3 }

    /// <summary>Số liệu Bot theo mức (chỉ số mảng = BotLevel).</summary>
    public sealed class BotConfig
    {
        /// <summary>Thấy đối thủ vung đòn thì bao lâu mới phản ứng (giây).</summary>
        public float[] ReactionSeconds = { 0.45f, 0.3f, 0.16f };

        /// <summary>Xác suất chịu đỡ khi thấy đòn tới.</summary>
        public float[] GuardChance = { 0.15f, 0.4f, 0.7f };

        /// <summary>Nghỉ bao lâu giữa 2 đòn (khoảng ngẫu nhiên, giây).</summary>
        public float[] AttackGapMin = { 1.2f, 0.7f, 0.35f };
        public float[] AttackGapMax = { 2.4f, 1.4f, 0.8f };

        /// <summary>Hệ số vuốt của Bot (khoảng ngẫu nhiên).</summary>
        public float[] QualityMin = { 0.75f, 0.85f, 0.95f };
        public float[] QualityMax = { 0.95f, 1.1f, 1.25f };

        /// <summary>Xác suất Bot vuốt sai (bị hở 1 giây).</summary>
        public float[] WrongChance = { 0.1f, 0.05f, 0.02f };

        /// <summary>Trọng số chọn chiêu Nhanh / Mạnh / Riêng / Ultimate.</summary>
        public float[][] MoveWeights =
        {
            new[] { 0.75f, 0.25f, 0f, 0f },
            new[] { 0.55f, 0.3f, 0.15f, 0f },
            new[] { 0.45f, 0.3f, 0.17f, 0.08f },
        };

        /// <summary>Có canh đỡ sát đòn để phản đòn không (mặc định chỉ mức Khó).</summary>
        public bool[] AimParry = { false, false, true };

        /// <summary>
        /// Cấu hình cho 1 người chơi ảo (PvP giả lập): tay nghề 0..1 nội suy giữa Dễ (0) / Thường (0.5) / Khó (1),
        /// rồi chỉnh theo lối đánh. Mọi ô theo mức đều cùng giá trị nên dùng với BotLevel nào cũng được.
        /// </summary>
        public static BotConfig Persona(float skill, BotStyle style)
        {
            var d = new BotConfig();
            float k = skill < 0f ? 0f : (skill > 1f ? 1f : skill);
            float P(float[] a) => k < 0.5f ? a[0] + (a[1] - a[0]) * (k * 2f) : a[1] + (a[2] - a[1]) * ((k - 0.5f) * 2f);
            float react = P(d.ReactionSeconds), guard = P(d.GuardChance), gapMin = P(d.AttackGapMin), gapMax = P(d.AttackGapMax);
            float qMin = P(d.QualityMin), qMax = P(d.QualityMax), wrong = P(d.WrongChance);
            var w = new float[4];
            for (int i = 0; i < 4; i++) w[i] = P(new[] { d.MoveWeights[0][i], d.MoveWeights[1][i], d.MoveWeights[2][i] });
            bool aim = k > 0.6f;
            switch (style)
            {
                case BotStyle.Aggressive:   // đánh dồn, ít đỡ, hay đánh mạnh
                    gapMin *= 0.75f;
                    gapMax *= 0.75f;
                    guard *= 0.6f;
                    w[1] += 0.15f;
                    break;
                case BotStyle.Defensive:    // đánh thưa, hay đỡ, sớm biết canh phản đòn
                    gapMin *= 1.3f;
                    gapMax *= 1.3f;
                    guard = Math.Min(0.9f, guard * 1.35f);
                    aim = k > 0.35f;
                    break;
                case BotStyle.Technical:    // hay ra chiêu riêng / Ultimate, vuốt đẹp hơn
                    w[2] += 0.12f;
                    if (k > 0.5f) w[3] += 0.06f;
                    qMin += 0.05f;
                    qMax += 0.05f;
                    break;
            }
            return new BotConfig
            {
                ReactionSeconds = new[] { react, react, react },
                GuardChance = new[] { guard, guard, guard },
                AttackGapMin = new[] { gapMin, gapMin, gapMin },
                AttackGapMax = new[] { gapMax, gapMax, gapMax },
                QualityMin = new[] { qMin, qMin, qMin },
                QualityMax = new[] { qMax, qMax, qMax },
                WrongChance = new[] { wrong, wrong, wrong },
                MoveWeights = new[] { w, w, w },
                AimParry = new[] { aim, aim, aim },
            };
        }
    }

    /// <summary>
    /// Bot đấu (C# thuần, có seed): dùng đúng cổng Submit như người chơi nên không ăn gian.
    /// Mức càng cao: phản ứng nhanh hơn, hay đỡ hơn (mức Khó canh để phản đòn), đánh dồn hơn, vuốt đẹp hơn.
    /// </summary>
    public sealed class BattleBot
    {
        private readonly int _side;
        private readonly int _lv;
        private readonly BotConfig _cfg;
        private readonly IRandom _rng;
        private float _attackIn;
        private float _guardIn = -1f;
        private bool _sawWindup;

        public BattleBot(int side, BotLevel level, ulong seed, BotConfig cfg = null)
        {
            _side = side;
            _lv = (int)level;
            _cfg = cfg ?? new BotConfig();
            _rng = new SeededRandom(seed, 13UL + (ulong)side);
            _attackIn = Gap();
        }

        public BotLevel Level => (BotLevel)_lv;

        /// <summary>Gọi mỗi bước mô phỏng (trước Tick). Trả nét vuốt Bot muốn làm, hoặc null.</summary>
        public SwipeResult? Think(BattleSimulation sim)
        {
            if (sim.IsOver) return null;
            float dt = sim.Config.TickSeconds;
            Fighter me = sim.Fighters[_side], foe = sim.Fighters[1 - _side];

            // thấy đối thủ bắt đầu vung đòn: quyết định có đỡ không, đỡ lúc nào
            if (foe.WindingUp && !_sawWindup)
            {
                _sawWindup = true;
                if (_rng.NextDouble() < _cfg.GuardChance[_lv])
                {
                    float react = _cfg.ReactionSeconds[_lv];
                    // Bot biết canh (mức Khó / người chơi ảo giỏi) thì đỡ sát đòn để phản đòn; còn lại đỡ ngay khi kịp phản ứng
                    float aim = _cfg.AimParry[_lv] ? foe.WindupLeft - sim.Config.ParryWindowSeconds * 0.5f : react;
                    _guardIn = Math.Max(react, aim);
                    if (_guardIn >= foe.WindupLeft) _guardIn = -1f;   // không kịp đỡ
                }
            }
            if (!foe.WindingUp) _sawWindup = false;

            if (_guardIn >= 0f)
            {
                _guardIn -= dt;
                if (_guardIn <= 0f)
                {
                    _guardIn = -1f;
                    if (me.CanAct && me.GuardCooldown <= 0f) return new SwipeResult { Kind = SwipeKind.Down, Quality = 1f };
                }
                return null;   // đang canh đỡ thì không ra đòn
            }

            _attackIn -= dt;
            if (_attackIn > 0f || !me.CanAct || me.Cooldown > 0f) return null;
            _attackIn = Gap();
            if (_rng.NextDouble() < _cfg.WrongChance[_lv]) return SwipeResult.Wrong("bot vuốt sai");
            float q = Lerp(_cfg.QualityMin[_lv], _cfg.QualityMax[_lv], (float)_rng.NextDouble());
            return new SwipeResult { Kind = PickMove(), Quality = q, Direction = _side == 0 ? 1 : -1 };
        }

        private SwipeKind PickMove()
        {
            float[] w = _cfg.MoveWeights[_lv];
            float total = 0f;
            foreach (float x in w) total += x;
            float r = (float)_rng.NextDouble() * total;
            for (int i = 0; i < w.Length; i++)
            {
                if (r < w[i])
                    return i == 0 ? SwipeKind.Horizontal : i == 1 ? SwipeKind.Up : i == 2 ? SwipeKind.V : SwipeKind.Z;
                r -= w[i];
            }
            return SwipeKind.Horizontal;
        }

        private float Gap() => Lerp(_cfg.AttackGapMin[_lv], _cfg.AttackGapMax[_lv], (float)_rng.NextDouble());

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        /// <summary>Đấu Bot với Bot tới hết trận (để test cân bằng). Trả kết quả.</summary>
        public static BattleOutcome Simulate(BattleSimulation sim, BattleBot a, BattleBot b)
        {
            while (!sim.IsOver)
            {
                SwipeResult? sa = a.Think(sim);
                if (sa.HasValue) sim.Submit(a._side, sa.Value);
                SwipeResult? sb = b.Think(sim);
                if (sb.HasValue) sim.Submit(b._side, sb.Value);
                sim.Tick();
            }
            return sim.Outcome;
        }
    }
}
