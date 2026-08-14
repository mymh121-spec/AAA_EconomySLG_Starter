using System;
using Game.Domain.World;

namespace Game.Application.World
{
    public sealed class MapEconomicSurveyState
    {
        public GridCoordinate Coordinate { get; }
        public MineKind? DepositKind { get; }
        public decimal YieldMultiplier { get; }
        public int SurveyedEconomicDay { get; }
        public bool HasViableDeposit => DepositKind.HasValue;

        public MapEconomicSurveyState(
            GridCoordinate coordinate,
            MineKind? depositKind,
            decimal yieldMultiplier,
            int surveyedEconomicDay)
        {
            Coordinate = coordinate;
            DepositKind = depositKind;
            YieldMultiplier = depositKind.HasValue
                ? Math.Clamp(yieldMultiplier, 0.50m, 1.50m)
                : 0m;
            SurveyedEconomicDay = Math.Max(0, surveyedEconomicDay);
        }
    }

    public sealed class MapMineConstructionState
    {
        public GridCoordinate Coordinate { get; }
        public string OwnerFactionId { get; }
        public MineKind Kind { get; }
        public decimal Cost { get; }
        public int TotalDays { get; }
        public int RemainingDays { get; private set; }
        public decimal YieldMultiplier { get; }
        public bool IsComplete => RemainingDays <= 0;

        public MapMineConstructionState(
            GridCoordinate coordinate,
            string ownerFactionId,
            MineKind kind,
            decimal cost,
            int totalDays,
            decimal yieldMultiplier)
        {
            Coordinate = coordinate;
            OwnerFactionId = ownerFactionId ?? string.Empty;
            Kind = kind;
            Cost = Math.Max(0m, cost);
            TotalDays = Math.Max(1, totalDays);
            RemainingDays = TotalDays;
            YieldMultiplier = Math.Clamp(yieldMultiplier, 0.50m, 1.50m);
        }

        internal bool AdvanceDay()
        {
            if (RemainingDays > 0)
                RemainingDays--;
            return IsComplete;
        }
    }

    public readonly struct MapMineConstructionCompletedRecord
    {
        public GridCoordinate Coordinate { get; }
        public string OwnerFactionId { get; }
        public MineKind Kind { get; }
        public int EconomicDay { get; }
        public decimal YieldMultiplier { get; }

        public MapMineConstructionCompletedRecord(
            GridCoordinate coordinate,
            string ownerFactionId,
            MineKind kind,
            int economicDay,
            decimal yieldMultiplier)
        {
            Coordinate = coordinate;
            OwnerFactionId = ownerFactionId ?? string.Empty;
            Kind = kind;
            EconomicDay = Math.Max(1, economicDay);
            YieldMultiplier = Math.Clamp(yieldMultiplier, 0.50m, 1.50m);
        }
    }

    public static class MapEconomicDevelopmentRules
    {
        public const decimal SurveyCost = 500m;
        public const int SurveyStaminaCost = 1;
        public const decimal NormalMineConstructionCost = 6000m;
        public const decimal GoldMineConstructionCost = 10000m;
        public const int NormalMineConstructionDays = 4;
        public const int GoldMineConstructionDays = 7;

        public static decimal GetConstructionCost(MineKind kind) =>
            kind == MineKind.Gold
                ? GoldMineConstructionCost
                : NormalMineConstructionCost;

        public static int GetConstructionDays(MineKind kind) =>
            kind == MineKind.Gold
                ? GoldMineConstructionDays
                : NormalMineConstructionDays;

        public static MapEconomicSurveyState Evaluate(
            GridMapLayout layout,
            GridCoordinate coordinate,
            int economicDay)
        {
            if (layout == null)
                throw new ArgumentNullException(nameof(layout));

            GridTerrainKind terrain = layout.GetTerrain(coordinate);
            uint hash = Mix(unchecked(
                (uint)layout.Seed * 747796405u ^
                (uint)coordinate.X * 2891336453u ^
                (uint)coordinate.Y * 1181783497u));
            int viableChance = GetViableChance(terrain);
            bool viable = hash % 100u < viableChance;
            if (!viable)
            {
                return new MapEconomicSurveyState(
                    coordinate,
                    null,
                    0m,
                    economicDay);
            }

            uint kindHash = Mix(hash ^ 0x9E3779B9u);
            int goldChance = terrain == GridTerrainKind.Desert
                ? 24
                : terrain == GridTerrainKind.Hills ? 18 : 10;
            MineKind kind = kindHash % 100u < goldChance
                ? MineKind.Gold
                : MineKind.Normal;
            decimal yieldMultiplier =
                0.80m + ((hash >> 16) % 41u) / 100m;
            return new MapEconomicSurveyState(
                coordinate,
                kind,
                yieldMultiplier,
                economicDay);
        }

        private static int GetViableChance(GridTerrainKind terrain)
        {
            switch (terrain)
            {
                case GridTerrainKind.Hills: return 72;
                case GridTerrainKind.Desert: return 58;
                case GridTerrainKind.Tundra: return 52;
                case GridTerrainKind.Plains: return 46;
                case GridTerrainKind.Forest: return 38;
                default: return 0;
            }
        }

        private static uint Mix(uint value)
        {
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            value ^= value >> 16;
            return value;
        }
    }
}
