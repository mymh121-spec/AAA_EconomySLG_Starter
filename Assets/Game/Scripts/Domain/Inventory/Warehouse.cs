using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Inventory
{
    public sealed class InventoryPosition
    {
        public decimal OnHand { get; private set; }
        public decimal Reserved { get; private set; }
        public decimal Available => OnHand - Reserved;

        public InventoryPosition(decimal initialAmount)
        {
            OnHand = Math.Max(0, initialAmount);
        }

        public bool TryReserve(decimal amount)
        {
            if (amount <= 0 || Available < amount)
                return false;

            Reserved += amount;
            return true;
        }

        public void Release(decimal amount)
        {
            if (amount <= 0 || amount > Reserved)
                throw new InvalidOperationException("Invalid reservation release.");

            Reserved -= amount;
        }

        public void ConsumeReserved(decimal amount)
        {
            if (amount <= 0 || amount > Reserved || amount > OnHand)
                throw new InvalidOperationException("Invalid reserved consumption.");

            Reserved -= amount;
            OnHand -= amount;
        }

        public void Add(decimal amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            OnHand += amount;
        }
    }

    public sealed class Warehouse
    {
        public WarehouseId Id { get; }
        public CompanyId OwnerId { get; }
        public RegionId RegionId { get; }
        public decimal Capacity { get; }
        public decimal UsedCapacity
        {
            get
            {
                decimal used = 0m;

                foreach (var pair in _stocks)
                {
                    decimal unitVolume = _unitVolumes.TryGetValue(
                        pair.Key,
                        out var volume)
                        ? volume
                        : 1m;
                    used += pair.Value.OnHand * unitVolume;
                }

                return used;
            }
        }
        public decimal AvailableCapacity =>
            Math.Max(0m, Capacity - UsedCapacity);
        public IReadOnlyDictionary<ResourceId, InventoryPosition> Stocks =>
            _stocks;

        private readonly Dictionary<ResourceId, InventoryPosition> _stocks =
            new Dictionary<ResourceId, InventoryPosition>();
        private readonly Dictionary<ResourceId, decimal> _unitVolumes =
            new Dictionary<ResourceId, decimal>();

        public Warehouse(
            WarehouseId id,
            CompanyId ownerId,
            RegionId regionId,
            decimal capacity)
        {
            Id = id;
            OwnerId = ownerId;
            RegionId = regionId;
            Capacity = Math.Max(0, capacity);
        }

        public InventoryPosition GetOrCreate(ResourceId resourceId)
        {
            if (!_stocks.TryGetValue(resourceId, out var position))
            {
                position = new InventoryPosition(0);
                _stocks[resourceId] = position;
            }

            return position;
        }

        public decimal GetAvailable(ResourceId resourceId) =>
            GetOrCreate(resourceId).Available;

        public bool TryReserve(ResourceId resourceId, decimal amount) =>
            GetOrCreate(resourceId).TryReserve(amount);

        public void ConsumeReserved(ResourceId resourceId, decimal amount) =>
            GetOrCreate(resourceId).ConsumeReserved(amount);

        public void ReleaseReservation(ResourceId resourceId, decimal amount) =>
            GetOrCreate(resourceId).Release(amount);

        public bool CanAdd(
            ResourceId resourceId,
            decimal amount,
            decimal unitVolume = 1m)
        {
            if (amount < 0)
                return false;

            decimal safeVolume = Math.Max(0.01m, unitVolume);
            return amount * safeVolume <= AvailableCapacity;
        }

        public bool TryAdd(
            ResourceId resourceId,
            decimal amount,
            decimal unitVolume = 1m)
        {
            if (!CanAdd(resourceId, amount, unitVolume))
                return false;

            _unitVolumes[resourceId] = Math.Max(0.01m, unitVolume);
            GetOrCreate(resourceId).Add(amount);
            return true;
        }

        public void Add(ResourceId resourceId, decimal amount)
        {
            if (!TryAdd(resourceId, amount))
            {
                throw new InvalidOperationException(
                    "창고의 남은 용량이 부족합니다.");
            }
        }

        public bool TryRemoveAvailable(
            ResourceId resourceId,
            decimal amount)
        {
            if (!TryReserve(resourceId, amount))
                return false;

            ConsumeReserved(resourceId, amount);
            return true;
        }
    }
}
