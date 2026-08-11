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

    public enum WorldOperationApproach
    {
        Negotiation,
        Logistics,
        TechnicalInvestment,
        CovertAction,
        ArmedSecurity,
        PublicRelief
    }

    public enum WorldOperationOutcome
    {
        None,
        GreatSuccess,
        Success,
        Compromise,
        Failure,
        Disaster
    }

    public readonly struct WorldOperationApproachProfile
    {
        public WorldOperationApproach Approach { get; }
        public string DisplayName { get; }
        public string Description { get; }
        public decimal CapabilityMultiplier { get; }
        public decimal SuccessChanceModifier { get; }
        public decimal MoneyRewardMultiplier { get; }
        public decimal ReputationRewardMultiplier { get; }
        public decimal UpfrontCostMultiplier { get; }
        public decimal ConsequenceStrength { get; }
        public decimal FailureEscalation { get; }

        public WorldOperationApproachProfile(
            WorldOperationApproach approach,
            string displayName,
            string description,
            decimal capabilityMultiplier,
            decimal successChanceModifier,
            decimal moneyRewardMultiplier,
            decimal reputationRewardMultiplier,
            decimal upfrontCostMultiplier,
            decimal consequenceStrength,
            decimal failureEscalation)
        {
            Approach = approach;
            DisplayName = displayName ?? approach.ToString();
            Description = description ?? string.Empty;
            CapabilityMultiplier = Math.Max(0.10m, capabilityMultiplier);
            SuccessChanceModifier = Math.Clamp(
                successChanceModifier,
                -0.50m,
                0.50m);
            MoneyRewardMultiplier = Math.Max(0m, moneyRewardMultiplier);
            ReputationRewardMultiplier = Math.Max(
                0m,
                reputationRewardMultiplier);
            UpfrontCostMultiplier = Math.Max(0m, upfrontCostMultiplier);
            ConsequenceStrength = Math.Clamp(
                consequenceStrength,
                0.10m,
                2m);
            FailureEscalation = Math.Clamp(
                failureEscalation,
                0.01m,
                0.50m);
        }
    }

    public static class WorldOperationCatalog
    {
        private static readonly WorldOperationApproachProfile[]
            SuppressBandits =
            {
                Profile(
                    WorldOperationApproach.ArmedSecurity,
                    "무력 진압",
                    "병력을 투입해 빠르게 위협을 제거합니다.",
                    1.20m, 0.14m, 1.10m, 0.75m, 0.12m, 1.20m, 0.18m),
                Profile(
                    WorldOperationApproach.Negotiation,
                    "지역 협상",
                    "지역 세력과 거래해 도적의 지원망을 끊습니다.",
                    0.95m, 0.05m, 0.75m, 1.45m, 0.08m, 0.90m, 0.08m),
                Profile(
                    WorldOperationApproach.CovertAction,
                    "비밀공작",
                    "정보원을 이용해 조직을 내부에서 붕괴시킵니다.",
                    1.05m, 0.08m, 1.40m, 0.55m, 0.06m, 1.05m, 0.22m)
            };

        private static readonly WorldOperationApproachProfile[] EscortSupply =
        {
            Profile(
                WorldOperationApproach.Logistics,
                "수송망 재설계",
                "우회로와 분산 수송으로 물자를 목적지까지 보냅니다.",
                1.20m, 0.15m, 1.05m, 1.00m, 0.14m, 1.15m, 0.10m),
            Profile(
                WorldOperationApproach.ArmedSecurity,
                "무장 호위",
                "호위 병력을 붙여 위험 구간을 정면 돌파합니다.",
                1.10m, 0.10m, 1.15m, 0.80m, 0.12m, 1.05m, 0.16m),
            Profile(
                WorldOperationApproach.CovertAction,
                "미끼 수송",
                "가짜 수송대를 노출하고 본대를 은밀히 이동시킵니다.",
                1.00m, 0.06m, 1.45m, 0.50m, 0.06m, 1.00m, 0.24m)
            };

        private static readonly WorldOperationApproachProfile[] RepairMine =
        {
            Profile(
                WorldOperationApproach.TechnicalInvestment,
                "기술 복구",
                "기술자와 기계를 투입해 설비와 갱도를 개선합니다.",
                1.25m, 0.18m, 1.10m, 1.05m, 0.20m, 1.30m, 0.08m),
            Profile(
                WorldOperationApproach.Logistics,
                "긴급 장비 조달",
                "복구 자재와 인력을 우선 수송해 조업을 재개합니다.",
                1.10m, 0.10m, 1.00m, 1.00m, 0.14m, 1.05m, 0.10m),
            Profile(
                WorldOperationApproach.Negotiation,
                "공동 복구 계약",
                "지역 업체와 비용 및 향후 채굴권을 나눕니다.",
                0.95m, 0.06m, 0.70m, 1.40m, 0.06m, 0.85m, 0.08m)
            };

        private static readonly WorldOperationApproachProfile[] SurveyVein =
        {
            Profile(
                WorldOperationApproach.TechnicalInvestment,
                "정밀 탐사",
                "장비와 기술자를 투입해 매장량을 정확히 조사합니다.",
                1.25m, 0.17m, 1.10m, 1.00m, 0.18m, 1.30m, 0.06m),
            Profile(
                WorldOperationApproach.CovertAction,
                "경쟁사 정보 탈취",
                "경쟁사의 조사 자료를 입수해 비용과 시간을 줄입니다.",
                1.05m, 0.07m, 1.50m, 0.45m, 0.05m, 1.00m, 0.22m),
            Profile(
                WorldOperationApproach.Negotiation,
                "현지 조사권 계약",
                "주민과 토지 소유자의 협조를 확보합니다.",
                1.00m, 0.08m, 0.80m, 1.35m, 0.08m, 0.90m, 0.08m)
            };

        private static readonly WorldOperationApproachProfile[] StabilizeRegion =
        {
            Profile(
                WorldOperationApproach.Negotiation,
                "이해관계 조정",
                "노동자·상인·지역 권력자 사이의 합의를 만듭니다.",
                1.20m, 0.14m, 0.75m, 1.50m, 0.10m, 1.20m, 0.07m),
            Profile(
                WorldOperationApproach.PublicRelief,
                "민생 지원",
                "식량과 의약품을 공급해 불안을 낮춥니다.",
                1.15m, 0.12m, 0.55m, 1.80m, 0.18m, 1.30m, 0.06m),
            Profile(
                WorldOperationApproach.ArmedSecurity,
                "치안 통제",
                "병력을 배치해 단기간에 질서를 회복합니다.",
                1.05m, 0.08m, 1.10m, 0.65m, 0.12m, 0.90m, 0.18m)
            };

        private static readonly WorldOperationApproachProfile[] ProtectFacility =
        {
            Profile(
                WorldOperationApproach.ArmedSecurity,
                "시설 경비 강화",
                "경비대와 방어 설비로 생산시설을 보호합니다.",
                1.20m, 0.15m, 1.10m, 0.80m, 0.14m, 1.20m, 0.14m),
            Profile(
                WorldOperationApproach.TechnicalInvestment,
                "설비 분산·보강",
                "핵심 공정을 분산하고 고장 지점을 보강합니다.",
                1.15m, 0.13m, 1.00m, 1.10m, 0.18m, 1.25m, 0.08m),
            Profile(
                WorldOperationApproach.CovertAction,
                "배후 추적",
                "파괴 공작의 배후를 추적해 재발을 막습니다.",
                1.00m, 0.06m, 1.45m, 0.50m, 0.06m, 1.05m, 0.22m)
            };

        private static readonly WorldOperationApproachProfile[] EmergencyDelivery =
        {
            Profile(
                WorldOperationApproach.Logistics,
                "긴급 납품",
                "운송 용량을 집중해 부족 물자를 우선 공급합니다.",
                1.20m, 0.16m, 1.05m, 1.10m, 0.16m, 1.20m, 0.08m),
            Profile(
                WorldOperationApproach.PublicRelief,
                "구호 배급",
                "이윤을 줄이고 주민에게 물자를 직접 배급합니다.",
                1.15m, 0.13m, 0.50m, 1.90m, 0.20m, 1.35m, 0.05m),
            Profile(
                WorldOperationApproach.Negotiation,
                "민간 조달 계약",
                "상인 조합과 가격 및 물량을 협상합니다.",
                1.00m, 0.08m, 0.80m, 1.35m, 0.10m, 0.95m, 0.08m)
            };

        public static IReadOnlyList<WorldOperationApproachProfile> GetApproaches(
            WorldOpportunityKind kind)
        {
            switch (kind)
            {
                case WorldOpportunityKind.SuppressBandits:
                    return SuppressBandits;
                case WorldOpportunityKind.EscortSupply:
                    return EscortSupply;
                case WorldOpportunityKind.RepairMine:
                    return RepairMine;
                case WorldOpportunityKind.SurveyVein:
                    return SurveyVein;
                case WorldOpportunityKind.ProtectFacility:
                    return ProtectFacility;
                case WorldOpportunityKind.EmergencyDelivery:
                    return EmergencyDelivery;
                default:
                    return StabilizeRegion;
            }
        }

        public static WorldOperationApproachProfile GetDefault(
            WorldOpportunityKind kind)
        {
            return GetApproaches(kind)[0];
        }

        public static bool TryGet(
            WorldOpportunityKind kind,
            WorldOperationApproach approach,
            out WorldOperationApproachProfile profile)
        {
            IReadOnlyList<WorldOperationApproachProfile> approaches =
                GetApproaches(kind);
            for (int i = 0; i < approaches.Count; i++)
            {
                if (approaches[i].Approach == approach)
                {
                    profile = approaches[i];
                    return true;
                }
            }

            profile = default;
            return false;
        }

        public static decimal CalculateUpfrontCost(
            WorldOpportunity opportunity,
            WorldOperationApproachProfile profile)
        {
            if (opportunity == null)
                return 0m;

            return Math.Round(
                opportunity.MoneyReward * profile.UpfrontCostMultiplier,
                0,
                MidpointRounding.AwayFromZero);
        }

        private static WorldOperationApproachProfile Profile(
            WorldOperationApproach approach,
            string displayName,
            string description,
            decimal capabilityMultiplier,
            decimal successChanceModifier,
            decimal moneyRewardMultiplier,
            decimal reputationRewardMultiplier,
            decimal upfrontCostMultiplier,
            decimal consequenceStrength,
            decimal failureEscalation)
        {
            return new WorldOperationApproachProfile(
                approach,
                displayName,
                description,
                capabilityMultiplier,
                successChanceModifier,
                moneyRewardMultiplier,
                reputationRewardMultiplier,
                upfrontCostMultiplier,
                consequenceStrength,
                failureEscalation);
        }
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

        public void Mitigate(decimal amount)
        {
            if (IsActive)
                Severity = Math.Clamp(Severity - amount, 0.05m, 1m);
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
        public WorldOperationApproach? SelectedApproach { get; private set; }
        public WorldOperationOutcome Outcome { get; private set; }

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
            SelectedApproach = null;
            Outcome = WorldOperationOutcome.None;
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
            Resolve(
                success
                    ? WorldOperationOutcome.Success
                    : WorldOperationOutcome.Failure,
                resolverId,
                WorldOperationCatalog.GetDefault(Kind).Approach);
        }

        public void Resolve(
            WorldOperationOutcome outcome,
            string resolverId,
            WorldOperationApproach approach)
        {
            if (Status != WorldOpportunityStatus.Offered &&
                Status != WorldOpportunityStatus.Accepted)
            {
                return;
            }

            Outcome = outcome;
            SelectedApproach = approach;
            Status = outcome == WorldOperationOutcome.GreatSuccess ||
                     outcome == WorldOperationOutcome.Success ||
                     outcome == WorldOperationOutcome.Compromise
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
        public WorldOperationOutcome Outcome { get; }
        public WorldOperationApproach Approach { get; }
        public string Message { get; }
        public decimal MoneyReward { get; }
        public decimal ReputationReward { get; }
        public decimal UpfrontCost { get; }

        public PlayerInterventionResult(
            bool accepted,
            bool success,
            string message,
            decimal moneyReward,
            decimal reputationReward,
            WorldOperationOutcome outcome = WorldOperationOutcome.None,
            WorldOperationApproach approach = WorldOperationApproach.Negotiation,
            decimal upfrontCost = 0m)
        {
            Accepted = accepted;
            Success = success;
            Outcome = outcome;
            Approach = approach;
            Message = message ?? string.Empty;
            MoneyReward = Math.Max(0m, moneyReward);
            ReputationReward = Math.Max(0m, reputationReward);
            UpfrontCost = Math.Max(0m, upfrontCost);
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
