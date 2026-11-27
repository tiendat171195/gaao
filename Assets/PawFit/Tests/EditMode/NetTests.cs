using System;
using System.Collections.Generic;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Online;
using PawFit.Pet;

namespace PawFit.Tests
{
    /// <summary>
    /// Phase 4 máy chủ riêng (phần C# thuần, chạy được cả Unity Test Runner): luật dùng chung, giao thức, giờ máy chủ,
    /// người chơi ảo, và trận chạy theo nhịp máy chủ — 2 máy + máy chủ phải ra đúng 1 trận tới từng bit.
    /// </summary>
    public class NetTests
    {
        private static readonly DateTime T0 = new DateTime(2026, 11, 27, 9, 0, 0, DateTimeKind.Utc);

        /// <summary>1 máy người chơi (thú + tiến hóa) để chạy BattleSession.</summary>
        private sealed class Client
        {
            public readonly PetService Pets;
            public readonly EvolutionService Evo;

            public Client(int seed)
            {
                var time = new ManualTimeProvider(T0);
                var bus = new EventBus();
                var cfg = GameConfig.CreateDefault();
                Pets = new PetService(time, new SeededRandom((ulong)seed), new InMemorySaveService(new NewtonsoftSerializer()), bus, cfg);
                Pets.LoadOrCreate("Kem" + seed);
                var health = new HealthService(Pets, time, cfg);
                Evo = new EvolutionService(Pets, health, time, bus, cfg);
            }
        }

        private static FoundMsg Found(FighterStats a, FighterStats b, int side, ulong seed, int durationMs, int maskA = 15, int maskB = 15) =>
            new FoundMsg
            {
                T = MsgType.Found,
                MatchId = "m-test",
                Seed = seed,
                Side = side,
                DurationMs = durationMs,
                Players = new[]
                {
                    new NetPlayer { PlayerId = "pA", Name = "A", PetName = "Mun", Stats = NetStats.From(a), LearnedMask = maskA },
                    new NetPlayer { PlayerId = "pB", Name = "B", PetName = "Bơ", Stats = NetStats.From(b), LearnedMask = maskB },
                },
            };

        private static EndMsg End(ServerMatch m) =>
            new EndMsg { T = MsgType.End, MatchId = "m-test", Outcome = (int)m.Outcome, EndTick = m.Tick, Reason = m.Reason };

        // ------------------------------------------------------------------
        // Giao thức
        // ------------------------------------------------------------------

        [Test]
        public void FloatBits_RoundTripExactly()
        {
            foreach (float v in new[] { 0f, 1f, -1f, 0.1f, 123.456f, float.Epsilon, float.MaxValue, 1f / 3f, -0f })
                Assert.AreEqual(NetProto.Bits(v), NetProto.Bits(NetProto.Float(NetProto.Bits(v))));
            Assert.AreEqual(1f / 3f, NetProto.Float(NetProto.Bits(1f / 3f)));
            Assert.IsTrue(float.IsNaN(NetProto.Float(NetProto.Bits(float.NaN))));
        }

        [Test]
        public void Protocol_JsonRoundTrip_KeepsSeedAndExactStats()
        {
            var stats = new FighterStats(123.456f, 7.7f, 8.8f, 9.9f, 5.5f);
            FoundMsg m = Found(stats, stats, 1, ulong.MaxValue - 5, 90000);
            string json = NetProto.Write(m);
            Assert.AreEqual(MsgType.Found, NetProto.Read<NetMsg>(json).T, "đọc loại tin trước");
            FoundMsg back = NetProto.Read<FoundMsg>(json);
            Assert.AreEqual(ulong.MaxValue - 5, back.Seed, "seed 64 bit không mất số");
            Assert.AreEqual(NetProto.Bits(123.456f), NetProto.Bits(back.Players[0].Stats.ToStats().Hp), "chỉ số đúng từng bit");
            Assert.AreEqual(1, back.Side);
            StringAssert.DoesNotContain("\n", json, "tin WebSocket viết gọn 1 dòng");
        }

        [Test]
        public void Swipe_FromNetwork_IsSanitized()
        {
            SwipeResult s = SwipeMsg.ToSwipe(99, NetProto.Bits(float.NaN), 7);
            Assert.AreEqual(SwipeKind.Z, s.Kind, "loại ngoài khoảng: kẹp lại");
            Assert.AreEqual(1f, s.Quality, "NaN: về 1");
            Assert.AreEqual(1, s.Direction);
            s = SwipeMsg.ToSwipe(-3, NetProto.Bits(50f), -2);
            Assert.AreEqual(SwipeKind.None, s.Kind);
            Assert.AreEqual(2f, s.Quality, "hệ số quá lớn: kẹp 2");
            Assert.AreEqual(-1, s.Direction);
            SwipeMsg wire = SwipeMsg.From(new SwipeResult { Kind = SwipeKind.V, Quality = 1.137f, Direction = -1 });
            SwipeResult back = SwipeMsg.ToSwipe(wire.Kind, wire.Q, wire.Dir);
            Assert.AreEqual(SwipeKind.V, back.Kind);
            Assert.AreEqual(1.137f, back.Quality);
        }

        [Test]
        public void TransferCode_And_Name_Rules()
        {
            Assert.AreEqual("ABCDEFGHJKLM", NetProto.NormalizeCode("abcd-efgh jklm"), "chữ thường + gạch + khoảng trắng đều được");
            Assert.IsNull(NetProto.NormalizeCode("ABCD-EFGH-IJKL"), "không có chữ I (dễ nhầm số 1)");
            Assert.IsNull(NetProto.NormalizeCode("ABCD"));
            Assert.AreEqual("ABCD-EFGH-JKLM", NetProto.FormatCode("ABCDEFGHJKLM"));
            Assert.AreEqual("Bé Na", NetProto.CleanName("  Bé   Na \n"));
            Assert.IsNull(NetProto.CleanName(" a "), "ngắn quá");
            Assert.AreEqual(NetProto.NameMax, NetProto.CleanName(new string('x', 40)).Length);
            Assert.AreEqual("ab", NetProto.CleanName("<a\u0007b>"), "bỏ ký tự điều khiển + < >");
            Assert.AreNotEqual(NetProto.Hash("a"), NetProto.Hash("b"));
            Assert.AreEqual(NetProto.Hash("game"), NetProto.Hash("game"));
        }

        [Test]
        public void ServerClock_UsesServerTime_AndIgnoresJitter()
        {
            var device = new ManualTimeProvider(T0);
            var clock = new ServerClock(device);
            Assert.AreEqual(T0, clock.UtcNow, "chưa đồng bộ: giờ máy");
            clock.Sync(T0.AddHours(-2), 0.2);   // giờ máy bị chỉnh nhanh 2 tiếng
            Assert.AreEqual(-2 * 3600 + 0.1, clock.OffsetSeconds, 1e-6);
            device.Advance(TimeSpan.FromMinutes(5));
            Assert.AreEqual(0.0, (T0.AddHours(-2).AddMinutes(5).AddSeconds(0.1) - clock.UtcNow).TotalMilliseconds, 1.0,
                            "giờ trôi theo máy, lệch theo máy chủ");
            double before = clock.OffsetSeconds;
            clock.Sync(device.UtcNow.AddSeconds(before + 0.3), 0.0);
            Assert.AreEqual(before, clock.OffsetSeconds, "lệch dưới 1 giây: giữ nguyên cho khỏi giật giờ");
            var reopened = new ServerClock(device, before);
            Assert.AreEqual(0.0, (clock.UtcNow - reopened.UtcNow).TotalMilliseconds, 1.0, "mở lại app: dùng độ lệch lần trước ngay");
        }

        // ------------------------------------------------------------------
        // Luật dùng chung (máy chủ + máy)
        // ------------------------------------------------------------------

        [Test]
        public void RankRules_RollSeasonApplyCopy()
        {
            RankConfig c = GameConfig.CreateDefault().Rank;
            var d = new RankData { Elo = 1400, Season = 0, SeasonPeakElo = 1400 };
            Assert.IsFalse(RankRules.RollSeason(d, c.SeasonEpochUtc.AddDays(45), c), "dữ liệu mới: chỉ ghi mùa");
            Assert.AreEqual(2, d.Season);
            Assert.IsTrue(RankRules.RollSeason(d, c.SeasonEpochUtc.AddDays(65), c), "qua mốc mùa 3");
            Assert.AreEqual(1200, d.Elo, "reset mềm 1000 + 400/2");
            Assert.AreEqual(RankTier.Gold, d.LastSeasonTier);
            Assert.IsFalse(RankRules.RollSeason(d, c.SeasonEpochUtc.AddDays(10), c), "đồng hồ lùi: giữ mùa");
            RankResult r = RankRules.Apply(d, 1200, 1.0, c);
            Assert.AreEqual(1216, r.EloAfter);
            Assert.AreEqual(1, d.Wins);
            var copy = new RankData();
            RankRules.Copy(d, copy);
            Assert.AreEqual(d.Elo, copy.Elo);
            Assert.AreEqual(d.Season, copy.Season);
            Assert.AreEqual(d.Wins, copy.Wins);
        }

        [Test]
        public void GhostSearch_FindsGhostOnlyAfterDelay()
        {
            GameConfig cfg = GameConfig.CreateDefault();
            var search = new GhostSearch(new SeededRandom(3), cfg);
            var mine = new MatchTicket
            {
                PlayerId = "me", Elo = 1000, Power = 300f, Stage = EvolutionStage.Child, QueuedUtc = T0,
                Stats = new FighterStats(150, 15, 15, 15, 5),
            };
            search.Start(mine, T0, 8.0);
            Assert.IsTrue(search.Active);
            Assert.IsNull(search.Update(T0.AddSeconds(8.0)), "máy chủ chờ người thật trước: chưa thả người chơi ảo");
            OpponentInfo o = null;
            DateTime t = T0.AddSeconds(8.0);
            for (int i = 0; i < 1200 && o == null; i++)
            {
                t = t.AddSeconds(0.1);
                o = search.Update(t);
            }
            Assert.NotNull(o, "vẫn tìm được người chơi ảo");
            Assert.IsTrue(o.IsGhost);
            Assert.AreEqual(EvolutionStage.Child, o.Stage);
            Assert.IsFalse(search.Active, "ghép xong tự dừng");
            search.Start(mine, t);
            search.Stop();
            Assert.IsNull(search.Update(t.AddMinutes(5)), "dừng rồi thì không ghép nữa");
        }

        [Test]
        public void Simulation_Mirrored_WithTieBreak_MatchesExactly()
        {
            var a = new FighterStats(140f, 13f, 9f, 12f, 30f);
            var b = new FighterStats(150f, 12f, 10f, 12f, 30f);   // SPD bằng nhau + chí mạng cao: thứ tự tính trong 1 bước quyết định
            for (ulong seed = 1; seed <= 20; seed++)
            {
                var server = new BattleSimulation(a, b, null, seed);
                var mirror = new BattleSimulation(b, a, null, seed, null, null, 1);
                for (int tick = 0; tick < 3000 && !server.IsOver; tick++)
                {
                    if (tick % 25 == 0)   // 2 bên ra đòn cùng bước => trúng cùng bước
                    {
                        var s = new SwipeResult { Kind = tick % 50 == 0 ? SwipeKind.Horizontal : SwipeKind.Up, Quality = 1.1f };
                        server.Submit(0, s);
                        server.Submit(1, s);
                        mirror.Submit(1, s);
                        mirror.Submit(0, s);
                    }
                    server.Tick();
                    mirror.Tick();
                }
                Assert.AreEqual(NetProto.Bits(server.Fighters[0].Hp), NetProto.Bits(mirror.Fighters[1].Hp), "seed " + seed);
                Assert.AreEqual(NetProto.Bits(server.Fighters[1].Hp), NetProto.Bits(mirror.Fighters[0].Hp), "seed " + seed);
                Assert.AreEqual(server.Outcome, FeedSides.Local(mirror.Outcome, 1), "seed " + seed);
            }
        }

        // ------------------------------------------------------------------
        // Trận theo nhịp máy chủ: máy chủ (ServerMatch) + 2 máy (BattleSession + NetMatchFeed)
        // ------------------------------------------------------------------

        /// <summary>Đường truyền giả: nét vuốt của máy → đổi thành tin mạng → máy chủ (đúng như qua WebSocket).</summary>
        private static Action<SwipeResult> Wire(ServerMatch server, int side) => s =>
        {
            SwipeMsg m = NetProto.Read<SwipeMsg>(NetProto.Write(SwipeMsg.From(s)));
            server.Submit(side, SwipeMsg.ToSwipe(m.Kind, m.Q, m.Dir));
        };

        private static void Deliver(NetMatchFeed feed, NetMsg m)
        {
            string json = NetProto.Write(m);   // đi qua JSON như thật
            if (m is FrameMsg) feed.ApplyFrame(NetProto.Read<FrameMsg>(json));
            else if (m is EndMsg) feed.ApplyEnd(NetProto.Read<EndMsg>(json));
            else feed.ApplyStart();
        }

        [Test]
        public void Lockstep_TwoClients_ReplayServerMatch_BitExact()
        {
            var a = new FighterStats(160f, 14f, 9f, 11f, 25f);
            var b = new FighterStats(150f, 13f, 10f, 11f, 25f);   // SPD bằng nhau: máy bên 1 phải tính đúng thứ tự như máy chủ
            const ulong seed = 0xC0FFEE1234UL;
            var server = new ServerMatch(a, b, BotPlayers.Learned(15), BotPlayers.Learned(7), seed, new BattleConfig { DurationSeconds = 20f });
            var ca = new Client(1);
            var cb = new Client(2);
            NetMatchFeed fa = null, fb = null;
            fa = new NetMatchFeed(Found(a, b, 0, seed, 20000, 15, 7), null, Wire(server, 0));
            fb = new NetMatchFeed(Found(a, b, 1, seed, 20000, 15, 7), null, Wire(server, 1));
            var sa = new BattleSession(ca.Pets, ca.Evo, new RankedOpponent { Name = "Bơ" }, fa);
            var sb = new BattleSession(cb.Pets, cb.Evo, new RankedOpponent { Name = "Mun" }, fb);
            Assert.AreEqual(NetProto.Bits(b.Hp), NetProto.Bits(sb.Sim.Fighters[0].Hp), "máy bên 1: mình (B) đứng bên 0 trên màn hình");
            Assert.AreEqual(20f, sb.Sim.Config.DurationSeconds, "thời lượng theo máy chủ");

            Deliver(fa, new MatchMsg { T = MsgType.Start });
            Deliver(fb, new MatchMsg { T = MsgType.Start });
            const float dt = 1f / 30f;
            for (int i = 0; i < 2000 && !server.IsOver; i++)
            {
                // 2 người chơi vuốt theo nhịp khác nhau (có lúc cùng bước), máy gửi lên máy chủ
                if (i % 20 == 0) sa.PlayerSwipe(new SwipeResult { Kind = i % 40 == 0 ? SwipeKind.Horizontal : SwipeKind.Up, Quality = 1.05f + (i % 7) * 0.01f });
                if (i % 20 == 0 || i % 33 == 5) sb.PlayerSwipe(new SwipeResult { Kind = i % 3 == 0 ? SwipeKind.Down : SwipeKind.Horizontal, Quality = 0.93f });
                if (i % 97 == 50) sb.PlayerSwipe(SwipeResult.Wrong("vuốt sai"));
                server.Step();
                FrameMsg f = server.Frame();
                Deliver(fa, f);
                Deliver(fb, f);
                sa.Advance(dt);
                sb.Advance(dt);
            }
            Assert.IsTrue(server.IsOver, "trận ở máy chủ kết thúc");
            EndMsg end = End(server);
            Deliver(fa, end);
            Deliver(fb, end);
            for (int i = 0; i < 20 && !(sa.IsOver && sb.IsOver); i++)
            {
                sa.Advance(dt);
                sb.Advance(dt);
            }
            Assert.IsTrue(sa.IsOver && sb.IsOver, "2 máy chạy hết phần đệm rồi kết thúc");
            Assert.AreEqual(server.Tick, sa.TicksPlayed, "máy A chạy đủ số bước của máy chủ");
            Assert.AreEqual(server.Tick, sb.TicksPlayed);
            Assert.AreEqual(0, sa.HpFixes, "máu khớp máy chủ từng bit, không phải sửa");
            Assert.AreEqual(0, sb.HpFixes);
            Assert.AreEqual(NetProto.Bits(server.Sim.Fighters[0].Hp), NetProto.Bits(sa.Sim.Fighters[0].Hp));
            Assert.AreEqual(NetProto.Bits(server.Sim.Fighters[1].Hp), NetProto.Bits(sa.Sim.Fighters[1].Hp));
            Assert.AreEqual(NetProto.Bits(server.Sim.Fighters[1].Hp), NetProto.Bits(sb.Sim.Fighters[0].Hp), "máy B thấy mình bên 0");
            Assert.AreEqual(NetProto.Bits(server.Sim.Fighters[0].Hp), NetProto.Bits(sb.Sim.Fighters[1].Hp));
            Assert.AreEqual(server.Outcome, sa.Result.Outcome);
            Assert.AreEqual(FeedSides.Local(server.Outcome, 1), sb.Result.Outcome);
            Assert.Greater(server.Sim.Fighters[0].Stats.Hp - server.Sim.Fighters[0].Hp + server.Sim.Fighters[1].Stats.Hp - server.Sim.Fighters[1].Hp, 0f,
                           "trận có đánh trúng thật");
        }

        [Test]
        public void Lockstep_ServerBot_PlaysLikeLocalBot_AndClientReplays()
        {
            var me = new FighterStats(150f, 13f, 10f, 12f, 8f);
            var bot = new FighterStats(150f, 13f, 10f, 11f, 8f);
            const ulong seed = 77UL;
            var server = new ServerMatch(me, bot, null, null, seed, new BattleConfig { DurationSeconds = 15f }, null, BotConfig.Persona(0.7f, BotStyle.Technical));
            Assert.IsTrue(server.IsBot(1));
            Assert.IsFalse(server.Submit(1, new SwipeResult { Kind = SwipeKind.Up, Quality = 1f }), "không ai gửi thay người chơi ảo được");
            var c = new Client(3);
            var feed = new NetMatchFeed(Found(me, bot, 0, seed, 15000), null, Wire(server, 0));
            var s = new BattleSession(c.Pets, c.Evo, new RankedOpponent { Name = "Tôm" }, feed);
            feed.ApplyStart();
            int framesWithBot = 0;
            for (int i = 0; i < 1000 && !server.IsOver; i++)
            {
                if (i % 30 == 0) s.PlayerSwipe(new SwipeResult { Kind = SwipeKind.Horizontal, Quality = 1f });
                server.Step();
                FrameMsg f = server.Frame();
                if (f.I != null)
                    for (int k = 1; k < f.I.Length; k += 5)
                        if (f.I[k] == 1) framesWithBot++;
                Deliver(feed, f);
                s.Advance(1f / 30f);
            }
            Deliver(feed, End(server));
            for (int i = 0; i < 10 && !s.IsOver; i++) s.Advance(1f / 30f);
            Assert.Greater(framesWithBot, 3, "người chơi ảo trên máy chủ có đánh");
            Assert.AreEqual(0, s.HpFixes);
            Assert.AreEqual(NetProto.Bits(server.Sim.Fighters[1].Hp), NetProto.Bits(s.Sim.Fighters[1].Hp));
            Assert.AreEqual(server.Outcome, s.Result.Outcome);
        }

        [Test]
        public void Lockstep_Forfeit_ServerOutcomeWins_AndLostConnectionIsLoss()
        {
            var st = new FighterStats(150f, 13f, 10f, 12f, 5f);
            var server = new ServerMatch(st, st, null, null, 5UL);
            var c = new Client(4);
            var feed = new NetMatchFeed(Found(st, st, 0, 5UL, 90000), null, Wire(server, 0));
            var s = new BattleSession(c.Pets, c.Evo, new RankedOpponent { Name = "X" }, feed);
            Assert.IsTrue(s.WaitingForServer, "chưa có tin bắt đầu: chờ");
            Assert.IsFalse(s.PlayerSwipe(new SwipeResult { Kind = SwipeKind.Up, Quality = 1f }), "chưa bắt đầu: không gửi");
            feed.ApplyStart();
            for (int i = 0; i < 40; i++)
            {
                server.Step();
                Deliver(feed, server.Frame());
                s.Advance(1f / 30f);
            }
            server.Forfeit(1);   // đối thủ thoát giữa trận
            Assert.AreEqual(BattleOutcome.Win0, server.Outcome);
            Assert.AreEqual("left", server.Reason);
            Deliver(feed, End(server));
            for (int i = 0; i < 10 && !s.IsOver; i++) s.Advance(1f / 30f);
            Assert.IsTrue(s.IsOver);
            Assert.IsTrue(s.Result.PlayerWon, "máu ngang nhau nhưng đối thủ bỏ cuộc: mình thắng theo máy chủ");

            var server2 = new ServerMatch(st, st, null, null, 6UL);
            var feed2 = new NetMatchFeed(Found(st, st, 1, 6UL, 90000), null, Wire(server2, 1));
            var c2 = new Client(5);
            var s2 = new BattleSession(c2.Pets, c2.Evo, new RankedOpponent { Name = "Y" }, feed2);
            feed2.ApplyStart();
            server2.Step();
            Deliver(feed2, server2.Frame());
            feed2.MarkLost();
            s2.Advance(0.1f);
            Assert.IsTrue(s2.IsOver, "mất kết nối giữa trận: kết thúc");
            Assert.IsFalse(s2.Result.PlayerWon, "tính thua (máy chủ cũng tính bỏ cuộc)");
        }

        [Test]
        public void Lockstep_KeepsBuffer_AndCatchesUpAfterStall()
        {
            var st = new FighterStats(150f, 13f, 10f, 12f, 5f);
            var server = new ServerMatch(st, st, null, null, 9UL);
            var c = new Client(6);
            var feed = new NetMatchFeed(Found(st, st, 0, 9UL, 90000));
            var cfg = new BattleSessionConfig();
            var s = new BattleSession(c.Pets, c.Evo, new RankedOpponent { Name = "Z" }, feed, null, cfg);
            feed.ApplyStart();
            for (int i = 0; i < 30; i++)
            {
                server.Step();
                Deliver(feed, server.Frame());
                s.Advance(1f / 30f);
            }
            Assert.AreEqual(30 - cfg.NetBufferTicks, s.TicksPlayed, "chạy chậm hơn máy chủ đúng số bước đệm");
            for (int i = 0; i < 25; i++)   // mạng nghẽn: 25 bước dồn lại tới cùng lúc
            {
                server.Step();
                Deliver(feed, server.Frame());
            }
            s.Advance(1f / 30f);
            Assert.AreEqual(55 - cfg.NetBufferTicks, s.TicksPlayed, "tụt xa: chạy dồn cho kịp ngay (vẫn giữ đệm)");
            s.Advance(1.0f);   // không có bước mới: đứng chờ, không dồn thời gian
            Assert.AreEqual(55 - cfg.NetBufferTicks, s.TicksPlayed);
            server.Step();
            Deliver(feed, server.Frame());
            s.Advance(1f / 30f);
            Assert.AreEqual(56 - cfg.NetBufferTicks, s.TicksPlayed, "chờ lâu xong không chạy vọt");
        }
    }
}
