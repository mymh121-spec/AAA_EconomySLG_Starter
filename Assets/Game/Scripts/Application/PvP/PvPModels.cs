using System;
using System.Collections.Generic;
using Game.Application.Turn;
using Game.Domain.Common;
using Game.Domain.Market;

namespace Game.Application.PvP
{
    public readonly struct PvpMatchId : IEquatable<PvpMatchId>
    {
        public string Value { get; }

        public PvpMatchId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("매치 ID가 필요합니다.", nameof(value));

            Value = value.Trim();
        }

        public bool Equals(PvpMatchId other) => Value == other.Value;
        public override bool Equals(object obj) =>
            obj is PvpMatchId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value ?? string.Empty;
    }

    public readonly struct PvpPlayerId : IEquatable<PvpPlayerId>
    {
        public string Value { get; }

        public PvpPlayerId(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException("플레이어 ID가 필요합니다.", nameof(value));

            Value = value.Trim();
        }

        public bool Equals(PvpPlayerId other) => Value == other.Value;
        public override bool Equals(object obj) =>
            obj is PvpPlayerId other && Equals(other);
        public override int GetHashCode() => Value?.GetHashCode() ?? 0;
        public override string ToString() => Value ?? string.Empty;
    }

    public enum PvpMatchPhase
    {
        Lobby,
        Planning,
        Locked,
        Resolving,
        Finished
    }

    public enum PvpCommandKind
    {
        MarketBuy,
        MarketSell,
        ChangeProduction,
        DispatchShipment,
        StartResearch,
        StartMission,
        BuildFacility,
        Attack,
        Defend,
        MoveUnit,
        OccupyResourceSite,
        OccupyCastle,
        StartSiege,
        CancelOrder
    }

    public enum PvpOperationCode
    {
        Accepted,
        MatchFinished,
        NotPlanning,
        NotLocked,
        NotResolving,
        WrongMatch,
        WrongTurn,
        UnknownPlayer,
        PlayerDisconnected,
        PlayerEliminated,
        PlayerAlreadyReady,
        CompanyOwnershipMismatch,
        SequenceMismatch,
        DuplicateCommand,
        InvalidPayload,
        InsufficientActionPoints,
        CommandLimitExceeded,
        NoCommandsToCancel,
        NotLastCommand,
        InvalidStateHash,
        ProtocolMismatch,
        AuthenticationMismatch,
        StaleRevision,
        DuplicateRequestConflict,
        UnsupportedRequest
    }

    public readonly struct PvpOperationResult
    {
        public PvpOperationCode Code { get; }
        public int ExpectedSequence { get; }
        public bool Success => Code == PvpOperationCode.Accepted;

        public PvpOperationResult(
            PvpOperationCode code,
            int expectedSequence = 0)
        {
            Code = code;
            ExpectedSequence = expectedSequence;
        }

        public static PvpOperationResult Accepted(int expectedSequence = 0) =>
            new PvpOperationResult(
                PvpOperationCode.Accepted,
                expectedSequence);
    }

    public sealed class PvpMatchRules
    {
        public int MinPlayers { get; }
        public int MaxPlayers { get; }
        public int MaxActionPointsPerPlayer { get; }
        public int MaxCommandsPerPlayer { get; }

        public PvpMatchRules(
            int minPlayers = 2,
            int maxPlayers = 4,
            int maxActionPointsPerPlayer = 5,
            int maxCommandsPerPlayer = 16)
        {
            MinPlayers = Math.Max(2, minPlayers);
            MaxPlayers = Math.Max(MinPlayers, maxPlayers);
            MaxActionPointsPerPlayer = Math.Max(
                1,
                maxActionPointsPerPlayer);
            MaxCommandsPerPlayer = Math.Max(
                1,
                maxCommandsPerPlayer);
        }
    }

    public sealed class PvpPlayerSlot
    {
        public int SlotIndex { get; }
        public PvpPlayerId PlayerId { get; }
        public CompanyId CompanyId { get; }
        public string DisplayName { get; }

        public PvpPlayerSlot(
            int slotIndex,
            PvpPlayerId playerId,
            CompanyId companyId,
            string displayName)
        {
            if (slotIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(slotIndex));
            if (string.IsNullOrWhiteSpace(playerId.Value))
                throw new ArgumentException("플레이어 ID가 필요합니다.", nameof(playerId));
            if (string.IsNullOrWhiteSpace(companyId.Value))
                throw new ArgumentException("회사 ID가 필요합니다.", nameof(companyId));

            SlotIndex = slotIndex;
            PlayerId = playerId;
            CompanyId = companyId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? playerId.Value
                : displayName;
        }
    }

    public sealed class PvpCommandPayload
    {
        public RegionId RegionId { get; }
        public ResourceId? ResourceId { get; }
        public CompanyId? TargetCompanyId { get; }
        public string TargetId { get; }
        public decimal Quantity { get; }
        public decimal LimitPrice { get; }
        public int? TargetX { get; }
        public int? TargetY { get; }
        public string Action { get; }

        public PvpCommandPayload(
            RegionId regionId,
            ResourceId? resourceId = null,
            CompanyId? targetCompanyId = null,
            string targetId = null,
            decimal quantity = 0m,
            decimal limitPrice = 0m,
            int? targetX = null,
            int? targetY = null,
            string action = null)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            TargetCompanyId = targetCompanyId;
            TargetId = targetId ?? string.Empty;
            Quantity = quantity;
            LimitPrice = limitPrice;
            TargetX = targetX;
            TargetY = targetY;
            Action = action ?? string.Empty;
        }

        public static PvpCommandPayload MarketOrder(
            RegionId regionId,
            ResourceId resourceId,
            decimal quantity,
            decimal limitPrice)
        {
            return new PvpCommandPayload(
                regionId,
                resourceId,
                quantity: quantity,
                limitPrice: limitPrice);
        }
    }

    public sealed class PvpCommandEnvelope
    {
        public string CommandId { get; }
        public PvpMatchId MatchId { get; }
        public PvpPlayerId PlayerId { get; }
        public CompanyId CompanyId { get; }
        public TurnNumber Turn { get; }
        public int Sequence { get; }
        public PvpCommandKind Kind { get; }
        public PvpCommandPayload Payload { get; }

        public PvpCommandEnvelope(
            string commandId,
            PvpMatchId matchId,
            PvpPlayerId playerId,
            CompanyId companyId,
            TurnNumber turn,
            int sequence,
            PvpCommandKind kind,
            PvpCommandPayload payload)
        {
            if (string.IsNullOrWhiteSpace(commandId))
                throw new ArgumentException("명령 ID가 필요합니다.", nameof(commandId));

            CommandId = commandId.Trim();
            MatchId = matchId;
            PlayerId = playerId;
            CompanyId = companyId;
            Turn = turn;
            Sequence = sequence;
            Kind = kind;
            Payload = payload ??
                throw new ArgumentNullException(nameof(payload));
        }
    }

    public interface IPvpCommandRulePolicy
    {
        int GetActionPointCost(PvpCommandEnvelope command);
        PvpOperationCode ValidatePayload(PvpCommandEnvelope command);
    }

    public sealed class DefaultPvpCommandRulePolicy : IPvpCommandRulePolicy
    {
        public int GetActionPointCost(PvpCommandEnvelope command)
        {
            switch (command.Kind)
            {
                case PvpCommandKind.MarketBuy:
                case PvpCommandKind.MarketSell:
                case PvpCommandKind.ChangeProduction:
                case PvpCommandKind.DispatchShipment:
                case PvpCommandKind.StartResearch:
                    return 1;
                case PvpCommandKind.StartMission:
                case PvpCommandKind.OccupyResourceSite:
                case PvpCommandKind.OccupyCastle:
                    return 2;
                case PvpCommandKind.BuildFacility:
                case PvpCommandKind.Attack:
                case PvpCommandKind.Defend:
                case PvpCommandKind.StartSiege:
                    return 3;
                case PvpCommandKind.MoveUnit:
                case PvpCommandKind.CancelOrder:
                    return 1;
                default:
                    return int.MaxValue;
            }
        }

        public PvpOperationCode ValidatePayload(PvpCommandEnvelope command)
        {
            if (command == null || command.Payload == null)
                return PvpOperationCode.InvalidPayload;

            switch (command.Kind)
            {
                case PvpCommandKind.MarketBuy:
                case PvpCommandKind.MarketSell:
                    return command.Payload.ResourceId.HasValue &&
                        !string.IsNullOrWhiteSpace(
                            command.Payload.RegionId.Value) &&
                        command.Payload.Quantity > 0m &&
                        command.Payload.LimitPrice > 0m
                        ? PvpOperationCode.Accepted
                        : PvpOperationCode.InvalidPayload;

                case PvpCommandKind.Attack:
                    return command.Payload.TargetCompanyId.HasValue
                        ? PvpOperationCode.Accepted
                        : PvpOperationCode.InvalidPayload;

                case PvpCommandKind.MoveUnit:
                case PvpCommandKind.OccupyResourceSite:
                case PvpCommandKind.OccupyCastle:
                case PvpCommandKind.StartSiege:
                    return !string.IsNullOrWhiteSpace(command.Payload.TargetId) &&
                        command.Payload.TargetX.HasValue &&
                        command.Payload.TargetY.HasValue
                        ? PvpOperationCode.Accepted
                        : PvpOperationCode.InvalidPayload;

                case PvpCommandKind.CancelOrder:
                    return !string.IsNullOrWhiteSpace(command.Payload.TargetId)
                        ? PvpOperationCode.Accepted
                        : PvpOperationCode.InvalidPayload;

                default:
                    return !string.IsNullOrWhiteSpace(command.Payload.TargetId)
                        ? PvpOperationCode.Accepted
                        : PvpOperationCode.InvalidPayload;
            }
        }
    }

    public sealed class PvpTurnPackage
    {
        public PvpMatchId MatchId { get; }
        public TurnNumber Turn { get; }
        public IReadOnlyList<PvpCommandEnvelope> Commands { get; }
        public string CommandHash { get; }

        public PvpTurnPackage(
            PvpMatchId matchId,
            TurnNumber turn,
            IReadOnlyList<PvpCommandEnvelope> commands)
        {
            MatchId = matchId;
            Turn = turn;
            Commands = commands ?? Array.Empty<PvpCommandEnvelope>();
            CommandHash = PvpChecksum.ComputeTurnPackage(
                MatchId,
                Turn,
                Commands);
        }
    }

    public sealed class PvpPlayerSnapshot
    {
        public int SlotIndex { get; }
        public PvpPlayerId PlayerId { get; }
        public CompanyId CompanyId { get; }
        public bool IsConnected { get; }
        public bool IsReady { get; }
        public bool IsEliminated { get; }
        public int SpentActionPoints { get; }
        public int ExpectedSequence { get; }

        public PvpPlayerSnapshot(
            int slotIndex,
            PvpPlayerId playerId,
            CompanyId companyId,
            bool isConnected,
            bool isReady,
            bool isEliminated,
            int spentActionPoints,
            int expectedSequence)
        {
            SlotIndex = slotIndex;
            PlayerId = playerId;
            CompanyId = companyId;
            IsConnected = isConnected;
            IsReady = isReady;
            IsEliminated = isEliminated;
            SpentActionPoints = spentActionPoints;
            ExpectedSequence = expectedSequence;
        }
    }

    public sealed class PvpMatchSnapshot
    {
        public PvpMatchId MatchId { get; }
        public TurnNumber Turn { get; }
        public PvpMatchPhase Phase { get; }
        public int Revision { get; }
        public string LastAuthoritativeStateHash { get; }
        public IReadOnlyList<PvpPlayerSnapshot> Players { get; }

        public PvpMatchSnapshot(
            PvpMatchId matchId,
            TurnNumber turn,
            PvpMatchPhase phase,
            int revision,
            string lastAuthoritativeStateHash,
            IReadOnlyList<PvpPlayerSnapshot> players)
        {
            MatchId = matchId;
            Turn = turn;
            Phase = phase;
            Revision = revision;
            LastAuthoritativeStateHash =
                lastAuthoritativeStateHash ?? string.Empty;
            Players = players ?? Array.Empty<PvpPlayerSnapshot>();
        }
    }

    public sealed class PvpMarketCommandTranslator
    {
        public bool TryCreateTurnCommand(
            PvpCommandEnvelope envelope,
            out ITurnCommand command,
            out PvpOperationCode code)
        {
            command = null;

            if (envelope == null ||
                !envelope.Payload.ResourceId.HasValue ||
                string.IsNullOrWhiteSpace(
                    envelope.Payload.RegionId.Value) ||
                envelope.Payload.Quantity <= 0m ||
                envelope.Payload.LimitPrice <= 0m)
            {
                code = PvpOperationCode.InvalidPayload;
                return false;
            }

            OrderSide side;
            if (envelope.Kind == PvpCommandKind.MarketBuy)
                side = OrderSide.Buy;
            else if (envelope.Kind == PvpCommandKind.MarketSell)
                side = OrderSide.Sell;
            else
            {
                code = PvpOperationCode.InvalidPayload;
                return false;
            }

            var order = new MarketOrder(
                envelope.CommandId,
                envelope.CompanyId,
                envelope.Payload.RegionId,
                envelope.Payload.ResourceId.Value,
                side,
                side == OrderSide.Buy
                    ? OrderPurpose.ProductionInput
                    : OrderPurpose.Export,
                envelope.Payload.Quantity,
                envelope.Payload.LimitPrice,
                envelope.Turn.Value);

            command = new SubmitMarketOrderTurnCommand(
                order,
                side == OrderSide.Buy
                    ? "PvP 시장 구매"
                    : "PvP 시장 판매");
            code = PvpOperationCode.Accepted;
            return true;
        }
    }
}
