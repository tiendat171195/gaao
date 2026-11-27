using System;
using System.Collections.Generic;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    /// <summary>1 người đang xếp hàng tìm trận.</summary>
    public sealed class MatchTicket
    {
        public string PlayerId;
        public string Name;
        public string PetName;
        public string DnaCode;
        public int Elo;

        /// <summary>Giai đoạn tiến hóa của thú (người chơi ảo sinh cùng giai đoạn cho trận cân sức).</summary>
        public EvolutionStage Stage;

        /// <summary>Lực chiến của thú (EvolutionService.Power, đã tính bệnh).</summary>
        public float Power;

        /// <summary>Chỉ số chiến đấu + chiêu đã thuộc (gửi cho đối thủ để dựng bên mình trong trận).</summary>
        public FighterStats Stats;
        public int LearnedMask;

        public DateTime QueuedUtc;
    }

    /// <summary>Trạng thái tìm trận để View hiện: đang tìm / đã nới khoảng (30 giây) / gợi ý đấu Bot (60 giây).</summary>
    public enum MatchSearchStatus { Searching = 0, Widened = 1, SuggestBot = 2 }

    /// <summary>1 cặp vừa ghép được.</summary>
    public struct MatchPair
    {
        public MatchTicket A;
        public MatchTicket B;
    }

    /// <summary>Luật ghép trận thuần toán: Elo ±100 + lực chiến ±10%, chờ 30 giây thì nới ±200 / ±20%.</summary>
    public static class MatchmakingRules
    {
        public static int EloWindow(double waitedSeconds, MatchmakingConfig c) =>
            waitedSeconds >= c.WidenAfterSeconds ? c.WidenedEloWindow : c.EloWindow;

        public static float PowerWindow(double waitedSeconds, MatchmakingConfig c) =>
            waitedSeconds >= c.WidenAfterSeconds ? c.WidenedPowerWindow : c.PowerWindow;

        public static MatchSearchStatus Status(double waitedSeconds, MatchmakingConfig c) =>
            waitedSeconds >= c.SuggestBotAfterSeconds ? MatchSearchStatus.SuggestBot
            : waitedSeconds >= c.WidenAfterSeconds ? MatchSearchStatus.Widened
            : MatchSearchStatus.Searching;

        /// <summary>
        /// 2 vé ghép được không. Khoảng cho phép tính theo người chờ LÂU hơn (ưu tiên người đợi lâu);
        /// lực chiến so theo tỉ lệ của bên mạnh hơn.
        /// </summary>
        public static bool CanMatch(MatchTicket a, MatchTicket b, DateTime now, MatchmakingConfig c)
        {
            if (a == null || b == null || a.PlayerId == b.PlayerId) return false;
            double waited = Math.Max((now - a.QueuedUtc).TotalSeconds, (now - b.QueuedUtc).TotalSeconds);
            if (Math.Abs(a.Elo - b.Elo) > EloWindow(waited, c)) return false;
            float strongest = Math.Max(a.Power, b.Power);
            return strongest <= 0f || Math.Abs(a.Power - b.Power) <= PowerWindow(waited, c) * strongest + 1e-4f;
        }
    }

    /// <summary>
    /// Hàng chờ ghép trận chạy trên 1 máy (C# thuần): dùng cho bản giả lập mạng + test.
    /// Máy chủ thật (Photon, Phase 4.2) dùng đúng <see cref="MatchmakingRules"/> để lọc phòng.
    /// Người chờ lâu nhất được ghép trước, với đối thủ gần Elo nhất (hòa thì gần lực chiến nhất).
    /// </summary>
    public sealed class Matchmaker
    {
        private readonly MatchmakingConfig _cfg;
        private readonly List<MatchTicket> _queue = new List<MatchTicket>();

        public Matchmaker(MatchmakingConfig cfg)
        {
            _cfg = cfg ?? throw new ArgumentNullException(nameof(cfg));
        }

        public int Count => _queue.Count;

        public bool Contains(string playerId) => _queue.Exists(t => t.PlayerId == playerId);

        /// <summary>Xếp hàng (cùng người xếp lại thì thay vé cũ).</summary>
        public void Enqueue(MatchTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));
            Cancel(ticket.PlayerId);
            _queue.Add(ticket);
        }

        public bool Cancel(string playerId) => _queue.RemoveAll(t => t.PlayerId == playerId) > 0;

        public void Clear() => _queue.Clear();

        /// <summary>Ghép các cặp được tới thời điểm `now`, bỏ họ khỏi hàng chờ.</summary>
        public List<MatchPair> Tick(DateTime now)
        {
            var pairs = new List<MatchPair>();
            _queue.Sort((x, y) => x.QueuedUtc.CompareTo(y.QueuedUtc));   // chờ lâu trước
            for (int i = 0; i < _queue.Count; i++)
            {
                MatchTicket a = _queue[i];
                int best = -1;
                for (int j = 0; j < _queue.Count; j++)
                {
                    if (j == i || !MatchmakingRules.CanMatch(a, _queue[j], now, _cfg)) continue;
                    if (best < 0 || Better(a, _queue[j], _queue[best])) best = j;
                }
                if (best < 0) continue;
                MatchTicket b = _queue[best];
                pairs.Add(new MatchPair { A = a, B = b });
                _queue.Remove(a);
                _queue.Remove(b);
                i = -1;   // hàng đã đổi: duyệt lại từ đầu (vẫn ưu tiên người chờ lâu)
            }
            return pairs;
        }

        private static bool Better(MatchTicket me, MatchTicket x, MatchTicket current)
        {
            int dx = Math.Abs(me.Elo - x.Elo), dc = Math.Abs(me.Elo - current.Elo);
            if (dx != dc) return dx < dc;
            return Math.Abs(me.Power - x.Power) < Math.Abs(me.Power - current.Power);
        }
    }
}
