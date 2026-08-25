using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.World;

namespace Game.Application.World
{
    public enum MapWorldMissionAction
    {
        Scout,
        Escort,
        Raid,
        Occupy,
        Deliver,
        Smuggle,
        Sabotage
    }

    public enum MapWorldMissionStatus
    {
        EnRoute,
        Performing,
        Completed,
        Cancelled
    }

    public sealed class MapWorldMissionState
    {
        public string OpportunityId { get; }
        public string UnitId { get; }
        public GridCoordinate Target { get; }
        public MapWorldMissionAction Action { get; }
        public MapWorldMissionStatus Status { get; internal set; }
        public GridCoordinate? CargoSource { get; internal set; }
        public MapSupplyKind? CargoKind { get; internal set; }
        public decimal RequiredCargo { get; internal set; }
        public decimal LoadedCargo { get; internal set; }
        public decimal DeliveredCargo { get; internal set; }
        public int SabotageDamage { get; internal set; }
        public decimal ExecutionBonus => Action == MapWorldMissionAction.Deliver
            ? Math.Min(12m, DeliveredCargo * 0.35m)
            : Action == MapWorldMissionAction.Smuggle
                ? Math.Min(10m, DeliveredCargo * 0.30m)
                : Action == MapWorldMissionAction.Sabotage
                    ? Math.Min(12m, SabotageDamage / 25m)
                    : 0m;

        public MapWorldMissionState(
            string opportunityId,
            string unitId,
            GridCoordinate target,
            MapWorldMissionAction action)
        {
            OpportunityId = opportunityId ?? string.Empty;
            UnitId = unitId ?? string.Empty;
            Target = target;
            Action = action;
            Status = MapWorldMissionStatus.EnRoute;
            CargoSource = null;
            CargoKind = null;
            RequiredCargo = 0m;
            LoadedCargo = 0m;
            DeliveredCargo = 0m;
            SabotageDamage = 0;
        }
    }

    public readonly struct WorldMissionMapTarget
    {
        public GridCoordinate Coordinate { get; }
        public MapWorldMissionAction Action { get; }
        public MapSupplyKind? CargoKind { get; }
        public decimal RequiredCargo { get; }

        public WorldMissionMapTarget(
            GridCoordinate coordinate,
            MapWorldMissionAction action,
            MapSupplyKind? cargoKind = null,
            decimal requiredCargo = 0m)
        {
            Coordinate = coordinate;
            Action = action;
            CargoKind = cargoKind;
            RequiredCargo = Math.Max(0m, requiredCargo);
        }
    }

    public static class WorldMissionMapBinder
    {
        public static bool TryBind(
            WorldOpportunity opportunity,
            GridMapLayout layout,
            RealtimeMapGameplayService gameplay,
            out WorldMissionMapTarget target,
            out string reason,
            WorldOperationApproach? approach = null,
            ResourceId? eventResourceId = null)
        {
            target = default;
            if (opportunity == null || layout == null || gameplay == null)
            {
                reason = "미션과 지도 정보가 준비되지 않았습니다.";
                return false;
            }

            MapWorldMissionAction action = GetAction(
                opportunity.Kind,
                approach);
            var candidates = new List<GridCoordinate>();
            AddSupplyRouteCandidates(candidates, layout, gameplay, action);

            if (action == MapWorldMissionAction.Occupy ||
                action == MapWorldMissionAction.Sabotage ||
                action == MapWorldMissionAction.Smuggle)
                AddTargetCastles(candidates, gameplay);
            if (action == MapWorldMissionAction.Deliver)
                AddFriendlyCastles(candidates, gameplay);
            if (opportunity.Kind == WorldOpportunityKind.RepairMine ||
                opportunity.Kind == WorldOpportunityKind.SurveyVein ||
                opportunity.Kind == WorldOpportunityKind.SuppressBandits)
            {
                AddMines(candidates, gameplay, opportunity.Kind);
            }
            if (candidates.Count == 0)
                candidates.Add(layout.PlayerStart);

            int index = PositiveModulo(StableHash(
                opportunity.Id + ":" + opportunity.RegionId.Value),
                candidates.Count);
            MapSupplyKind? cargoKind =
                action == MapWorldMissionAction.Deliver ||
                action == MapWorldMissionAction.Smuggle
                    ? ResolveCargoKind(eventResourceId)
                    : (MapSupplyKind?)null;
            decimal requiredCargo = cargoKind.HasValue
                ? Math.Round(
                    8m + opportunity.Difficulty * 22m,
                    1,
                    MidpointRounding.AwayFromZero)
                : 0m;
            target = new WorldMissionMapTarget(
                candidates[index],
                action,
                cargoKind,
                requiredCargo);
            reason = string.Empty;
            return true;
        }

        private static MapWorldMissionAction GetAction(
            WorldOpportunityKind kind,
            WorldOperationApproach? approach)
        {
            switch (kind)
            {
                case WorldOpportunityKind.SuppressBandits:
                    return MapWorldMissionAction.Raid;
                case WorldOpportunityKind.EscortSupply:
                    return approach == WorldOperationApproach.CovertAction
                        ? MapWorldMissionAction.Smuggle
                        : MapWorldMissionAction.Escort;
                case WorldOpportunityKind.EmergencyDelivery:
                    return MapWorldMissionAction.Deliver;
                case WorldOpportunityKind.StabilizeRegion:
                    return MapWorldMissionAction.Occupy;
                case WorldOpportunityKind.ProtectFacility:
                    return approach == WorldOperationApproach.CovertAction
                        ? MapWorldMissionAction.Sabotage
                        : MapWorldMissionAction.Scout;
                default:
                    return MapWorldMissionAction.Scout;
            }
        }

        private static void AddSupplyRouteCandidates(
            List<GridCoordinate> candidates,
            GridMapLayout layout,
            RealtimeMapGameplayService gameplay,
            MapWorldMissionAction action)
        {
            if (action != MapWorldMissionAction.Escort &&
                action != MapWorldMissionAction.Raid)
                return;

            for (int y = 0; y < layout.Height; y++)
            {
                for (int x = 0; x < layout.Width; x++)
                {
                    var coordinate = new GridCoordinate(x, y);
                    if (!gameplay.TryGetPendingSupplyRouteOwnerAt(
                            coordinate,
                            out string owner))
                        continue;
                    bool friendly = string.Equals(
                        owner,
                        gameplay.PlayerFactionId,
                        StringComparison.Ordinal);
                    if ((action == MapWorldMissionAction.Escort && friendly) ||
                        (action == MapWorldMissionAction.Raid && !friendly))
                    {
                        candidates.Add(coordinate);
                    }
                }
            }
        }

        private static void AddFriendlyCastles(
            List<GridCoordinate> candidates,
            RealtimeMapGameplayService gameplay)
        {
            for (int i = 0; i < gameplay.Castles.Count; i++)
            {
                MapCastleControlState castle = gameplay.Castles[i];
                if (!castle.IsDestroyed && string.Equals(
                        castle.OwnerFactionId,
                        gameplay.PlayerFactionId,
                        StringComparison.Ordinal))
                    candidates.Add(castle.Coordinate);
            }
        }

        private static MapSupplyKind ResolveCargoKind(
            ResourceId? resourceId)
        {
            string id = resourceId?.Value ?? "food";
            if (string.Equals(id, "steel", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(id, "iron", StringComparison.OrdinalIgnoreCase))
                return MapSupplyKind.Equipment;
            if (string.Equals(
                    id,
                    "medicine",
                    StringComparison.OrdinalIgnoreCase))
                return MapSupplyKind.Medicine;
            if (string.Equals(
                    id,
                    "horse",
                    StringComparison.OrdinalIgnoreCase))
                return MapSupplyKind.Horse;
            return MapSupplyKind.Food;
        }

        private static void AddTargetCastles(
            List<GridCoordinate> candidates,
            RealtimeMapGameplayService gameplay)
        {
            for (int i = 0; i < gameplay.Castles.Count; i++)
            {
                MapCastleControlState castle = gameplay.Castles[i];
                if (!castle.IsDestroyed && !string.Equals(
                        castle.OwnerFactionId,
                        gameplay.PlayerFactionId,
                        StringComparison.Ordinal))
                    candidates.Add(castle.Coordinate);
            }
        }

        private static void AddMines(
            List<GridCoordinate> candidates,
            RealtimeMapGameplayService gameplay,
            WorldOpportunityKind kind)
        {
            for (int i = 0; i < gameplay.Mines.Count; i++)
            {
                MapMineControlState mine = gameplay.Mines[i];
                bool owned = string.Equals(
                    mine.OwnerFactionId,
                    gameplay.PlayerFactionId,
                    StringComparison.Ordinal);
                if (kind == WorldOpportunityKind.RepairMine
                    ? owned
                    : !owned)
                    candidates.Add(mine.Coordinate);
            }
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < (value?.Length ?? 0); i++)
                    hash = hash * 31 + value[i];
                return hash;
            }
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int result = value % divisor;
            return result < 0 ? result + divisor : result;
        }
    }
}
