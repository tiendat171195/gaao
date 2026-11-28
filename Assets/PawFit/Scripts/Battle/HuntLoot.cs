using System;
using System.Collections.Generic;
using PawFit.Core;

namespace PawFit.Battle
{
    /// <summary>1 lần quái rơi hạt giống khi bị hạ (Phase 10.3).</summary>
    public readonly struct SeedDrop
    {
        public readonly CropKind Kind;
        public readonly int Count;
        public readonly MonsterKind From;

        public SeedDrop(CropKind kind, int count, MonsterKind from)
        {
            Kind = kind;
            Count = count;
            From = from;
        }
    }

    public sealed class HuntLootConfig
    {
        /// <summary>Quái thường bị hạ: chừng này phần trăm rơi 1 hạt (mỗi lần săn tối đa MaxDrops hạt từ quái thường).</summary>
        public float DropChance = 0.3f;
        public int MaxDrops = 4;

        /// <summary>Trùm bị hạ luôn rơi chừng này hạt quý của vùng.</summary>
        public int BossSeeds = 2;
    }

    /// <summary>
    /// Quái rừng rơi hạt giống cho vườn rau (Phase 10.3, C# thuần): vùng càng xa hạt càng quý (Đồi Cỏ Gió: dâu tây / cà rốt …
    /// Núi Lửa, Hang Pha Lê: bí ngô). Trùm luôn rơi 2 hạt quý. Quay số bằng SeededRandom của lần săn nên test lặp lại được.
    /// </summary>
    public static class HuntLoot
    {
        private static readonly CropKind[][] RegionSeeds =
        {
            new[] { CropKind.Strawberry, CropKind.Carrot },   // Đồi Cỏ Gió
            new[] { CropKind.Carrot, CropKind.Blueberry },    // Rừng Nấm
            new[] { CropKind.Blueberry, CropKind.Strawberry }, // Hồ Sương
            new[] { CropKind.Pumpkin, CropKind.Carrot },      // Núi Lửa
            new[] { CropKind.Pumpkin, CropKind.Blueberry },   // Hang Pha Lê
        };

        private static readonly CropKind[] BossSeeds =
            { CropKind.Blueberry, CropKind.Blueberry, CropKind.Pumpkin, CropKind.Pumpkin, CropKind.Pumpkin };

        private static int Clamp(int region) => Math.Max(0, Math.Min(RegionSeeds.Length - 1, region));

        /// <summary>Các loại hạt quái thường của vùng hay rơi.</summary>
        public static IReadOnlyList<CropKind> SeedsOf(int region) => RegionSeeds[Clamp(region)];

        /// <summary>Hạt quý trùm vùng này rơi.</summary>
        public static CropKind BossSeed(int region) => BossSeeds[Clamp(region)];

        /// <summary>Quái `kind` vừa bị hạ ở vùng `region` (đã rơi `dropped` hạt từ quái thường lần săn này): rơi gì không.</summary>
        public static SeedDrop? Roll(MonsterKind kind, int region, int dropped, IRandom rng, HuntLootConfig cfg)
        {
            if (rng == null) throw new ArgumentNullException(nameof(rng));
            cfg = cfg ?? new HuntLootConfig();
            if (MonsterBook.Get(kind).Boss) return new SeedDrop(BossSeed(region), Math.Max(1, cfg.BossSeeds), kind);
            if (dropped >= cfg.MaxDrops || rng.NextDouble() >= cfg.DropChance) return null;
            CropKind[] pool = RegionSeeds[Clamp(region)];
            return new SeedDrop(pool[rng.Next(pool.Length)], 1, kind);
        }
    }
}
