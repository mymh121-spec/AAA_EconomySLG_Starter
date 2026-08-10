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
using Game.Domain.Production;
using Game.Domain.Resources;

namespace Game.Presentation
{
    public sealed class SimulationBootstrapper : MonoBehaviour
    {
        [SerializeField] private ResourceDefinitionAsset[] resourceAssets;
        [SerializeField] private RecipeDefinitionAsset[] recipeAssets;
        [SerializeField] private SimulationSettingsAsset simulationSettings;
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
        private readonly Dictionary<ResourceId, ResourceMarketState> _states =
            new Dictionary<ResourceId, ResourceMarketState>();
        private readonly List<PhysicalFlow> _turnFlowBuffer =
            new List<PhysicalFlow>(16);
        private ResourceCatalog _catalog;
        private CampaignParticipantState _playerCampaignState;
        private CampaignSession _campaignSession;
        private CampaignRuleSet _campaignRules;
        private WorldEconomyState _worldEconomy;

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
        public int MaxCampaignTurns => simulationSettings != null
            ? simulationSettings.MaxCampaignTurns
            : 30;

        private void Awake()
        {
            BuildSimulation();
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
            _campaignRules = null;
            _turnFlowBuffer.Clear();
            BuildSimulation();
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
            _catalog = new ResourceCatalog();
            _states.Clear();

            ResourceDefinitionAsset[] configuredResources = resourceAssets;
            if (configuredResources == null || configuredResources.Length == 0)
            {
                configuredResources =
                    UnityEngine.Resources.LoadAll<ResourceDefinitionAsset>(
                        string.Empty);
            }

            int resourceCount = configuredResources?.Length ?? 0;
            for (int i = 0; i < resourceCount; i++)
            {
                var asset = configuredResources[i];
                if (asset == null)
                    continue;

                var definition = asset.ToDomain();
                _catalog.Register(definition);
                decimal stock = simulationSettings != null
                    ? simulationSettings.InitialMarketStock
                    : (decimal)initialMarketStock;

                _states[definition.Id] = new ResourceMarketState(
                    definition.Id,
                    definition.BasePrice,
                    stock);
            }

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
            var region = new RegionId(regionId);

            foreach (var definition in _catalog.GetAll())
            {
                decimal baseDemand = definition.Id.Value == "food"
                    ? 100m
                    : 60m;
                decimal baseSupply = definition.Id.Value == "iron"
                    ? 80m
                    : 70m;

                _worldEconomy.RegisterMarket(new MarketRuntimeState(
                    region,
                    definition,
                    _states[definition.Id],
                    baseSupply,
                    baseDemand));
            }

            RecipeDefinition starterRecipe = CreateStarterRecipe();

            for (int i = 0;
                i < _campaignSession.State.Participants.Count;
                i++)
            {
                CampaignParticipantState participant =
                    _campaignSession.State.Participants[i];
                var warehouse = new Warehouse(
                    new WarehouseId($"warehouse_{participant.Company.Id.Value}"),
                    participant.Company.Id,
                    region,
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
                        region,
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
                    : new ResourceSiteEventSettings());
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
            _turnFlowBuffer.Clear();
            var region = new RegionId(regionId);

            foreach (var definition in _catalog.GetAll())
            {
                var state = _states[definition.Id];
                decimal baseDemand = definition.Id.Value == "food"
                    ? 100m
                    : 60m;

                decimal baseSupply = definition.Id.Value == "iron"
                    ? 80m
                    : 70m;

                _turnFlowBuffer.Add(new PhysicalFlow(
                    region,
                    definition.Id,
                    definition,
                    state,
                    baseSupply,
                    baseDemand,
                    0));
            }

            return _turnFlowBuffer;
        }
    }
}
