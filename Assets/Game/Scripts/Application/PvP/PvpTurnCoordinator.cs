using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Application.PvP
{
    public sealed class PvpTurnCoordinator
    {
        private sealed class PlayerTurnState
        {
            public PvpPlayerSlot Slot;
            public bool IsConnected = true;
            public bool IsReady;
            public bool IsEliminated;
            public int SpentActionPoints;
            public int ExpectedSequence = 1;
            public int CommandCount;

            public void ResetForNextTurn()
            {
                IsReady = false;
                SpentActionPoints = 0;
                ExpectedSequence = 1;
                CommandCount = 0;
            }
        }

        private readonly PvpMatchRules _rules;
        private readonly IPvpCommandRulePolicy _commandPolicy;
        private readonly Dictionary<PvpPlayerId, PlayerTurnState>
            _playerById =
                new Dictionary<PvpPlayerId, PlayerTurnState>();
        private readonly List<PlayerTurnState> _players =
            new List<PlayerTurnState>(4);
        private readonly List<PvpCommandEnvelope> _pendingCommands =
            new List<PvpCommandEnvelope>(64);
        private readonly HashSet<string> _usedCommandIds =
            new HashSet<string>(StringComparer.Ordinal);

        public PvpMatchId MatchId { get; }
        public TurnNumber CurrentTurn { get; private set; }
        public PvpMatchPhase Phase { get; private set; }
        public int Revision { get; private set; }
        public string LastAuthoritativeStateHash { get; private set; }
        public int PendingCommandCount => _pendingCommands.Count;

        public PvpTurnCoordinator(
            PvpMatchId matchId,
            IReadOnlyList<PvpPlayerSlot> playerSlots,
            PvpMatchRules rules = null,
            IPvpCommandRulePolicy commandPolicy = null,
            TurnNumber? initialTurn = null)
        {
            if (string.IsNullOrWhiteSpace(matchId.Value))
                throw new ArgumentException("매치 ID가 필요합니다.", nameof(matchId));

            MatchId = matchId;
            _rules = rules ?? new PvpMatchRules();
            _commandPolicy = commandPolicy ??
                new DefaultPvpCommandRulePolicy();

            if (playerSlots == null ||
                playerSlots.Count < _rules.MinPlayers ||
                playerSlots.Count > _rules.MaxPlayers)
            {
                throw new ArgumentException(
                    $"PvP 참가자는 {_rules.MinPlayers}~" +
                    $"{_rules.MaxPlayers}명이어야 합니다.",
                    nameof(playerSlots));
            }

            var usedCompanies = new HashSet<CompanyId>();
            var usedSlots = new HashSet<int>();

            for (int i = 0; i < playerSlots.Count; i++)
            {
                PvpPlayerSlot slot = playerSlots[i] ??
                    throw new ArgumentException(
                        "PvP 플레이어 슬롯이 비어 있습니다.",
                        nameof(playerSlots));

                if (_playerById.ContainsKey(slot.PlayerId) ||
                    !usedCompanies.Add(slot.CompanyId) ||
                    !usedSlots.Add(slot.SlotIndex))
                {
                    throw new ArgumentException(
                        "플레이어, 회사 또는 슬롯이 중복되었습니다.",
                        nameof(playerSlots));
                }

                var state = new PlayerTurnState { Slot = slot };
                _playerById.Add(slot.PlayerId, state);
                _players.Add(state);
            }

            _players.Sort(ComparePlayerSlots);
            CurrentTurn = initialTurn ?? new TurnNumber(1);
            Phase = PvpMatchPhase.Planning;
            LastAuthoritativeStateHash = string.Empty;
        }

        public PvpOperationResult SubmitCommand(
            PvpCommandEnvelope command)
        {
            if (Phase == PvpMatchPhase.Finished)
                return new PvpOperationResult(PvpOperationCode.MatchFinished);
            if (Phase != PvpMatchPhase.Planning)
                return new PvpOperationResult(PvpOperationCode.NotPlanning);
            if (command == null)
                return new PvpOperationResult(PvpOperationCode.InvalidPayload);
            if (!command.MatchId.Equals(MatchId))
                return new PvpOperationResult(PvpOperationCode.WrongMatch);
            if (!command.Turn.Equals(CurrentTurn))
                return new PvpOperationResult(PvpOperationCode.WrongTurn);
            if (!_playerById.TryGetValue(
                command.PlayerId,
                out var player))
            {
                return new PvpOperationResult(PvpOperationCode.UnknownPlayer);
            }
            if (!player.IsConnected)
                return new PvpOperationResult(PvpOperationCode.PlayerDisconnected);
            if (player.IsEliminated)
                return new PvpOperationResult(PvpOperationCode.PlayerEliminated);
            if (player.IsReady)
                return new PvpOperationResult(PvpOperationCode.PlayerAlreadyReady);
            if (!player.Slot.CompanyId.Equals(command.CompanyId))
            {
                return new PvpOperationResult(
                    PvpOperationCode.CompanyOwnershipMismatch,
                    player.ExpectedSequence);
            }
            if (_usedCommandIds.Contains(command.CommandId))
            {
                return new PvpOperationResult(
                    PvpOperationCode.DuplicateCommand,
                    player.ExpectedSequence);
            }
            if (command.Sequence != player.ExpectedSequence)
            {
                return new PvpOperationResult(
                    PvpOperationCode.SequenceMismatch,
                    player.ExpectedSequence);
            }
            if (player.CommandCount >= _rules.MaxCommandsPerPlayer)
            {
                return new PvpOperationResult(
                    PvpOperationCode.CommandLimitExceeded,
                    player.ExpectedSequence);
            }

            PvpOperationCode payloadResult =
                _commandPolicy.ValidatePayload(command);
            if (payloadResult != PvpOperationCode.Accepted)
            {
                return new PvpOperationResult(
                    payloadResult,
                    player.ExpectedSequence);
            }

            int actionPointCost =
                _commandPolicy.GetActionPointCost(command);
            if (actionPointCost <= 0 ||
                player.SpentActionPoints + actionPointCost >
                    _rules.MaxActionPointsPerPlayer)
            {
                return new PvpOperationResult(
                    PvpOperationCode.InsufficientActionPoints,
                    player.ExpectedSequence);
            }

            _pendingCommands.Add(command);
            _usedCommandIds.Add(command.CommandId);
            player.SpentActionPoints += actionPointCost;
            player.CommandCount++;
            player.ExpectedSequence++;
            return PvpOperationResult.Accepted(
                player.ExpectedSequence);
        }

        public PvpOperationResult CancelLastCommand(
            PvpPlayerId playerId,
            string commandId)
        {
            if (Phase == PvpMatchPhase.Finished)
                return new PvpOperationResult(PvpOperationCode.MatchFinished);
            if (Phase != PvpMatchPhase.Planning)
                return new PvpOperationResult(PvpOperationCode.NotPlanning);
            if (!_playerById.TryGetValue(playerId, out var player))
                return new PvpOperationResult(PvpOperationCode.UnknownPlayer);
            if (player.IsReady)
                return new PvpOperationResult(PvpOperationCode.PlayerAlreadyReady);
            if (player.CommandCount <= 0)
                return new PvpOperationResult(PvpOperationCode.NoCommandsToCancel);

            int index = FindLastPlayerCommandIndex(playerId);
            if (index < 0)
                return new PvpOperationResult(PvpOperationCode.NoCommandsToCancel);

            PvpCommandEnvelope command = _pendingCommands[index];
            if (!string.Equals(
                command.CommandId,
                commandId,
                StringComparison.Ordinal))
            {
                return new PvpOperationResult(
                    PvpOperationCode.NotLastCommand,
                    player.ExpectedSequence);
            }

            int cost = _commandPolicy.GetActionPointCost(command);
            _pendingCommands.RemoveAt(index);
            _usedCommandIds.Remove(command.CommandId);
            player.SpentActionPoints = Math.Max(
                0,
                player.SpentActionPoints - cost);
            player.CommandCount--;
            player.ExpectedSequence = Math.Max(
                1,
                player.ExpectedSequence - 1);
            return PvpOperationResult.Accepted(
                player.ExpectedSequence);
        }

        public PvpOperationResult MarkReady(PvpPlayerId playerId)
        {
            if (Phase == PvpMatchPhase.Finished)
                return new PvpOperationResult(PvpOperationCode.MatchFinished);
            if (Phase != PvpMatchPhase.Planning)
                return new PvpOperationResult(PvpOperationCode.NotPlanning);
            if (!_playerById.TryGetValue(playerId, out var player))
                return new PvpOperationResult(PvpOperationCode.UnknownPlayer);
            if (!player.IsConnected)
                return new PvpOperationResult(PvpOperationCode.PlayerDisconnected);
            if (player.IsEliminated)
                return new PvpOperationResult(PvpOperationCode.PlayerEliminated);
            if (player.IsReady)
                return new PvpOperationResult(PvpOperationCode.PlayerAlreadyReady);

            player.IsReady = true;
            if (AreAllActivePlayersReady())
                Phase = PvpMatchPhase.Locked;

            return PvpOperationResult.Accepted(
                player.ExpectedSequence);
        }

        public PvpOperationResult TryBeginResolution(
            out PvpTurnPackage package)
        {
            package = null;

            if (Phase == PvpMatchPhase.Finished)
                return new PvpOperationResult(PvpOperationCode.MatchFinished);
            if (Phase != PvpMatchPhase.Locked)
                return new PvpOperationResult(PvpOperationCode.NotLocked);

            var sortedCommands =
                new List<PvpCommandEnvelope>(_pendingCommands);
            sortedCommands.Sort(CompareCommands);
            package = new PvpTurnPackage(
                MatchId,
                CurrentTurn,
                sortedCommands);
            Phase = PvpMatchPhase.Resolving;
            return PvpOperationResult.Accepted();
        }

        public PvpOperationResult CompleteResolution(
            string authoritativeStateHash,
            bool matchFinished)
        {
            if (Phase != PvpMatchPhase.Resolving)
                return new PvpOperationResult(PvpOperationCode.NotResolving);
            if (string.IsNullOrWhiteSpace(authoritativeStateHash))
                return new PvpOperationResult(PvpOperationCode.InvalidStateHash);

            LastAuthoritativeStateHash = authoritativeStateHash.Trim();
            Revision++;
            _pendingCommands.Clear();

            if (matchFinished)
            {
                Phase = PvpMatchPhase.Finished;
                return PvpOperationResult.Accepted();
            }

            CurrentTurn = CurrentTurn.Next();
            for (int i = 0; i < _players.Count; i++)
                _players[i].ResetForNextTurn();

            Phase = PvpMatchPhase.Planning;
            return PvpOperationResult.Accepted();
        }

        public PvpOperationResult SetConnected(
            PvpPlayerId playerId,
            bool connected)
        {
            if (!_playerById.TryGetValue(playerId, out var player))
                return new PvpOperationResult(PvpOperationCode.UnknownPlayer);

            player.IsConnected = connected;
            return PvpOperationResult.Accepted(
                player.ExpectedSequence);
        }

        public PvpOperationResult SetEliminated(
            PvpPlayerId playerId,
            bool eliminated)
        {
            if (!_playerById.TryGetValue(playerId, out var player))
                return new PvpOperationResult(PvpOperationCode.UnknownPlayer);

            player.IsEliminated = eliminated;
            if (Phase == PvpMatchPhase.Planning &&
                AreAllActivePlayersReady())
            {
                Phase = PvpMatchPhase.Locked;
            }

            return PvpOperationResult.Accepted(
                player.ExpectedSequence);
        }

        public PvpMatchSnapshot CreateSnapshot()
        {
            var players = new List<PvpPlayerSnapshot>(_players.Count);

            for (int i = 0; i < _players.Count; i++)
            {
                PlayerTurnState player = _players[i];
                players.Add(new PvpPlayerSnapshot(
                    player.Slot.SlotIndex,
                    player.Slot.PlayerId,
                    player.Slot.CompanyId,
                    player.IsConnected,
                    player.IsReady,
                    player.IsEliminated,
                    player.SpentActionPoints,
                    player.ExpectedSequence));
            }

            return new PvpMatchSnapshot(
                MatchId,
                CurrentTurn,
                Phase,
                Revision,
                LastAuthoritativeStateHash,
                players);
        }

        public IReadOnlyList<PvpCommandEnvelope> GetPendingCommands(
            PvpPlayerId playerId)
        {
            var result = new List<PvpCommandEnvelope>();

            for (int i = 0; i < _pendingCommands.Count; i++)
            {
                if (_pendingCommands[i].PlayerId.Equals(playerId))
                    result.Add(_pendingCommands[i]);
            }

            result.Sort(CompareCommands);
            return result;
        }

        private int FindLastPlayerCommandIndex(PvpPlayerId playerId)
        {
            for (int i = _pendingCommands.Count - 1; i >= 0; i--)
            {
                if (_pendingCommands[i].PlayerId.Equals(playerId))
                    return i;
            }

            return -1;
        }

        private bool AreAllActivePlayersReady()
        {
            int activePlayers = 0;

            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].IsEliminated)
                    continue;

                activePlayers++;
                if (!_players[i].IsReady)
                    return false;
            }

            return activePlayers > 0;
        }

        private static int ComparePlayerSlots(
            PlayerTurnState left,
            PlayerTurnState right)
        {
            return left.Slot.SlotIndex.CompareTo(
                right.Slot.SlotIndex);
        }

        private int CompareCommands(
            PvpCommandEnvelope left,
            PvpCommandEnvelope right)
        {
            int leftSlot = _playerById[left.PlayerId].Slot.SlotIndex;
            int rightSlot = _playerById[right.PlayerId].Slot.SlotIndex;
            int slot = leftSlot.CompareTo(rightSlot);
            if (slot != 0)
                return slot;

            int sequence = left.Sequence.CompareTo(right.Sequence);
            return sequence != 0
                ? sequence
                : string.CompareOrdinal(left.CommandId, right.CommandId);
        }
    }
}
