using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Inventory;
using Game.Domain.Resources;

namespace Game.Application.World
{
    public readonly struct HeadquartersInventoryItem
    {
        public ResourceId ResourceId { get; }
        public string DisplayName { get; }
        public decimal OnHand { get; }
        public decimal Reserved { get; }
        public decimal Available { get; }

        public HeadquartersInventoryItem(
            ResourceId resourceId,
            string displayName,
            decimal onHand,
            decimal reserved)
        {
            ResourceId = resourceId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? resourceId.Value
                : displayName;
            OnHand = Math.Max(0m, onHand);
            Reserved = Math.Clamp(reserved, 0m, OnHand);
            Available = OnHand - Reserved;
        }
    }

    public sealed class HeadquartersInventorySnapshot
    {
        public static HeadquartersInventorySnapshot Empty { get; } =
            new HeadquartersInventorySnapshot(
                0m,
                0m,
                Array.Empty<HeadquartersInventoryItem>());

        public decimal Capacity { get; }
        public decimal UsedCapacity { get; }
        public decimal AvailableCapacity =>
            Math.Max(0m, Capacity - UsedCapacity);
        public IReadOnlyList<HeadquartersInventoryItem> Items { get; }

        public HeadquartersInventorySnapshot(
            decimal capacity,
            decimal usedCapacity,
            IReadOnlyList<HeadquartersInventoryItem> items)
        {
            Capacity = Math.Max(0m, capacity);
            UsedCapacity = Math.Clamp(usedCapacity, 0m, Capacity);

            if (items == null || items.Count == 0)
            {
                Items = Array.Empty<HeadquartersInventoryItem>();
                return;
            }

            var copy = new HeadquartersInventoryItem[items.Count];
            for (int i = 0; i < items.Count; i++)
                copy[i] = items[i];
            Items = copy;
        }
    }

    public sealed class HeadquartersInventoryQuery
    {
        private readonly ResourceCatalog _catalog;

        public HeadquartersInventoryQuery(ResourceCatalog catalog)
        {
            _catalog = catalog ??
                throw new ArgumentNullException(nameof(catalog));
        }

        public HeadquartersInventorySnapshot Execute(Warehouse warehouse)
        {
            if (warehouse == null)
                return HeadquartersInventorySnapshot.Empty;

            var items = new List<HeadquartersInventoryItem>(
                warehouse.Stocks.Count);
            foreach (var pair in warehouse.Stocks)
            {
                InventoryPosition position = pair.Value;
                if (position.OnHand <= 0m && position.Reserved <= 0m)
                    continue;

                string displayName = _catalog.TryGet(
                    pair.Key,
                    out ResourceDefinition definition)
                    ? definition.DisplayName
                    : pair.Key.Value;
                items.Add(new HeadquartersInventoryItem(
                    pair.Key,
                    displayName,
                    position.OnHand,
                    position.Reserved));
            }

            items.Sort((left, right) => string.Compare(
                left.DisplayName,
                right.DisplayName,
                StringComparison.Ordinal));
            return new HeadquartersInventorySnapshot(
                warehouse.Capacity,
                warehouse.UsedCapacity,
                items);
        }
    }

    public readonly struct MapMineProductionDepositReport
    {
        public decimal StoredIronAmount { get; }
        public decimal RejectedIronAmount { get; }
        public decimal CreditedCashAmount { get; }

        public MapMineProductionDepositReport(
            decimal storedIronAmount,
            decimal rejectedIronAmount,
            decimal creditedCashAmount)
        {
            StoredIronAmount = Math.Max(0m, storedIronAmount);
            RejectedIronAmount = Math.Max(0m, rejectedIronAmount);
            CreditedCashAmount = Math.Max(0m, creditedCashAmount);
        }
    }

    public sealed class MapMineProductionDepositService
    {
        private readonly WorldEconomyState _world;
        private readonly ResourceCatalog _catalog;

        public MapMineProductionDepositService(
            WorldEconomyState world,
            ResourceCatalog catalog)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _catalog = catalog ??
                throw new ArgumentNullException(nameof(catalog));
        }

        public MapMineProductionDepositReport Deposit(
            IReadOnlyList<MapMineProductionRecord> production)
        {
            if (production == null || production.Count == 0)
                return default;

            decimal storedIron = 0m;
            decimal rejectedIron = 0m;
            decimal creditedCash = 0m;
            bool hasIron = _catalog.TryGet(
                new ResourceId("iron"),
                out ResourceDefinition iron);

            for (int i = 0; i < production.Count; i++)
            {
                MapMineProductionRecord record = production[i];
                if (!_world.TryGetCompany(
                    new CompanyId(record.OwnerFactionId),
                    out CompanyEconomyRuntime company))
                {
                    continue;
                }

                if (record.IronAmount > 0m)
                {
                    decimal storedForCompany = 0m;
                    if (hasIron)
                    {
                        decimal storable = Math.Min(
                            record.IronAmount,
                            company.PrimaryWarehouse.AvailableCapacity /
                            iron.StorageVolume);
                        if (storable > 0m && company.PrimaryWarehouse.TryAdd(
                            iron.Id,
                            storable,
                            iron.StorageVolume))
                        {
                            storedForCompany = storable;
                            storedIron += storable;
                        }
                    }

                    rejectedIron += record.IronAmount - storedForCompany;
                }

                if (record.CashAmount > 0m)
                {
                    company.Company.Receive(record.CashAmount);
                    creditedCash += record.CashAmount;
                }
            }

            return new MapMineProductionDepositReport(
                storedIron,
                rejectedIron,
                creditedCash);
        }
    }

    public readonly struct MapCapitalSupplyStockReport
    {
        public decimal FoodAmount { get; }
        public decimal EquipmentAmount { get; }
        public decimal MedicineAmount { get; }
        public decimal HorseAmount { get; }

        public MapCapitalSupplyStockReport(
            decimal foodAmount,
            decimal equipmentAmount,
            decimal medicineAmount,
            decimal horseAmount = 0m)
        {
            FoodAmount = Math.Max(0m, foodAmount);
            EquipmentAmount = Math.Max(0m, equipmentAmount);
            MedicineAmount = Math.Max(0m, medicineAmount);
            HorseAmount = Math.Max(0m, horseAmount);
        }
    }

    public sealed class MapSupplyStockingService
    {
        private static readonly (MapSupplyKind Kind, ResourceId ResourceId)[]
            SupplyResources =
            {
                (MapSupplyKind.Food, new ResourceId("food")),
                (MapSupplyKind.Equipment, new ResourceId("steel")),
                (MapSupplyKind.Medicine, new ResourceId("medicine")),
                (MapSupplyKind.Horse, new ResourceId("horse"))
            };

        private readonly WorldEconomyState _world;

        public MapSupplyStockingService(WorldEconomyState world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public MapCapitalSupplyStockReport StockFactionCapitals(
            RealtimeMapGameplayService gameplay)
        {
            if (gameplay == null)
                throw new ArgumentNullException(nameof(gameplay));

            decimal food = 0m;
            decimal equipment = 0m;
            decimal medicine = 0m;
            decimal horses = 0m;
            for (int companyIndex = 0;
                 companyIndex < _world.Companies.Count;
                 companyIndex++)
            {
                CompanyEconomyRuntime company =
                    _world.Companies[companyIndex];
                string factionId = company.Company.Id.Value;
                gameplay.ConfigureFactionLogistics(
                    factionId,
                    company.VehicleCount);
                MapCastleControlState capital = gameplay.FindCapital(factionId);
                if (capital == null || capital.IsDestroyed)
                    continue;

                for (int resourceIndex = 0;
                     resourceIndex < SupplyResources.Length;
                     resourceIndex++)
                {
                    (MapSupplyKind kind, ResourceId resourceId) =
                        SupplyResources[resourceIndex];
                    decimal available = company.PrimaryWarehouse.GetAvailable(
                        resourceId);
                    if (available <= 0m ||
                        !company.PrimaryWarehouse.TryRemoveAvailable(
                            resourceId,
                            available) ||
                        !gameplay.TryStockFactionCapitalWarehouse(
                            factionId,
                            kind,
                            available,
                            out decimal stored))
                    {
                        continue;
                    }

                    switch (kind)
                    {
                        case MapSupplyKind.Food:
                            food += stored;
                            break;
                        case MapSupplyKind.Equipment:
                            equipment += stored;
                            break;
                        case MapSupplyKind.Medicine:
                            medicine += stored;
                            break;
                        case MapSupplyKind.Horse:
                            horses += stored;
                            break;
                    }
                }
            }

            return new MapCapitalSupplyStockReport(
                food,
                equipment,
                medicine,
                horses);
        }

        public decimal SettleTransportCosts(
            IReadOnlyList<MapSupplyTransportRecord> transports)
        {
            if (transports == null || transports.Count == 0)
                return 0m;

            decimal settled = 0m;
            for (int i = 0; i < transports.Count; i++)
            {
                MapSupplyTransportRecord transport = transports[i];
                if (transport.Cost <= 0m || !_world.TryGetCompany(
                        new CompanyId(transport.OwnerFactionId),
                        out CompanyEconomyRuntime company))
                {
                    continue;
                }

                decimal paid = Math.Min(
                    company.Company.Cash,
                    transport.Cost);
                if (paid > 0m)
                {
                    company.Company.TrySpend(paid);
                    settled += paid;
                }
                decimal unpaid = transport.Cost - paid;
                if (unpaid > 0m)
                    company.Company.AddDebt(unpaid);
            }
            return settled;
        }
    }
}
