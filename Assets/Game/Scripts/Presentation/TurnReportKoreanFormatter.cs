using System.Text;
using Game.Application;
using Game.Domain.Localization;
using Game.Domain.World;

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

                AutonomousWorldTurnReport autonomous =
                    report.WorldReport.AutonomousWorld;
                if (autonomous.GeneratedEvents.Count > 0)
                {
                    builder.Append("\n[세계 사건] ");
                    for (int i = 0; i < autonomous.GeneratedEvents.Count; i++)
                    {
                        if (i > 0)
                            builder.Append(", ");
                        WorldEventInstance worldEvent =
                            autonomous.GeneratedEvents[i];
                        builder.Append(GetWorldEventName(worldEvent.Kind));
                        builder.Append('(');
                        builder.Append(worldEvent.RegionId.Value);
                        builder.Append(')');
                    }
                }

                if (autonomous.OfferedOpportunities.Count > 0)
                {
                    builder.Append("\n[개입 가능] ");
                    for (int i = 0;
                        i < autonomous.OfferedOpportunities.Count;
                        i++)
                    {
                        if (i > 0)
                            builder.Append(", ");
                        WorldOpportunity opportunity =
                            autonomous.OfferedOpportunities[i];
                        builder.Append(opportunity.DisplayName);
                        builder.Append(" · NPC 처리 예정 ");
                        builder.Append(opportunity.NpcResolveTurn.Value);
                        builder.Append("턴");
                    }
                }

                if (autonomous.ResolvedEvents.Count > 0)
                {
                    builder.Append("\nNPC/세계가 해결한 사건 ");
                    builder.Append(autonomous.ResolvedEvents.Count);
                    builder.Append("건");
                }

                if (autonomous.ArmyReadiness.Count > 0)
                {
                    decimal readinessTotal = 0m;
                    decimal upkeepTotal = 0m;
                    for (int i = 0; i < autonomous.ArmyReadiness.Count; i++)
                    {
                        readinessTotal += autonomous.ArmyReadiness[i].Readiness;
                        upkeepTotal += autonomous.ArmyReadiness[i].DailyUpkeep;
                    }

                    builder.Append("\n세계 군대 ");
                    builder.Append(autonomous.ArmyReadiness.Count);
                    builder.Append("개 · 평균 준비도 ");
                    builder.Append((readinessTotal /
                        autonomous.ArmyReadiness.Count * 100m)
                        .ToString("0.#"));
                    builder.Append("% · 실제 유지비 ");
                    builder.Append(KoreanFormat.Money(upkeepTotal));
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
                case "machinery": return "기계";
                case "semiconductor": return "반도체";
                default: return resourceId;
            }
        }

        private static string GetWorldEventName(WorldEventKind kind)
        {
            switch (kind)
            {
                case WorldEventKind.HarvestFailure: return "흉작";
                case WorldEventKind.BountifulHarvest: return "풍년";
                case WorldEventKind.MineCollapse: return "광산 붕괴";
                case WorldEventKind.NewVeinDiscovered: return "신규 광맥";
                case WorldEventKind.BanditIncrease: return "도적 증가";
                case WorldEventKind.ImportantNpcDeath: return "중요 인물 사망";
                case WorldEventKind.MilitarySupplyShortage: return "군수품 부족";
                case WorldEventKind.FactoryDisruption: return "생산 차질";
                default: return kind.ToString();
            }
        }
    }
}
