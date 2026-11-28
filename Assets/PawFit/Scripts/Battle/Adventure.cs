using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Battle
{
    /// <summary>1 vùng trên bản đồ Thám hiểm (GDD G15). Sinh vật trong vùng có chỉ số theo giai đoạn tiến hóa <see cref="Stage"/>.</summary>
    public sealed class AdventureRegion
    {
        public int Index;
        public string Name;
        public string Blurb;
        public EvolutionStage Stage;

        /// <summary>Màu lông sinh vật trong vùng (FurHue 0..63 của DNA).</summary>
        public int[] Hues;

        /// <summary>Dạng Adult của sinh vật (vùng Adult / Ultimate), lần lượt theo màn.</summary>
        public AdultForm[] Forms;

        public int FirstStage;
        public int StageCount;
    }

    /// <summary>1 màn Thám hiểm = 1 trận với 1 sinh vật hoang. Màn cuối mỗi vùng là trùm.</summary>
    public sealed class AdventureStage
    {
        /// <summary>Số thứ tự trên cả bản đồ (0..).</summary>
        public int Index;

        public int Region;

        /// <summary>Thứ tự trong vùng (1..).</summary>
        public int Number;

        public bool Boss;
        public string Creature;
        public EvolutionStage Stage;
        public AdultForm Form;

        /// <summary>Chỉ số = chỉ số chuẩn của giai đoạn × hệ số này (trừ Crit).</summary>
        public float StatFactor;

        /// <summary>Tay nghề bot 0..1 (BotConfig.Persona).</summary>
        public float Skill;

        public BotStyle Style;

        /// <summary>Chiêu đã thuộc (bit 0..3 = ngang / lên / V / Z).</summary>
        public int LearnedMask;

        /// <summary>Coin thưởng khi thắng lần đầu (1 sao).</summary>
        public int Coins;

        /// <summary>Mã màn hiện cho người chơi, vd "2-5".</summary>
        public string Code => (Region + 1) + "-" + Number;
    }

    /// <summary>Số liệu Thám hiểm (chỉnh ở đây, không phải sửa code) — GDD G15.</summary>
    public sealed class AdventureConfig
    {
        /// <summary>
        /// Hệ số chỉ số của 5 màn trong 1 vùng (màn 5 = trùm), so với thú chuẩn của giai đoạn đó.
        /// Đo bằng bot (GDD G15): người chơi khá (tay nghề 0,5) ở đúng giai đoạn thắng dễ 3 màn đầu, màn 4 ~75–100%,
        /// trùm 96% → 65% → 46% → 30% → 15% theo vùng; lớn thêm 1 giai đoạn thì vùng cũ dễ hẳn.
        /// </summary>
        public float[] StageFactors = { 0.7f, 0.8f, 0.9f, 0.98f, 1.05f };

        /// <summary>Tay nghề sinh vật ở màn 1 của từng vùng; mỗi màn sau +SkillStep, trùm thêm BossSkillBonus.</summary>
        public float[] RegionSkill = { 0.05f, 0.12f, 0.2f, 0.27f, 0.34f };
        public float SkillStep = 0.04f;
        public float BossSkillBonus = 0.05f;

        /// <summary>Thắng còn ≥ bấy nhiêu %HP thì được 2 / 3 sao (thắng = 1 sao).</summary>
        public float TwoStarHp = 0.4f;
        public float ThreeStarHp = 0.7f;

        /// <summary>Coin thắng lần đầu = CoinsBase + CoinsPerStage × số thứ tự màn; trùm ×BossCoinFactor.</summary>
        public int CoinsBase = 10;
        public int CoinsPerStage = 4;
        public float BossCoinFactor = 2f;

        /// <summary>Mỗi sao mới sau sao đầu được thêm bấy nhiêu phần Coin lần đầu; thắng lại (không thêm sao) được bấy nhiêu phần.</summary>
        public float NewStarCoinFactor = 0.5f;
        public float RepeatCoinFactor = 0.2f;

        /// <summary>Năng lượng của bé dưới mức này thì không đi thám hiểm được (mỗi trận tốn năng lượng như đấu Bot).</summary>
        public float MinEnergy = 15f;

        /// <summary>So lực chiến sinh vật / thú: dưới mốc đầu = Dễ, rồi Vừa, Khó, trên mốc cuối = Rất khó.</summary>
        public float[] DifficultyRatios = { 0.9f, 1.05f, 1.25f };
    }

    public enum AdventureDifficulty { Easy = 0, Fair = 1, Hard = 2, VeryHard = 3 }

    /// <summary>Kết quả 1 trận Thám hiểm sau khi đã cộng sao + Coin.</summary>
    public sealed class AdventureResult
    {
        public int Stage;
        public bool Won;

        /// <summary>Sao của trận này (0..3) / sao tốt nhất của màn sau trận / số sao mới có thêm.</summary>
        public int Stars;
        public int BestStars;
        public int NewStars;

        public int Coins;
        public bool FirstClear;

        /// <summary>Vừa thắng trùm lần đầu: mở vùng này (-1 = không mở vùng mới).</summary>
        public int UnlockedRegion = -1;
    }

    /// <summary>
    /// Thám hiểm (Phase 7.2, C# thuần): bản đồ 5 vùng × 5 màn, mỗi màn đấu 1 sinh vật hoang (bot có lối đánh riêng).
    /// Độ khó bám theo giai đoạn tiến hóa (vùng 1 cho Baby ... vùng 5 cho Ultimate): bé lớn lên mới đi xa được.
    /// Thắng = 1 sao, còn ≥ 40% / 70% HP = 2 / 3 sao. Coin khi thắng lần đầu, mỗi sao mới, ít hơn khi thắng lại.
    /// Màn sau mở khi màn trước có sao; thắng trùm thì mở vùng sau. Mỗi trận tốn năng lượng + no như đấu Bot.
    /// </summary>
    public sealed class AdventureService
    {
        private static readonly string[][] Names =
        {
            new[] { "Bông Gió", "Hạt Dẻ", "Mầm Non", "Gai Nhỏ", "Vua Đồi Cỏ" },
            new[] { "Rêu Xanh", "Nấm Đỏ", "Bào Tử", "Gai Rừng", "Chúa Nấm" },
            new[] { "Sương Mai", "Bọt Nước", "Đá Cuội", "Sóng Bạc", "Thần Hồ Sương" },
            new[] { "Than Hồng", "Tro Bay", "Đá Nóng", "Lửa Nhỏ", "Hỏa Vương" },
            new[] { "Pha Lê Tím", "Bóng Đêm", "Ánh Ngọc", "Gương Vỡ", "Bóng Ma Pha Lê" },
        };

        private readonly PetService _pets;
        private readonly EvolutionService _evo;
        private readonly HealthService _health;
        private readonly ICoinWallet _wallet;
        private readonly EventBus _bus;
        private readonly AdventureConfig _cfg;
        private readonly List<AdventureRegion> _regions = new List<AdventureRegion>();
        private readonly List<AdventureStage> _stages = new List<AdventureStage>();

        public AdventureService(PetService pets, EvolutionService evo, HealthService health, ICoinWallet wallet = null,
                                EventBus bus = null, AdventureConfig cfg = null)
        {
            _pets = pets ?? throw new ArgumentNullException(nameof(pets));
            _evo = evo ?? throw new ArgumentNullException(nameof(evo));
            _health = health ?? throw new ArgumentNullException(nameof(health));
            _wallet = wallet;
            _bus = bus;
            _cfg = cfg ?? new AdventureConfig();
            BuildMap();
        }

        public AdventureConfig Config => _cfg;
        public IReadOnlyList<AdventureRegion> Regions => _regions;
        public IReadOnlyList<AdventureStage> Stages => _stages;
        public int MaxStars => _stages.Count * 3;

        private AdventureData Data => _pets.Adventure;

        public int StarsOf(int stage)
        {
            AdventureData d = Data;
            return d != null && stage >= 0 && stage < d.Stars.Count ? Math.Max(0, Math.Min(3, d.Stars[stage])) : 0;
        }

        public int TotalStars
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _stages.Count; i++) n += StarsOf(i);
                return n;
            }
        }

        public int RegionStars(int region)
        {
            if (region < 0 || region >= _regions.Count) return 0;
            AdventureRegion r = _regions[region];
            int n = 0;
            for (int i = 0; i < r.StageCount; i++) n += StarsOf(r.FirstStage + i);
            return n;
        }

        /// <summary>Màn đầu luôn mở; màn sau mở khi màn trước đã thắng (có sao).</summary>
        public bool IsUnlocked(int stage) => stage >= 0 && stage < _stages.Count && (stage == 0 || StarsOf(stage - 1) > 0);

        public bool IsRegionUnlocked(int region) => region >= 0 && region < _regions.Count && IsUnlocked(_regions[region].FirstStage);

        /// <summary>Màn nên đánh tiếp: màn đã mở đầu tiên chưa có sao; thắng hết rồi thì màn chưa đủ 3 sao đầu tiên; -1 nếu đủ 3 sao cả bản đồ.</summary>
        public int NextStage
        {
            get
            {
                for (int i = 0; i < _stages.Count; i++)
                    if (IsUnlocked(i) && StarsOf(i) == 0) return i;
                for (int i = 0; i < _stages.Count; i++)
                    if (StarsOf(i) < 3) return i;
                return -1;
            }
        }

        /// <summary>Chỉ số sinh vật của màn (đúng công thức chỉ số thú G8 của giai đoạn đó × hệ số màn).</summary>
        public PetStats CreatureStats(int stage)
        {
            AdventureStage s = _stages[stage];
            StatsConfig c = _pets.Config.Stats;
            PetStats p = StatsRules.Compute(s.Stage, s.Form, 1f, c);
            p.Hp *= s.StatFactor;
            p.Atk *= s.StatFactor;
            p.Def *= s.StatFactor;
            p.Spd *= s.StatFactor;
            p.Power = StatsRules.Power(p, c);
            return p;
        }

        /// <summary>Độ khó so với thú hiện tại (lực chiến sinh vật / lực chiến thú, đã tính bệnh).</summary>
        public AdventureDifficulty DifficultyOf(int stage)
        {
            float mine = _evo.Stats.Power;
            if (mine <= 0f) return AdventureDifficulty.VeryHard;
            float ratio = CreatureStats(stage).Power / mine;
            float[] t = _cfg.DifficultyRatios;
            if (ratio < t[0]) return AdventureDifficulty.Easy;
            if (ratio < t[1]) return AdventureDifficulty.Fair;
            return ratio < t[2] ? AdventureDifficulty.Hard : AdventureDifficulty.VeryHard;
        }

        /// <summary>Đánh được màn này không: màn đã mở + thú khỏe, thức, đủ năng lượng.</summary>
        public bool CanStart(int stage, out string reason)
        {
            reason = null;
            if (stage < 0 || stage >= _stages.Count) reason = "không có màn này";
            else if (!IsUnlocked(stage)) reason = "chưa mở: thắng màn trước đã";
            else if (!BattleSession.CanStart(_pets, _health, out string why)) reason = "thú " + why;
            else if (_pets.Current.Needs.Energy < _cfg.MinEnergy) reason = "bé mệt rồi, cho bé ngủ hoặc uống nước tăng lực đã";
            return reason == null;
        }

        /// <summary>Đối thủ của màn: chỉ số, chiêu đã thuộc, bot theo tay nghề + lối đánh của sinh vật.</summary>
        public RankedOpponent OpponentFor(int stage)
        {
            AdventureStage s = _stages[stage];
            PetStats p = CreatureStats(stage);
            var learned = new bool[4];
            for (int i = 0; i < 4; i++) learned[i] = (s.LearnedMask & (1 << i)) != 0;
            return new RankedOpponent
            {
                Name = s.Creature,
                Stats = new FighterStats(p.Hp, p.Atk, p.Def, p.Spd, p.Crit),
                Learned = learned,
                Bot = BotConfig.Persona(s.Skill, s.Style),
            };
        }

        /// <summary>DNA (hình dáng + màu) của sinh vật: cố định theo màn, màu lông theo vùng.</summary>
        public PetDna CreatureDna(int stage)
        {
            AdventureStage s = _stages[stage];
            AdventureRegion r = _regions[s.Region];
            PetDna dna = DnaGenerator.Random(new SeededRandom(0xAD0E_0000UL + (ulong)stage * 977UL, 7UL));
            dna.FurHue = r.Hues[(s.Number - 1) % r.Hues.Length];
            return dna;
        }

        /// <summary>Tạo trận cho màn này (mode Thám hiểm). Gọi <see cref="CanStart"/> trước.</summary>
        public BattleSession StartBattle(int stage, ulong seed) =>
            new BattleSession(_pets, _evo, OpponentFor(stage), seed, BattleMode.Adventure);

        /// <summary>Sao của 1 trận theo kết quả + %HP thú còn lại (0 = thua / hòa / bỏ cuộc).</summary>
        public int StarsFor(bool won, float hpLeft01)
        {
            if (!won) return 0;
            if (hpLeft01 >= _cfg.ThreeStarHp - 1e-4f) return 3;
            return hpLeft01 >= _cfg.TwoStarHp - 1e-4f ? 2 : 1;
        }

        /// <summary>Hết trận Thám hiểm: tính sao (theo %HP còn lại), cộng Coin, ghi tiến độ, lưu file.</summary>
        public AdventureResult Complete(int stage, BattleResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            return Complete(stage, result.PlayerWon, result.PlayerWon ? result.HpLeft : 0f);
        }

        /// <summary>Như trên, khi đã biết thắng / thua và %HP còn lại (để test).</summary>
        public AdventureResult Complete(int stage, bool won, float hpLeft01)
        {
            if (stage < 0 || stage >= _stages.Count) throw new ArgumentOutOfRangeException(nameof(stage));
            AdventureData d = Data ?? throw new InvalidOperationException("Chưa nạp file lưu");
            AdventureStage s = _stages[stage];
            int before = StarsOf(stage);
            int stars = StarsFor(won, hpLeft01);
            var r = new AdventureResult { Stage = stage, Won = won, Stars = stars, BestStars = Math.Max(before, stars) };
            if (!won)
            {
                d.Losses++;
                _pets.Save();
                return r;
            }
            d.Wins++;
            while (d.Stars.Count <= stage) d.Stars.Add(0);
            d.Stars[stage] = r.BestStars;
            r.NewStars = Math.Max(0, stars - before);
            r.FirstClear = before == 0;
            float coins = 0f;
            for (int k = before + 1; k <= stars; k++) coins += k == 1 ? s.Coins : s.Coins * _cfg.NewStarCoinFactor;
            if (r.NewStars == 0) coins = s.Coins * _cfg.RepeatCoinFactor;
            r.Coins = Math.Max(1, (int)Math.Round(coins));
            if (_wallet != null) _wallet.Add(r.Coins, "thám hiểm " + s.Code);
            else r.Coins = 0;
            if (s.Boss && r.FirstClear && s.Region + 1 < _regions.Count) r.UnlockedRegion = s.Region + 1;
            _pets.Save();
            _bus?.Publish(new AdventureWon(stage, stars, r.NewStars, r.FirstClear));
            return r;
        }

        /// <summary>Công cụ test (bảng DEV): mở hết bản đồ (mỗi màn 1 sao) hoặc xóa tiến độ.</summary>
        public void SetAllForDebug(int stars)
        {
            AdventureData d = Data;
            if (d == null) return;
            d.Stars.Clear();
            for (int i = 0; i < _stages.Count && stars > 0; i++) d.Stars.Add(Math.Min(3, stars));
            _pets.Save();
        }

        private void BuildMap()
        {
            AddRegion("Đồi Cỏ Gió", "Đồi cỏ sau vườn, toàn bé hoang hiền lành. Hợp với bé mới nở.",
                      EvolutionStage.Baby, new[] { 20, 8, 13, 4, 24 }, null);
            AddRegion("Rừng Nấm", "Rừng rậm đầy nấm phát sáng. Sinh vật ở đây lì đòn hơn.",
                      EvolutionStage.Child, new[] { 58, 52, 2, 46, 60 }, null);
            AddRegion("Hồ Sương", "Hồ nước phủ sương mờ. Sinh vật nhanh nhẹn, hay canh đỡ.",
                      EvolutionStage.Teen, new[] { 36, 40, 32, 38, 34 }, null);
            AddRegion("Núi Lửa", "Đá nóng bỏng chân. Sinh vật hung hăng, đánh rất đau.",
                      EvolutionStage.Adult, new[] { 0, 4, 62, 2, 1 },
                      new[] { AdultForm.Fire, AdultForm.Shadow, AdultForm.Steel, AdultForm.Fire, AdultForm.Fire });
            AddRegion("Hang Pha Lê", "Hang sâu lấp lánh, nơi những sinh vật mạnh nhất sinh sống.",
                      EvolutionStage.Ultimate, new[] { 48, 44, 34, 50, 47 },
                      new[] { AdultForm.Light, AdultForm.Shadow, AdultForm.Water, AdultForm.Light, AdultForm.Shadow });
        }

        private void AddRegion(string name, string blurb, EvolutionStage stage, int[] hues, AdultForm[] forms)
        {
            int ri = _regions.Count;
            var region = new AdventureRegion
            {
                Index = ri, Name = name, Blurb = blurb, Stage = stage, Hues = hues, Forms = forms,
                FirstStage = _stages.Count, StageCount = _cfg.StageFactors.Length,
            };
            _regions.Add(region);
            float baseSkill = _cfg.RegionSkill[Math.Min(ri, _cfg.RegionSkill.Length - 1)];
            for (int n = 1; n <= region.StageCount; n++)
            {
                bool boss = n == region.StageCount;
                int index = _stages.Count;
                float skill = baseSkill + _cfg.SkillStep * (n - 1) + (boss ? _cfg.BossSkillBonus : 0f);
                // vùng 1 mới thuộc ngang + lên, trùm vùng 1 + cả vùng 2 thêm V, từ trùm vùng 2 thuộc đủ 4 chiêu
                int learned = ri == 0 ? (boss ? 7 : 3) : ri == 1 ? (boss ? 15 : 7) : 15;
                int coins = _cfg.CoinsBase + _cfg.CoinsPerStage * index;
                _stages.Add(new AdventureStage
                {
                    Index = index,
                    Region = ri,
                    Number = n,
                    Boss = boss,
                    Creature = Names[Math.Min(ri, Names.Length - 1)][Math.Min(n - 1, 4)],
                    Stage = stage,
                    Form = forms != null ? forms[(n - 1) % forms.Length] : AdultForm.None,
                    StatFactor = _cfg.StageFactors[n - 1],
                    Skill = Math.Min(1f, skill),
                    Style = boss ? (ri % 2 == 0 ? BotStyle.Technical : BotStyle.Aggressive) : (BotStyle)((ri + n) % 4),
                    LearnedMask = learned,
                    Coins = boss ? (int)Math.Round(coins * _cfg.BossCoinFactor) : coins,
                });
            }
        }
    }
}
