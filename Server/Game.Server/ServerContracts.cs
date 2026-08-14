namespace Game.Server;

public sealed record HealthResponse(
    string Status,
    string Service,
    string Version,
    DateTimeOffset UtcTime);

public sealed record ApiError(string Code, string Message)
{
    public static ApiError Unauthorized() =>
        new("인증실패", "유효한 Bearer 토큰이 필요합니다.");
}

public sealed record CreateRoomRequest(
    string DisplayName,
    int MaxPlayers);

public sealed record JoinRoomRequest(string DisplayName);

public sealed record RoomPlayerResponse(
    int Slot,
    string PlayerId,
    string DisplayName,
    bool IsHost,
    bool Connected);

public sealed record RoomStateResponse(
    string RoomCode,
    string MatchId,
    string Status,
    int MaxPlayers,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset LastActivityUtc,
    IReadOnlyList<RoomPlayerResponse> Players);

public sealed record RoomSessionResponse(
    string RoomCode,
    string PlayerId,
    string CompanyId,
    string AccessToken,
    bool IsHost,
    RoomStateResponse Room);

public sealed record SubmitCommandRequest(
    string RequestId,
    int ProtocolVersion,
    string MatchId,
    int ExpectedRevision,
    string CommandId,
    int Turn,
    int Sequence,
    string Kind,
    string RegionId,
    string? ResourceId,
    string? TargetCompanyId,
    string? TargetId,
    decimal Quantity,
    decimal LimitPrice,
    int? TargetX,
    int? TargetY,
    string? Action);

public sealed record ReadyRequest(
    string RequestId,
    int ProtocolVersion,
    string MatchId,
    int Turn,
    int ExpectedRevision,
    int LastSequence);

public sealed record CommandResponse(
    string RequestId,
    bool Accepted,
    string Code,
    string Message,
    int ExpectedSequence,
    int Revision,
    int Turn,
    bool IsReplay = false);

public sealed record ReadyResponse(
    string RequestId,
    bool Accepted,
    string Code,
    string Message,
    int Revision,
    int Turn,
    bool TurnResolved,
    string CommandHash,
    string StateHash,
    string TurnDeadlineUtc,
    WorldStateResponse? World,
    bool IsReplay = false);

public sealed record PlayerStateResponse(
    int Slot,
    string PlayerId,
    string CompanyId,
    bool Connected,
    bool Ready,
    bool Eliminated,
    int SpentActionPoints,
    int ExpectedSequence);

public sealed record PendingCommandResponse(
    string CommandId,
    int Turn,
    int Sequence,
    string Kind,
    string RegionId,
    string? ResourceId,
    decimal Quantity,
    decimal LimitPrice);

public sealed record MarketStateResponse(
    string RegionId,
    string ResourceId,
    string DisplayName,
    decimal CurrentPrice,
    decimal Supply,
    decimal Demand,
    decimal MarketStock);

public sealed record PublicCompanyStateResponse(
    string CompanyId,
    string DisplayName,
    bool IsEliminated,
    decimal EconomicPower);

public sealed record InventoryStateResponse(
    string ResourceId,
    decimal OnHand,
    decimal Reserved);

public sealed record OwnCompanyStateResponse(
    string CompanyId,
    decimal Cash,
    decimal Debt,
    bool IsBankrupt,
    IReadOnlyList<InventoryStateResponse> Inventory);

public sealed record ResourceSiteStateResponse(
    string SiteId,
    string RegionId,
    string ResourceId,
    int DiscoveryTurn,
    decimal CurrentOutput,
    decimal MinimumOutput,
    bool IsActive);

public sealed record MapCoordinateResponse(int X, int Y);

public sealed record MapUnitStateResponse(
    string UnitId,
    string OwnerCompanyId,
    string Archetype,
    int X,
    int Y,
    int? DestinationX,
    int? DestinationY,
    int MovementProgress,
    int MovementStepsPerTile,
    int RemainingTiles,
    int Stamina,
    int MaxStamina,
    int Soldiers,
    decimal AttackPower,
    decimal DefensePower,
    decimal Morale,
    decimal Fatigue,
    IReadOnlyList<MapCoordinateResponse> PlannedPath);

public sealed record MapMineStateResponse(
    int X,
    int Y,
    string Kind,
    string OwnerCompanyId,
    string CapturingCompanyId,
    int CaptureProgress,
    int CaptureRequired);

public sealed record MapCastleStateResponse(
    int X,
    int Y,
    string OwnerCompanyId,
    string OriginalOwnerCompanyId,
    string CapturingCompanyId,
    bool IsCapital,
    bool IsDestroyed,
    string Role,
    string ConflictKind,
    string SiegeAction,
    string OccupationPolicy,
    int CaptureProgress,
    int CaptureRequired,
    int WallDurability,
    int MaxWallDurability,
    int FoodSupply,
    int MaxFoodSupply,
    int GarrisonUnitCount);

public sealed record MapWorldStateResponse(
    int Width,
    int Height,
    int Seed,
    bool WrapHorizontally,
    int FixedStepsPerTurn,
    int CurrentEconomicDay,
    IReadOnlyList<int> Terrain,
    IReadOnlyList<MapUnitStateResponse> Units,
    IReadOnlyList<MapMineStateResponse> Mines,
    IReadOnlyList<MapCastleStateResponse> Castles);

public sealed record WorldStateResponse(
    int Turn,
    int CalendarDay,
    IReadOnlyList<MarketStateResponse> Markets,
    IReadOnlyList<PublicCompanyStateResponse> Companies,
    OwnCompanyStateResponse? OwnCompany,
    IReadOnlyList<ResourceSiteStateResponse> ResourceSites,
    MapWorldStateResponse Map,
    bool IsFinished,
    string WinnerCompanyId);

public sealed record ReconnectResponse(
    string MatchId,
    string PlayerId,
    int Turn,
    string Phase,
    int Revision,
    string StateHash,
    string TurnDeadlineUtc,
    IReadOnlyList<PlayerStateResponse> Players,
    IReadOnlyList<PendingCommandResponse> OwnPendingCommands,
    WorldStateResponse World);

public sealed record ServerPlayerConfiguration(
    string PlayerId,
    string CompanyId,
    string DisplayName,
    string Token,
    bool TokenIsSha256Hash = false);

public readonly record struct AuthenticatedPlayer(
    string PlayerId,
    string CompanyId);
