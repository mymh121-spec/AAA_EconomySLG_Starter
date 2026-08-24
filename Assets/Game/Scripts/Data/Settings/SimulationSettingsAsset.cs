using UnityEngine;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Market;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;

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

        [Header("실시간 진행")]
        [SerializeField, Min(5f)] private float realSecondsPerGameDay = 60f;
        [SerializeField, Range(0.02f, 0.5f)]
        private float fixedRealtimeStepSeconds = 0.1f;
        [SerializeField, Range(1, 4)] private int initialGameSpeed = 1;
        [SerializeField, Range(1, 64)] private int maxRealtimeStepsPerFrame = 16;

        [Header("캠페인 승패")]
        [SerializeField, Min(1)] private int maxCampaignTurns =
            GameCalendarDate.DaysPerYear;
        [SerializeField, Min(1)] private int dominanceCheckStartTurn = 181;
        [SerializeField, Min(1f)] private float dominanceMultiplier = 3f;
        [SerializeField, Min(1)] private int dominanceRequiredTurns =
            GameCalendarDate.DaysPerMonth * 2;
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

        [Header("시드 기반 랜덤 세계")]
        [SerializeField] private int worldSeed = 12345;
        [SerializeField, Min(2)] private int generatedRegionCount = 6;
        [SerializeField, Min(2)] private int generatedFactionCount = 3;
        [SerializeField, Min(2)] private int generatedSettlementCount = 5;
        [SerializeField, Min(2)] private int generatedNpcCount = 12;
        [SerializeField, Min(1)] private int initialResourceSiteCount = 8;
        [SerializeField, Min(100)] private int minimumRegionPopulation = 800;
        [SerializeField, Min(100)] private int maximumRegionPopulation = 8000;
        [SerializeField, Min(100f)] private float initialResourceReserve = 12000f;
        [SerializeField, Min(0.1f)] private float generatedSiteOutput = 85f;
        [SerializeField, Min(0.1f)] private float generatedSiteMinimumOutput = 12f;
        [SerializeField, Range(0f, 0.5f)] private float generatedSiteDeclineRate = 0.015f;

        [Header("자율 세계 이벤트와 개입")]
        [SerializeField, Range(0f, 1f)] private float randomEventChancePerTurn = 0.28f;
        [SerializeField, Range(0.01f, 1f)] private float causalShortageThreshold = 0.20f;
        [SerializeField, Min(1)] private int npcAutoResolveDelayTurns = 3;
        [SerializeField, Range(0f, 1f)] private float npcBaseSuccessChance = 0.35f;
        [SerializeField, Range(0f, 1f)] private float playerInterventionEfficiency = 0.20f;
        [SerializeField, Min(0f)] private float playerBaseMissionReward = 2500f;
        [SerializeField, Min(0f)] private float playerReputationReward = 3f;
        [SerializeField, Range(0.05f, 1f)] private float eventProductionPenalty = 0.55f;
        [SerializeField, Range(1f, 3f)] private float bountifulProductionBonus = 1.30f;
        [SerializeField, Min(0f)] private float newVeinReserveBonus = 3000f;
        [SerializeField, Min(1)] private int maxCausalEventsPerTurn = 3;
        [SerializeField, Min(1)] private int repeatEventCooldownTurns = 3;

        [Header("군대와 군수 수요")]
        [SerializeField, Min(10)] private int initialArmySoldiersPerFaction = 180;
        [SerializeField, Min(0f)] private float soldierDailyWage = 0.8f;
        [SerializeField, Min(0f)] private float mobilizationCostPerSoldier = 0.5f;
        [SerializeField, Min(0f)] private float distanceSupplyCostPerSoldier = 0.02f;
        [SerializeField, Min(0f)] private float militaryFoodDemandPerSoldier = 0.03f;
        [SerializeField, Min(0f)] private float militaryEquipmentDemandPerSoldier = 0.004f;
        [SerializeField, Min(0f)] private float militaryMedicineDemandPerSoldier = 0.001f;

        public decimal InitialMarketStock => (decimal)initialMarketStock;
        public int MaxOrdersPerTurn => maxOrdersPerTurn;
        public int MaxActionPoints => maxActionPoints;
        public int DaysPerTurn => daysPerTurn;
        public int TurnsPerFrame => turnsPerFrame;
        public int AICompaniesPerBatch => aiCompaniesPerBatch;
        public int AIActionsPerCompany => aiActionsPerCompany;
        public double RealSecondsPerGameDay => realSecondsPerGameDay;
        public double FixedRealtimeStepSeconds => fixedRealtimeStepSeconds;
        public int InitialGameSpeed => initialGameSpeed;
        public int MaxRealtimeStepsPerFrame => maxRealtimeStepsPerFrame;
        public int MaxCampaignTurns => maxCampaignTurns;
        public int DominanceCheckStartTurn => dominanceCheckStartTurn;
        public int WorldSeed => worldSeed;
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

        public WorldGenerationSettings CreateWorldGenerationSettings()
        {
            return new WorldGenerationSettings(
                generatedRegionCount,
                generatedFactionCount,
                generatedSettlementCount,
                generatedNpcCount,
                initialResourceSiteCount,
                minimumRegionPopulation,
                maximumRegionPopulation,
                (decimal)initialResourceReserve,
                (decimal)generatedSiteOutput,
                (decimal)generatedSiteMinimumOutput,
                (decimal)generatedSiteDeclineRate);
        }

        public AutonomousWorldTuning CreateAutonomousWorldTuning()
        {
            var military = new MilitaryLogisticsTuning(
                (decimal)soldierDailyWage,
                (decimal)mobilizationCostPerSoldier,
                (decimal)distanceSupplyCostPerSoldier,
                (decimal)militaryFoodDemandPerSoldier,
                (decimal)militaryEquipmentDemandPerSoldier,
                (decimal)militaryMedicineDemandPerSoldier);

            return new AutonomousWorldTuning(
                (decimal)randomEventChancePerTurn,
                (decimal)causalShortageThreshold,
                npcAutoResolveDelayTurns,
                (decimal)npcBaseSuccessChance,
                (decimal)playerInterventionEfficiency,
                (decimal)playerBaseMissionReward,
                (decimal)playerReputationReward,
                (decimal)eventProductionPenalty,
                (decimal)bountifulProductionBonus,
                (decimal)newVeinReserveBonus,
                initialArmySoldiersPerFaction,
                military,
                maxCausalEventsPerTurn,
                repeatEventCooldownTurns);
        }
    }
}
