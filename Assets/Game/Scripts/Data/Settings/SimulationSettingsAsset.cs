using UnityEngine;
using Game.Domain.Campaign;
using Game.Domain.Economy;
using Game.Domain.Market;
using Game.Domain.Resources;

namespace Game.Data
{
    [CreateAssetMenu(
        fileName = "SimulationSettings",
        menuName = "게임/설정/시뮬레이션 설정")]
    public sealed class SimulationSettingsAsset : ScriptableObject
    {
        [Header("시장")]
        [SerializeField, Min(1f)] private float initialMarketStock = 1000f;
        [SerializeField, Min(1f)] private float targetStockDays = 14f;
        [SerializeField, Range(0f, 2f)] private float priceElasticity = 0.5f;
        [SerializeField, Range(0f, 2f)] private float stockWeight = 0.2f;
        [SerializeField, Range(0f, 2f)] private float tradeWeight = 0.1f;
        [SerializeField, Range(0f, 1f)] private float meanReversion = 0.05f;
        [SerializeField, Range(0.001f, 1f)] private float maxDailyPriceChange = 0.15f;
        [SerializeField, Min(1)] private int maxOrdersPerTurn = 10000;

        [Header("턴 규칙")]
        [SerializeField, Min(1)] private int maxActionPoints = 5;
        [SerializeField, Min(1)] private int daysPerTurn = 1;
        [SerializeField, Min(1)] private int turnsPerFrame = 4;
        [SerializeField, Min(1)] private int aiCompaniesPerBatch = 100;
        [SerializeField, Min(1)] private int aiActionsPerCompany = 2;

        [Header("캠페인 승패")]
        [SerializeField, Min(1)] private int maxCampaignTurns = 30;
        [SerializeField, Min(1)] private int dominanceCheckStartTurn = 15;
        [SerializeField, Min(1f)] private float dominanceMultiplier = 3f;
        [SerializeField, Min(1)] private int dominanceRequiredTurns = 2;
        [SerializeField, Min(0f)] private float recentProfitMultiplier = 5f;

        [Header("경제력 자산 평가")]
        [SerializeField, Min(0f)] private float factoryAssetValue = 10000f;
        [SerializeField, Min(0f)] private float warehouseAssetValue = 5000f;
        [SerializeField, Min(0f)] private float vehicleAssetValue = 2000f;

        [Header("회사 운영비")]
        [SerializeField, Min(0f)] private float factoryDailyCost = 100f;
        [SerializeField, Min(0f)] private float warehouseDailyCost = 30f;
        [SerializeField, Min(0f)] private float vehicleDailyCost = 20f;
        [SerializeField, Min(0f)] private float employeeDailyWage = 5f;
        [SerializeField, Min(0f)] private float dailyInterestRate = 0.001f;
        [SerializeField, Min(0f)] private float bankruptcyDebtLimit = 100000f;

        [Header("채굴지 이벤트")]
        [SerializeField, Min(1)] private int resourceSiteSpawnIntervalTurns = 5;
        [SerializeField, Min(0.01f)] private float resourceSiteInitialOutput = 100f;
        [SerializeField, Min(0.01f)] private float resourceSiteMinimumOutput = 20f;
        [SerializeField, Range(0f, 1f)] private float resourceSiteDeclineRatePerTurn = 0.10f;
        [SerializeField] private string[] resourceSiteResourceIds =
        {
            "iron",
            "coal",
            "wood",
            "oil"
        };

        public decimal InitialMarketStock => (decimal)initialMarketStock;
        public int MaxOrdersPerTurn => maxOrdersPerTurn;
        public int MaxActionPoints => maxActionPoints;
        public int DaysPerTurn => daysPerTurn;
        public int TurnsPerFrame => turnsPerFrame;
        public int AICompaniesPerBatch => aiCompaniesPerBatch;
        public int AIActionsPerCompany => aiActionsPerCompany;
        public int MaxCampaignTurns => maxCampaignTurns;
        public int DominanceCheckStartTurn => dominanceCheckStartTurn;
        public decimal FactoryAssetValue => (decimal)factoryAssetValue;
        public decimal WarehouseAssetValue => (decimal)warehouseAssetValue;
        public decimal VehicleAssetValue => (decimal)vehicleAssetValue;

        public MarketTuning CreateMarketTuning()
        {
            return new MarketTuning(
                (decimal)targetStockDays,
                (decimal)priceElasticity,
                (decimal)stockWeight,
                (decimal)tradeWeight,
                (decimal)meanReversion,
                (decimal)maxDailyPriceChange);
        }

        public OperatingCostPolicy CreateOperatingCostPolicy()
        {
            return new OperatingCostPolicy(
                (decimal)factoryDailyCost,
                (decimal)warehouseDailyCost,
                (decimal)vehicleDailyCost,
                (decimal)employeeDailyWage,
                (decimal)dailyInterestRate,
                (decimal)bankruptcyDebtLimit);
        }

        public CampaignRuleSet CreateCampaignRuleSet()
        {
            return new CampaignRuleSet(
                maxCampaignTurns,
                dominanceCheckStartTurn,
                (decimal)dominanceMultiplier,
                dominanceRequiredTurns,
                (decimal)recentProfitMultiplier);
        }

        public ResourceSiteEventSettings CreateResourceSiteEventSettings()
        {
            return new ResourceSiteEventSettings(
                resourceSiteSpawnIntervalTurns,
                (decimal)resourceSiteInitialOutput,
                (decimal)resourceSiteMinimumOutput,
                (decimal)resourceSiteDeclineRatePerTurn,
                resourceSiteResourceIds);
        }
    }
}
