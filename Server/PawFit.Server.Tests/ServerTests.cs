using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Online;
using PawFit.Pet;

namespace PawFit.Server.Tests
{
    /// <summary>Máy chủ thật chạy trong tiến trình test (cổng ngẫu nhiên, file dữ liệu tạm).</summary>
    public sealed class LiveServer : IDisposable
    {
        public readonly WebApplication App;
        public readonly string BaseUrl;
        private readonly string _dir;

        public LiveServer(Action<ServerOptions> configure = null)
        {
            _dir = Path.Combine(Path.GetTempPath(), "pawfit-test-" + Guid.NewGuid().ToString("N"));
            App = ServerApp.Build(new[] { "--urls", "http://127.0.0.1:0", "--Logging:LogLevel:Default=Warning" }, o =>
            {
                o.DbPath = Path.Combine(_dir, "test.db");
                configure?.Invoke(o);
            });
            App.StartAsync().GetAwaiter().GetResult();
            BaseUrl = App.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>().Addresses.First();
        }

        public GameStore Store => App.Services.GetRequiredService<GameStore>();

        public void Dispose()
        {
            App.StopAsync().GetAwaiter().GetResult();
            ((IDisposable)App).Dispose();
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_dir, true);
            }
            catch (Exception)
            {
                // file tạm: xóa không được cũng không sao
            }
        }
    }

    /// <summary>1 "điện thoại": file lưu trong RAM + thú + rank + OnlineService thật nối máy chủ thật.</summary>
    public sealed class Device
    {
        public readonly InMemorySaveService Save;
        public readonly ServerClock Clock = new ServerClock(new SystemTimeProvider());
        public readonly ServerApi Api;
        public PetService Pets;
        public RankService Rank;
        public OnlineService Online;

        public Device(string baseUrl, string petName, InMemorySaveService save = null)
        {
            Save = save ?? new InMemorySaveService(new NewtonsoftSerializer());
            Api = new ServerApi(baseUrl, new DotNetHttpTransport());
            Boot(petName);
        }

        /// <summary>Mở game (lần đầu hoặc mở lại sau khi nhập mã chuyển máy).</summary>
        public void Boot(string petName)
        {
            OnlineService.ApplyPendingRestore(Save);
            var bus = new EventBus();
            GameConfig cfg = GameConfig.CreateDefault();
            Pets = new PetService(Clock, new SeededRandom((ulong)Environment.TickCount64), Save, bus, cfg);
            Pets.LoadOrCreate(petName);
            Rank = new RankService(Pets, Clock, bus, cfg);
            Online = new OnlineService(Api, Save, new NewtonsoftSerializer(), Pets, Rank, Clock, bus);
        }

        public void Pump() => Online.Update(0.005f);
    }

    /// <summary>1 người chơi PvP: tài khoản + mạng PvP thật + thú để đánh.</summary>
    public sealed class Player
    {
        public readonly ServerApi Api;
        public readonly ServerNetworkManager Net;
        public readonly PetService Pets;
        public readonly EvolutionService Evo;
        public string Token;
        public string Id;
        public string Name;
        public MatchInfo Match;
        public BattleSession Session;

        public Player(string baseUrl, string petName)
        {
            Api = new ServerApi(baseUrl, new DotNetHttpTransport());
            var time = new SystemTimeProvider();
            var bus = new EventBus();
            GameConfig cfg = GameConfig.CreateDefault();
            Pets = new PetService(time, new SeededRandom((ulong)petName.GetHashCode()), new InMemorySaveService(new NewtonsoftSerializer()), bus, cfg);
            Pets.LoadOrCreate(petName);
            Evo = new EvolutionService(Pets, new HealthService(Pets, time, cfg), time, bus, cfg);
            Net = new ServerNetworkManager(Api.PvpUrl, () => Token, time, cfg, null, null, "test");
            Net.MatchFound += m => Match = m;
        }

        public void Pump()
        {
            Api.Update();
            Net.Update();
        }

        public MatchTicket Ticket(float atk = 15f) => new MatchTicket
        {
            PetName = Pets.Current.Name,
            DnaCode = Pets.DnaCode,
            Stage = EvolutionStage.Child,
            Stats = new FighterStats(150f, atk, 15f, 15f, 5f),
            LearnedMask = 3,
            Power = 0f,
        };
    }

    public class ServerTests
    {
        private static void PumpUntil(Func<bool> done, double seconds, params Action[] pumps)
        {
            var sw = Stopwatch.StartNew();
            while (!done())
            {
                if (sw.Elapsed.TotalSeconds > seconds) Assert.Fail("Quá " + seconds + " giây mà chưa xong");
                foreach (Action p in pumps) p();
                Thread.Sleep(3);
            }
        }

        private static void Login(Player p)
        {
            ApiResult<AuthReply> r = null;
            p.Api.Guest(new GuestRequest { Proto = NetProto.Version }, x => r = x);
            PumpUntil(() => r != null, 10, p.Pump);
            Assert.IsTrue(r.Ok, r.Error);
            p.Token = r.Value.Token;
            p.Id = r.Value.PlayerId;
            p.Name = r.Value.Name;
            p.Api.Token = p.Token;
        }

        private static void Connect(Player p)
        {
            bool? ok = null;
            p.Net.Connect(null, x => ok = x);
            PumpUntil(() => ok.HasValue, 10, p.Pump);
            Assert.IsTrue(ok.Value, p.Net.LastError);
            Assert.AreEqual(NetworkState.Online, p.Net.State);
        }

        private static BattleSession Enter(Player p) =>
            p.Session = new BattleSession(p.Pets, p.Evo, new RankedOpponent { Name = p.Match.Opponent.PetName }, p.Match.Feed);

        /// <summary>Đánh tới hết trận theo giờ thật: mỗi người vuốt theo nhịp riêng.</summary>
        private static void Fight(double seconds, params Player[] players)
        {
            var sw = Stopwatch.StartNew();
            double last = 0;
            int frame = 0;
            while (!players.All(p => p.Session.IsOver))
            {
                if (sw.Elapsed.TotalSeconds > seconds) Assert.Fail("Trận chưa xong sau " + seconds + " giây");
                double now = sw.Elapsed.TotalSeconds;
                float dt = (float)(now - last);
                last = now;
                frame++;
                for (int i = 0; i < players.Length; i++)
                {
                    Player p = players[i];
                    p.Pump();
                    if (frame % (9 + i * 4) == 0)
                        p.Session.PlayerSwipe(new SwipeResult { Kind = (frame / 9 + i) % 3 == 0 ? SwipeKind.Down : SwipeKind.Horizontal, Quality = 1.1f });
                    p.Session.Advance(dt);
                }
                Thread.Sleep(4);
            }
        }

        // ------------------------------------------------------------------
        // REST: tài khoản + lưu game + mã chuyển máy
        // ------------------------------------------------------------------

        [Test]
        public void Account_Save_Transfer_EndToEnd()
        {
            using (var server = new LiveServer())
            {
                var phone1 = new Device(server.BaseUrl, "Kem");
                phone1.Online.Start();
                PumpUntil(() => phone1.Online.State == OnlineState.Online && phone1.Online.Account.SaveRevision == 1, 10, phone1.Pump);
                string id = phone1.Online.PlayerId;
                StringAssert.StartsWith("p", id);
                Assert.AreEqual(13, id.Length);
                Assert.IsFalse(string.IsNullOrEmpty(phone1.Online.Name), "máy chủ đặt sẵn biệt danh");
                Assert.IsTrue(phone1.Clock.Synced, "đã lấy giờ máy chủ");
                StringAssert.Contains("\"Kem\"", server.Store.GetSave(id).Data, "file game đã lên máy chủ");
                Assert.AreEqual(1000, phone1.Rank.Elo, "rank theo máy chủ");

                bool? renamed = null;
                phone1.Online.Rename("  Đạt   Pro ", (ok, _) => renamed = ok);
                PumpUntil(() => renamed.HasValue, 10, phone1.Pump);
                Assert.IsTrue(renamed.Value);
                Assert.AreEqual("Đạt Pro", phone1.Online.Name);
                Assert.AreEqual("Đạt Pro", server.Store.GetPlayer(id).Name);

                TransferCodeReply code = null;
                phone1.Online.RequestTransferCode((c, _) => code = c);
                PumpUntil(() => code != null, 10, phone1.Pump);
                StringAssert.IsMatch("^[A-Z2-9]{4}-[A-Z2-9]{4}-[A-Z2-9]{4}$", code.Code);

                // máy mới: đang có thú khác, nhập mã => nhận tài khoản + thú Kem
                var phone2 = new Device(server.BaseUrl, "Bơ");
                phone2.Online.Start();
                PumpUntil(() => phone2.Online.State == OnlineState.Online, 10, phone2.Pump);
                Assert.AreNotEqual(id, phone2.Online.PlayerId, "máy mới có tài khoản khách riêng trước đã");
                bool? redeemed = null;
                string message = null;
                phone2.Online.RedeemTransferCode(code.Code.ToLowerInvariant(), (ok, msg) =>
                {
                    redeemed = ok;
                    message = msg;
                });
                PumpUntil(() => redeemed.HasValue, 10, phone2.Pump);
                Assert.IsTrue(redeemed.Value, message);
                StringAssert.Contains("Kem", message);
                Assert.IsTrue(phone2.Online.RestartPending, "phải mở lại game");
                Assert.AreEqual("Bơ", phone2.Pets.Current.Name, "game đang chạy chưa bị thay giữa chừng");
                phone2.Boot("Bơ");   // mở lại game
                Assert.AreEqual("Kem", phone2.Pets.Current.Name, "mở lại: thú đã chuyển sang máy mới");
                Assert.AreEqual(id, phone2.Online.PlayerId);
                phone2.Online.Start();
                PumpUntil(() => phone2.Online.State == OnlineState.Online, 10, phone2.Pump);
                Assert.AreEqual("Đạt Pro", phone2.Online.Name);

                // máy cũ mất quyền: lần tải lên sau bị 401, nối lại thì tự thành tài khoản mới (thú vẫn còn trên máy)
                phone1.Pets.Spend(NeedType.Energy, 5f);
                phone1.Online.UploadIfChanged();
                PumpUntil(() => phone1.Online.State == OnlineState.Offline, 10, phone1.Pump);
                phone1.Online.OnResume();
                PumpUntil(() => phone1.Online.State == OnlineState.Online, 10, phone1.Pump);
                Assert.AreNotEqual(id, phone1.Online.PlayerId, "máy cũ thành tài khoản khác");
                Assert.AreEqual("Kem", phone1.Pets.Current.Name);

                bool? again = null;
                string why = null;
                var phone3 = new Device(server.BaseUrl, "Y");
                phone3.Online.RedeemTransferCode(code.Code, (ok, msg) =>
                {
                    again = ok;
                    why = msg;
                });
                PumpUntil(() => again.HasValue, 10, phone3.Pump);
                Assert.IsFalse(again.Value, "mã chỉ dùng 1 lần");
                StringAssert.Contains("hết hạn", why);
            }
        }

        [Test]
        public void Save_Validation_Conflict_Auth()
        {
            using (var server = new LiveServer(o => o.MaxSaveBytes = 64 * 1024))
            {
                var p = new Player(server.BaseUrl, "Kem");
                Login(p);
                ApiResult<SaveReply> r = null;
                void Put(long rev, string data)
                {
                    r = null;
                    p.Api.PutSave(new SavePut { BaseRevision = rev, Data = data }, x => r = x);
                    PumpUntil(() => r != null, 10, p.Pump);
                }
                Put(0, "không phải JSON");
                Assert.AreEqual(400, r.Status);
                Put(0, "{\"Version\":5,\"Pet\":null}");
                Assert.AreEqual(200, r.Status);
                Assert.AreEqual(1, r.Value.Revision);
                Put(0, "{\"Version\":5}");
                Assert.AreEqual(409, r.Status, "dựa vào bản cũ: từ chối");
                Assert.AreEqual(1, r.ConflictRevision);
                Put(1, "{\"Version\":5,\"Pad\":\"" + new string('x', 70 * 1024) + "\"}");
                Assert.AreEqual(413, r.Status, "quá lớn");
                p.Api.Token = "sai-ma";
                Put(1, "{\"Version\":5}");
                Assert.AreEqual(401, r.Status);
                ApiResult<TimeReply> t = null;
                p.Api.Time(x => t = x);
                PumpUntil(() => t != null, 10, p.Pump);
                Assert.Less(Math.Abs((NetProto.FromMs(t.Value.UtcMs) - DateTime.UtcNow).TotalSeconds), 5.0);
            }
        }

        [Test]
        public void GuestAccounts_RateLimitedPerIp()
        {
            using (var server = new LiveServer(o => o.GuestPerHourPerIp = 2))
            {
                var p = new Player(server.BaseUrl, "Kem");
                int[] codes = new int[3];
                for (int i = 0; i < 3; i++)
                {
                    ApiResult<AuthReply> r = null;
                    p.Api.Guest(new GuestRequest { Proto = NetProto.Version }, x => r = x);
                    PumpUntil(() => r != null, 10, p.Pump);
                    codes[i] = r.Status;
                }
                CollectionAssert.AreEqual(new[] { 200, 200, 429 }, codes);
            }
        }

        // ------------------------------------------------------------------
        // PvP qua WebSocket
        // ------------------------------------------------------------------

        [Test]
        public void Pvp_TwoHumans_FullMatch_ClientsMatchServer()
        {
            using (var server = new LiveServer(o =>
            {
                o.BotDelaySeconds = 1000;   // chỉ người thật
                o.BattleSeconds = 3f;
            }))
            {
                var a = new Player(server.BaseUrl, "Kem");
                var b = new Player(server.BaseUrl, "Bơ");
                Login(a);
                Login(b);
                Connect(a);
                Connect(b);
                a.Net.FindMatch(a.Ticket(15f));
                b.Net.FindMatch(b.Ticket(14.5f));
                Assert.AreEqual(NetworkState.Searching, a.Net.State);
                PumpUntil(() => a.Match != null && b.Match != null, 10, a.Pump, b.Pump);
                Assert.AreEqual(a.Match.MatchId, b.Match.MatchId);
                Assert.AreNotEqual(a.Match.LocalSide, b.Match.LocalSide, "2 người 2 bên");
                Assert.AreEqual(b.Name, a.Match.Opponent.Name, "thấy đúng biệt danh đối thủ");
                Assert.AreEqual("Bơ", a.Match.Opponent.PetName);
                Assert.IsFalse(a.Match.Opponent.IsGhost);
                Assert.AreEqual(14.5f, a.Match.Opponent.Stats.Atk, "chỉ số đối thủ đúng từng bit");
                Assert.AreEqual(3f, a.Match.Feed.DurationSeconds);

                Enter(a);
                Enter(b);
                Fight(20, a, b);

                Assert.AreEqual(0, a.Session.HpFixes, "2 máy khớp máy chủ, không phải sửa máu");
                Assert.AreEqual(0, b.Session.HpFixes);
                Assert.AreEqual(a.Session.TicksPlayed, b.Session.TicksPlayed);
                Assert.AreEqual(NetProto.Bits(a.Session.Sim.Fighters[0].Hp), NetProto.Bits(b.Session.Sim.Fighters[1].Hp));
                Assert.AreEqual(NetProto.Bits(a.Session.Sim.Fighters[1].Hp), NetProto.Bits(b.Session.Sim.Fighters[0].Hp));
                Assert.AreEqual(a.Session.Result.Outcome, FeedSides.Local(b.Session.Result.Outcome, 1), "2 máy cùng kết quả");

                EndMsg ea = a.Match.Feed.End, eb = b.Match.Feed.End;
                Assert.NotNull(ea);
                Assert.NotNull(eb);
                Assert.AreEqual(1000, ea.EloBefore);
                if (a.Session.Result.Outcome == BattleOutcome.Draw)
                {
                    Assert.AreEqual(1000, ea.Rank.Elo);
                    Assert.AreEqual(0, ea.Coins);
                }
                else
                {
                    Player winner = a.Session.Result.PlayerWon ? a : b, loser = winner == a ? b : a;
                    Assert.AreEqual(1016, winner.Match.Feed.End.Rank.Elo, "thắng người ngang Elo: +16 (K = 32)");
                    Assert.AreEqual(984, loser.Match.Feed.End.Rank.Elo);
                    Assert.AreEqual(30, winner.Match.Feed.End.Coins, "thắng PvP +30 Coin");
                    Assert.AreEqual(0, loser.Match.Feed.End.Coins);
                    Assert.AreEqual(1016, server.Store.GetRank(winner.Id, DateTime.UtcNow).Elo, "máy chủ đã ghi Elo");
                    Assert.AreEqual(1, server.Store.GetRank(loser.Id, DateTime.UtcNow).Losses);
                }
                Assert.AreEqual(1, server.Store.MatchCount());
                a.Net.LeaveMatch();
                Assert.AreEqual(NetworkState.Online, a.Net.State, "hết trận: tìm trận tiếp được");
            }
        }

        [Test]
        public void Pvp_Alone_GetsServerGhost_ThatLooksHuman()
        {
            using (var server = new LiveServer(o =>
            {
                o.BotDelaySeconds = 0.0;
                o.BattleSeconds = 3f;
                o.Ghosts = new LocalNetworkConfig { GhostEverySecondsMin = 0.05, GhostEverySecondsMax = 0.2 };
            }))
            {
                var a = new Player(server.BaseUrl, "Kem");
                Login(a);
                Connect(a);
                a.Net.FindMatch(a.Ticket());
                PumpUntil(() => a.Match != null, 15, a.Pump);
                OpponentInfo o = a.Match.Opponent;
                StringAssert.IsMatch("^p[0-9a-f]{12}$", o.PlayerId, "mã người chơi ảo trông như người thật");
                Assert.IsFalse(o.IsGhost, "máy không biết đối thủ là ảo");
                Assert.IsFalse(string.IsNullOrEmpty(o.Name));
                Assert.AreEqual(EvolutionStage.Child, o.Stage, "cùng giai đoạn với thú mình");
                Assert.IsNotNull(o.Dna);
                Enter(a);
                Fight(20, a);
                Assert.AreEqual(0, a.Session.HpFixes);
                EndMsg end = a.Match.Feed.End;
                Assert.NotNull(end?.Rank, "có Elo mới");
                Assert.AreEqual(a.Session.Result.PlayerWon ? 1 : 0, end.Rank.Wins);
                Assert.AreEqual(1, server.Store.MatchCount());
            }
        }

        [Test]
        public void Pvp_Disconnect_IsForfeit()
        {
            using (var server = new LiveServer(o =>
            {
                o.BotDelaySeconds = 1000;
                o.BattleSeconds = 30f;
            }))
            {
                var a = new Player(server.BaseUrl, "Kem");
                var b = new Player(server.BaseUrl, "Bơ");
                Login(a);
                Login(b);
                Connect(a);
                Connect(b);
                a.Net.FindMatch(a.Ticket());
                b.Net.FindMatch(b.Ticket());
                PumpUntil(() => a.Match != null && b.Match != null, 10, a.Pump, b.Pump);
                Enter(a);
                Enter(b);
                a.Session.Advance(0f);   // vào màn đấu => báo sẵn sàng
                b.Session.Advance(0f);
                PumpUntil(() => a.Match.Feed.Started && b.Match.Feed.Started, 10, a.Pump, b.Pump);
                b.Net.Disconnect();   // B tắt app giữa trận
                Assert.IsTrue(b.Match.Feed.Lost);
                b.Session.Advance(0.05f);
                Assert.IsTrue(b.Session.IsOver);
                Assert.IsFalse(b.Session.Result.PlayerWon, "máy B: mất kết nối = thua");
                PumpUntil(() => a.Session.IsOver, 10, () =>
                {
                    a.Pump();
                    a.Session.Advance(0.02f);
                });
                Assert.IsTrue(a.Session.Result.PlayerWon, "A thắng vì B bỏ cuộc");
                Assert.AreEqual("left", a.Match.Feed.EndReason);
                Assert.AreEqual(1016, a.Match.Feed.End.Rank.Elo);
                Assert.AreEqual(984, server.Store.GetRank(b.Id, DateTime.UtcNow).Elo, "B vẫn bị trừ Elo");
            }
        }

        [Test]
        public void Pvp_RejectsBadToken_OldProto_AndBadTicket()
        {
            using (var server = new LiveServer())
            {
                var p = new Player(server.BaseUrl, "Kem");
                p.Token = "sai";
                bool? ok = null;
                p.Net.Connect(null, x => ok = x);
                PumpUntil(() => ok.HasValue, 10, p.Pump);
                Assert.IsFalse(ok.Value);
                StringAssert.Contains("Mã đăng nhập", p.Net.LastError);

                var raw = new ClientWebSocketTransport();
                raw.Connect(new ServerApi(server.BaseUrl, new DotNetHttpTransport()).PvpUrl);
                string error = null;
                bool closed = false;
                PumpUntil(() => closed, 10, () =>
                {
                    while (raw.TryReceive(out SocketEvent e))
                    {
                        if (e.Kind == SocketEventKind.Open) raw.Send(NetProto.Write(new HelloMsg { T = MsgType.Hello, Token = "x", Proto = 999 }));
                        else if (e.Kind == SocketEventKind.Message) error = NetProto.Read<ErrorMsg>(e.Text).Code;
                        else closed = true;
                    }
                });
                Assert.AreEqual("proto", error, "bản game khác giao thức: bị từ chối");

                Login(p);
                Connect(p);
                MatchTicket cheat = p.Ticket();
                cheat.Stats = new FighterStats(1e9f, 1e9f, 15f, 15f, 5f);
                p.Net.FindMatch(cheat);
                PumpUntil(() => p.Net.State == NetworkState.Online, 10, p.Pump);
                StringAssert.Contains("không hợp lệ", p.Net.LastError, "chỉ số vượt trần: từ chối");
            }
        }

        [Test]
        public void Pvp_SecondConnection_ReplacesFirst()
        {
            using (var server = new LiveServer())
            {
                var a = new Player(server.BaseUrl, "Kem");
                Login(a);
                Connect(a);
                var again = new Player(server.BaseUrl, "Kem") { Token = a.Token };
                Connect(again);
                PumpUntil(() => a.Net.State == NetworkState.Offline, 10, a.Pump, again.Pump);
                Assert.AreEqual(NetworkState.Online, again.Net.State, "mỗi tài khoản 1 kết nối: kết nối mới thắng");
            }
        }
    }
}
