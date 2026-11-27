using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;
using PawFit.Core;
using PawFit.Online;

namespace PawFit.Server
{
    public sealed class PlayerRow
    {
        public string Id;
        public string Name;
    }

    /// <summary>1 trận đã đấu (lưu lại để tra khi có người báo lỗi / nghi gian lận).</summary>
    public sealed class MatchRecord
    {
        public string Id;
        public DateTime StartedUtc;
        public DateTime EndedUtc;
        public string AId;
        public string BId;
        public int Outcome;
        public string Reason;
        public int Ticks;
        public int AEloBefore, AEloAfter, BEloBefore, BEloAfter;
    }

    /// <summary>
    /// Dữ liệu máy chủ trên SQLite (1 file, sao lưu = chép file). Mã đăng nhập chỉ lưu bản băm SHA-256:
    /// lộ file dữ liệu cũng không đăng nhập thay người khác được. Mọi lệnh đi qua 1 khóa (máy chủ nhỏ, đủ nhanh).
    /// </summary>
    public sealed class GameStore
    {
        private const string Schema = @"
CREATE TABLE IF NOT EXISTS players (
  id TEXT PRIMARY KEY,
  name TEXT NOT NULL,
  created_ms INTEGER NOT NULL,
  seen_ms INTEGER NOT NULL,
  elo INTEGER NOT NULL,
  season INTEGER NOT NULL DEFAULT 0,
  wins INTEGER NOT NULL DEFAULT 0,
  losses INTEGER NOT NULL DEFAULT 0,
  draws INTEGER NOT NULL DEFAULT 0,
  peak INTEGER NOT NULL,
  last_tier INTEGER NOT NULL DEFAULT 0
);
CREATE TABLE IF NOT EXISTS tokens (
  hash TEXT PRIMARY KEY,
  player_id TEXT NOT NULL REFERENCES players(id),
  created_ms INTEGER NOT NULL
);
CREATE INDEX IF NOT EXISTS tokens_player ON tokens(player_id);
CREATE TABLE IF NOT EXISTS saves (
  player_id TEXT PRIMARY KEY REFERENCES players(id),
  revision INTEGER NOT NULL,
  data TEXT NOT NULL,
  updated_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS transfer_codes (
  code TEXT PRIMARY KEY,
  player_id TEXT NOT NULL REFERENCES players(id),
  expires_ms INTEGER NOT NULL
);
CREATE TABLE IF NOT EXISTS matches (
  id TEXT PRIMARY KEY,
  started_ms INTEGER NOT NULL,
  ended_ms INTEGER NOT NULL,
  a_id TEXT NOT NULL,
  b_id TEXT NOT NULL,
  outcome INTEGER NOT NULL,
  reason TEXT NOT NULL,
  ticks INTEGER NOT NULL,
  a_elo_before INTEGER NOT NULL,
  a_elo_after INTEGER NOT NULL,
  b_elo_before INTEGER NOT NULL,
  b_elo_after INTEGER NOT NULL
);";

        private readonly string _cs;
        private readonly RankConfig _rank;
        private readonly object _gate = new object();

        public GameStore(string dbPath, RankConfig rank)
        {
            if (string.IsNullOrWhiteSpace(dbPath)) throw new ArgumentException("Thiếu đường dẫn file dữ liệu", nameof(dbPath));
            _rank = rank ?? throw new ArgumentNullException(nameof(rank));
            string dir = Path.GetDirectoryName(Path.GetFullPath(dbPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            _cs = new SqliteConnectionStringBuilder { DataSource = dbPath, Mode = SqliteOpenMode.ReadWriteCreate }.ToString();
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                {
                    Exec(db, "PRAGMA journal_mode=WAL;");
                    Exec(db, Schema);
                }
            }
        }

        // ------------------------------------------------------------------
        // Tài khoản
        // ------------------------------------------------------------------

        /// <summary>Tạo tài khoản khách: mã người chơi ngẫu nhiên + mã đăng nhập (trả về 1 lần duy nhất).</summary>
        public PlayerRow CreateGuest(string name, DateTime now, out string token)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteTransaction tx = db.BeginTransaction())
                {
                    string id = null;
                    for (int tries = 0; tries < 5 && id == null; tries++)
                    {
                        string candidate = "p" + RandomHex(6);
                        if (Scalar(db, tx, "SELECT COUNT(*) FROM players WHERE id = $id", ("$id", candidate)) == 0) id = candidate;
                    }
                    if (id == null) throw new InvalidOperationException("Không tạo được mã người chơi");
                    long ms = NetProto.ToMs(now);
                    Exec(db, tx, "INSERT INTO players (id, name, created_ms, seen_ms, elo, peak) VALUES ($id, $name, $ms, $ms, $elo, $elo)",
                         ("$id", id), ("$name", name), ("$ms", ms), ("$elo", _rank.StartElo));
                    token = NewToken(db, tx, id, ms);
                    tx.Commit();
                    return new PlayerRow { Id = id, Name = name };
                }
            }
        }

        /// <summary>Mã đăng nhập → người chơi (null nếu sai / đã thu hồi). Ghi luôn lần thấy gần nhất.</summary>
        public PlayerRow FindByToken(string token, DateTime now)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 200) return null;
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                {
                    PlayerRow p = null;
                    using (SqliteCommand cmd = Cmd(db, null, "SELECT p.id, p.name FROM tokens t JOIN players p ON p.id = t.player_id WHERE t.hash = $h",
                                                   ("$h", HashToken(token))))
                    using (SqliteDataReader r = cmd.ExecuteReader())
                        if (r.Read()) p = new PlayerRow { Id = r.GetString(0), Name = r.GetString(1) };
                    if (p != null) Exec(db, null, "UPDATE players SET seen_ms = $ms WHERE id = $id", ("$ms", NetProto.ToMs(now)), ("$id", p.Id));
                    return p;
                }
            }
        }

        public PlayerRow GetPlayer(string id)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteCommand cmd = Cmd(db, null, "SELECT id, name FROM players WHERE id = $id", ("$id", id)))
                using (SqliteDataReader r = cmd.ExecuteReader())
                    return r.Read() ? new PlayerRow { Id = r.GetString(0), Name = r.GetString(1) } : null;
            }
        }

        public PlayerRow Rename(string id, string name)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                {
                    int n = Exec(db, null, "UPDATE players SET name = $name WHERE id = $id", ("$name", name), ("$id", id));
                    return n > 0 ? new PlayerRow { Id = id, Name = name } : null;
                }
            }
        }

        // ------------------------------------------------------------------
        // Rank (luật ở RankRules, dùng chung với game)
        // ------------------------------------------------------------------

        /// <summary>Rank của người chơi, đã tính sang mùa theo giờ `now` (có đổi thì ghi lại luôn).</summary>
        public RankData GetRank(string id, DateTime now)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteTransaction tx = db.BeginTransaction())
                {
                    RankData d = ReadRank(db, tx, id);
                    if (d == null) return null;
                    int season = d.Season;
                    if (RankRules.RollSeason(d, now, _rank) || d.Season != season)
                    {
                        WriteRank(db, tx, id, d);
                        tx.Commit();
                    }
                    return d;
                }
            }
        }

        /// <summary>Ghi kết quả 1 trận: rank mới của các bên là người thật + lịch sử trận, trong 1 giao dịch.</summary>
        public void FinishMatch(MatchRecord rec, string idA, RankData rankA, string idB, RankData rankB)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteTransaction tx = db.BeginTransaction())
                {
                    if (idA != null && rankA != null) WriteRank(db, tx, idA, rankA);
                    if (idB != null && rankB != null) WriteRank(db, tx, idB, rankB);
                    Exec(db, tx, @"INSERT OR REPLACE INTO matches (id, started_ms, ended_ms, a_id, b_id, outcome, reason, ticks,
                                   a_elo_before, a_elo_after, b_elo_before, b_elo_after)
                                   VALUES ($id, $s, $e, $a, $b, $o, $r, $t, $ab, $aa, $bb, $ba)",
                         ("$id", rec.Id), ("$s", NetProto.ToMs(rec.StartedUtc)), ("$e", NetProto.ToMs(rec.EndedUtc)), ("$a", rec.AId), ("$b", rec.BId),
                         ("$o", rec.Outcome), ("$r", rec.Reason ?? ""), ("$t", rec.Ticks), ("$ab", rec.AEloBefore), ("$aa", rec.AEloAfter),
                         ("$bb", rec.BEloBefore), ("$ba", rec.BEloAfter));
                    tx.Commit();
                }
            }
        }

        public int MatchCount()
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                    return (int)Scalar(db, null, "SELECT COUNT(*) FROM matches");
            }
        }

        // ------------------------------------------------------------------
        // Lưu game
        // ------------------------------------------------------------------

        /// <summary>Bản lưu mới nhất (Revision 0 + Data null nếu chưa từng lưu).</summary>
        public SaveReply GetSave(string id)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteCommand cmd = Cmd(db, null, "SELECT revision, data, updated_ms FROM saves WHERE player_id = $id", ("$id", id)))
                using (SqliteDataReader r = cmd.ExecuteReader())
                    return r.Read() ? new SaveReply { Revision = r.GetInt64(0), Data = r.GetString(1), UpdatedUtcMs = r.GetInt64(2) } : new SaveReply();
            }
        }

        /// <summary>
        /// Ghi bản lưu mới nếu `baseRevision` đúng là bản mới nhất (chống 2 máy ghi đè lẫn nhau).
        /// Trả false + `revision` = bản đang có nếu lệch.
        /// </summary>
        public bool PutSave(string id, long baseRevision, string data, DateTime now, out long revision)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteTransaction tx = db.BeginTransaction())
                {
                    long current = Scalar(db, tx, "SELECT COALESCE(MAX(revision), 0) FROM saves WHERE player_id = $id", ("$id", id));
                    if (current != baseRevision)
                    {
                        revision = current;
                        return false;
                    }
                    revision = current + 1;
                    Exec(db, tx, @"INSERT INTO saves (player_id, revision, data, updated_ms) VALUES ($id, $rev, $data, $ms)
                                   ON CONFLICT(player_id) DO UPDATE SET revision = $rev, data = $data, updated_ms = $ms",
                         ("$id", id), ("$rev", revision), ("$data", data), ("$ms", NetProto.ToMs(now)));
                    tx.Commit();
                    return true;
                }
            }
        }

        // ------------------------------------------------------------------
        // Mã chuyển máy
        // ------------------------------------------------------------------

        /// <summary>Mã mới (mã cũ của người này bị hủy). Dùng 1 lần, hết hạn ở `expiresUtc`.</summary>
        public string CreateTransferCode(string id, DateTime expiresUtc)
        {
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteTransaction tx = db.BeginTransaction())
                {
                    Exec(db, tx, "DELETE FROM transfer_codes WHERE player_id = $id", ("$id", id));
                    string code = null;
                    for (int tries = 0; tries < 5 && code == null; tries++)
                    {
                        string candidate = RandomCode();
                        if (Scalar(db, tx, "SELECT COUNT(*) FROM transfer_codes WHERE code = $c", ("$c", candidate)) == 0) code = candidate;
                    }
                    if (code == null) throw new InvalidOperationException("Không tạo được mã chuyển máy");
                    Exec(db, tx, "INSERT INTO transfer_codes (code, player_id, expires_ms) VALUES ($c, $id, $e)",
                         ("$c", code), ("$id", id), ("$e", NetProto.ToMs(expiresUtc)));
                    tx.Commit();
                    return code;
                }
            }
        }

        /// <summary>
        /// Nhập mã chuyển máy: đúng + còn hạn thì xóa mã, THU HỒI mọi mã đăng nhập cũ (máy cũ mất quyền),
        /// cấp mã đăng nhập mới cho máy này. Sai / hết hạn = null.
        /// </summary>
        public PlayerRow Redeem(string code, DateTime now, out string token)
        {
            token = null;
            lock (_gate)
            {
                using (SqliteConnection db = Open())
                using (SqliteTransaction tx = db.BeginTransaction())
                {
                    long ms = NetProto.ToMs(now);
                    Exec(db, tx, "DELETE FROM transfer_codes WHERE expires_ms <= $ms", ("$ms", ms));
                    string id = null;
                    using (SqliteCommand cmd = Cmd(db, tx, "SELECT player_id FROM transfer_codes WHERE code = $c", ("$c", code)))
                    using (SqliteDataReader r = cmd.ExecuteReader())
                        if (r.Read()) id = r.GetString(0);
                    if (id == null)
                    {
                        tx.Commit();
                        return null;
                    }
                    Exec(db, tx, "DELETE FROM transfer_codes WHERE code = $c", ("$c", code));
                    Exec(db, tx, "DELETE FROM tokens WHERE player_id = $id", ("$id", id));
                    token = NewToken(db, tx, id, ms);
                    string name = null;
                    using (SqliteCommand cmd = Cmd(db, tx, "SELECT name FROM players WHERE id = $id", ("$id", id)))
                    using (SqliteDataReader r = cmd.ExecuteReader())
                        if (r.Read()) name = r.GetString(0);
                    tx.Commit();
                    return new PlayerRow { Id = id, Name = name };
                }
            }
        }

        // ------------------------------------------------------------------

        private RankData ReadRank(SqliteConnection db, SqliteTransaction tx, string id)
        {
            using (SqliteCommand cmd = Cmd(db, tx, "SELECT elo, season, wins, losses, draws, peak, last_tier FROM players WHERE id = $id", ("$id", id)))
            using (SqliteDataReader r = cmd.ExecuteReader())
            {
                if (!r.Read()) return null;
                return new RankData
                {
                    Elo = r.GetInt32(0),
                    Season = r.GetInt32(1),
                    Wins = r.GetInt32(2),
                    Losses = r.GetInt32(3),
                    Draws = r.GetInt32(4),
                    SeasonPeakElo = r.GetInt32(5),
                    LastSeasonTier = (RankTier)r.GetInt32(6),
                };
            }
        }

        private static void WriteRank(SqliteConnection db, SqliteTransaction tx, string id, RankData d) =>
            Exec(db, tx, @"UPDATE players SET elo = $elo, season = $season, wins = $w, losses = $l, draws = $d, peak = $peak, last_tier = $tier
                           WHERE id = $id",
                 ("$elo", d.Elo), ("$season", d.Season), ("$w", d.Wins), ("$l", d.Losses), ("$d", d.Draws), ("$peak", d.SeasonPeakElo),
                 ("$tier", (int)d.LastSeasonTier), ("$id", id));

        private static string NewToken(SqliteConnection db, SqliteTransaction tx, string playerId, long ms)
        {
            byte[] bytes = RandomNumberGenerator.GetBytes(32);
            string token = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
            Exec(db, tx, "INSERT INTO tokens (hash, player_id, created_ms) VALUES ($h, $id, $ms)", ("$h", HashToken(token)), ("$id", playerId), ("$ms", ms));
            return token;
        }

        private static string HashToken(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

        private static string RandomHex(int bytes) => Convert.ToHexString(RandomNumberGenerator.GetBytes(bytes)).ToLowerInvariant();

        private static string RandomCode()
        {
            var sb = new StringBuilder(NetProto.CodeLength);
            for (int i = 0; i < NetProto.CodeLength; i++) sb.Append(NetProto.CodeAlphabet[RandomNumberGenerator.GetInt32(NetProto.CodeAlphabet.Length)]);
            return sb.ToString();
        }

        private SqliteConnection Open()
        {
            var c = new SqliteConnection(_cs);
            c.Open();
            Exec(c, "PRAGMA busy_timeout = 5000; PRAGMA foreign_keys = ON;");
            return c;
        }

        private static void Exec(SqliteConnection db, string sql)
        {
            using (SqliteCommand cmd = db.CreateCommand())
            {
                cmd.CommandText = sql;
                cmd.ExecuteNonQuery();
            }
        }

        private static int Exec(SqliteConnection db, SqliteTransaction tx, string sql, params (string, object)[] args)
        {
            using (SqliteCommand cmd = Cmd(db, tx, sql, args))
                return cmd.ExecuteNonQuery();
        }

        private static long Scalar(SqliteConnection db, SqliteTransaction tx, string sql, params (string, object)[] args)
        {
            using (SqliteCommand cmd = Cmd(db, tx, sql, args))
                return Convert.ToInt64(cmd.ExecuteScalar() ?? 0L);
        }

        private static SqliteCommand Cmd(SqliteConnection db, SqliteTransaction tx, string sql, params (string, object)[] args)
        {
            SqliteCommand cmd = db.CreateCommand();
            cmd.CommandText = sql;
            cmd.Transaction = tx;
            foreach ((string name, object value) in args) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
            return cmd;
        }
    }
}
