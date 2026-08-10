using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Resources;

namespace Game.Domain.Market
{
    public sealed class MarketManager
    {
        private sealed class OrderFlowAccumulator
        {
            public decimal Supply;
            public decimal Demand;
        }

        private readonly SupplyDemandLedger _ledger;
        private readonly IPriceCalculator _priceCalculator;
        private readonly int _maxOrdersPerTurn;
        private int _submittedOrderCount;
        private readonly Dictionary<(RegionId, ResourceId), OrderBook> _orderBooks =
            new Dictionary<(RegionId, ResourceId), OrderBook>();
        private readonly Dictionary<(RegionId, ResourceId), OrderFlowAccumulator>
            _submittedFlows =
                new Dictionary<(RegionId, ResourceId), OrderFlowAccumulator>();

        public int RegisteredOrderBookCount => _orderBooks.Count;
        public int SubmittedOrderCount => _submittedOrderCount;

        public MarketManager(
            SupplyDemandLedger ledger,
            IPriceCalculator priceCalculator,
            int maxOrdersPerTurn = 10000)
        {
            _ledger = ledger;
            _priceCalculator = priceCalculator;
            _maxOrdersPerTurn = Math.Max(1, maxOrdersPerTurn);
        }

        public void SubmitOrder(MarketOrder order)
        {
            if (_submittedOrderCount >= _maxOrdersPerTurn)
                throw new InvalidOperationException(
                    "한 턴 최대 시장 주문 수를 초과했습니다.");

            var key = (order.RegionId, order.ResourceId);

            if (!_orderBooks.TryGetValue(key, out var book))
            {
                book = new OrderBook();
                _orderBooks[key] = book;
            }

            book.Add(order);
            _submittedOrderCount++;

            if (!_submittedFlows.TryGetValue(key, out var flow))
            {
                flow = new OrderFlowAccumulator();
                _submittedFlows[key] = flow;
            }

            if (order.Side == OrderSide.Buy)
                flow.Demand += order.RemainingQuantity;
            else
                flow.Supply += order.RemainingQuantity;
        }

        public MarketTickReport ProcessMarketPhase(
            GameDay day,
            IReadOnlyList<PhysicalFlow> flows)
        {
            _ledger.BeginDay(day);

            foreach (var flow in flows)
                _ledger.Record(flow);

            foreach (var submitted in _submittedFlows)
            {
                _ledger.TryRecordMarketActivity(
                    submitted.Key.Item1,
                    submitted.Key.Item2,
                    submitted.Value.Supply,
                    submitted.Value.Demand);
            }

            var fills = new List<TradeFill>();

            foreach (var book in _orderBooks.Values)
                book.Match(fills);

            _submittedOrderCount = 0;
            _submittedFlows.Clear();

            _ledger.FinalizeDay();

            var priceChanges = new List<PriceChange>();

            foreach (var entry in _ledger.GetEntries())
            {
                decimal nextPrice = _priceCalculator.Calculate(
                    entry.Definition,
                    entry.State,
                    entry.ToPriceInput());

                entry.State.ApplyPrice(nextPrice);

                priceChanges.Add(new PriceChange(
                    entry.RegionId,
                    entry.ResourceId,
                    nextPrice));
            }

            return new MarketTickReport(
                day.Value,
                fills,
                priceChanges);
        }

        public MarketSnapshot GetSnapshot(
            RegionId regionId,
            ResourceId resourceId)
        {
            return _ledger.CreateSnapshot(regionId, resourceId);
        }
    }
}
