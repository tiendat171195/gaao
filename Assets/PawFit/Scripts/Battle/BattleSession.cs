using System;
using System.Collections.Generic;
using PawFit.Pet;

namespace PawFit.Battle
{
    /// <summary>Huấn luyện = học chiêu với hình nộm (không thua, vuốt sai không bị phạt); Đấu Bot = trận thật 90 giây.</summary>
    public enum BattleMode { Training = 0, BotFight = 1 }

    public sealed class BattleSessionConfig
    {
        /// <summary>Mode Huấn luyện chạy chậm hơn thường; khi hình nộm đang vung đòn thì chậm hẳn (slow-mo) để kịp đỡ.</summary>
        public float TrainingTimeScale = 0.75f;
        public float SlowMoTimeScale = 0.3f;
        public float TrainingDurationSeconds = 600f;

        /// <summary>Huấn luyện: 2 bên rất nhiều máu (không ai gục), hình nộm đánh nhẹ.</summary>
        public float TrainingHp = 100000f;
        public float TrainingBotAtkFactor = 0.5f;

        /// <summary>Hình nộm Dễ / Thường / Khó: chỉ số = chỉ số thú × hệ số này.</summary>
        public float[] BotStatFactor = { 0.7f, 1f, 1.25f };

        // ---- điểm tập mỗi đòn trúng của thú ----
        public float QuickSpeed = 1f;
        public float HeavyStrength = 1.5f;
        public float SpecialStrength = 1f;
        public float SpecialSpeed = 1f;
        public float UltimateStrength = 2f;
        public float UltimateSpeed = 1.5f;
        public float CounterSpeed = 1.5f;

        /// <summary>Thắng hình nộm Dễ / Thường / Khó được thêm điểm.</summary>
        public float[] WinStrength = { 3f, 5f, 8f };
        public float[] WinSpeed = { 2f, 3f, 5f };

        /// <summary>Đấu / tập tốn sức (trừ lúc kết thúc).</summary>
        public float TrainingEnergyCost = 6f;
        public float TrainingSatietyCost = 4f;
        public float FightEnergyCost = 10f;
        public float FightSatietyCost = 6f;
    }

    public enum SessionNoteKind { Practiced, Learned, AllLearned, Retry, GuardHint, Finished }

    /// <summary>Ghi chú của phiên (View hiện chữ / hiệu ứng): đã tập chiêu, vừa thuộc chiêu, vuốt lại, gợi ý đỡ...</summary>
    public struct SessionNote
    {
        public SessionNoteKind Kind;

        /// <summary>Chiêu liên quan (0 = ngang, 1 = lên, 2 = V, 3 = Z).</summary>
        public int Skill;

        public int Progress;
        public string Text;
    }

    public sealed class BattleResult
    {
        public BattleOutcome Outcome;
        public bool PlayerWon;
        public TrainingVector Gained = new TrainingVector();
        public readonly List<int> Learned = new List<int>();
    }

    /// <summary>
    /// 1 phiên đấu ở sân tập (C# thuần): nối BattleSimulation + BattleBot với dữ liệu thú.
    /// Chỉ số lấy từ EvolutionService (đã tính bệnh), chiêu đã thuộc lấy từ tiến độ học, điểm tập cộng theo đòn trúng.
    /// Mode Huấn luyện (GDD): bóng mờ gesture (View vẽ theo Lesson), slow-mo khi hình nộm vung đòn,
    /// vuốt sai thì cho vuốt lại, vuốt đúng 10 lần thì thuộc chiêu.
    /// </summary>
    public sealed class BattleSession
    {
        public const int PlayerSide = 0;
        public const int BotSide = 1;

        private readonly PetService _pets;
        private readonly EvolutionService _evo;
        private readonly BattleSessionConfig _sc;
        private readonly BattleBot _bot;
        private float _accum;
        private bool _quit;
        private bool _finished;
        private bool _hintedGuard;

        /// <summary>Có vào sân tập được không (cần thú còn sống, không ốm — GDD G3).</summary>
        public static bool CanStart(PetService pets, HealthService health, out string reason)
        {
            reason = null;
            if (pets == null || !pets.HasPet) reason = "không có thú";
            else if (!health.CanTrain) reason = "đang ốm, phải chữa khỏi mới tập / đấu được";
            else if (pets.Current.IsSleeping) reason = "đang ngủ";
            return reason == null;
        }

        public BattleSession(BattleMode mode, BotLevel level, PetService pets, EvolutionService evo, ulong seed,
                             BattleConfig battleCfg = null, BattleSessionConfig sessionCfg = null, BotConfig botCfg = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _sc = sessionCfg ?? new BattleSessionConfig();
            Mode = mode;
            Level = level;
            PetStats ps = evo.Stats;
            var me = new FighterStats(ps.Hp, ps.Atk, ps.Def, ps.Spd, ps.Crit);
            float k = _sc.BotStatFactor[Math.Max(0, Math.Min(_sc.BotStatFactor.Length - 1, (int)level))];
            var bot = new FighterStats(ps.Hp * k, ps.Atk * k, ps.Def * k, ps.Spd * k, ps.Crit);
            BattleConfig bc = battleCfg ?? new BattleConfig();
            if (mode == BattleMode.Training)
            {
                bc.DurationSeconds = _sc.TrainingDurationSeconds;
                me.Hp = bot.Hp = _sc.TrainingHp;
                bot.Atk *= _sc.TrainingBotAtkFactor;
            }
            Sim = new BattleSimulation(me, bot, bc, seed, evo.LearnedMask(), null);
            _bot = new BattleBot(BotSide, mode == BattleMode.Training ? BotLevel.Easy : level, seed * 7 + 3, botCfg);
            Result = new BattleResult();
        }

        public BattleMode Mode { get; }
        public BotLevel Level { get; }
        public BattleSimulation Sim { get; }
        public BattleResult Result { get; }
        public bool IsOver => _finished;

        /// <summary>Huấn luyện: chiêu đang học (chiêu chưa thuộc đầu tiên), -1 nếu đã thuộc cả 4.</summary>
        public int Lesson
        {
            get
            {
                if (Mode != BattleMode.Training) return -1;
                for (int i = 0; i < EvolutionService.SkillCount; i++)
                    if (!_evo.IsLearned(i)) return i;
                return -1;
            }
        }

        public int LessonProgress => Lesson >= 0 ? _evo.SkillProgress(Lesson) : 0;

        /// <summary>Huấn luyện: hình nộm đang vung đòn => chạy chậm để người chơi kịp vuốt xuống đỡ.</summary>
        public bool SlowMo => Mode == BattleMode.Training && Sim.Fighters[BotSide].WindingUp;

        public float TimeScale => Mode != BattleMode.Training ? 1f : (SlowMo ? _sc.SlowMoTimeScale : _sc.TrainingTimeScale);

        public static SwipeKind SwipeOf(int skill) =>
            skill == 0 ? SwipeKind.Horizontal : skill == 1 ? SwipeKind.Up : skill == 2 ? SwipeKind.V : SwipeKind.Z;

        public static int SkillOf(SwipeKind k) =>
            k == SwipeKind.Horizontal ? 0 : k == SwipeKind.Up ? 1 : k == SwipeKind.V ? 2 : k == SwipeKind.Z ? 3 : -1;

        /// <summary>Người chơi vuốt xong 1 nét. Trả true nếu trận nhận nét đó.</summary>
        public bool PlayerSwipe(SwipeResult swipe, List<BattleEvent> events = null, List<SessionNote> notes = null)
        {
            if (_finished) return false;
            if (Mode == BattleMode.Training && !swipe.IsValid)   // huấn luyện: vuốt sai không bị hở, cho vuốt lại
            {
                notes?.Add(new SessionNote { Kind = SessionNoteKind.Retry, Skill = Lesson, Text = swipe.Reason });
                return false;
            }
            int lesson = Lesson;
            bool ok = Sim.Submit(PlayerSide, swipe, events);
            if (ok && Mode == BattleMode.Training && lesson >= 0 && SkillOf(swipe.Kind) == lesson)
            {
                bool learned = _evo.PracticeSkill(lesson);
                notes?.Add(new SessionNote { Kind = SessionNoteKind.Practiced, Skill = lesson, Progress = _evo.SkillProgress(lesson) });
                if (learned)
                {
                    Sim.Fighters[PlayerSide].Learned[lesson] = true;
                    Result.Learned.Add(lesson);
                    notes?.Add(new SessionNote { Kind = SessionNoteKind.Learned, Skill = lesson, Progress = _evo.SkillProgress(lesson) });
                    if (Lesson < 0) notes?.Add(new SessionNote { Kind = SessionNoteKind.AllLearned, Skill = -1 });
                }
            }
            return ok;
        }

        /// <summary>Chạy phiên theo thời gian thật (View gọi mỗi frame). Mô phỏng vẫn theo bước cố định bên trong.</summary>
        public void Advance(float realSeconds, List<BattleEvent> events = null, List<SessionNote> notes = null)
        {
            if (_finished) return;
            _accum += Math.Max(0f, realSeconds) * TimeScale;
            float tick = Sim.Config.TickSeconds;
            var local = new List<BattleEvent>();
            while (_accum >= tick && !Sim.IsOver)
            {
                _accum -= tick;
                SwipeResult? s = _bot.Think(Sim);
                if (s.HasValue) Sim.Submit(BotSide, s.Value, local);
                Sim.Tick(local);
                if (Mode == BattleMode.Training && Sim.Fighters[BotSide].WindingUp && !_hintedGuard)
                {
                    _hintedGuard = true;   // gợi ý đỡ lần đầu thấy hình nộm vung đòn
                    notes?.Add(new SessionNote { Kind = SessionNoteKind.GuardHint, Skill = -1 });
                }
            }
            foreach (BattleEvent e in local)
            {
                Reward(e);
                events?.Add(e);
            }
            if (Sim.IsOver) Finish(notes);
        }

        /// <summary>Rời sân (Huấn luyện: lúc nào cũng được; Đấu: bỏ cuộc = thua).</summary>
        public void Quit(List<SessionNote> notes = null)
        {
            if (_finished) return;
            _quit = true;
            Finish(notes);
        }

        private void Reward(BattleEvent e)
        {
            if (e.Kind == BattleEventKind.Countered && e.Side == PlayerSide) Gain(TrainingAxis.Speed, _sc.CounterSpeed);
            if (e.Kind != BattleEventKind.Hit || e.Side != PlayerSide) return;
            switch (e.Move)
            {
                case MoveKind.Quick: Gain(TrainingAxis.Speed, _sc.QuickSpeed); break;
                case MoveKind.Heavy: Gain(TrainingAxis.Strength, _sc.HeavyStrength); break;
                case MoveKind.Special:
                    Gain(TrainingAxis.Strength, _sc.SpecialStrength);
                    Gain(TrainingAxis.Speed, _sc.SpecialSpeed);
                    break;
                default:
                    Gain(TrainingAxis.Strength, _sc.UltimateStrength);
                    Gain(TrainingAxis.Speed, _sc.UltimateSpeed);
                    break;
            }
        }

        private void Gain(TrainingAxis axis, float points) => Result.Gained.Add(axis, _evo.AddTraining(axis, points));

        private void Finish(List<SessionNote> notes)
        {
            if (_finished) return;
            _finished = true;
            Result.Outcome = _quit && Mode == BattleMode.BotFight ? BattleOutcome.Win1 : Sim.Outcome;
            Result.PlayerWon = Result.Outcome == BattleOutcome.Win0;
            if (Mode == BattleMode.BotFight && Result.PlayerWon)
            {
                int lv = Math.Max(0, Math.Min(_sc.WinStrength.Length - 1, (int)Level));
                Gain(TrainingAxis.Strength, _sc.WinStrength[lv]);
                Gain(TrainingAxis.Speed, _sc.WinSpeed[lv]);
            }
            if (_pets.HasPet)
            {
                bool training = Mode == BattleMode.Training;
                _pets.Spend(NeedType.Energy, training ? _sc.TrainingEnergyCost : _sc.FightEnergyCost);
                _pets.Spend(NeedType.Satiety, training ? _sc.TrainingSatietyCost : _sc.FightSatietyCost);
            }
            notes?.Add(new SessionNote { Kind = SessionNoteKind.Finished, Skill = -1 });
        }
    }
}
