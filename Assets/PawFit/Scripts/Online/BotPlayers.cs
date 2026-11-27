using System;
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
}
