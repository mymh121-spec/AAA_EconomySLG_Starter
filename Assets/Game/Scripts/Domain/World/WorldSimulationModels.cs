using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Military;
using Game.Domain.Resources;

namespace Game.Domain.World
{
    public enum WorldEventTrigger
    {
        Random,
        Causal
    }

    public enum WorldEventKind
    {
        HarvestFailure,
        BountifulHarvest,
        MineCollapse,
        NewVeinDiscovered,
        BanditIncrease,
        ImportantNpcDeath,
        MilitarySupplyShortage,
        FactoryDisruption
    }

    public enum WorldEventStatus
    {
        Active,
        ResolvedByPlayer,
        ResolvedByNpc,
        Expired
    }

    public enum WorldOpportunityKind
    {
        SuppressBandits,
        EscortSupply,
        RepairMine,
        SurveyVein,
        StabilizeRegion,
        ProtectFacility,
        EmergencyDelivery
    }

    public enum WorldOpportunityStatus
    {
        Offered,
        Accepted,
        Resolved,
        Failed,
        Closed
    }

    public sealed class AutonomousWorldTuning
    {
        public decimal RandomEventChancePerTurn { get; }
        public decimal CausalShortageThreshold { get; }
        public int NpcAutoResolveDelayTurns { get; }
        public decimal NpcBaseSuccessChance { get; }
        public decimal PlayerInterventionEfficiency { get; }
        public decimal PlayerBaseReward { get; }
        public decimal PlayerReputationReward { get; }
        public decimal EventProductionPenalty { get; }
        public decimal BountifulProductionBonus { get; }
        public decimal NewVeinReserveBonus { get; }
        public int InitialArmySoldiersPerFaction { get; }
        public MilitaryLogisticsTuning MilitaryLogistics { get; }
        public int MaxCausalEventsPerTurn { get; }
        public int RepeatEventCooldownTurns { get; }

        public AutonomousWorldTuning(
            decimal randomEventChancePerTurn = 0.28m,
            decimal causalShortageThreshold = 0.20m,
            int npcAutoResolveDelayTurns = 3,
            decimal npcBaseSuccessChance = 0.35m,
            decimal playerInterventionEfficiency = 0.20m,
            decimal playerBaseReward = 2500m,
            decimal playerReputationReward = 3m,
            decimal eventProductionPenalty = 0.55m,
            decimal bountifulProductionBonus = 1.30m,
            decimal newVeinReserveBonus = 3000m,
            int initialArmySoldiersPerFaction = 180,
            MilitaryLogisticsTuning militaryLogistics = null,
            int maxCausalEventsPerTurn = 3,
            int repeatEventCooldownTurns = 3)
        {
            RandomEventChancePerTurn = Math.Clamp(
                randomEventChancePerTurn,
                0m,
                1m);
            CausalShortageThreshold = Math.Clamp(
                causalShortageThreshold,
                0.01m,
                1m);
            NpcAutoResolveDelayTurns = Math.Max(
                1,
                npcAutoResolveDelayTurns);
            NpcBaseSuccessChance = Math.Clamp(
                npcBaseSuccessChance,
                0m,
                1m);
            PlayerInterventionEfficiency = Math.Clamp(
                playerInterventionEfficiency,
                0m,
                1m);
            PlayerBaseReward = Math.Max(0m, playerBaseReward);
            PlayerReputationReward = Math.Max(
                0m,
                playerReputationReward);
            EventProductionPenalty = Math.Clamp(
                eventProductionPenalty,
                0.05m,
                1m);
            BountifulProductionBonus = Math.Clamp(
                bountifulProductionBonus,
                1m,
                3m);
            NewVeinReserveBonus = Math.Max(0m, newVeinReserveBonus);
            InitialArmySoldiersPerFaction = Math.Max(
                10,
                initialArmySoldiersPerFaction);
            MilitaryLogistics = militaryLogistics ??
                new MilitaryLogisticsTuning();
            MaxCausalEventsPerTurn = Math.Max(
                1,
                maxCausalEventsPerTurn);
            RepeatEventCooldownTurns = Math.Max(
                1,
                repeatEventCooldownTurns);
        }
    }

    public readonly struct WorldFlowContribution
    {
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public decimal Supply { get; }
        public decimal Demand { get; }
        public decimal MarketStockChange { get; }
        public string Reason { get; }

        public WorldFlowContribution(
            RegionId regionId,
            ResourceId resourceId,
            decimal supply,
            decimal demand,
            decimal marketStockChange,
            string reason)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            Supply = Math.Max(0m, supply);
            Demand = Math.Max(0m, demand);
            MarketStockChange = marketStockChange;
            Reason = reason ?? string.Empty;
        }
    }

    public sealed class WorldEventInstance
    {
        public string Id { get; }
        public WorldEventKind Kind { get; }
        public WorldEventTrigger Trigger { get; }
        public RegionId RegionId { get; }
        public ResourceId? ResourceId { get; }
        public string TargetId { get; }
        public TurnNumber CreatedTurn { get; }
        public decimal Severity { get; private set; }
        public WorldEventStatus Status { get; private set; }
        public string ResolutionActorId { get; private set; }
        public bool IsActive => Status == WorldEventStatus.Active;

        public WorldEventInstance(
            string id,
            WorldEventKind kind,
            WorldEventTrigger trigger,
            RegionId regionId,
            ResourceId? resourceId,
            string targetId,
            TurnNumber createdTurn,
            decimal severity)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            Trigger = trigger;
            RegionId = regionId;
            ResourceId = resourceId;
            TargetId = targetId ?? string.Empty;
            CreatedTurn = createdTurn;
            Severity = Math.Clamp(severity, 0.05m, 1m);
            Status = WorldEventStatus.Active;
            ResolutionActorId = string.Empty;
        }

        public void Escalate(decimal amount)
        {
            if (IsActive)
                Severity = Math.Clamp(Severity + amount, 0.05m, 1m);
        }

        public void Resolve(bool byPlayer, string actorId)
        {
            if (!IsActive)
                return;

            Status = byPlayer
                ? WorldEventStatus.ResolvedByPlayer
                : WorldEventStatus.ResolvedByNpc;
            ResolutionActorId = actorId ?? string.Empty;
        }

        public void Expire()
        {
            if (IsActive)
                Status = WorldEventStatus.Expired;
        }
    }

    public sealed class WorldOpportunity
    {
        public string Id { get; }
        public string EventId { get; }
        public WorldOpportunityKind Kind { get; }
        public string DisplayName { get; }
        public RegionId RegionId { get; }
        public TurnNumber OfferedTurn { get; }
        public TurnNumber NpcResolveTurn { get; }
        public decimal Difficulty { get; }
        public decimal MoneyReward { get; }
        public decimal ReputationReward { get; }
        public WorldOpportunityStatus Status { get; private set; }
        public string ResolverId { get; private set; }

        public WorldOpportunity(
            string id,
            string eventId,
            WorldOpportunityKind kind,
            string displayName,
            RegionId regionId,
            TurnNumber offeredTurn,
            TurnNumber npcResolveTurn,
            decimal difficulty,
            decimal moneyReward,
            decimal reputationReward)
        {
            Id = id ?? string.Empty;
            EventId = eventId ?? string.Empty;
            Kind = kind;
            DisplayName = displayName ?? kind.ToString();
            RegionId = regionId;
            OfferedTurn = offeredTurn;
            NpcResolveTurn = npcResolveTurn;
            Difficulty = Math.Clamp(difficulty, 0.05m, 1m);
            MoneyReward = Math.Max(0m, moneyReward);
            ReputationReward = Math.Max(0m, reputationReward);
            Status = WorldOpportunityStatus.Offered;
            ResolverId = string.Empty;
        }

        public bool TryAccept()
        {
            if (Status != WorldOpportunityStatus.Offered)
                return false;
            Status = WorldOpportunityStatus.Accepted;
            return true;
        }

        public void Resolve(bool success, string resolverId)
        {
            if (Status != WorldOpportunityStatus.Offered &&
                Status != WorldOpportunityStatus.Accepted)
            {
                return;
            }

            Status = success
                ? WorldOpportunityStatus.Resolved
                : WorldOpportunityStatus.Failed;
            ResolverId = resolverId ?? string.Empty;
        }

        public void Close()
        {
            if (Status == WorldOpportunityStatus.Offered)
                Status = WorldOpportunityStatus.Closed;
        }
    }

    public readonly struct PlayerInterventionResult
    {
        public bool Accepted { get; }
        public bool Success { get; }
        public string Message { get; }
        public decimal MoneyReward { get; }
        public decimal ReputationReward { get; }

        public PlayerInterventionResult(
            bool accepted,
            bool success,
            string message,
            decimal moneyReward,
            decimal reputationReward)
        {
            Accepted = accepted;
            Success = success;
            Message = message ?? string.Empty;
            MoneyReward = Math.Max(0m, moneyReward);
            ReputationReward = Math.Max(0m, reputationReward);
        }
    }

    public sealed class PlayerCharacterState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public CompanyId AffiliatedCompanyId { get; }
        public RegionId CurrentRegionId { get; private set; }
        public decimal CombatSkill { get; private set; }
        public decimal TradeSkill { get; private set; }
        public decimal LeadershipSkill { get; private set; }
        public decimal PersonalFunds { get; private set; }
        public decimal Reputation { get; private set; }

        public PlayerCharacterState(
            string id,
            string displayName,
            CompanyId affiliatedCompanyId,
            RegionId currentRegionId,
            decimal combatSkill = 45m,
            decimal tradeSkill = 45m,
            decimal leadershipSkill = 45m)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "player_agent" : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "플레이어"
                : displayName;
            AffiliatedCompanyId = affiliatedCompanyId;
            CurrentRegionId = currentRegionId;
            CombatSkill = Math.Clamp(combatSkill, 0m, 100m);
            TradeSkill = Math.Clamp(tradeSkill, 0m, 100m);
            LeadershipSkill = Math.Clamp(leadershipSkill, 0m, 100m);
        }

        public decimal GetCapability(WorldOpportunityKind kind)
        {
            switch (kind)
            {
                case WorldOpportunityKind.SuppressBandits:
                case WorldOpportunityKind.ProtectFacility:
                    return CombatSkill * 0.70m +
                        LeadershipSkill * 0.30m + Reputation * 0.05m;
                case WorldOpportunityKind.EscortSupply:
                case WorldOpportunityKind.StabilizeRegion:
                    return LeadershipSkill * 0.65m +
                        CombatSkill * 0.20m +
                        TradeSkill * 0.15m + Reputation * 0.05m;
                default:
                    return TradeSkill * 0.55m +
                        LeadershipSkill * 0.45m + Reputation * 0.05m;
            }
        }

        public void MoveTo(RegionId regionId)
        {
            CurrentRegionId = regionId;
        }

        public void AddMissionReward(decimal money, decimal reputation)
        {
            PersonalFunds += Math.Max(0m, money);
            Reputation += Math.Max(0m, reputation);
        }
    }

    public sealed class AutonomousWorldState
    {
        private readonly List<ResourceExtractionSite> _resourceSites =
            new List<ResourceExtractionSite>(16);
        private readonly List<ArmyState> _armies =
            new List<ArmyState>(8);
        private readonly List<WorldEventInstance> _events =
            new List<WorldEventInstance>(32);
        private readonly List<WorldOpportunity> _opportunities =
            new List<WorldOpportunity>(32);

        public ProceduralWorldState World { get; }
        public PlayerCharacterState PlayerCharacter { get; }
        public IReadOnlyList<ResourceExtractionSite> ResourceSites =>
            _resourceSites;
        public IReadOnlyList<ArmyState> Armies => _armies;
        public IReadOnlyList<WorldEventInstance> Events => _events;
        public IReadOnlyList<WorldOpportunity> Opportunities =>
            _opportunities;
        public decimal PlayerMoneyEarned { get; private set; }
        public decimal PlayerReputation { get; private set; }

        public AutonomousWorldState(
            ProceduralWorldState world,
            PlayerCharacterState playerCharacter = null)
        {
            World = world ?? throw new ArgumentNullException(nameof(world));
            RegionId startRegion = World.Regions.Count > 0
                ? World.Regions[0].Id
                : new RegionId("unknown");
            PlayerCharacter = playerCharacter ?? new PlayerCharacterState(
                "player_agent",
                "플레이어",
                new CompanyId("player"),
                startRegion);
        }

        public void AddResourceSite(ResourceExtractionSite site)
        {
            if (site == null || FindResourceSite(site.Id) != null)
                return;

            _resourceSites.Add(site);
        }

        public void AddArmy(ArmyState army)
        {
            if (army != null)
                _armies.Add(army);
        }

        public void AddEvent(WorldEventInstance worldEvent)
        {
            if (worldEvent != null)
                _events.Add(worldEvent);
        }

        public void AddOpportunity(WorldOpportunity opportunity)
        {
            if (opportunity != null)
                _opportunities.Add(opportunity);
        }

        public WorldEventInstance FindEvent(string id)
        {
            for (int i = 0; i < _events.Count; i++)
            {
                if (string.Equals(
                    _events[i].Id,
                    id,
                    StringComparison.Ordinal))
                {
                    return _events[i];
                }
            }

            return null;
        }

        public WorldOpportunity FindOpportunity(string id)
        {
            for (int i = 0; i < _opportunities.Count; i++)
            {
                if (string.Equals(
                    _opportunities[i].Id,
                    id,
                    StringComparison.Ordinal))
                {
                    return _opportunities[i];
                }
            }

            return null;
        }

        public ResourceExtractionSite FindResourceSite(string id)
        {
            for (int i = 0; i < _resourceSites.Count; i++)
            {
                if (string.Equals(
                    _resourceSites[i].Id,
                    id,
                    StringComparison.Ordinal))
                {
                    return _resourceSites[i];
                }
            }

            return null;
        }

        public void AddPlayerRewards(decimal money, decimal reputation)
        {
            PlayerMoneyEarned += Math.Max(0m, money);
            PlayerReputation += Math.Max(0m, reputation);
            PlayerCharacter.AddMissionReward(money, reputation);
        }
    }

    public readonly struct ArmyReadinessRecord
    {
        public string ArmyId { get; }
        public int Soldiers { get; }
        public decimal SupplyRatio { get; }
        public decimal Readiness { get; }
        public decimal DailyUpkeep { get; }

        public ArmyReadinessRecord(
            string armyId,
            int soldiers,
            decimal supplyRatio,
            decimal readiness,
            decimal dailyUpkeep)
        {
            ArmyId = armyId ?? string.Empty;
            Soldiers = Math.Max(0, soldiers);
            SupplyRatio = Math.Clamp(supplyRatio, 0m, 1m);
            Readiness = Math.Clamp(readiness, 0m, 1m);
            DailyUpkeep = Math.Max(0m, dailyUpkeep);
        }
    }

    public sealed class AutonomousWorldTurnReport
    {
        public static AutonomousWorldTurnReport Empty { get; } =
            new AutonomousWorldTurnReport(
                Array.Empty<WorldFlowContribution>(),
                Array.Empty<WorldEventInstance>(),
                Array.Empty<WorldEventInstance>(),
                Array.Empty<WorldOpportunity>(),
                Array.Empty<ArmyReadinessRecord>());

        public IReadOnlyList<WorldFlowContribution> Flows { get; }
        public IReadOnlyList<WorldEventInstance> GeneratedEvents { get; }
        public IReadOnlyList<WorldEventInstance> ResolvedEvents { get; }
        public IReadOnlyList<WorldOpportunity> OfferedOpportunities { get; }
        public IReadOnlyList<ArmyReadinessRecord> ArmyReadiness { get; }

        public AutonomousWorldTurnReport(
            IReadOnlyList<WorldFlowContribution> flows,
            IReadOnlyList<WorldEventInstance> generatedEvents,
            IReadOnlyList<WorldEventInstance> resolvedEvents,
            IReadOnlyList<WorldOpportunity> offeredOpportunities,
            IReadOnlyList<ArmyReadinessRecord> armyReadiness)
        {
            Flows = flows ?? Array.Empty<WorldFlowContribution>();
            GeneratedEvents = generatedEvents ??
                Array.Empty<WorldEventInstance>();
            ResolvedEvents = resolvedEvents ??
                Array.Empty<WorldEventInstance>();
            OfferedOpportunities = offeredOpportunities ??
                Array.Empty<WorldOpportunity>();
            ArmyReadiness = armyReadiness ??
                Array.Empty<ArmyReadinessRecord>();
        }
    }
}
