using System;
using System.Collections.Generic;
using Game.Domain.World;

namespace Game.Application.World
{
    public enum FactionStrategyKind
    {
        EconomicExpansion,
        MilitaryDominance,
        LongTermBalance
    }

    public enum StrategicObjectiveKind
    {
        Mine,
        Castle,
        Resupply
    }

    public readonly struct StrategicObjective
    {
        public StrategicObjectiveKind Kind { get; }
        public GridCoordinate Coordinate { get; }
        public decimal Score { get; }

        public StrategicObjective(
            StrategicObjectiveKind kind,
            GridCoordinate coordinate,
            decimal score)
        {
            Kind = kind;
            Coordinate = coordinate;
            Score = score;
        }
    }

    public static class FactionStrategicAi
    {
        public static FactionStrategyKind GetStrategy(int factionIndex)
        {
            switch (PositiveModulo(factionIndex, 3))
            {
                case 0: return FactionStrategyKind.EconomicExpansion;
                case 1: return FactionStrategyKind.MilitaryDominance;
                default: return FactionStrategyKind.LongTermBalance;
            }
        }

        public static bool TryChooseObjective(
            GridMapLayout layout,
            IReadOnlyList<MapMineControlState> mines,
            IReadOnlyList<MapCastleControlState> castles,
            IReadOnlyList<MapUnitState> units,
            string factionId,
            MapUnitState unit,
            FactionStrategyKind strategy,
            GridCoordinate? previousLongTermTarget,
            out StrategicObjective objective)
        {
            objective = default;
            decimal bestScore = decimal.MinValue;

            for (int i = 0; i < mines.Count; i++)
            {
                MapMineControlState mine = mines[i];
                if (string.Equals(
                    mine.OwnerFactionId,
                    factionId,
                    StringComparison.Ordinal))
                    continue;
                decimal value = mine.Kind == MineKind.Gold ? 125m : 90m;
                value *= strategy == FactionStrategyKind.EconomicExpansion
                    ? 1.45m
                    : strategy == FactionStrategyKind.MilitaryDominance
                        ? 0.72m
                        : 1m;
                Consider(
                    StrategicObjectiveKind.Mine,
                    mine.Coordinate,
                    value,
                    layout,
                    units,
                    factionId,
                    unit,
                    previousLongTermTarget,
                    ref objective,
                    ref bestScore);
            }

            for (int i = 0; i < castles.Count; i++)
            {
                MapCastleControlState castle = castles[i];
                if (castle.IsDestroyed || string.Equals(
                    castle.OwnerFactionId,
                    factionId,
                    StringComparison.Ordinal))
                    continue;
                decimal value = castle.IsCapital ? 175m : 120m;
                value *= strategy == FactionStrategyKind.MilitaryDominance
                    ? 1.50m
                    : strategy == FactionStrategyKind.EconomicExpansion
                        ? 0.72m
                        : 1.10m;
                Consider(
                    StrategicObjectiveKind.Castle,
                    castle.Coordinate,
                    value,
                    layout,
                    units,
                    factionId,
                    unit,
                    previousLongTermTarget,
                    ref objective,
                    ref bestScore);
            }

            if (unit.UsesSupplySystem && unit.SupplyRatio < 0.35m)
            {
                for (int i = 0; i < castles.Count; i++)
                {
                    MapCastleControlState castle = castles[i];
                    if (!castle.IsDestroyed && string.Equals(
                        castle.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                    {
                        Consider(
                            StrategicObjectiveKind.Resupply,
                            castle.Coordinate,
                            220m * (1m - unit.SupplyRatio),
                            layout,
                            units,
                            factionId,
                            unit,
                            previousLongTermTarget,
                            ref objective,
                            ref bestScore);
                    }
                }
            }

            return bestScore > decimal.MinValue;
        }

        private static void Consider(
            StrategicObjectiveKind kind,
            GridCoordinate coordinate,
            decimal value,
            GridMapLayout layout,
            IReadOnlyList<MapUnitState> units,
            string factionId,
            MapUnitState actingUnit,
            GridCoordinate? previousTarget,
            ref StrategicObjective best,
            ref decimal bestScore)
        {
            int distance = layout.ManhattanDistance(
                actingUnit.Coordinate,
                coordinate);
            decimal risk = 0m;
            decimal support = 0m;
            for (int i = 0; i < units.Count; i++)
            {
                MapUnitState other = units[i];
                if (!other.Coordinate.Equals(coordinate))
                    continue;
                if (string.Equals(
                    other.OwnerFactionId,
                    factionId,
                    StringComparison.Ordinal))
                    support += other.DefensePower / 150m;
                else
                    risk += other.DefensePower / 100m;
            }

            decimal supplyPenalty = actingUnit.UsesSupplySystem
                ? distance * (1m - actingUnit.SupplyRatio) * 2.5m
                : 0m;
            decimal commitment = previousTarget.HasValue &&
                previousTarget.Value.Equals(coordinate)
                    ? 32m
                    : 0m;
            decimal score = value + support + commitment -
                distance * 3.25m - risk - supplyPenalty;
            if (score <= bestScore)
                return;
            bestScore = score;
            best = new StrategicObjective(kind, coordinate, score);
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
