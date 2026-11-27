using System;
using System.Runtime.InteropServices;
using System.Text;
using Newtonsoft.Json;
using PawFit.Battle;
using PawFit.Core;

namespace PawFit.Online
{
    /// <summary>
    /// Giao thức máy chủ riêng ↔ game (Phase 4). JSON bằng Newtonsoft ở cả 2 đầu (cùng 1 file code này).
    ///  * REST (HTTP): tài khoản khách, giờ máy chủ, lưu game, mã chuyển máy, biệt danh.
    ///  * WebSocket: tìm trận + PvP. Máy chủ chạy trận thật, gửi về các nét vuốt đã chốt kèm số bước.
    /// Số thực của trận (chỉ số, hệ số vuốt, máu) gửi dạng bit (int): 2 máy + máy chủ dùng đúng cùng 1 số,
    /// không lệch vì làm tròn khi đổi chữ ↔ số => cùng seed thì cả 3 nơi ra cùng 1 trận.
    /// </summary>
    public static class NetProto
    {
        /// <summary>
        /// Đổi luật trận / tin nhắn thì tăng số này: máy chủ từ chối bản game cũ (báo cập nhật).
        /// 2 = Phase 7.1: bộ đệm vuốt, nhịp nhanh hơn, combo, thanh Nộ cho Tuyệt chiêu.
        /// </summary>
        public const int Version = 2;

        /// <summary>Tin nhắn WebSocket dài nhất (byte). Dài hơn = hỏng / phá, cắt kết nối.</summary>
        public const int MaxMessageBytes = 16 * 1024;

        public static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
        };

        public static string Write(object value) => JsonConvert.SerializeObject(value, Json);

        public static T Read<T>(string json) => JsonConvert.DeserializeObject<T>(json, Json);

        [StructLayout(LayoutKind.Explicit)]
        private struct FloatBits
        {
            [FieldOffset(0)] public float F;
            [FieldOffset(0)] public int I;
        }

        /// <summary>float → int giữ nguyên từng bit.</summary>
        public static int Bits(float v)
        {
            var b = new FloatBits { F = v };
            return b.I;
        }

        public static float Float(int bits)
        {
            var b = new FloatBits { I = bits };
            return b.F;
        }

        private static readonly DateTime Epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static long ToMs(DateTime utc) => (long)Math.Floor((utc - Epoch).TotalMilliseconds);

        public static DateTime FromMs(long ms) => Epoch.AddMilliseconds(ms);

        // ---- biệt danh + mã chuyển máy: luật chung để máy kiểm trước, máy chủ kiểm lại ----

        public const int NameMin = 2;
        public const int NameMax = 16;

        /// <summary>Biệt danh hợp lệ (bỏ ký tự điều khiển, gộp khoảng trắng, 2–16 ký tự) hoặc null.</summary>
        public static string CleanName(string name)
        {
            if (name == null) return null;
            var sb = new StringBuilder();
            bool space = false;
            foreach (char ch in name.Trim())
            {
                if (char.IsControl(ch) || ch == '<' || ch == '>') continue;
                if (char.IsWhiteSpace(ch))
                {
                    if (!space && sb.Length > 0) sb.Append(' ');
                    space = true;
                    continue;
                }
                space = false;
                sb.Append(ch);
            }
            string s = sb.ToString().Trim();
            if (s.Length > NameMax) s = s.Substring(0, NameMax).Trim();
            return s.Length >= NameMin ? s : null;
        }

        /// <summary>Bộ chữ của mã chuyển máy: bỏ I / O / 0 / 1 cho khỏi đọc nhầm. 12 ký tự = 32^12 mã, đoán mò không nổi.</summary>
        public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        public const int CodeLength = 12;

        /// <summary>Mã người chơi gõ (có gạch / khoảng trắng / chữ thường) → 12 ký tự chuẩn, hoặc null nếu sai.</summary>
        public static string NormalizeCode(string code)
        {
            if (code == null) return null;
            var sb = new StringBuilder();
            foreach (char ch in code.ToUpperInvariant())
            {
                if (ch == '-' || ch == ' ') continue;
                if (CodeAlphabet.IndexOf(ch) < 0) return null;
                sb.Append(ch);
            }
            return sb.Length == CodeLength ? sb.ToString() : null;
        }

        /// <summary>12 ký tự → "XXXX-XXXX-XXXX" cho dễ đọc / chép.</summary>
        public static string FormatCode(string code) =>
            code != null && code.Length == CodeLength ? code.Substring(0, 4) + "-" + code.Substring(4, 4) + "-" + code.Substring(8, 4) : code;

        /// <summary>Dấu nội dung (FNV-1a 64 bit): file game không đổi thì không tải lên lại.</summary>
        public static string Hash(string text)
        {
            ulong h = 14695981039346656037UL;
            if (text != null)
                foreach (char ch in text)
                {
                    h ^= ch;
                    h *= 1099511628211UL;
                }
            return h.ToString("x16");
        }
    }

    /// <summary>Chỉ số chiến đấu dạng bit (đúng từng bit của float).</summary>
    public sealed class NetStats
    {
        public int Hp, Atk, Def, Spd, Crit;

        public static NetStats From(FighterStats s) => new NetStats
        {
            Hp = NetProto.Bits(s.Hp),
            Atk = NetProto.Bits(s.Atk),
            Def = NetProto.Bits(s.Def),
            Spd = NetProto.Bits(s.Spd),
            Crit = NetProto.Bits(s.Crit),
        };

        public FighterStats ToStats() =>
            new FighterStats(NetProto.Float(Hp), NetProto.Float(Atk), NetProto.Float(Def), NetProto.Float(Spd), NetProto.Float(Crit));
    }

    // =====================================================================
    // REST (HTTP, JSON). Lỗi: mã HTTP + ApiError.
    // =====================================================================

    public sealed class GuestRequest
    {
        public int Proto;
        public string Platform;
    }

    public sealed class AuthReply
    {
        public string PlayerId;
        public string Token;
        public string Name;
    }

    public sealed class TimeReply
    {
        public long UtcMs;
    }

    /// <summary>GET /v1/me: hồ sơ + giờ máy chủ + rank (đã tính sang mùa) + bản lưu game mới nhất trên máy chủ.</summary>
    public sealed class MeReply
    {
        public string PlayerId;
        public string Name;
        public long UtcMs;
        public RankData Rank;
        public long SaveRevision;
    }

    /// <summary>PUT /v1/save: tải file game lên. BaseRevision = bản trên máy chủ mà file này dựa vào (khác = 409).</summary>
    public sealed class SavePut
    {
        public long BaseRevision;
        public string Data;
    }

    public sealed class SaveReply
    {
        public long Revision;
        public string Data;
        public long UpdatedUtcMs;
    }

    public sealed class TransferCodeReply
    {
        public string Code;
        public long ExpiresUtcMs;
    }

    public sealed class RedeemRequest
    {
        public string Code;
    }

    public sealed class RenameRequest
    {
        public string Name;
    }

    public sealed class ApiError
    {
        public string Code;
        public string Message;

        /// <summary>409 khi lưu game: bản mới nhất đang ở máy chủ.</summary>
        public long Revision;
    }

    // =====================================================================
    // WebSocket (mỗi tin là 1 object JSON, T = loại tin)
    // =====================================================================

    public static class MsgType
    {
        // máy → máy chủ
        public const string Hello = "hello";
        public const string Queue = "queue";
        public const string Cancel = "cancel";
        public const string Ready = "ready";
        public const string Swipe = "swipe";
        public const string Leave = "leave";
        public const string Ping = "ping";

        // máy chủ → máy
        public const string Welcome = "welcome";
        public const string Error = "error";
        public const string Queued = "queued";
        public const string Found = "found";
        public const string Start = "start";
        public const string Frame = "f";
        public const string End = "end";
        public const string Pong = "pong";
    }

    /// <summary>Đầu mọi tin WebSocket. Đọc T trước rồi mới đọc đúng loại tin.</summary>
    public class NetMsg
    {
        public string T;
    }

    public sealed class HelloMsg : NetMsg
    {
        public string Token;
        public int Proto;
        public string Build;
    }

    public sealed class WelcomeMsg : NetMsg
    {
        public string PlayerId;
        public string Name;
        public long UtcMs;
        public int Elo;
    }

    /// <summary>Mã lỗi: auth (sai tài khoản), proto (bản game cũ), bad (tin hỏng), state (làm sai lúc), busy.</summary>
    public sealed class ErrorMsg : NetMsg
    {
        public string Code;
        public string Message;
    }

    /// <summary>Thú mình mang đi tìm trận. Elo không gửi: máy chủ lấy Elo thật của tài khoản.</summary>
    public sealed class NetTicket
    {
        public string PetName;
        public string DnaCode;
        public int Stage;
        public int Form;
        public int Accessory;
        public NetStats Stats;
        public int LearnedMask;

        public static NetTicket From(MatchTicket t) => new NetTicket
        {
            PetName = t.PetName,
            DnaCode = t.DnaCode,
            Stage = (int)t.Stage,
            Form = (int)t.Form,
            Accessory = (int)t.Accessory,
            Stats = NetStats.From(t.Stats),
            LearnedMask = t.LearnedMask,
        };
    }

    public sealed class QueueMsg : NetMsg
    {
        public NetTicket Ticket;
    }

    public sealed class QueuedMsg : NetMsg
    {
        public int Elo;
    }

    /// <summary>1 bên của trận (người thật hoặc người chơi ảo — máy không phân biệt được).</summary>
    public sealed class NetPlayer
    {
        public string PlayerId;
        public string Name;
        public string PetName;
        public string DnaCode;
        public int Elo;
        public float Power;
        public int Stage;
        public int Form;
        public int Accessory;
        public NetStats Stats;
        public int LearnedMask;
    }

    /// <summary>Ghép được trận. Players[0] / [1] = bên 0 / 1 ở máy chủ; Side = mình là bên nào.</summary>
    public sealed class FoundMsg : NetMsg
    {
        public string MatchId;
        public ulong Seed;
        public int Side;
        public int DurationMs;
        public NetPlayer[] Players;
    }

    public sealed class MatchMsg : NetMsg
    {
        public string MatchId;
    }

    public sealed class SwipeMsg : NetMsg
    {
        public int Kind;

        /// <summary>Hệ số vuốt (bit của float).</summary>
        public int Q;

        public int Dir;

        public static SwipeMsg From(SwipeResult s) =>
            new SwipeMsg { T = MsgType.Swipe, Kind = (int)s.Kind, Q = NetProto.Bits(s.Quality), Dir = s.Direction };

        /// <summary>Đọc nét vuốt từ mạng: loại sai / số hỏng thì về giá trị an toàn.</summary>
        public static SwipeResult ToSwipe(int kind, int qBits, int dir)
        {
            var k = (SwipeKind)FeedSides.Clamp(kind, 0, 5);
            float q = FeedSides.Finite(NetProto.Float(qBits), 1f);
            return new SwipeResult
            {
                Kind = k,
                Quality = Math.Max(0f, Math.Min(2f, q)),
                Direction = dir < 0 ? -1 : dir > 0 ? 1 : 0,
                Reason = k == SwipeKind.None ? "vuốt sai" : null,
            };
        }
    }

    /// <summary>
    /// Máy chủ đã chạy xong N bước. I = các nét vuốt đã chốt từ tin trước, mỗi nét 5 số liền nhau:
    /// [bước, bên, loại vuốt, hệ số (bit), hướng]. H = máu 2 bên sau bước N (bit).
    /// </summary>
    public sealed class FrameMsg : NetMsg
    {
        public int N;
        public int[] I;
        public int[] H;
    }

    /// <summary>Hết trận. Outcome theo bên máy chủ (BattleOutcome). Reason: ko / time / left. Rank = rank mới của mình.</summary>
    public sealed class EndMsg : NetMsg
    {
        public string MatchId;
        public int Outcome;
        public int EndTick;
        public string Reason;
        public int EloBefore;
        public RankData Rank;
        public int Coins;
    }

    public sealed class PingMsg : NetMsg
    {
        /// <summary>Giờ gửi của máy (ms) — máy chủ trả lại nguyên để máy tính độ trễ.</summary>
        public long C;

        /// <summary>Giờ máy chủ (ms).</summary>
        public long S;
    }
}
