using System.Collections.Generic;
using NUnit.Framework;
using Game.Application;
using Game.Application.AI;
using Game.Application.Campaign;
using Game.Application.PvP;
using Game.Application.Session;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Logistics;
using Game.Domain.Market;
using Game.Domain.Military;
using Game.Domain.Inventory;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Technology;
using Game.Domain.World;

namespace Game.Tests
{
    public sealed class EconomyDomainTests
    {
        [Test]
        public void GridMapLayout_UsesFifteenByFifteenAndProtectsCompanyStarts()
        {
            var generator = new GridMapLayoutGenerator();
            var playerStart = new GridCoordinate(0, 0);
            var opponentStarts = new[]
            {
                new GridCoordinate(14, 14),
                new GridCoordinate(0, 14),
                new GridCoordinate(14, 0)
            };
            GridMapLayout layout = generator.Generate(
                15,
                28,
                12345,
                playerStart,
                opponentStarts);

            Assert.That(layout.Size, Is.EqualTo(15));
            Assert.That(layout.PlayerStart, Is.EqualTo(playerStart));
            Assert.That(layout.OpponentStarts.Count, Is.EqualTo(3));
            Assert.That(layout.Mines.Count, Is.EqualTo(28));

            var uniqueCoordinates = new HashSet<GridCoordinate>();
            for (int i = 0; i < layout.Mines.Count; i++)
            {
                Assert.That(
                    layout.Mines[i].Coordinate,
                    Is.Not.EqualTo(playerStart));
                for (int opponentIndex = 0;
                     opponentIndex < opponentStarts.Length;
                     opponentIndex++)
                {
                    Assert.That(
                        layout.Mines[i].Coordinate,
                        Is.Not.EqualTo(opponentStarts[opponentIndex]));
                }
                Assert.That(
                    uniqueCoordinates.Add(layout.Mines[i].Coordinate),
                    Is.True);
                Assert.That(
                    layout.Mines[i].Kind,
                    Is.AnyOf(MineKind.Normal, MineKind.Gold));
            }

            GridMapLayout allAvailableTiles = generator.Generate(
                15,
                221,
                54321,
                playerStart,
                opponentStarts);
            bool centerCanContainMine = false;
            for (int i = 0; i < allAvailableTiles.Mines.Count; i++)
            {
                if (allAvailableTiles.Mines[i].Coordinate.Equals(
                    new GridCoordinate(7, 7)))
                {
                    centerCanContainMine = true;
                    break;
                }
            }

            Assert.That(centerCanContainMine, Is.True);
        }

        [Test]
        public void GameModeSelection_AllowsOneModeUntilCleared()
        {
            var selection = new GameModeSelection();

            Assert.That(selection.HasSelection, Is.False);
            Assert.That(
                selection.TrySelect(GamePlayMode.SinglePlayer, out _),
                Is.True);
            Assert.That(selection.IsSinglePlayer, Is.True);
            Assert.That(
                selection.TrySelect(GamePlayMode.Multiplayer, out string reason),
                Is.False);
            Assert.That(reason, Is.Not.Empty);

            selection.Clear();

            Assert.That(
                selection.TrySelect(GamePlayMode.Multiplayer, out _),
                Is.True);
            Assert.That(selection.IsMultiplayer, Is.True);
        }

        [Test]
        public void GameModeSelection_RejectsNoneAsPlayableMode()
        {
            var selection = new GameModeSelection();

            bool selected = selection.TrySelect(
                GamePlayMode.None,
                out string reason);

            Assert.That(selected, Is.False);
            Assert.That(selection.HasSelection, Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void OrderBook_MatchesByPriceAndSupportsPartialFill()
        {
            var region = new RegionId("test");
            var resource = new ResourceId("iron");
            var book = new OrderBook();

            book.Add(new MarketOrder(
                "buy",
                new CompanyId("buyer"),
                region,
                resource,
                OrderSide.Buy,
                OrderPurpose.ProductionInput,
                10,
                120,
                1));

            book.Add(new MarketOrder(
                "sell",
                new CompanyId("seller"),
                region,
                resource,
                OrderSide.Sell,
                OrderPurpose.Export,
                4,
                100,
                1));

            var fills = new List<TradeFill>();
            book.Match(fills);

            Assert.That(fills.Count, Is.EqualTo(1));
            Assert.That(fills[0].Quantity, Is.EqualTo(4));
            Assert.That(fills[0].UnitPrice, Is.EqualTo(110));
        }

        [Test]
        public void PriceCalculator_LowersPriceWhenSupplyExceedsDemand()
        {
            var definition = new ResourceDefinition(
                new ResourceId("iron"),
                "철광석",
                100,
                ResourceRarity.Common,
                1,
                false);

            var state = new ResourceMarketState(
                definition.Id,
                100,
                100);

            var input = new PriceInput
            {
                PreviousPrice = 100,
                BasePrice = 100,
                EffectiveSupply = 200,
                EffectiveDemand = 100,
                EndingStock = 120,
                TargetStock = 100,
                RecentAverageVolume = 100,
                NetMarketAbsorption = 0
            };

            decimal result =
                new PriceCalculator().Calculate(
                    definition,
                    state,
                    input);

            Assert.That(result, Is.LessThan(100));
        }

        [Test]
        public void PriceCalculator_RespectsExactDailyChangeLimit()
        {
            var definition = new ResourceDefinition(
                new ResourceId("iron"),
                "철광석",
                100,
                ResourceRarity.Common,
                1,
                false);

            var state = new ResourceMarketState(
                definition.Id,
                100,
                0);

            var input = new PriceInput
            {
                PreviousPrice = 100,
                BasePrice = 100,
                EffectiveSupply = 0,
                EffectiveDemand = 100,
                EndingStock = 0,
                TargetStock = 1400,
                RecentAverageVolume = 100,
                NetMarketAbsorption = 100,
                MaxDailyChange = 0.15m
            };

            decimal result =
                new PriceCalculator().Calculate(
                    definition,
                    state,
                    input);

            Assert.That(result, Is.EqualTo(115));
        }

        [Test]
        public void Logistics_DeliversAfterTravelDays()
        {
            var route = new TradeRoute(
                "route",
                new RegionId("a"),
                new RegionId("b"),
                2,
                100,
                0.1m,
                2);

            var shipment = new Shipment(
                "shipment",
                new CompanyId("company"),
                new ResourceId("steel"),
                route,
                50);

            var service = new LogisticsService();
            var arrivals = new List<ShipmentArrival>();

            Assert.That(service.TryDispatch(route, shipment), Is.True);

            service.AdvanceDay(0, arrivals);
            Assert.That(arrivals.Count, Is.EqualTo(0));

            service.AdvanceDay(0, arrivals);
            Assert.That(arrivals.Count, Is.EqualTo(1));
            Assert.That(arrivals[0].Quantity, Is.EqualTo(45));
        }

        [Test]
        public void Technology_CompletesAtResearchCost()
        {
            var definition = new TechnologyDefinition(
                "advanced_steelmaking",
                "고급 제철 공정",
                100,
                System.Array.Empty<string>(),
                new[]
                {
                    new TechnologyEffect(
                        TechnologyEffectType.ProductionEfficiency,
                        0.1m)
                });

            var state = new TechnologyState();

            Assert.That(state.AddResearch(definition, 60), Is.False);
            Assert.That(state.AddResearch(definition, 40), Is.True);
            Assert.That(state.IsCompleted(definition.Id), Is.True);
        }

        [Test]
        public void FinanceSystem_ConvertsUnpaidCostIntoDebt()
        {
            var company = new Company(
                new CompanyId("company"),
                "테스트 회사",
                50);

            var policy = new OperatingCostPolicy(
                100,
                0,
                0,
                0,
                0,
                1000);

            var result = new CompanyFinanceSystem().ProcessDay(
                company,
                new DailyOperatingCosts(1, 0, 0, 0),
                policy);

            Assert.That(company.Cash, Is.EqualTo(0));
            Assert.That(company.Debt, Is.EqualTo(50));
            Assert.That(result.Bankrupt, Is.False);
        }

        [Test]
        public void TurnCommandQueue_RejectsOrderWhenActionPointsAreSpent()
        {
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var context = new TurnCommandContext(market);
            var queue = new TurnCommandQueue(1);
            var region = new RegionId("test");
            var resource = new ResourceId("iron");

            var first = new SubmitMarketOrderTurnCommand(
                new MarketOrder(
                    "first",
                    new CompanyId("player"),
                    region,
                    resource,
                    OrderSide.Buy,
                    OrderPurpose.ProductionInput,
                    10,
                    100,
                    1),
                "철광석 구매");

            var second = new SubmitMarketOrderTurnCommand(
                new MarketOrder(
                    "second",
                    new CompanyId("player"),
                    region,
                    resource,
                    OrderSide.Buy,
                    OrderPurpose.ProductionInput,
                    10,
                    100,
                    1),
                "추가 구매");

            Assert.That(
                queue.TryQueue(first, context, out _),
                Is.True);
            Assert.That(queue.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(
                queue.TryQueue(second, context, out var reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("남은 행동력이 부족합니다."));
        }

        [Test]
        public void SimulationEngine_EndTurnAdvancesTurnAndCalendar()
        {
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(5, 2),
                new TurnNumber(1),
                new GameDay(0));

            TurnReport report = engine.EndTurn();

            Assert.That(report.Turn.Value, Is.EqualTo(1));
            Assert.That(engine.CurrentTurn.Value, Is.EqualTo(2));
            Assert.That(engine.CurrentCalendarDay.Value, Is.EqualTo(2));
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.PlayerPlanning));
        }

        [Test]
        public void Campaign_DominanceCheckStartsAtTurn15AndWinsAtTurn16()
        {
            CampaignState state = CreateCampaign(
                300,
                50,
                50);
            var evaluator = new CampaignVictoryEvaluator(
                new CampaignRuleSet(
                    maxTurns: 30,
                    dominanceCheckStartTurn: 15,
                    dominanceMultiplier: 3,
                    dominanceRequiredConsecutiveTurns: 2));

            CampaignTurnResult turn14 = evaluator.Evaluate(
                new TurnNumber(14),
                state);
            CampaignTurnResult turn15 = evaluator.Evaluate(
                new TurnNumber(15),
                state);
            CampaignTurnResult turn16 = evaluator.Evaluate(
                new TurnNumber(16),
                state);

            Assert.That(turn14.Outcome, Is.EqualTo(CampaignOutcome.InProgress));
            Assert.That(turn14.DominanceConsecutiveTurns, Is.EqualTo(0));
            Assert.That(turn15.Outcome, Is.EqualTo(CampaignOutcome.InProgress));
            Assert.That(turn15.DominanceConsecutiveTurns, Is.EqualTo(1));
            Assert.That(turn16.Outcome, Is.EqualTo(CampaignOutcome.Victory));
            Assert.That(
                turn16.EndReason,
                Is.EqualTo(CampaignEndReason.EconomicDominance));
        }

        [Test]
        public void EconomicPower_IncludesAssetsProfitDebtAndUnpaidCosts()
        {
            var company = new Company(
                new CompanyId("player"),
                "플레이어 기업",
                100);
            company.AddDebt(30);
            var participant = new CampaignParticipantState(
                company,
                true);
            participant.UpdateAssetValues(
                inventoryValue: 10,
                facilityValue: 10,
                logisticsValue: 10,
                territoryValue: 10,
                technologyValue: 10,
                unpaidCosts: 20);
            participant.RecordOperatingProfit(20);

            decimal power = new EconomicPowerCalculator().Calculate(
                participant,
                new CampaignRuleSet(recentProfitMultiplier: 5));

            Assert.That(power, Is.EqualTo(200));
        }

        [Test]
        public void Campaign_DominanceStreakResetsWhenRatioDropsBelowThree()
        {
            CampaignState state = CreateCampaign(
                300,
                50,
                50);
            var evaluator = new CampaignVictoryEvaluator(
                new CampaignRuleSet());

            CampaignTurnResult turn15 = evaluator.Evaluate(
                new TurnNumber(15),
                state);
            state.Participants[1].Company.Receive(100);
            CampaignTurnResult turn16 = evaluator.Evaluate(
                new TurnNumber(16),
                state);

            Assert.That(turn15.DominanceConsecutiveTurns, Is.EqualTo(1));
            Assert.That(turn16.Outcome, Is.EqualTo(CampaignOutcome.InProgress));
            Assert.That(turn16.DominanceConsecutiveTurns, Is.EqualTo(0));
        }

        [Test]
        public void Campaign_BankruptcyAndCapitalDestructionAreImmediateDefeats()
        {
            CampaignState bankruptState = CreateCampaign(100, 100);
            bankruptState.Player.Company.MarkBankrupt();
            CampaignTurnResult bankruptResult =
                new CampaignVictoryEvaluator(new CampaignRuleSet())
                    .Evaluate(new TurnNumber(1), bankruptState);

            CampaignState capitalState = CreateCampaign(100, 100);
            capitalState.Player.DestroyCapital();
            CampaignTurnResult capitalResult =
                new CampaignVictoryEvaluator(new CampaignRuleSet())
                    .Evaluate(new TurnNumber(1), capitalState);

            Assert.That(
                bankruptResult.EndReason,
                Is.EqualTo(CampaignEndReason.Bankruptcy));
            Assert.That(
                capitalResult.EndReason,
                Is.EqualTo(CampaignEndReason.CapitalDestroyed));
            Assert.That(bankruptResult.Outcome, Is.EqualTo(CampaignOutcome.Defeat));
            Assert.That(capitalResult.Outcome, Is.EqualTo(CampaignOutcome.Defeat));
        }

        [Test]
        public void Campaign_Turn30AwardsVictoryToHighestEconomicPower()
        {
            CampaignState state = CreateCampaign(
                101,
                100,
                90);
            CampaignTurnResult result =
                new CampaignVictoryEvaluator(new CampaignRuleSet())
                    .Evaluate(new TurnNumber(30), state);

            Assert.That(result.Outcome, Is.EqualTo(CampaignOutcome.Victory));
            Assert.That(
                result.EndReason,
                Is.EqualTo(CampaignEndReason.TurnLimitVictory));
        }

        [Test]
        public void SimulationEngine_DoesNotAdvanceAfterCampaignDefeat()
        {
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            CampaignState state = CreateCampaign(100, 100);
            state.Player.DestroyCapital();
            var campaign = new CampaignSession(
                state,
                new CampaignVictoryEvaluator(new CampaignRuleSet()));
            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(),
                new TurnNumber(1),
                new GameDay(0),
                campaignSession: campaign);

            TurnReport report = engine.EndTurn();

            Assert.That(report.CampaignResult.IsFinished, Is.True);
            Assert.That(engine.CurrentTurn.Value, Is.EqualTo(1));
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.Completed));
            Assert.That(engine.IsCampaignFinished, Is.True);
        }

        [Test]
        public void FullTurn_ProductionFinanceAndEconomicPowerAreIntegrated()
        {
            var region = new RegionId("starter");
            var iron = CreateResource("iron", 100);
            var coal = CreateResource("coal", 80);
            var steel = CreateResource("steel", 220);
            CampaignState campaignState = CreateCampaign(1000, 1000);
            var world = new WorldEconomyState();

            RegisterMarket(world, region, iron, 80, 60);
            RegisterMarket(world, region, coal, 70, 60);
            RegisterMarket(world, region, steel, 70, 60);

            var playerWarehouse = new Warehouse(
                new WarehouseId("player_warehouse"),
                campaignState.Player.Company.Id,
                region,
                1000);
            playerWarehouse.TryAdd(iron.Id, 2, iron.StorageVolume);
            playerWarehouse.TryAdd(coal.Id, 1, coal.StorageVolume);

            var playerRuntime = new CompanyEconomyRuntime(
                campaignState.Player,
                playerWarehouse,
                0,
                10,
                5);
            playerRuntime.AddFactory(new Factory(
                new FactoryId("steel_factory"),
                campaignState.Player.Company.Id,
                region,
                new RecipeDefinition(
                    "steel_recipe",
                    new[]
                    {
                        new ResourceAmount(iron.Id, 2),
                        new ResourceAmount(coal.Id, 1)
                    },
                    new[] { new ResourceAmount(steel.Id, 1) },
                    10,
                    5,
                    1,
                    "강철 생산")));
            world.RegisterCompany(playerRuntime);

            var opponent = campaignState.Participants[1];
            world.RegisterCompany(new CompanyEconomyRuntime(
                opponent,
                new Warehouse(
                    new WarehouseId("opponent_warehouse"),
                    opponent.Company.Id,
                    region,
                    1000),
                0,
                0,
                0));

            var campaignRules = new CampaignRuleSet();
            var worldService = new WorldEconomyTurnService(
                world,
                new WorldEconomyTuning(new OperatingCostPolicy(
                    100,
                    30,
                    0,
                    0,
                    0,
                    100000)),
                campaignRules);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market, worldService),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(),
                new TurnNumber(1),
                new GameDay(0),
                campaignSession: new CampaignSession(
                    campaignState,
                    new CampaignVictoryEvaluator(campaignRules)));

            TurnReport report = engine.EndTurn();

            Assert.That(playerWarehouse.GetAvailable(iron.Id), Is.EqualTo(0));
            Assert.That(playerWarehouse.GetAvailable(coal.Id), Is.EqualTo(0));
            Assert.That(playerWarehouse.GetAvailable(steel.Id), Is.EqualTo(1));
            Assert.That(campaignState.Player.Company.Cash, Is.EqualTo(870));
            Assert.That(report.WorldReport.Production.Count, Is.EqualTo(1));
            Assert.That(report.WorldReport.Production[0].Produced, Is.True);
            Assert.That(report.WorldReport.Finances.Count, Is.EqualTo(2));
            Assert.That(campaignState.Player.InventoryValue, Is.GreaterThan(0));
            Assert.That(report.CampaignResult.Outcome,
                Is.EqualTo(CampaignOutcome.InProgress));
        }

        [Test]
        public void FullTurn_MarketFillTransfersCashAndWarehouseStock()
        {
            var region = new RegionId("starter");
            var steel = CreateResource("steel", 100);
            CampaignState campaignState = CreateCampaign(1000, 100);
            var world = new WorldEconomyState();
            RegisterMarket(world, region, steel, 10, 10);

            var buyerWarehouse = new Warehouse(
                new WarehouseId("buyer_warehouse"),
                campaignState.Player.Company.Id,
                region,
                1000);
            var buyer = new CompanyEconomyRuntime(
                campaignState.Player,
                buyerWarehouse,
                0,
                0,
                0);
            world.RegisterCompany(buyer);

            var sellerState = campaignState.Participants[1];
            var sellerWarehouse = new Warehouse(
                new WarehouseId("seller_warehouse"),
                sellerState.Company.Id,
                region,
                1000);
            sellerWarehouse.TryAdd(steel.Id, 5, steel.StorageVolume);
            world.RegisterCompany(new CompanyEconomyRuntime(
                sellerState,
                sellerWarehouse,
                0,
                0,
                0));

            var campaignRules = new CampaignRuleSet();
            var worldService = new WorldEconomyTurnService(
                world,
                new WorldEconomyTuning(new OperatingCostPolicy(
                    0,
                    0,
                    0,
                    0,
                    0,
                    100000)),
                campaignRules);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            market.SubmitOrder(new MarketOrder(
                "buy",
                campaignState.Player.Company.Id,
                region,
                steel.Id,
                OrderSide.Buy,
                OrderPurpose.ProductionInput,
                2,
                120,
                0));
            market.SubmitOrder(new MarketOrder(
                "sell",
                sellerState.Company.Id,
                region,
                steel.Id,
                OrderSide.Sell,
                OrderPurpose.Export,
                2,
                80,
                0));

            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market, worldService),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(),
                new TurnNumber(1),
                new GameDay(0),
                campaignSession: new CampaignSession(
                    campaignState,
                    new CampaignVictoryEvaluator(campaignRules)));

            TurnReport report = engine.EndTurn();

            Assert.That(campaignState.Player.Company.Cash, Is.EqualTo(800));
            Assert.That(sellerState.Company.Cash, Is.EqualTo(300));
            Assert.That(buyerWarehouse.GetAvailable(steel.Id), Is.EqualTo(2));
            Assert.That(sellerWarehouse.GetAvailable(steel.Id), Is.EqualTo(3));
            Assert.That(report.WorldReport.Trades.Count, Is.EqualTo(1));
            Assert.That(report.WorldReport.Trades[0].Settled, Is.True);
        }

        [Test]
        public void Warehouse_RejectsStockBeyondCapacity()
        {
            var warehouse = new Warehouse(
                new WarehouseId("warehouse"),
                new CompanyId("company"),
                new RegionId("region"),
                10);

            Assert.That(
                warehouse.TryAdd("steel", 4, 2),
                Is.True);
            Assert.That(warehouse.UsedCapacity, Is.EqualTo(8));
            Assert.That(
                warehouse.TryAdd("iron", 3, 1),
                Is.False);
            Assert.That(warehouse.GetAvailable("iron"), Is.EqualTo(0));
        }

        [Test]
        public void Market_BuyOrderRaisesDemandAndDerivedPrice()
        {
            var region = new RegionId("region");
            var definition = CreateResource("iron", 100);
            var state = new ResourceMarketState(
                definition.Id,
                100,
                1000);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            market.SubmitOrder(new MarketOrder(
                "player_buy",
                new CompanyId("player"),
                region,
                definition.Id,
                OrderSide.Buy,
                OrderPurpose.ProductionInput,
                100,
                120,
                0));

            market.ProcessMarketPhase(
                new GameDay(0),
                new[]
                {
                    new PhysicalFlow(
                        region,
                        definition.Id,
                        definition,
                        state,
                        100,
                        100,
                        0)
                });

            Assert.That(state.DailyDemand, Is.EqualTo(200));
            Assert.That(state.CurrentPrice, Is.GreaterThan(100));
        }

        [Test]
        public void Market_PhysicalFlowChangesStockAndRecordsRealShortage()
        {
            var region = new RegionId("region");
            ResourceDefinition iron = CreateResource("iron", 100m);
            var state = new ResourceMarketState(iron.Id, 100m, 10m);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());

            market.ProcessMarketPhase(
                new GameDay(0),
                new[]
                {
                    new PhysicalFlow(
                        region,
                        iron.Id,
                        iron,
                        state,
                        supply: 5m,
                        demand: 12m,
                        marketStockChange: 0m)
                });

            Assert.That(state.MarketStock, Is.EqualTo(3m));
            Assert.That(state.UnmetDemand, Is.EqualTo(0m));

            market.ProcessMarketPhase(
                new GameDay(1),
                new[]
                {
                    new PhysicalFlow(
                        region,
                        iron.Id,
                        iron,
                        state,
                        supply: 0m,
                        demand: 10m,
                        marketStockChange: 0m)
                });

            Assert.That(state.MarketStock, Is.EqualTo(0m));
            Assert.That(state.UnmetDemand, Is.EqualTo(7m));
        }

        [Test]
        public void CompanyAI_SubmitsDeterministicSellOrderFromMarketSurplus()
        {
            var region = new RegionId("region");
            var iron = CreateResource("iron", 100);
            var marketState = new ResourceMarketState(
                iron.Id,
                100,
                1000);
            marketState.BeginDay();
            marketState.RecordSupply(200);
            marketState.RecordDemand(100);

            CampaignState campaign = CreateCampaign(1000, 1000);
            var world = new WorldEconomyState();
            world.RegisterMarket(new MarketRuntimeState(
                region,
                iron,
                marketState,
                100,
                100));

            var player = campaign.Player;
            world.RegisterCompany(new CompanyEconomyRuntime(
                player,
                new Warehouse(
                    new WarehouseId("player_warehouse"),
                    player.Company.Id,
                    region,
                    1000),
                0,
                0,
                0));

            var opponent = campaign.Participants[1];
            var aiWarehouse = new Warehouse(
                new WarehouseId("ai_warehouse"),
                opponent.Company.Id,
                region,
                1000);
            aiWarehouse.TryAdd(iron.Id, 10, iron.StorageVolume);
            world.RegisterCompany(new CompanyEconomyRuntime(
                opponent,
                aiWarehouse,
                0,
                0,
                0));

            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var ai = new AICompanyTurnService(
                world,
                market,
                maxActionsPerCompany: 2);

            ai.ResolveTurn(new TurnNumber(2), new GameDay(1));

            Assert.That(ai.LastSubmittedOrderCount, Is.EqualTo(1));
            Assert.That(market.SubmittedOrderCount, Is.EqualTo(1));
        }

        [Test]
        public void PvpCoordinator_RejectsWrongTurnOwnershipReplayAndSequence()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "wrong_turn",
                    "player_1",
                    "company_1",
                    2,
                    1)).Code,
                Is.EqualTo(PvpOperationCode.WrongTurn));

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "wrong_owner",
                    "player_1",
                    "company_2",
                    1,
                    1)).Code,
                Is.EqualTo(PvpOperationCode.CompanyOwnershipMismatch));

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "command_1",
                    "player_1",
                    "company_1",
                    1,
                    1)).Success,
                Is.True);

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "command_1",
                    "player_1",
                    "company_1",
                    1,
                    2)).Code,
                Is.EqualTo(PvpOperationCode.DuplicateCommand));

            PvpOperationResult sequence = coordinator.SubmitCommand(
                CreatePvpMarketCommand(
                    "command_3",
                    "player_1",
                    "company_1",
                    1,
                    3));
            Assert.That(sequence.Code, Is.EqualTo(PvpOperationCode.SequenceMismatch));
            Assert.That(sequence.ExpectedSequence, Is.EqualTo(2));
        }

        [Test]
        public void PvpCoordinator_EnforcesPerPlayerActionPointBudget()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var matchId = new PvpMatchId("match");
            var playerId = new PvpPlayerId("player_1");
            var companyId = new CompanyId("company_1");

            var first = new PvpCommandEnvelope(
                "build_1",
                matchId,
                playerId,
                companyId,
                new TurnNumber(1),
                1,
                PvpCommandKind.BuildFacility,
                new PvpCommandPayload(
                    new RegionId("region"),
                    targetId: "factory_a"));
            var second = new PvpCommandEnvelope(
                "build_2",
                matchId,
                playerId,
                companyId,
                new TurnNumber(1),
                2,
                PvpCommandKind.BuildFacility,
                new PvpCommandPayload(
                    new RegionId("region"),
                    targetId: "factory_b"));

            Assert.That(coordinator.SubmitCommand(first).Success, Is.True);
            Assert.That(
                coordinator.SubmitCommand(second).Code,
                Is.EqualTo(PvpOperationCode.InsufficientActionPoints));
        }

        [Test]
        public void PvpCoordinator_LocksSortsHashesAndAdvancesTurn()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();

            Assert.That(coordinator.SubmitCommand(CreatePvpMarketCommand(
                "slot_1",
                "player_2",
                "company_2",
                1,
                1)).Success, Is.True);
            Assert.That(coordinator.SubmitCommand(CreatePvpMarketCommand(
                "slot_0",
                "player_1",
                "company_1",
                1,
                1)).Success, Is.True);

            Assert.That(
                coordinator.MarkReady(new PvpPlayerId("player_1")).Success,
                Is.True);
            Assert.That(
                coordinator.MarkReady(new PvpPlayerId("player_2")).Success,
                Is.True);
            Assert.That(coordinator.Phase, Is.EqualTo(PvpMatchPhase.Locked));

            PvpOperationResult begin = coordinator.TryBeginResolution(
                out var package);
            Assert.That(begin.Success, Is.True);
            Assert.That(package.Commands.Count, Is.EqualTo(2));
            Assert.That(package.Commands[0].PlayerId.Value, Is.EqualTo("player_1"));
            Assert.That(package.CommandHash.Length, Is.EqualTo(64));

            var duplicatePackage = new PvpTurnPackage(
                package.MatchId,
                package.Turn,
                package.Commands);
            Assert.That(
                duplicatePackage.CommandHash,
                Is.EqualTo(package.CommandHash));

            Assert.That(
                coordinator.CompleteResolution("authoritative_hash", false).Success,
                Is.True);
            Assert.That(coordinator.CurrentTurn.Value, Is.EqualTo(2));
            Assert.That(coordinator.Phase, Is.EqualTo(PvpMatchPhase.Planning));
            Assert.That(coordinator.Revision, Is.EqualTo(1));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(0));
            Assert.That(
                coordinator.CreateSnapshot().Players[0].SpentActionPoints,
                Is.EqualTo(0));
        }

        [Test]
        public void PvpCoordinator_CancelRefundsPointsAndRestoresSequence()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            PvpCommandEnvelope command = CreatePvpMarketCommand(
                "cancel_me",
                "player_1",
                "company_1",
                1,
                1);

            Assert.That(coordinator.SubmitCommand(command).Success, Is.True);
            PvpOperationResult cancelled = coordinator.CancelLastCommand(
                new PvpPlayerId("player_1"),
                "cancel_me");

            Assert.That(cancelled.Success, Is.True);
            Assert.That(cancelled.ExpectedSequence, Is.EqualTo(1));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(0));
            Assert.That(
                coordinator.CreateSnapshot().Players[0].SpentActionPoints,
                Is.EqualTo(0));
            Assert.That(coordinator.SubmitCommand(command).Success, Is.True);
        }

        [Test]
        public void PvpMarketTranslator_CreatesExistingTurnCommand()
        {
            var translator = new PvpMarketCommandTranslator();
            bool created = translator.TryCreateTurnCommand(
                CreatePvpMarketCommand(
                    "translate",
                    "player_1",
                    "company_1",
                    1,
                    1),
                out var command,
                out var code);

            Assert.That(created, Is.True);
            Assert.That(code, Is.EqualTo(PvpOperationCode.Accepted));
            Assert.That(command, Is.TypeOf<SubmitMarketOrderTurnCommand>());
            Assert.That(command.ActorId, Is.EqualTo(new CompanyId("company_1")));
        }

        [Test]
        public void PvpCoordinator_ReconnectRestoresOnlyOwnPendingCommands()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var player1 = new PvpPlayerId("player_1");

            Assert.That(
                coordinator.SetConnected(player1, false).Success,
                Is.True);
            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "offline",
                    "player_1",
                    "company_1",
                    1,
                    1)).Code,
                Is.EqualTo(PvpOperationCode.PlayerDisconnected));

            coordinator.SetConnected(player1, true);
            coordinator.SubmitCommand(CreatePvpMarketCommand(
                "own_command",
                "player_1",
                "company_1",
                1,
                1));
            coordinator.SubmitCommand(CreatePvpMarketCommand(
                "opponent_command",
                "player_2",
                "company_2",
                1,
                1));

            IReadOnlyList<PvpCommandEnvelope> restored =
                coordinator.GetPendingCommands(player1);

            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored[0].CommandId, Is.EqualTo("own_command"));
            Assert.That(
                coordinator.CreateSnapshot().Players[0].IsConnected,
                Is.True);
        }

        [Test]
        public void PvpGateway_ReplayedRequestIsIdempotent()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            var peer = new PvpPeerContext(
                "connection_1",
                new PvpPlayerId("player_1"));
            PvpClientRequest request = CreatePvpSubmitRequest(
                "request_1",
                "command_1",
                "player_1",
                "company_1",
                0,
                1);

            PvpServerResponse first = gateway.Handle(peer, request);
            PvpServerResponse replay = gateway.Handle(peer, request);

            Assert.That(first.Result.Success, Is.True);
            Assert.That(replay.Result.Success, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(1));
            Assert.That(replay.OwnPendingCommands.Count, Is.EqualTo(1));
        }

        [Test]
        public void PvpGateway_RejectsSpoofedAuthenticatedPlayer()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            gateway.Handle(
                new PvpPeerContext(
                    "connection_1",
                    new PvpPlayerId("player_1")),
                CreatePvpSubmitRequest(
                    "victim_request",
                    "victim_command",
                    "player_1",
                    "company_1",
                    0,
                    1));
            var peer = new PvpPeerContext(
                "connection_2",
                new PvpPlayerId("player_2"));
            PvpClientRequest spoofed = CreatePvpSubmitRequest(
                "spoofed_request",
                "spoofed_command",
                "player_1",
                "company_1",
                0,
                1);

            PvpServerResponse response = gateway.Handle(peer, spoofed);

            Assert.That(
                response.Result.Code,
                Is.EqualTo(PvpOperationCode.AuthenticationMismatch));
            Assert.That(response.OwnPendingCommands.Count, Is.EqualTo(0));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(1));
        }

        [Test]
        public void PvpGateway_RejectsRequestIdReuseWithDifferentPayload()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            var peer = new PvpPeerContext(
                "connection_1",
                new PvpPlayerId("player_1"));

            gateway.Handle(peer, CreatePvpSubmitRequest(
                "same_request",
                "command_1",
                "player_1",
                "company_1",
                0,
                1));
            PvpServerResponse conflict = gateway.Handle(
                peer,
                CreatePvpSubmitRequest(
                    "same_request",
                    "command_2",
                    "player_1",
                    "company_1",
                    0,
                    2));

            Assert.That(
                conflict.Result.Code,
                Is.EqualTo(PvpOperationCode.DuplicateRequestConflict));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(1));
        }

        [Test]
        public void PvpGateway_RejectsStaleRevisionBeforeMutation()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            var peer = new PvpPeerContext(
                "connection_1",
                new PvpPlayerId("player_1"));
            PvpClientRequest stale = CreatePvpSubmitRequest(
                "stale_request",
                "stale_command",
                "player_1",
                "company_1",
                1,
                1);

            PvpServerResponse response = gateway.Handle(peer, stale);

            Assert.That(
                response.Result.Code,
                Is.EqualTo(PvpOperationCode.StaleRevision));
            Assert.That(response.Snapshot.Revision, Is.EqualTo(0));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(0));
        }

        [Test]
        public void ResourceExtractionSite_OutputDeclinesButNeverBelowMinimum()
        {
            var site = new ResourceExtractionSite(
                "iron_site",
                new RegionId("starter"),
                new ResourceId("iron"),
                new TurnNumber(5),
                initialOutput: 100m,
                minimumOutput: 20m,
                declineRatePerTurn: 0.5m);

            Assert.That(site.GetOutput(new TurnNumber(4)), Is.EqualTo(0m));
            Assert.That(site.GetOutput(new TurnNumber(5)), Is.EqualTo(100m));
            Assert.That(site.GetOutput(new TurnNumber(6)), Is.EqualTo(50m));
            Assert.That(site.GetOutput(new TurnNumber(7)), Is.EqualTo(25m));
            Assert.That(site.GetOutput(new TurnNumber(8)), Is.EqualTo(20m));
            Assert.That(site.GetOutput(new TurnNumber(30)), Is.EqualTo(20m));
        }

        [Test]
        public void ProceduralWorld_SameSeedRecreatesInitialConditions()
        {
            var resources = new[]
            {
                new ResourceId("food"),
                new ResourceId("wood"),
                new ResourceId("iron"),
                new ResourceId("coal")
            };
            var settings = new WorldGenerationSettings(
                regionCount: 6,
                factionCount: 3,
                settlementCount: 5,
                npcCount: 12,
                initialResourceSiteCount: 8);
            var generator = new ProceduralWorldGenerator();

            ProceduralWorldState first = generator.Generate(
                12345,
                "world",
                settings,
                resources);
            ProceduralWorldState second = generator.Generate(
                12345,
                "world",
                settings,
                resources);

            Assert.That(first.Regions.Count, Is.EqualTo(6));
            Assert.That(first.Factions.Count, Is.EqualTo(3));
            Assert.That(first.Npcs.Count, Is.EqualTo(12));
            Assert.That(first.ResourceSiteSeeds.Count, Is.EqualTo(8));
            Assert.That(first.Regions[0].Terrain,
                Is.EqualTo(second.Regions[0].Terrain));
            Assert.That(first.Relations[0].Score,
                Is.EqualTo(second.Relations[0].Score));
        }

        [Test]
        public void ResourceExtraction_UsesReserveAndSupportsDeepDevelopment()
        {
            var site = new ResourceExtractionSite(
                "deep_iron",
                new RegionId("mountain"),
                new ResourceId("iron"),
                new TurnNumber(1),
                100m,
                20m,
                0.10m,
                1000m,
                1m,
                100m,
                100m,
                "faction",
                ExtractionMethod.Surface);

            decimal initialReserve = site.RemainingReserve;
            site.Extract(new TurnNumber(1));
            Assert.That(site.RemainingReserve, Is.LessThan(initialReserve));

            decimal depletedReserve = site.RemainingReserve;
            site.DevelopDeepLayer(500m, 0.15m);
            Assert.That(site.RemainingReserve,
                Is.EqualTo(depletedReserve + 500m));
            Assert.That(site.Method, Is.EqualTo(ExtractionMethod.DeepMining));
            Assert.That(site.ExtractionEfficiency, Is.EqualTo(1.15m));
        }

        [Test]
        public void Military_RangedApproachArmorAndRecruitDilutionAreApplied()
        {
            var catalog = MilitaryBalanceCatalog.CreatePrototypeDefaults();
            var archer = new MilitaryUnit(
                "archer",
                "attacker",
                catalog.Get(UnitArchetype.Archer),
                new EquipmentLoadout(
                    "light",
                    "경장비",
                    ArmorProfile.Light),
                100,
                averageExperience: 80m);
            decimal experiencedAverage = archer.AverageExperience;
            archer.Recruit(100);

            Assert.That(
                archer.AverageExperience,
                Is.LessThan(experiencedAverage));
            Assert.That(
                new DamageProfile(0m, 0m, 1m)
                    .ResolveAgainst(ArmorProfile.Heavy),
                Is.GreaterThan(
                    new DamageProfile(1m, 0m, 0m)
                        .ResolveAgainst(ArmorProfile.Heavy)));
            var logistics = new MilitaryLogisticsTuning();
            Assert.That(
                logistics.GetReplacementSpeed(1m),
                Is.GreaterThan(logistics.GetReplacementSpeed(0m)));

            var attackers = new ArmyState(
                "attackers",
                "attacker",
                new RegionId("field"));
            attackers.AddUnit(archer);
            var defenders = new ArmyState(
                "defenders",
                "defender",
                new RegionId("field"));
            defenders.AddUnit(new MilitaryUnit(
                "spear",
                "defender",
                catalog.Get(UnitArchetype.Spearman),
                new EquipmentLoadout(
                    "heavy",
                    "중장비",
                    ArmorProfile.Heavy),
                180));

            BattleReport report = new BattleResolver(
                logistics).Resolve(
                    attackers,
                    defenders,
                    77);
            bool sawRangedApproach = false;
            bool sawMelee = false;
            for (int i = 0; i < report.Phases.Count; i++)
            {
                sawRangedApproach |= report.Phases[i].Phase ==
                    BattlePhase.RangedApproach;
                sawMelee |= report.Phases[i].Phase == BattlePhase.Melee;
            }

            Assert.That(sawRangedApproach, Is.True);
            Assert.That(sawMelee, Is.True);
        }

        [Test]
        public void ResourceSiteEvent_EveryFiveTurnsAddsDecliningMarketSupply()
        {
            var region = new RegionId("starter");
            var iron = CreateResource("iron", 100m);
            var world = new WorldEconomyState();
            RegisterMarket(world, region, iron, supply: 10m, demand: 10m);

            var service = new WorldEconomyTurnService(
                world,
                new WorldEconomyTuning(new OperatingCostPolicy(
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    100000m)),
                new CampaignRuleSet(),
                new ResourceSiteEventSettings(
                    spawnIntervalTurns: 5,
                    initialOutput: 100m,
                    minimumOutput: 20m,
                    declineRatePerTurn: 0.5m,
                    allowedResourceIds: new[] { "iron" }));
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());

            decimal turn5Supply = 0m;
            decimal turn6Supply = 0m;
            decimal turn8Supply = 0m;
            decimal turn10Supply = 0m;
            int turn5SpawnCount = 0;
            int turn10SpawnCount = 0;

            for (int turn = 1; turn <= 10; turn++)
            {
                var turnNumber = new TurnNumber(turn);
                var day = new GameDay(turn - 1);
                IReadOnlyList<PhysicalFlow> flows =
                    service.PrepareTurn(turnNumber, day);
                decimal supply = flows[0].Supply;
                MarketTickReport marketReport =
                    market.ProcessMarketPhase(day, flows);
                WorldTurnReport report = service.CompleteTurn(
                    turnNumber,
                    day,
                    marketReport);

                if (turn == 5)
                {
                    turn5Supply = supply;
                    turn5SpawnCount = report.ResourceSites
                        .SpawnedSites.Count;
                }
                else if (turn == 6)
                {
                    turn6Supply = supply;
                }
                else if (turn == 8)
                {
                    turn8Supply = supply;
                }
                else if (turn == 10)
                {
                    turn10Supply = supply;
                    turn10SpawnCount = report.ResourceSites
                        .SpawnedSites.Count;
                }
            }

            Assert.That(turn5Supply, Is.EqualTo(110m));
            Assert.That(turn6Supply, Is.EqualTo(60m));
            Assert.That(turn8Supply, Is.EqualTo(30m));
            Assert.That(turn10Supply, Is.EqualTo(130m));
            Assert.That(turn5SpawnCount, Is.EqualTo(1));
            Assert.That(turn10SpawnCount, Is.EqualTo(1));
            Assert.That(world.ResourceSites.Count, Is.EqualTo(2));
        }

        private static PvpTurnCoordinator CreatePvpCoordinator()
        {
            return new PvpTurnCoordinator(
                new PvpMatchId("match"),
                new[]
                {
                    new PvpPlayerSlot(
                        0,
                        new PvpPlayerId("player_1"),
                        new CompanyId("company_1"),
                        "플레이어 1"),
                    new PvpPlayerSlot(
                        1,
                        new PvpPlayerId("player_2"),
                        new CompanyId("company_2"),
                        "플레이어 2")
                },
                new PvpMatchRules(
                    minPlayers: 2,
                    maxPlayers: 2,
                    maxActionPointsPerPlayer: 5,
                    maxCommandsPerPlayer: 8));
        }

        private static PvpCommandEnvelope CreatePvpMarketCommand(
            string commandId,
            string playerId,
            string companyId,
            int turn,
            int sequence)
        {
            return new PvpCommandEnvelope(
                commandId,
                new PvpMatchId("match"),
                new PvpPlayerId(playerId),
                new CompanyId(companyId),
                new TurnNumber(turn),
                sequence,
                PvpCommandKind.MarketBuy,
                PvpCommandPayload.MarketOrder(
                    new RegionId("region"),
                    new ResourceId("iron"),
                    10,
                    100));
        }

        private static PvpClientRequest CreatePvpSubmitRequest(
            string requestId,
            string commandId,
            string playerId,
            string companyId,
            int expectedRevision,
            int sequence)
        {
            return new PvpClientRequest(
                PvpProtocol.CurrentVersion,
                requestId,
                PvpClientRequestKind.SubmitCommand,
                new PvpMatchId("match"),
                new PvpPlayerId(playerId),
                expectedRevision,
                CreatePvpMarketCommand(
                    commandId,
                    playerId,
                    companyId,
                    1,
                    sequence));
        }

        private static ResourceDefinition CreateResource(
            string id,
            decimal price)
        {
            return new ResourceDefinition(
                new ResourceId(id),
                id,
                price,
                ResourceRarity.Common,
                1,
                false);
        }

        private static void RegisterMarket(
            WorldEconomyState world,
            RegionId region,
            ResourceDefinition definition,
            decimal supply,
            decimal demand)
        {
            world.RegisterMarket(new MarketRuntimeState(
                region,
                definition,
                new ResourceMarketState(
                    definition.Id,
                    definition.BasePrice,
                    1000),
                supply,
                demand));
        }

        private static CampaignState CreateCampaign(
            decimal playerCash,
            params decimal[] opponentCash)
        {
            var participants = new List<CampaignParticipantState>
            {
                new CampaignParticipantState(
                    new Company(
                        new CompanyId("player"),
                        "플레이어 기업",
                        playerCash),
                    true)
            };

            for (int i = 0; i < opponentCash.Length; i++)
            {
                participants.Add(new CampaignParticipantState(
                    new Company(
                        new CompanyId($"opponent_{i + 1}"),
                        $"경쟁 기업 {i + 1}",
                        opponentCash[i]),
                    false));
            }

            return new CampaignState(participants);
        }
    }
}
