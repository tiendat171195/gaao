using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Battle
{
    /// <summary>1 vùng trên bản đồ Thám hiểm (GDD G15). Quái trong vùng có chỉ số theo giai đoạn tiến hóa <see cref="Stage"/>.</summary>
    public sealed class AdventureRegion
    {
        public int Index;
        public string Name;
        public string Blurb;
        public EvolutionStage Stage;
        public int FirstStage;
        public int StageCount;
    }

    /// <summary>1 màn Thám hiểm = 1 lần Săn quái (Phase 9.2: các đợt quái rừng, GDD G21). Màn cuối mỗi vùng là trùm.</summary>
    public sealed class AdventureStage
    {
        /// <summary>Số thứ tự trên cả bản đồ (0..).</summary>
        public int Index;

        public int Region;

        /// <summary>Thứ tự trong vùng (1..).</summary>
        public int Number;

        public bool Boss;

        /// <summary>Tên màn (màn trùm = tên trùm), vd "Bầy chất nhờn", "Vua Chất Nhờn".</summary>
        public string Creature;

        public EvolutionStage Stage;

        /// <summary>Chỉ số quái = chỉ số thú chuẩn của giai đoạn × hệ số này (trừ Crit).</summary>
        public float StatFactor;

        /// <summary>Coin thưởng khi thắng lần đầu (1 sao).</summary>
        public int Coins;

        /// <summary>Mã màn hiện cho người chơi, vd "2-5".</summary>
        public string Code => (Region + 1) + "-" + Number;
    }

    /// <summary>Số liệu Thám hiểm (chỉnh ở đây, không phải sửa code) — GDD G15.</summary>
    public sealed class AdventureConfig
    {
        /// <summary>
        /// Hệ số chỉ số quái của 5 màn trong 1 vùng (màn 5 = trùm), so với thú chuẩn của giai đoạn đó.
        /// Đo bằng bot Săn quái (GDD G21, test HuntBalance): ở đúng giai đoạn, người chơi khá thắng chắc 3 màn đầu, trùm khó hơn;
        /// lớn thêm 1 giai đoạn thì vùng cũ dễ hẳn.
        /// </summary>
        public float[] StageFactors = { 0.7f, 0.8f, 0.9f, 0.98f, 1.05f };

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
    /// Thám hiểm (Phase 7.2, C# thuần): bản đồ 5 vùng × 5 màn. Từ Phase 9.2 mỗi màn là 1 lần Săn quái rừng (đánh theo đợt,
    /// khác trận 1-1 PvP — HuntSession / HuntSimulation), màn cuối vùng có trùm.
    /// Độ khó bám theo giai đoạn tiến hóa (vùng 1 cho Baby ... vùng 5 cho Ultimate): bé lớn lên mới đi xa được.
    /// Thắng = 1 sao, còn ≥ 40% / 70% HP = 2 / 3 sao. Coin khi thắng lần đầu, mỗi sao mới, ít hơn khi thắng lại.
    /// Màn sau mở khi màn trước có sao; thắng trùm thì mở vùng sau. Mỗi trận tốn năng lượng + no như đấu Bot.
    /// </summary>
    public sealed class AdventureService
    {
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

        /// <summary>Chỉ số quái chuẩn của màn (đúng công thức chỉ số thú G8 của giai đoạn đó × hệ số màn).</summary>
        public PetStats CreatureStats(int stage)
        {
            AdventureStage s = _stages[stage];
            StatsConfig c = _pets.Config.Stats;
            PetStats p = StatsRules.Compute(s.Stage, AdultForm.None, 1f, c);
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

        /// <summary>Kịch bản Săn quái của màn: các đợt quái rừng + chỉ số quái chuẩn (cố định theo màn).</summary>
        public HuntPlan PlanFor(int stage)
        {
            AdventureStage s = _stages[stage];
            return HuntPlan.Create(s.Region, s.Number, s.Boss, CreatureStats(stage));
        }

        /// <summary>Đi săn màn này. Gọi <see cref="CanStart"/> trước.</summary>
        public HuntSession StartHunt(int stage, ulong seed, HuntConfig cfg = null) =>
            new HuntSession(_pets, _evo, this, stage, seed, cfg);

        /// <summary>Màn kế tiếp đã mở (nút "Đánh tiếp" ở thẻ kết quả), -1 nếu không có.</summary>
        public int NextAfter(int stage) => stage + 1 < _stages.Count && IsUnlocked(stage + 1) ? stage + 1 : -1;

        /// <summary>Sao của 1 trận theo kết quả + %HP thú còn lại (0 = thua / hòa / bỏ cuộc).</summary>
        public int StarsFor(bool won, float hpLeft01)
        {
            if (!won) return 0;
            if (hpLeft01 >= _cfg.ThreeStarHp - 1e-4f) return 3;
            return hpLeft01 >= _cfg.TwoStarHp - 1e-4f ? 2 : 1;
        }

        /// <summary>Hết 1 lần săn: tính sao (theo %HP bé còn lại), cộng Coin, ghi tiến độ, lưu file (HuntSession gọi).</summary>
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
            AddRegion("Đồi Cỏ Gió", "Đồi cỏ sau vườn: chất nhờn lúc nhúc, dơi bay vù vù. Hợp với bé mới nở.", EvolutionStage.Baby);
            AddRegion("Rừng Nấm", "Rừng rậm đầy nấm phát sáng. Nấm độc phun bào tử từ xa, phải lao tới đánh.", EvolutionStage.Child);
            AddRegion("Hồ Sương", "Hồ nước phủ sương mờ. Đá rêu đập đất rất đau: thấy lấy đà thì lướt né.", EvolutionStage.Teen);
            AddRegion("Núi Lửa", "Đá nóng bỏng chân. Heo rừng lao húc: đỡ sát lúc để phản đòn.", EvolutionStage.Adult);
            AddRegion("Hang Pha Lê", "Hang sâu lấp lánh, nơi quái mạnh nhất sinh sống.", EvolutionStage.Ultimate);
        }

        private void AddRegion(string name, string blurb, EvolutionStage stage)
        {
            int ri = _regions.Count;
            var region = new AdventureRegion
            {
                Index = ri, Name = name, Blurb = blurb, Stage = stage, FirstStage = _stages.Count, StageCount = _cfg.StageFactors.Length,
            };
            _regions.Add(region);
            for (int n = 1; n <= region.StageCount; n++)
            {
                bool boss = n == region.StageCount;
                int index = _stages.Count;
                int coins = _cfg.CoinsBase + _cfg.CoinsPerStage * index;
                _stages.Add(new AdventureStage
                {
                    Index = index,
                    Region = ri,
                    Number = n,
                    Boss = boss,
                    Creature = HuntPlan.TitleFor(ri, n, boss),
                    Stage = stage,
                    StatFactor = _cfg.StageFactors[n - 1],
                    Coins = boss ? (int)Math.Round(coins * _cfg.BossCoinFactor) : coins,
                });
            }
        }
    }
}
