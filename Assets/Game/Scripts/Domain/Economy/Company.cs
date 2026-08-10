using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Economy
{
    public sealed class Company
    {
        public CompanyId Id { get; }
        public string Name { get; }
        public decimal Cash { get; private set; }
        public decimal Debt { get; private set; }
        public bool IsBankrupt { get; private set; }

        private readonly HashSet<FactoryId> _factoryIds = new HashSet<FactoryId>();
        private readonly HashSet<WarehouseId> _warehouseIds = new HashSet<WarehouseId>();
        private readonly HashSet<string> _technologyIds = new HashSet<string>();

        public IReadOnlyCollection<FactoryId> FactoryIds => _factoryIds;
        public IReadOnlyCollection<WarehouseId> WarehouseIds => _warehouseIds;

        public Company(CompanyId id, string name, decimal initialCash)
        {
            Id = id;
            Name = name;
            Cash = Math.Max(0, initialCash);
        }

        public bool CanAfford(decimal amount) =>
            amount >= 0 && Cash >= amount;

        public bool TrySpend(decimal amount)
        {
            if (!CanAfford(amount))
                return false;

            Cash -= amount;
            return true;
        }

        public void Receive(decimal amount)
        {
            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Cash += amount;
        }

        public void AddDebt(decimal amount)
        {
            if (amount <= 0)
                throw new ArgumentOutOfRangeException(nameof(amount));

            Debt += amount;
        }

        public void RegisterFactory(FactoryId id) => _factoryIds.Add(id);
        public void RegisterWarehouse(WarehouseId id) => _warehouseIds.Add(id);

        public void CompleteTechnology(string id) => _technologyIds.Add(id);
        public bool HasTechnology(string id) => _technologyIds.Contains(id);

        public void MarkBankrupt() => IsBankrupt = true;
    }
}
