using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Battle
{
    /// <summary>
    /// Huấn luyện = học chiêu với hình nộm (không thua, vuốt sai không bị phạt); Đấu Bot = trận thật 90 giây với hình nộm;
    /// Ranked = PvP 90 giây với người chơi khác (hoặc người chơi ảo khi vắng người), tính Elo;
    /// Thám hiểm = đấu sinh vật hoang ở các vùng (Phase 7.2), sao + Coin do AdventureService tính sau trận.
    /// </summary>
    public enum BattleMode { Training = 0, BotFight = 1, Ranked = 2, Adventure = 3 }

    /// <summary>Đối thủ PvP cho trước: chỉ số + chiêu đã thuộc của thú họ; Bot != null = máy điều khiển (người chơi ảo).</summary>
    public sealed class RankedOpponent
    {
        public string Name;
        public FighterStats Stats;
        public bool[] Learned;

        /// <summary>
        /// Cấu hình điều khiển của người chơi ảo chạy trên máy này (BotConfig.Persona, mạng giả lập).
        /// null = trận qua máy chủ: đối thủ (người thật, hoặc người chơi ảo chạy trên máy chủ) đến qua <see cref="IMatchFeed"/>.
        /// </summary>
        public BotConfig Bot;
    }

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

        /// <summary>
        /// PvP qua máy chủ: chạy chậm hơn máy chủ bấy nhiêu bước làm đệm (mạng chập chờn không bị giật);
        /// tụt lại quá bấy nhiêu bước thì chạy dồn cho kịp.
        /// </summary>
        public int NetBufferTicks = 2;
        public int NetCatchUpTicks = 6;
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

        /// <summary>Coin nhận được (thắng Bot, Phase 5.1).</summary>
        public int Coins;

        /// <summary>%HP thú còn lại lúc hết trận (0..1) — Thám hiểm tính sao theo đây.</summary>
        public float HpLeft;
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
        private readonly ICoinWallet _wallet;
        private readonly IMatchFeed _feed;
        private float _accum;
        private bool _quit;
        private bool _finished;
        private bool _hintedGuard;
        private bool _readySent;
        private int _tick;
        private BattleOutcome? _forced;

        /// <summary>Có vào sân tập được không (cần thú còn sống, không ốm — GDD G3).</summary>
        public static bool CanStart(PetService pets, HealthService health, out string reason)
        {
            reason = null;
            if (pets == null || !pets.HasPet) reason = "không có thú";
            else if (!health.CanTrain) reason = "đang ốm, phải chữa khỏi mới tập / đấu được";
            else if (pets.Current.IsSleeping) reason = "đang ngủ";
            return reason == null;
        }

        /// <param name="wallet">Ví để cộng Coin khi thắng Bot (null = không thưởng Coin).</param>
        public BattleSession(BattleMode mode, BotLevel level, PetService pets, EvolutionService evo, ulong seed,
                             BattleConfig battleCfg = null, BattleSessionConfig sessionCfg = null, BotConfig botCfg = null,
                             ICoinWallet wallet = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _sc = sessionCfg ?? new BattleSessionConfig();
            _wallet = wallet;
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
                bc.UltimateNeedsRage = false;   // học vuốt Z không phải chờ đầy Nộ
                me.Hp = bot.Hp = _sc.TrainingHp;
                bot.Atk *= _sc.TrainingBotAtkFactor;
            }
            Sim = new BattleSimulation(me, bot, bc, seed, evo.LearnedMask(), null);
            _bot = new BattleBot(BotSide, mode == BattleMode.Training ? BotLevel.Easy : level, seed * 7 + 3, botCfg);
            Result = new BattleResult();
        }

        /// <summary>
        /// PvP với người chơi ảo chạy trên máy (mạng giả lập): đấu 90 giây với đối thủ cho trước, cùng luật như đấu Bot
        /// (thú mình dùng chỉ số thật, đã tính bệnh). Elo + Coin do RankService tính sau trận (BattleSession không biết rank).
        /// Thắng không có điểm thưởng thêm như đấu Bot.
        /// </summary>
        public BattleSession(PetService pets, EvolutionService evo, RankedOpponent opponent, ulong seed,
                             BattleConfig battleCfg = null, BattleSessionConfig sessionCfg = null)
            : this(pets, evo, opponent, seed, BattleMode.Ranked, battleCfg, sessionCfg)
        {
        }

        /// <summary>
        /// Đấu đối thủ cho trước, chạy ngay trên máy: Ranked (người chơi ảo, mạng giả lập) hoặc Thám hiểm (sinh vật hoang).
        /// Đối thủ do <see cref="RankedOpponent.Bot"/> điều khiển; phần thưởng tính ở ngoài (RankService / AdventureService).
        /// </summary>
        public BattleSession(PetService pets, EvolutionService evo, RankedOpponent opponent, ulong seed, BattleMode mode,
                             BattleConfig battleCfg = null, BattleSessionConfig sessionCfg = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            if (opponent == null) throw new ArgumentNullException(nameof(opponent));
            if (mode != BattleMode.Ranked && mode != BattleMode.Adventure) throw new ArgumentException("chỉ dùng cho Ranked / Thám hiểm", nameof(mode));
            _sc = sessionCfg ?? new BattleSessionConfig();
            Mode = mode;
            Level = BotLevel.Normal;
            Opponent = opponent;
            PetStats ps = evo.Stats;
            var me = new FighterStats(ps.Hp, ps.Atk, ps.Def, ps.Spd, ps.Crit);
            Sim = new BattleSimulation(me, opponent.Stats, battleCfg ?? new BattleConfig(), seed, evo.LearnedMask(), opponent.Learned);
            _bot = opponent.Bot != null ? new BattleBot(BotSide, BotLevel.Normal, seed * 7 + 3, opponent.Bot) : null;
            Result = new BattleResult();
        }

        /// <summary>
        /// PvP qua máy chủ riêng (Phase 4.2): máy chủ chạy trận và quyết định kết quả; máy này chạy lại đúng các bước +
        /// nét vuốt máy chủ đã chốt (<see cref="IMatchFeed"/>), mình luôn là bên 0 trên màn hình.
        /// Nét vuốt của mình gửi lên máy chủ chứ không áp thẳng (chờ máy chủ chốt bước rồi mới áp, 2 máy như nhau).
        /// Chỉ số 2 bên lấy đúng bản máy chủ gửi về. `opponent` chỉ dùng để hiện tên.
        /// </summary>
        public BattleSession(PetService pets, EvolutionService evo, RankedOpponent opponent, IMatchFeed feed,
                             BattleConfig battleCfg = null, BattleSessionConfig sessionCfg = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            Opponent = opponent ?? throw new ArgumentNullException(nameof(opponent));
            _feed = feed ?? throw new ArgumentNullException(nameof(feed));
            _sc = sessionCfg ?? new BattleSessionConfig();
            Mode = BattleMode.Ranked;
            Level = BotLevel.Normal;
            int me = feed.ServerSide == 1 ? 1 : 0;
            BattleConfig bc = battleCfg ?? new BattleConfig();
            if (feed.DurationSeconds > 0f) bc.DurationSeconds = feed.DurationSeconds;
            Sim = new BattleSimulation(feed.StatsOf(me), feed.StatsOf(1 - me), bc, feed.Seed, feed.LearnedOf(me), feed.LearnedOf(1 - me), me);
            Result = new BattleResult();
        }

        /// <summary>Đối thủ PvP (null nếu không phải trận Ranked).</summary>
        public RankedOpponent Opponent { get; }

        /// <summary>Trận qua máy chủ (có <see cref="IMatchFeed"/>).</summary>
        public bool Networked => _feed != null;

        /// <summary>Trận qua máy chủ: đang chờ máy chủ bắt đầu (đối thủ chưa vào màn đấu).</summary>
        public bool WaitingForServer => _feed != null && !_feed.Started && !_finished;

        /// <summary>Trận qua máy chủ: số bước đã chạy trên máy này.</summary>
        public int TicksPlayed => _tick;

        /// <summary>Số lần phải sửa máu theo máy chủ (0 = 2 máy + máy chủ khớp hoàn toàn).</summary>
        public int HpFixes { get; private set; }

        /// <summary>Có thì hết trận phát <see cref="BattleEnded"/> (nhiệm vụ ngày đếm trận thắng / trận PvP). View / App gắn vào.</summary>
        public EventBus Events { get; set; }

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

        /// <summary>
        /// Người chơi vuốt xong 1 nét. Trả true nếu trận nhận nét đó (ra đòn ngay, hoặc giữ trong bộ đệm rồi ra đòn khi rảnh).
        /// Vuốt Z lúc chưa đầy Nộ: báo RageLow ngay trên máy này, kể cả khi đấu qua máy chủ (không gửi đi).
        /// </summary>
        public bool PlayerSwipe(SwipeResult swipe, List<BattleEvent> events = null, List<SessionNote> notes = null)
        {
            if (_finished) return false;
            if (swipe.IsValid && swipe.Kind == SwipeKind.Z && !Sim.UltimateReady(PlayerSide))
            {
                events?.Add(new BattleEvent { Kind = BattleEventKind.RageLow, Side = PlayerSide, Move = MoveKind.Ultimate, Time = Sim.Time });
                return false;
            }
            if (_feed != null)   // qua máy chủ: gửi lên, máy chủ chốt bước rồi cả 2 máy cùng áp
            {
                if (!_feed.Started) return false;
                _feed.SendSwipe(swipe);
                return true;
            }
            if (Mode == BattleMode.Training && !swipe.IsValid)   // huấn luyện: vuốt sai không bị hở, cho vuốt lại
            {
                notes?.Add(new SessionNote { Kind = SessionNoteKind.Retry, Skill = Lesson, Text = swipe.Reason });
                return false;
            }
            var local = new List<BattleEvent>();
            bool ok = Sim.Submit(PlayerSide, swipe, local);
            foreach (BattleEvent e in local)
            {
                Practice(e, notes);
                events?.Add(e);
            }
            return ok;
        }

        /// <summary>Huấn luyện: mỗi lần thú thật sự ra chiêu đang học (kể cả nét lấy từ bộ đệm) tính 1 lần tập; đủ 10 lần thì thuộc.</summary>
        private void Practice(BattleEvent e, List<SessionNote> notes)
        {
            if (Mode != BattleMode.Training || e.Kind != BattleEventKind.AttackStarted || e.Side != PlayerSide) return;
            int lesson = Lesson;
            if (lesson < 0 || (int)e.Move != lesson) return;
            bool learned = _evo.PracticeSkill(lesson);
            notes?.Add(new SessionNote { Kind = SessionNoteKind.Practiced, Skill = lesson, Progress = _evo.SkillProgress(lesson) });
            if (!learned) return;
            Sim.Fighters[PlayerSide].Learned[lesson] = true;
            Result.Learned.Add(lesson);
            notes?.Add(new SessionNote { Kind = SessionNoteKind.Learned, Skill = lesson, Progress = _evo.SkillProgress(lesson) });
            if (Lesson < 0) notes?.Add(new SessionNote { Kind = SessionNoteKind.AllLearned, Skill = -1 });
        }

        /// <summary>Chạy phiên theo thời gian thật (View gọi mỗi frame). Mô phỏng vẫn theo bước cố định bên trong.</summary>
        public void Advance(float realSeconds, List<BattleEvent> events = null, List<SessionNote> notes = null)
        {
            if (_finished) return;
            if (_feed != null)
            {
                AdvanceFeed(realSeconds, events, notes);
                return;
            }
            _accum += Math.Max(0f, realSeconds) * TimeScale;
            float tick = Sim.Config.TickSeconds;
            var local = new List<BattleEvent>();
            while (_accum >= tick && !Sim.IsOver)
            {
                _accum -= tick;
                SwipeResult? s = _bot?.Think(Sim);   // người chơi ảo / hình nộm; người thật ở máy khác gửi nét qua mạng
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
                Practice(e, notes);
                Reward(e);
                events?.Add(e);
            }
            if (Sim.IsOver) Finish(notes);
        }

        /// <summary>Rời sân (Huấn luyện: lúc nào cũng được; Đấu: bỏ cuộc = thua; qua máy chủ: báo máy chủ là bỏ cuộc).</summary>
        public void Quit(List<SessionNote> notes = null)
        {
            if (_finished) return;
            if (_feed != null && _feed.Ended) _forced = FeedSides.Local(_feed.EndOutcome, _feed.ServerSide);   // máy chủ đã chốt rồi
            else
            {
                _feed?.Leave();
                _quit = true;
            }
            Finish(notes);
        }

        /// <summary>Qua máy chủ: chạy các bước máy chủ đã chốt theo đồng hồ máy (giữ vài bước đệm), hết trận theo máy chủ.</summary>
        private void AdvanceFeed(float realSeconds, List<BattleEvent> events, List<SessionNote> notes)
        {
            if (!_readySent)
            {
                _readySent = true;
                _feed.Ready();
            }
            var local = new List<BattleEvent>();
            if (_feed.Started)
            {
                float dt = Sim.Config.TickSeconds;
                _accum += Math.Max(0f, realSeconds);
                int avail = Math.Max(0, _feed.ConfirmedTicks - _tick);
                int ahead = _feed.Ended ? avail : Math.Max(0, avail - Math.Max(0, _sc.NetBufferTicks));
                int steps = (int)(_accum / dt + 1e-3f);   // + chút xíu: 2 × (1/30) cộng float có thể ra 1,99999
                if (ahead > steps + Math.Max(1, _sc.NetCatchUpTicks)) steps = ahead;   // tụt lại quá xa: chạy dồn cho kịp
                steps = Math.Min(steps, ahead);
                _accum = Math.Max(0f, Math.Min(_accum - steps * dt, dt * 2f));   // đang chờ máy chủ thì không dồn thời gian
                for (int i = 0; i < steps && !Sim.IsOver; i++) StepFeed(local);
            }
            foreach (BattleEvent e in local)
            {
                Reward(e);
                events?.Add(e);
            }
            if (_feed.Ended)
            {
                if (_tick >= _feed.EndTick || Sim.IsOver)
                {
                    _forced = FeedSides.Local(_feed.EndOutcome, _feed.ServerSide);
                    Finish(notes);
                }
            }
            else if (_feed.Lost)   // mất kết nối giữa trận: máy chủ tính bỏ cuộc
            {
                _quit = true;
                Finish(notes);
            }
        }

        /// <summary>1 bước: áp nét vuốt máy chủ đã chốt ở bước này (đúng thứ tự máy chủ), chạy bước, so máu với máy chủ.</summary>
        private void StepFeed(List<BattleEvent> local)
        {
            int me = _feed.ServerSide;
            while (_feed.TryPeekInput(out TickInput input) && input.Tick <= _tick)
            {
                _feed.PopInput();
                Sim.Submit(FeedSides.Local(input.Side, me), input.Swipe, local);
            }
            Sim.Tick(local);
            _tick++;
            while (_feed.TryPeekHp(out HpCheck c) && c.Ticks <= _tick)
            {
                _feed.PopHp();
                if (c.Ticks != _tick || Sim.IsOver) continue;
                FixHp(FeedSides.Local(0, me), c.Hp0);
                FixHp(FeedSides.Local(1, me), c.Hp1);
            }
        }

        /// <summary>Máu lệch so với máy chủ (lẽ ra không xảy ra vì cùng seed + cùng chỉ số tới từng bit) thì theo máy chủ.</summary>
        private void FixHp(int side, float hp)
        {
            Fighter f = Sim.Fighters[side];
            if (float.IsNaN(hp) || f.Hp == hp) return;
            f.Hp = hp;
            HpFixes++;
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
            Result.Outcome = _forced ?? (_quit && Mode != BattleMode.Training ? BattleOutcome.Win1 : Sim.Outcome);   // bỏ cuộc = thua
            Result.PlayerWon = Result.Outcome == BattleOutcome.Win0;
            Result.HpLeft = Sim.Fighters[PlayerSide].HpPercent;
            if (Mode == BattleMode.BotFight && Result.PlayerWon)
            {
                int lv = Math.Max(0, Math.Min(_sc.WinStrength.Length - 1, (int)Level));
                Gain(TrainingAxis.Strength, _sc.WinStrength[lv]);
                Gain(TrainingAxis.Speed, _sc.WinSpeed[lv]);
                int[] coins = _pets.Config.Economy.BotWinCoins;   // thắng Bot có Coin (GDD G11b)
                int c = coins != null && coins.Length > 0 ? coins[Math.Max(0, Math.Min(coins.Length - 1, (int)Level))] : 0;
                if (_wallet != null && c > 0)
                {
                    _wallet.Add(c, "thắng hình nộm " + Level);
                    Result.Coins = c;
                }
            }
            if (_pets.HasPet)
            {
                bool training = Mode == BattleMode.Training;
                _pets.Spend(NeedType.Energy, training ? _sc.TrainingEnergyCost : _sc.FightEnergyCost);
                _pets.Spend(NeedType.Satiety, training ? _sc.TrainingSatietyCost : _sc.FightSatietyCost);
            }
            notes?.Add(new SessionNote { Kind = SessionNoteKind.Finished, Skill = -1 });
            Events?.Publish(new BattleEnded((int)Mode, Result.PlayerWon));
        }
    }
}
