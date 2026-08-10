using System;
using Game.Domain.Resources;

namespace Game.Domain.Market
{
    public sealed class MarketTuning
    {
        public decimal TargetStockDays { get; }
        public decimal Elasticity { get; }
        public decimal StockWeight { get; }
        public decimal TradeWeight { get; }
        public decimal MeanReversion { get; }
        public decimal MaxDailyChange { get; }

        public MarketTuning(
            decimal targetStockDays = 14m,
            decimal elasticity = 0.5m,
            decimal stockWeight = 0.2m,
            decimal tradeWeight = 0.1m,
            decimal meanReversion = 0.05m,
            decimal maxDailyChange = 0.15m)
        {
            TargetStockDays = Math.Max(1, targetStockDays);
            Elasticity = Math.Max(0, elasticity);
            StockWeight = Math.Max(0, stockWeight);
            TradeWeight = Math.Max(0, tradeWeight);
            MeanReversion = Math.Max(0, meanReversion);
            MaxDailyChange = Math.Clamp(maxDailyChange, 0.001m, 1.0m);
        }
    }

    public sealed class PriceInput
    {
        public decimal PreviousPrice { get; set; }
        public decimal BasePrice { get; set; }
        public decimal EffectiveSupply { get; set; }
        public decimal EffectiveDemand { get; set; }
        public decimal EndingStock { get; set; }
        public decimal TargetStock { get; set; }
        public decimal RecentAverageVolume { get; set; }
        public decimal NetMarketAbsorption { get; set; }
        public decimal Elasticity { get; set; } = 0.5m;
        public decimal StockWeight { get; set; } = 0.2m;
        public decimal TradeWeight { get; set; } = 0.1m;
        public decimal MeanReversion { get; set; } = 0.05m;
        public decimal MaxDailyChange { get; set; } = 0.15m;
    }

    public interface IPriceCalculator
    {
        decimal Calculate(
            ResourceDefinition definition,
            ResourceMarketState state,
            PriceInput input);
    }

    public sealed class PriceCalculator : IPriceCalculator
    {
        public decimal Calculate(
            ResourceDefinition definition,
            ResourceMarketState state,
            PriceInput input)
        {
            decimal safeDemand = Math.Max(1.0m, input.EffectiveDemand);
            decimal imbalance =
                (input.EffectiveDemand - input.EffectiveSupply) / safeDemand;

            decimal safeTargetStock = Math.Max(1.0m, input.TargetStock);
            decimal stockPressure =
                (input.TargetStock - input.EndingStock) / safeTargetStock;

            decimal safeVolume = Math.Max(1.0m, input.RecentAverageVolume);
            decimal tradePressure =
                input.NetMarketAbsorption / safeVolume;

            decimal meanReversion = (decimal)Math.Log(
                (double)(input.BasePrice / Math.Max(0.01m, input.PreviousPrice)));

            decimal logReturn =
                input.Elasticity * imbalance
                + input.StockWeight * stockPressure
                + input.TradeWeight * tradePressure
                + input.MeanReversion * meanReversion;

            double safeLogReturn = Math.Clamp(
                (double)logReturn,
                -5.0,
                5.0);

            decimal multiplier =
                (decimal)Math.Exp(safeLogReturn);

            multiplier = Math.Clamp(
                multiplier,
                1.0m - input.MaxDailyChange,
                1.0m + input.MaxDailyChange);

            decimal price =
                input.PreviousPrice * multiplier;

            decimal minimumPrice = Math.Max(0.01m, definition.BasePrice * 0.02m);
            decimal maximumPrice = definition.BasePrice * 100.0m;

            return Math.Clamp(price, minimumPrice, maximumPrice);
        }
    }
}
