using System;
using System.Text;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Localization;

namespace Game.Presentation
{
    public static class CampaignResultKoreanFormatter
    {
        public static string FormatHud(
            CampaignTurnResult result,
            CampaignState state,
            int currentDay,
            int maxTurns,
            decimal bankruptcyDebtLimit,
            bool capitalUnderSiege,
            int capitalWallDurability,
            int capitalMaxWallDurability,
            string transitionAlert)
        {
            int safeMaxTurns = Math.Max(1, maxTurns);
            int safeCurrentDay = Math.Clamp(currentDay, 1, safeMaxTurns);
            var builder = new StringBuilder(420);

            if (result == null)
            {
                return builder
                    .Append("경제력 집계 대기")
                    .Append("\n위험 알림: 없음")
                    .ToString();
            }

            decimal ratio = result.OpponentCombinedEconomicPower <= 0m
                ? 0m
                : result.PlayerEconomicPower /
                  result.OpponentCombinedEconomicPower;
            builder.Append("경제력: 플레이어 ")
                .Append(KoreanFormat.Money(result.PlayerEconomicPower))
                .Append(" · 상대 합계 ")
                .Append(KoreanFormat.Money(
                    result.OpponentCombinedEconomicPower))
                .Append(" · 패권 비율 ")
                .Append(result.OpponentCombinedEconomicPower <= 0m
                    ? "상대 전멸"
                    : ratio.ToString("F2") + "배")
                .Append("\n상대 경제력: ")
                .Append(FormatOpponentPowers(result));

            if (result.ResolvedTurn.Value < result.DominanceCheckStartTurn)
            {
                int untilCheck = Math.Max(
                    0,
                    result.DominanceCheckStartTurn - safeCurrentDay);
                builder.Append("\n경제 패권: ")
                    .Append(GameCalendarDate.FromDayNumber(
                        result.DominanceCheckStartTurn))
                    .Append(" 판정 시작까지 ")
                    .Append(untilCheck)
                    .Append("일");
            }
            else
            {
                builder.Append("\n경제 패권: ")
                    .Append(result.DominanceConsecutiveTurns)
                    .Append('/')
                    .Append(result.DominanceRequiredConsecutiveTurns)
                    .Append("일 · 목표 ")
                    .Append(result.DominanceMultiplier.ToString("F0"))
                    .Append("배");
            }

            int playerRank = GetPlayerRank(result);
            builder.Append("\n12월 30일 예상 순위: ")
                .Append(playerRank > 0 ? playerRank.ToString() : "-")
                .Append('/')
                .Append(result.Rankings.Count)
                .Append("위 · 승리 전망: ")
                .Append(FormatVictoryForecast(result, playerRank));

            string risks = FormatRisks(
                state,
                bankruptcyDebtLimit,
                capitalUnderSiege,
                capitalWallDurability,
                capitalMaxWallDurability,
                transitionAlert);
            builder.Append("\n위험 알림: ").Append(risks);
            return builder.ToString();
        }

        public static string FormatFinalSummary(
            CampaignTurnResult result,
            CampaignState state)
        {
            if (result == null)
                return "최종 결과를 불러오지 못했습니다.";

            int playerRank = GetPlayerRank(result);
            var builder = new StringBuilder(640)
                .Append("최종 판정: ")
                .Append(GetOutcomeName(result.Outcome))
                .Append("\n종료 사유: ")
                .Append(GetReasonName(result.EndReason))
                .Append("\n종료 날짜: ")
                .Append(GameCalendarDate.FromDayNumber(
                    result.ResolvedTurn.Value))
                .Append(" / 전체 12개월")
                .Append("\n최종 순위: ")
                .Append(playerRank > 0 ? playerRank.ToString() : "-")
                .Append('/')
                .Append(result.Rankings.Count)
                .Append("위")
                .Append("\n플레이어 경제력: ")
                .Append(KoreanFormat.Money(result.PlayerEconomicPower))
                .Append(" · 상대 합계: ")
                .Append(KoreanFormat.Money(
                    result.OpponentCombinedEconomicPower));

            if (state?.Player?.Company != null)
            {
                builder.Append("\n최종 현금: ")
                    .Append(KoreanFormat.Money(state.Player.Company.Cash))
                    .Append(" · 부채: ")
                    .Append(KoreanFormat.Money(state.Player.Company.Debt))
                    .Append(" · 수도: ")
                    .Append(state.Player.IsCapitalStanding
                        ? "존속"
                        : "멸망");
            }

            builder.Append("\n경제 패권 유지: ")
                .Append(result.DominanceConsecutiveTurns)
                .Append('/')
                .Append(result.DominanceRequiredConsecutiveTurns)
                .Append("일")
                .Append("\n전체 순위:");
            for (int i = 0; i < result.Rankings.Count; i++)
            {
                EconomicPowerSnapshot ranking = result.Rankings[i];
                builder.Append("\n")
                    .Append(i + 1)
                    .Append("위 ")
                    .Append(ranking.CompanyName)
                    .Append(ranking.IsPlayer ? " (플레이어)" : string.Empty)
                    .Append(" · ")
                    .Append(KoreanFormat.Money(ranking.EconomicPower))
                    .Append(ranking.IsEliminated ? " · 탈락" : string.Empty);
            }
            return builder.ToString();
        }

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
                        $"{GameCalendarDate.FromDayNumber(result.DominanceCheckStartTurn)}부터";
                }

                string streak = result.DominanceConsecutiveTurns > 0
                    ? $", 패권 조건 " +
                      $"{result.DominanceConsecutiveTurns}/" +
                      $"{result.DominanceRequiredConsecutiveTurns}일"
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
                    return "12개월 종료 경제력 1위";
                case CampaignEndReason.TurnLimitDefeat:
                    return "12개월 종료 경제 경쟁 패배";
                case CampaignEndReason.TurnLimitDraw:
                    return "12개월 종료 공동 1위";
                default:
                    return "승패 미결정";
            }
        }

        private static string FormatOpponentPowers(CampaignTurnResult result)
        {
            var builder = new StringBuilder(160);
            for (int i = 0; i < result.Rankings.Count; i++)
            {
                EconomicPowerSnapshot ranking = result.Rankings[i];
                if (ranking.IsPlayer)
                    continue;
                if (builder.Length > 0)
                    builder.Append(" · ");
                builder.Append(ranking.CompanyName)
                    .Append(' ')
                    .Append(KoreanFormat.Money(ranking.EconomicPower))
                    .Append(ranking.IsEliminated ? "(탈락)" : string.Empty);
            }
            return builder.Length == 0 ? "없음" : builder.ToString();
        }

        private static int GetPlayerRank(CampaignTurnResult result)
        {
            for (int i = 0; i < result.Rankings.Count; i++)
            {
                if (result.Rankings[i].IsPlayer)
                    return i + 1;
            }
            return 0;
        }

        private static string FormatVictoryForecast(
            CampaignTurnResult result,
            int playerRank)
        {
            if (result.IsFinished)
                return GetOutcomeName(result.Outcome) + " 확정";
            if (result.DominanceConsecutiveTurns > 0)
            {
                int remaining = Math.Max(
                    0,
                    result.DominanceRequiredConsecutiveTurns -
                    result.DominanceConsecutiveTurns);
                return $"경제 패권 승리까지 {remaining}일";
            }

            decimal strongestOpponent = 0m;
            bool tiedForFirst = false;
            for (int i = 0; i < result.Rankings.Count; i++)
            {
                EconomicPowerSnapshot ranking = result.Rankings[i];
                if (ranking.IsPlayer || ranking.IsEliminated)
                    continue;
                strongestOpponent = Math.Max(
                    strongestOpponent,
                    ranking.EconomicPower);
                tiedForFirst |= ranking.EconomicPower ==
                    result.PlayerEconomicPower;
            }

            if (playerRank == 1 && tiedForFirst)
                return "현재 공동 1위 · 종료 시 무승부";
            if (playerRank == 1)
                return "현재대로면 경제력 1위 승리";

            decimal gap = Math.Max(
                0m,
                strongestOpponent - result.PlayerEconomicPower);
            return $"현재 {playerRank}위 · 1위까지 " +
                KoreanFormat.Money(gap);
        }

        private static string FormatRisks(
            CampaignState state,
            decimal bankruptcyDebtLimit,
            bool capitalUnderSiege,
            int capitalWallDurability,
            int capitalMaxWallDurability,
            string transitionAlert)
        {
            var risks = new StringBuilder(180);
            if (!string.IsNullOrWhiteSpace(transitionAlert))
                AppendRisk(risks, transitionAlert.Trim());

            if (state?.Player?.Company != null)
            {
                if (state.Player.Company.IsBankrupt)
                {
                    AppendRisk(risks, "기업 파산");
                }
                else if (bankruptcyDebtLimit > 0m &&
                         state.Player.Company.Debt >=
                         bankruptcyDebtLimit * 0.8m)
                {
                    AppendRisk(
                        risks,
                        "파산 위험: 부채 " +
                        KoreanFormat.Money(state.Player.Company.Debt) +
                        " / 한도 " +
                        KoreanFormat.Money(bankruptcyDebtLimit));
                }

                if (!state.Player.IsCapitalStanding)
                    AppendRisk(risks, "수도 멸망");
            }

            int safeMaxWall = Math.Max(0, capitalMaxWallDurability);
            int wallPercent = safeMaxWall == 0
                ? 100
                : Math.Clamp(
                    (int)Math.Round(
                        capitalWallDurability * 100m / safeMaxWall),
                    0,
                    100);
            if (capitalUnderSiege)
            {
                AppendRisk(
                    risks,
                    $"수도 공성 중 · 성벽 {wallPercent}%");
            }
            else if (safeMaxWall > 0 && wallPercent <= 30)
            {
                AppendRisk(
                    risks,
                    $"수도 성벽 위험 · {wallPercent}%");
            }

            return risks.Length == 0
                ? "현재 즉시 패배 위험 없음"
                : risks.ToString();
        }

        private static void AppendRisk(StringBuilder builder, string risk)
        {
            if (builder.Length > 0)
                builder.Append(" · ");
            builder.Append(risk);
        }
    }
}
