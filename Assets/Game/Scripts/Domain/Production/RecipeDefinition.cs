using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Inventory;

namespace Game.Domain.Production
{
    public readonly struct ResourceAmount
    {
        public ResourceId ResourceId { get; }
        public decimal Amount { get; }

        public ResourceAmount(ResourceId resourceId, decimal amount)
        {
            ResourceId = resourceId;
            Amount = Math.Max(0, amount);
        }
    }

    public sealed class RecipeDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ResourceAmount> Inputs { get; }
        public IReadOnlyList<ResourceAmount> Outputs { get; }
        public decimal LaborRequired { get; }
        public decimal PowerRequired { get; }
        public int DaysPerCycle { get; }

        public RecipeDefinition(
            string id,
            IReadOnlyList<ResourceAmount> inputs,
            IReadOnlyList<ResourceAmount> outputs,
            decimal laborRequired,
            decimal powerRequired,
            int daysPerCycle,
            string displayName = null)
        {
            Id = id;
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? id : displayName;
            Inputs = inputs;
            Outputs = outputs;
            LaborRequired = Math.Max(0.01m, laborRequired);
            PowerRequired = Math.Max(0.01m, powerRequired);
            DaysPerCycle = Math.Max(1, daysPerCycle);
        }
    }

    public sealed class ProductionContext
    {
        public Warehouse InputWarehouse { get; }
        public Warehouse OutputWarehouse { get; }
        public decimal AvailableWorkers { get; }
        public decimal AvailablePower { get; }

        public ProductionContext(
            Warehouse inputWarehouse,
            Warehouse outputWarehouse,
            decimal availableWorkers,
            decimal availablePower)
        {
            InputWarehouse = inputWarehouse;
            OutputWarehouse = outputWarehouse;
            AvailableWorkers = Math.Max(0, availableWorkers);
            AvailablePower = Math.Max(0, availablePower);
        }
    }

    public enum FactoryStatus
    {
        Operating,
        Shortage,
        NoPower,
        NoWorkers,
        Damaged,
        Maintenance,
        Disabled,
        WarehouseFull
    }

    public sealed class ProductionResult
    {
        public bool Produced { get; }
        public FactoryStatus Status { get; }
        public decimal Efficiency { get; }
        public IReadOnlyList<ResourceAmount> Outputs { get; }

        private ProductionResult(
            bool produced,
            FactoryStatus status,
            decimal efficiency,
            IReadOnlyList<ResourceAmount> outputs)
        {
            Produced = produced;
            Status = status;
            Efficiency = efficiency;
            Outputs = outputs;
        }

        public static ProductionResult Success(
            IReadOnlyList<ResourceAmount> outputs,
            decimal efficiency) =>
            new ProductionResult(true, FactoryStatus.Operating, efficiency, outputs);

        public static ProductionResult NoProduction(FactoryStatus status) =>
            new ProductionResult(false, status, 0, Array.Empty<ResourceAmount>());
    }

    public sealed class Factory
    {
        public FactoryId Id { get; }
        public CompanyId OwnerId { get; }
        public RegionId RegionId { get; }
        public RecipeDefinition Recipe { get; }

        public FactoryStatus Status { get; private set; }
        public decimal Condition { get; private set; }
        public decimal Efficiency { get; private set; }

        public Factory(
            FactoryId id,
            CompanyId ownerId,
            RegionId regionId,
            RecipeDefinition recipe)
        {
            Id = id;
            OwnerId = ownerId;
            RegionId = regionId;
            Recipe = recipe;
            Status = FactoryStatus.Operating;
            Condition = 1.0m;
            Efficiency = 1.0m;
        }

        public ProductionResult Produce(ProductionContext context)
        {
            if (Status == FactoryStatus.Damaged ||
                Status == FactoryStatus.Maintenance ||
                Status == FactoryStatus.Disabled)
            {
                return ProductionResult.NoProduction(Status);
            }

            decimal workerRatio =
                Math.Min(1.0m, context.AvailableWorkers / Recipe.LaborRequired);

            decimal powerRatio =
                Math.Min(1.0m, context.AvailablePower / Recipe.PowerRequired);

            Efficiency = Math.Clamp(
                Condition * workerRatio * powerRatio,
                0.0m,
                1.0m);

            if (Efficiency <= 0)
            {
                Status = workerRatio <= 0
                    ? FactoryStatus.NoWorkers
                    : FactoryStatus.NoPower;

                return ProductionResult.NoProduction(Status);
            }

            decimal cycles = Efficiency / Recipe.DaysPerCycle;

            decimal requiredOutputCapacity = 0m;
            foreach (var output in Recipe.Outputs)
                requiredOutputCapacity += output.Amount * cycles;

            if (requiredOutputCapacity >
                context.OutputWarehouse.AvailableCapacity)
            {
                Status = FactoryStatus.WarehouseFull;
                return ProductionResult.NoProduction(Status);
            }

            foreach (var input in Recipe.Inputs)
            {
                decimal required = input.Amount * cycles;
                if (context.InputWarehouse.GetAvailable(input.ResourceId) < required)
                {
                    Status = FactoryStatus.Shortage;
                    return ProductionResult.NoProduction(Status);
                }
            }

            foreach (var input in Recipe.Inputs)
            {
                decimal required = input.Amount * cycles;
                context.InputWarehouse.TryReserve(input.ResourceId, required);
            }

            foreach (var input in Recipe.Inputs)
            {
                decimal required = input.Amount * cycles;
                context.InputWarehouse.ConsumeReserved(input.ResourceId, required);
            }

            foreach (var output in Recipe.Outputs)
            {
                context.OutputWarehouse.TryAdd(
                    output.ResourceId,
                    output.Amount * cycles);
            }

            Status = FactoryStatus.Operating;
            return ProductionResult.Success(Recipe.Outputs, Efficiency);
        }

        public void ApplyDamage(decimal amount)
        {
            Condition = Math.Clamp(Condition - Math.Max(0, amount), 0, 1);
            if (Condition <= 0)
                Status = FactoryStatus.Disabled;
        }

        public void Repair(decimal amount)
        {
            Condition = Math.Clamp(Condition + Math.Max(0, amount), 0, 1);
            if (Condition > 0 && Status == FactoryStatus.Disabled)
                Status = FactoryStatus.Maintenance;
        }
    }
}
