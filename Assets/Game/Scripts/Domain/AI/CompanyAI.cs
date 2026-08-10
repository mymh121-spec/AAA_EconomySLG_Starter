using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Market;

namespace Game.Domain.AI
{
    public enum AIActionType
    {
        BuyResource,
        SellResource,
        BuildFactory,
        StartMission,
        ResearchTechnology,
        ExpandWarehouse
    }

    public sealed class AIAction
    {
        public AIActionType Type { get; }
        public decimal Score { get; }
        public ResourceId? ResourceId { get; }
        public decimal Quantity { get; }

        public AIAction(
            AIActionType type,
            decimal score,
            ResourceId? resourceId = null,
            decimal quantity = 0)
        {
            Type = type;
            Score = score;
            ResourceId = resourceId;
            Quantity = quantity;
        }
    }

    public sealed class AIDecisionContext
    {
        public Company Company { get; }
        public IReadOnlyList<MarketSnapshot> MarketSnapshots { get; }
        public int MaxDailyActions { get; }

        public AIDecisionContext(
            Company company,
            IReadOnlyList<MarketSnapshot> marketSnapshots,
            int maxDailyActions)
        {
            Company = company;
            MarketSnapshots = marketSnapshots;
            MaxDailyActions = Math.Max(1, maxDailyActions);
        }
    }

    public sealed class CompanyAI
    {
        private static readonly Comparison<AIAction> ScoreComparison =
            CompareScoreDescending;

        private readonly List<AIAction> _candidateBuffer =
            new List<AIAction>(32);

        public IReadOnlyList<AIAction> Think(AIDecisionContext context)
        {
            var results = new List<AIAction>(context.MaxDailyActions);
            Think(context, results);
            return results;
        }

        public void Think(
            AIDecisionContext context,
            List<AIAction> results)
        {
            if (results == null)
                throw new ArgumentNullException(nameof(results));

            results.Clear();
            _candidateBuffer.Clear();

            foreach (var snapshot in context.MarketSnapshots)
            {
                decimal shortageRatio = snapshot.Demand /
                    Math.Max(1.0m, snapshot.Supply);

                if (shortageRatio > 1.15m && snapshot.UnmetDemand > 0)
                {
                    _candidateBuffer.Add(new AIAction(
                        AIActionType.BuyResource,
                        shortageRatio,
                        snapshot.ResourceId,
                        snapshot.UnmetDemand));
                }
                else if (snapshot.Supply > snapshot.Demand * 1.25m)
                {
                    _candidateBuffer.Add(new AIAction(
                        AIActionType.SellResource,
                        snapshot.Supply / Math.Max(1.0m, snapshot.Demand),
                        snapshot.ResourceId,
                        snapshot.Supply - snapshot.Demand));
                }
            }

            _candidateBuffer.Sort(ScoreComparison);

            for (int i = 0;
                 i < _candidateBuffer.Count &&
                 results.Count < context.MaxDailyActions;
                 i++)
            {
                if (_candidateBuffer[i].Score > 1.0m)
                    results.Add(_candidateBuffer[i]);
            }
        }

        private static int CompareScoreDescending(
            AIAction left,
            AIAction right)
        {
            return right.Score.CompareTo(left.Score);
        }
    }
}
