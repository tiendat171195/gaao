using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Mẹo hướng dẫn theo tình huống: đúng lúc, mỗi mẹo 1 lần, tự làm rồi thì bỏ qua.</summary>
    public class TipTests
    {
        private GardenFixture _f;
        private InMemorySaveService _save;
        private SettingsService _settings;
        private TipService _tips;

        [SetUp]
        public void SetUp()
        {
            _f = new GardenFixture();
            _save = new InMemorySaveService(new NewtonsoftSerializer());
            _settings = new SettingsService(_save, _f.Bus);
            _tips = new TipService(_settings, _f.Pets, _f.Health, _f.Evo, new ExpressionConfig(), _f.Bus);
        }

        /// <summary>Cho thời gian trôi tới khi có mẹo (tối đa `maxSeconds`).</summary>
        private TipId? Next(float maxSeconds = 120f)
        {
            for (float t = 0; t < maxSeconds; t += 0.5f)
            {
                TipId? tip = _tips.Update(0.5f);
                if (tip.HasValue) return tip;
            }
            return null;
        }

        [Test]
        public void Intro_InOrder_WaitsForPlayerOrTimeout()
        {
            Assert.IsNull(_tips.Update(1f), "chưa tới 2,5 giây");
            Assert.AreEqual(TipId.Move, Next(5f));
            Assert.IsNull(Next(20f), "2 mẹo cách nhau tối thiểu 25 giây");
            _tips.Done(TipId.Move);   // người chơi chạm đất
            Assert.AreEqual(TipId.Pet, Next(10f));
            Assert.AreEqual(TipId.Lead, Next(60f), "chưa vuốt ve thì chờ 40 giây rồi mới chỉ tiếp");
        }

        [Test]
        public void DoneBeforeShown_IsSkipped_SeenPersists()
        {
            _tips.Done(TipId.Move);
            _f.Care.Pet();   // vuốt ve qua luật chăm sóc thật => CareDone(Pet)
            TipId? first = Next(10f);
            Assert.AreEqual(TipId.Lead, first, "đã tự làm 2 việc đầu nên chỉ còn mẹo dắt");
            var again = new TipService(new SettingsService(_save, new EventBus()), _f.Pets, _f.Health, _f.Evo, new ExpressionConfig(), new EventBus());
            for (int i = 0; i < 20; i++)
            {
                TipId? t = again.Update(1f);
                Assert.IsFalse(t == TipId.Move || t == TipId.Pet || t == TipId.Lead, "mở lại app không hiện lại mẹo đã xem");
            }
        }

        [Test]
        public void Situational_HungryDirtySleepy_SickFirst()
        {
            _tips.Done(TipId.Move);
            _tips.Done(TipId.Pet);
            _tips.Done(TipId.Lead);
            _f.Pets.SetNeedForDebug(NeedType.Satiety, 10f);
            Assert.AreEqual(TipId.Feed, Next(10f));
            _f.Pets.SetNeedForDebug(NeedType.Hygiene, 10f);
            Assert.AreEqual(TipId.Bathe, Next(40f));
            _f.Pets.SetNeedForDebug(NeedType.Energy, 10f);
            Assert.AreEqual(TipId.Sleep, Next(40f));
            _f.Pets.SetNeglectForDebug(_f.Pets.Config.Health.SickAfterHours + 1);
            Assert.AreEqual(TipId.Sick, Next(40f));
        }

        [Test]
        public void Discovery_ShopTrainingAdventureArena_ByPlayTime()
        {
            foreach (TipId id in new[] { TipId.Move, TipId.Pet, TipId.Lead, TipId.Feed, TipId.Bathe, TipId.Sleep }) _tips.Done(id);
            var shown = new List<TipId>();
            for (int i = 0; i < 1200; i++)
            {
                TipId? t = _tips.Update(0.5f);
                if (t.HasValue) shown.Add(t.Value);
            }
            CollectionAssert.AreEqual(new[] { TipId.Shop, TipId.Training, TipId.Adventure, TipId.Arena }, shown);
        }

        [Test]
        public void Restart_ShowsAgain_NoTipsWithoutPet()
        {
            Assert.AreEqual(TipId.Move, Next(5f));
            _tips.Restart();
            Assert.AreEqual(TipId.Move, Next(5f), "xem lại hướng dẫn từ đầu");
            _f.Pets.SetNeglectForDebug(_f.Pets.Config.Health.DeathAfterHours + 1);
            Assert.IsNull(Next(60f), "đang chờ trứng nở thì không hiện mẹo");
        }

        [Test]
        public void WelcomeBack_DescribesState()
        {
            Assert.IsNull(_tips.WelcomeBack(0.5), "vắng chưa tới 1 giờ thì thôi");
            StringAssert.Contains("vẫn ổn", _tips.WelcomeBack(3));
            _f.Pets.SetNeedForDebug(NeedType.Satiety, 10f);
            _f.Pets.SetNeedForDebug(NeedType.Hygiene, 10f);
            string text = _tips.WelcomeBack(6.2);
            StringAssert.Contains("6 giờ", text);
            StringAssert.Contains("đói", text);
            StringAssert.Contains("bẩn", text);
            StringAssert.Contains("3 ngày", _tips.WelcomeBack(75));
            foreach (TipId id in Enum.GetValues(typeof(TipId))) Assert.IsNotEmpty(TipService.Text(id));
        }
    }
}
