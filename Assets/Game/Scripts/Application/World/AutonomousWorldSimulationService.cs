using System;
using System.Collections.Generic;
using Game.Application.Turn;
using Game.Domain.Common;
using Game.Domain.Market;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Application.World
{
    public interface IAutonomousWorldTurnService
    {
        AutonomousWorldState State { get; }

        void SynchronizeResourceSites();

        AutonomousWorldTurnReport PrepareTurn(
            TurnNumber turn,
            GameDay calendarDay);

        void CompleteTurn(
            TurnNumber turn,
            GameDay calendarDay,
            MarketTickReport marketReport);

        bool CanPlayerIntervene(string opportunityId, out string reason);

        bool CanPlayerIntervene(
            string opportunityId,
            WorldOperationApproach approach,
            out string reason);

        PlayerInterventionResult TryPlayerIntervention(
            string opportunityId,
            decimal playerCapability,
            TurnNumber turn);

        PlayerInterventionResult TryPlayerIntervention(
            string opportunityId,
            decimal playerCapability,
            WorldOperationApproach approach,
            TurnNumber turn);
    }

    public sealed class AutonomousWorldSimulationService :
        IAutonomousWorldTurnService
    {
        private readonly WorldEconomyState _economy;
        private readonly AutonomousWorldTuning _tuning;
        private readonly MilitaryBalanceCatalog _militaryBalance;
        private readonly List<WorldFlowContribution> _flowBuffer =
            new List<WorldFlowContribution>(64);
        private readonly List<WorldEventInstance> _generatedEventBuffer =
            new List<WorldEventInstance>(8);
        private readonly List<WorldEventInstance> _resolvedEventBuffer =
            new List<WorldEventInstance>(8);
        private readonly List<WorldOpportunity> _offeredOpportunityBuffer =
            new List<WorldOpportunity>(8);
        private readonly List<ArmyReadinessRecord> _armyReadinessBuffer =
            new List<ArmyReadinessRecord>(8);
        private readonly HashSet<string> _causalEventKeys =
            new HashSet<string>(StringComparer.Ordinal);

        public AutonomousWorldState State { get; }
        public PlayerInterventionResult LastPlayerIntervention { get; private set; }

        public AutonomousWorldSimulationService(
            AutonomousWorldState state,
            WorldEconomyState economy,
            AutonomousWorldTuning tuning,
            MilitaryBalanceCatalog militaryBalance = null)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
            _economy = economy ??
                throw new ArgumentNullException(nameof(economy));
            _tuning = tuning ??
                throw new ArgumentNullException(nameof(tuning));
            _militaryBalance = militaryBalance ??
                MilitaryBalanceCatalog.CreatePrototypeDefaults();

            InitializeResourceSites();
            InitializeArmies();
        }

        public AutonomousWorldTurnReport PrepareTurn(
            TurnNumber turn,
            GameDay calendarDay)
        {
            _flowBuffer.Clear();
            _generatedEventBuffer.Clear();
            _resolvedEventBuffer.Clear();
            _offeredOpportunityBuffer.Clear();
            _armyReadinessBuffer.Clear();

            SynchronizeResourceSites();
            RestoreTransientProductionModifiers();
            ResolveDueOpportunities(turn);
            ResolveExpiredPositiveEvents(turn);
            GenerateRandomEvent(turn);
            GenerateCausalEvents(turn);
            EnsureOpportunitiesForActiveEvents(turn);
            ApplyActiveEventModifiers();
            BuildFacilityFlows();
            BuildMilitaryFlows();

            return new AutonomousWorldTurnReport(
                new List<WorldFlowContribution>(_flowBuffer),
                new List<WorldEventInstance>(_generatedEventBuffer),
                new List<WorldEventInstance>(_resolvedEventBuffer),
                new List<WorldOpportunity>(_offeredOpportunityBuffer),
                new List<ArmyReadinessRecord>(_armyReadinessBuffer));
        }

        public void CompleteTurn(
            TurnNumber turn,
            GameDay calendarDay,
            MarketTickReport marketReport)
        {
            if (marketReport == null)
                throw new ArgumentNullException(nameof(marketReport));

            // 다음 턴의 인과 이벤트는 시장 상태의 실제 미충족 수요를
            // 읽어 생성한다. 가격을 직접 조작하지 않는다.
            for (int i = 0; i < State.World.Regions.Count; i++)
            {
                GeneratedRegionState region = State.World.Regions[i];
                if (region.BanditThreat > 0.75m)
                    region.AdjustStability(-0.02m);
                else
                    region.AdjustStability(0.005m);
            }
        }

        public bool CanPlayerIntervene(
            string opportunityId,
            out string reason)
        {
            WorldOpportunity opportunity =
                State.FindOpportunity(opportunityId);
            if (opportunity == null)
            {
                reason = "해당 미션을 찾을 수 없습니다.";
                return false;
            }
            if (opportunity.Status != WorldOpportunityStatus.Offered)
            {
                reason = "이미 처리되었거나 진행 중인 미션입니다.";
                return false;
            }

            return CanPlayerIntervene(
                opportunityId,
                WorldOperationCatalog.GetDefault(opportunity.Kind).Approach,
                out reason);
        }

        public bool CanPlayerIntervene(
            string opportunityId,
            WorldOperationApproach approach,
            out string reason)
        {
            WorldOpportunity opportunity =
                State.FindOpportunity(opportunityId);
            if (opportunity == null)
            {
                reason = "해당 작전을 찾을 수 없습니다.";
                return false;
            }
            if (opportunity.Status != WorldOpportunityStatus.Offered)
            {
                reason = "이미 처리되었거나 진행 중인 작전입니다.";
                return false;
            }
            if (!WorldOperationCatalog.TryGet(
                opportunity.Kind,
                approach,
                out WorldOperationApproachProfile profile))
            {
                reason = "이 작전에는 선택한 해결 방식을 사용할 수 없습니다.";
                return false;
            }

            decimal cost = CalculateUpfrontCost(opportunity, profile);
            CompanyEconomyRuntime playerCompany = FindPlayerCompany();
            if (playerCompany == null)
            {
                reason = "플레이어 회사를 찾을 수 없습니다.";
                return false;
            }
            if (!playerCompany.Company.CanAfford(cost))
            {
                reason = $"작전 준비금 {cost:N0}원이 필요합니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public PlayerInterventionResult TryPlayerIntervention(
            string opportunityId,
            decimal playerCapability,
            TurnNumber turn)
        {
            WorldOpportunity opportunity =
                State.FindOpportunity(opportunityId);
            WorldOperationApproach approach = opportunity == null
                ? WorldOperationApproach.Negotiation
                : WorldOperationCatalog.GetDefault(opportunity.Kind).Approach;
            return TryPlayerIntervention(
                opportunityId,
                playerCapability,
                approach,
                turn);
        }

        public PlayerInterventionResult TryPlayerIntervention(
            string opportunityId,
            decimal playerCapability,
            WorldOperationApproach approach,
            TurnNumber turn)
        {
            if (!CanPlayerIntervene(
                opportunityId,
                approach,
                out string reason))
            {
                LastPlayerIntervention = new PlayerInterventionResult(
                    false,
                    false,
                    reason,
                    0m,
                    0m,
                    WorldOperationOutcome.None,
                    approach);
                return LastPlayerIntervention;
            }

            WorldOpportunity opportunity =
                State.FindOpportunity(opportunityId);
            WorldOperationCatalog.TryGet(
                opportunity.Kind,
                approach,
                out WorldOperationApproachProfile profile);
            decimal upfrontCost = CalculateUpfrontCost(opportunity, profile);
            CompanyEconomyRuntime playerCompany = FindPlayerCompany();
            if (!playerCompany.Company.TrySpend(upfrontCost))
            {
                LastPlayerIntervention = new PlayerInterventionResult(
                    false,
                    false,
                    "다른 지출로 인해 작전 준비금이 부족해졌습니다.",
                    0m,
                    0m,
                    WorldOperationOutcome.None,
                    approach);
                return LastPlayerIntervention;
            }

            opportunity.TryAccept();
            decimal effectiveCapability = playerCapability > 0m
                ? playerCapability
                : State.PlayerCharacter.GetCapability(opportunity.Kind);
            effectiveCapability *= profile.CapabilityMultiplier;
            decimal capabilityRatio = effectiveCapability /
                Math.Max(1m, effectiveCapability +
                    opportunity.Difficulty * 100m);
            decimal successChance = Math.Clamp(
                0.25m +
                capabilityRatio * 0.60m +
                _tuning.PlayerInterventionEfficiency +
                profile.SuccessChanceModifier,
                0.10m,
                0.98m);
            var random = CreateTurnRandom(
                turn,
                StableHash(opportunity.Id) ^ 0x5f3759df);
            decimal roll = (decimal)random.NextDouble();
            WorldOperationOutcome outcome = DetermineOutcome(
                roll,
                successChance);
            bool success = outcome == WorldOperationOutcome.GreatSuccess ||
                           outcome == WorldOperationOutcome.Success;
            bool compromise = outcome == WorldOperationOutcome.Compromise;
            opportunity.Resolve(outcome, "player", approach);

            WorldEventInstance worldEvent =
                State.FindEvent(opportunity.EventId);
            decimal money = 0m;
            decimal reputation = 0m;
            if (success)
            {
                if (worldEvent != null)
                {
                    worldEvent.Resolve(true, "player");
                    ApplyEventResolution(
                        worldEvent,
                        profile.ConsequenceStrength);
                    _resolvedEventBuffer.Add(worldEvent);
                }
            }
            else if (compromise)
            {
                worldEvent?.Mitigate(
                    0.18m * profile.ConsequenceStrength);
            }
            else
            {
                decimal escalation = outcome == WorldOperationOutcome.Disaster
                    ? profile.FailureEscalation
                    : profile.FailureEscalation * 0.50m;
                worldEvent?.Escalate(escalation);
            }

            decimal outcomeRewardMultiplier =
                GetOutcomeRewardMultiplier(outcome);
            if (outcomeRewardMultiplier > 0m)
            {
                money = opportunity.MoneyReward *
                    profile.MoneyRewardMultiplier *
                    outcomeRewardMultiplier;
                reputation = opportunity.ReputationReward *
                    profile.ReputationRewardMultiplier *
                    outcomeRewardMultiplier;
                State.AddPlayerRewards(money, reputation);
                RewardPlayerCompany(money);
            }

            ApplyApproachConsequence(
                opportunity,
                worldEvent,
                profile,
                outcome);

            LastPlayerIntervention = new PlayerInterventionResult(
                true,
                success,
                BuildOutcomeMessage(profile.DisplayName, outcome),
                money,
                reputation,
                outcome,
                approach,
                upfrontCost);
            return LastPlayerIntervention;
        }

        private void InitializeResourceSites()
        {
            if (State.ResourceSites.Count > 0)
                return;

            for (int i = 0; i < State.World.ResourceSiteSeeds.Count; i++)
            {
                ResourceSiteSeed seed = State.World.ResourceSiteSeeds[i];
                var site = new ResourceExtractionSite(
                    seed.Id,
                    seed.RegionId,
                    seed.ResourceId,
                    new TurnNumber(1),
                    seed.InitialOutput,
                    seed.MinimumOutput,
                    seed.DeclineRate,
                    seed.TotalReserve,
                    seed.ExtractionEfficiency,
                    seed.Labor,
                    70m,
                    seed.OwnerFactionId,
                    seed.Method);
                State.AddResourceSite(site);
                _economy.RegisterResourceSite(site);
            }
        }

        public void SynchronizeResourceSites()
        {
            for (int i = 0; i < _economy.ResourceSites.Count; i++)
                State.AddResourceSite(_economy.ResourceSites[i]);
        }

        private void InitializeArmies()
        {
            if (State.Armies.Count > 0)
                return;

            UnitArchetype[] archetypes =
            {
                UnitArchetype.Swordsman,
                UnitArchetype.Spearman,
                UnitArchetype.Maceman,
                UnitArchetype.Archer,
                UnitArchetype.Slinger,
                UnitArchetype.Cavalry
            };
            int soldiersPerUnit = Math.Max(
                1,
                _tuning.InitialArmySoldiersPerFaction /
                archetypes.Length);

            for (int i = 0; i < State.World.Factions.Count; i++)
            {
                WorldFactionState faction = State.World.Factions[i];
                RegionId homeRegion = FindFactionHome(faction.Id);
                var army = new ArmyState(
                    $"army_{faction.Id}",
                    faction.Id,
                    homeRegion);
                for (int j = 0; j < archetypes.Length; j++)
                {
                    UnitArchetype archetype = archetypes[j];
                    army.AddUnit(new MilitaryUnit(
                        $"unit_{faction.Id}_{archetype.ToString().ToLowerInvariant()}",
                        faction.Id,
                        _militaryBalance.Get(archetype),
                        CreateStarterEquipment(archetype),
                        soldiersPerUnit,
                        averageExperience: 8m + i * 3m,
                        morale: 1m,
                        supplyRatio: 1m));
                }

                State.AddArmy(army);
            }
        }

        private static EquipmentLoadout CreateStarterEquipment(
            UnitArchetype archetype)
        {
            switch (archetype)
            {
                case UnitArchetype.Maceman:
                case UnitArchetype.Spearman:
                    return new EquipmentLoadout(
                        "heavy_kit",
                        "중장비",
                        ArmorProfile.Heavy,
                        1m,
                        0.78m,
                        1.10m,
                        1.45m);
                case UnitArchetype.Archer:
                case UnitArchetype.Slinger:
                    return new EquipmentLoadout(
                        "light_kit",
                        "경장비",
                        ArmorProfile.Light,
                        1m,
                        1.05m,
                        0.85m,
                        0.90m);
                case UnitArchetype.Cavalry:
                    return new EquipmentLoadout(
                        "cavalry_kit",
                        "기마 장비",
                        ArmorProfile.Light,
                        1.05m,
                        1.12m,
                        1.18m,
                        1.55m);
                default:
                    return new EquipmentLoadout(
                        "standard_kit",
                        "표준 장비",
                        ArmorProfile.Light);
            }
        }

        private void RestoreTransientProductionModifiers()
        {
            for (int i = 0; i < State.ResourceSites.Count; i++)
                State.ResourceSites[i].RestoreTemporaryEfficiency();
        }

        private void ResolveDueOpportunities(TurnNumber turn)
        {
            for (int i = 0; i < State.Opportunities.Count; i++)
            {
                WorldOpportunity opportunity = State.Opportunities[i];
                if (opportunity.Status != WorldOpportunityStatus.Offered ||
                    turn.Value < opportunity.NpcResolveTurn.Value)
                {
                    continue;
                }

                WorldNpcState npc = FindBestNpc(opportunity.RegionId);
                decimal competence = npc?.Competence ?? 0.25m;
                decimal initiative = npc?.Initiative ?? 0.20m;
                decimal chance = Math.Clamp(
                    _tuning.NpcBaseSuccessChance +
                    competence * 0.40m +
                    initiative * 0.20m -
                    opportunity.Difficulty * 0.35m,
                    0.05m,
                    0.95m);
                var random = CreateTurnRandom(
                    turn,
                    StableHash(opportunity.Id));
                bool success = (decimal)random.NextDouble() <= chance;
                string resolver = npc?.Id ?? "world_simulation";
                opportunity.Resolve(success, resolver);
                WorldEventInstance worldEvent =
                    State.FindEvent(opportunity.EventId);
                if (success)
                {
                    if (worldEvent != null)
                    {
                        worldEvent.Resolve(false, resolver);
                        ApplyEventResolution(worldEvent);
                        _resolvedEventBuffer.Add(worldEvent);
                    }
                }
                else
                {
                    worldEvent?.Escalate(0.08m);
                }
            }
        }

        private void ResolveExpiredPositiveEvents(TurnNumber turn)
        {
            for (int i = 0; i < State.Events.Count; i++)
            {
                WorldEventInstance worldEvent = State.Events[i];
                if (!worldEvent.IsActive ||
                    (worldEvent.Kind != WorldEventKind.BountifulHarvest &&
                     worldEvent.Kind != WorldEventKind.NewVeinDiscovered) ||
                    turn.Value <= worldEvent.CreatedTurn.Value)
                {
                    continue;
                }

                worldEvent.Resolve(false, "world_simulation");
                _resolvedEventBuffer.Add(worldEvent);
            }
        }

        private void GenerateRandomEvent(TurnNumber turn)
        {
            var random = CreateTurnRandom(turn, 0x2c1b3c6d);
            if ((decimal)random.NextDouble() >
                _tuning.RandomEventChancePerTurn)
            {
                return;
            }

            GeneratedRegionState region = State.World.Regions[
                random.Next(State.World.Regions.Count)];
            WorldEventKind[] randomKinds =
            {
                WorldEventKind.HarvestFailure,
                WorldEventKind.BountifulHarvest,
                WorldEventKind.MineCollapse,
                WorldEventKind.NewVeinDiscovered,
                WorldEventKind.BanditIncrease,
                WorldEventKind.ImportantNpcDeath
            };
            WorldEventKind kind = randomKinds[random.Next(randomKinds.Length)];
            ResourceExtractionSite site = FindRegionSite(region.Id, true);
            if ((kind == WorldEventKind.MineCollapse ||
                 kind == WorldEventKind.NewVeinDiscovered) &&
                site == null)
            {
                kind = WorldEventKind.BanditIncrease;
            }

            ResourceId? resourceId = site?.ResourceId;
            string targetId = site?.Id ?? region.Id.Value;
            CreateWorldEvent(
                kind,
                WorldEventTrigger.Random,
                region.Id,
                resourceId,
                targetId,
                turn,
                NextDecimal(random, 0.30m, 0.85m));
        }

        private void GenerateCausalEvents(TurnNumber turn)
        {
            int generatedThisTurn = 0;
            _causalEventKeys.Clear();
            for (int i = 0; i < State.Events.Count; i++)
            {
                WorldEventInstance active = State.Events[i];
                if (active.IsActive && active.ResourceId.HasValue)
                {
                    _causalEventKeys.Add(
                        BuildCausalKey(active.RegionId, active.ResourceId.Value));
                }
            }

            for (int i = 0; i < _economy.Markets.Count; i++)
            {
                if (generatedThisTurn >= _tuning.MaxCausalEventsPerTurn)
                    break;

                MarketRuntimeState market = _economy.Markets[i];
                decimal demand = market.MarketState.DailyDemand;
                if (demand <= 0m)
                    continue;
                decimal shortage = market.MarketState.UnmetDemand / demand;
                if (shortage < _tuning.CausalShortageThreshold)
                    continue;

                string key = BuildCausalKey(
                    market.RegionId,
                    market.Definition.Id);
                if (!_causalEventKeys.Add(key))
                    continue;
                if (HasRecentCausalEvent(
                    market.RegionId,
                    market.Definition.Id,
                    turn))
                {
                    continue;
                }

                WorldEventKind kind =
                    market.Definition.Id.Value == "steel" ||
                    market.Definition.Id.Value == "medicine"
                        ? WorldEventKind.MilitarySupplyShortage
                        : WorldEventKind.FactoryDisruption;
                CreateWorldEvent(
                    kind,
                    WorldEventTrigger.Causal,
                    market.RegionId,
                    market.Definition.Id,
                    key,
                    turn,
                    Math.Clamp(shortage, 0.20m, 1m));
                generatedThisTurn++;
            }

            for (int i = 0; i < State.World.Regions.Count; i++)
            {
                if (generatedThisTurn >= _tuning.MaxCausalEventsPerTurn)
                    break;

                GeneratedRegionState region = State.World.Regions[i];
                if (region.BanditThreat < 0.70m ||
                    HasActiveEvent(WorldEventKind.BanditIncrease, region.Id))
                {
                    continue;
                }

                CreateWorldEvent(
                    WorldEventKind.BanditIncrease,
                    WorldEventTrigger.Causal,
                    region.Id,
                    null,
                    region.Id.Value,
                    turn,
                    region.BanditThreat);
                generatedThisTurn++;
            }
        }

        private bool HasRecentCausalEvent(
            RegionId regionId,
            ResourceId resourceId,
            TurnNumber turn)
        {
            for (int i = State.Events.Count - 1; i >= 0; i--)
            {
                WorldEventInstance worldEvent = State.Events[i];
                if (worldEvent.Trigger != WorldEventTrigger.Causal ||
                    !worldEvent.RegionId.Equals(regionId) ||
                    !worldEvent.ResourceId.HasValue ||
                    !worldEvent.ResourceId.Value.Equals(resourceId))
                {
                    continue;
                }

                return turn.Value - worldEvent.CreatedTurn.Value <=
                    _tuning.RepeatEventCooldownTurns;
            }

            return false;
        }

        private void CreateWorldEvent(
            WorldEventKind kind,
            WorldEventTrigger trigger,
            RegionId regionId,
            ResourceId? resourceId,
            string targetId,
            TurnNumber turn,
            decimal severity)
        {
            string id = $"event_{turn.Value:D3}_{State.Events.Count + 1:D3}";
            var worldEvent = new WorldEventInstance(
                id,
                kind,
                trigger,
                regionId,
                resourceId,
                targetId,
                turn,
                severity);
            State.AddEvent(worldEvent);
            _generatedEventBuffer.Add(worldEvent);

            GeneratedRegionState region = State.World.FindRegion(regionId);
            switch (kind)
            {
                case WorldEventKind.MineCollapse:
                    State.FindResourceSite(targetId)?.SetActive(false);
                    break;
                case WorldEventKind.NewVeinDiscovered:
                    State.FindResourceSite(targetId)?.DiscoverAdditionalReserve(
                        _tuning.NewVeinReserveBonus * severity);
                    break;
                case WorldEventKind.BanditIncrease:
                    region?.AdjustBanditThreat(0.12m * severity);
                    break;
                case WorldEventKind.ImportantNpcDeath:
                    KillRegionNpc(regionId);
                    region?.AdjustStability(-0.12m * severity);
                    break;
            }
        }

        private void EnsureOpportunitiesForActiveEvents(TurnNumber turn)
        {
            for (int i = 0; i < State.Events.Count; i++)
            {
                WorldEventInstance worldEvent = State.Events[i];
                if (!worldEvent.IsActive ||
                    worldEvent.Kind == WorldEventKind.BountifulHarvest ||
                    worldEvent.Kind == WorldEventKind.NewVeinDiscovered ||
                    HasOpenOpportunity(worldEvent.Id))
                {
                    continue;
                }

                WorldOpportunityKind kind = GetOpportunityKind(
                    worldEvent.Kind);
                int resolveTurn = turn.Value +
                    _tuning.NpcAutoResolveDelayTurns;
                var opportunity = new WorldOpportunity(
                    $"mission_{worldEvent.Id}_{State.Opportunities.Count + 1:D2}",
                    worldEvent.Id,
                    kind,
                    GetOpportunityName(kind),
                    worldEvent.RegionId,
                    turn,
                    new TurnNumber(resolveTurn),
                    worldEvent.Severity,
                    _tuning.PlayerBaseReward *
                        (0.65m + worldEvent.Severity),
                    _tuning.PlayerReputationReward *
                        (0.75m + worldEvent.Severity));
                State.AddOpportunity(opportunity);
                _offeredOpportunityBuffer.Add(opportunity);
            }
        }

        private void ApplyActiveEventModifiers()
        {
            for (int i = 0; i < State.Events.Count; i++)
            {
                WorldEventInstance worldEvent = State.Events[i];
                if (!worldEvent.IsActive)
                    continue;

                if (worldEvent.Kind == WorldEventKind.MineCollapse)
                {
                    State.FindResourceSite(worldEvent.TargetId)?
                        .ApplyTemporaryEfficiency(
                            _tuning.EventProductionPenalty);
                }
                else if (worldEvent.Kind == WorldEventKind.BanditIncrease)
                {
                    for (int j = 0; j < State.ResourceSites.Count; j++)
                    {
                        ResourceExtractionSite site = State.ResourceSites[j];
                        if (site.RegionId.Equals(worldEvent.RegionId))
                        {
                            site.ApplyTemporaryEfficiency(
                                1m - worldEvent.Severity * 0.35m);
                        }
                    }
                }
            }
        }

        private void BuildFacilityFlows()
        {
            for (int i = 0; i < State.World.Facilities.Count; i++)
            {
                WorldFacilityState facility = State.World.Facilities[i];
                if (!facility.IsOperational)
                    continue;

                WorldFactionState owner = State.World.FindFaction(
                    facility.OwnerFactionId);
                decimal ratio = facility.OperatingRatio;
                decimal cost = facility.MaintenanceCost * ratio;
                if (owner != null && !owner.TrySpend(cost))
                    ratio *= 0.25m;

                ratio *= GetFacilityEventFactor(facility);
                if (facility.InputResourceId.HasValue)
                {
                    AddFlowIfMarketExists(
                        facility.RegionId,
                        facility.InputResourceId.Value,
                        0m,
                        facility.InputPerTurn * ratio,
                        $"{facility.Kind} 원재료 수요");
                }
                if (facility.OutputResourceId.HasValue)
                {
                    AddFlowIfMarketExists(
                        facility.RegionId,
                        facility.OutputResourceId.Value,
                        facility.OutputPerTurn * ratio,
                        0m,
                        $"{facility.Kind} 생산");
                }
            }
        }

        private decimal GetFacilityEventFactor(WorldFacilityState facility)
        {
            decimal factor = 1m;
            for (int i = 0; i < State.Events.Count; i++)
            {
                WorldEventInstance worldEvent = State.Events[i];
                if (!worldEvent.IsActive ||
                    !worldEvent.RegionId.Equals(facility.RegionId))
                {
                    continue;
                }

                if (worldEvent.Kind == WorldEventKind.HarvestFailure &&
                    facility.Kind == WorldFacilityKind.Farm)
                {
                    factor *= _tuning.EventProductionPenalty;
                }
                else if (worldEvent.Kind == WorldEventKind.BountifulHarvest &&
                    facility.Kind == WorldFacilityKind.Farm)
                {
                    factor *= _tuning.BountifulProductionBonus;
                }
                else if (worldEvent.Kind == WorldEventKind.FactoryDisruption &&
                    (facility.Kind == WorldFacilityKind.Workshop ||
                     facility.Kind == WorldFacilityKind.Arsenal))
                {
                    factor *= _tuning.EventProductionPenalty;
                }
                else if (worldEvent.Kind == WorldEventKind.BanditIncrease)
                {
                    factor *= 1m - worldEvent.Severity * 0.25m;
                }
            }

            return Math.Clamp(factor, 0.05m, 2.5m);
        }

        private void BuildMilitaryFlows()
        {
            for (int i = 0; i < State.Armies.Count; i++)
            {
                ArmyState army = State.Armies[i];
                decimal supplyRatio = CalculateArmySupplyRatio(army);
                WorldFactionState faction = State.World.FindFaction(
                    army.FactionId);
                ReinforceArmy(army, faction, supplyRatio);
                decimal upkeep = _tuning.MilitaryLogistics
                    .CalculateDailyUpkeep(army);
                if (faction != null && !faction.TrySpend(upkeep))
                    supplyRatio *= 0.60m;

                for (int j = 0; j < army.Units.Count; j++)
                    army.Units[j].SetSupplyRatio(supplyRatio);

                int soldiers = army.TotalSoldiers;
                AddMilitaryDemand(
                    army.RegionId,
                    "food",
                    soldiers *
                    _tuning.MilitaryLogistics.FoodDemandPerSoldier,
                    "군대 식량 수요");
                AddMilitaryDemand(
                    army.RegionId,
                    "steel",
                    soldiers *
                    _tuning.MilitaryLogistics.EquipmentDemandPerSoldier,
                    "군수 장비 수요");
                AddMilitaryDemand(
                    army.RegionId,
                    "medicine",
                    soldiers *
                    _tuning.MilitaryLogistics.MedicineDemandPerSoldier,
                    "군 의료 수요");
                _armyReadinessBuffer.Add(new ArmyReadinessRecord(
                    army.Id,
                    soldiers,
                    supplyRatio,
                    _tuning.MilitaryLogistics.GetReadiness(supplyRatio),
                    upkeep));
            }
        }

        private void ReinforceArmy(
            ArmyState army,
            WorldFactionState faction,
            decimal supplyRatio)
        {
            int deficit = Math.Max(
                0,
                _tuning.InitialArmySoldiersPerFaction -
                army.TotalSoldiers);
            if (deficit == 0 || army.Units.Count == 0)
                return;

            decimal factionCapacity = faction == null
                ? 0.50m
                : 0.35m + faction.MilitaryFocus * 0.65m;
            int replacementCapacity = Math.Max(
                1,
                (int)decimal.Floor(
                    12m *
                    factionCapacity *
                    _tuning.MilitaryLogistics.GetReplacementSpeed(
                        supplyRatio)));
            int remaining = Math.Min(deficit, replacementCapacity);
            for (int i = 0; i < army.Units.Count && remaining > 0; i++)
            {
                int unitsLeft = army.Units.Count - i;
                int recruits = Math.Max(1, remaining / unitsLeft);
                army.Units[i].Recruit(recruits);
                remaining -= recruits;
            }
        }

        private decimal CalculateArmySupplyRatio(ArmyState army)
        {
            decimal ratio = 1m;
            bool found = false;
            string[] resources = { "food", "steel", "medicine" };
            for (int i = 0; i < resources.Length; i++)
            {
                if (!_economy.TryGetMarket(
                    army.RegionId,
                    new ResourceId(resources[i]),
                    out var market))
                {
                    continue;
                }

                found = true;
                decimal target = Math.Max(
                    1m,
                    market.MarketState.DailyDemand * 7m);
                decimal resourceRatio = Math.Clamp(
                    market.MarketState.MarketStock / target,
                    0m,
                    1m);
                ratio = Math.Min(ratio, resourceRatio);
            }

            return found ? ratio : 0.65m;
        }

        private void AddMilitaryDemand(
            RegionId regionId,
            string resourceId,
            decimal demand,
            string reason)
        {
            AddFlowIfMarketExists(
                regionId,
                new ResourceId(resourceId),
                0m,
                demand,
                reason);
        }

        private void AddFlowIfMarketExists(
            RegionId regionId,
            ResourceId resourceId,
            decimal supply,
            decimal demand,
            string reason)
        {
            if (!_economy.TryGetMarket(regionId, resourceId, out _))
                return;

            _flowBuffer.Add(new WorldFlowContribution(
                regionId,
                resourceId,
                supply,
                demand,
                0m,
                reason));
        }

        private void ApplyEventResolution(
            WorldEventInstance worldEvent,
            decimal strength = 1m)
        {
            if (worldEvent == null)
                return;

            decimal resolvedStrength = Math.Clamp(strength, 0.10m, 2m);

            GeneratedRegionState region = State.World.FindRegion(
                worldEvent.RegionId);
            switch (worldEvent.Kind)
            {
                case WorldEventKind.MineCollapse:
                    State.FindResourceSite(worldEvent.TargetId)?.SetActive(true);
                    break;
                case WorldEventKind.BanditIncrease:
                    region?.AdjustBanditThreat(-0.35m * resolvedStrength);
                    region?.AdjustStability(0.08m * resolvedStrength);
                    break;
                case WorldEventKind.HarvestFailure:
                case WorldEventKind.FactoryDisruption:
                case WorldEventKind.MilitarySupplyShortage:
                    region?.AdjustStability(0.04m * resolvedStrength);
                    break;
                case WorldEventKind.ImportantNpcDeath:
                    region?.AdjustStability(0.06m * resolvedStrength);
                    break;
            }
        }

        private void ApplyApproachConsequence(
            WorldOpportunity opportunity,
            WorldEventInstance worldEvent,
            WorldOperationApproachProfile profile,
            WorldOperationOutcome outcome)
        {
            GeneratedRegionState region = State.World.FindRegion(
                opportunity.RegionId);
            decimal outcomeStrength = GetOutcomeConsequenceMultiplier(outcome) *
                profile.ConsequenceStrength;
            if (outcomeStrength <= 0m)
            {
                if (outcome == WorldOperationOutcome.Disaster)
                {
                    region?.AdjustStability(-0.05m);
                    if (profile.Approach == WorldOperationApproach.CovertAction ||
                        profile.Approach == WorldOperationApproach.ArmedSecurity)
                    {
                        region?.AdjustBanditThreat(0.08m);
                    }
                }
                return;
            }

            switch (profile.Approach)
            {
                case WorldOperationApproach.Negotiation:
                    region?.AdjustStability(0.035m * outcomeStrength);
                    break;
                case WorldOperationApproach.Logistics:
                    region?.AdjustStability(0.018m * outcomeStrength);
                    break;
                case WorldOperationApproach.TechnicalInvestment:
                    State.FindResourceSite(worldEvent?.TargetId)?
                        .DiscoverAdditionalReserve(350m * outcomeStrength);
                    region?.AdjustStability(0.015m * outcomeStrength);
                    break;
                case WorldOperationApproach.CovertAction:
                    region?.AdjustBanditThreat(-0.10m * outcomeStrength);
                    break;
                case WorldOperationApproach.ArmedSecurity:
                    region?.AdjustBanditThreat(-0.16m * outcomeStrength);
                    region?.AdjustStability(0.012m * outcomeStrength);
                    break;
                case WorldOperationApproach.PublicRelief:
                    region?.AdjustStability(0.075m * outcomeStrength);
                    break;
            }
        }

        private CompanyEconomyRuntime FindPlayerCompany()
        {
            for (int i = 0; i < _economy.Companies.Count; i++)
            {
                CompanyEconomyRuntime company = _economy.Companies[i];
                if (company.CampaignState.IsPlayer)
                    return company;
            }

            return null;
        }

        private static decimal CalculateUpfrontCost(
            WorldOpportunity opportunity,
            WorldOperationApproachProfile profile)
        {
            return WorldOperationCatalog.CalculateUpfrontCost(
                opportunity,
                profile);
        }

        private static WorldOperationOutcome DetermineOutcome(
            decimal roll,
            decimal successChance)
        {
            if (roll <= successChance * 0.18m)
                return WorldOperationOutcome.GreatSuccess;
            if (roll <= successChance)
                return WorldOperationOutcome.Success;
            if (roll <= Math.Min(0.97m, successChance + 0.16m))
                return WorldOperationOutcome.Compromise;
            return roll >= 0.965m
                ? WorldOperationOutcome.Disaster
                : WorldOperationOutcome.Failure;
        }

        private static decimal GetOutcomeRewardMultiplier(
            WorldOperationOutcome outcome)
        {
            switch (outcome)
            {
                case WorldOperationOutcome.GreatSuccess:
                    return 1.25m;
                case WorldOperationOutcome.Success:
                    return 1m;
                case WorldOperationOutcome.Compromise:
                    return 0.35m;
                default:
                    return 0m;
            }
        }

        private static decimal GetOutcomeConsequenceMultiplier(
            WorldOperationOutcome outcome)
        {
            switch (outcome)
            {
                case WorldOperationOutcome.GreatSuccess:
                    return 1.25m;
                case WorldOperationOutcome.Success:
                    return 1m;
                case WorldOperationOutcome.Compromise:
                    return 0.40m;
                default:
                    return 0m;
            }
        }

        private static string BuildOutcomeMessage(
            string approachName,
            WorldOperationOutcome outcome)
        {
            string result;
            switch (outcome)
            {
                case WorldOperationOutcome.GreatSuccess:
                    result = "대성공: 주목표와 추가 이권을 확보했습니다.";
                    break;
                case WorldOperationOutcome.Success:
                    result = "성공: 세계 문제가 해결되고 경제에 반영됩니다.";
                    break;
                case WorldOperationOutcome.Compromise:
                    result = "부분 성공: 피해를 줄였지만 후속 작전이 필요합니다.";
                    break;
                case WorldOperationOutcome.Disaster:
                    result = "대실패: 사태가 악화되고 지역 불안이 커졌습니다.";
                    break;
                default:
                    result = "실패: 목표를 달성하지 못했고 세계는 계속 변합니다.";
                    break;
            }

            return $"{approachName} · {result}";
        }

        private void RewardPlayerCompany(decimal amount)
        {
            FindPlayerCompany()?.Company.Receive(amount);
        }

        private WorldNpcState FindBestNpc(RegionId regionId)
        {
            WorldNpcState best = null;
            decimal bestScore = decimal.MinValue;
            for (int i = 0; i < State.World.Npcs.Count; i++)
            {
                WorldNpcState npc = State.World.Npcs[i];
                if (!npc.IsAlive)
                    continue;
                decimal localBonus = npc.RegionId.Equals(regionId)
                    ? 0.20m
                    : 0m;
                decimal score = npc.Competence * 0.65m +
                    npc.Initiative * 0.35m +
                    localBonus;
                if (score > bestScore)
                {
                    best = npc;
                    bestScore = score;
                }
            }

            return best;
        }

        private void KillRegionNpc(RegionId regionId)
        {
            for (int i = 0; i < State.World.Npcs.Count; i++)
            {
                WorldNpcState npc = State.World.Npcs[i];
                if (npc.IsAlive && npc.RegionId.Equals(regionId))
                {
                    npc.Kill();
                    return;
                }
            }
        }

        private ResourceExtractionSite FindRegionSite(
            RegionId regionId,
            bool activeOnly)
        {
            for (int i = 0; i < State.ResourceSites.Count; i++)
            {
                ResourceExtractionSite site = State.ResourceSites[i];
                if (site.RegionId.Equals(regionId) &&
                    (!activeOnly || site.IsActive))
                {
                    return site;
                }
            }

            return null;
        }

        private RegionId FindFactionHome(string factionId)
        {
            for (int i = 0; i < State.World.Regions.Count; i++)
            {
                GeneratedRegionState region = State.World.Regions[i];
                if (region.OwnerFactionId == factionId &&
                    region.Settlement == SettlementKind.Capital)
                {
                    return region.Id;
                }
            }

            for (int i = 0; i < State.World.Regions.Count; i++)
            {
                if (State.World.Regions[i].OwnerFactionId == factionId)
                    return State.World.Regions[i].Id;
            }

            return State.World.Regions[0].Id;
        }

        private bool HasActiveEvent(
            WorldEventKind kind,
            RegionId regionId)
        {
            for (int i = 0; i < State.Events.Count; i++)
            {
                WorldEventInstance worldEvent = State.Events[i];
                if (worldEvent.IsActive &&
                    worldEvent.Kind == kind &&
                    worldEvent.RegionId.Equals(regionId))
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasOpenOpportunity(string eventId)
        {
            for (int i = 0; i < State.Opportunities.Count; i++)
            {
                WorldOpportunity opportunity = State.Opportunities[i];
                if (opportunity.EventId == eventId &&
                    (opportunity.Status == WorldOpportunityStatus.Offered ||
                     opportunity.Status == WorldOpportunityStatus.Accepted))
                {
                    return true;
                }
            }

            return false;
        }

        private static WorldOpportunityKind GetOpportunityKind(
            WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.MineCollapse:
                    return WorldOpportunityKind.RepairMine;
                case WorldEventKind.BanditIncrease:
                    return WorldOpportunityKind.SuppressBandits;
                case WorldEventKind.MilitarySupplyShortage:
                    return WorldOpportunityKind.EscortSupply;
                case WorldEventKind.FactoryDisruption:
                    return WorldOpportunityKind.ProtectFacility;
                case WorldEventKind.HarvestFailure:
                    return WorldOpportunityKind.EmergencyDelivery;
                default:
                    return WorldOpportunityKind.StabilizeRegion;
            }
        }

        private static string GetOpportunityName(WorldOpportunityKind kind)
        {
            switch (kind)
            {
                case WorldOpportunityKind.SuppressBandits:
                    return "도적 토벌";
                case WorldOpportunityKind.EscortSupply:
                    return "군수품 수송 호위";
                case WorldOpportunityKind.RepairMine:
                    return "붕괴 광산 복구";
                case WorldOpportunityKind.SurveyVein:
                    return "신규 광맥 조사";
                case WorldOpportunityKind.ProtectFacility:
                    return "생산시설 보호";
                case WorldOpportunityKind.EmergencyDelivery:
                    return "긴급 물자 납품";
                default:
                    return "지역 안정화";
            }
        }

        private Random CreateTurnRandom(TurnNumber turn, int salt)
        {
            return new Random(unchecked(
                State.World.Seed * 397 ^
                turn.Value * 7919 ^
                salt));
        }

        private static string BuildCausalKey(
            RegionId regionId,
            ResourceId resourceId)
        {
            return regionId.Value + ":" + resourceId.Value;
        }

        private static int StableHash(string value)
        {
            unchecked
            {
                int hash = (int)2166136261;
                string source = value ?? string.Empty;
                for (int i = 0; i < source.Length; i++)
                {
                    hash ^= source[i];
                    hash *= 16777619;
                }
                return hash;
            }
        }

        private static decimal NextDecimal(
            Random random,
            decimal minimum,
            decimal maximum)
        {
            return minimum +
                (maximum - minimum) * (decimal)random.NextDouble();
        }
    }

    public sealed class InterveneWorldOpportunityTurnCommand : ITurnCommand
    {
        private readonly IAutonomousWorldTurnService _world;
        private readonly string _opportunityId;
        private readonly decimal _playerCapability;
        private readonly WorldOperationApproach? _approach;

        public CompanyId ActorId { get; }
        public string DisplayName { get; }
        public int ActionPointCost { get; }

        public InterveneWorldOpportunityTurnCommand(
            IAutonomousWorldTurnService world,
            string opportunityId,
            CompanyId actorId,
            decimal playerCapability,
            string displayName,
            int actionPointCost = 2)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _opportunityId = opportunityId ?? string.Empty;
            _playerCapability = Math.Max(0m, playerCapability);
            _approach = null;
            ActorId = actorId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "세계 사건 개입"
                : displayName;
            ActionPointCost = Math.Max(1, actionPointCost);
        }

        public InterveneWorldOpportunityTurnCommand(
            IAutonomousWorldTurnService world,
            string opportunityId,
            CompanyId actorId,
            decimal playerCapability,
            string displayName,
            WorldOperationApproach approach,
            int actionPointCost = 2)
            : this(
                world,
                opportunityId,
                actorId,
                playerCapability,
                displayName,
                actionPointCost)
        {
            _approach = approach;
        }

        public bool CanExecute(
            TurnCommandContext context,
            out string reason)
        {
            return _approach.HasValue
                ? _world.CanPlayerIntervene(
                    _opportunityId,
                    _approach.Value,
                    out reason)
                : _world.CanPlayerIntervene(_opportunityId, out reason);
        }

        public void Execute(TurnCommandContext context)
        {
            PlayerInterventionResult result = _approach.HasValue
                ? _world.TryPlayerIntervention(
                    _opportunityId,
                    _playerCapability,
                    _approach.Value,
                    context.Turn)
                : _world.TryPlayerIntervention(
                    _opportunityId,
                    _playerCapability,
                    context.Turn);
            if (!result.Accepted)
                throw new InvalidOperationException(result.Message);
        }
    }
}
