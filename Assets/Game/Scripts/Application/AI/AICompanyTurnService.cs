using System;
using System.Collections.Generic;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.AI;
using Game.Domain.Common;
using Game.Domain.Market;

namespace Game.Application.AI
{
    public sealed class AICompanyTurnService : IAIResolutionService
    {
        private readonly WorldEconomyState _world;
        private readonly MarketManager _market;
        private readonly CompanyAI _companyAI;
        private readonly int _maxActionsPerCompany;
        private readonly List<MarketSnapshot> _snapshotBuffer =
            new List<MarketSnapshot>(64);
        private readonly List<AIAction> _actionBuffer =
            new List<AIAction>(16);

        public int LastSubmittedOrderCount { get; private set; }

        public AICompanyTurnService(
            WorldEconomyState world,
            MarketManager market,
            CompanyAI companyAI = null,
            int maxActionsPerCompany = 2)
        {
            _world = world ??
                throw new ArgumentNullException(nameof(world));
            _market = market ??
                throw new ArgumentNullException(nameof(market));
            _companyAI = companyAI ?? new CompanyAI();
            _maxActionsPerCompany = Math.Max(
                1,
                maxActionsPerCompany);
        }

        public void ResolveTurn(
            TurnNumber turn,
            GameDay calendarDay)
        {
            LastSubmittedOrderCount = 0;
            BuildMarketSnapshots();

            for (int i = 0; i < _world.Companies.Count; i++)
            {
                CompanyEconomyRuntime runtime = _world.Companies[i];
                if (runtime.CampaignState.IsPlayer ||
                    runtime.CampaignState.IsEliminated)
                {
                    continue;
                }

                _companyAI.Think(
                    new AIDecisionContext(
                        runtime.Company,
                        _snapshotBuffer,
                        _maxActionsPerCompany),
                    _actionBuffer);

                for (int j = 0; j < _actionBuffer.Count; j++)
                {
                    if (TrySubmitAction(
                        runtime,
                        _actionBuffer[j],
                        turn,
                        calendarDay,
                        j))
                    {
                        LastSubmittedOrderCount++;
                    }
                }
            }
        }

        private void BuildMarketSnapshots()
        {
            _snapshotBuffer.Clear();

            for (int i = 0; i < _world.Markets.Count; i++)
            {
                MarketRuntimeState market = _world.Markets[i];
                _snapshotBuffer.Add(new MarketSnapshot(
                    market.RegionId,
                    market.Definition.Id,
                    market.MarketState));
            }
        }

        private bool TrySubmitAction(
            CompanyEconomyRuntime runtime,
            AIAction action,
            TurnNumber turn,
            GameDay calendarDay,
            int actionIndex)
        {
            if (!action.ResourceId.HasValue)
                return false;

            ResourceId resourceId = action.ResourceId.Value;
            MarketRuntimeState market = FindCompanyMarket(
                runtime,
                resourceId);
            if (market == null)
                return false;

            decimal quantity;
            decimal limitPrice;
            OrderSide side;
            OrderPurpose purpose;

            if (action.Type == AIActionType.BuyResource)
            {
                limitPrice = market.MarketState.CurrentPrice * 1.05m;
                decimal affordable = runtime.Company.Cash /
                    Math.Max(0.01m, limitPrice);
                decimal storable = runtime.PrimaryWarehouse
                    .AvailableCapacity /
                    market.Definition.StorageVolume;
                quantity = MinPositive(
                    action.Quantity,
                    affordable,
                    storable);
                side = OrderSide.Buy;
                purpose = OrderPurpose.ProductionInput;
            }
            else if (action.Type == AIActionType.SellResource)
            {
                limitPrice = market.MarketState.CurrentPrice * 0.95m;
                quantity = Math.Min(
                    action.Quantity,
                    runtime.PrimaryWarehouse.GetAvailable(resourceId));
                side = OrderSide.Sell;
                purpose = OrderPurpose.Export;
            }
            else
            {
                return false;
            }

            if (quantity <= 0m || limitPrice <= 0m)
                return false;

            var order = new MarketOrder(
                $"ai_{turn.Value}_{runtime.Company.Id.Value}_{actionIndex}",
                runtime.Company.Id,
                market.RegionId,
                resourceId,
                side,
                purpose,
                quantity,
                limitPrice,
                calendarDay.Value);

            try
            {
                _market.SubmitOrder(order);
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private MarketRuntimeState FindCompanyMarket(
            CompanyEconomyRuntime runtime,
            ResourceId resourceId)
        {
            _world.TryGetMarket(
                runtime.PrimaryWarehouse.RegionId,
                resourceId,
                out var market);
            return market;
        }

        private static decimal MinPositive(
            decimal first,
            decimal second,
            decimal third)
        {
            return Math.Max(
                0m,
                Math.Min(first, Math.Min(second, third)));
        }
    }
}
