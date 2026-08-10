using System.Text;
using Game.Application;
using Game.Domain.Localization;

namespace Game.Presentation
{
    public static class TurnReportKoreanFormatter
    {
        public static string Format(TurnReport report)
        {
            if (report == null)
                return "턴 보고서가 없습니다.";

            var builder = new StringBuilder(256);
            builder.Append(report.Turn);
            builder.Append(" 정산 완료 · 명령 ");
            builder.Append(report.CommandResults.Count);
            builder.Append("건 · 가격 변동 ");
            builder.Append(report.MarketReport.PriceChanges.Count);
            builder.Append("건");

            if (report.WorldReport != null)
            {
                int produced = 0;
                for (int i = 0;
                    i < report.WorldReport.Production.Count;
                    i++)
                {
                    if (report.WorldReport.Production[i].Produced)
                        produced++;
                }

                int settledTrades = 0;
                for (int i = 0;
                    i < report.WorldReport.Trades.Count;
                    i++)
                {
                    if (report.WorldReport.Trades[i].Settled)
                        settledTrades++;
                }

                decimal operatingCosts = 0m;
                decimal newDebt = 0m;
                for (int i = 0;
                    i < report.WorldReport.Finances.Count;
                    i++)
                {
                    operatingCosts += report.WorldReport
                        .Finances[i].FinanceResult.OperatingCost;
                    newDebt += report.WorldReport
                        .Finances[i].FinanceResult.NewDebt;
                }

                builder.Append("\n생산 공장 ");
                builder.Append(produced);
                builder.Append('/');
                builder.Append(report.WorldReport.Production.Count);
                builder.Append(" · 운송 도착 ");
                builder.Append(report.WorldReport.Arrivals.Count);
                builder.Append("건 · 거래 정산 ");
                builder.Append(settledTrades);
                builder.Append('/');
                builder.Append(report.WorldReport.Trades.Count);
                builder.Append("건");
                builder.Append("\n전체 운영비 ");
                builder.Append(KoreanFormat.Money(operatingCosts));
                builder.Append(" · 신규 부채 ");
                builder.Append(KoreanFormat.Money(newDebt));

                if (report.WorldReport.ResourceSites.SpawnedSites.Count > 0)
                {
                    builder.Append("\n[자원 이벤트] 신규 채굴지 발견: ");
                    for (int i = 0;
                        i < report.WorldReport.ResourceSites.SpawnedSites.Count;
                        i++)
                    {
                        if (i > 0)
                            builder.Append(", ");

                        var site = report.WorldReport.ResourceSites
                            .SpawnedSites[i];
                        builder.Append(GetResourceName(site.ResourceId.Value));
                        builder.Append(" 채굴지(");
                        builder.Append(site.RegionId.Value);
                        builder.Append(", 초기 ");
                        builder.Append(site.InitialOutput.ToString("0.##"));
                        builder.Append(" / 최소 ");
                        builder.Append(site.MinimumOutput.ToString("0.##"));
                        builder.Append(')');
                    }
                }

                decimal resourceSiteOutput = 0m;
                for (int i = 0;
                    i < report.WorldReport.ResourceSites.Production.Count;
                    i++)
                {
                    resourceSiteOutput += report.WorldReport.ResourceSites
                        .Production[i].Output;
                }

                if (report.WorldReport.ResourceSites.Production.Count > 0)
                {
                    builder.Append("\n가동 채굴지 ");
                    builder.Append(
                        report.WorldReport.ResourceSites.Production.Count);
                    builder.Append("곳 · 이번 턴 채광 ");
                    builder.Append(resourceSiteOutput.ToString("0.##"));
                }
            }

            builder.Append("\n");
            builder.Append(CampaignResultKoreanFormatter.Format(
                report.CampaignResult));
            builder.Append("\n처리 시간 ");
            builder.Append(report.Performance.ElapsedMilliseconds
                .ToString("F3"));
            builder.Append("ms");
            return builder.ToString();
        }

        private static string GetResourceName(string resourceId)
        {
            switch (resourceId)
            {
                case "iron": return "철";
                case "coal": return "석탄";
                case "wood": return "목재";
                case "oil": return "석유";
                case "steel": return "강철";
                case "food": return "식량";
                case "medicine": return "의약품";
                case "machine": return "기계";
                case "semiconductor": return "반도체";
                default: return resourceId;
            }
        }
    }
}
