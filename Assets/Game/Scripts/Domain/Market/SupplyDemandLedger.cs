using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Resources;

namespace Game.Domain.Market
{
    public sealed class LedgerEntry
    {
        private readonly MarketTuning _tuning;

        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public ResourceDefinition Definition { get; }
        public ResourceMarketState State { get; }
        public decimal Supply { get; private set; }
        public decimal Demand { get; private set; }
        public decimal MarketStockChange { get; private set; }

        public LedgerEntry(
            RegionId regionId,
            ResourceId resourceId,
            ResourceDefinition definition,
            ResourceMarketState state,
            MarketTuning tuning)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            Definition = definition;
            State = state;
            _tuning = tuning;
        }

        public void BeginDay()
        {
            Supply = 0;
            Demand = 0;
            MarketStockChange = 0;
            State.BeginDay();
        }

        public void Record(PhysicalFlow flow)
        {
            Supply += flow.Supply;
            Demand += flow.Demand;
            MarketStockChange += flow.MarketStockChange;
            State.RecordSupply(flow.Supply);
            State.RecordDemand(flow.Demand);

            if (flow.MarketStockChange > 0)
                State.AddMarketStock(flow.MarketStockChange);
            else if (flow.MarketStockChange < 0)
                State.TryRemoveMarketStock(-flow.MarketStockChange);
        }

        public void RecordMarketActivity(
            decimal submittedSupply,
            decimal submittedDemand)
        {
            Supply += System.Math.Max(0m, submittedSupply);
            Demand += System.Math.Max(0m, submittedDemand);
            State.RecordSupply(submittedSupply);
            State.RecordDemand(submittedDemand);
        }

        public MarketSnapshot CreateSnapshot() =>
            new MarketSnapshot(RegionId, ResourceId, State);

        public void FinalizeDay()
        {
            State.RecordUnmetDemand(
                System.Math.Max(0m, Demand - Supply));
        }

        public PriceInput ToPriceInput()
        {
            decimal targetStock =
                System.Math.Max(10.0m, Demand * _tuning.TargetStockDays);

            return new PriceInput
            {
                PreviousPrice = State.PreviousPrice,
                BasePrice = Definition.BasePrice,
                EffectiveSupply = Supply,
                EffectiveDemand = Demand,
                EndingStock = State.MarketStock,
                TargetStock = targetStock,
                RecentAverageVolume = System.Math.Max(1.0m, Demand),
                NetMarketAbsorption = System.Math.Max(0, -MarketStockChange),
                Elasticity = _tuning.Elasticity,
                StockWeight = _tuning.StockWeight,
                TradeWeight = _tuning.TradeWeight,
                MeanReversion = _tuning.MeanReversion,
                MaxDailyChange = _tuning.MaxDailyChange
            };
        }
    }

    public sealed class SupplyDemandLedger
    {
        private readonly MarketTuning _tuning;

        private readonly Dictionary<(RegionId, ResourceId), LedgerEntry> _entries =
            new Dictionary<(RegionId, ResourceId), LedgerEntry>();

        public SupplyDemandLedger(MarketTuning tuning = null)
        {
            _tuning = tuning ?? new MarketTuning();
        }

        public void BeginDay(GameDay day)
        {
            foreach (var entry in _entries.Values)
                entry.BeginDay();
        }

        public void Record(PhysicalFlow flow)
        {
            var key = (flow.RegionId, flow.ResourceId);

            if (!_entries.TryGetValue(key, out var entry))
            {
                entry = new LedgerEntry(
                    flow.RegionId,
                    flow.ResourceId,
                    flow.Definition,
                    flow.State,
                    _tuning);

                _entries[key] = entry;
            }

            entry.Record(flow);
        }

        public IReadOnlyCollection<LedgerEntry> GetEntries() => _entries.Values;

        public void FinalizeDay()
        {
            foreach (var entry in _entries.Values)
                entry.FinalizeDay();
        }

        public bool TryRecordMarketActivity(
            RegionId regionId,
            ResourceId resourceId,
            decimal submittedSupply,
            decimal submittedDemand)
        {
            if (!_entries.TryGetValue(
                (regionId, resourceId),
                out var entry))
            {
                return false;
            }

            entry.RecordMarketActivity(
                submittedSupply,
                submittedDemand);
            return true;
        }

        public MarketSnapshot CreateSnapshot(
            RegionId regionId,
            ResourceId resourceId)
        {
            return _entries[(regionId, resourceId)].CreateSnapshot();
        }
    }
}
