using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Application;
using Game.Application.AI;
using Game.Application.Campaign;
using Game.Application.Turn;
using Game.Application.World;
using Game.Data;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Market;
using Game.Domain.Inventory;
using Game.Domain.Logistics;
using Game.Domain.Military;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Presentation
{
    public sealed class SimulationBootstrapper : MonoBehaviour
    {
        [SerializeField] private ResourceDefinitionAsset[] resourceAssets;
        [SerializeField] private RecipeDefinitionAsset[] recipeAssets;
        [SerializeField] private SimulationSettingsAsset simulationSettings;
        [SerializeField] private MilitaryBalanceAsset militaryBalance;
        [SerializeField] private string regionId = "starter_region";
        [SerializeField] private float initialMarketStock = 1000f;

        [Header("캠페인 참가 회사")]
        [SerializeField] private string playerCompanyName = "플레이어 기업";
        [SerializeField, Min(0f)] private float initialCompanyCash = 1000000f;
        [SerializeField, Min(1)] private int aiCompanyCount = 3;

        [Header("MVP 회사 운영")]
        [SerializeField, Min(1f)] private float starterWarehouseCapacity = 10000f;
        [SerializeField, Min(0f)] private float starterIronStock = 100f;
        [SerializeField, Min(0f)] private float starterCoalStock = 60f;
        [SerializeField, Min(0f)] private float starterFoodStock = 50f;
        [SerializeField, Min(0)] private int starterEmployees = 20;
        [SerializeField, Min(0f)] private float starterWorkers = 20f;
        [SerializeField, Min(0f)] private float starterPower = 20f;
        [SerializeField, Min(0)] private int starterVehicles = 1;

        private SimulationEngine _simulation;
        private readonly List<PhysicalFlow> _turnFlowBuffer =
            new List<PhysicalFlow>(16);
        private ResourceCatalog _catalog;
        private CampaignParticipantState _playerCampaignState;
        private CampaignSession _campaignSession;
        private CampaignRuleSet _campaignRules;
        private WorldEconomyState _worldEconomy;
        private AutonomousWorldState _autonomousWorldState;
        private IAutonomousWorldTurnService _autonomousWorldService;
        private RealtimeSimulationClock _realtimeClock;
        private int _lastRealtimeMinuteStamp = -1;

        public TurnNumber CurrentTurn =>
            _simulation?.CurrentTurn ?? new TurnNumber(1);
        public TurnPhase CurrentPhase =>
            _simulation?.Phase ?? TurnPhase.PlayerPlanning;
        public int RemainingActionPoints =>
            _simulation?.PlayerCommands.RemainingActionPoints ?? 0;
        public int QueuedCommandCount =>
            _simulation?.PlayerCommands.Count ?? 0;
        public bool IsCampaignFinished =>
            _simulation?.IsCampaignFinished ?? false;
        public CampaignTurnResult CampaignResult =>
            _simulation?.CampaignResult;
        public CampaignState CurrentCampaignState =>
            _campaignSession?.State;
        public WorldEconomyState CurrentWorldEconomy =>
            _worldEconomy;
        public AutonomousWorldState CurrentAutonomousWorld =>
            _autonomousWorldState;
        public int MaxCampaignTurns => simulationSettings != null
            ? simulationSettings.MaxCampaignTurns
            : 30;
        public int RealtimeDayNumber =>
            _realtimeClock?.CurrentDayNumber ?? CurrentTurn.Value;
        public int RealtimeHour => _realtimeClock?.HourOfDay ?? 0;
        public int RealtimeMinute => _realtimeClock?.MinuteOfHour ?? 0;
        public int RealtimeSpeedMultiplier =>
            _realtimeClock?.SpeedMultiplier ?? 0;
        public bool IsRealtimePaused =>
            _realtimeClock?.IsPaused ?? true;

        public event Action RealtimeStateChanged;
        public event Action<TurnReport> RealtimeDayResolved;

        private void Awake()
        {
            BuildSimulation();
        }

        private void Update()
        {
            if (_realtimeClock == null ||
                _simulation == null ||
                IsCampaignFinished)
            {
                return;
            }

            RealtimeAdvanceResult advance = _realtimeClock.Advance(
                Time.unscaledDeltaTime);
            if (advance.FixedStepCount <= 0)
                return;

            for (int i = 0;
                 i < advance.CompletedGameDays && !IsCampaignFinished;
                 i++)
            {
                TurnReport report = ResolveCurrentTurn(false);
                RealtimeDayResolved?.Invoke(report);
            }

            if (IsCampaignFinished)
                _realtimeClock.SetSpeed(0);

            NotifyRealtimeStateWhenMinuteChanged();
        }

        public bool SetRealtimeSpeed(int speedMultiplier)
        {
            if (_realtimeClock == null)
                BuildRealtimeClock();
            if (IsCampaignFinished && speedMultiplier > 0)
                return false;

            bool changed = _realtimeClock.SetSpeed(speedMultiplier);
            if (changed)
                RealtimeStateChanged?.Invoke();
            return changed;
        }

        public bool ToggleRealtimePause()
        {
            if (_realtimeClock == null)
                BuildRealtimeClock();
            if (IsCampaignFinished)
                return false;

            bool changed = _realtimeClock.TogglePause();
            if (changed)
                RealtimeStateChanged?.Invoke();
            return changed;
        }

        // Unity UI의 "턴 종료" 버튼에 연결한다.
        public void EndTurn()
        {
            if (IsCampaignFinished)
            {
                Debug.LogWarning("캠페인이 종료되어 턴을 진행할 수 없습니다.");
                return;
            }

            ResolveCurrentTurn();
        }

        public TurnReport ResolveCurrentTurn()
        {
            return ResolveCurrentTurn(true);
        }

        public TurnReport ResolveCurrentTurn(bool writeLog)
        {
            if (_simulation == null)
                BuildSimulation();

            TurnReport report = _simulation.EndTurn();

            if (writeLog)
                Debug.Log(TurnReportKoreanFormatter.Format(report));

            return report;
        }

        public void RestartSimulation()
        {
            _simulation = null;
            _campaignSession = null;
            _playerCampaignState = null;
            _worldEconomy = null;
            _autonomousWorldState = null;
            _autonomousWorldService = null;
            _campaignRules = null;
            _realtimeClock = null;
            _lastRealtimeMinuteStamp = -1;
            _turnFlowBuffer.Clear();
            BuildSimulation();
            RealtimeStateChanged?.Invoke();
        }

        public bool TryQueueMarketOrder(
            MarketOrder order,
            string displayName,
            out string reason)
        {
            if (_simulation == null)
                BuildSimulation();

            if (!_worldEconomy.TryGetCompany(
                order.CompanyId,
                out var company) ||
                !company.CampaignState.IsPlayer)
            {
                reason = "플레이어 회사의 주문만 예약할 수 있습니다.";
                return false;
            }

            if (order.Side == OrderSide.Buy &&
                !company.Company.CanAfford(
                    order.RemainingQuantity * order.LimitPrice))
            {
                reason = "주문에 필요한 현금이 부족합니다.";
                return false;
            }

            if (order.Side == OrderSide.Sell &&
                company.PrimaryWarehouse.GetAvailable(order.ResourceId) <
                    order.RemainingQuantity)
            {
                reason = "판매할 창고 재고가 부족합니다.";
                return false;
            }

            return _simulation.TryQueuePlayerCommand(
                new SubmitMarketOrderTurnCommand(
                    order,
                    displayName),
                out reason);
        }

        public bool CancelLastCommand()
        {
            return _simulation != null &&
                _simulation.TryCancelLastPlayerCommand();
        }

        public bool TryQueueWorldIntervention(
            string opportunityId,
            decimal playerCapability,
            out string reason)
        {
            if (_simulation == null)
                BuildSimulation();
            if (_autonomousWorldService == null)
            {
                reason = "자율 세계 시뮬레이션이 준비되지 않았습니다.";
                return false;
            }

            WorldOpportunity opportunity =
                _autonomousWorldState?.FindOpportunity(opportunityId);
            string displayName = opportunity == null
                ? "세계 사건 개입"
                : opportunity.DisplayName;
            return _simulation.TryQueuePlayerCommand(
                new InterveneWorldOpportunityTurnCommand(
                    _autonomousWorldService,
                    opportunityId,
                    _playerCampaignState.Company.Id,
                    playerCapability,
                    displayName),
                out reason);
        }

        public bool TryQueueWorldIntervention(
            string opportunityId,
            out string reason)
        {
            return TryQueueWorldIntervention(
                opportunityId,
                playerCapability: 0m,
                out reason);
        }

        public void DestroyPlayerCapital()
        {
            _playerCampaignState?.DestroyCapital();
        }

        public void MarkPlayerBankrupt()
        {
            _playerCampaignState?.Company.MarkBankrupt();
        }

        public void UpdatePlayerEconomicAssets(
            decimal inventoryValue,
            decimal facilityValue,
            decimal logisticsValue,
            decimal territoryValue,
            decimal technologyValue,
            decimal unpaidCosts = 0m)
        {
            _playerCampaignState?.UpdateAssetValues(
                inventoryValue,
                facilityValue,
                logisticsValue,
                territoryValue,
                technologyValue,
                unpaidCosts);
        }

        public void RecordPlayerOperatingProfit(decimal operatingProfit)
        {
            _playerCampaignState?.RecordOperatingProfit(
                operatingProfit);
        }

        private void BuildSimulation()
        {
            LoadRuntimeSettings();
            _catalog = new ResourceCatalog();

            ResourceDefinitionAsset[] configuredResources = resourceAssets;
            if (configuredResources == null || configuredResources.Length == 0)
            {
                configuredResources =
                    UnityEngine.Resources.LoadAll<ResourceDefinitionAsset>(
                        string.Empty);
            }

            int resourceCount = configuredResources?.Length ?? 0;
            int registeredResourceCount = 0;
            for (int i = 0; i < resourceCount; i++)
            {
                var asset = configuredResources[i];
                if (asset == null)
                    continue;

                var definition = asset.ToDomain();
                _catalog.Register(definition);
                registeredResourceCount++;
            }

            if (registeredResourceCount == 0)
                RegisterFallbackResources();

            var marketTuning = simulationSettings != null
                ? simulationSettings.CreateMarketTuning()
                : new MarketTuning();

            int maxOrders = simulationSettings != null
                ? simulationSettings.MaxOrdersPerTurn
                : 10000;

            var ledger = new SupplyDemandLedger(marketTuning);
            var market = new MarketManager(
                ledger,
                new PriceCalculator(),
                maxOrders);

            var rules = new TurnRuleSet(
                simulationSettings != null
                    ? simulationSettings.MaxActionPoints
                    : 5,
                simulationSettings != null
                    ? simulationSettings.DaysPerTurn
                    : 1);

            _campaignRules = simulationSettings != null
                ? simulationSettings.CreateCampaignRuleSet()
                : new CampaignRuleSet();
            _campaignSession = BuildCampaign(_campaignRules);
            var worldTurnService = BuildWorldEconomy(
                _campaignRules);
            var orchestrator = new TurnResolutionOrchestrator(
                market,
                worldTurnService);
            var aiTurnService = new AICompanyTurnService(
                _worldEconomy,
                market,
                maxActionsPerCompany: simulationSettings != null
                    ? simulationSettings.AIActionsPerCompany
                    : 2);
            _simulation = new SimulationEngine(
                orchestrator,
                market,
                BuildTurnFlows,
                rules,
                new TurnNumber(1),
                new GameDay(0),
                aiResolution: aiTurnService,
                campaignSession: _campaignSession);
            BuildRealtimeClock();
        }

        private void BuildRealtimeClock()
        {
            _realtimeClock = new RealtimeSimulationClock(
                simulationSettings != null
                    ? simulationSettings.RealSecondsPerGameDay
                    : 60d,
                simulationSettings != null
                    ? simulationSettings.FixedRealtimeStepSeconds
                    : 0.1d,
                simulationSettings != null
                    ? simulationSettings.MaxRealtimeStepsPerFrame
                    : 16,
                simulationSettings != null
                    ? simulationSettings.InitialGameSpeed
                    : 1);
            _lastRealtimeMinuteStamp = 0;
        }

        private void NotifyRealtimeStateWhenMinuteChanged()
        {
            int minuteStamp =
                (RealtimeDayNumber - 1) * 24 * 60 +
                RealtimeHour * 60 +
                RealtimeMinute;
            if (minuteStamp == _lastRealtimeMinuteStamp)
                return;

            _lastRealtimeMinuteStamp = minuteStamp;
            RealtimeStateChanged?.Invoke();
        }

        private void LoadRuntimeSettings()
        {
            if (simulationSettings == null)
            {
                SimulationSettingsAsset[] settings =
                    UnityEngine.Resources.LoadAll<SimulationSettingsAsset>(
                        string.Empty);
                if (settings.Length > 0)
                    simulationSettings = settings[0];
            }

            if (militaryBalance == null)
            {
                MilitaryBalanceAsset[] balances =
                    UnityEngine.Resources.LoadAll<MilitaryBalanceAsset>(
                        string.Empty);
                if (balances.Length > 0)
                    militaryBalance = balances[0];
            }
        }

        private void RegisterFallbackResources()
        {
            RegisterFallbackResource(
                "iron", "철광석", 100m, ResourceRarity.Common, false);
            RegisterFallbackResource(
                "coal", "석탄", 80m, ResourceRarity.Common, false);
            RegisterFallbackResource(
                "wood", "목재", 60m, ResourceRarity.Common, false);
            RegisterFallbackResource(
                "food", "식량", 40m, ResourceRarity.Common, true);
            RegisterFallbackResource(
                "steel", "강철", 220m, ResourceRarity.Uncommon, false);
            RegisterFallbackResource(
                "machinery", "기계", 600m, ResourceRarity.Rare, false);
            RegisterFallbackResource(
                "medicine", "의약품", 450m, ResourceRarity.Rare, true);
            RegisterFallbackResource(
                "semiconductor", "반도체", 1200m,
                ResourceRarity.Strategic, false);
        }

        private void RegisterFallbackResource(
            string id,
            string displayName,
            decimal basePrice,
            ResourceRarity rarity,
            bool isPerishable)
        {
            _catalog.Register(new ResourceDefinition(
                new ResourceId(id),
                displayName,
                basePrice,
                rarity,
                storageVolume: 1m,
                isPerishable: isPerishable));
        }

        private CampaignSession BuildCampaign(
            CampaignRuleSet campaignRules)
        {
            var participants = new List<CampaignParticipantState>(
                aiCompanyCount + 1);

            decimal startingCash = (decimal)initialCompanyCash;
            var playerCompany = new Company(
                new CompanyId("player"),
                playerCompanyName,
                startingCash);
            _playerCampaignState = new CampaignParticipantState(
                playerCompany,
                true);
            participants.Add(_playerCampaignState);

            for (int i = 0; i < aiCompanyCount; i++)
            {
                var aiCompany = new Company(
                    new CompanyId($"ai_{i + 1}"),
                    $"경쟁 기업 {i + 1}",
                    startingCash);
                participants.Add(new CampaignParticipantState(
                    aiCompany,
                    false));
            }

            return new CampaignSession(
                new CampaignState(participants),
                new CampaignVictoryEvaluator(campaignRules));
        }

        private WorldEconomyTurnService BuildWorldEconomy(
            CampaignRuleSet campaignRules)
        {
            _worldEconomy = new WorldEconomyState();
            var availableResources = new List<ResourceId>();
            foreach (ResourceDefinition definition in _catalog.GetAll())
                availableResources.Add(definition.Id);

            WorldGenerationSettings generationSettings =
                simulationSettings != null
                    ? simulationSettings.CreateWorldGenerationSettings()
                    : new WorldGenerationSettings();
            int seed = simulationSettings != null
                ? simulationSettings.WorldSeed
                : 12345;
            ProceduralWorldState generatedWorld =
                new ProceduralWorldGenerator().Generate(
                    seed,
                    regionId,
                    generationSettings,
                    availableResources);
            _autonomousWorldState = new AutonomousWorldState(
                generatedWorld,
                new PlayerCharacterState(
                    "player_agent",
                    "플레이어",
                    _playerCampaignState.Company.Id,
                    generatedWorld.Regions[0].Id));

            decimal defaultStock = simulationSettings != null
                ? simulationSettings.InitialMarketStock
                : (decimal)initialMarketStock;
            for (int i = 0; i < generatedWorld.EconomySeeds.Count; i++)
            {
                RegionalEconomySeed economy =
                    generatedWorld.EconomySeeds[i];
                if (!_catalog.TryGet(economy.ResourceId, out var definition))
                    continue;

                decimal baselineSupply = definition.Id.Value == "food"
                    ? 22m
                    : 14m;
                decimal baselineDemand = definition.Id.Value == "food"
                    ? 32m
                    : 18m;
                decimal initialPrice = definition.BasePrice * Math.Clamp(
                    economy.DemandMultiplier /
                    Math.Max(0.10m, economy.SupplyMultiplier),
                    0.75m,
                    1.35m);
                var marketState = new ResourceMarketState(
                    definition.Id,
                    initialPrice,
                    defaultStock * economy.StockMultiplier);
                _worldEconomy.RegisterMarket(new MarketRuntimeState(
                    economy.RegionId,
                    definition,
                    marketState,
                    baselineSupply * economy.SupplyMultiplier,
                    baselineDemand * economy.DemandMultiplier));
            }

            RegisterGeneratedTradeRoutes(generatedWorld);

            RecipeDefinition starterRecipe = CreateStarterRecipe();

            for (int i = 0;
                i < _campaignSession.State.Participants.Count;
                i++)
            {
                CampaignParticipantState participant =
                    _campaignSession.State.Participants[i];
                RegionId companyRegion = generatedWorld.Regions[
                    i % generatedWorld.Regions.Count].Id;
                var warehouse = new Warehouse(
                    new WarehouseId($"warehouse_{participant.Company.Id.Value}"),
                    participant.Company.Id,
                    companyRegion,
                    (decimal)starterWarehouseCapacity);

                AddStarterInventory(
                    warehouse,
                    "iron",
                    (decimal)starterIronStock);
                AddStarterInventory(
                    warehouse,
                    "coal",
                    (decimal)starterCoalStock);
                AddStarterInventory(
                    warehouse,
                    "food",
                    (decimal)starterFoodStock);

                var runtime = new CompanyEconomyRuntime(
                    participant,
                    warehouse,
                    employeeCount: starterEmployees,
                    availableWorkers: (decimal)starterWorkers,
                    availablePower: (decimal)starterPower,
                    vehicleCount: starterVehicles);

                if (starterRecipe != null)
                {
                    runtime.AddFactory(new Factory(
                        new FactoryId(
                            $"factory_{participant.Company.Id.Value}"),
                        participant.Company.Id,
                        companyRegion,
                        starterRecipe));
                }

                _worldEconomy.RegisterCompany(runtime);
            }

            var operatingCosts = simulationSettings != null
                ? simulationSettings.CreateOperatingCostPolicy()
                : new OperatingCostPolicy(
                    100m,
                    30m,
                    20m,
                    5m,
                    0.001m,
                    100000m);

            MilitaryBalanceCatalog balance = militaryBalance != null
                ? militaryBalance.ToDomain()
                : MilitaryBalanceCatalog.CreatePrototypeDefaults();
            _autonomousWorldService =
                new AutonomousWorldSimulationService(
                    _autonomousWorldState,
                    _worldEconomy,
                    simulationSettings != null
                        ? simulationSettings.CreateAutonomousWorldTuning()
                        : new AutonomousWorldTuning(),
                    balance);

            return new WorldEconomyTurnService(
                _worldEconomy,
                new WorldEconomyTuning(
                    operatingCosts,
                    simulationSettings != null
                        ? simulationSettings.FactoryAssetValue
                        : 10000m,
                    simulationSettings != null
                        ? simulationSettings.WarehouseAssetValue
                        : 5000m,
                    simulationSettings != null
                        ? simulationSettings.VehicleAssetValue
                        : 2000m),
                campaignRules,
                simulationSettings != null
                    ? simulationSettings.CreateResourceSiteEventSettings()
                    : new ResourceSiteEventSettings(),
                _autonomousWorldService);
        }

        private void RegisterGeneratedTradeRoutes(
            ProceduralWorldState world)
        {
            for (int i = 0; i < world.Regions.Count; i++)
            {
                RegionId origin = world.Regions[i].Id;
                RegionId destination = world.Regions[
                    (i + 1) % world.Regions.Count].Id;
                _worldEconomy.RegisterRoute(new TradeRoute(
                    $"route_{origin.Value}_{destination.Value}",
                    origin,
                    destination,
                    travelDays: 1 + i % 3,
                    dailyCapacity: 250m,
                    baseLossRate: 0.01m + i % 3 * 0.01m,
                    tollPerUnit: 0.25m));
            }
        }

        private RecipeDefinition CreateStarterRecipe()
        {
            if (recipeAssets != null)
            {
                for (int i = 0; i < recipeAssets.Length; i++)
                {
                    if (recipeAssets[i] != null)
                        return recipeAssets[i].ToDomain();
                }
            }

            if (!_catalog.TryGet("iron", out _) ||
                !_catalog.TryGet("coal", out _) ||
                !_catalog.TryGet("steel", out _))
            {
                return null;
            }

            return new RecipeDefinition(
                "steel_recipe",
                new[]
                {
                    new ResourceAmount("iron", 2m),
                    new ResourceAmount("coal", 1m)
                },
                new[]
                {
                    new ResourceAmount("steel", 1m)
                },
                10m,
                5m,
                1,
                "강철 생산");
        }

        private void AddStarterInventory(
            Warehouse warehouse,
            ResourceId resourceId,
            decimal amount)
        {
            if (_catalog.TryGet(resourceId, out var definition))
            {
                warehouse.TryAdd(
                    resourceId,
                    amount,
                    definition.StorageVolume);
            }
        }

        private IReadOnlyList<PhysicalFlow> BuildTurnFlows(
            TurnNumber turn)
        {
            // 실제 흐름은 WorldEconomyTurnService.PrepareTurn에서 생산,
            // 자원지, 군대, 이벤트를 모두 합산한다. 이 공급자는 월드
            // 서비스가 없는 테스트 구성과 인터페이스 호환을 위해 둔다.
            _turnFlowBuffer.Clear();
            return _turnFlowBuffer;
        }
    }
}
