using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Economy;

namespace Game.Domain.Campaign
{
    public enum CampaignOutcome
    {
        InProgress,
        Victory,
        Defeat,
        Draw
    }

    public enum CampaignEndReason
    {
        None,
        Bankruptcy,
        CapitalDestroyed,
        EconomicDominance,
        LastCompanyStanding,
        TurnLimitVictory,
        TurnLimitDefeat,
        TurnLimitDraw
    }

    public sealed class CampaignRuleSet
    {
        public int MaxTurns { get; }
        public int DominanceCheckStartTurn { get; }
        public decimal DominanceMultiplier { get; }
        public int DominanceRequiredConsecutiveTurns { get; }
        public decimal RecentProfitMultiplier { get; }

        public CampaignRuleSet(
            int maxTurns = GameCalendarDate.DaysPerYear,
            int dominanceCheckStartTurn = 181,
            decimal dominanceMultiplier = 3m,
            int dominanceRequiredConsecutiveTurns =
                GameCalendarDate.DaysPerMonth * 2,
            decimal recentProfitMultiplier = 5m)
        {
            MaxTurns = Math.Max(1, maxTurns);
            DominanceCheckStartTurn = Math.Clamp(
                dominanceCheckStartTurn,
                1,
                MaxTurns);
            DominanceMultiplier = Math.Max(
                1m,
                dominanceMultiplier);
            DominanceRequiredConsecutiveTurns = Math.Max(
                1,
                dominanceRequiredConsecutiveTurns);
            RecentProfitMultiplier = Math.Max(
                0m,
                recentProfitMultiplier);
        }
    }

    public sealed class CampaignParticipantState
    {
        private readonly decimal[] _recentOperatingProfits =
            new decimal[3];
        private int _profitWriteIndex;
        private int _profitCount;

        public Company Company { get; }
        public bool IsPlayer { get; }
        public bool IsCapitalStanding { get; private set; }
        public decimal InventoryValue { get; private set; }
        public decimal FacilityValue { get; private set; }
        public decimal LogisticsValue { get; private set; }
        public decimal TerritoryValue { get; private set; }
        public decimal TechnologyValue { get; private set; }
        public decimal UnpaidCosts { get; private set; }
        public bool IsEliminated =>
            Company.IsBankrupt || !IsCapitalStanding;

        public decimal RecentAverageOperatingProfit
        {
            get
            {
                if (_profitCount == 0)
                    return 0m;

                decimal total = 0m;
                for (int i = 0; i < _profitCount; i++)
                    total += _recentOperatingProfits[i];

                return total / _profitCount;
            }
        }

        public CampaignParticipantState(
            Company company,
            bool isPlayer)
        {
            Company = company ??
                throw new ArgumentNullException(nameof(company));
            IsPlayer = isPlayer;
            IsCapitalStanding = true;
        }

        public void UpdateAssetValues(
            decimal inventoryValue,
            decimal facilityValue,
            decimal logisticsValue,
            decimal territoryValue,
            decimal technologyValue,
            decimal unpaidCosts = 0m)
        {
            InventoryValue = Math.Max(0m, inventoryValue);
            FacilityValue = Math.Max(0m, facilityValue);
            LogisticsValue = Math.Max(0m, logisticsValue);
            TerritoryValue = Math.Max(0m, territoryValue);
            TechnologyValue = Math.Max(0m, technologyValue);
            UnpaidCosts = Math.Max(0m, unpaidCosts);
        }

        public void RecordOperatingProfit(decimal operatingProfit)
        {
            _recentOperatingProfits[_profitWriteIndex] =
                operatingProfit;
            _profitWriteIndex =
                (_profitWriteIndex + 1) % _recentOperatingProfits.Length;
            _profitCount = Math.Min(
                _profitCount + 1,
                _recentOperatingProfits.Length);
        }

        public void DestroyCapital()
        {
            IsCapitalStanding = false;
        }
    }

    public readonly struct EconomicPowerSnapshot
    {
        public CompanyId CompanyId { get; }
        public string CompanyName { get; }
        public bool IsPlayer { get; }
        public bool IsEliminated { get; }
        public decimal EconomicPower { get; }

        public EconomicPowerSnapshot(
            CompanyId companyId,
            string companyName,
            bool isPlayer,
            bool isEliminated,
            decimal economicPower)
        {
            CompanyId = companyId;
            CompanyName = companyName;
            IsPlayer = isPlayer;
            IsEliminated = isEliminated;
            EconomicPower = economicPower;
        }
    }

    public sealed class EconomicPowerCalculator
    {
        public decimal Calculate(
            CampaignParticipantState participant,
            CampaignRuleSet rules)
        {
            if (participant == null)
                throw new ArgumentNullException(nameof(participant));
            if (rules == null)
                throw new ArgumentNullException(nameof(rules));

            decimal profitValue = Math.Max(
                0m,
                participant.RecentAverageOperatingProfit *
                rules.RecentProfitMultiplier);

            decimal assets =
                participant.Company.Cash +
                participant.InventoryValue +
                participant.FacilityValue +
                participant.LogisticsValue +
                participant.TerritoryValue +
                participant.TechnologyValue +
                profitValue;

            decimal liabilities =
                participant.Company.Debt +
                participant.UnpaidCosts;

            return Math.Max(0m, assets - liabilities);
        }
    }

    public sealed class CampaignTurnResult
    {
        public CampaignOutcome Outcome { get; }
        public CampaignEndReason EndReason { get; }
        public TurnNumber ResolvedTurn { get; }
        public decimal PlayerEconomicPower { get; }
        public decimal OpponentCombinedEconomicPower { get; }
        public int DominanceConsecutiveTurns { get; }
        public int DominanceCheckStartTurn { get; }
        public decimal DominanceMultiplier { get; }
        public int DominanceRequiredConsecutiveTurns { get; }
        public IReadOnlyList<EconomicPowerSnapshot> Rankings { get; }
        public bool IsFinished => Outcome != CampaignOutcome.InProgress;

        public CampaignTurnResult(
            CampaignOutcome outcome,
            CampaignEndReason endReason,
            TurnNumber resolvedTurn,
            decimal playerEconomicPower,
            decimal opponentCombinedEconomicPower,
            int dominanceConsecutiveTurns,
            int dominanceCheckStartTurn,
            decimal dominanceMultiplier,
            int dominanceRequiredConsecutiveTurns,
            IReadOnlyList<EconomicPowerSnapshot> rankings)
        {
            Outcome = outcome;
            EndReason = endReason;
            ResolvedTurn = resolvedTurn;
            PlayerEconomicPower = playerEconomicPower;
            OpponentCombinedEconomicPower =
                opponentCombinedEconomicPower;
            DominanceConsecutiveTurns =
                dominanceConsecutiveTurns;
            DominanceCheckStartTurn = dominanceCheckStartTurn;
            DominanceMultiplier = dominanceMultiplier;
            DominanceRequiredConsecutiveTurns =
                dominanceRequiredConsecutiveTurns;
            Rankings = rankings ??
                Array.Empty<EconomicPowerSnapshot>();
        }
    }

    public sealed class CampaignState
    {
        private readonly List<CampaignParticipantState> _participants;

        public IReadOnlyList<CampaignParticipantState> Participants =>
            _participants;
        public CampaignParticipantState Player { get; }
        public int DominanceConsecutiveTurns { get; internal set; }
        public CampaignTurnResult LastResult { get; internal set; }
        public bool IsFinished =>
            LastResult != null && LastResult.IsFinished;

        public CampaignState(
            IReadOnlyList<CampaignParticipantState> participants)
        {
            if (participants == null)
                throw new ArgumentNullException(nameof(participants));

            _participants = new List<CampaignParticipantState>(
                participants.Count);

            CampaignParticipantState player = null;
            for (int i = 0; i < participants.Count; i++)
            {
                var participant = participants[i] ??
                    throw new ArgumentException(
                        "캠페인 참가자는 비어 있을 수 없습니다.",
                        nameof(participants));

                _participants.Add(participant);

                if (!participant.IsPlayer)
                    continue;

                if (player != null)
                {
                    throw new ArgumentException(
                        "플레이어 회사는 하나만 등록할 수 있습니다.",
                        nameof(participants));
                }

                player = participant;
            }

            Player = player ??
                throw new ArgumentException(
                    "플레이어 회사가 필요합니다.",
                    nameof(participants));
        }

        public CampaignParticipantState FindParticipant(
            CompanyId companyId)
        {
            for (int i = 0; i < _participants.Count; i++)
            {
                if (_participants[i].Company.Id.Equals(companyId))
                    return _participants[i];
            }

            return null;
        }
    }

    public sealed class CampaignVictoryEvaluator
    {
        private static readonly Comparison<EconomicPowerSnapshot>
            RankingComparison = CompareRankings;

        private readonly CampaignRuleSet _rules;
        private readonly EconomicPowerCalculator _powerCalculator;
        private readonly List<EconomicPowerSnapshot> _rankingBuffer =
            new List<EconomicPowerSnapshot>(16);

        public CampaignVictoryEvaluator(
            CampaignRuleSet rules,
            EconomicPowerCalculator powerCalculator = null)
        {
            _rules = rules ??
                throw new ArgumentNullException(nameof(rules));
            _powerCalculator = powerCalculator ??
                new EconomicPowerCalculator();
        }

        public CampaignTurnResult Evaluate(
            TurnNumber resolvedTurn,
            CampaignState state)
        {
            if (state == null)
                throw new ArgumentNullException(nameof(state));
            if (state.IsFinished)
                return state.LastResult;

            BuildRankings(state);

            EconomicPowerSnapshot player = FindPlayerRanking();
            decimal opponentTotal = CalculateOpponentTotal();

            if (!state.Player.IsCapitalStanding)
            {
                return Finish(
                    state,
                    CampaignOutcome.Defeat,
                    CampaignEndReason.CapitalDestroyed,
                    resolvedTurn,
                    player.EconomicPower,
                    opponentTotal);
            }

            if (state.Player.Company.IsBankrupt)
            {
                return Finish(
                    state,
                    CampaignOutcome.Defeat,
                    CampaignEndReason.Bankruptcy,
                    resolvedTurn,
                    player.EconomicPower,
                    opponentTotal);
            }

            if (CountActiveOpponents() == 0)
            {
                return Finish(
                    state,
                    CampaignOutcome.Victory,
                    CampaignEndReason.LastCompanyStanding,
                    resolvedTurn,
                    player.EconomicPower,
                    opponentTotal);
            }

            bool dominanceEligible =
                resolvedTurn.Value >=
                    _rules.DominanceCheckStartTurn &&
                opponentTotal > 0m &&
                player.EconomicPower >=
                    opponentTotal * _rules.DominanceMultiplier;

            state.DominanceConsecutiveTurns = dominanceEligible
                ? state.DominanceConsecutiveTurns + 1
                : 0;

            if (state.DominanceConsecutiveTurns >=
                _rules.DominanceRequiredConsecutiveTurns)
            {
                return Finish(
                    state,
                    CampaignOutcome.Victory,
                    CampaignEndReason.EconomicDominance,
                    resolvedTurn,
                    player.EconomicPower,
                    opponentTotal);
            }

            if (resolvedTurn.Value >= _rules.MaxTurns)
            {
                return FinishByRanking(
                    state,
                    resolvedTurn,
                    player.EconomicPower,
                    opponentTotal);
            }

            var result = CreateResult(
                CampaignOutcome.InProgress,
                CampaignEndReason.None,
                resolvedTurn,
                player.EconomicPower,
                opponentTotal,
                state.DominanceConsecutiveTurns);
            state.LastResult = result;
            return result;
        }

        private CampaignTurnResult FinishByRanking(
            CampaignState state,
            TurnNumber resolvedTurn,
            decimal playerPower,
            decimal opponentTotal)
        {
            decimal strongestOpponent = 0m;

            for (int i = 0; i < _rankingBuffer.Count; i++)
            {
                var ranking = _rankingBuffer[i];
                if (!ranking.IsPlayer && !ranking.IsEliminated)
                {
                    strongestOpponent = Math.Max(
                        strongestOpponent,
                        ranking.EconomicPower);
                }
            }

            if (playerPower > strongestOpponent)
            {
                return Finish(
                    state,
                    CampaignOutcome.Victory,
                    CampaignEndReason.TurnLimitVictory,
                    resolvedTurn,
                    playerPower,
                    opponentTotal);
            }

            if (playerPower == strongestOpponent)
            {
                return Finish(
                    state,
                    CampaignOutcome.Draw,
                    CampaignEndReason.TurnLimitDraw,
                    resolvedTurn,
                    playerPower,
                    opponentTotal);
            }

            return Finish(
                state,
                CampaignOutcome.Defeat,
                CampaignEndReason.TurnLimitDefeat,
                resolvedTurn,
                playerPower,
                opponentTotal);
        }

        private CampaignTurnResult Finish(
            CampaignState state,
            CampaignOutcome outcome,
            CampaignEndReason reason,
            TurnNumber resolvedTurn,
            decimal playerPower,
            decimal opponentTotal)
        {
            var result = CreateResult(
                outcome,
                reason,
                resolvedTurn,
                playerPower,
                opponentTotal,
                state.DominanceConsecutiveTurns);
            state.LastResult = result;
            return result;
        }

        private CampaignTurnResult CreateResult(
            CampaignOutcome outcome,
            CampaignEndReason reason,
            TurnNumber resolvedTurn,
            decimal playerPower,
            decimal opponentTotal,
            int dominanceTurns)
        {
            return new CampaignTurnResult(
                outcome,
                reason,
                resolvedTurn,
                playerPower,
                opponentTotal,
                dominanceTurns,
                _rules.DominanceCheckStartTurn,
                _rules.DominanceMultiplier,
                _rules.DominanceRequiredConsecutiveTurns,
                new List<EconomicPowerSnapshot>(_rankingBuffer));
        }

        private void BuildRankings(CampaignState state)
        {
            _rankingBuffer.Clear();

            for (int i = 0; i < state.Participants.Count; i++)
            {
                var participant = state.Participants[i];
                decimal power = participant.IsEliminated
                    ? 0m
                    : _powerCalculator.Calculate(
                        participant,
                        _rules);

                _rankingBuffer.Add(new EconomicPowerSnapshot(
                    participant.Company.Id,
                    participant.Company.Name,
                    participant.IsPlayer,
                    participant.IsEliminated,
                    power));
            }

            _rankingBuffer.Sort(RankingComparison);
        }

        private EconomicPowerSnapshot FindPlayerRanking()
        {
            for (int i = 0; i < _rankingBuffer.Count; i++)
            {
                if (_rankingBuffer[i].IsPlayer)
                    return _rankingBuffer[i];
            }

            throw new InvalidOperationException(
                "플레이어 경제력 정보를 찾을 수 없습니다.");
        }

        private decimal CalculateOpponentTotal()
        {
            decimal total = 0m;

            for (int i = 0; i < _rankingBuffer.Count; i++)
            {
                var ranking = _rankingBuffer[i];
                if (!ranking.IsPlayer && !ranking.IsEliminated)
                    total += ranking.EconomicPower;
            }

            return total;
        }

        private int CountActiveOpponents()
        {
            int count = 0;

            for (int i = 0; i < _rankingBuffer.Count; i++)
            {
                if (!_rankingBuffer[i].IsPlayer &&
                    !_rankingBuffer[i].IsEliminated)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CompareRankings(
            EconomicPowerSnapshot left,
            EconomicPowerSnapshot right)
        {
            return right.EconomicPower.CompareTo(
                left.EconomicPower);
        }
    }
}
