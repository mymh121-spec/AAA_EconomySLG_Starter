using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Game.Application;
using Game.Application.PvP;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Inventory;
using Game.Domain.Market;
using Game.Domain.Military;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Server;

public sealed class AuthoritativeSimulationRuntime
{
    public const int MapFixedStepsPerTurn = 10;
    private const int MapWidth = 80;
    private const int MapHeight = 48;
    private const int NeutralCastleCount = 8;
    private const int StartingSoldiersPerUnit = 300;
    private static readonly string[] ResourceIds =
    {
        "iron", "coal", "wood", "oil", "steel",
        "food", "medicine", "machine", "semiconductor"
    };

    private readonly SimulationEngine _simulation;
    private readonly WorldEconomyState _world;
    private readonly Dictionary<CompanyId, CampaignParticipantState>
        _participants = new();
    private readonly PvpMarketCommandTranslator _marketTranslator = new();
    private readonly CampaignRuleSet _campaignRules = new();
    private readonly EconomicPowerCalculator _powerCalculator = new();
    private readonly Dictionary<CompanyId, int> _dominanceTurns = new();
    private readonly IReadOnlyList<PvpPlayerSlot> _playerSlots;
    private readonly GridMapLayout _mapLayout;
    private readonly RealtimeMapGameplayService _mapGameplay;

    public TurnReport? LastTurnReport { get; private set; }
    public bool IsFinished { get; private set; }
    public string WinnerCompanyId { get; private set; } = string.Empty;

    public bool IsCompanyEliminated(CompanyId companyId)
    {
        return _participants.TryGetValue(companyId, out var participant) &&
            participant.IsEliminated;
    }

    public AuthoritativeSimulationRuntime(
        IReadOnlyList<PvpPlayerSlot> playerSlots,
        string matchId,
        TurnNumber? initialTurn = null)
    {
        if (playerSlots == null || playerSlots.Count < 2 || playerSlots.Count > 4)
            throw new ArgumentException("권위 서버 매치는 2~4명이 필요합니다.", nameof(playerSlots));
        if (string.IsNullOrWhiteSpace(matchId))
            throw new ArgumentException("권위 지도 생성을 위한 매치 ID가 필요합니다.", nameof(matchId));

        _playerSlots = playerSlots.ToArray();
        _world = new WorldEconomyState();
        var region = new RegionId("capital");
        var marketTuning = new MarketTuning();
        var market = new MarketManager(
            new SupplyDemandLedger(marketTuning),
            new PriceCalculator());

        RegisterMarkets(region);
        RegisterCompanies(playerSlots, region);
        (_mapLayout, _mapGameplay) = CreateAuthoritativeMap(
            playerSlots,
            matchId);

        var worldService = new WorldEconomyTurnService(
            _world,
            new WorldEconomyTuning(new OperatingCostPolicy(
                factoryCost: 100m,
                warehouseCost: 30m,
                vehicleCost: 20m,
                employeeWage: 5m,
                dailyInterestRate: 0.001m,
                bankruptcyDebtLimit: 100000m)),
            _campaignRules,
            new ResourceSiteEventSettings());

        _simulation = new SimulationEngine(
            new TurnResolutionOrchestrator(market, worldService),
            market,
            _ => Array.Empty<PhysicalFlow>(),
            new TurnRuleSet(maxActionPoints: 5, daysPerTurn: 1),
            initialTurn ?? new TurnNumber(1),
            new GameDay((initialTurn?.Value ?? 1) - 1));
    }

    public TurnReport Resolve(PvpTurnPackage package)
    {
        if (package == null)
            throw new ArgumentNullException(nameof(package));
        if (!_simulation.CurrentTurn.Equals(package.Turn))
            throw new InvalidOperationException("서버 경제 턴과 PvP 턴이 일치하지 않습니다.");

        var commands = new List<ITurnCommand>(package.Commands.Count);
        for (int i = 0; i < package.Commands.Count; i++)
        {
            PvpCommandEnvelope envelope = package.Commands[i];
            if (IsMapCommand(envelope.Kind))
            {
                if (!TryApplyMapCommand(envelope, out string mapReason))
                {
                    throw new InvalidOperationException(
                        $"권위 지도 명령 적용에 실패했습니다: " +
                        $"{envelope.Kind} ({mapReason})");
                }
                continue;
            }

            if (!_marketTranslator.TryCreateTurnCommand(
                    envelope,
                    out ITurnCommand command,
                    out PvpOperationCode code))
            {
                throw new InvalidOperationException(
                    $"지원되지 않는 권위 명령입니다: {envelope.Kind} ({code})");
            }

            commands.Add(command);
        }

        LastTurnReport = _simulation.EndAuthoritativeTurn(commands);
        _mapGameplay.AdvanceFixedSteps(MapFixedStepsPerTurn);
        _mapGameplay.AdvanceEconomicDay(out _);
        ApplyMapProductionToEconomy();
        SynchronizeMapCampaignState();
        EvaluateMatchEnd(LastTurnReport.Turn);
        return LastTurnReport;
    }

    public PvpOperationCode ValidateCommand(
        PvpCommandEnvelope command,
        out string reason)
    {
        reason = string.Empty;
        if (command == null)
        {
            reason = "명령이 필요합니다.";
            return PvpOperationCode.InvalidPayload;
        }
        if (!IsMapCommand(command.Kind))
            return PvpOperationCode.Accepted;

        MapUnitState? unit = _mapGameplay.FindUnit(command.Payload.TargetId);
        if (unit == null)
        {
            reason = "지도에서 명령 대상 부대를 찾을 수 없습니다.";
            return PvpOperationCode.InvalidPayload;
        }
        if (!string.Equals(
                unit.OwnerFactionId,
                command.CompanyId.Value,
                StringComparison.Ordinal))
        {
            reason = "다른 회사의 부대에는 명령할 수 없습니다.";
            return PvpOperationCode.CompanyOwnershipMismatch;
        }

        if (command.Kind == PvpCommandKind.CancelOrder)
        {
            return _mapGameplay.CanCancelMove(
                command.CompanyId.Value,
                unit.Id,
                out reason)
                ? PvpOperationCode.Accepted
                : PvpOperationCode.InvalidPayload;
        }

        if (!command.Payload.TargetX.HasValue ||
            !command.Payload.TargetY.HasValue)
        {
            reason = "지도 목표 좌표가 필요합니다.";
            return PvpOperationCode.InvalidPayload;
        }

        var coordinate = new GridCoordinate(
            command.Payload.TargetX.Value,
            command.Payload.TargetY.Value);
        if (!_mapLayout.TryNormalize(coordinate, out coordinate))
        {
            reason = "지도 목표 좌표가 범위를 벗어났습니다.";
            return PvpOperationCode.InvalidPayload;
        }

        switch (command.Kind)
        {
            case PvpCommandKind.MoveUnit:
                return _mapGameplay.CanIssueMove(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    out _,
                    out reason)
                    ? PvpOperationCode.Accepted
                    : PvpOperationCode.InvalidPayload;

            case PvpCommandKind.OccupyResourceSite:
                MapMineControlState? mine = _mapGameplay.FindMine(coordinate);
                if (mine == null)
                {
                    reason = "목표 좌표에 광산이 없습니다.";
                    return PvpOperationCode.InvalidPayload;
                }
                if (string.Equals(
                        mine.OwnerFactionId,
                        command.CompanyId.Value,
                        StringComparison.Ordinal))
                {
                    reason = "이미 우리 회사가 소유한 광산입니다.";
                    return PvpOperationCode.InvalidPayload;
                }
                if (unit.Coordinate.Equals(coordinate))
                    return PvpOperationCode.Accepted;
                return _mapGameplay.CanIssueMove(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    out _,
                    out reason)
                    ? PvpOperationCode.Accepted
                    : PvpOperationCode.InvalidPayload;

            case PvpCommandKind.OccupyCastle:
                return _mapGameplay.CanIssueCastleOccupation(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    out reason)
                    ? PvpOperationCode.Accepted
                    : PvpOperationCode.InvalidPayload;

            case PvpCommandKind.StartSiege:
                MapCastleControlState? castle =
                    _mapGameplay.FindCastle(coordinate);
                if (castle == null ||
                    string.IsNullOrWhiteSpace(castle.OwnerFactionId) ||
                    string.Equals(
                        castle.OwnerFactionId,
                        command.CompanyId.Value,
                        StringComparison.Ordinal) ||
                    !unit.Coordinate.Equals(coordinate))
                {
                    reason = "적 성에 도착한 아군 부대만 공성을 시작할 수 있습니다.";
                    return PvpOperationCode.InvalidPayload;
                }
                if (!TryParseSiegeAction(
                        command.Payload.Action,
                        out _))
                {
                    reason = "공성 행동은 Assault, Encirclement, Blockade, Negotiation 중 하나여야 합니다.";
                    return PvpOperationCode.InvalidPayload;
                }
                return PvpOperationCode.Accepted;

            default:
                reason = "지원되지 않는 지도 명령입니다.";
                return PvpOperationCode.UnsupportedRequest;
        }
    }

    public WorldStateResponse CreateWorldView(CompanyId viewerCompanyId)
    {
        var markets = _world.Markets
            .OrderBy(item => item.RegionId.Value, StringComparer.Ordinal)
            .ThenBy(item => item.Definition.Id.Value, StringComparer.Ordinal)
            .Select(item => new MarketStateResponse(
                item.RegionId.Value,
                item.Definition.Id.Value,
                item.Definition.DisplayName,
                item.MarketState.CurrentPrice,
                item.MarketState.DailySupply,
                item.MarketState.DailyDemand,
                item.MarketState.MarketStock))
            .ToArray();

        var companies = _world.Companies
            .OrderBy(item => item.Company.Id.Value, StringComparer.Ordinal)
            .Select(item => new PublicCompanyStateResponse(
                item.Company.Id.Value,
                item.Company.Name,
                item.CampaignState.IsEliminated,
                _powerCalculator.Calculate(item.CampaignState, _campaignRules)))
            .ToArray();

        CompanyEconomyRuntime? viewer = null;
        _world.TryGetCompany(viewerCompanyId, out viewer);
        OwnCompanyStateResponse? ownCompany = viewer == null
            ? null
            : new OwnCompanyStateResponse(
                viewer.Company.Id.Value,
                viewer.Company.Cash,
                viewer.Company.Debt,
                viewer.Company.IsBankrupt,
                viewer.PrimaryWarehouse.Stocks
                    .OrderBy(item => item.Key.Value, StringComparer.Ordinal)
                    .Select(item => new InventoryStateResponse(
                        item.Key.Value,
                        item.Value.OnHand,
                        item.Value.Reserved))
                    .ToArray());

        TurnNumber outputTurn = _simulation.CurrentTurn;
        var sites = _world.ResourceSites
            .OrderBy(item => item.Id, StringComparer.Ordinal)
            .Select(item => new ResourceSiteStateResponse(
                item.Id,
                item.RegionId.Value,
                item.ResourceId.Value,
                item.DiscoveryTurn.Value,
                item.GetOutput(outputTurn),
                item.MinimumOutput,
                item.IsActive))
            .ToArray();

        int visibleTurn = IsFinished && LastTurnReport != null
            ? LastTurnReport.Turn.Value
            : _simulation.CurrentTurn.Value;

        return new WorldStateResponse(
            visibleTurn,
            _simulation.CurrentCalendarDay.Value,
            markets,
            companies,
            ownCompany,
            sites,
            CreateMapView(),
            IsFinished,
            WinnerCompanyId);
    }

    private MapWorldStateResponse CreateMapView()
    {
        var units = _mapGameplay.Units
            .OrderBy(unit => unit.Id, StringComparer.Ordinal)
            .Select(unit => new MapUnitStateResponse(
                unit.Id,
                unit.OwnerFactionId,
                unit.Archetype.ToString(),
                unit.Coordinate.X,
                unit.Coordinate.Y,
                unit.Destination?.X,
                unit.Destination?.Y,
                unit.MovementProgress,
                _mapGameplay.GetRequiredMovementStepsPerTile(unit),
                unit.RemainingMovementTileCount,
                unit.Stamina,
                unit.MaxStamina,
                unit.Soldiers,
                unit.AttackPower,
                unit.DefensePower,
                unit.Morale,
                unit.Fatigue,
                unit.PlannedPath.Select(coordinate =>
                    new MapCoordinateResponse(
                        coordinate.X,
                        coordinate.Y)).ToArray()))
            .ToArray();

        var mines = _mapGameplay.Mines
            .OrderBy(mine => mine.Coordinate.Y)
            .ThenBy(mine => mine.Coordinate.X)
            .Select(mine => new MapMineStateResponse(
                mine.Coordinate.X,
                mine.Coordinate.Y,
                mine.Kind.ToString(),
                mine.OwnerFactionId,
                mine.CapturingFactionId,
                mine.CaptureProgress,
                _mapGameplay.FixedStepsToCapture))
            .ToArray();

        var castles = _mapGameplay.Castles
            .OrderBy(castle => castle.Coordinate.Y)
            .ThenBy(castle => castle.Coordinate.X)
            .Select(castle => new MapCastleStateResponse(
                castle.Coordinate.X,
                castle.Coordinate.Y,
                castle.OwnerFactionId,
                castle.OriginalOwnerFactionId,
                castle.CapturingFactionId,
                castle.IsCapital,
                castle.IsDestroyed,
                castle.Role.ToString(),
                castle.ConflictKind.ToString(),
                castle.SiegeAction.ToString(),
                castle.OccupationPolicy.ToString(),
                castle.CaptureProgress,
                _mapGameplay.GetCastleCaptureRequired(castle),
                castle.WallDurability,
                castle.MaxWallDurability,
                castle.FoodSupply,
                castle.MaxFoodSupply,
                castle.GarrisonUnitCount))
            .ToArray();

        return new MapWorldStateResponse(
            _mapLayout.Width,
            _mapLayout.Height,
            _mapLayout.Seed,
            _mapLayout.WrapHorizontally,
            MapFixedStepsPerTurn,
            _mapGameplay.CurrentEconomicDay,
            _mapLayout.Terrain.Select(terrain => (int)terrain).ToArray(),
            units,
            mines,
            castles);
    }

    public string ComputeStateHash(int revision)
    {
        var canonical = new StringBuilder(4096);
        Append(canonical, revision);
        Append(canonical, _simulation.CurrentTurn.Value);
        Append(canonical, _simulation.CurrentCalendarDay.Value);
        Append(canonical, IsFinished ? 1 : 0);
        Append(canonical, WinnerCompanyId);

        foreach (MarketRuntimeState market in _world.Markets
                     .OrderBy(item => item.RegionId.Value, StringComparer.Ordinal)
                     .ThenBy(item => item.Definition.Id.Value, StringComparer.Ordinal))
        {
            Append(canonical, market.RegionId.Value);
            Append(canonical, market.Definition.Id.Value);
            Append(canonical, market.MarketState.CurrentPrice);
            Append(canonical, market.MarketState.DailySupply);
            Append(canonical, market.MarketState.DailyDemand);
            Append(canonical, market.MarketState.MarketStock);
        }

        foreach (CompanyEconomyRuntime company in _world.Companies
                     .OrderBy(item => item.Company.Id.Value, StringComparer.Ordinal))
        {
            Append(canonical, company.Company.Id.Value);
            Append(canonical, company.Company.Cash);
            Append(canonical, company.Company.Debt);
            Append(canonical, company.Company.IsBankrupt ? 1 : 0);

            foreach (var stock in company.PrimaryWarehouse.Stocks
                         .OrderBy(item => item.Key.Value, StringComparer.Ordinal))
            {
                Append(canonical, stock.Key.Value);
                Append(canonical, stock.Value.OnHand);
                Append(canonical, stock.Value.Reserved);
            }
        }

        foreach (ResourceExtractionSite site in _world.ResourceSites
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            Append(canonical, site.Id);
            Append(canonical, site.RegionId.Value);
            Append(canonical, site.ResourceId.Value);
            Append(canonical, site.DiscoveryTurn.Value);
            Append(canonical, site.GetOutput(_simulation.CurrentTurn));
            Append(canonical, site.IsActive ? 1 : 0);
        }

        Append(canonical, _mapLayout.Seed);
        Append(canonical, _mapGameplay.CurrentEconomicDay);
        foreach (MapUnitState unit in _mapGameplay.Units
                     .OrderBy(item => item.Id, StringComparer.Ordinal))
        {
            Append(canonical, unit.Id);
            Append(canonical, unit.OwnerFactionId);
            Append(canonical, unit.Coordinate.X);
            Append(canonical, unit.Coordinate.Y);
            Append(canonical, unit.Destination?.X ?? int.MinValue);
            Append(canonical, unit.Destination?.Y ?? int.MinValue);
            Append(canonical, unit.MovementProgress);
            Append(canonical, unit.Stamina);
            Append(canonical, unit.Soldiers);
            Append(canonical, unit.Morale);
            Append(canonical, unit.Fatigue);
            Append(canonical, unit.PlannedPath.Count);
            for (int i = 0; i < unit.PlannedPath.Count; i++)
            {
                Append(canonical, unit.PlannedPath[i].X);
                Append(canonical, unit.PlannedPath[i].Y);
            }
        }

        foreach (MapMineControlState mine in _mapGameplay.Mines
                     .OrderBy(item => item.Coordinate.Y)
                     .ThenBy(item => item.Coordinate.X))
        {
            Append(canonical, mine.Coordinate.X);
            Append(canonical, mine.Coordinate.Y);
            Append(canonical, (int)mine.Kind);
            Append(canonical, mine.OwnerFactionId);
            Append(canonical, mine.CapturingFactionId);
            Append(canonical, mine.CaptureProgress);
            Append(canonical, mine.YieldMultiplier);
        }

        foreach (MapCastleControlState castle in _mapGameplay.Castles
                     .OrderBy(item => item.Coordinate.Y)
                     .ThenBy(item => item.Coordinate.X))
        {
            Append(canonical, castle.Coordinate.X);
            Append(canonical, castle.Coordinate.Y);
            Append(canonical, castle.OwnerFactionId);
            Append(canonical, castle.CapturingFactionId);
            Append(canonical, castle.IsCapital ? 1 : 0);
            Append(canonical, castle.IsDestroyed ? 1 : 0);
            Append(canonical, (int)castle.Role);
            Append(canonical, (int)castle.ConflictKind);
            Append(canonical, (int)castle.SiegeAction);
            Append(canonical, (int)castle.OccupationPolicy);
            Append(canonical, castle.CaptureProgress);
            Append(canonical, castle.WallDurability);
            Append(canonical, castle.FoodSupply);
            Append(canonical, castle.GarrisonUnitCount);
        }

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static (
        GridMapLayout Layout,
        RealtimeMapGameplayService Gameplay) CreateAuthoritativeMap(
        IReadOnlyList<PvpPlayerSlot> playerSlots,
        string matchId)
    {
        var playerStart = new GridCoordinate(8, MapHeight / 2);
        GridCoordinate[] anchors =
        {
            new(48, MapHeight / 2),
            new(28, 38),
            new(68, 10)
        };
        var opponentStarts = new List<GridCoordinate>(
            playerSlots.Count - 1);
        for (int i = 1; i < playerSlots.Count; i++)
            opponentStarts.Add(anchors[i - 1]);

        int seed = ComputeStableMapSeed(matchId);
        int mineCount = Math.Max(
            1,
            (int)Math.Round(
                MapWidth * MapHeight * 0.03m,
                MidpointRounding.AwayFromZero));
        GridMapLayout layout = new GridMapLayoutGenerator().Generate(
            MapWidth,
            MapHeight,
            mineCount,
            seed,
            playerStart,
            opponentStarts,
            wrapHorizontally: true,
            neutralCastleCount: NeutralCastleCount);
        var opponents = playerSlots
            .Skip(1)
            .Select(slot => slot.CompanyId.Value)
            .ToArray();
        var gameplay = new RealtimeMapGameplayService(
            layout,
            playerSlots[0].CompanyId.Value,
            opponents,
            new MapGameplayTuning(
                initialSoldiersPerUnit: StartingSoldiersPerUnit),
            enableAi: false);

        for (int i = 0; i < playerSlots.Count; i++)
        {
            PvpPlayerSlot slot = playerSlots[i];
            if (!gameplay.TryCreateUnit(
                    slot.CompanyId.Value,
                    UnitArchetype.Swordsman,
                    out _,
                    out string reason))
            {
                throw new InvalidOperationException(
                    $"PvP 시작 부대 생성 실패({slot.CompanyId.Value}): {reason}");
            }
        }

        return (layout, gameplay);
    }

    private bool TryApplyMapCommand(
        PvpCommandEnvelope command,
        out string reason)
    {
        MapUnitState unit = _mapGameplay.FindUnit(command.Payload.TargetId);
        if (unit == null)
        {
            reason = "지도 명령 대상 부대가 없습니다.";
            return false;
        }

        GridCoordinate coordinate = command.Payload.TargetX.HasValue &&
            command.Payload.TargetY.HasValue
            ? new GridCoordinate(
                command.Payload.TargetX.Value,
                command.Payload.TargetY.Value)
            : unit.Coordinate;

        switch (command.Kind)
        {
            case PvpCommandKind.MoveUnit:
                return _mapGameplay.TryIssueMove(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    out reason);

            case PvpCommandKind.OccupyResourceSite:
                if (unit.Coordinate.Equals(coordinate))
                {
                    reason = string.Empty;
                    return _mapGameplay.FindMine(coordinate) != null;
                }
                return _mapGameplay.TryIssueMove(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    out reason);

            case PvpCommandKind.OccupyCastle:
                return _mapGameplay.TryIssueCastleOccupation(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    out reason);

            case PvpCommandKind.StartSiege:
                if (!_mapGameplay.TryIssueCastleOccupation(
                        command.CompanyId.Value,
                        unit.Id,
                        coordinate,
                        out reason))
                {
                    return false;
                }
                if (!TryParseSiegeAction(
                        command.Payload.Action,
                        out MapSiegeAction action))
                {
                    reason = "올바른 공성 행동이 아닙니다.";
                    return false;
                }
                MapCastleControlState? siegeCastle =
                    _mapGameplay.FindCastle(coordinate);
                if (siegeCastle?.SiegeAction == action)
                {
                    reason = string.Empty;
                    return true;
                }
                return _mapGameplay.TrySetSiegeAction(
                    command.CompanyId.Value,
                    unit.Id,
                    coordinate,
                    action,
                    out reason);

            case PvpCommandKind.CancelOrder:
                return _mapGameplay.TryCancelMove(
                    command.CompanyId.Value,
                    unit.Id,
                    out reason);

            default:
                reason = "지원하지 않는 지도 명령입니다.";
                return false;
        }
    }

    private void ApplyMapProductionToEconomy()
    {
        IReadOnlyList<MapMineProductionRecord> production =
            _mapGameplay.CreateDailyProduction();
        for (int i = 0; i < production.Count; i++)
        {
            MapMineProductionRecord record = production[i];
            if (!_world.TryGetCompany(
                    new CompanyId(record.OwnerFactionId),
                    out CompanyEconomyRuntime? company) ||
                company == null)
            {
                continue;
            }

            if (record.CashAmount > 0m)
                company.Company.Receive(record.CashAmount);
            if (record.IronAmount > 0m)
            {
                company.PrimaryWarehouse.TryAdd(
                    new ResourceId("iron"),
                    record.IronAmount,
                    1m);
            }
        }
    }

    private void SynchronizeMapCampaignState()
    {
        for (int i = 0; i < _playerSlots.Count; i++)
        {
            PvpPlayerSlot slot = _playerSlots[i];
            CampaignParticipantState participant =
                _participants[slot.CompanyId];
            MapCastleControlState? capital =
                _mapGameplay.FindCapital(slot.CompanyId.Value);
            if (capital?.IsDestroyed == true && participant.IsCapitalStanding)
                participant.DestroyCapital();

            int mineCount = _mapGameplay.Mines.Count(mine =>
                string.Equals(
                    mine.OwnerFactionId,
                    slot.CompanyId.Value,
                    StringComparison.Ordinal));
            int castleCount = _mapGameplay.Castles.Count(castle =>
                !castle.IsDestroyed &&
                string.Equals(
                    castle.OwnerFactionId,
                    slot.CompanyId.Value,
                    StringComparison.Ordinal));
            participant.UpdateAssetValues(
                participant.InventoryValue,
                participant.FacilityValue,
                participant.LogisticsValue,
                mineCount * 25_000m + castleCount * 100_000m,
                participant.TechnologyValue,
                participant.UnpaidCosts);
        }
    }

    private static bool IsMapCommand(PvpCommandKind kind) =>
        kind is PvpCommandKind.MoveUnit or
            PvpCommandKind.OccupyResourceSite or
            PvpCommandKind.OccupyCastle or
            PvpCommandKind.StartSiege or
            PvpCommandKind.CancelOrder;

    private static bool TryParseSiegeAction(
        string value,
        out MapSiegeAction action) =>
        Enum.TryParse(value, ignoreCase: true, out action) &&
        action != MapSiegeAction.None;

    private static int ComputeStableMapSeed(string matchId)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(matchId));
        int value =
            hash[0] |
            hash[1] << 8 |
            hash[2] << 16 |
            hash[3] << 24;
        return value == int.MinValue
            ? int.MaxValue
            : Math.Abs(value);
    }

    private void RegisterMarkets(RegionId region)
    {
        for (int i = 0; i < ResourceIds.Length; i++)
        {
            string id = ResourceIds[i];
            decimal price = id switch
            {
                "iron" => 100m,
                "coal" => 80m,
                "wood" => 60m,
                "oil" => 180m,
                "steel" => 220m,
                "food" => 50m,
                "medicine" => 300m,
                "machine" => 500m,
                "semiconductor" => 1200m,
                _ => 100m
            };
            var definition = new ResourceDefinition(
                new ResourceId(id),
                ResourceDisplayName(id),
                price,
                ResourceRarity.Common,
                storageVolume: 1m,
                isPerishable: id == "food" || id == "medicine");
            var state = new ResourceMarketState(definition.Id, price, 1000m);
            _world.RegisterMarket(new MarketRuntimeState(
                region,
                definition,
                state,
                baseSupply: id == "iron" ? 80m : 70m,
                baseDemand: id == "food" ? 100m : 60m));
        }
    }

    private void RegisterCompanies(
        IReadOnlyList<PvpPlayerSlot> playerSlots,
        RegionId region)
    {
        for (int i = 0; i < playerSlots.Count; i++)
        {
            PvpPlayerSlot slot = playerSlots[i];
            var company = new Company(
                slot.CompanyId,
                slot.DisplayName,
                initialCash: 1_000_000m);
            var participant = new CampaignParticipantState(company, i == 0);
            var warehouse = new Warehouse(
                new WarehouseId($"warehouse_{slot.CompanyId.Value}"),
                slot.CompanyId,
                region,
                capacity: 20_000m);

            foreach (MarketRuntimeState market in _world.Markets)
            {
                decimal initialStock = market.Definition.Id.Value switch
                {
                    "iron" => 500m,
                    "coal" => 300m,
                    "wood" => 300m,
                    "food" => 200m,
                    _ => 50m
                };
                warehouse.TryAdd(
                    market.Definition.Id,
                    initialStock,
                    market.Definition.StorageVolume);
            }

            var runtime = new CompanyEconomyRuntime(
                participant,
                warehouse,
                employeeCount: 0,
                availableWorkers: 0m,
                availablePower: 0m);
            _world.RegisterCompany(runtime);
            _participants.Add(company.Id, participant);
            _dominanceTurns.Add(company.Id, 0);
        }
    }

    private void EvaluateMatchEnd(TurnNumber resolvedTurn)
    {
        var active = _participants.Values
            .Where(item => !item.IsEliminated)
            .ToArray();

        if (active.Length == 1)
        {
            Finish(active[0].Company.Id);
            return;
        }

        if (resolvedTurn.Value >= _campaignRules.DominanceCheckStartTurn)
        {
            for (int i = 0; i < active.Length; i++)
            {
                CampaignParticipantState candidate = active[i];
                decimal candidatePower = _powerCalculator.Calculate(
                    candidate,
                    _campaignRules);
                decimal opponentPower = 0m;

                for (int j = 0; j < active.Length; j++)
                {
                    if (i != j)
                    {
                        opponentPower += _powerCalculator.Calculate(
                            active[j],
                            _campaignRules);
                    }
                }

                bool dominates = opponentPower > 0m &&
                    candidatePower >= opponentPower * _campaignRules.DominanceMultiplier;
                _dominanceTurns[candidate.Company.Id] = dominates
                    ? _dominanceTurns[candidate.Company.Id] + 1
                    : 0;

                if (_dominanceTurns[candidate.Company.Id] >=
                    _campaignRules.DominanceRequiredConsecutiveTurns)
                {
                    Finish(candidate.Company.Id);
                    return;
                }
            }
        }

        if (resolvedTurn.Value < _campaignRules.MaxTurns)
            return;

        var ranking = active
            .Select(item => new
            {
                item.Company.Id,
                Power = _powerCalculator.Calculate(item, _campaignRules)
            })
            .OrderByDescending(item => item.Power)
            .ThenBy(item => item.Id.Value, StringComparer.Ordinal)
            .ToArray();

        if (ranking.Length > 0 &&
            (ranking.Length == 1 || ranking[0].Power > ranking[1].Power))
        {
            Finish(ranking[0].Id);
        }
        else
        {
            IsFinished = true;
            WinnerCompanyId = string.Empty;
        }
    }

    private void Finish(CompanyId winner)
    {
        IsFinished = true;
        WinnerCompanyId = winner.Value;
    }

    private static string ResourceDisplayName(string id) => id switch
    {
        "iron" => "철",
        "coal" => "석탄",
        "wood" => "목재",
        "oil" => "석유",
        "steel" => "강철",
        "food" => "식량",
        "medicine" => "의약품",
        "machine" => "기계",
        "semiconductor" => "반도체",
        _ => id
    };

    private static void Append(StringBuilder builder, string? value)
    {
        string safe = value ?? string.Empty;
        builder.Append(safe.Length).Append(':').Append(safe).Append('|');
    }

    private static void Append(StringBuilder builder, int value) =>
        Append(builder, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, decimal value) =>
        Append(builder, value.ToString(
            "0.############################",
            CultureInfo.InvariantCulture));
}
