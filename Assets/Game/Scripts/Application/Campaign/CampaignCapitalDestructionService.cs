using System;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;

namespace Game.Application.Campaign
{
    public sealed class CampaignCapitalDestructionService
    {
        public bool Apply(
            CampaignState campaign,
            MapCapitalDestroyedRecord destruction)
        {
            if (campaign == null)
                throw new ArgumentNullException(nameof(campaign));
            if (string.IsNullOrWhiteSpace(destruction.DestroyedFactionId))
                return false;

            CampaignParticipantState participant = campaign.FindParticipant(
                new CompanyId(destruction.DestroyedFactionId));
            if (participant == null || !participant.IsCapitalStanding)
                return false;

            participant.DestroyCapital();
            return true;
        }
    }
}
