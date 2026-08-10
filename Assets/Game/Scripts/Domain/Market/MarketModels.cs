using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Resources;

namespace Game.Domain.Market
{
    public enum OrderSide
    {
        Buy,
        Sell
    }

    public enum OrderPurpose
    {
        ProductionInput,
        ConsumerDemand,
        Export,
        Speculation,
        Emergency,
        Mission
    }

    public sealed class MarketOrder
    {
        public string OrderId { get; }
        public CompanyId CompanyId { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public OrderSide Side { get; }
        public OrderPurpose Purpose { get; }
        public decimal LimitPrice { get; }
        public int CreatedDay { get; }
        public decimal RemainingQuantity { get; private set; }

        public MarketOrder(
            string orderId,
            CompanyId companyId,
            RegionId regionId,
            ResourceId resourceId,
            OrderSide side,
            OrderPurpose purpose,
            decimal quantity,
            decimal limitPrice,
            int createdDay)
        {
            if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
            if (limitPrice <= 0) throw new ArgumentOutOfRangeException(nameof(limitPrice));

            OrderId = orderId;
            CompanyId = companyId;
            RegionId = regionId;
            ResourceId = resourceId;
            Side = side;
            Purpose = purpose;
            RemainingQuantity = quantity;
            LimitPrice = limitPrice;
            CreatedDay = createdDay;
        }

        public decimal Consume(decimal amount)
        {
            if (amount <= 0 || amount > RemainingQuantity)
                throw new InvalidOperationException("Invalid order consumption.");

            RemainingQuantity -= amount;
            return amount;
        }
    }

    public sealed class TradeFill
    {
        public string BuyOrderId { get; }
        public string SellOrderId { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public CompanyId BuyerId { get; }
        public CompanyId SellerId { get; }
        public decimal Quantity { get; }
        public decimal UnitPrice { get; }
        public decimal TotalPrice => Quantity * UnitPrice;

        public TradeFill(
            string buyOrderId,
            string sellOrderId,
            RegionId regionId,
            ResourceId resourceId,
            CompanyId buyerId,
            CompanyId sellerId,
            decimal quantity,
            decimal unitPrice)
        {
            BuyOrderId = buyOrderId;
            SellOrderId = sellOrderId;
            RegionId = regionId;
            ResourceId = resourceId;
            BuyerId = buyerId;
            SellerId = sellerId;
            Quantity = quantity;
            UnitPrice = unitPrice;
        }
    }

    public readonly struct PhysicalFlow
    {
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public ResourceDefinition Definition { get; }
        public ResourceMarketState State { get; }
        public decimal Supply { get; }
        public decimal Demand { get; }
        public decimal MarketStockChange { get; }

        public PhysicalFlow(
            RegionId regionId,
            ResourceId resourceId,
            ResourceDefinition definition,
            ResourceMarketState state,
            decimal supply,
            decimal demand,
            decimal marketStockChange)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            Definition = definition;
            State = state;
            Supply = supply;
            Demand = demand;
            MarketStockChange = marketStockChange;
        }
    }

    public sealed class MarketSnapshot
    {
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public decimal CurrentPrice { get; }
        public decimal PreviousPrice { get; }
        public decimal Supply { get; }
        public decimal Demand { get; }
        public decimal MarketStock { get; }
        public decimal UnmetDemand { get; }

        public MarketSnapshot(
            RegionId regionId,
            ResourceId resourceId,
            ResourceMarketState state)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            CurrentPrice = state.CurrentPrice;
            PreviousPrice = state.PreviousPrice;
            Supply = state.DailySupply;
            Demand = state.DailyDemand;
            MarketStock = state.MarketStock;
            UnmetDemand = state.UnmetDemand;
        }
    }

    public sealed class MarketTickReport
    {
        public int Day { get; }
        public IReadOnlyList<TradeFill> Fills { get; }
        public IReadOnlyList<PriceChange> PriceChanges { get; }

        public MarketTickReport(
            int day,
            IReadOnlyList<TradeFill> fills,
            IReadOnlyList<PriceChange> priceChanges)
        {
            Day = day;
            Fills = fills;
            PriceChanges = priceChanges;
        }
    }

    public readonly struct PriceChange
    {
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public decimal Price { get; }

        public PriceChange(RegionId regionId, ResourceId resourceId, decimal price)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            Price = price;
        }
    }
}
