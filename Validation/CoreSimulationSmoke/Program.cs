using System.Text;
using Game.Application.World;
using Game.Application.Turn;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Market;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;

Console.OutputEncoding = Encoding.UTF8;

var realtimeClock = new RealtimeSimulationClock(
    realSecondsPerGameDay: 60d,
    fixedRealStepSeconds: 1d,
    maxStepsPerAdvance: 100,
    initialSpeedMultiplier: 4);
RealtimeAdvanceResult realtimeDay = realtimeClock.Advance(15d);
Check(realtimeDay.CompletedGameDays == 1,
    "4배속에서 현실 15초마다 게임 하루 진행");
Check(GameCalendarDate.FromDayNumber(31).ToString() == "2월 1일" &&
      GameCalendarDate.FromDayNumber(360).ToString() == "12월 30일",
    "30일제 12개월 달력 변환");
foreach (WorldOpportunityKind kind in Enum.GetValues<WorldOpportunityKind>())
{
    IReadOnlyList<WorldOperationApproachProfile> approaches =
        WorldOperationCatalog.GetApproaches(kind);
    Check(approaches.Count == 3 &&
          approaches[0].Approach != approaches[1].Approach &&
          approaches[1].Approach != approaches[2].Approach,
        $"{kind} 경제 작전 해결 방식 3종");
}
Check(realtimeClock.CurrentDayNumber == 2,
    "실시간 날짜 증가");
realtimeClock.TogglePause();
Check(realtimeClock.Advance(60d).FixedStepCount == 0,
    "일시정지 중 게임 시간 고정");
realtimeClock.TogglePause();
Check(realtimeClock.SpeedMultiplier == 4,
    "일시정지 해제 시 기존 배속 복원");

var gridGenerator = new GridMapLayoutGenerator();
var playerMapStart = new GridCoordinate(4, 24);
GridMapLayout wrappedMap = gridGenerator.Generate(
    80,
    48,
    160,
    12345,
    playerMapStart,
    new[]
    {
        new GridCoordinate(44, 23),
        new GridCoordinate(30, 32),
        new GridCoordinate(57, 16)
    },
    true,
    neutralCastleCount: 8);
Check(wrappedMap.Width == 80 && wrappedMap.Height == 48,
    "대형 80×48 월드 생성");
Check(wrappedMap.Move(new GridCoordinate(79, 10), 1, 0).Equals(
      new GridCoordinate(0, 10)),
    "동쪽 끝에서 서쪽 끝으로 래핑");
Check(wrappedMap.ManhattanDistance(
          new GridCoordinate(0, 10),
          new GridCoordinate(79, 10)) == 1,
    "가로 래핑 최단 거리");
Check(wrappedMap.Mines.All(x => wrappedMap.IsLand(x.Coordinate)),
    "광산은 육지에만 배치");
Check(wrappedMap.NeutralCastles.Count == 8 &&
      wrappedMap.NeutralCastles.All(wrappedMap.IsLand),
    "중립 빈 성 8개를 육지에 배치");
Check(wrappedMap.NeutralCastles.All(castle =>
      wrappedMap.Mines.All(mine => !mine.Coordinate.Equals(castle))),
    "중립 빈 성과 광산 좌표 분리");
Check(wrappedMap.NeutralCastles.Any(castle =>
      IsCoastal(wrappedMap, castle)),
    "항구 역할용 해안 빈 성 최소 1개 확보");

var mapGameplay = new RealtimeMapGameplayService(
    wrappedMap,
    "player",
    new[] { "ai_1", "ai_2", "ai_3" },
    new MapGameplayTuning(
        fixedStepsPerMove: 1,
        fixedStepsToCapture: 2,
        aiDecisionIntervalSteps: 100));
Check(mapGameplay.TryCreateUnit(
        "player",
        out MapUnitState playerMapUnit,
        out _),
    "플레이어 지도 유닛 생성");
GridCoordinate wrappedDestination = playerMapStart;
for (int offset = 1; offset < wrappedMap.Width; offset++)
{
    GridCoordinate candidate = wrappedMap.Move(playerMapStart, -offset, 0);
    if (wrappedMap.IsLand(candidate))
    {
        wrappedDestination = candidate;
        break;
    }
}
Check(!wrappedDestination.Equals(playerMapStart),
    "플레이어 본사와 다른 육지 목적지 검색");
Check(mapGameplay.TryIssueMove(
        "player",
        playerMapUnit.Id,
        wrappedDestination,
        out _),
    "가로 래핑 지도 이동 명령");
mapGameplay.AdvanceFixedSteps(wrappedMap.Width * wrappedMap.Height);
Check(playerMapUnit.Coordinate.Equals(wrappedDestination),
    "실시간 고정 스텝 유닛 이동");

var captureTerrain = new GridTerrainKind[5 * 3];
Array.Fill(captureTerrain, GridTerrainKind.Plains);
var captureCoordinate = new GridCoordinate(4, 1);
var captureLayout = new GridMapLayout(
    5,
    3,
    77,
    new GridCoordinate(0, 1),
    Array.Empty<GridCoordinate>(),
    new[] { new MinePlacement(captureCoordinate, MineKind.Normal) },
    true,
    captureTerrain);
var captureGameplay = new RealtimeMapGameplayService(
    captureLayout,
    "player",
    tuning: new MapGameplayTuning(
        fixedStepsPerMove: 1,
        fixedStepsToCapture: 2,
        aiDecisionIntervalSteps: 100));
Check(captureGameplay.TryCreateUnit(
        "player",
        out MapUnitState captureUnit,
        out _),
    "점령용 유닛 생성");
Check(captureGameplay.TryIssueMove(
        "player",
        captureUnit.Id,
        captureCoordinate,
        out _),
    "좌우 래핑 광산 이동 명령");
captureGameplay.AdvanceFixedSteps(2);
Check(captureGameplay.FindMine(captureCoordinate).OwnerFactionId == "player",
    "광산 점령 및 소유권 변경");
Check(captureGameplay.FindMine(captureCoordinate).GuardUnitId ==
      captureUnit.Id,
    "광산 공식 경비대 1개 기록");
Check(!captureGameplay.CanCreateUnitAt(
        "player",
        captureCoordinate,
        out _),
    "광산 현지 징병 금지");
Check(captureGameplay.CreateDailyProduction()[0].IronAmount == 12m,
    "점령 광산의 일일 생산량 반영");

var aiCaptureTerrain = new GridTerrainKind[6 * 3];
Array.Fill(aiCaptureTerrain, GridTerrainKind.Plains);
var aiCaptureLayout = new GridMapLayout(
    6,
    3,
    78,
    new GridCoordinate(0, 1),
    new[] { new GridCoordinate(5, 1) },
    new[] { new MinePlacement(new GridCoordinate(4, 1), MineKind.Gold) },
    true,
    aiCaptureTerrain);
var aiCaptureGameplay = new RealtimeMapGameplayService(
    aiCaptureLayout,
    "player",
    new[] { "ai_1" },
    new MapGameplayTuning(
        fixedStepsPerMove: 1,
        fixedStepsToCapture: 2,
        aiDecisionIntervalSteps: 1));
aiCaptureGameplay.AdvanceFixedSteps(2);
Check(aiCaptureGameplay.FindMine(new GridCoordinate(4, 1)).OwnerFactionId ==
      "ai_1",
    "AI가 동일한 이동·점령 규칙 사용");

var castleTerrain = new GridTerrainKind[6 * 3];
Array.Fill(castleTerrain, GridTerrainKind.Plains);
var castleCoordinate = new GridCoordinate(4, 1);
var castleLayout = new GridMapLayout(
    6,
    3,
    79,
    new GridCoordinate(0, 1),
    new[] { new GridCoordinate(5, 1) },
    Array.Empty<MinePlacement>(),
    false,
    castleTerrain,
    new[] { castleCoordinate });
var castleGameplay = new RealtimeMapGameplayService(
    castleLayout,
    "player",
    new[] { "ai_1" },
    new MapGameplayTuning(
        fixedStepsPerMove: 1,
        aiDecisionIntervalSteps: 1000,
        fixedStepsToCaptureCastle: 1,
        fixedStepsToSiegeUndefendedCastle: 2));
Check(castleGameplay.TryCreateUnit(
        "ai_1",
        out MapUnitState castleDefender,
        out _),
    "AI 성 점령 부대 생성");
Check(castleGameplay.TryIssueCastleOccupation(
        "ai_1",
        castleDefender.Id,
        castleCoordinate,
        out _),
    "AI 빈 성 점령 명령");
castleGameplay.AdvanceFixedSteps(1);
MapCastleControlState controlledCastle =
    castleGameplay.FindCastle(castleCoordinate);
Check(controlledCastle.OwnerFactionId == "ai_1" &&
      controlledCastle.GarrisonUnitCount == 1,
    "빈 성 소유권과 주둔군 기록");
Check(castleGameplay.TryCreateUnit(
        "player",
        out MapUnitState castleAttacker,
        out _),
    "플레이어 공성 부대 생성");
Check(castleGameplay.TryIssueCastleOccupation(
        "player",
        castleAttacker.Id,
        castleCoordinate,
        out _),
    "적성 공성 명령");
castleGameplay.AdvanceFixedSteps(4);
Check(controlledCastle.IsUnderSiege &&
      controlledCastle.OwnerFactionId == "ai_1" &&
      controlledCastle.CaptureProgress == 0,
    "수비대가 있는 적성은 자동 점령 대신 공성 대상으로 유지");
Check(castleGameplay.TryIssueMove(
        "ai_1",
        castleDefender.Id,
        new GridCoordinate(5, 1),
        out _),
    "적성 수비대 철수");
castleGameplay.AdvanceFixedSteps(2);
Check(controlledCastle.OwnerFactionId == "player" &&
      controlledCastle.GarrisonUnitIds.Contains(castleAttacker.Id),
    "무방비 적성 공성 완료와 주둔군 갱신");
Check(castleGameplay.TrySetCastleRole(
        "player",
        castleCoordinate,
        MapCastleRole.IndustrialCity,
        out _),
    "점령 성 역할 선택");
Check(controlledCastle.Role == MapCastleRole.IndustrialCity,
    "성 역할 상태 저장");
Check(castleGameplay.TryCreateUnitAt(
        "player",
        castleCoordinate,
        UnitArchetype.Spearman,
        UnitWeaponType.Spear,
        ArmorClass.Light,
        out MapUnitState localCastleRecruit,
        out _),
    "점령 성의 지역 징집 인력으로 현지 징병");
Check(localCastleRecruit.Coordinate.Equals(castleCoordinate) &&
      controlledCastle.GarrisonUnitCount == 2,
    "현지 징병 부대의 성 주둔 기록");

var resources = new List<ResourceId>
{
    new ResourceId("food"),
    new ResourceId("wood"),
    new ResourceId("iron"),
    new ResourceId("coal"),
    new ResourceId("steel"),
    new ResourceId("medicine")
};
var definitions = new Dictionary<ResourceId, ResourceDefinition>();
foreach (ResourceId id in resources)
{
    definitions[id] = new ResourceDefinition(
        id,
        id.Value,
        id.Value == "steel" ? 80m : 40m,
        ResourceRarity.Common,
        1m,
        id.Value == "food");
}

var generation = new WorldGenerationSettings(
    regionCount: 6,
    factionCount: 3,
    settlementCount: 5,
    npcCount: 12,
    initialResourceSiteCount: 8);
var generator = new ProceduralWorldGenerator();
ProceduralWorldState first = generator.Generate(
    12345,
    "smoke",
    generation,
    resources);
ProceduralWorldState second = generator.Generate(
    12345,
    "smoke",
    generation,
    resources);
Check(first.Regions.Count == 6, "지역 수");
Check(first.Factions.Count == 3, "세력 수");
Check(first.Npcs.Count == 12, "NPC 수");
Check(first.ResourceSiteSeeds.Count == 8, "초기 자원지 수");
Check(first.Regions[0].Terrain == second.Regions[0].Terrain,
    "동일 시드 지형 재현");
Check(first.Relations[0].Score == second.Relations[0].Score,
    "동일 시드 외교 재현");

var stockDefinition = definitions[new ResourceId("iron")];
var stockState = new ResourceMarketState(stockDefinition.Id, 40m, 10m);
var stockMarket = new MarketManager(
    new SupplyDemandLedger(new MarketTuning()),
    new PriceCalculator());
stockMarket.ProcessMarketPhase(new GameDay(0), new[]
{
    new PhysicalFlow(
        first.Regions[0].Id,
        stockDefinition.Id,
        stockDefinition,
        stockState,
        5m,
        12m,
        0m)
});
Check(stockState.MarketStock == 3m, "물리 흐름의 시장재고 증감");
stockMarket.ProcessMarketPhase(new GameDay(1), new[]
{
    new PhysicalFlow(
        first.Regions[0].Id,
        stockDefinition.Id,
        stockDefinition,
        stockState,
        0m,
        10m,
        0m)
});
Check(stockState.UnmetDemand == 7m, "재고 소진 후 실제 미충족 수요");

var periodicWorld = new WorldEconomyState();
periodicWorld.RegisterMarket(new MarketRuntimeState(
    first.Regions[0].Id,
    stockDefinition,
    new ResourceMarketState(stockDefinition.Id, 40m, 100m),
    10m,
    10m));
var periodicSites = new ResourceSiteEventSystem(
    periodicWorld,
    new ResourceSiteEventSettings(
        5,
        100m,
        20m,
        0.5m,
        new[] { "iron" }));
ResourceSiteTurnReport turn5Sites = ResourceSiteTurnReport.Empty;
ResourceSiteTurnReport turn6Sites = ResourceSiteTurnReport.Empty;
for (int turn = 1; turn <= 6; turn++)
{
    ResourceSiteTurnReport siteReport = periodicSites.ProcessTurn(
        new TurnNumber(turn));
    if (turn == 5) turn5Sites = siteReport;
    if (turn == 6) turn6Sites = siteReport;
}
Check(turn5Sites.SpawnedSites.Count == 1 &&
      turn5Sites.Production[0].Output == 100m,
    "5턴 신규 자원지와 초기 생산량");
Check(turn6Sites.Production[0].Output == 50m,
    "신규 자원지 턴별 감쇠");

var extraction = new ResourceExtractionSite(
    "reserve_test",
    first.Regions[0].Id,
    new ResourceId("iron"),
    new TurnNumber(1),
    100m,
    20m,
    0.10m,
    1000m,
    1m,
    100m,
    100m,
    first.Factions[0].Id,
    ExtractionMethod.Surface);
decimal reserveBefore = extraction.RemainingReserve;
decimal output1 = extraction.Extract(new TurnNumber(1));
decimal output5 = extraction.Extract(new TurnNumber(5));
Check(extraction.RemainingReserve < reserveBefore, "채취 시 매장량 감소");
Check(output5 < output1 && output5 >= extraction.MinimumOutput,
    "생산량 감소와 최소 생산량");
decimal beforeDeepLayer = extraction.RemainingReserve;
extraction.DevelopDeepLayer(500m, 0.15m);
Check(extraction.RemainingReserve == beforeDeepLayer + 500m,
    "심층 자원 개발");

DamageProfile equalSlash = new DamageProfile(1m, 0m, 0m);
DamageProfile equalBlunt = new DamageProfile(0m, 0m, 1m);
Check(equalBlunt.ResolveAgainst(ArmorProfile.Heavy) >
      equalSlash.ResolveAgainst(ArmorProfile.Heavy),
    "둔기-중장갑 상호작용");

MilitaryBalanceCatalog balance =
    MilitaryBalanceCatalog.CreatePrototypeDefaults();
var archer = new MilitaryUnit(
    "archer",
    "a",
    balance.Get(UnitArchetype.Archer),
    new EquipmentLoadout("light", "경장비", ArmorProfile.Light),
    100,
    averageExperience: 80m);
decimal veteranExperience = archer.AverageExperience;
archer.Recruit(100);
Check(archer.AverageExperience < veteranExperience,
    "신병 충원에 따른 평균 숙련도 희석");
var militaryLogistics = new MilitaryLogisticsTuning();
Check(militaryLogistics.GetReplacementSpeed(1m) >
      militaryLogistics.GetReplacementSpeed(0m),
    "군수 보급에 따른 손실 보충 속도");

var attackers = new ArmyState("attackers", "a", first.Regions[0].Id);
attackers.AddUnit(archer);
attackers.AddUnit(new MilitaryUnit(
    "sword",
    "a",
    balance.Get(UnitArchetype.Swordsman),
    new EquipmentLoadout("light", "경장비", ArmorProfile.Light),
    120));
var defenders = new ArmyState("defenders", "b", first.Regions[1].Id);
defenders.AddUnit(new MilitaryUnit(
    "spear",
    "b",
    balance.Get(UnitArchetype.Spearman),
    new EquipmentLoadout("heavy", "중장비", ArmorProfile.Heavy),
    180));
BattleReport battle = new BattleResolver(
    militaryLogistics).Resolve(attackers, defenders, 77);
Check(battle.Phases.Any(x => x.Phase == BattlePhase.RangedApproach),
    "접근 전 원거리 공격 단계");
Check(battle.Phases.Any(x => x.Phase == BattlePhase.Melee),
    "접촉 후 근접전 단계");

var economy = new WorldEconomyState();
foreach (RegionalEconomySeed seed in first.EconomySeeds)
{
    ResourceDefinition definition = definitions[seed.ResourceId];
    economy.RegisterMarket(new MarketRuntimeState(
        seed.RegionId,
        definition,
        new ResourceMarketState(definition.Id, definition.BasePrice, 500m),
        12m * seed.SupplyMultiplier,
        18m * seed.DemandMultiplier));
}

var autonomousState = new AutonomousWorldState(first);
var tuning = new AutonomousWorldTuning(
    randomEventChancePerTurn: 1m,
    npcAutoResolveDelayTurns: 2,
    npcBaseSuccessChance: 0.95m,
    initialArmySoldiersPerFaction: 180);
var autonomous = new AutonomousWorldSimulationService(
    autonomousState,
    economy,
    tuning,
    balance);
var marketManager = new MarketManager(
    new SupplyDemandLedger(new MarketTuning()),
    new PriceCalculator());
var worldService = new WorldEconomyTurnService(
    economy,
    new WorldEconomyTuning(
        new Game.Domain.Economy.OperatingCostPolicy(
            0m, 0m, 0m, 0m, 0m, 100000m)),
    new CampaignRuleSet(),
    new ResourceSiteEventSettings(),
    autonomous);

bool sawArmyDemand = false;
bool sawMission = false;
for (int turn = 1; turn <= GameCalendarDate.DaysPerYear; turn++)
{
    IReadOnlyList<PhysicalFlow> flows = worldService.PrepareTurn(
        new TurnNumber(turn),
        new GameDay(turn - 1));
    sawArmyDemand |= flows.Any(x =>
        (x.ResourceId.Value == "food" || x.ResourceId.Value == "steel") &&
        x.Demand > 0m);
    MarketTickReport marketReport = marketManager.ProcessMarketPhase(
        new GameDay(turn - 1),
        flows);
    WorldTurnReport report = worldService.CompleteTurn(
        new TurnNumber(turn),
        new GameDay(turn - 1),
        marketReport);
    sawMission |= report.AutonomousWorld.OfferedOpportunities.Count > 0;
}

Check(sawArmyDemand, "군대가 시장에 군수 수요 생성");
Check(sawMission, "세계 사건이 개입 미션 생성");
Check(autonomousState.Opportunities.Any(x =>
        x.Status != WorldOpportunityStatus.Offered),
    "무시한 미션의 NPC 자동 처리");
Check(autonomousState.ResourceSites.Any(x =>
        x.RemainingReserve < x.TotalReserve),
    "자원지 생산이 실제 매장량 사용");
Check(economy.Markets.Any(x =>
        x.MarketState.CurrentPrice != x.Definition.BasePrice),
    "물리 수요·공급에 따른 지역 가격 변화");

Console.WriteLine(
    $"PASS | 지역 {first.Regions.Count}, 세력 {first.Factions.Count}, " +
    $"자원지 {autonomousState.ResourceSites.Count}, 사건 {autonomousState.Events.Count}, " +
    $"미션 {autonomousState.Opportunities.Count}, 군대 {autonomousState.Armies.Count}");

static void Check(bool condition, string name)
{
    if (!condition)
        throw new InvalidOperationException("검증 실패: " + name);
}

static bool IsCoastal(
    GridMapLayout layout,
    GridCoordinate coordinate)
{
    GridCoordinate[] offsets =
    {
        new GridCoordinate(1, 0),
        new GridCoordinate(-1, 0),
        new GridCoordinate(0, 1),
        new GridCoordinate(0, -1)
    };
    for (int i = 0; i < offsets.Length; i++)
    {
        try
        {
            GridCoordinate neighbor = layout.Move(
                coordinate,
                offsets[i].X,
                offsets[i].Y);
            if (!layout.IsLand(neighbor))
                return true;
        }
        catch (ArgumentOutOfRangeException)
        {
        }
    }

    return false;
}
