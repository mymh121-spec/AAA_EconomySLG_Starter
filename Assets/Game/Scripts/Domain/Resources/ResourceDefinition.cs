using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Resources
{
    public enum ResourceRarity
    {
        Common,
        Uncommon,
        Rare,
        Strategic
    }

    public sealed class ResourceDefinition
    {
        public ResourceId Id { get; }
        public string DisplayName { get; }
        public decimal BasePrice { get; }
        public ResourceRarity Rarity { get; }
        public decimal StorageVolume { get; }
        public bool IsPerishable { get; }

        public ResourceDefinition(
            ResourceId id,
            string displayName,
            decimal basePrice,
            ResourceRarity rarity,
            decimal storageVolume,
            bool isPerishable)
        {
            if (basePrice <= 0)
                throw new ArgumentOutOfRangeException(nameof(basePrice));

            Id = id;
            DisplayName = displayName;
            BasePrice = basePrice;
            Rarity = rarity;
            StorageVolume = Math.Max(0.01m, storageVolume);
            IsPerishable = isPerishable;
        }
    }

    public sealed class ResourceMarketState
    {
        public ResourceId ResourceId { get; }
        public decimal CurrentPrice { get; private set; }
        public decimal PreviousPrice { get; private set; }
        public decimal DailySupply { get; private set; }
        public decimal DailyDemand { get; private set; }
        public decimal MarketStock { get; private set; }
        public decimal UnmetDemand { get; private set; }

        public ResourceMarketState(
            ResourceId resourceId,
            decimal initialPrice,
            decimal initialStock)
        {
            ResourceId = resourceId;
            CurrentPrice = Math.Max(0.01m, initialPrice);
            PreviousPrice = CurrentPrice;
            MarketStock = Math.Max(0, initialStock);
        }

        public void BeginDay()
        {
            PreviousPrice = CurrentPrice;
            DailySupply = 0;
            DailyDemand = 0;
            UnmetDemand = 0;
        }

        public void RecordSupply(decimal amount)
        {
            if (amount > 0) DailySupply += amount;
        }

        public void RecordDemand(decimal amount)
        {
            if (amount > 0) DailyDemand += amount;
        }

        public void RecordUnmetDemand(decimal amount)
        {
            if (amount > 0) UnmetDemand += amount;
        }

        public void AddMarketStock(decimal amount)
        {
            if (amount > 0) MarketStock += amount;
        }

        public bool TryRemoveMarketStock(decimal amount)
        {
            if (amount <= 0 || MarketStock < amount)
                return false;

            MarketStock -= amount;
            return true;
        }

        public void ApplyPrice(decimal nextPrice)
        {
            PreviousPrice = CurrentPrice;
            CurrentPrice = Math.Max(0.01m, nextPrice);
        }
    }

    public sealed class ResourceCatalog
    {
        private readonly Dictionary<ResourceId, ResourceDefinition> _definitions =
            new Dictionary<ResourceId, ResourceDefinition>();

        public void Register(ResourceDefinition definition)
        {
            _definitions[definition.Id] = definition;
        }

        public ResourceDefinition Get(ResourceId id)
        {
            if (!_definitions.TryGetValue(id, out var definition))
                throw new KeyNotFoundException($"Unknown resource: {id}");

            return definition;
        }

        public bool TryGet(
            ResourceId id,
            out ResourceDefinition definition)
        {
            return _definitions.TryGetValue(id, out definition);
        }

        public IReadOnlyCollection<ResourceDefinition> GetAll() => _definitions.Values;
    }
}
