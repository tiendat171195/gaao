using System;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Garden
{
    /// <summary>Thú "nói" nhu cầu bằng hình (vườn không có thanh số): đói, bẩn, buồn ngủ, đang ngủ.</summary>
    [Flags]
    public enum PetCondition
    {
        None = 0,
        Hungry = 1,
        Dirty = 2,
        Sleepy = 4,
        Sleeping = 8,
    }

    /// <summary>Hành động biểu cảm ngắn View cần chạy đúng 1 lần.</summary>
    public enum ExpressionCue { None, LookAtFood, Cry, Yawn }

    /// <summary>Trạng thái hình hiện tại. View đọc mỗi frame.</summary>
    public struct PetExpression
    {
        public PetCondition Conditions;

        /// <summary>Độ mở mắt: 1 = mở to, 0 = nhắm.</summary>
        public float EyeOpen;

        /// <summary>Độ lấm lem của lông: 0 = sạch, 1 = rất bẩn.</summary>
        public float Dirt;

        /// <summary>Số ruồi bay quanh thú.</summary>
        public int Flies;

        /// <summary>Đang quay đầu nhìn về chòi ăn.</summary>
        public bool LookAtFood;

        /// <summary>Tai / đuôi cụp xuống: 0 = bình thường, 1 = cụp hẳn (ngủ).</summary>
        public float Droop;

        public bool Has(PetCondition c) => (Conditions & c) != 0;
    }

    /// <summary>Ngưỡng + nhịp của biểu cảm (chỉnh số ở đây, không phải sửa code).</summary>
    public sealed class ExpressionConfig
    {
        /// <summary>No dưới mức này là đói (trùng ngưỡng bộ não dùng để tự tới chòi ăn).</summary>
        public float HungryBelowSatiety = 35f;

        public float DirtyBelowHygiene = 40f;
        public float VeryDirtyBelowHygiene = 15f;
        public int FliesDirty = 2;
        public int FliesVeryDirty = 4;

        /// <summary>Năng lượng dưới mức này thì mắt díp. Cao hơn ngưỡng đi ngủ (20) để người chơi thấy trước.</summary>
        public float SleepyBelowEnergy = 30f;

        /// <summary>Độ mở mắt khi vừa buồn ngủ / buồn ngủ nhất.</summary>
        public float SleepyEyeOpenMax = 0.72f;
        public float SleepyEyeOpenMin = 0.42f;

        // ---- nhịp kêu khi đói ----
        public float FirstCryDelaySeconds = 2f;
        public float CryIntervalSeconds = 9f;

        /// <summary>Mỗi lần đòi ăn: quay đầu nhìn chòi ăn bấy nhiêu giây...</summary>
        public float LookAtFoodSeconds = 2.2f;

        /// <summary>...và kêu sau khi bắt đầu nhìn bấy nhiêu giây.</summary>
        public float CryAfterLookSeconds = 0.8f;

        // ---- ngáp khi buồn ngủ ----
        public float FirstYawnDelaySeconds = 3f;
        public float YawnIntervalSeconds = 11f;

        /// <summary>Nhịp kêu / ngáp lệch ngẫu nhiên ±30% cho tự nhiên.</summary>
        public float IntervalJitter = 0.3f;
    }

    /// <summary>
    /// Biểu cảm của thú (C# thuần): từ nhu cầu + việc thú đang làm suy ra mắt mở bao nhiêu,
    /// bẩn bao nhiêu, mấy con ruồi, lúc nào nhìn chòi ăn + kêu, lúc nào ngáp.
    /// View gọi Update mỗi frame rồi vẽ theo <see cref="Current"/> và chạy cue trả về.
    /// </summary>
    public sealed class PetExpressionService
    {
        private readonly PetService _pets;
        private readonly PetBrain _brain;
        private readonly ExpressionConfig _cfg;
        private readonly IRandom _rng;
        private float _cryTimer;
        private float _lookTimer;
        private float _yawnTimer;

        public PetExpressionService(PetService pets, PetBrain brain, ExpressionConfig cfg, IRandom rng)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _brain = brain ?? throw new ArgumentNullException(nameof(brain));
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _cryTimer = cfg.FirstCryDelaySeconds;
            _yawnTimer = cfg.FirstYawnDelaySeconds;
            Current = new PetExpression { EyeOpen = 1f };
        }

        public PetExpression Current { get; private set; }

        public ExpressionCue Update(float dt)
        {
            if (dt < 0f) dt = 0f;
            PetData pet = _pets.Current;
            PetNeeds n = pet.Needs;
            bool sleeping = pet.IsSleeping;
            var e = new PetExpression { EyeOpen = 1f };

            if (sleeping) e.Conditions |= PetCondition.Sleeping;
            else
            {
                if (n.Satiety < _cfg.HungryBelowSatiety) e.Conditions |= PetCondition.Hungry;
                if (n.Energy < _cfg.SleepyBelowEnergy) e.Conditions |= PetCondition.Sleepy;
            }
            if (n.Hygiene < _cfg.DirtyBelowHygiene) e.Conditions |= PetCondition.Dirty;

            // mắt + tai / đuôi
            if (sleeping)
            {
                e.EyeOpen = 0f;
                e.Droop = 1f;
            }
            else if (e.Has(PetCondition.Sleepy))
            {
                float t = Clamp01((_cfg.SleepyBelowEnergy - n.Energy) / _cfg.SleepyBelowEnergy);
                e.EyeOpen = Lerp(_cfg.SleepyEyeOpenMax, _cfg.SleepyEyeOpenMin, t);
                e.Droop = Lerp(0.35f, 0.8f, t);
            }

            // bẩn: lông lấm lem + ruồi
            if (e.Has(PetCondition.Dirty))
            {
                float t = Clamp01((_cfg.DirtyBelowHygiene - n.Hygiene) / _cfg.DirtyBelowHygiene);
                e.Dirt = Lerp(0.3f, 1f, t);
                e.Flies = n.Hygiene < _cfg.VeryDirtyBelowHygiene ? _cfg.FliesVeryDirty : _cfg.FliesDirty;
            }

            PetActivity act = _brain.Activity;
            bool free = act == PetActivity.Idle || act == PetActivity.Wander ||
                        act == PetActivity.MoveTo || act == PetActivity.GoingToZone;   // rảnh mới kêu / ngáp
            ExpressionCue cue = ExpressionCue.None;

            // đói: quay đầu nhìn chòi ăn, kêu 1 tiếng, lặp lại theo nhịp
            if (e.Has(PetCondition.Hungry) && free)
            {
                if (_lookTimer > 0f)
                {
                    float before = _lookTimer;
                    _lookTimer -= dt;
                    float cryAt = _cfg.LookAtFoodSeconds - _cfg.CryAfterLookSeconds;
                    if (before > cryAt && _lookTimer <= cryAt) cue = ExpressionCue.Cry;
                    if (_lookTimer > 0f) e.LookAtFood = true;
                    else _cryTimer = Jitter(_cfg.CryIntervalSeconds);
                }
                else
                {
                    _cryTimer -= dt;
                    if (_cryTimer <= 0f)
                    {
                        _lookTimer = _cfg.LookAtFoodSeconds;
                        e.LookAtFood = true;
                        cue = ExpressionCue.LookAtFood;
                    }
                }
            }
            else
            {
                _lookTimer = 0f;
                if (!e.Has(PetCondition.Hungry)) _cryTimer = _cfg.FirstCryDelaySeconds;
            }

            // buồn ngủ: thỉnh thoảng ngáp (không ngáp chồng lên lúc đang đòi ăn)
            if (e.Has(PetCondition.Sleepy) && free && !e.LookAtFood && cue == ExpressionCue.None)
            {
                _yawnTimer -= dt;
                if (_yawnTimer <= 0f)
                {
                    cue = ExpressionCue.Yawn;
                    _yawnTimer = Jitter(_cfg.YawnIntervalSeconds);
                }
            }
            else if (!e.Has(PetCondition.Sleepy)) _yawnTimer = _cfg.FirstYawnDelaySeconds;

            Current = e;
            return cue;
        }

        private float Jitter(float seconds) =>
            seconds * (1f + (float)(_rng.NextDouble() * 2.0 - 1.0) * _cfg.IntervalJitter);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
