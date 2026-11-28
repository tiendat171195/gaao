using System;
using System.Collections.Generic;
using PawFit.Core;
using PawFit.Pet;

namespace PawFit.Battle
{
    /// <summary>Quái rừng của Săn quái (Phase 9.2). 5 loại thường + 5 trùm (mỗi vùng 1 trùm).</summary>
    public enum MonsterKind
    {
        Slime = 0, Bat = 1, Mushroom = 2, Boar = 3, Golem = 4,
        KingSlime = 5, MushroomKing = 6, StoneGolem = 7, BoarChief = 8, CrystalGolem = 9,
    }

    /// <summary>Kiểu đòn của quái: cắn / đập đất (trúng cả vùng) / bắn từ xa / lao húc.</summary>
    public enum MonsterMove { Melee = 0, Smash = 1, Ranged = 2, Charge = 3 }

    /// <summary>1 kiểu đòn của quái. Trước khi đánh quái "lấy đà" Windup giây (View hiện dấu ! đỏ) để người chơi kịp đỡ / lướt né.</summary>
    public sealed class MonsterAttackDef
    {
        public MonsterMove Move;

        /// <summary>Khoảng cách (mép thân tới mép thân, m) thì bắt đầu đánh. Bắn: tầm bắn. Lao húc: lao khi bé trong tầm này.</summary>
        public float Range;

        /// <summary>Đập đất: trúng mọi thứ cách tâm quái ≤ Radius (m).</summary>
        public float Radius;

        public float Windup, Recover, Cooldown;

        /// <summary>Sức đòn (nhân vào công thức sát thương).</summary>
        public float Power = 1f;

        /// <summary>Bắn: tốc độ đạn (m/s), số viên 1 loạt, cách nhau bao giây; đạn sát đất (sóng đất) thì Low.</summary>
        public float ShotSpeed;
        public int Shots = 1;
        public float ShotGap = 0.25f;
        public bool LowShot;

        /// <summary>Lao húc: tốc độ lao (m/s). Lao xong quái bị choáng Recover giây (lúc đó đánh dễ).</summary>
        public float ChargeSpeed;
    }

    /// <summary>Thông số 1 loại quái. Máu / công / thủ = chỉ số thú chuẩn của giai đoạn vùng × hệ số ở đây.</summary>
    public sealed class MonsterDef
    {
        public MonsterKind Kind;
        public string Name;
        public bool Boss;
        public float Hp, Atk, Def;

        /// <summary>Tốc độ đi (m/s), nửa bề ngang thân (m), bay cao bao nhiêu (0 = đi đất).</summary>
        public float Speed, Size, Flying;

        /// <summary>Quái bắn: đứng cách bé chừng này (m) mà bắn, bé lại gần thì lùi.</summary>
        public float KeepDistance;

        public MonsterAttackDef Primary;

        /// <summary>Đòn phụ (có thể null): dùng khi đòn chính không hợp (bé đứng quá gần / quá xa).</summary>
        public MonsterAttackDef Secondary;

        /// <summary>Đang lấy đà mà bị đánh trúng thì khựng, mất đòn (quái nhỏ). Trùm thì không.</summary>
        public bool Interruptible = true;

        /// <summary>Trùm gọi thêm quái: loại nào, mỗi bao giây, mỗi lần mấy con, tối đa bao nhiêu con cùng lúc.</summary>
        public MonsterKind Summon;
        public float SummonEvery;
        public int SummonCount;
        public int SummonCap;
    }

    /// <summary>Sách quái: thông số cố định của từng loại (chỉnh ở đây). Tên tiếng Việt hiện trên HUD.</summary>
    public static class MonsterBook
    {
        private static readonly MonsterDef[] Defs =
        {
            new MonsterDef
            {
                Kind = MonsterKind.Slime, Name = "Chất Nhờn", Hp = 0.4f, Atk = 0.8f, Def = 0.6f, Speed = 1.3f, Size = 0.32f,
                Primary = new MonsterAttackDef { Move = MonsterMove.Melee, Range = 0.35f, Windup = 0.6f, Recover = 0.35f, Cooldown = 1.3f, Power = 3f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.Bat, Name = "Dơi Rừng", Hp = 0.22f, Atk = 0.7f, Def = 0.4f, Speed = 2.6f, Size = 0.25f, Flying = 1.2f,
                Primary = new MonsterAttackDef { Move = MonsterMove.Melee, Range = 0.3f, Windup = 0.45f, Recover = 0.3f, Cooldown = 1.1f, Power = 2.4f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.Mushroom, Name = "Nấm Độc", Hp = 0.34f, Atk = 0.8f, Def = 0.7f, Speed = 0.8f, Size = 0.33f, KeepDistance = 3.4f,
                Primary = new MonsterAttackDef { Move = MonsterMove.Ranged, Range = 4.6f, Windup = 0.7f, Recover = 0.4f, Cooldown = 2.6f, Power = 2.8f, ShotSpeed = 5f },
                Secondary = new MonsterAttackDef { Move = MonsterMove.Melee, Range = 0.35f, Windup = 0.55f, Recover = 0.4f, Cooldown = 1.6f, Power = 2.2f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.Boar, Name = "Heo Rừng", Hp = 0.7f, Atk = 1f, Def = 0.9f, Speed = 1.1f, Size = 0.45f,
                Primary = new MonsterAttackDef { Move = MonsterMove.Charge, Range = 5.5f, Windup = 0.85f, Recover = 1.1f, Cooldown = 2.4f, Power = 4f, ChargeSpeed = 7.5f },
                Secondary = new MonsterAttackDef { Move = MonsterMove.Melee, Range = 0.4f, Windup = 0.6f, Recover = 0.4f, Cooldown = 1.5f, Power = 2.8f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.Golem, Name = "Đá Rêu", Hp = 1f, Atk = 1f, Def = 1.3f, Speed = 0.65f, Size = 0.5f,
                Primary = new MonsterAttackDef { Move = MonsterMove.Smash, Range = 0.7f, Radius = 1.7f, Windup = 0.95f, Recover = 0.5f, Cooldown = 2.4f, Power = 3.8f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.KingSlime, Name = "Vua Chất Nhờn", Boss = true, Hp = 5f, Atk = 1f, Def = 0.9f, Speed = 0.9f, Size = 0.85f,
                Interruptible = false, Summon = MonsterKind.Slime, SummonEvery = 10f, SummonCount = 2, SummonCap = 3,
                Primary = new MonsterAttackDef { Move = MonsterMove.Smash, Range = 0.8f, Radius = 2.2f, Windup = 0.95f, Recover = 0.7f, Cooldown = 2.4f, Power = 4.2f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.MushroomKing, Name = "Chúa Nấm", Boss = true, Hp = 4.8f, Atk = 1f, Def = 1f, Speed = 0.7f, Size = 0.8f, KeepDistance = 4f,
                Interruptible = false, Summon = MonsterKind.Mushroom, SummonEvery = 12f, SummonCount = 1, SummonCap = 2,
                Primary = new MonsterAttackDef { Move = MonsterMove.Ranged, Range = 6f, Windup = 0.8f, Recover = 0.5f, Cooldown = 3f, Power = 2.8f, ShotSpeed = 5.5f, Shots = 3, ShotGap = 0.3f },
                Secondary = new MonsterAttackDef { Move = MonsterMove.Smash, Range = 0.7f, Radius = 2f, Windup = 0.85f, Recover = 0.6f, Cooldown = 2f, Power = 3.8f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.StoneGolem, Name = "Người Đá Sương", Boss = true, Hp = 5.6f, Atk = 1f, Def = 1.3f, Speed = 0.6f, Size = 0.9f,
                Interruptible = false,
                Primary = new MonsterAttackDef { Move = MonsterMove.Smash, Range = 0.9f, Radius = 2.6f, Windup = 1.1f, Recover = 0.8f, Cooldown = 2.6f, Power = 4.6f },
                Secondary = new MonsterAttackDef { Move = MonsterMove.Ranged, Range = 7f, Windup = 0.9f, Recover = 0.6f, Cooldown = 3.2f, Power = 3.2f, ShotSpeed = 6f, LowShot = true },
            },
            new MonsterDef
            {
                Kind = MonsterKind.BoarChief, Name = "Heo Lửa Đầu Đàn", Boss = true, Hp = 5f, Atk = 1f, Def = 1.1f, Speed = 1f, Size = 0.85f,
                Interruptible = false, Summon = MonsterKind.Boar, SummonEvery = 14f, SummonCount = 1, SummonCap = 1,
                Primary = new MonsterAttackDef { Move = MonsterMove.Charge, Range = 7f, Windup = 0.8f, Recover = 1.2f, Cooldown = 2.6f, Power = 4.6f, ChargeSpeed = 8.5f },
                Secondary = new MonsterAttackDef { Move = MonsterMove.Smash, Range = 0.8f, Radius = 2.1f, Windup = 0.9f, Recover = 0.6f, Cooldown = 2.2f, Power = 4f },
            },
            new MonsterDef
            {
                Kind = MonsterKind.CrystalGolem, Name = "Người Đá Pha Lê", Boss = true, Hp = 5.6f, Atk = 1f, Def = 1.3f, Speed = 0.7f, Size = 0.9f,
                Interruptible = false, Summon = MonsterKind.Bat, SummonEvery = 11f, SummonCount = 2, SummonCap = 3,
                Primary = new MonsterAttackDef { Move = MonsterMove.Smash, Range = 0.9f, Radius = 2.4f, Windup = 1f, Recover = 0.7f, Cooldown = 2.4f, Power = 4.6f },
                Secondary = new MonsterAttackDef { Move = MonsterMove.Ranged, Range = 6.5f, Windup = 0.8f, Recover = 0.5f, Cooldown = 3f, Power = 3f, ShotSpeed = 6.5f, Shots = 2, ShotGap = 0.35f },
            },
        };

        public static MonsterDef Get(MonsterKind kind) => Defs[(int)kind];

        public static IReadOnlyList<MonsterDef> All => Defs;
    }

    /// <summary>Số liệu Săn quái — GDD G21 (chỉnh ở đây, không phải sửa code).</summary>
    public sealed class HuntConfig
    {
        public float TickSeconds = 1f / 30f;

        /// <summary>Lối đánh là 1 dải ngang: quái đi vào từ ±(ArenaHalf + 0,5), bé chạy trong ±HunterHalf (m).</summary>
        public float ArenaHalf = 8f;
        public float HunterHalf = 7.8f;
        public float HunterSize = 0.4f;

        /// <summary>Máu bé khi săn = HP chỉ số × bấy nhiêu (đánh nhiều quái nên dày máu hơn trận 1-1).</summary>
        public float HunterHpScale = 1.4f;

        /// <summary>Chạm = bé lao tới quái gần nhất (hoặc con được chạm) đánh 1 đòn: tầm với, tốc độ lao, lúc đòn trúng, đòn dài bao lâu.</summary>
        public float StrikeReach = 0.55f;
        public float ApproachSpeed = 8f;
        public float StrikeHitTime = 0.1f;
        public float StrikeTime = 0.22f;
        public float StrikePower = 1f;

        /// <summary>2 đòn chạm cách nhau ít nhất bấy nhiêu giây (SPD cao thì nhanh hơn chút).</summary>
        public float StrikeCooldown = 0.32f;

        /// <summary>Đánh trúng liên tục (cách nhau ≤ ComboWindow giây) thì mạnh dần: mỗi bậc +ComboStep, tối đa ComboMax bậc.</summary>
        public float ComboWindow = 1.6f;
        public float ComboStep = 0.06f;
        public int ComboMax = 8;
        public float CritMultiplier = 1.6f;

        /// <summary>Vuốt lên = đập đất: trúng mọi quái trong bán kính, đẩy lùi + choáng.</summary>
        public float SlamTime = 0.4f;
        public float SlamHitTime = 0.32f;
        public float SlamRadius = 2.4f;
        public float SlamPower = 1.4f;
        public float SlamKnockback = 1.6f;
        public float SlamStun = 0.7f;
        public float SlamCooldown = 5f;

        /// <summary>Vuốt xuống = đỡ: đỡ trúng bị trừ GuardTakes phần sát thương; đỡ sát lúc bị đánh (≤ PerfectWindow giây) = phản đòn.</summary>
        public float GuardTime = 0.45f;
        public float PerfectWindow = 0.22f;
        public float GuardTakes = 0.25f;
        public float CounterPower = 0.9f;
        public float CounterStun = 1f;
        public float GuardCooldown = 0.5f;

        /// <summary>Vuốt ngang = lướt: lướt nhanh 1 đoạn, không bị đánh trúng lúc lướt, lướt xuyên quái thì gây sát thương.</summary>
        public float DashDistance = 3.2f;
        public float DashTime = 0.22f;
        public float DashPower = 0.5f;
        public float DashCooldown = 1.1f;

        /// <summary>Nộ đầy (vuốt Z) = tuyệt chiêu trúng mọi quái + choáng.</summary>
        public float RageMax = 100f;
        public float RageOnHit = 5f;
        public float RageOnKill = 6f;
        public float RageOnHurt = 10f;
        public float RagePerfectGuard = 20f;
        public float UltimateTime = 0.8f;
        public float UltimateHitTime = 0.5f;
        public float UltimatePower = 3f;
        public float UltimateStun = 1.5f;

        /// <summary>Bị đánh trúng (không đỡ) thì khựng bấy nhiêu giây.</summary>
        public float HurtTime = 0.25f;

        /// <summary>Lệnh vuốt / chạm lúc bé đang bận được giữ lại bấy nhiêu giây rồi làm ngay khi rảnh.</summary>
        public float InputBuffer = 0.3f;

        /// <summary>
        /// Quái nhỏ bị đánh trúng lúc mới lấy đà (còn hơn InterruptBefore phần thời gian lấy đà) thì khựng, chờ InterruptCooldown giây
        /// mới lấy đà lại. Sát lúc ra đòn thì đánh không cắt được nữa: phải đỡ / lướt (đập đất / tuyệt chiêu / phản đòn thì luôn cắt được).
        /// </summary>
        public float InterruptBefore = 0.5f;
        public float InterruptCooldown = 0.7f;

        /// <summary>Trùm còn dưới bấy nhiêu phần máu thì nổi giận: lấy đà nhanh hơn, ít nghỉ hơn, đi nhanh hơn.</summary>
        public float EnrageBelow = 0.5f;
        public float EnrageWindup = 0.8f;
        public float EnrageCooldown = 0.75f;
        public float EnrageSpeed = 1.2f;

        /// <summary>Nghỉ giữa 2 đợt quái; đợt đầu bắt đầu sau StartDelay giây.</summary>
        public float StartDelay = 1f;
        public float WaveBreak = 1.6f;

        /// <summary>An toàn: quá bấy nhiêu giây mà chưa xong thì tính thua.</summary>
        public float MaxSeconds = 300f;

        /// <summary>Điểm tập mỗi lần săn: mỗi quái hạ được + Sức mạnh, mỗi lần đỡ sát lúc / lướt né + Tốc độ (có trần mỗi lần săn).</summary>
        public float KillStrength = 0.4f;
        public float MaxStrengthPoints = 6f;
        public float DodgeSpeed = 0.5f;
        public float MaxSpeedPoints = 4f;
    }

    /// <summary>1 con quái xuất hiện: loại, bên nào (+1 phải, -1 trái), chờ bao giây sau con trước.</summary>
    public struct HuntSpawn
    {
        public MonsterKind Kind;
        public int Side;
        public float Delay;
    }

    public sealed class HuntWave
    {
        public readonly List<HuntSpawn> Spawns = new List<HuntSpawn>();
    }

    /// <summary>
    /// Kịch bản 1 màn Săn quái: các đợt quái + chỉ số chuẩn của vùng. Cố định theo màn (cùng màn = cùng đợt quái),
    /// chỉ thứ tự ra hơi khác theo màn. Màn 1–4: 3 đợt, đông dần; màn 5: 1 đợt quái + đợt trùm.
    /// </summary>
    public sealed class HuntPlan
    {
        private static readonly MonsterKind[][] Pools =
        {
            new[] { MonsterKind.Slime, MonsterKind.Bat },                                                // Đồi Cỏ Gió
            new[] { MonsterKind.Mushroom, MonsterKind.Slime, MonsterKind.Bat },                          // Rừng Nấm
            new[] { MonsterKind.Golem, MonsterKind.Slime, MonsterKind.Bat, MonsterKind.Mushroom },       // Hồ Sương
            new[] { MonsterKind.Boar, MonsterKind.Bat, MonsterKind.Golem, MonsterKind.Slime },           // Núi Lửa
            new[] { MonsterKind.Golem, MonsterKind.Bat, MonsterKind.Boar, MonsterKind.Mushroom },        // Hang Pha Lê
        };

        private static readonly MonsterKind[] Bosses =
            { MonsterKind.KingSlime, MonsterKind.MushroomKing, MonsterKind.StoneGolem, MonsterKind.BoarChief, MonsterKind.CrystalGolem };

        /// <summary>Vùng đầu tiên có loài quái này (sổ tay quái rừng chỉ chỗ gặp, Phase 10.3). -1 = không vùng nào.</summary>
        public static int FirstRegionOf(MonsterKind kind)
        {
            for (int r = 0; r < Pools.Length; r++)
            {
                if (Bosses[r] == kind) return r;
                foreach (MonsterKind k in Pools[r])
                    if (k == kind) return r;
            }
            return -1;
        }

        private static readonly string[][] Titles =
        {
            new[] { "Chất nhờn đầu đồi", "Dơi gió", "Bầy chất nhờn", "Đồi rộn ràng" },
            new[] { "Nấm độc ven rừng", "Chất nhờn rêu", "Dơi hang nấm", "Rừng nấm phát sáng" },
            new[] { "Đá rêu bờ hồ", "Chất nhờn sương", "Dơi mặt hồ", "Sương mù dày đặc" },
            new[] { "Heo rừng nham thạch", "Dơi lửa", "Đá nóng", "Miệng núi lửa" },
            new[] { "Đá pha lê", "Dơi pha lê", "Heo hang sâu", "Lòng hang sâu" },
        };

        public int Region;
        public int Number;
        public bool Boss;
        public string Title;
        public MonsterKind BossKind;
        public readonly List<HuntWave> Waves = new List<HuntWave>();

        /// <summary>Chỉ số chuẩn của quái (thú chuẩn của giai đoạn vùng × hệ số màn).</summary>
        public float RefHp, RefAtk, RefDef, RefSpd;

        public int TotalMonsters
        {
            get
            {
                int n = 0;
                foreach (HuntWave w in Waves) n += w.Spawns.Count;
                return n;
            }
        }

        /// <summary>Loại quái có trong màn (để bản đồ / thẻ màn ghi "Gặp: ...").</summary>
        public List<MonsterKind> Kinds
        {
            get
            {
                var k = new List<MonsterKind>();
                foreach (HuntWave w in Waves)
                    foreach (HuntSpawn s in w.Spawns)
                        if (!k.Contains(s.Kind)) k.Add(s.Kind);
                return k;
            }
        }

        public static string TitleFor(int region, int number, bool boss) =>
            boss ? MonsterBook.Get(Bosses[Math.Max(0, Math.Min(Bosses.Length - 1, region))]).Name
                 : Titles[Math.Max(0, Math.Min(Titles.Length - 1, region))][Math.Max(0, Math.Min(3, number - 1))];

        /// <summary>Dựng kịch bản cho 1 màn (region 0..4, number 1..5) với chỉ số chuẩn cho trước.</summary>
        public static HuntPlan Create(int region, int number, bool boss, PetStats reference)
        {
            int r = Math.Max(0, Math.Min(Pools.Length - 1, region));
            MonsterKind[] pool = Pools[r];
            var rng = new SeededRandom(0x4A57_0000UL + (ulong)(r * 16 + number), 3UL);
            var plan = new HuntPlan
            {
                Region = r, Number = number, Boss = boss, Title = TitleFor(r, number, boss), BossKind = Bosses[r],
                RefHp = reference.Hp, RefAtk = reference.Atk, RefDef = reference.Def, RefSpd = reference.Spd,
            };
            if (!boss)
            {
                for (int w = 0; w < 3; w++)
                {
                    var wave = new HuntWave();
                    int count = 3 + number / 2 + w;
                    for (int i = 0; i < count; i++)
                    {
                        // đợt đầu: quái đặc trưng của vùng (dạy cách đánh), đợt sau trộn dần cả bầy
                        MonsterKind kind = w == 0 ? pool[i % Math.Min(pool.Length, number >= 2 ? 2 : 1)]
                                                  : pool[(i + w + number) % pool.Length];
                        int side = w == 0 ? 1 : w == 1 ? (i % 2 == 0 ? 1 : -1) : (rng.NextDouble() < 0.5 ? 1 : -1);
                        wave.Spawns.Add(new HuntSpawn { Kind = kind, Side = side, Delay = i == 0 ? 0f : 0.7f + (float)rng.NextDouble() * 0.7f });
                    }
                    plan.Waves.Add(wave);
                }
            }
            else
            {
                var adds = new HuntWave();
                for (int i = 0; i < 4; i++)
                    adds.Spawns.Add(new HuntSpawn { Kind = pool[i % pool.Length], Side = i % 2 == 0 ? 1 : -1, Delay = i == 0 ? 0f : 0.9f });
                plan.Waves.Add(adds);
                var bossWave = new HuntWave();
                bossWave.Spawns.Add(new HuntSpawn { Kind = Bosses[r], Side = 1, Delay = 0f });
                bossWave.Spawns.Add(new HuntSpawn { Kind = pool[0], Side = -1, Delay = 1.5f });
                bossWave.Spawns.Add(new HuntSpawn { Kind = pool[1 % pool.Length], Side = -1, Delay = 1.2f });
                plan.Waves.Add(bossWave);
            }
            return plan;
        }
    }

    /// <summary>Chỉ số của bé khi đi săn (lấy từ chỉ số trận, đã tính bệnh).</summary>
    public struct HunterStats
    {
        public float Hp, Atk, Def, Spd, Crit;

        public static HunterStats From(PetStats s) => new HunterStats { Hp = s.Hp, Atk = s.Atk, Def = s.Def, Spd = s.Spd, Crit = s.Crit };
    }
}
