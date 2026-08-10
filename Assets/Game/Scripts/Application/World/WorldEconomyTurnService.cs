using System;
using System.Collections.Generic;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Inventory;
using Game.Domain.Logistics;
using Game.Domain.Market;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Application.World
{
    public interface IWorldTurnService
    {
        IReadOnlyList<PhysicalFlow> PrepareTurn(
            TurnNumber turn,
            GameDay calendarDay);

        WorldTurnReport CompleteTurn(
            TurnNumber turn,
            GameDay calendarDay,
            MarketTickReport marketReport);
    }

    public sealed class WorldEconomyTuning
    {
        public OperatingCostPolicy OperatingCosts { get; }
        public decimal FactoryAssetValue { get; }
        public decimal WarehouseAssetValue { get; }
        public decimal VehicleAssetValue { get; }
        public decimal LogisticsRiskModifier { get; }

        public WorldEconomyTuning(
            OperatingCostPolicy operatingCosts,
            decimal factoryAssetValue = 10000m,
            decimal warehouseAssetValue = 5000m,
            decimal vehicleAssetValue = 2000m,
            decimal logisticsRiskModifier = 0m)
        {
            OperatingCosts = operatingCosts ??
                throw new ArgumentNullException(nameof(operatingCosts));
            FactoryAssetValue = Math.Max(0m, factoryAssetValue);
            WarehouseAssetValue = Math.Max(0m, warehouseAssetValue);
            VehicleAssetValue = Math.Max(0m, vehicleAssetValue);
            LogisticsRiskModifier = Math.Clamp(
                logisticsRiskModifier,
                0m,
                1m);
        }
    }

    public sealed class CompanyEconomyRuntime
    {
        private readonly List<Factory> _factories =
            new List<Factory>(8);

        public CampaignParticipantState CampaignState { get; }
        public Company Company => CampaignState.Company;
        public Warehouse PrimaryWarehouse { get; }
        public IReadOnlyList<Factory> Factories => _factories;
        public int VehicleCount { get; set; }
        public int EmployeeCount { get; set; }
        public decimal AvailableWorkers { get; set; }
        public decimal AvailablePower { get; set; }
        internal decimal StartingCash { get; set; }
        internal decimal StartingDebt { get; set; }
        internal int OperatingFactoryCountThisTurn { get; set; }

        public CompanyEconomyRuntime(
            CampaignParticipantState campaignState,
            Warehouse primaryWarehouse,
            int employeeCount,
            decimal availableWorkers,
            decimal availablePower,
            int vehicleCount = 0)
        {
            CampaignState = campaignState ??
                throw new ArgumentNullException(nameof(campaignState));
            PrimaryWarehouse = primaryWarehouse ??
                throw new ArgumentNullException(nameof(primaryWarehouse));

            if (!PrimaryWarehouse.OwnerId.Equals(Company.Id))
            {
                throw new ArgumentException(
                    "창고 소유 회사가 일치하지 않습니다.",
                    nameof(primaryWarehouse));
            }

            EmployeeCount = Math.Max(0, employeeCount);
            AvailableWorkers = Math.Max(0m, availableWorkers);
            AvailablePower = Math.Max(0m, availablePower);
            VehicleCount = Math.Max(0, vehicleCount);
            Company.RegisterWarehouse(PrimaryWarehouse.Id);
        }

        public void AddFactory(Factory factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));
            if (!factory.OwnerId.Equals(Company.Id))
            {
                throw new ArgumentException(
                    "공장 소유 회사가 일치하지 않습니다.",
                    nameof(factory));
            }

            _factories.Add(factory);
            Company.RegisterFactory(factory.Id);
        }
    }

    public sealed class MarketRuntimeState
    {
        public RegionId RegionId { get; }
        public ResourceDefinition Definition { get; }
        public ResourceMarketState MarketState { get; }
        public decimal BaseSupply { get; set; }
        public decimal BaseDemand { get; set; }
        public decimal BaseMarketStockChange { get; set; }

        public MarketRuntimeState(
            RegionId regionId,
            ResourceDefinition definition,
            ResourceMarketState marketState,
            decimal baseSupply,
            decimal baseDemand,
            decimal baseMarketStockChange = 0m)
        {
            RegionId = regionId;
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            MarketState = marketState ??
                throw new ArgumentNullException(nameof(marketState));
            BaseSupply = Math.Max(0m, baseSupply);
            BaseDemand = Math.Max(0m, baseDemand);
            BaseMarketStockChange = baseMarketStockChange;
        }
    }

    public sealed class WorldEconomyState
    {
        private readonly Dictionary<CompanyId, CompanyEconomyRuntime>
            _companies =
                new Dictionary<CompanyId, CompanyEconomyRuntime>();
        private readonly List<CompanyEconomyRuntime> _companyList =
            new List<CompanyEconomyRuntime>(16);
        private readonly Dictionary<(RegionId, ResourceId), MarketRuntimeState>
            _markets =
                new Dictionary<(RegionId, ResourceId), MarketRuntimeState>();
        private readonly List<MarketRuntimeState> _marketList =
            new List<MarketRuntimeState>(64);
        private readonly List<TradeRoute> _routes =
            new List<TradeRoute>(16);
        private readonly Dictionary<string, ResourceExtractionSite>
            _resourceSitesById =
                new Dictionary<string, ResourceExtractionSite>(
                    StringComparer.Ordinal);
        private readonly List<ResourceExtractionSite> _resourceSites =
            new List<ResourceExtractionSite>(16);

        public IReadOnlyList<CompanyEconomyRuntime> Companies =>
            _companyList;
        public IReadOnlyList<MarketRuntimeState> Markets =>
            _marketList;
        public IReadOnlyList<TradeRoute> Routes => _routes;
        public IReadOnlyList<ResourceExtractionSite> ResourceSites =>
            _resourceSites;
        public LogisticsService Logistics { get; } =
            new LogisticsService();

        public void RegisterCompany(CompanyEconomyRuntime company)
        {
            if (company == null)
                throw new ArgumentNullException(nameof(company));

            _companies[company.Company.Id] = company;
            _companyList.Add(company);
        }

        public void RegisterMarket(MarketRuntimeState market)
        {
            if (market == null)
                throw new ArgumentNullException(nameof(market));

            var key = (market.RegionId, market.Definition.Id);
            _markets[key] = market;
            _marketList.Add(market);
        }

        public void RegisterRoute(TradeRoute route)
        {
            if (route == null)
                throw new ArgumentNullException(nameof(route));

            _routes.Add(route);
        }

        public bool RegisterResourceSite(ResourceExtractionSite site)
        {
            if (site == null)
                throw new ArgumentNullException(nameof(site));
            if (_resourceSitesById.ContainsKey(site.Id))
                return false;

            _resourceSitesById.Add(site.Id, site);
            _resourceSites.Add(site);
            return true;
        }

        public bool TryGetCompany(
            CompanyId companyId,
            out CompanyEconomyRuntime company)
        {
            return _companies.TryGetValue(companyId, out company);
        }

        public bool TryGetMarket(
            RegionId regionId,
            ResourceId resourceId,
            out MarketRuntimeState market)
        {
            return _markets.TryGetValue(
                (regionId, resourceId),
                out market);
        }
    }

    public readonly struct FactoryTurnRecord
    {
        public FactoryId FactoryId { get; }
        public CompanyId CompanyId { get; }
        public bool Produced { get; }
        public FactoryStatus Status { get; }
        public decimal Efficiency { get; }

        public FactoryTurnRecord(
            Factory factory,
            ProductionResult result)
        {
            FactoryId = factory.Id;
            CompanyId = factory.OwnerId;
            Produced = result.Produced;
            Status = result.Status;
            Efficiency = result.Efficiency;
        }
    }

    public readonly struct TradeSettlementRecord
    {
        public TradeFill Fill { get; }
        public bool Settled { get; }
        public string Message { get; }

        public TradeSettlementRecord(
            TradeFill fill,
            bool settled,
            string message)
        {
            Fill = fill;
            Settled = settled;
            Message = message;
        }
    }

    public readonly struct CompanyFinanceTurnRecord
    {
        public CompanyId CompanyId { get; }
        public decimal OperatingProfit { get; }
        public DailyFinanceResult FinanceResult { get; }
        public decimal EconomicPower { get; }

        public CompanyFinanceTurnRecord(
            CompanyId companyId,
            decimal operatingProfit,
            DailyFinanceResult financeResult,
            decimal economicPower)
        {
            CompanyId = companyId;
            OperatingProfit = operatingProfit;
            FinanceResult = financeResult;
            EconomicPower = economicPower;
        }
    }

    public sealed class WorldTurnReport
    {
        public IReadOnlyList<FactoryTurnRecord> Production { get; }
        public IReadOnlyList<ShipmentArrival> Arrivals { get; }
        public IReadOnlyList<TradeSettlementRecord> Trades { get; }
        public IReadOnlyList<CompanyFinanceTurnRecord> Finances { get; }
        public ResourceSiteTurnReport ResourceSites { get; }
        public AutonomousWorldTurnReport AutonomousWorld { get; }

        public WorldTurnReport(
            IReadOnlyList<FactoryTurnRecord> production,
            IReadOnlyList<ShipmentArrival> arrivals,
            IReadOnlyList<TradeSettlementRecord> trades,
            IReadOnlyList<CompanyFinanceTurnRecord> finances,
            ResourceSiteTurnReport resourceSites = null,
            AutonomousWorldTurnReport autonomousWorld = null)
        {
            Production = production ?? Array.Empty<FactoryTurnRecord>();
            Arrivals = arrivals ?? Array.Empty<ShipmentArrival>();
            Trades = trades ?? Array.Empty<TradeSettlementRecord>();
            Finances = finances ??
                Array.Empty<CompanyFinanceTurnRecord>();
            ResourceSites = resourceSites ?? ResourceSiteTurnReport.Empty;
            AutonomousWorld = autonomousWorld ??
                AutonomousWorldTurnReport.Empty;
        }
    }

    public sealed class WorldEconomyTurnService : IWorldTurnService
    {
        private sealed class FlowAccumulator
        {
            public MarketRuntimeState Market;
            public decimal Supply;
            public decimal Demand;
            public decimal StockChange;
        }

        private readonly WorldEconomyState _world;
        private readonly WorldEconomyTuning _tuning;
        private readonly CompanyFinanceSystem _finance =
            new CompanyFinanceSystem();
        private readonly EconomicPowerCalculator _powerCalculator =
            new EconomicPowerCalculator();
        private readonly CampaignRuleSet _campaignRules;
        private readonly ResourceSiteEventSystem _resourceSiteEvents;
        private readonly IAutonomousWorldTurnService _autonomousWorld;
        private readonly Dictionary<(RegionId, ResourceId), FlowAccumulator>
            _flowByMarket =
                new Dictionary<(RegionId, ResourceId), FlowAccumulator>();
        private readonly List<PhysicalFlow> _flowBuffer =
            new List<PhysicalFlow>(64);
        private readonly List<FactoryTurnRecord> _productionBuffer =
            new List<FactoryTurnRecord>(128);
        private readonly List<ShipmentArrival> _arrivalBuffer =
            new List<ShipmentArrival>(128);
        private readonly List<TradeSettlementRecord> _tradeBuffer =
            new List<TradeSettlementRecord>(128);
        private readonly List<CompanyFinanceTurnRecord> _financeBuffer =
            new List<CompanyFinanceTurnRecord>(32);
        private bool _turnPrepared;
        private ResourceSiteTurnReport _resourceSiteReport =
            ResourceSiteTurnReport.Empty;
        private AutonomousWorldTurnReport _autonomousWorldReport =
            AutonomousWorldTurnReport.Empty;

        public WorldEconomyTurnService(
            WorldEconomyState world,
            WorldEconomyTuning tuning,
            CampaignRuleSet campaignRules,
            ResourceSiteEventSettings resourceSiteSettings = null,
            IAutonomousWorldTurnService autonomousWorld = null)
        {
            _world = world ??
                throw new ArgumentNullException(nameof(world));
            _tuning = tuning ??
                throw new ArgumentNullException(nameof(tuning));
            _campaignRules = campaignRules ??
                throw new ArgumentNullException(nameof(campaignRules));
            _resourceSiteEvents = new ResourceSiteEventSystem(
                _world,
                resourceSiteSettings ?? new ResourceSiteEventSettings());
            _autonomousWorld = autonomousWorld;
        }

        public IReadOnlyList<PhysicalFlow> PrepareTurn(
            TurnNumber turn,
            GameDay calendarDay)
        {
            if (_turnPrepared)
            {
                throw new InvalidOperationException(
                    "이전 세계 턴 정산이 완료되지 않았습니다.");
            }

            _turnPrepared = true;
            _flowBuffer.Clear();
            _productionBuffer.Clear();
            _arrivalBuffer.Clear();
            _tradeBuffer.Clear();
            _financeBuffer.Clear();
            BuildBaseFlows();
            _autonomousWorldReport = _autonomousWorld?.PrepareTurn(
                turn,
                calendarDay) ?? AutonomousWorldTurnReport.Empty;
            AddAutonomousWorldFlows(_autonomousWorldReport);
            _resourceSiteReport = _resourceSiteEvents.ProcessTurn(turn);
            _autonomousWorld?.SynchronizeResourceSites();
            AddResourceSiteSupply(_resourceSiteReport);

            for (int i = 0; i < _world.Companies.Count; i++)
            {
                var company = _world.Companies[i];
                company.StartingCash = company.Company.Cash;
                company.StartingDebt = company.Company.Debt;
                company.OperatingFactoryCountThisTurn = 0;

                if (company.CampaignState.IsEliminated)
                    continue;

                ProduceCompany(company);
            }

            AdvanceLogistics();
            BuildPhysicalFlowBuffer();
            return _flowBuffer;
        }

        public WorldTurnReport CompleteTurn(
            TurnNumber turn,
            GameDay calendarDay,
            MarketTickReport marketReport)
        {
            if (!_turnPrepared)
            {
                throw new InvalidOperationException(
                    "세계 턴 준비 단계가 실행되지 않았습니다.");
            }
            if (marketReport == null)
                throw new ArgumentNullException(nameof(marketReport));

            for (int i = 0; i < marketReport.Fills.Count; i++)
                SettleTrade(marketReport.Fills[i]);

            for (int i = 0; i < _world.Companies.Count; i++)
                SettleCompanyFinance(_world.Companies[i]);

            for (int i = 0; i < _world.Routes.Count; i++)
                _world.Routes[i].BeginDay();

            _autonomousWorld?.CompleteTurn(
                turn,
                calendarDay,
                marketReport);

            _turnPrepared = false;

            return new WorldTurnReport(
                new List<FactoryTurnRecord>(_productionBuffer),
                new List<ShipmentArrival>(_arrivalBuffer),
                new List<TradeSettlementRecord>(_tradeBuffer),
                new List<CompanyFinanceTurnRecord>(_financeBuffer),
                _resourceSiteReport,
                _autonomousWorldReport);
        }

        private void AddAutonomousWorldFlows(
            AutonomousWorldTurnReport report)
        {
            for (int i = 0; i < report.Flows.Count; i++)
            {
                WorldFlowContribution contribution = report.Flows[i];
                if (!_flowByMarket.TryGetValue(
                    (contribution.RegionId, contribution.ResourceId),
                    out var flow))
                {
                    continue;
                }

                flow.Supply += contribution.Supply;
                flow.Demand += contribution.Demand;
                flow.StockChange += contribution.MarketStockChange;
            }
        }

        private void AddResourceSiteSupply(
            ResourceSiteTurnReport report)
        {
            for (int i = 0; i < report.Production.Count; i++)
            {
                ResourceSiteProductionRecord production =
                    report.Production[i];
                AddSupply(
                    production.RegionId,
                    production.ResourceId,
                    production.Output);
            }
        }

        private void BuildBaseFlows()
        {
            _flowByMarket.Clear();

            for (int i = 0; i < _world.Markets.Count; i++)
            {
                var market = _world.Markets[i];
                _flowByMarket[(market.RegionId, market.Definition.Id)] =
                    new FlowAccumulator
                    {
                        Market = market,
                        Supply = market.BaseSupply,
                        Demand = market.BaseDemand,
                        StockChange = market.BaseMarketStockChange
                    };
            }
        }

        private void ProduceCompany(CompanyEconomyRuntime company)
        {
            var context = new ProductionContext(
                company.PrimaryWarehouse,
                company.PrimaryWarehouse,
                company.AvailableWorkers,
                company.AvailablePower);

            for (int i = 0; i < company.Factories.Count; i++)
            {
                Factory factory = company.Factories[i];
                ProductionResult result = factory.Produce(context);
                _productionBuffer.Add(new FactoryTurnRecord(
                    factory,
                    result));

                if (!result.Produced)
                    continue;

                company.OperatingFactoryCountThisTurn++;

                decimal cycles = factory.Efficiency /
                    factory.Recipe.DaysPerCycle;

                for (int j = 0; j < factory.Recipe.Inputs.Count; j++)
                {
                    ResourceAmount input = factory.Recipe.Inputs[j];
                    AddDemand(
                        factory.RegionId,
                        input.ResourceId,
                        input.Amount * cycles);
                }

                for (int j = 0; j < factory.Recipe.Outputs.Count; j++)
                {
                    ResourceAmount output = factory.Recipe.Outputs[j];
                    AddSupply(
                        factory.RegionId,
                        output.ResourceId,
                        output.Amount * cycles);
                }
            }
        }

        private void AdvanceLogistics()
        {
            _world.Logistics.AdvanceDay(
                _tuning.LogisticsRiskModifier,
                _arrivalBuffer);

            for (int i = 0; i < _arrivalBuffer.Count; i++)
            {
                ShipmentArrival arrival = _arrivalBuffer[i];
                if (!_world.TryGetCompany(
                    arrival.OwnerId,
                    out var company))
                {
                    continue;
                }

                decimal unitVolume = 1m;
                if (_world.TryGetMarket(
                    arrival.Destination,
                    arrival.ResourceId,
                    out var market))
                {
                    unitVolume = market.Definition.StorageVolume;
                }

                company.PrimaryWarehouse.TryAdd(
                    arrival.ResourceId,
                    arrival.Quantity,
                    unitVolume);
            }
        }

        private void BuildPhysicalFlowBuffer()
        {
            foreach (var pair in _flowByMarket)
            {
                FlowAccumulator flow = pair.Value;
                _flowBuffer.Add(new PhysicalFlow(
                    flow.Market.RegionId,
                    flow.Market.Definition.Id,
                    flow.Market.Definition,
                    flow.Market.MarketState,
                    flow.Supply,
                    flow.Demand,
                    flow.StockChange));
            }
        }

        private void AddSupply(
            RegionId regionId,
            ResourceId resourceId,
            decimal amount)
        {
            if (_flowByMarket.TryGetValue(
                (regionId, resourceId),
                out var flow))
            {
                flow.Supply += Math.Max(0m, amount);
            }
        }

        private void AddDemand(
            RegionId regionId,
            ResourceId resourceId,
            decimal amount)
        {
            if (_flowByMarket.TryGetValue(
                (regionId, resourceId),
                out var flow))
            {
                flow.Demand += Math.Max(0m, amount);
            }
        }

        private void SettleTrade(TradeFill fill)
        {
            if (fill.BuyerId.Equals(fill.SellerId))
            {
                AddFailedTrade(fill, "자기 회사와의 거래는 정산하지 않습니다.");
                return;
            }

            if (!_world.TryGetCompany(fill.BuyerId, out var buyer) ||
                !_world.TryGetCompany(fill.SellerId, out var seller))
            {
                AddFailedTrade(fill, "거래 회사를 찾을 수 없습니다.");
                return;
            }

            if (buyer.CampaignState.IsEliminated ||
                seller.CampaignState.IsEliminated)
            {
                AddFailedTrade(fill, "제거된 회사가 포함된 거래입니다.");
                return;
            }

            if (!_world.TryGetMarket(
                fill.RegionId,
                fill.ResourceId,
                out var market))
            {
                AddFailedTrade(fill, "거래 자원 시장을 찾을 수 없습니다.");
                return;
            }

            if (!buyer.Company.CanAfford(fill.TotalPrice))
            {
                AddFailedTrade(fill, "구매 회사의 현금이 부족합니다.");
                return;
            }

            if (seller.PrimaryWarehouse.GetAvailable(fill.ResourceId) <
                fill.Quantity)
            {
                AddFailedTrade(fill, "판매 회사의 재고가 부족합니다.");
                return;
            }

            if (!buyer.PrimaryWarehouse.CanAdd(
                fill.ResourceId,
                fill.Quantity,
                market.Definition.StorageVolume))
            {
                AddFailedTrade(fill, "구매 회사의 창고 용량이 부족합니다.");
                return;
            }

            seller.PrimaryWarehouse.TryReserve(
                fill.ResourceId,
                fill.Quantity);

            if (!buyer.Company.TrySpend(fill.TotalPrice))
            {
                seller.PrimaryWarehouse.ReleaseReservation(
                    fill.ResourceId,
                    fill.Quantity);
                AddFailedTrade(fill, "거래 대금 결제에 실패했습니다.");
                return;
            }

            seller.PrimaryWarehouse.ConsumeReserved(
                fill.ResourceId,
                fill.Quantity);
            seller.Company.Receive(fill.TotalPrice);
            buyer.PrimaryWarehouse.TryAdd(
                fill.ResourceId,
                fill.Quantity,
                market.Definition.StorageVolume);

            _tradeBuffer.Add(new TradeSettlementRecord(
                fill,
                true,
                "거래 정산 완료"));
        }

        private void AddFailedTrade(
            TradeFill fill,
            string message)
        {
            _tradeBuffer.Add(new TradeSettlementRecord(
                fill,
                false,
                message));
        }

        private void SettleCompanyFinance(
            CompanyEconomyRuntime runtime)
        {
            if (runtime.CampaignState.IsEliminated)
                return;

            var counts = new DailyOperatingCosts(
                runtime.OperatingFactoryCountThisTurn,
                1,
                runtime.VehicleCount,
                runtime.EmployeeCount);
            DailyFinanceResult result = _finance.ProcessDay(
                runtime.Company,
                counts,
                _tuning.OperatingCosts);

            decimal debtIncrease =
                runtime.Company.Debt - runtime.StartingDebt;
            decimal operatingProfit =
                runtime.Company.Cash - runtime.StartingCash -
                debtIncrease;
            runtime.CampaignState.RecordOperatingProfit(
                operatingProfit);

            UpdateEconomicAssets(runtime);
            decimal economicPower = _powerCalculator.Calculate(
                runtime.CampaignState,
                _campaignRules);

            _financeBuffer.Add(new CompanyFinanceTurnRecord(
                runtime.Company.Id,
                operatingProfit,
                result,
                economicPower));
        }

        private void UpdateEconomicAssets(
            CompanyEconomyRuntime runtime)
        {
            decimal inventoryValue = 0m;

            foreach (var stock in runtime.PrimaryWarehouse.Stocks)
            {
                if (_world.TryGetMarket(
                    runtime.PrimaryWarehouse.RegionId,
                    stock.Key,
                    out var market))
                {
                    inventoryValue +=
                        stock.Value.OnHand *
                        market.MarketState.CurrentPrice;
                }
            }

            decimal facilityValue =
                runtime.Factories.Count * _tuning.FactoryAssetValue +
                _tuning.WarehouseAssetValue;
            decimal logisticsValue =
                runtime.VehicleCount * _tuning.VehicleAssetValue;

            runtime.CampaignState.UpdateAssetValues(
                inventoryValue,
                facilityValue,
                logisticsValue,
                runtime.CampaignState.TerritoryValue,
                runtime.CampaignState.TechnologyValue,
                runtime.CampaignState.UnpaidCosts);
        }
    }
}
