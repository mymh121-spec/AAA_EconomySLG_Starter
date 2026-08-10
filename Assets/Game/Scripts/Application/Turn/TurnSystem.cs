using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Market;

namespace Game.Application.Turn
{
    public enum TurnPhase
    {
        PlayerPlanning,
        PlayerResolution,
        AIResolution,
        WorldResolution,
        CampaignResolution,
        Report,
        Completed
    }

    public sealed class TurnRuleSet
    {
        public int MaxActionPoints { get; }
        public int DaysPerTurn { get; }

        public TurnRuleSet(
            int maxActionPoints = 5,
            int daysPerTurn = 1)
        {
            MaxActionPoints = Math.Max(1, maxActionPoints);
            DaysPerTurn = Math.Max(1, daysPerTurn);
        }
    }

    public interface IAIResolutionService
    {
        void ResolveTurn(
            TurnNumber turn,
            GameDay calendarDay);
    }

    public sealed class NullAIResolutionService : IAIResolutionService
    {
        public static readonly NullAIResolutionService Instance =
            new NullAIResolutionService();

        private NullAIResolutionService()
        {
        }

        public void ResolveTurn(
            TurnNumber turn,
            GameDay calendarDay)
        {
            // MVP에서는 비어 있다. 이후 CompanyAI 어댑터를 주입한다.
        }
    }

    public sealed class TurnCommandContext
    {
        public MarketManager Market { get; }
        public TurnNumber Turn { get; internal set; }

        public TurnCommandContext(MarketManager market)
        {
            Market = market;
            Turn = new TurnNumber(1);
        }
    }

    public interface ITurnCommand
    {
        CompanyId ActorId { get; }
        string DisplayName { get; }
        int ActionPointCost { get; }

        bool CanExecute(
            TurnCommandContext context,
            out string reason);

        void Execute(TurnCommandContext context);
    }

    public readonly struct TurnCommandResult
    {
        public string DisplayName { get; }
        public bool Success { get; }
        public string Message { get; }

        public TurnCommandResult(
            string displayName,
            bool success,
            string message)
        {
            DisplayName = displayName;
            Success = success;
            Message = message;
        }
    }

    public sealed class TurnCommandQueue
    {
        private readonly List<ITurnCommand> _commands =
            new List<ITurnCommand>(16);

        public int MaxActionPoints { get; }
        public int SpentActionPoints { get; private set; }
        public int RemainingActionPoints =>
            MaxActionPoints - SpentActionPoints;
        public int Count => _commands.Count;

        public TurnCommandQueue(int maxActionPoints)
        {
            MaxActionPoints = Math.Max(1, maxActionPoints);
        }

        public bool TryQueue(
            ITurnCommand command,
            TurnCommandContext context,
            out string reason)
        {
            if (command == null)
            {
                reason = "명령이 없습니다.";
                return false;
            }

            if (command.ActionPointCost <= 0)
            {
                reason = "행동력 비용은 1 이상이어야 합니다.";
                return false;
            }

            if (command.ActionPointCost > RemainingActionPoints)
            {
                reason = "남은 행동력이 부족합니다.";
                return false;
            }

            if (!command.CanExecute(context, out reason))
                return false;

            _commands.Add(command);
            SpentActionPoints += command.ActionPointCost;
            reason = string.Empty;
            return true;
        }

        public bool TryCancelLast()
        {
            if (_commands.Count == 0)
                return false;

            int last = _commands.Count - 1;
            SpentActionPoints -= _commands[last].ActionPointCost;
            _commands.RemoveAt(last);
            return true;
        }

        public void ExecuteAll(
            TurnCommandContext context,
            List<TurnCommandResult> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();

            for (int i = 0; i < _commands.Count; i++)
            {
                var command = _commands[i];

                if (!command.CanExecute(context, out var reason))
                {
                    results.Add(new TurnCommandResult(
                        command.DisplayName,
                        false,
                        reason));
                    continue;
                }

                try
                {
                    command.Execute(context);
                    results.Add(new TurnCommandResult(
                        command.DisplayName,
                        true,
                        "실행 완료"));
                }
                catch (Exception exception)
                {
                    results.Add(new TurnCommandResult(
                        command.DisplayName,
                        false,
                        exception.Message));
                }
            }

            _commands.Clear();
            SpentActionPoints = 0;
        }
    }

    public sealed class SubmitMarketOrderTurnCommand : ITurnCommand
    {
        private readonly MarketOrder _order;

        public CompanyId ActorId => _order.CompanyId;
        public string DisplayName { get; }
        public int ActionPointCost { get; }

        public SubmitMarketOrderTurnCommand(
            MarketOrder order,
            string displayName,
            int actionPointCost = 1)
        {
            _order = order ??
                throw new ArgumentNullException(nameof(order));
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "시장 주문"
                : displayName;
            ActionPointCost = Math.Max(1, actionPointCost);
        }

        public bool CanExecute(
            TurnCommandContext context,
            out string reason)
        {
            if (context?.Market == null)
            {
                reason = "시장 시스템을 사용할 수 없습니다.";
                return false;
            }

            if (_order.RemainingQuantity <= 0)
            {
                reason = "주문 수량이 없습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public void Execute(TurnCommandContext context)
        {
            context.Market.SubmitOrder(_order);
        }
    }
}
