using System;
using System.Collections.Generic;

namespace Game.Domain.Market
{
    public sealed class OrderBook
    {
        private static readonly Comparison<MarketOrder> BuyComparison =
            CompareBuyOrders;

        private static readonly Comparison<MarketOrder> SellComparison =
            CompareSellOrders;

        private readonly List<MarketOrder> _buyOrders = new List<MarketOrder>();
        private readonly List<MarketOrder> _sellOrders = new List<MarketOrder>();

        public int PendingOrderCount => _buyOrders.Count + _sellOrders.Count;

        public void Add(MarketOrder order)
        {
            if (order.Side == OrderSide.Buy)
                _buyOrders.Add(order);
            else
                _sellOrders.Add(order);
        }

        public IReadOnlyList<TradeFill> Match()
        {
            var fills = new List<TradeFill>();
            Match(fills);
            return fills;
        }

        public void Match(List<TradeFill> fills)
        {
            if (fills == null)
                throw new ArgumentNullException(nameof(fills));

            _buyOrders.Sort(BuyComparison);
            _sellOrders.Sort(SellComparison);

            int buyIndex = 0;
            int sellIndex = 0;

            while (buyIndex < _buyOrders.Count &&
                   sellIndex < _sellOrders.Count)
            {
                var buy = _buyOrders[buyIndex];
                var sell = _sellOrders[sellIndex];

                if (buy.RemainingQuantity <= 0)
                {
                    buyIndex++;
                    continue;
                }

                if (sell.RemainingQuantity <= 0)
                {
                    sellIndex++;
                    continue;
                }

                if (buy.LimitPrice < sell.LimitPrice)
                    break;

                decimal quantity = Math.Min(
                    buy.RemainingQuantity,
                    sell.RemainingQuantity);

                decimal executionPrice =
                    (buy.LimitPrice + sell.LimitPrice) / 2.0m;

                buy.Consume(quantity);
                sell.Consume(quantity);

                fills.Add(new TradeFill(
                    buy.OrderId,
                    sell.OrderId,
                    buy.RegionId,
                    buy.ResourceId,
                    buy.CompanyId,
                    sell.CompanyId,
                    quantity,
                    executionPrice));

                if (buy.RemainingQuantity <= 0)
                    buyIndex++;

                if (sell.RemainingQuantity <= 0)
                    sellIndex++;
            }

            _buyOrders.Clear();
            _sellOrders.Clear();
        }

        private static int CompareBuyOrders(
            MarketOrder left,
            MarketOrder right)
        {
            int price = right.LimitPrice.CompareTo(left.LimitPrice);
            return price != 0
                ? price
                : left.CreatedDay.CompareTo(right.CreatedDay);
        }

        private static int CompareSellOrders(
            MarketOrder left,
            MarketOrder right)
        {
            int price = left.LimitPrice.CompareTo(right.LimitPrice);
            return price != 0
                ? price
                : left.CreatedDay.CompareTo(right.CreatedDay);
        }
    }
}
