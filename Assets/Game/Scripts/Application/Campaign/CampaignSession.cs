using System;
using Game.Domain.Campaign;
using Game.Domain.Common;

namespace Game.Application.Campaign
{
    public sealed class CampaignSession
    {
        private readonly CampaignVictoryEvaluator _evaluator;

        public CampaignState State { get; }
        public CampaignTurnResult LastResult => State.LastResult;
        public bool IsFinished => State.IsFinished;

        public CampaignSession(
            CampaignState state,
            CampaignVictoryEvaluator evaluator)
        {
            State = state ??
                throw new ArgumentNullException(nameof(state));
            _evaluator = evaluator ??
                throw new ArgumentNullException(nameof(evaluator));
        }

        public CampaignTurnResult EvaluateTurn(
            TurnNumber resolvedTurn)
        {
            return _evaluator.Evaluate(
                resolvedTurn,
                State);
        }
    }
}
