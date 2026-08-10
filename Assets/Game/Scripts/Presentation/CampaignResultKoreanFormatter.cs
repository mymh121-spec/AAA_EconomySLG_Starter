using Game.Domain.Campaign;
using Game.Domain.Localization;

namespace Game.Presentation
{
    public static class CampaignResultKoreanFormatter
    {
        public static string Format(CampaignTurnResult result)
        {
            if (result == null)
                return "캠페인 시작 · 경제 패권 판정 대기";

            string power =
                $"경제력 {KoreanFormat.Money(result.PlayerEconomicPower)} / " +
                $"상대 합계 {KoreanFormat.Money(result.OpponentCombinedEconomicPower)}";

            if (!result.IsFinished)
            {
                if (result.ResolvedTurn.Value <
                    result.DominanceCheckStartTurn)
                {
                    return $"캠페인 진행 중 · {power} · " +
                        $"경제 패권 판정은 " +
                        $"{result.DominanceCheckStartTurn}턴부터";
                }

                string streak = result.DominanceConsecutiveTurns > 0
                    ? $", 패권 조건 " +
                      $"{result.DominanceConsecutiveTurns}/" +
                      $"{result.DominanceRequiredConsecutiveTurns}턴"
                    : string.Empty;
                return $"캠페인 진행 중 · {power}{streak}";
            }

            return $"{GetOutcomeName(result.Outcome)} · " +
                $"{GetReasonName(result.EndReason)} · {power}";
        }

        public static string GetOutcomeName(CampaignOutcome outcome)
        {
            switch (outcome)
            {
                case CampaignOutcome.Victory:
                    return "승리";
                case CampaignOutcome.Defeat:
                    return "패배";
                case CampaignOutcome.Draw:
                    return "무승부";
                default:
                    return "진행 중";
            }
        }

        public static string GetReasonName(CampaignEndReason reason)
        {
            switch (reason)
            {
                case CampaignEndReason.Bankruptcy:
                    return "기업 파산";
                case CampaignEndReason.CapitalDestroyed:
                    return "수도 멸망";
                case CampaignEndReason.EconomicDominance:
                    return "경제 패권 달성";
                case CampaignEndReason.LastCompanyStanding:
                    return "모든 경쟁자 제거";
                case CampaignEndReason.TurnLimitVictory:
                    return "30턴 경제력 1위";
                case CampaignEndReason.TurnLimitDefeat:
                    return "30턴 경제 경쟁 패배";
                case CampaignEndReason.TurnLimitDraw:
                    return "30턴 공동 1위";
                default:
                    return "승패 미결정";
            }
        }
    }
}
