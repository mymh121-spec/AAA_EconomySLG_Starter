using System.Reflection;
using Game.Domain.Campaign;
using Game.Domain.Common;
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

                int resolvedDays = 0;
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

        private static void InvokePrivate(object target, string methodName)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"{methodName} 동작을 찾을 수 있어야 합니다.");
            method.Invoke(target, null);
        }
    }
}
