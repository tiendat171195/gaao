using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Online;
using PawFit.Pet;

namespace PawFit.Server
{
    /// <summary>Kiểm vé tìm trận máy gửi lên: số hỏng / chỉ số vượt trần của giai đoạn đó = từ chối. Lực chiến máy chủ tự tính.</summary>
    public static class TicketRules
    {
        public static bool Validate(NetTicket t, GameConfig cfg, out FighterStats stats, out float power, out string error)
        {
            stats = default;
            power = 0f;
            error = null;
            if (t?.Stats == null)
            {
                error = "Thiếu chỉ số thú";
                return false;
            }
            if (t.Stage < 0 || t.Stage > (int)EvolutionStage.Ultimate)
            {
                error = "Giai đoạn tiến hóa không hợp lệ";
                return false;
            }
            StatsConfig c = cfg.Stats;
            FighterStats s = t.Stats.ToStats();
            // trần = giai đoạn đó + dạng mạnh nhất (+25%) + dư 5%; bệnh chỉ làm giảm nên không có sàn trên 0
            float k = c.StageMultiplier[Math.Min(t.Stage, c.StageMultiplier.Length - 1)] * (1f + c.FormBonus) * 1.05f;
            if (Bad(s.Hp, c.BaseHp * k) || s.Hp <= 0f || Bad(s.Atk, c.BaseAtk * k) || Bad(s.Def, c.BaseDef * k) || Bad(s.Spd, c.BaseSpd * k) ||
                Bad(s.Crit, c.BaseCrit + c.ShadowCritBonus + 1f))
            {
                error = "Chỉ số thú không hợp lệ";
                return false;
            }
            stats = s;
            power = StatsRules.Power(new PetStats { Hp = s.Hp, Atk = s.Atk, Def = s.Def, Spd = s.Spd, Crit = s.Crit }, c);
            return true;
        }

        private static bool Bad(float v, float max) => float.IsNaN(v) || float.IsInfinity(v) || v < 0f || v > max;
    }

    /// <summary>
    /// Tìm trận + chạy trận PvP trên 1 luồng duy nhất (không cần khóa): mọi tin từ kết nối được xếp hàng (<see cref="Post"/>)
    /// rồi xử lý trong vòng lặp ~200 lần / giây.
    ///  * Ghép người thật với nhau trước (đúng luật Matchmaker: ±100 Elo / ±10% lực chiến, 30 giây nới).
    ///  * Chờ <see cref="ServerOptions.BotDelaySeconds"/> giây không có ai thì người chơi ảo bắt đầu tới hàng của người đó
    ///    (<see cref="GhostSearch"/>, như mạng giả lập), người chơi ảo đánh ngay trên máy chủ bằng BattleBot.
    ///  * Mỗi trận là 1 <see cref="ServerMatch"/>, chạy 30 bước / giây, gửi nhịp trận cho 2 máy; hết trận tính Elo bằng RankRules.
    /// </summary>
    public sealed class MatchService : BackgroundService
    {
        private sealed class Seat
        {
            public PvpConnection Conn;
            public string PlayerId;
            public MatchTicket Ticket;
            public GhostSearch Ghosts;
            public Room Room;
            public int Side;
        }

        private sealed class Room
        {
            public string Id;
            public ServerMatch Match;
            public readonly Seat[] Seats = new Seat[2];
            public readonly bool[] Ready = new bool[2];
            public readonly int[] EloBefore = new int[2];
            public readonly string[] Ids = new string[2];
            public readonly bool[] Human = new bool[2];
            public bool Started;
            public double CreatedAt;
            public double Accum;
            public DateTime StartedUtc;
        }

        private readonly ConcurrentQueue<Action> _inbox = new ConcurrentQueue<Action>();
        private readonly GameStore _store;
        private readonly GameConfig _cfg;
        private readonly ServerOptions _opt;
        private readonly ITimeProvider _time;
        private readonly ILogger<MatchService> _log;
        private readonly Matchmaker _humans;
        private readonly Dictionary<long, Seat> _seats = new Dictionary<long, Seat>();
        private readonly Dictionary<string, Seat> _byPlayer = new Dictionary<string, Seat>();
        private readonly List<Room> _rooms = new List<Room>();
        private readonly SeededRandom _rng;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private double _last, _lastMatchmake;
        private int _roomCount, _searching;

        public MatchService(GameStore store, GameConfig cfg, ServerOptions opt, ITimeProvider time, ILogger<MatchService> log)
        {
            _store = store;
            _cfg = cfg;
            _opt = opt;
            _time = time;
            _log = log;
            _humans = new Matchmaker(cfg.Matchmaking);
            _rng = new SeededRandom(BitConverter.ToUInt64(RandomNumberGenerator.GetBytes(8), 0));
        }

        /// <summary>Số trận đang đánh / số người đang tìm (đọc từ luồng khác chỉ để xem).</summary>
        public int Rooms => Volatile.Read(ref _roomCount);
        public int Searching => Volatile.Read(ref _searching);

        /// <summary>Xếp 1 việc vào luồng trận (gọi được từ mọi luồng).</summary>
        public void Post(Action a) => _inbox.Enqueue(a);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(5)))
            {
                try
                {
                    while (await timer.WaitForNextTickAsync(stoppingToken)) Pump();
                }
                catch (OperationCanceledException)
                {
                    // máy chủ tắt
                }
            }
        }

        private void Pump()
        {
            while (_inbox.TryDequeue(out Action a))
            {
                try
                {
                    a();
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Lỗi xử lý tin PvP");
                }
            }
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.25, now - _last);   // máy chủ khựng lâu thì không chạy dồn quá nhiều
            _last = now;
            if (now - _lastMatchmake >= 0.25)
            {
                _lastMatchmake = now;
                try
                {
                    Matchmake(now);
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Lỗi ghép trận");
                }
            }
            for (int i = _rooms.Count - 1; i >= 0; i--)
            {
                Room room = _rooms[i];
                try
                {
                    Advance(room, dt, now);
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Lỗi trận {Id}", room.Id);
                    Abort(room);
                }
            }
            int searching = 0;
            foreach (Seat s in _seats.Values)
                if (s.Ticket != null) searching++;
            Volatile.Write(ref _searching, searching);
            Volatile.Write(ref _roomCount, _rooms.Count);
        }

        // ------------------------------------------------------------------
        // Tin từ kết nối (chạy trên luồng trận)
        // ------------------------------------------------------------------

        internal void OnConnected(PvpConnection conn)
        {
            if (conn.IsClosed) return;
            if (_byPlayer.TryGetValue(conn.Player.Id, out Seat old) && old.Conn != conn) Drop(old);   // kết nối cũ: bỏ cuộc trận đang đánh
            var seat = new Seat
            {
                Conn = conn,
                PlayerId = conn.Player.Id,
                Ghosts = new GhostSearch(new SeededRandom(((ulong)_rng.NextUInt() << 32) | _rng.NextUInt()), _cfg, _opt.Ghosts),
            };
            _seats[conn.Id] = seat;
            _byPlayer[seat.PlayerId] = seat;
        }

        internal void OnDisconnected(PvpConnection conn)
        {
            if (_seats.TryGetValue(conn.Id, out Seat s)) Drop(s);
        }

        internal void OnQueue(PvpConnection conn, NetTicket t)
        {
            if (!_seats.TryGetValue(conn.Id, out Seat s)) return;
            if (s.Room != null)
            {
                conn.Send(PvpHub.Error("state", "Đang trong trận"));
                return;
            }
            if (!TicketRules.Validate(t, _cfg, out FighterStats stats, out float power, out string error))
            {
                conn.Send(PvpHub.Error("bad", error));
                return;
            }
            DateTime utc = _time.UtcNow;
            RankData rank = _store.GetRank(s.PlayerId, utc);
            PlayerRow p = _store.GetPlayer(s.PlayerId);
            StopSearch(s);
            s.Ticket = new MatchTicket
            {
                PlayerId = s.PlayerId,
                Name = p?.Name ?? conn.Player.Name,
                PetName = NetProto.CleanName(t.PetName) ?? "Thú",
                DnaCode = DnaCodec.TryDecode(t.DnaCode, out _) ? t.DnaCode : null,
                Elo = rank?.Elo ?? _cfg.Rank.StartElo,
                Power = power,
                Stage = (EvolutionStage)t.Stage,
                Form = (AdultForm)FeedSides.Clamp(t.Form, 0, (int)AdultForm.Shadow),
                Accessory = (AccessoryKind)FeedSides.Clamp(t.Accessory, 0, (int)AccessoryKind.Crown),
                Stats = stats,
                LearnedMask = t.LearnedMask & 15,
                QueuedUtc = utc,
            };
            _humans.Enqueue(s.Ticket);
            s.Ghosts.Start(s.Ticket, utc, _opt.BotDelaySeconds);
            conn.Send(new QueuedMsg { T = MsgType.Queued, Elo = s.Ticket.Elo });
        }

        internal void OnCancel(PvpConnection conn)
        {
            if (_seats.TryGetValue(conn.Id, out Seat s)) StopSearch(s);
        }

        internal void OnReady(PvpConnection conn, string matchId)
        {
            if (_seats.TryGetValue(conn.Id, out Seat s) && s.Room != null && s.Room.Id == matchId) s.Room.Ready[s.Side] = true;
        }

        internal void OnSwipe(PvpConnection conn, SwipeMsg m)
        {
            if (!_seats.TryGetValue(conn.Id, out Seat s) || s.Room == null || !s.Room.Started) return;
            s.Room.Match.Submit(s.Side, SwipeMsg.ToSwipe(m.Kind, m.Q, m.Dir));
        }

        /// <summary>Bỏ cuộc: thua ngay, rời phòng luôn (tìm trận mới được liền).</summary>
        internal void OnLeave(PvpConnection conn)
        {
            if (_seats.TryGetValue(conn.Id, out Seat s)) LeaveRoom(s);
        }

        // ------------------------------------------------------------------

        private void Drop(Seat s)
        {
            _seats.Remove(s.Conn.Id);
            if (_byPlayer.TryGetValue(s.PlayerId, out Seat cur) && cur == s) _byPlayer.Remove(s.PlayerId);
            StopSearch(s);
            LeaveRoom(s);
        }

        private void LeaveRoom(Seat s)
        {
            Room room = s.Room;
            if (room == null) return;
            if (!room.Match.IsOver) room.Match.Forfeit(s.Side);
            room.Seats[s.Side] = null;   // Elo vẫn tính (theo mã người chơi), chỉ không gửi tin nữa
            s.Room = null;
        }

        private void StopSearch(Seat s)
        {
            if (s.Ticket == null) return;
            _humans.Cancel(s.PlayerId);
            s.Ghosts.Stop();
            s.Ticket = null;
        }

        private void Matchmake(double now)
        {
            DateTime utc = _time.UtcNow;
            foreach (MatchPair pair in _humans.Tick(utc))   // người thật với nhau trước
            {
                if (!_byPlayer.TryGetValue(pair.A.PlayerId, out Seat a) || !_byPlayer.TryGetValue(pair.B.PlayerId, out Seat b)) continue;
                a.Ghosts.Stop();
                b.Ghosts.Stop();
                a.Ticket = null;
                b.Ticket = null;
                CreateRoom(a, pair.A, b, pair.B, null, now);
            }
            foreach (Seat s in new List<Seat>(_seats.Values))   // chờ lâu: người chơi ảo
            {
                if (s.Ticket == null || s.Room != null) continue;
                OpponentInfo ghost = s.Ghosts.Update(utc);
                if (ghost == null) continue;
                MatchTicket mine = s.Ticket;
                _humans.Cancel(s.PlayerId);
                s.Ticket = null;
                CreateRoom(s, mine, null, null, ghost, now);
            }
        }

        private void CreateRoom(Seat a, MatchTicket ta, Seat b, MatchTicket tb, OpponentInfo ghost, double now)
        {
            var room = new Room { Id = "m" + _rng.NextUInt().ToString("x8") + _rng.NextUInt().ToString("x8"), CreatedAt = now };
            ulong seed = ((ulong)_rng.NextUInt() << 32) | _rng.NextUInt();
            int sideA = _rng.Next(2);   // ai bên 0 cũng được: máy nào cũng thấy mình đứng bên trái
            var players = new NetPlayer[2];
            var stats = new FighterStats[2];
            var learned = new bool[2][];
            var bots = new BotConfig[2];

            void PlaceHuman(int side, Seat seat, MatchTicket t)
            {
                players[side] = new NetPlayer
                {
                    PlayerId = t.PlayerId, Name = t.Name, PetName = t.PetName, DnaCode = t.DnaCode, Elo = t.Elo, Power = t.Power,
                    Stage = (int)t.Stage, Form = (int)t.Form, Accessory = (int)t.Accessory, Stats = NetStats.From(t.Stats), LearnedMask = t.LearnedMask,
                };
                stats[side] = t.Stats;
                learned[side] = BotPlayers.Learned(t.LearnedMask);
                room.Seats[side] = seat;
                room.Ids[side] = t.PlayerId;
                room.EloBefore[side] = t.Elo;
                room.Human[side] = true;
                seat.Room = room;
                seat.Side = side;
            }

            PlaceHuman(sideA, a, ta);
            int other = 1 - sideA;
            if (b != null) PlaceHuman(other, b, tb);
            else
            {
                players[other] = new NetPlayer
                {
                    PlayerId = Disguise(ghost.PlayerId), Name = ghost.Name, PetName = ghost.PetName, DnaCode = ghost.DnaCode, Elo = ghost.Elo,
                    Power = ghost.Power, Stage = (int)ghost.Stage, Form = (int)ghost.Form, Accessory = (int)ghost.Accessory,
                    Stats = NetStats.From(ghost.Stats), LearnedMask = ghost.LearnedMask,
                };
                stats[other] = ghost.Stats;
                learned[other] = BotPlayers.Learned(ghost.LearnedMask);
                bots[other] = BotPlayers.BotFor(ghost);
                room.Ids[other] = ghost.PlayerId;
                room.EloBefore[other] = ghost.Elo;
                room.Ready[other] = true;   // người chơi ảo luôn sẵn sàng
            }
            room.Match = new ServerMatch(stats[0], stats[1], learned[0], learned[1], seed,
                                         new BattleConfig { DurationSeconds = _opt.BattleSeconds }, bots[0], bots[1]);
            _rooms.Add(room);
            var found = new FoundMsg
            {
                T = MsgType.Found, MatchId = room.Id, Seed = seed, DurationMs = (int)Math.Round(_opt.BattleSeconds * 1000f), Players = players,
            };
            for (int side = 0; side < 2; side++)
            {
                if (room.Seats[side] == null) continue;
                found.Side = side;
                room.Seats[side].Conn.Send(NetProto.Write(found));
            }
            _log.LogInformation("Trận {Id}: {A} (Elo {EA}) vs {B} (Elo {EB}){Bot}", room.Id, players[0].Name, players[0].Elo, players[1].Name,
                                players[1].Elo, b == null ? " · người chơi ảo" : "");
        }

        private void Advance(Room room, double dt, double now)
        {
            if (!room.Started)
            {
                if (!(room.Ready[0] && room.Ready[1]) && now - room.CreatedAt < _opt.ReadyTimeoutSeconds && !room.Match.IsOver) return;
                room.Started = true;
                room.StartedUtc = _time.UtcNow;
                room.Accum = 0.0;
                Broadcast(room, new MatchMsg { T = MsgType.Start, MatchId = room.Id });
                if (!room.Match.IsOver) return;
            }
            if (!room.Match.IsOver)
            {
                double tick = room.Match.Sim.Config.TickSeconds;
                room.Accum += dt;
                int steps = 0;
                while (room.Accum >= tick && !room.Match.IsOver && steps < 8)
                {
                    room.Accum -= tick;
                    steps++;
                    room.Match.Step();
                    if (room.Match.Tick % Math.Max(1, _opt.FrameEveryTicks) == 0 || room.Match.IsOver) Broadcast(room, room.Match.Frame());
                }
            }
            if (room.Match.IsOver) End(room);
        }

        /// <summary>Hết trận: tính Elo cho bên là người thật (RankRules, như trên máy), ghi dữ liệu, báo 2 máy.</summary>
        private void End(Room room)
        {
            _rooms.Remove(room);
            ServerMatch m = room.Match;
            Broadcast(room, m.Frame());   // chốt nốt nét vuốt còn lại
            DateTime utc = _time.UtcNow;
            var ranks = new RankData[2];
            var before = new int[2];
            var coins = new int[2];
            for (int side = 0; side < 2; side++)
            {
                before[side] = room.EloBefore[side];
                if (!room.Human[side]) continue;
                RankData rank = _store.GetRank(room.Ids[side], utc);
                if (rank == null) continue;
                double score = RankRules.Score(m.Outcome, side);
                RankResult r = RankRules.Apply(rank, room.EloBefore[1 - side], score, _cfg.Rank);
                ranks[side] = rank;
                before[side] = r.EloBefore;
                coins[side] = score >= 1.0 ? _cfg.Economy.PvpWinCoins : 0;
            }
            var rec = new MatchRecord
            {
                Id = room.Id,
                StartedUtc = room.Started ? room.StartedUtc : utc,
                EndedUtc = utc,
                AId = room.Ids[0],
                BId = room.Ids[1],
                Outcome = (int)m.Outcome,
                Reason = m.Reason ?? "?",
                Ticks = m.Tick,
                AEloBefore = before[0],
                AEloAfter = ranks[0]?.Elo ?? before[0],
                BEloBefore = before[1],
                BEloAfter = ranks[1]?.Elo ?? before[1],
            };
            try
            {
                _store.FinishMatch(rec, room.Human[0] ? room.Ids[0] : null, ranks[0], room.Human[1] ? room.Ids[1] : null, ranks[1]);
            }
            catch (Exception e)
            {
                _log.LogError(e, "Không ghi được kết quả trận {Id}", room.Id);
            }
            for (int side = 0; side < 2; side++)
            {
                Seat s = room.Seats[side];
                if (s == null) continue;
                s.Room = null;
                s.Conn.Send(new EndMsg
                {
                    T = MsgType.End, MatchId = room.Id, Outcome = (int)m.Outcome, EndTick = m.Tick, Reason = m.Reason,
                    EloBefore = before[side], Rank = ranks[side], Coins = coins[side],
                });
            }
            _log.LogInformation("Hết trận {Id}: {Outcome} ({Reason}) sau {Ticks} bước · Elo {A0}→{A1} / {B0}→{B1}", room.Id, m.Outcome, m.Reason,
                                m.Tick, rec.AEloBefore, rec.AEloAfter, rec.BEloBefore, rec.BEloAfter);
        }

        /// <summary>Trận lỗi (lẽ ra không xảy ra): hủy, không tính Elo.</summary>
        private void Abort(Room room)
        {
            _rooms.Remove(room);
            for (int side = 0; side < 2; side++)
            {
                Seat s = room.Seats[side];
                if (s == null) continue;
                s.Room = null;
                s.Conn.Send(new EndMsg { T = MsgType.End, MatchId = room.Id, Outcome = (int)BattleOutcome.Draw, EndTick = room.Match.Tick, Reason = "error" });
            }
        }

        private static void Broadcast(Room room, NetMsg m)
        {
            string json = NetProto.Write(m);
            foreach (Seat s in room.Seats) s?.Conn.Send(json);
        }

        /// <summary>Mã người chơi ảo trông như mã người thật ("p" + 12 chữ hex), cố định theo từng người chơi ảo.</summary>
        private static string Disguise(string botId) => "p" + NetProto.Hash("pawfit-bot:" + botId).Substring(0, 12);
    }
}
