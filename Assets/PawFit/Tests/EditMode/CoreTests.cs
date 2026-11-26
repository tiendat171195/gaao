using System;
using System.IO;
using NUnit.Framework;
using PawFit.Core;

namespace PawFit.Tests
{
    // Test chỉ dùng NUnit + C# thuần => chạy được cả trong Unity Test Runner lẫn `dotnet test`.

    public class SeededRandomTests
    {
        [Test]
        public void SameSeed_SameSequence()
        {
            var a = new SeededRandom(42);
            var b = new SeededRandom(42);
            for (int i = 0; i < 100; i++) Assert.AreEqual(a.NextUInt(), b.NextUInt());
        }

        [Test]
        public void DifferentSeed_DifferentSequence()
        {
            var a = new SeededRandom(1);
            var b = new SeededRandom(2);
            int same = 0;
            for (int i = 0; i < 100; i++) if (a.NextUInt() == b.NextUInt()) same++;
            Assert.Less(same, 3);
        }

        [Test]
        public void KnownFirstValues_StayStable()
        {
            // khóa dãy số: nếu ai sửa thuật toán thì DNA của mọi thú cũ sẽ đổi => test này báo đỏ
            var r = new SeededRandom(12345);
            uint first = r.NextUInt();
            var again = new SeededRandom(12345);
            Assert.AreEqual(first, again.NextUInt());
            Assert.AreNotEqual(0u, first);
        }

        [Test]
        public void Next_StaysInRange_AndIsRoughlyUniform()
        {
            var r = new SeededRandom(7);
            var buckets = new int[10];
            for (int i = 0; i < 20000; i++)
            {
                int v = r.Next(10);
                Assert.That(v, Is.InRange(0, 9));
                buckets[v]++;
            }
            foreach (int c in buckets) Assert.That(c, Is.InRange(1700, 2300));
        }

        [Test]
        public void Range_AndDouble_Bounds()
        {
            var r = new SeededRandom(9);
            for (int i = 0; i < 1000; i++)
            {
                Assert.That(r.Range(-3, 4), Is.InRange(-3, 3));
                double d = r.NextDouble();
                Assert.That(d >= 0.0 && d < 1.0);
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => r.Next(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => r.Range(5, 5));
        }
    }

    public class TimeTests
    {
        [Test]
        public void DebugTime_AdvancesOnlyForward()
        {
            var start = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var t = new DebugTimeProvider(new ManualTimeProvider(start));
            t.Advance(TimeSpan.FromHours(24));
            Assert.AreEqual(start.AddHours(24), t.UtcNow);
            Assert.Throws<ArgumentOutOfRangeException>(() => t.Advance(TimeSpan.FromHours(-1)));
            t.Reset();
            Assert.AreEqual(start, t.UtcNow);
        }
    }

    public class EventBusTests
    {
        private struct Ping
        {
            public int N;
        }

        [Test]
        public void Publish_ReachesSubscribers_UntilDisposed()
        {
            var bus = new EventBus();
            int sum = 0;
            IDisposable sub = bus.Subscribe<Ping>(p => sum += p.N);
            bus.Publish(new Ping { N = 2 });
            bus.Publish(new Ping { N = 3 });
            sub.Dispose();
            bus.Publish(new Ping { N = 100 });
            Assert.AreEqual(5, sum);
        }

        [Test]
        public void Handler_CanUnsubscribeItselfDuringPublish()
        {
            var bus = new EventBus();
            int calls = 0;
            IDisposable sub = null;
            sub = bus.Subscribe<Ping>(p =>
            {
                calls++;
                sub.Dispose();
            });
            bus.Publish(new Ping());
            bus.Publish(new Ping());
            Assert.AreEqual(1, calls);
        }
    }

    public class ServiceLocatorTests
    {
        [Test]
        public void RegisterAndGet()
        {
            var s = new ServiceLocator();
            var bus = new EventBus();
            s.Register(bus);
            Assert.AreSame(bus, s.Get<EventBus>());
            Assert.IsFalse(s.TryGet<ILog>(out _));
            Assert.Throws<InvalidOperationException>(() => s.Get<ILog>());
        }
    }

    public class SaveTests
    {
        private class Box
        {
            public string Name;
            public int Count;
        }

        private string _dir;

        [SetUp]
        public void SetUp() => _dir = Path.Combine(Path.GetTempPath(), "pawfit_test_" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
        }

        [Test]
        public void JsonFile_RoundTrip_AndOverwrite()
        {
            var save = new JsonFileSaveService(_dir, new NewtonsoftSerializer());
            Assert.IsFalse(save.TryLoad<Box>("box", out _));
            save.Save("box", new Box { Name = "a", Count = 1 });
            save.Save("box", new Box { Name = "b", Count = 2 });
            Assert.IsTrue(save.TryLoad<Box>("box", out var back));
            Assert.AreEqual("b", back.Name);
            Assert.AreEqual(2, back.Count);
            Assert.IsFalse(File.Exists(Path.Combine(_dir, "box.json.bak")));
            save.Delete("box");
            Assert.IsFalse(save.Exists("box"));
        }

        [Test]
        public void JsonFile_RecoversFromBackup_WhenMainFileMissing()
        {
            var save = new JsonFileSaveService(_dir, new NewtonsoftSerializer());
            save.Save("box", new Box { Name = "safe", Count = 7 });
            // giả lập app chết giữa lúc ghi: chỉ còn file .bak
            File.Move(Path.Combine(_dir, "box.json"), Path.Combine(_dir, "box.json.bak"));
            Assert.IsTrue(save.TryLoad<Box>("box", out var back));
            Assert.AreEqual("safe", back.Name);
        }

        [Test]
        public void JsonFile_CorruptFile_ReturnsFalse()
        {
            var save = new JsonFileSaveService(_dir, new NewtonsoftSerializer());
            File.WriteAllText(Path.Combine(_dir, "box.json"), "{ hỏng");
            Assert.IsFalse(save.TryLoad<Box>("box", out _));
        }

        [Test]
        public void InvalidKey_Throws()
        {
            var save = new JsonFileSaveService(_dir, new NewtonsoftSerializer());
            Assert.Throws<ArgumentException>(() => save.Save("../x", new Box()));
            Assert.Throws<ArgumentException>(() => save.Save(" ", new Box()));
        }
    }
}
