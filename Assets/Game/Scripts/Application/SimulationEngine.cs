using System;
using System.Collections.Generic;
using System.Diagnostics;
using Game.Application.Campaign;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Market;

namespace Game.Application
{
    public readonly struct TurnPerformanceMetrics
    {
        public double ElapsedMilliseconds { get; }
        public int PhysicalFlowCount { get; }
        public int TradeFillCount { get; }
        public int PriceChangeCount { get; }

        public TurnPerformanceMetrics(
            double elapsedMilliseconds,
            int physicalFlowCount,
            int tradeFillCount,
            int priceChangeCount)
        {
            ElapsedMilliseconds = elapsedMilliseconds;
            PhysicalFlowCount = physicalFlowCount;
            TradeFillCount = tradeFillCount;
            PriceChangeCount = priceChangeCount;
        }
    }

    public sealed class TurnResolutionResult
    {
        public MarketTickReport MarketReport { get; }
        public TurnPerformanceMetrics Performance { get; }
        public WorldTurnReport WorldReport { get; }

        public TurnResolutionResult(
            MarketTickReport marketReport,
            TurnPerformanceMetrics performance,
            WorldTurnReport worldReport)
        {
            MarketReport = marketReport;
            Performance = performance;
            WorldReport = worldReport;
        }
    }

    public sealed class TurnReport
    {
        public TurnNumber Turn { get; }
        public GameDay CalendarDay { get; }
        public MarketTickReport MarketReport { get; }
        public TurnPerformanceMetrics Performance { get; }
        public IReadOnlyList<TurnCommandResult> CommandResults { get; }
        public WorldTurnReport WorldReport { get; }
        public CampaignTurnResult CampaignResult { get; }

        public TurnReport(
            TurnNumber turn,
            GameDay calendarDay,
            MarketTickReport marketReport,
            TurnPerformanceMetrics performance,
            IReadOnlyList<TurnCommandResult> commandResults,
            WorldTurnReport worldReport,
            CampaignTurnResult campaignResult)
        {
            Turn = turn;
            CalendarDay = calendarDay;
            MarketReport = marketReport;
            Performance = performance;
            CommandResults = commandResults;
            WorldReport = worldReport;
            CampaignResult = campaignResult;
        }
    }

    public sealed class TurnResolutionOrchestrator
    {
        private readonly MarketManager _marketManager;
        private readonly IWorldTurnService _worldTurnService;

        public TurnResolutionOrchestrator(
            MarketManager marketManager,
            IWorldTurnService worldTurnService = null)
        {
            _marketManager = marketManager ??
                throw new ArgumentNullException(nameof(marketManager));
            _worldTurnService = worldTurnService;
        }

        public TurnResolutionResult ResolveTurn(
            TurnNumber turn,
            GameDay calendarDay,
            IReadOnlyList<PhysicalFlow> physicalFlows)
        {
            if (physicalFlows == null)
                throw new ArgumentNullException(nameof(physicalFlows));

            long startedAt = Stopwatch.GetTimestamp();

            IReadOnlyList<PhysicalFlow> resolvedFlows =
                _worldTurnService?.PrepareTurn(
                    turn,
                    calendarDay) ??
                physicalFlows;

            // 생산, 소비, 물류, 미션 결과를 물리 흐름으로 모은 뒤
            // 시장이 그 결과만 보고 거래와 가격을 정산한다.
            MarketTickReport marketReport =
                _marketManager.ProcessMarketPhase(
                    calendarDay,
                    resolvedFlows);

            WorldTurnReport worldReport =
                _worldTurnService?.CompleteTurn(
                    turn,
                    calendarDay,
                    marketReport);

            long elapsedTicks = Stopwatch.GetTimestamp() - startedAt;
            double elapsedMilliseconds =
                elapsedTicks * 1000.0 / Stopwatch.Frequency;

            var performance = new TurnPerformanceMetrics(
                elapsedMilliseconds,
                resolvedFlows.Count,
                marketReport.Fills.Count,
                marketReport.PriceChanges.Count);

            return new TurnResolutionResult(
                marketReport,
                performance,
                worldReport);
        }
    }

    public sealed class SimulationEngine
    {
        private readonly TurnResolutionOrchestrator _orchestrator;
        private readonly MarketManager _market;
        private readonly Func<TurnNumber, IReadOnlyList<PhysicalFlow>>
            _flowProvider;
        private readonly TurnRuleSet _rules;
        private readonly TurnCommandContext _commandContext;
        private readonly IAIResolutionService _aiResolution;
        private readonly CampaignSession _campaignSession;
        private readonly List<TurnCommandResult> _commandResultBuffer =
            new List<TurnCommandResult>(16);

        public TurnNumber CurrentTurn { get; private set; }
        public GameDay CurrentCalendarDay { get; private set; }
        public TurnPhase Phase { get; private set; }
        public TurnCommandQueue PlayerCommands { get; }
        public CampaignTurnResult CampaignResult =>
            _campaignSession?.LastResult;
        public bool IsCampaignFinished =>
            _campaignSession?.IsFinished ?? false;

        public event Action<TurnPhase> PhaseChanged;

        public SimulationEngine(
            TurnResolutionOrchestrator orchestrator,
            MarketManager market,
            Func<TurnNumber, IReadOnlyList<PhysicalFlow>> flowProvider,
            TurnRuleSet rules,
            TurnNumber initialTurn,
            GameDay initialCalendarDay,
            IAIResolutionService aiResolution = null,
            CampaignSession campaignSession = null)
        {
            _orchestrator = orchestrator ??
                throw new ArgumentNullException(nameof(orchestrator));
            _market = market ??
                throw new ArgumentNullException(nameof(market));
            _flowProvider = flowProvider ??
                throw new ArgumentNullException(nameof(flowProvider));
            _rules = rules ??
                throw new ArgumentNullException(nameof(rules));
            _commandContext = new TurnCommandContext(_market);
            _aiResolution = aiResolution ??
                NullAIResolutionService.Instance;
            _campaignSession = campaignSession;

            CurrentTurn = initialTurn;
            CurrentCalendarDay = initialCalendarDay;
            Phase = TurnPhase.PlayerPlanning;
            PlayerCommands = new TurnCommandQueue(
                _rules.MaxActionPoints);
        }

        public bool TryQueuePlayerCommand(
            ITurnCommand command,
            out string reason)
        {
            if (Phase != TurnPhase.PlayerPlanning)
            {
                reason = "계획 단계에서만 명령을 예약할 수 있습니다.";
                return false;
            }

            _commandContext.Turn = CurrentTurn;
            return PlayerCommands.TryQueue(
                command,
                _commandContext,
                out reason);
        }

        public bool TryCancelLastPlayerCommand()
        {
            return Phase == TurnPhase.PlayerPlanning &&
                PlayerCommands.TryCancelLast();
        }

        public TurnReport EndTurn()
        {
            return ResolveTurn(null);
        }

        public TurnReport EndAuthoritativeTurn(
            IReadOnlyList<ITurnCommand> authoritativeCommands)
        {
            if (authoritativeCommands == null)
                throw new ArgumentNullException(nameof(authoritativeCommands));
            if (PlayerCommands.Count != 0)
            {
                throw new InvalidOperationException(
                    "권위 서버 정산 중에는 로컬 명령 큐가 비어 있어야 합니다.");
            }

            return ResolveTurn(authoritativeCommands);
        }

        private TurnReport ResolveTurn(
            IReadOnlyList<ITurnCommand> authoritativeCommands)
        {
            if (IsCampaignFinished)
                throw new InvalidOperationException(
                    "캠페인이 종료되어 다음 턴을 진행할 수 없습니다.");

            if (Phase != TurnPhase.PlayerPlanning)
                throw new InvalidOperationException(
                    "계획 단계에서만 턴을 종료할 수 있습니다.");

            TurnNumber resolvedTurn = CurrentTurn;
            GameDay resolvedDay = CurrentCalendarDay;
            _commandContext.Turn = resolvedTurn;

            SetPhase(TurnPhase.PlayerResolution);
            if (authoritativeCommands == null)
            {
                PlayerCommands.ExecuteAll(
                    _commandContext,
                    _commandResultBuffer);
            }
            else
            {
                ExecuteAuthoritativeCommands(authoritativeCommands);
            }

            SetPhase(TurnPhase.AIResolution);
            _aiResolution.ResolveTurn(
                resolvedTurn,
                resolvedDay);

            SetPhase(TurnPhase.WorldResolution);
            TurnResolutionResult resolution =
                _orchestrator.ResolveTurn(
                    resolvedTurn,
                    resolvedDay,
                    _flowProvider(resolvedTurn));

            SetPhase(TurnPhase.CampaignResolution);
            CampaignTurnResult campaignResult =
                _campaignSession?.EvaluateTurn(resolvedTurn);

            SetPhase(TurnPhase.Report);
            var commandResults =
                new List<TurnCommandResult>(_commandResultBuffer);
            var report = new TurnReport(
                resolvedTurn,
                resolvedDay,
                resolution.MarketReport,
                resolution.Performance,
                commandResults,
                resolution.WorldReport,
                campaignResult);

            SetPhase(TurnPhase.Completed);
            if (!IsCampaignFinished)
            {
                CurrentTurn = CurrentTurn.Next();
                CurrentCalendarDay = CurrentCalendarDay.Add(
                    _rules.DaysPerTurn);
                SetPhase(TurnPhase.PlayerPlanning);
            }

            return report;
        }

        private void ExecuteAuthoritativeCommands(
            IReadOnlyList<ITurnCommand> commands)
        {
            _commandResultBuffer.Clear();

            for (int i = 0; i < commands.Count; i++)
            {
                ITurnCommand command = commands[i];
                if (command == null)
                {
                    _commandResultBuffer.Add(new TurnCommandResult(
                        "알 수 없는 명령",
                        false,
                        "명령이 없습니다."));
                    continue;
                }

                if (!command.CanExecute(_commandContext, out string reason))
                {
                    _commandResultBuffer.Add(new TurnCommandResult(
                        command.DisplayName,
                        false,
                        reason));
                    continue;
                }

                try
                {
                    command.Execute(_commandContext);
                    _commandResultBuffer.Add(new TurnCommandResult(
                        command.DisplayName,
                        true,
                        "서버 정산 완료"));
                }
                catch (Exception exception)
                {
                    _commandResultBuffer.Add(new TurnCommandResult(
                        command.DisplayName,
                        false,
                        exception.Message));
                }
            }
        }

        public void EndTurns(
            int turnCount,
            List<TurnReport> reports)
        {
            if (reports == null)
                throw new ArgumentNullException(nameof(reports));

            reports.Clear();

            for (int i = 0; i < turnCount; i++)
            {
                if (IsCampaignFinished)
                    break;

                reports.Add(EndTurn());
            }
        }

        private void SetPhase(TurnPhase phase)
        {
            Phase = phase;
            PhaseChanged?.Invoke(phase);
        }
    }
}
