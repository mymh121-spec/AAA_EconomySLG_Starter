using Game.Application.PvP;
using Game.Domain.Common;
using Game.Server;

var slots = new[]
{
    new PvpPlayerSlot(
        0,
        new PvpPlayerId("player_host"),
        new CompanyId("company_host"),
        "Host"),
    new PvpPlayerSlot(
        1,
        new PvpPlayerId("player_guest"),
        new CompanyId("company_guest"),
        "Guest")
};

AuthoritativeSimulationRuntime? runtime = null;
WorldStateResponse? initial = null;
MapUnitStateResponse? hostUnit = null;
MapUnitStateResponse? guestUnit = null;
MapMineStateResponse? mineTarget = null;
MapCastleStateResponse? neutralCastleTarget = null;
MapCastleStateResponse? enemyCapital = null;
MapCoordinateResponse? guestMoveTarget = null;
string matchId = string.Empty;

for (int seedIndex = 0; seedIndex < 100; seedIndex++)
{
    string candidateMatchId = $"map-authority-smoke-{seedIndex}";
    var candidate = new AuthoritativeSimulationRuntime(
        slots,
        candidateMatchId);
    WorldStateResponse candidateWorld =
        candidate.CreateWorldView(slots[0].CompanyId);
    MapUnitStateResponse candidateHost = candidateWorld.Map.Units.Single(
        unit => unit.OwnerCompanyId == slots[0].CompanyId.Value);
    MapUnitStateResponse candidateGuest = candidateWorld.Map.Units.Single(
        unit => unit.OwnerCompanyId == slots[1].CompanyId.Value);
    MapCastleStateResponse candidateEnemyCapital =
        candidateWorld.Map.Castles.Single(castle =>
            castle.IsCapital &&
            castle.OwnerCompanyId == slots[1].CompanyId.Value);

    MapMineStateResponse? candidateMine = candidateWorld.Map.Mines
        .Where(mine => string.IsNullOrEmpty(mine.OwnerCompanyId))
        .OrderBy(mine => Distance(candidateWorld.Map, candidateHost, mine.X, mine.Y))
        .FirstOrDefault(mine => IsAccepted(
            candidate,
            CreateMapCommand(
                candidateWorld,
                slots[0],
                candidateHost.UnitId,
                PvpCommandKind.OccupyResourceSite,
                mine.X,
                mine.Y)));
    MapCastleStateResponse? candidateNeutral =
        candidateWorld.Map.Castles
            .Where(castle =>
                !castle.IsCapital &&
                string.IsNullOrEmpty(castle.OwnerCompanyId))
            .OrderBy(castle => Distance(
                candidateWorld.Map,
                candidateHost,
                castle.X,
                castle.Y))
            .FirstOrDefault(castle => IsAccepted(
                candidate,
                CreateMapCommand(
                    candidateWorld,
                    slots[0],
                    candidateHost.UnitId,
                    PvpCommandKind.OccupyCastle,
                    castle.X,
                    castle.Y)));
    PvpCommandEnvelope enemyCapitalOrder = CreateMapCommand(
        candidateWorld,
        slots[0],
        candidateHost.UnitId,
        PvpCommandKind.OccupyCastle,
        candidateEnemyCapital.X,
        candidateEnemyCapital.Y);
    MapCoordinateResponse? candidateGuestMove =
        FindAdjacentLand(candidateWorld.Map, candidateGuest);

    if (candidateMine == null ||
        candidateNeutral == null ||
        candidateGuestMove == null ||
        !IsAccepted(candidate, enemyCapitalOrder))
    {
        continue;
    }

    runtime = candidate;
    initial = candidateWorld;
    hostUnit = candidateHost;
    guestUnit = candidateGuest;
    mineTarget = candidateMine;
    neutralCastleTarget = candidateNeutral;
    enemyCapital = candidateEnemyCapital;
    guestMoveTarget = candidateGuestMove;
    matchId = candidateMatchId;
    break;
}

Require(runtime != null, "reachable deterministic PvP map");
Require(initial != null, "initial world snapshot");
Require(hostUnit != null && guestUnit != null, "starting units");
Require(mineTarget != null, "reachable mine");
Require(neutralCastleTarget != null, "reachable neutral castle");
Require(enemyCapital != null, "enemy capital");
Require(guestMoveTarget != null, "guest movement target");

var packages = new List<PvpTurnPackage>();
var expectedHashes = new List<string>();
int commandSequence = 0;

Resolve(
    runtime,
    packages,
    expectedHashes,
    new[]
    {
        CreateMapCommand(
            Current(runtime),
            slots[0],
            hostUnit.UnitId,
            PvpCommandKind.OccupyResourceSite,
            mineTarget.X,
            mineTarget.Y,
            commandSequence++),
        CreateMapCommand(
            Current(runtime),
            slots[1],
            guestUnit.UnitId,
            PvpCommandKind.MoveUnit,
            guestMoveTarget.X,
            guestMoveTarget.Y,
            commandSequence++)
    });

AdvanceUntil(
    runtime,
    packages,
    expectedHashes,
    world => world.Map.Mines.Any(mine =>
        mine.X == mineTarget.X &&
        mine.Y == mineTarget.Y &&
        mine.OwnerCompanyId == slots[0].CompanyId.Value),
    120,
    "mine capture");

Resolve(
    runtime,
    packages,
    expectedHashes,
    new[]
    {
        CreateMapCommand(
            Current(runtime),
            slots[0],
            hostUnit.UnitId,
            PvpCommandKind.OccupyCastle,
            neutralCastleTarget.X,
            neutralCastleTarget.Y,
            commandSequence++)
    });
AdvanceUntil(
    runtime,
    packages,
    expectedHashes,
    world => world.Map.Castles.Any(castle =>
        castle.X == neutralCastleTarget.X &&
        castle.Y == neutralCastleTarget.Y &&
        castle.OwnerCompanyId == slots[0].CompanyId.Value),
    120,
    "neutral castle capture");

Resolve(
    runtime,
    packages,
    expectedHashes,
    new[]
    {
        CreateMapCommand(
            Current(runtime),
            slots[0],
            hostUnit.UnitId,
            PvpCommandKind.OccupyCastle,
            enemyCapital.X,
            enemyCapital.Y,
            commandSequence++)
    });
AdvanceUntil(
    runtime,
    packages,
    expectedHashes,
    world =>
    {
        MapUnitStateResponse unit = world.Map.Units.Single(
            item => item.UnitId == hostUnit.UnitId);
        MapCastleStateResponse capital = world.Map.Castles.Single(
            castle => castle.X == enemyCapital.X && castle.Y == enemyCapital.Y);
        return unit.X == enemyCapital.X &&
               unit.Y == enemyCapital.Y &&
               capital.ConflictKind == "Siege";
    },
    160,
    "enemy capital siege arrival");

PvpCommandEnvelope siege = CreateMapCommand(
    Current(runtime),
    slots[0],
    hostUnit.UnitId,
    PvpCommandKind.StartSiege,
    enemyCapital.X,
    enemyCapital.Y,
    commandSequence++,
    action: "Assault");
Require(
    runtime.ValidateCommand(siege, out string siegeReason) ==
        PvpOperationCode.Accepted,
    $"siege validation: {siegeReason}");
Resolve(runtime, packages, expectedHashes, new[] { siege });
AdvanceUntil(
    runtime,
    packages,
    expectedHashes,
    world => world.IsFinished,
    120,
    "capital siege campaign finish");

WorldStateResponse final = Current(runtime);
Require(final.IsFinished, "campaign finished after capital siege");
Require(
    final.WinnerCompanyId == slots[0].CompanyId.Value,
    "host wins after enemy capital destruction");
Require(
    final.Map.Castles.Single(castle =>
        castle.X == enemyCapital.X &&
        castle.Y == enemyCapital.Y).IsDestroyed,
    "enemy capital marked destroyed");

var replay = new AuthoritativeSimulationRuntime(slots, matchId);
for (int i = 0; i < packages.Count; i++)
{
    replay.Resolve(packages[i]);
    string replayHash = replay.ComputeStateHash(i + 1);
    Require(
        replayHash == expectedHashes[i],
        $"deterministic replay hash at package {i + 1}");
}

WorldStateResponse replayFinal = Current(replay);
Require(replayFinal.IsFinished, "replayed campaign finished");
Require(
    replayFinal.WinnerCompanyId == final.WinnerCompanyId,
    "replayed winner");

Console.WriteLine(
    "PASS PvpMapAuthoritySmoke " +
    $"match={matchId} turns={packages.Count} " +
    $"mine=({mineTarget.X},{mineTarget.Y}) " +
    $"castle=({neutralCastleTarget.X},{neutralCastleTarget.Y}) " +
    $"enemyCapital=({enemyCapital.X},{enemyCapital.Y}) " +
    $"winner={final.WinnerCompanyId}");

static WorldStateResponse Current(AuthoritativeSimulationRuntime runtime) =>
    runtime.CreateWorldView(new CompanyId("company_host"));

static bool IsAccepted(
    AuthoritativeSimulationRuntime runtime,
    PvpCommandEnvelope command) =>
    runtime.ValidateCommand(command, out _) == PvpOperationCode.Accepted;

static PvpCommandEnvelope CreateMapCommand(
    WorldStateResponse world,
    PvpPlayerSlot slot,
    string unitId,
    PvpCommandKind kind,
    int x,
    int y,
    int sequence = 0,
    string action = "")
{
    return new PvpCommandEnvelope(
        $"command_{world.Turn}_{slot.SlotIndex}_{sequence}_{kind}",
        new PvpMatchId("placeholder"),
        slot.PlayerId,
        slot.CompanyId,
        new TurnNumber(world.Turn),
        sequence,
        kind,
        new PvpCommandPayload(
            new RegionId("map"),
            targetId: unitId,
            targetX: x,
            targetY: y,
            action: action));
}

static void Resolve(
    AuthoritativeSimulationRuntime runtime,
    List<PvpTurnPackage> packages,
    List<string> expectedHashes,
    IReadOnlyList<PvpCommandEnvelope> commands)
{
    WorldStateResponse current = Current(runtime);
    var package = new PvpTurnPackage(
        new PvpMatchId("placeholder"),
        new TurnNumber(current.Turn),
        commands);
    runtime.Resolve(package);
    packages.Add(package);
    expectedHashes.Add(runtime.ComputeStateHash(packages.Count));
}

static void AdvanceUntil(
    AuthoritativeSimulationRuntime runtime,
    List<PvpTurnPackage> packages,
    List<string> expectedHashes,
    Func<WorldStateResponse, bool> condition,
    int maxTurns,
    string label)
{
    for (int i = 0; i < maxTurns; i++)
    {
        if (condition(Current(runtime)))
            return;
        Resolve(
            runtime,
            packages,
            expectedHashes,
            Array.Empty<PvpCommandEnvelope>());
    }

    throw new InvalidOperationException(
        $"Timed out while waiting for {label}.");
}

static int Distance(
    MapWorldStateResponse map,
    MapUnitStateResponse unit,
    int x,
    int y)
{
    int directX = Math.Abs(unit.X - x);
    int horizontal = map.WrapHorizontally
        ? Math.Min(directX, map.Width - directX)
        : directX;
    return horizontal + Math.Abs(unit.Y - y);
}

static MapCoordinateResponse? FindAdjacentLand(
    MapWorldStateResponse map,
    MapUnitStateResponse unit)
{
    (int X, int Y)[] offsets =
    {
        (1, 0),
        (-1, 0),
        (0, 1),
        (0, -1)
    };
    foreach ((int offsetX, int offsetY) in offsets)
    {
        int x = (unit.X + offsetX + map.Width) % map.Width;
        int y = unit.Y + offsetY;
        if (y < 0 || y >= map.Height)
            continue;
        if (map.Terrain[y * map.Width + x] != 0)
            return new MapCoordinateResponse(x, y);
    }
    return null;
}

static void Require(bool condition, string message)
{
    if (!condition)
        throw new InvalidOperationException($"FAILED: {message}");
}
