using System;
using System.Collections.Generic;
using System.Numerics;
using NUnit.Framework;
using PawFit.Core;
using PawFit.Economy;
using PawFit.Garden;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>Đồ chơi + trò chơi trong vườn (Phase 8.3).</summary>
    public class PlayTests
    {
        private static PlayService Play(GardenFixture f) => new PlayService(f.Pets, f.Health, f.Evo, f.Time, f.Bus);

        [Test]
        public void Fetch_RaisesMood_AddsSpeed_CostsEnergy_PublishesEvent()
        {
            var f = new GardenFixture();
            PlayService p = Play(f);
            f.Pets.SetNeedForDebug(NeedType.Mood, 50f);
            float energy = f.Pets.Current.Needs.Energy, speed = f.Evo.Training.Speed;
            var done = new List<PlayKind>();
            f.Bus.Subscribe<PlayDone>(e => done.Add(e.Kind));
            Assert.IsTrue(p.Reward(PlayKind.Fetch));
            Assert.AreEqual(50f + p.Config.Mood[0], f.Pets.Current.Needs.Mood, 0.01f);
            Assert.Less(f.Pets.Current.Needs.Energy, energy, "chạy nhặt bóng tốn sức");
            Assert.Greater(f.Evo.Training.Speed, speed, "chạy nhặt bóng = tập Tốc độ");
            CollectionAssert.AreEqual(new[] { PlayKind.Fetch }, done);
        }

        [Test]
        public void Rewards_HaveCooldown_PerKind()
        {
            var f = new GardenFixture();
            PlayService p = Play(f);
            Assert.IsTrue(p.Reward(PlayKind.Pounce));
            Assert.IsFalse(p.Reward(PlayKind.Pounce), "vồ liên tục không cộng mãi");
            Assert.IsTrue(p.Reward(PlayKind.BubblePop), "kiểu khác thì vẫn được");
            f.Time.Advance(TimeSpan.FromSeconds(1.5));
            Assert.IsTrue(p.Reward(PlayKind.Pounce));
        }

        [Test]
        public void NoPlay_WhenSleeping_Sick_OrTired()
        {
            var f = new GardenFixture();
            PlayService p = Play(f);
            f.Pets.SetSleeping(true);
            Assert.IsFalse(p.CanPlay(out string sleeping));
            StringAssert.Contains("ngủ", sleeping);
            f.Pets.SetSleeping(false);
            f.Pets.SetNeedForDebug(NeedType.Energy, 5f);
            Assert.IsFalse(p.CanPlay(out string tired));
            StringAssert.Contains("mệt", tired);
            Assert.IsFalse(p.Reward(PlayKind.Fetch));
            f.Pets.SetNeedForDebug(NeedType.Energy, 80f);
            f.Pets.SetNeglectForDebug(80);
            Assert.IsFalse(p.CanPlay(out string sick));
            StringAssert.Contains("ốm", sick);
        }

        [Test]
        public void Brain_PlayFollowsToy_StopsWhenViewStopsUpdating()
        {
            var f = new GardenFixture();
            var ball = new Vector2(3f, -2f);
            f.Brain.Play(ball);
            Assert.AreEqual(PetActivity.Playing, f.Brain.Activity);
            Assert.IsTrue(f.Brain.IsMoving);
            Assert.IsTrue(f.Brain.Run, "chạy theo đồ chơi");
            f.Simulate(2f);
            Assert.Less(Vector2.Distance(f.Pos, ball), 0.5f, "chạy tới bóng");
            f.Brain.EndPlay();
            Assert.AreEqual(PetActivity.Reacting, f.Brain.Activity, "nhặt xong: vui vẻ");
            f.Brain.Play(ball);
            f.Simulate(6f);
            Assert.AreNotEqual(PetActivity.Playing, f.Brain.Activity, "View thôi cập nhật quá 4 giây thì thôi chơi");
        }

        [Test]
        public void Brain_SelfPlaysWithBall_WhenBored()
        {
            var f = new GardenFixture();
            PlayService p = Play(f);
            f.Pets.SetNeedForDebug(NeedType.Mood, 40f);
            f.Pets.SetNeedForDebug(NeedType.Energy, 80f);
            f.Pets.SetNeedForDebug(NeedType.Satiety, 80f);
            var ball = new Vector2(2.6f, -0.4f);
            f.Brain.BallPosition = () => ball;
            f.Brain.WantsSelfPlay = roll => p.WantsSelfPlay(roll * 0.1);   // luôn muốn (để test chắc chắn)
            bool selfPlayed = false;
            for (int i = 0; i < 600 && !selfPlayed; i++)
            {
                f.Simulate(0.1f);
                selfPlayed = f.Brain.Activity == PetActivity.Playing && f.Brain.SelfPlaying;
            }
            Assert.IsTrue(selfPlayed, "bé chán thì tự tới chơi bóng");
        }

        [Test]
        public void Toys_BuyOnce_ShowsInGarden()
        {
            var time = new ManualTimeProvider(GardenFixture.T0);
            var bus = new EventBus();
            var cfg = GameConfig.CreateDefault();
            var pets = new PetService(time, new SeededRandom(5), new InMemorySaveService(new NewtonsoftSerializer()), bus, cfg);
            pets.LoadOrCreate("Kem");
            var eco = new EconomyService(pets, new MockAdService(), time, bus, cfg);
            var cos = new CosmeticsService(pets, eco, bus, cfg);
            var added = new List<ToyKind>();
            bus.Subscribe<ToyAdded>(e => added.Add(e.Kind));
            int coins = eco.Coins;
            Assert.AreEqual(cfg.Economy.ToyPrices[0], cos.PriceOf(ToyKind.Ball));
            Assert.AreEqual(CosmeticRefusal.None, cos.Buy(ToyKind.Ball));
            Assert.AreEqual(coins - cos.PriceOf(ToyKind.Ball), eco.Coins);
            Assert.IsTrue(cos.Owns(ToyKind.Ball));
            Assert.AreEqual(CosmeticRefusal.AlreadyOwned, cos.Buy(ToyKind.Ball), "mua 1 lần");
            CollectionAssert.AreEqual(new[] { ToyKind.Ball }, added, "vườn bày bóng ra");
        }

        [Test]
        public void DailyQuest_PlayWithPet_OnlyWithToys()
        {
            var f = new GardenFixture();
            var daily = new DailyService(f.Pets, f.Time, f.Bus, null, f.Layout, toLocal: utc => utc, hasToys: () => true);
            bool seen = false;
            for (int d = 0; d < 30 && !seen; d++)
            {
                f.Time.Advance(TimeSpan.FromDays(1));
                foreach (QuestState q in daily.Quests) seen |= q.Kind == QuestKind.Play;
            }
            Assert.IsTrue(seen, "có đồ chơi thì có ngày ra nhiệm vụ chơi với bé");
            daily.Quests[0].Kind = QuestKind.Play;
            daily.Quests[0].Progress = 0;
            daily.Quests[0].Target = 2;
            f.Bus.Publish(new PlayDone(PlayKind.Fetch));
            f.Bus.Publish(new PlayDone(PlayKind.Pounce));
            Assert.IsTrue(daily.Quests[0].Done);
            daily.Dispose();
            var noToys = new DailyService(f.Pets, f.Time, f.Bus, null, f.Layout, toLocal: utc => utc);
            for (int d = 0; d < 30; d++)
            {
                f.Time.Advance(TimeSpan.FromDays(1));
                foreach (QuestState q in noToys.Quests) Assert.AreNotEqual(QuestKind.Play, q.Kind, "chưa có đồ chơi thì không ra");
            }
            noToys.Dispose();
        }
    }
}
