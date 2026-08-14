using System.Reflection;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.World;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Tests.Playability
{
    public sealed class SinglePlayerPlayabilityTests
    {
        [Test]
        public void DefaultSinglePlayer_StartsAndReachesCampaignResult()
        {
            var root = new GameObject("싱글플레이 통합 테스트");

            try
            {
                var controller = root.AddComponent<GameModeSelectionController>();
                InvokePrivate(controller, "Start");

                Assert.That(root.GetComponent<UIDocument>(), Is.Not.Null,
                    "게임 모드 UI가 생성되어야 합니다.");

                MethodInfo selectSinglePlayer = typeof(GameModeSelectionController)
                    .GetMethod(
                        "SelectSinglePlayer",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectSinglePlayer, Is.Not.Null,
                    "1인 플레이 시작 동작을 찾을 수 있어야 합니다.");
                selectSinglePlayer.Invoke(controller, null);

                Assert.That(controller.CurrentMode,
                    Is.EqualTo(Game.Application.Session.GamePlayMode.SinglePlayer));

                SimulationBootstrapper simulation =
                    Object.FindAnyObjectByType<SimulationBootstrapper>();
                StarterMapController map =
                    Object.FindAnyObjectByType<StarterMapController>();
                Assert.That(simulation, Is.Not.Null,
                    "1인 플레이 경제 시뮬레이션이 생성되어야 합니다.");
                Assert.That(map, Is.Not.Null,
                    "1인 플레이 지도가 생성되어야 합니다.");
                if (simulation.CurrentCampaignState == null)
                    InvokePrivate(simulation, "Awake");
                Assert.That(simulation.CurrentCampaignState, Is.Not.Null);
                Assert.That(simulation.CurrentCampaignState.Participants.Count,
                    Is.EqualTo(4));
                Assert.That(simulation.CurrentWorldEconomy.Markets.Count,
                    Is.GreaterThan(0));
                Assert.That(map.GameplayService, Is.Not.Null);
                Assert.That(map.GameplayService.Units.Count,
                    Is.GreaterThan(0));

                Label campaignHud = GetPrivateField<Label>(
                    controller,
                    "_campaignHudLabel");
                Assert.That(campaignHud, Is.Not.Null);
                Assert.That(campaignHud.text, Does.Contain("현재"));
                Assert.That(campaignHud.text, Does.Contain("남은"));

                simulation.ResolveCurrentTurn(false);
                InvokePrivate(controller, "RefreshSinglePlayerStatus");
                Assert.That(campaignHud.text, Does.Contain("경제력: 플레이어"));
                Assert.That(campaignHud.text, Does.Contain("상대 경제력:"));
                Assert.That(campaignHud.text, Does.Contain("경제 패권:"));
                Assert.That(campaignHud.text,
                    Does.Contain("12월 30일 예상 순위:"));
                Assert.That(campaignHud.text, Does.Contain("승리 전망:"));
                Assert.That(campaignHud.text, Does.Contain("위험 알림:"));

                int resolvedDays = 1;
                while (!simulation.IsCampaignFinished &&
                       resolvedDays < GameCalendarDate.DaysPerYear)
                {
                    simulation.ResolveCurrentTurn(false);
                    resolvedDays++;
                }

                Assert.That(simulation.IsCampaignFinished, Is.True,
                    "기본 싱글플레이가 360일 안에 최종 결과에 도달해야 합니다.");
                Assert.That(simulation.CampaignResult, Is.Not.Null);
                Assert.That(simulation.CampaignResult.Outcome,
                    Is.Not.EqualTo(CampaignOutcome.InProgress));
                Assert.That(simulation.CampaignResult.Rankings.Count,
                    Is.EqualTo(4));

                InvokePrivate(controller, "ShowSinglePlayerResult");
                Label resultText = GetPrivateField<Label>(
                    controller,
                    "_singlePlayerResultText");
                Assert.That(resultText.text, Does.Contain("최종 판정:"));
                Assert.That(resultText.text, Does.Contain("종료 사유:"));
                Assert.That(resultText.text, Does.Contain("최종 순위:"));
                Assert.That(resultText.text, Does.Contain("최종 현금:"));
                Assert.That(resultText.text, Does.Contain("전체 순위:"));
            }
            finally
            {
                foreach (GameModeSelectionController controller in
                         Object.FindObjectsByType<GameModeSelectionController>(
                             FindObjectsSortMode.None))
                {
                    Object.DestroyImmediate(controller.gameObject);
                }

                foreach (SimulationBootstrapper simulation in
                         Object.FindObjectsByType<SimulationBootstrapper>(
                             FindObjectsSortMode.None))
                {
                    if (simulation != null)
                        Object.DestroyImmediate(simulation.gameObject);
                }

                foreach (StarterMapController map in
                         Object.FindObjectsByType<StarterMapController>(
                             FindObjectsSortMode.None))
                {
                    if (map != null)
                        Object.DestroyImmediate(map.gameObject);
                }
            }
        }

        [Test]
        public void CampaignHud_ShowsDominanceResetBankruptcyAndCapitalRisk()
        {
            var playerCompany = new Company(
                new CompanyId("player"),
                "플레이어 기업",
                100m);
            playerCompany.AddDebt(850m);
            var player = new CampaignParticipantState(playerCompany, true);
            var opponent = new CampaignParticipantState(
                new Company(new CompanyId("opponent"), "경쟁 기업", 200m),
                false);
            var state = new CampaignState(new[] { player, opponent });
            var result = new CampaignTurnResult(
                CampaignOutcome.InProgress,
                CampaignEndReason.None,
                new TurnNumber(200),
                100m,
                200m,
                0,
                181,
                3m,
                60,
                new[]
                {
                    new EconomicPowerSnapshot(
                        opponent.Company.Id,
                        opponent.Company.Name,
                        false,
                        false,
                        200m),
                    new EconomicPowerSnapshot(
                        player.Company.Id,
                        player.Company.Name,
                        true,
                        false,
                        100m)
                });

            string text = CampaignResultKoreanFormatter.FormatHud(
                result,
                state,
                200,
                GameCalendarDate.DaysPerYear,
                1000m,
                true,
                20,
                100,
                "3배 패권 유지 중단 · 0일부터 다시 계산");

            Assert.That(text, Does.Contain("3배 패권 유지 중단"));
            Assert.That(text, Does.Contain("파산 위험"));
            Assert.That(text, Does.Contain("수도 공성 중 · 성벽 20%"));
            Assert.That(text, Does.Contain("현재 2위"));
        }

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"{methodName} 동작을 찾을 수 있어야 합니다.");
            method.Invoke(target, null);
        }

        private static T GetPrivateField<T>(object target, string fieldName)
            where T : class
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null,
                $"{fieldName} 필드를 찾을 수 있어야 합니다.");
            return field.GetValue(target) as T;
        }
    }
}
