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
    decimal LimitPrice);

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

public sealed record WorldStateResponse(
    int Turn,
    int CalendarDay,
    IReadOnlyList<MarketStateResponse> Markets,
    IReadOnlyList<PublicCompanyStateResponse> Companies,
    OwnCompanyStateResponse? OwnCompany,
    IReadOnlyList<ResourceSiteStateResponse> ResourceSites,
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
    string Token);

public readonly record struct AuthenticatedPlayer(
    string PlayerId,
    string CompanyId);
