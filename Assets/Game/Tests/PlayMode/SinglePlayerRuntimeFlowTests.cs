using System;
using System.Collections;
using System.Reflection;
using System.Threading.Tasks;
using Game.Application.Session;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.World;
using Game.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace Game.Tests.PlayMode
{
    public sealed class SinglePlayerRuntimeFlowTests
    {
        [UnityTest]
        public IEnumerator OnlineSession_ConnectsAuthenticatedRealtimeStream_WhenConfigured()
        {
            string endpoint = Environment.GetEnvironmentVariable(
                "PVP_UNITY_INTEGRATION_ENDPOINT");
            string roomCode = Environment.GetEnvironmentVariable(
                "PVP_UNITY_INTEGRATION_ROOM");
            string accessToken = Environment.GetEnvironmentVariable(
                "PVP_UNITY_INTEGRATION_TOKEN");
            if (string.IsNullOrWhiteSpace(endpoint) ||
                string.IsNullOrWhiteSpace(roomCode) ||
                string.IsNullOrWhiteSpace(accessToken))
            {
                Assert.Pass("전용 서버 환경 변수가 없으므로 외부 통합 연결은 생략합니다.");
                yield break;
            }

            var root = new GameObject("pvp-unity-websocket-integration");
            PvpOnlineSessionController session =
                root.AddComponent<PvpOnlineSessionController>();
            Assert.That(session.ConfigureServerEndpoint(endpoint), Is.True);
            Task<bool> connecting = session.ConnectAsync(
                roomCode,
                accessToken);
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!connecting.IsCompleted &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(connecting.IsCompleted, Is.True,
                "Unity HTTP 초기 연결이 제한시간 안에 끝나야 합니다.");
            Assert.That(connecting.IsFaulted, Is.False,
                connecting.Exception?.ToString());
            Assert.That(connecting.Result, Is.True, session.LastError);

            while ((session.RealtimeConnectionState !=
                        PvpRealtimeConnectionState.Connected ||
                    !session.LastRealtimeMessageUtc.HasValue) &&
                   Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(session.RealtimeConnectionState,
                Is.EqualTo(PvpRealtimeConnectionState.Connected),
                session.LastRealtimeError);
            Assert.That(session.LastRealtimeMessageUtc.HasValue, Is.True,
                "Unity ClientWebSocket이 초기 전체 상태를 자동 수신해야 합니다.");
            Assert.That(session.CurrentState, Is.Not.Null);
            Assert.That(session.CurrentState.world?.map, Is.Not.Null);
            Assert.That(session.CurrentState.world.map.width, Is.EqualTo(80));
            Assert.That(session.CurrentState.world.map.height, Is.EqualTo(48));

            session.Disconnect();
            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator RealtimeStream_AppliesMonotonicVersionsAndNewEpoch()
        {
            var root = new GameObject("pvp-realtime-stream-test");
            PvpOnlineSessionController session =
                root.AddComponent<PvpOnlineSessionController>();
            int stateChangedCount = 0;
            session.StateChanged += _ => stateChangedCount++;

            InvokePrivate(
                session,
                "ApplyRealtimeMessage",
                CreateStreamJson("epoch-a", 5, "match-stream", 5));
            Assert.That(session.CurrentState, Is.Not.Null);
            Assert.That(session.CurrentState.revision, Is.EqualTo(5));
            Assert.That(stateChangedCount, Is.EqualTo(1));

            InvokePrivate(
                session,
                "ApplyRealtimeMessage",
                CreateStreamJson("epoch-a", 4, "match-stream", 4));
            Assert.That(session.CurrentState.revision, Is.EqualTo(5),
                "같은 스트림의 과거 버전은 현재 상태를 덮어쓰면 안 됩니다.");
            Assert.That(stateChangedCount, Is.EqualTo(1));

            InvokePrivate(
                session,
                "ApplyRealtimeMessage",
                CreateStreamJson("epoch-a", 6, "match-stream", 5, true));
            Assert.That(session.CurrentState.revision, Is.EqualTo(5));
            Assert.That(session.CurrentState.players[0].ready, Is.True,
                "A newer state version must apply even when the match revision is unchanged.");
            Assert.That(stateChangedCount, Is.EqualTo(2));

            InvokePrivate(
                session,
                "ApplyRealtimeMessage",
                CreateStreamJson("epoch-b", 1, "match-stream", 6));
            Assert.That(session.CurrentState.revision, Is.EqualTo(6),
                "서버 재시작으로 streamId가 바뀌면 낮은 스트림 버전도 받아야 합니다.");
            Assert.That(stateChangedCount, Is.EqualTo(3));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator AuthoritativeMapSnapshot_RestoresServerState()
        {
            const int width = 12;
            const int height = 8;
            var terrain = new int[width * height];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = (int)GridTerrainKind.Plains;

            var snapshot = new PvpMapWorldStateDto
            {
                width = width,
                height = height,
                seed = 721,
                wrapHorizontally = true,
                fixedStepsPerTurn = 10,
                currentEconomicDay = 4,
                terrain = terrain,
                units = new[]
                {
                    new PvpMapUnitStateDto
                    {
                        unitId = "server_host_unit",
                        ownerCompanyId = "company_host",
                        archetype = "Swordsman",
                        x = 2,
                        y = 4,
                        stamina = 7,
                        maxStamina = 10,
                        soldiers = 287,
                        morale = 88d,
                        fatigue = 12d,
                        plannedPath = Array.Empty<PvpMapCoordinateDto>()
                    },
                    new PvpMapUnitStateDto
                    {
                        unitId = "server_guest_unit",
                        ownerCompanyId = "company_guest",
                        archetype = "Swordsman",
                        x = 8,
                        y = 4,
                        stamina = 10,
                        maxStamina = 10,
                        soldiers = 300,
                        morale = 100d,
                        fatigue = 0d,
                        plannedPath = Array.Empty<PvpMapCoordinateDto>()
                    }
                },
                mines = new[]
                {
                    new PvpMapMineStateDto
                    {
                        x = 3,
                        y = 4,
                        kind = "Normal",
                        ownerCompanyId = "company_host",
                        captureProgress = 4,
                        captureRequired = 4
                    }
                },
                castles = new[]
                {
                    new PvpMapCastleStateDto
                    {
                        x = 1,
                        y = 4,
                        ownerCompanyId = "company_host",
                        originalOwnerCompanyId = "company_host",
                        isCapital = true
                    },
                    new PvpMapCastleStateDto
                    {
                        x = 8,
                        y = 4,
                        ownerCompanyId = string.Empty,
                        originalOwnerCompanyId = "company_guest",
                        isCapital = true,
                        isDestroyed = true
                    }
                }
            };

            var root = new GameObject("authoritative-map-snapshot-test");
            StarterMapController map = root.AddComponent<StarterMapController>();
            Assert.That(
                map.ApplyAuthoritativeSnapshot(
                    snapshot,
                    "company_host",
                    out string reason),
                Is.True,
                reason);

            Assert.That(map.IsAuthoritativeMap, Is.True);
            Assert.That(map.CurrentLayout.Width, Is.EqualTo(width));
            Assert.That(map.CurrentLayout.Height, Is.EqualTo(height));
            Assert.That(map.SelectedAuthoritativeServerUnitId,
                Is.EqualTo("server_host_unit"));
            Assert.That(map.SelectedPlayerUnit.Coordinate,
                Is.EqualTo(new GridCoordinate(2, 4)));
            Assert.That(map.SelectedPlayerUnit.Soldiers, Is.EqualTo(287));
            Assert.That(map.SelectedPlayerUnit.Stamina, Is.EqualTo(7));
            Assert.That(map.GameplayService.FindMine(new GridCoordinate(3, 4))
                .OwnerFactionId, Is.EqualTo("company_host"));

            UnityEngine.Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MenuToMovementAndCampaignResult_CompletesInPlayMode()
        {
            GameModeSelectionController controller =
                UnityEngine.Object.FindAnyObjectByType<GameModeSelectionController>();
            if (controller == null)
            {
                var root = new GameObject("플레이 모드 통합 테스트");
                controller = root.AddComponent<GameModeSelectionController>();
            }

            yield return null;

            UIDocument document = controller.GetComponent<UIDocument>();
            Assert.That(document, Is.Not.Null,
                "실제 Play Mode에서 모드 선택 UI가 생성되어야 합니다.");
            Assert.That(
                document.rootVisualElement.Q<ScrollView>(
                    "single-player-scroll"),
                Is.Not.Null,
                "긴 싱글플레이 HUD는 세로 스크롤을 제공해야 합니다.");
            Assert.That(
                document.rootVisualElement.Q<ScrollView>(
                    "multiplayer-connection-scroll"),
                Is.Not.Null,
                "멀티플레이 방 연결 화면도 세로 스크롤을 제공해야 합니다.");
            Button singlePlayerButton = null;
            Button createRoomButton = null;
            Button joinRoomButton = null;
            document.rootVisualElement.Query<Button>().ForEach(button =>
            {
                if (button.text == "1인이서 하기")
                    singlePlayerButton = button;
                else if (button.text == "새 방 만들기")
                    createRoomButton = button;
                else if (button.text == "초대 코드로 참가")
                    joinRoomButton = button;
            });
            Assert.That(singlePlayerButton, Is.Not.Null,
                "1인 플레이 버튼이 실제 UI 트리에 있어야 합니다.");
            Assert.That(createRoomButton, Is.Not.Null,
                "멀티플레이 새 방 만들기 버튼이 있어야 합니다.");
            Assert.That(joinRoomButton, Is.Not.Null,
                "멀티플레이 초대 코드 참가 버튼이 있어야 합니다.");

            InvokePrivate(controller, "SelectSinglePlayer");
            yield return null;

            Assert.That(controller.CurrentMode, Is.EqualTo(GamePlayMode.SinglePlayer));
            SimulationBootstrapper simulation =
                UnityEngine.Object.FindAnyObjectByType<SimulationBootstrapper>();
            StarterMapController map =
                UnityEngine.Object.FindAnyObjectByType<StarterMapController>();
            Assert.That(simulation, Is.Not.Null);
            Assert.That(map, Is.Not.Null);
            Assert.That(simulation.gameObject.activeInHierarchy, Is.True);
            Assert.That(map.gameObject.activeInHierarchy, Is.True);
            Assert.That(simulation.CurrentCampaignState.Participants.Count,
                Is.EqualTo(4));
            Assert.That(simulation.CurrentWorldEconomy.Markets.Count,
                Is.GreaterThan(0));

            MapUnitState unit = map.SelectedPlayerUnit;
            Assert.That(unit, Is.Not.Null,
                "새 싱글플레이에 선택된 플레이어 부대가 있어야 합니다.");
            Assert.That(map.CurrentSelection.HasValue, Is.True,
                "시작 즉시 플레이어 본사 칸이 선택되어야 합니다.");
            Assert.That(map.CurrentSelection.Value.Coordinate,
                Is.EqualTo(unit.Coordinate));
            Label mapActionTitle = null;
            document.rootVisualElement.Query<Label>().ForEach(label =>
            {
                if (!string.IsNullOrEmpty(label.text) &&
                    label.text.StartsWith("지도 행동 ·"))
                {
                    mapActionTitle = label;
                }
            });
            Assert.That(mapActionTitle, Is.Not.Null);
            Assert.That(mapActionTitle.text, Does.Contain(unit.Id));
            Assert.That(mapActionTitle.text, Does.Not.Contain("없음"));
            GridCoordinate destination = FindReachableDestination(map, unit);
            Assert.That(destination, Is.Not.EqualTo(unit.Coordinate),
                "초기 부대가 이동할 수 있는 육지 칸이 있어야 합니다.");
            Assert.That(map.TryMoveSelectedPlayerUnit(destination, out string reason),
                Is.True,
                reason);
            int movementSteps = map.GameplayService
                .GetRemainingMovementFixedSteps(unit);
            map.AdvanceGameplayFixedSteps(Math.Max(1, movementSteps));
            Assert.That(unit.Coordinate, Is.EqualTo(destination),
                "플레이어 이동 명령이 실제 지도 위치를 변경해야 합니다.");

            int resolvedDays = 0;
            while (!simulation.IsCampaignFinished &&
                   resolvedDays < GameCalendarDate.DaysPerYear)
            {
                map.AdvanceEconomicDay(out _);
                simulation.ResolveCurrentTurn(false);
                resolvedDays++;
            }

            Assert.That(simulation.IsCampaignFinished, Is.True,
                "Play Mode 싱글플레이가 360일 안에 종료되어야 합니다.");
            Assert.That(simulation.CampaignResult.Outcome,
                Is.Not.EqualTo(CampaignOutcome.InProgress));
            InvokePrivate(controller, "HandleSinglePlayerRealtimeStateChanged");
            yield return null;

            Label resultLabel = null;
            document.rootVisualElement.Query<Label>().ForEach(label =>
            {
                if (!string.IsNullOrEmpty(label.text) &&
                    label.text.Contains("최종 판정"))
                {
                    resultLabel = label;
                }
            });
            Assert.That(resultLabel, Is.Not.Null,
                "캠페인 종료 후 최종 판정 UI가 표시되어야 합니다.");
        }

        private static GridCoordinate FindReachableDestination(
            StarterMapController map,
            MapUnitState unit)
        {
            GridMapLayout layout = map.CurrentLayout;
            for (int distance = 1; distance <= 10; distance++)
            {
                for (int y = 0; y < layout.Height; y++)
                {
                    for (int x = 0; x < layout.Width; x++)
                    {
                        var coordinate = new GridCoordinate(x, y);
                        if (layout.ManhattanDistance(
                                unit.Coordinate,
                                coordinate) != distance)
                        {
                            continue;
                        }
                        if (map.CanMoveSelectedPlayerUnit(coordinate, out _))
                            return coordinate;
                    }
                }
            }

            return unit.Coordinate;
        }

        private static string CreateStreamJson(
            string streamId,
            long version,
            string matchId,
            int revision,
            bool ready = false)
        {
            return JsonUtility.ToJson(new PvpStreamMessageDto
            {
                type = "state",
                streamId = streamId,
                version = version,
                serverUtc = DateTimeOffset.UtcNow.ToString("O"),
                state = new PvpReconnectDto
                {
                    matchId = matchId,
                    playerId = "player_1",
                    turn = 1,
                    phase = "Planning",
                    revision = revision,
                    stateHash = "hash",
                    streamId = streamId,
                    stateVersion = version,
                    turnDeadlineUtc = DateTimeOffset.UtcNow
                        .AddMinutes(2)
                        .ToString("O"),
                    players = new[]
                    {
                        new PvpPlayerStateDto
                        {
                            slot = 0,
                            playerId = "player_1",
                            companyId = "company_1",
                            connected = true,
                            ready = ready
                        }
                    },
                    ownPendingCommands = Array.Empty<PvpPendingCommandDto>()
                }
            });
        }

        private static void InvokePrivate(
            object target,
            string methodName,
            params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                $"{methodName} 동작을 찾을 수 있어야 합니다.");
            method.Invoke(target, arguments);
        }
    }
}
