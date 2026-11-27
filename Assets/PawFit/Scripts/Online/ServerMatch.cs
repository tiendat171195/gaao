using System;
using System.Collections.Generic;
using PawFit.Battle;

namespace PawFit.Online
{
    /// <summary>
    /// 1 trận PvP chạy ở máy chủ (C# thuần; máy chủ chỉ bọc thêm phần mạng). Máy chủ là trọng tài:
    ///  * Nét vuốt nhận được xếp hàng, áp vào ở bước kế tiếp theo đúng thứ tự nhận; rồi người chơi ảo (nếu có) nghĩ; rồi chạy bước.
    ///  * Nét nào được trận nhận thì ghi lại kèm số bước => gửi cho 2 máy chạy lại y hệt (<see cref="IMatchFeed"/>).
    ///  * Bỏ cuộc / mất kết nối = thua ngay.
    /// Người chơi ảo dùng BattleBot qua đúng cổng vuốt như người chơi (không ăn gian), cùng seed như bản trên máy.
    /// </summary>
    public sealed class ServerMatch
    {
        /// <summary>Mỗi bên tối đa bấy nhiêu nét chờ trong 1 bước (vuốt dồn / phá thì bỏ bớt).</summary>
        public const int MaxPendingPerSide = 4;

        private readonly BattleBot[] _bots = new BattleBot[2];
        private readonly List<KeyValuePair<int, SwipeResult>> _pending = new List<KeyValuePair<int, SwipeResult>>();
        private readonly int[] _pendingCount = new int[2];
        private readonly List<int> _record = new List<int>();
        private int _forfeit = -1;

        /// <param name="botA">Người chơi ảo điều khiển bên 0 (null = người thật).</param>
        /// <param name="botB">Người chơi ảo điều khiển bên 1.</param>
        public ServerMatch(FighterStats a, FighterStats b, bool[] learnedA, bool[] learnedB, ulong seed,
                           BattleConfig cfg = null, BotConfig botA = null, BotConfig botB = null)
        {
            Seed = seed;
            Sim = new BattleSimulation(a, b, cfg ?? new BattleConfig(), seed, learnedA, learnedB);
            if (botA != null) _bots[0] = new BattleBot(0, BotLevel.Normal, seed * 7 + 3, botA);
            if (botB != null) _bots[1] = new BattleBot(1, BotLevel.Normal, seed * 7 + 3, botB);
        }

        public ulong Seed { get; }
        public BattleSimulation Sim { get; }

        /// <summary>Số bước đã chạy.</summary>
        public int Tick { get; private set; }

        public bool IsBot(int side) => _bots[side == 1 ? 1 : 0] != null;

        public bool IsOver => _forfeit >= 0 || Sim.IsOver;

        /// <summary>Kết quả (bỏ cuộc thì bên kia thắng).</summary>
        public BattleOutcome Outcome => _forfeit >= 0 ? (_forfeit == 0 ? BattleOutcome.Win1 : BattleOutcome.Win0) : Sim.Outcome;

        /// <summary>Vì sao hết trận: ko / time / left (null khi chưa hết).</summary>
        public string Reason
        {
            get
            {
                if (_forfeit >= 0) return "left";
                if (!Sim.IsOver) return null;
                return Sim.Fighters[0].Hp <= 0f || Sim.Fighters[1].Hp <= 0f ? "ko" : "time";
            }
        }

        /// <summary>Nhận 1 nét vuốt (đã kiểm số) của người thật bên `side`: áp ở bước kế tiếp. Trả false nếu bỏ qua.</summary>
        public bool Submit(int side, SwipeResult swipe)
        {
            if (IsOver || side < 0 || side > 1 || _bots[side] != null) return false;
            if (_pendingCount[side] >= MaxPendingPerSide) return false;
            _pendingCount[side]++;
            _pending.Add(new KeyValuePair<int, SwipeResult>(side, swipe));
            return true;
        }

        /// <summary>Bỏ cuộc / mất kết nối: bên `side` thua ngay.</summary>
        public void Forfeit(int side)
        {
            if (IsOver || side < 0 || side > 1) return;
            _forfeit = side;
            _pending.Clear();
        }

        /// <summary>Chạy 1 bước cố định.</summary>
        public void Step()
        {
            if (IsOver) return;
            foreach (KeyValuePair<int, SwipeResult> p in _pending)
                if (Sim.Submit(p.Key, p.Value)) Record(p.Key, p.Value);
            _pending.Clear();
            _pendingCount[0] = _pendingCount[1] = 0;
            for (int side = 0; side < 2; side++)
            {
                SwipeResult? s = _bots[side]?.Think(Sim);
                if (s.HasValue && Sim.Submit(side, s.Value)) Record(side, s.Value);
            }
            Sim.Tick();
            Tick++;
        }

        /// <summary>Các nét đã chốt từ lần lấy trước, dạng phẳng 5 số / nét: [bước, bên, loại, hệ số (bit), hướng].</summary>
        public int[] TakeInputs()
        {
            if (_record.Count == 0) return null;
            int[] a = _record.ToArray();
            _record.Clear();
            return a;
        }

        /// <summary>Máu 2 bên (bit) để 2 máy đối chiếu.</summary>
        public int[] HpBits() => new[] { NetProto.Bits(Sim.Fighters[0].Hp), NetProto.Bits(Sim.Fighters[1].Hp) };

        /// <summary>Tin nhịp trận gửi 2 máy sau bước vừa chạy.</summary>
        public FrameMsg Frame() => new FrameMsg { T = MsgType.Frame, N = Tick, I = TakeInputs(), H = HpBits() };

        private void Record(int side, SwipeResult s)
        {
            _record.Add(Tick);
            _record.Add(side);
            _record.Add((int)s.Kind);
            _record.Add(NetProto.Bits(s.Quality));
            _record.Add(s.Direction);
        }
    }
}
