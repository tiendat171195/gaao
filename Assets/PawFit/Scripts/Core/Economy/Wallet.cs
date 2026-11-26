namespace PawFit.Core
{
    /// <summary>
    /// Ví Coin của người chơi (Phase 5.1). Nằm trong file lưu game, KHÔNG mất khi thú mất.
    /// Phase 4.1 đồng bộ lên Firestore cùng hồ sơ người chơi.
    /// </summary>
    public sealed class WalletData
    {
        public int Coins;

        /// <summary>Ngày (giờ trên máy, dạng yyyy-MM-dd) có lượt xem quảng cáo gần nhất + số lượt đã xem trong ngày đó.</summary>
        public string AdDay;
        public int AdsToday;

        /// <summary>Tổng Coin đã nhận / đã tiêu từ trước tới nay (thống kê, sau này đối chiếu chống gian lận).</summary>
        public long Earned;
        public long Spent;
    }

    /// <summary>
    /// Ví Coin tối giản để các phần khác cộng / trừ tiền mà không cần biết ví lưu ở đâu
    /// (vd phiên đấu cộng Coin khi thắng Bot). Bản thật: EconomyService.
    /// </summary>
    public interface ICoinWallet
    {
        int Coins { get; }

        /// <summary>Cộng Coin (amount > 0). `reason` để ghi log.</summary>
        void Add(int amount, string reason);

        /// <summary>Trừ Coin nếu đủ tiền. Không đủ thì không trừ gì và trả false.</summary>
        bool TrySpend(int amount, string reason);
    }

    /// <summary>Số Coin vừa thay đổi (View cập nhật chữ / hiệu ứng). Delta âm = vừa tiêu.</summary>
    public readonly struct CoinsChanged
    {
        public readonly int Coins;
        public readonly int Delta;
        public readonly string Reason;

        public CoinsChanged(int coins, int delta, string reason)
        {
            Coins = coins;
            Delta = delta;
            Reason = reason;
        }
    }
}
