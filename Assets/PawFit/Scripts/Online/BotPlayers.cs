using System;
using System.Collections.Generic;
using PawFit.Battle;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Online
{
    /// <summary>
    /// Người chơi ảo cho PvP lúc vắng người (C# thuần): trông và đánh như người chơi thật.
    ///  * Mỗi số hiệu (0..IdentityCount-1) luôn ra đúng 1 người: cùng tên, cùng thú (DNA, tên thú), cùng lối đánh, cùng phụ kiện.
    ///  * Elo + lực chiến đặt quanh mức của mình khi ghép trận; thú cùng giai đoạn tiến hóa với thú mình, chỉ số khớp lực chiến.
    ///  * Đánh bằng BattleBot qua đúng cổng vuốt như người chơi (không ăn gian); tay nghề tăng theo Elo, lối đánh theo tính cách.
    /// </summary>
    public static class BotPlayers
    {
        public const int IdentityCount = 600;

        private static readonly string[] Prefixes = { "Bé", "Anh", "Chị", "Tiểu", "Mèo", "Gấu", "Cún", "Thỏ" };
        private static readonly string[] Bases =
        {
            "Na", "Bin", "Su", "Tít", "Mập", "Bống", "Ken", "Mỡ", "Cốm", "Đậu", "Nhím", "Tôm", "Khoai", "Bắp", "Mít", "Xoài",
            "Bơ", "Sữa", "Gạo", "Kem", "Ty", "Bông", "Rùa", "Vịt", "Heo", "Kiwi", "Lu", "Bo", "Mi", "Sóc",
        };
        private static readonly string[] Suffixes = { "99", "2k9", "_vn", " pro", "123", "xinh", "tv", "07", "2k8", "_gg", "88", "0602" };
        private static readonly string[] PetNames =
        {
            "Mun", "Bông", "Su Su", "Tôm", "Bơ", "Cốm", "Mochi", "Kẹo", "Mít", "Nấm", "Gạo", "Sữa", "Lucky", "Milo", "Đậu Đỏ",
            "Bánh Bao", "Socola", "Lu", "Tiểu Hổ", "Bắp", "Mây", "Xíu", "Tofu", "Pudding",
        };

        /// <summary>Người chơi ảo số `identity`, đặt ở Elo + lực chiến cho trước, thú cùng giai đoạn `stage`.</summary>
        public static OpponentInfo Create(int identity, int elo, float power, EvolutionStage stage, GameConfig cfg)
        {
            if (cfg == null) throw new ArgumentNullException(nameof(cfg));
            int id = ((identity % IdentityCount) + IdentityCount) % IdentityCount;
            var rng = new SeededRandom(0x9E3779B97F4A7C15UL ^ (ulong)(id * 2654435761L), 77UL);   // cố định theo số hiệu
            string nick = Nick(rng);
            string petName = PetNames[rng.Next(PetNames.Length)];
            PetDna dna = DnaGenerator.Random(rng);
            var style = (BotStyle)rng.Next(4);
            float skillJitter = (float)(rng.NextDouble() * 0.16 - 0.08);   // có người giỏi / kém hơn Elo của họ 1 chút
            AccessoryKind accessory = rng.NextDouble() < 0.35 ? (AccessoryKind)(1 + rng.Next(4)) : AccessoryKind.None;
            AdultForm lean = FormFor(style, rng);
            int learned = LearnedFor(stage, rng);
            AdultForm form = stage >= EvolutionStage.Adult ? lean : AdultForm.None;
            PetStats st = StatsRules.Compute(stage, form, 1f, cfg.Stats);
            return new OpponentInfo
            {
                PlayerId = "bot-" + id,
                Name = nick,
                PetName = petName,
                Dna = dna,
                DnaCode = DnaCodec.Encode(dna),
                Elo = elo,
                Power = power,
                Stats = ScaleToPower(st, power, cfg.Stats),
                LearnedMask = (1 << learned) - 1,
                Stage = stage,
                Form = stage >= EvolutionStage.Teen ? lean : AdultForm.None,
                Accessory = accessory,
                Style = style,
                Skill = Clamp01((elo - 850f) / 900f + skillJitter),
                IsGhost = true,
            };
        }

        /// <summary>Cấu hình điều khiển của người chơi ảo (tay nghề + lối đánh).</summary>
        public static BotConfig BotFor(OpponentInfo o) => BotConfig.Persona(o.Skill, o.Style);

        /// <summary>Chiêu đã thuộc từ mặt nạ bit (0..3 = ngang / lên / V / Z).</summary>
        public static bool[] Learned(int mask) => new[] { (mask & 1) != 0, (mask & 2) != 0, (mask & 4) != 0, (mask & 8) != 0 };

        /// <summary>Đối thủ cho BattleSession (Ranked).</summary>
        public static RankedOpponent ToRanked(OpponentInfo o) => new RankedOpponent
        {
            Name = o.PetName,
            Stats = o.Stats,
            Learned = Learned(o.LearnedMask),
            Bot = o.IsGhost ? BotFor(o) : null,
        };

        /// <summary>Dáng tiến hóa để dựng hình thú đối thủ (giữa giai đoạn: Child lớn đều, Teen nghiêng về dạng sắp thành).</summary>
        public static GrowthLook LookFor(EvolutionStage stage, AdultForm form, EvolutionConfig c)
        {
            var look = new GrowthLook();
            switch (stage)
            {
                case EvolutionStage.Child:
                    foreach (AdultForm f in EvolutionRules.Forms) look.Set(f, c.ChildGrowth * 0.5f / EvolutionRules.Forms.Length);
                    break;
                case EvolutionStage.Teen:
                    float total = (c.ChildGrowth + c.TeenGrowth) * 0.5f;
                    foreach (AdultForm f in EvolutionRules.Forms) look.Set(f, f == form ? total * 0.6f : total * 0.1f);
                    break;
                case EvolutionStage.Adult:
                case EvolutionStage.Ultimate:
                    if (form != AdultForm.None) look.Set(form, 1f);
                    look.Ultimate = stage == EvolutionStage.Ultimate;
                    break;
            }
            return look;
        }

        /// <summary>Kéo chỉ số theo đúng lực chiến mong muốn (giữ nguyên tỉ lệ giữa các chỉ số và tỉ lệ chí mạng).</summary>
        public static FighterStats ScaleToPower(PetStats s, float targetPower, StatsConfig c)
        {
            float critPart = c.PowerCrit * s.Crit;
            float rest = Math.Max(1e-3f, s.Power - critPart);
            float k = Math.Max(0.1f, (targetPower - critPart) / rest);
            return new FighterStats(s.Hp * k, s.Atk * k, s.Def * k, s.Spd * k, s.Crit);
        }

        /// <summary>Biệt danh ngẫu nhiên cùng kiểu với người chơi ảo (máy chủ đặt sẵn cho tài khoản mới, người chơi đổi sau).</summary>
        public static string RandomNick(IRandom rng) => Nick(rng ?? throw new ArgumentNullException(nameof(rng)));

        private static string Nick(IRandom rng)
        {
            string b = Bases[rng.Next(Bases.Length)];
            int kind = rng.Next(10);
            if (kind < 4) return Prefixes[rng.Next(Prefixes.Length)] + " " + b;
            if (kind < 8) return b + Suffixes[rng.Next(Suffixes.Length)];
            return b + " " + Bases[rng.Next(Bases.Length)];
        }

        /// <summary>Dạng hợp tính cách: Hăng → Lửa / Bóng Tối, Thủ → Thép, Kỹ thuật → Nước, Cân bằng → Ánh Sáng (hoặc bất kỳ).</summary>
        private static AdultForm FormFor(BotStyle style, IRandom rng)
        {
            switch (style)
            {
                case BotStyle.Aggressive: return rng.NextDouble() < 0.7 ? AdultForm.Fire : AdultForm.Shadow;
                case BotStyle.Defensive: return AdultForm.Steel;
                case BotStyle.Technical: return AdultForm.Water;
                default: return rng.NextDouble() < 0.6 ? AdultForm.Light : EvolutionRules.Forms[rng.Next(EvolutionRules.Forms.Length)];
            }
        }

        /// <summary>Số chiêu đã thuộc theo giai đoạn (người chơi lớn hơn thường đã tập nhiều hơn). Ultimate cần đủ 4.</summary>
        private static int LearnedFor(EvolutionStage stage, IRandom rng)
        {
            switch (stage)
            {
                case EvolutionStage.Baby: return rng.Next(2);
                case EvolutionStage.Child: return 1 + rng.Next(2);
                case EvolutionStage.Teen: return 2 + rng.Next(2);
                case EvolutionStage.Adult: return 3 + rng.Next(2);
                default: return 4;
            }
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }

    /// <summary>
    /// Hàng chờ riêng của 1 người đang tìm trận: người chơi ảo lần lượt tới quanh Elo + lực chiến của người đó
    /// (cách nhau 1,5–6 giây, Elo ±180, lực chiến ±20%) và ghép bằng <see cref="Matchmaker"/> thật
    /// (±100 / ±10%, 30 giây nới). Người chơi ảo cũng ghép với nhau (giống người khác tìm được trận).
    /// Dùng ở mạng giả lập trên máy (<see cref="LocalNetworkManager"/>) và ở máy chủ (khi vắng người thật).
    /// Không gặp lại người chơi ảo trong vài trận gần nhất.
    /// </summary>
    public sealed class GhostSearch
    {
        private readonly IRandom _rng;
        private readonly GameConfig _game;
        private readonly LocalNetworkConfig _net;
        private readonly Matchmaker _matchmaker;
        private readonly Dictionary<string, OpponentInfo> _info = new Dictionary<string, OpponentInfo>();
        private readonly Queue<int> _recent = new Queue<int>();
        private DateTime _nextGhostUtc;
        private int _ghosts;

        public GhostSearch(IRandom rng, GameConfig cfg, LocalNetworkConfig net = null)
        {
            _rng = rng ?? throw new ArgumentNullException(nameof(rng));
            _game = cfg ?? throw new ArgumentNullException(nameof(cfg));
            _net = net ?? new LocalNetworkConfig();
            _matchmaker = new Matchmaker(cfg.Matchmaking);
        }

        /// <summary>Vé của người đang tìm (null = không tìm).</summary>
        public MatchTicket Mine { get; private set; }

        public bool Active => Mine != null;

        /// <summary>Bắt đầu tìm cho `mine` (vé phải có QueuedUtc). Người chơi ảo đầu tiên tới sau `delaySeconds` + 1 khoảng ngẫu nhiên.</summary>
        public void Start(MatchTicket mine, DateTime now, double delaySeconds = 0.0)
        {
            Stop();
            Mine = mine ?? throw new ArgumentNullException(nameof(mine));
            _ghosts = 0;
            _nextGhostUtc = now.AddSeconds(Math.Max(0.0, delaySeconds) + NextGap());
            _matchmaker.Enqueue(mine);
        }

        /// <summary>Thôi tìm: bỏ cả người chơi ảo còn chờ (lần tìm sau sinh người mới quanh Elo lúc đó).</summary>
        public void Stop()
        {
            _matchmaker.Clear();
            _info.Clear();
            Mine = null;
        }

        /// <summary>Cho thời gian trôi tới `now`. Trả người chơi ảo vừa ghép với mình (đã tự dừng tìm), hoặc null.</summary>
        public OpponentInfo Update(DateTime now)
        {
            if (Mine == null) return null;
            while (_ghosts < _net.MaxGhosts && now >= _nextGhostUtc)
            {
                _matchmaker.Enqueue(MakeGhost(_nextGhostUtc));
                _nextGhostUtc = _nextGhostUtc.AddSeconds(NextGap());
            }
            foreach (MatchPair p in _matchmaker.Tick(now))
            {
                MatchTicket other = p.A == Mine ? p.B : p.B == Mine ? p.A : null;
                if (other == null) continue;   // 2 người chơi ảo ghép với nhau
                OpponentInfo info = Remember(other);
                Stop();
                return info;
            }
            return null;
        }

        private double NextGap() =>
            _net.GhostEverySecondsMin + _rng.NextDouble() * Math.Max(0.0, _net.GhostEverySecondsMax - _net.GhostEverySecondsMin);

        private MatchTicket MakeGhost(DateTime queuedUtc)
        {
            _ghosts++;
            float powerK = 1f + (float)((_rng.NextDouble() * 2.0 - 1.0) * _net.GhostPowerSpread);
            int elo = Math.Max(0, Mine.Elo + _rng.Range(-_net.GhostEloSpread, _net.GhostEloSpread + 1));
            OpponentInfo info = BotPlayers.Create(PickIdentity(), elo, Math.Max(1f, Mine.Power * powerK), Mine.Stage, _game);
            var t = new MatchTicket
            {
                PlayerId = info.PlayerId + "#" + _ghosts,   // mỗi lần vào hàng là 1 vé riêng
                Name = info.Name,
                PetName = info.PetName,
                DnaCode = info.DnaCode,
                Elo = info.Elo,
                Power = info.Power,
                Stage = info.Stage,
                Stats = info.Stats,
                LearnedMask = info.LearnedMask,
                QueuedUtc = queuedUtc,
            };
            _info[t.PlayerId] = info;
            return t;
        }

        /// <summary>Chọn người chơi ảo chưa gặp gần đây.</summary>
        private int PickIdentity()
        {
            int id = _rng.Next(BotPlayers.IdentityCount);
            for (int tries = 0; tries < 8 && _recent.Contains(id); tries++) id = _rng.Next(BotPlayers.IdentityCount);
            return id;
        }

        private OpponentInfo Remember(MatchTicket t)
        {
            if (!_info.TryGetValue(t.PlayerId, out OpponentInfo info)) return null;
            int id = int.Parse(info.PlayerId.Substring("bot-".Length));
            _recent.Enqueue(id);
            while (_recent.Count > Math.Max(0, _net.AvoidRecentOpponents)) _recent.Dequeue();
            return info;
        }
    }
}
