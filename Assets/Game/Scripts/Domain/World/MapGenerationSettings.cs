using System;

namespace Game.Domain.World
{
    public enum MapSizePreset
    {
        Small,
        Standard,
        Large
    }

    public enum MapResourceAbundance
    {
        Sparse,
        Standard,
        Rich
    }

    public enum MapWaterLevel
    {
        Low,
        Standard,
        High
    }

    public sealed class MapGenerationSettings
    {
        public MapSizePreset SizePreset { get; }
        public MapResourceAbundance ResourceAbundance { get; }
        public MapWaterLevel WaterLevel { get; }
        public int Seed { get; }
        public int NeutralCastleCount { get; }
        public bool WrapHorizontally { get; }

        public int Width => SizePreset == MapSizePreset.Small
            ? 48
            : SizePreset == MapSizePreset.Large ? 112 : 80;
        public int Height => SizePreset == MapSizePreset.Small
            ? 30
            : SizePreset == MapSizePreset.Large ? 64 : 48;
        public int MineCount
        {
            get
            {
                decimal density = ResourceAbundance == MapResourceAbundance.Sparse
                    ? 0.025m
                    : ResourceAbundance == MapResourceAbundance.Rich
                        ? 0.055m
                        : 0.042m;
                return Math.Max(20, (int)Math.Round(Width * Height * density));
            }
        }
        public double OceanThreshold => WaterLevel == MapWaterLevel.Low
            ? 0.27d
            : WaterLevel == MapWaterLevel.High ? 0.41d : 0.34d;

        public MapGenerationSettings(
            MapSizePreset sizePreset = MapSizePreset.Standard,
            MapResourceAbundance resourceAbundance =
                MapResourceAbundance.Standard,
            MapWaterLevel waterLevel = MapWaterLevel.Standard,
            int seed = 42,
            int neutralCastleCount = 8,
            bool wrapHorizontally = true)
        {
            SizePreset = sizePreset;
            ResourceAbundance = resourceAbundance;
            WaterLevel = waterLevel;
            Seed = seed;
            NeutralCastleCount = Math.Clamp(neutralCastleCount, 0, 24);
            WrapHorizontally = wrapHorizontally;
        }
    }
}
