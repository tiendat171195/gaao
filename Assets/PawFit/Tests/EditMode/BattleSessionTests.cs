using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Phiên đấu ở sân tập: Mode Huấn luyện (học chiêu) + Đấu Bot (Phase 3.2).</summary>
    public class BattleSessionTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 26, 8, 0, 0, DateTimeKind.Utc);
        private ManualTimeProvider _time;
        private EventBus _bus;
        private GameConfig _cfg;
        private PetService _pets;
        private HealthService _health;
        private EvolutionService _evo;

        [SetUp]
        public void SetUp()
        {
            _time = new ManualTimeProvider(T0);
            _bus = new EventBus();
            _cfg = GameConfig.CreateDefault();
            _pets = new PetService(_time, new SeededRandom(31), new InMemorySaveService(new NewtonsoftSerializer()), _bus, _cfg);
            _pets.LoadOrCreate("Kem");
            _health = new HealthService(_pets, _time, _cfg);
            _evo = new EvolutionService(_pets, _health, _time, _bus, _cfg);
        }

        private static SwipeResult S(SwipeKind k) => new SwipeResult { Kind = k, Quality = 1f };

        private BattleSession Training() => new BattleSession(BattleMode.Training, BotLevel.Easy, _pets, _evo, 5);

        [Test]
        public void CanStart_OnlyWhenHealthyAndAwake()
        {
            Assert.IsTrue(BattleSession.CanStart(_pets, _health, out _));
            _pets.SetSleeping(true);
            Assert.IsFalse(BattleSession.CanStart(_pets, _health, out _));
            _pets.SetSleeping(false);
            _pets.SetNeglectForDebug(80);
            Assert.IsFalse(BattleSession.CanStart(_pets, _health, out string reason));
            StringAssert.Contains("ốm", reason);
        }

        [Test]
        public void Training_WrongSwipe_IsNotPunished()
        {
            BattleSession s = Training();
            var notes = new List<SessionNote>();
            Assert.IsFalse(s.PlayerSwipe(SwipeResult.Wrong("không rõ hình"), null, notes));
            Assert.AreEqual(SessionNoteKind.Retry, notes[0].Kind);
            Assert.IsTrue(s.PlayerSwipe(S(SwipeKind.Horizontal)), "vuốt lại được ngay, không bị hở 1 giây");
        }

        [Test]
        public void Training_TenCorrectSwipes_LearnsSkill_ThenNextLesson()
        {
            BattleSession s = Training();
            Assert.AreEqual(0, s.Lesson, "học chiêu ngang trước");
            var notes = new List<SessionNote>();
            Assert.IsTrue(s.PlayerSwipe(S(SwipeKind.Up)));
            Assert.AreEqual(0, _evo.SkillProgress(0), "vuốt sai chiêu đang học thì không tính");
            s.Advance(1.5f);
            for (int i = 0; i < 60 && !_evo.IsLearned(0); i++)
            {
                s.PlayerSwipe(S(SwipeKind.Horizontal), null, notes);
                s.Advance(1.2f);
            }
            Assert.IsTrue(_evo.IsLearned(0));
            Assert.AreEqual(_cfg.Evolution.PracticeToLearn, _evo.SkillProgress(0));
            Assert.IsTrue(notes.Exists(n => n.Kind == SessionNoteKind.Learned && n.Skill == 0));
            Assert.AreEqual(1, s.Lesson, "chuyển sang học chiêu lên");
            Assert.Contains(0, s.Result.Learned);
            Assert.AreEqual(1, _pets.Current.Growth.LearnedSkills);
        }

        [Test]
        public void Training_SlowsDown_WhenDummyAttacks()
        {
            BattleSession s = Training();
            bool slow = false;
            var notes = new List<SessionNote>();
            for (int i = 0; i < 600 && !slow; i++)
            {
                s.Advance(0.05f, null, notes);
                slow = s.SlowMo;
            }
            Assert.IsTrue(slow, "hình nộm vung đòn thì chậm lại");
            Assert.AreEqual(0.3f, s.TimeScale, 1e-4f);
            Assert.IsTrue(notes.Exists(n => n.Kind == SessionNoteKind.GuardHint), "gợi ý vuốt xuống để đỡ");
        }

        [Test]
        public void BotFight_HitsGivePoints_WinGivesBonus_CostsEnergy()
        {
            float energy = _pets.Current.Needs.Energy;
            var s = new BattleSession(BattleMode.BotFight, BotLevel.Easy, _pets, _evo, 8);
            for (int i = 0; i < 2000 && !s.IsOver; i++)
            {
                s.PlayerSwipe(S(i % 2 == 0 ? SwipeKind.Horizontal : SwipeKind.Up));
                s.Advance(0.1f);
            }
            Assert.IsTrue(s.IsOver);
            Assert.IsTrue(s.Result.PlayerWon, "thú đánh liên tục thắng hình nộm Dễ");
            Assert.Greater(s.Result.Gained.Speed, 0f, "đòn ngang trúng => Tốc độ");
            Assert.GreaterOrEqual(s.Result.Gained.Strength, new BattleSessionConfig().WinStrength[0], "thắng được thêm Sức mạnh");
            Assert.AreEqual(energy - new BattleSessionConfig().FightEnergyCost, _pets.Current.Needs.Energy, 0.01f);
        }

        [Test]
        public void BotFight_Quit_IsALoss()
        {
            var s = new BattleSession(BattleMode.BotFight, BotLevel.Hard, _pets, _evo, 2);
            s.Advance(1f);
            s.Quit();
            Assert.IsTrue(s.IsOver);
            Assert.IsFalse(s.Result.PlayerWon);
        }

        [Test]
        public void LearnedSkill_HitsHarder()
        {
            float before = new BattleSession(BattleMode.BotFight, BotLevel.Normal, _pets, _evo, 1).Sim.BaseDamage(0, MoveKind.Heavy, 1f);
            for (int i = 0; i < _cfg.Evolution.PracticeToLearn; i++) _evo.PracticeSkill(1);
            float after = new BattleSession(BattleMode.BotFight, BotLevel.Normal, _pets, _evo, 1).Sim.BaseDamage(0, MoveKind.Heavy, 1f);
            Assert.AreEqual(before / new BattleConfig().UnlearnedFactor, after, 1e-3f);
        }

        [Test]
        public void BotStats_ScaleWithPetAndLevel()
        {
            PetStats me = _evo.Stats;
            var easy = new BattleSession(BattleMode.BotFight, BotLevel.Easy, _pets, _evo, 1);
            var hard = new BattleSession(BattleMode.BotFight, BotLevel.Hard, _pets, _evo, 1);
            Assert.AreEqual(me.Atk * 0.7f, easy.Sim.Fighters[1].Stats.Atk, 1e-3f);
            Assert.AreEqual(me.Atk * 1.25f, hard.Sim.Fighters[1].Stats.Atk, 1e-3f);
            Assert.AreEqual(me.Hp, easy.Sim.Fighters[0].Stats.Hp, 1e-3f, "thú đấu bằng chỉ số thật");
        }
    }
}
