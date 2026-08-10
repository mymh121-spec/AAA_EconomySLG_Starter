using System;

namespace Game.Domain.Economy
{
    public readonly struct DailyOperatingCosts
    {
        public int FactoryCount { get; }
        public int WarehouseCount { get; }
        public int VehicleCount { get; }
        public int EmployeeCount { get; }

        public DailyOperatingCosts(
            int factoryCount,
            int warehouseCount,
            int vehicleCount,
            int employeeCount)
        {
            FactoryCount = Math.Max(0, factoryCount);
            WarehouseCount = Math.Max(0, warehouseCount);
            VehicleCount = Math.Max(0, vehicleCount);
            EmployeeCount = Math.Max(0, employeeCount);
        }
    }

    public sealed class OperatingCostPolicy
    {
        public decimal FactoryCost { get; }
        public decimal WarehouseCost { get; }
        public decimal VehicleCost { get; }
        public decimal EmployeeWage { get; }
        public decimal DailyInterestRate { get; }
        public decimal BankruptcyDebtLimit { get; }

        public OperatingCostPolicy(
            decimal factoryCost,
            decimal warehouseCost,
            decimal vehicleCost,
            decimal employeeWage,
            decimal dailyInterestRate,
            decimal bankruptcyDebtLimit)
        {
            FactoryCost = Math.Max(0, factoryCost);
            WarehouseCost = Math.Max(0, warehouseCost);
            VehicleCost = Math.Max(0, vehicleCost);
            EmployeeWage = Math.Max(0, employeeWage);
            DailyInterestRate = Math.Max(0, dailyInterestRate);
            BankruptcyDebtLimit = Math.Max(0, bankruptcyDebtLimit);
        }
    }

    public readonly struct DailyFinanceResult
    {
        public decimal OperatingCost { get; }
        public decimal Interest { get; }
        public decimal NewDebt { get; }
        public bool Bankrupt { get; }

        public DailyFinanceResult(
            decimal operatingCost,
            decimal interest,
            decimal newDebt,
            bool bankrupt)
        {
            OperatingCost = operatingCost;
            Interest = interest;
            NewDebt = newDebt;
            Bankrupt = bankrupt;
        }
    }

    public sealed class CompanyFinanceSystem
    {
        public DailyFinanceResult ProcessDay(
            Company company,
            DailyOperatingCosts counts,
            OperatingCostPolicy policy)
        {
            decimal operatingCost =
                counts.FactoryCount * policy.FactoryCost +
                counts.WarehouseCount * policy.WarehouseCost +
                counts.VehicleCount * policy.VehicleCost +
                counts.EmployeeCount * policy.EmployeeWage;

            decimal interest =
                company.Debt * policy.DailyInterestRate;

            decimal total = operatingCost + interest;
            decimal debtBefore = company.Debt;

            if (!company.TrySpend(total))
            {
                decimal availableCash = company.Cash;

                if (availableCash > 0)
                    company.TrySpend(availableCash);

                decimal shortfall = total - availableCash;

                if (shortfall > 0)
                    company.AddDebt(shortfall);
            }

            if (company.Debt >= policy.BankruptcyDebtLimit &&
                policy.BankruptcyDebtLimit > 0)
            {
                company.MarkBankrupt();
            }

            return new DailyFinanceResult(
                operatingCost,
                interest,
                company.Debt - debtBefore,
                company.IsBankrupt);
        }
    }
}
