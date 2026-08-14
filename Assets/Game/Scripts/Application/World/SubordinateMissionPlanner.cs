using System;
using System.Collections.Generic;
using Game.Domain.World;

namespace Game.Application.World
{
    public readonly struct SubordinateMissionPlan
    {
        public string OpportunityId { get; }
        public string CommanderId { get; }
        public string CommanderDisplayName { get; }
        public string UnitId { get; }
        public WorldOperationApproach Approach { get; }
        public decimal Capability { get; }
        public decimal UnitReadiness { get; }
        public bool IsRecommendedApproach { get; }

        public SubordinateMissionPlan(
            string opportunityId,
            string commanderId,
            string commanderDisplayName,
            string unitId,
            WorldOperationApproach approach,
            decimal capability,
            decimal unitReadiness,
            bool isRecommendedApproach)
        {
            OpportunityId = opportunityId ?? string.Empty;
            CommanderId = commanderId ?? string.Empty;
            CommanderDisplayName = commanderDisplayName ?? string.Empty;
            UnitId = unitId ?? string.Empty;
            Approach = approach;
            Capability = Math.Clamp(capability, 1m, 150m);
            UnitReadiness = Math.Clamp(unitReadiness, 0m, 100m);
            IsRecommendedApproach = isRecommendedApproach;
        }
    }

    public static class SubordinateMissionPlanner
    {
        public static bool TryCreatePlan(
            WorldOpportunity opportunity,
            MapCommanderState commander,
            MapUnitState unit,
            WorldOperationApproach approach,
            out SubordinateMissionPlan plan,
            out string reason)
        {
            plan = default;
            if (opportunity == null)
            {
                reason = "위임할 작전을 찾을 수 없습니다.";
                return false;
            }
            if (commander == null || commander.IsAvailable)
            {
                reason = "고용되어 부대에 배속된 지휘관이 필요합니다.";
                return false;
            }
            if (unit == null || unit.Commander != commander ||
                !string.Equals(
                    commander.AssignedUnitId,
                    unit.Id,
                    StringComparison.Ordinal))
            {
                reason = "지휘관의 배속 부대를 찾을 수 없습니다.";
                return false;
            }
            if (unit.Soldiers <= 0)
            {
                reason = "병력이 없는 부대에는 작전을 위임할 수 없습니다.";
                return false;
            }
            if (!WorldOperationCatalog.TryGet(
                opportunity.Kind,
                approach,
                out WorldOperationApproachProfile profile))
            {
                reason = "선택한 해결 방식은 이 작전에 사용할 수 없습니다.";
                return false;
            }

            decimal readiness = CalculateUnitReadiness(unit);
            decimal baseCapability = CalculateBaseCapability(
                opportunity.Kind,
                commander,
                readiness);
            decimal personalityModifier = GetPersonalityModifier(
                commander.Personality,
                approach);
            decimal loyaltyModifier = 0.70m + commander.Loyalty * 0.003m;
            decimal capability = Math.Clamp(
                (baseCapability + personalityModifier) * loyaltyModifier,
                1m,
                150m);
            WorldOperationApproach recommended = GetRecommendedApproach(
                opportunity,
                commander,
                unit);

            plan = new SubordinateMissionPlan(
                opportunity.Id,
                commander.Id,
                commander.DisplayName,
                unit.Id,
                profile.Approach,
                capability,
                readiness,
                profile.Approach == recommended);
            reason = string.Empty;
            return true;
        }

        public static WorldOperationApproach GetRecommendedApproach(
            WorldOpportunity opportunity,
            MapCommanderState commander,
            MapUnitState unit)
        {
            if (opportunity == null)
                return WorldOperationApproach.Negotiation;

            IReadOnlyList<WorldOperationApproachProfile> approaches =
                WorldOperationCatalog.GetApproaches(opportunity.Kind);
            if (approaches.Count == 0)
                return WorldOperationApproach.Negotiation;
            if (commander == null || unit == null)
                return approaches[0].Approach;

            decimal readiness = CalculateUnitReadiness(unit);
            decimal baseCapability = CalculateBaseCapability(
                opportunity.Kind,
                commander,
                readiness);
            WorldOperationApproach best = approaches[0].Approach;
            decimal bestScore = decimal.MinValue;
            for (int i = 0; i < approaches.Count; i++)
            {
                WorldOperationApproachProfile profile = approaches[i];
                decimal score =
                    (baseCapability + GetPersonalityModifier(
                        commander.Personality,
                        profile.Approach)) * profile.CapabilityMultiplier +
                    profile.SuccessChanceModifier * 100m -
                    profile.FailureEscalation * 20m;
                if (score > bestScore)
                {
                    best = profile.Approach;
                    bestScore = score;
                }
            }

            return best;
        }

        public static decimal CalculateUnitReadiness(MapUnitState unit)
        {
            if (unit == null || unit.Soldiers <= 0)
                return 0m;

            decimal soldierStrength = Math.Clamp(
                unit.Soldiers / 100m,
                0m,
                1.25m);
            decimal morale = Math.Clamp(unit.Morale / 100m, 0m, 1.25m);
            decimal fatigue = Math.Clamp(1m - unit.Fatigue / 100m, 0m, 1m);
            decimal supply = unit.UsesSupplySystem
                ? unit.SupplyRatio
                : 1m;
            return Math.Clamp(
                (soldierStrength * 0.30m +
                 morale * 0.30m +
                 fatigue * 0.20m +
                 supply * 0.20m) * 100m,
                0m,
                100m);
        }

        private static decimal CalculateBaseCapability(
            WorldOpportunityKind kind,
            MapCommanderState commander,
            decimal readiness)
        {
            switch (kind)
            {
                case WorldOpportunityKind.SuppressBandits:
                case WorldOpportunityKind.ProtectFacility:
                    return commander.Command * 0.30m +
                        commander.Tactics * 0.40m +
                        commander.Logistics * 0.05m +
                        readiness * 0.25m;
                case WorldOpportunityKind.EscortSupply:
                case WorldOpportunityKind.StabilizeRegion:
                    return commander.Command * 0.30m +
                        commander.Tactics * 0.10m +
                        commander.Logistics * 0.35m +
                        readiness * 0.25m;
                default:
                    return commander.Command * 0.15m +
                        commander.Tactics * 0.15m +
                        commander.Logistics * 0.45m +
                        readiness * 0.25m;
            }
        }

        private static decimal GetPersonalityModifier(
            MapCommanderPersonality personality,
            WorldOperationApproach approach)
        {
            switch (personality)
            {
                case MapCommanderPersonality.Aggressive:
                    return approach == WorldOperationApproach.ArmedSecurity
                        ? 12m
                        : approach == WorldOperationApproach.CovertAction
                            ? 3m
                            : 0m;
                case MapCommanderPersonality.Cautious:
                    return approach == WorldOperationApproach.Negotiation ||
                           approach == WorldOperationApproach.PublicRelief
                        ? 10m
                        : approach == WorldOperationApproach.ArmedSecurity
                            ? -6m
                            : 0m;
                case MapCommanderPersonality.Opportunistic:
                    return approach == WorldOperationApproach.CovertAction
                        ? 12m
                        : approach == WorldOperationApproach.Negotiation
                            ? 4m
                            : 0m;
                case MapCommanderPersonality.Logistician:
                    return approach == WorldOperationApproach.Logistics ||
                           approach == WorldOperationApproach.TechnicalInvestment
                        ? 12m
                        : approach == WorldOperationApproach.ArmedSecurity
                            ? -4m
                            : 0m;
                default:
                    return 0m;
            }
        }
    }
}
