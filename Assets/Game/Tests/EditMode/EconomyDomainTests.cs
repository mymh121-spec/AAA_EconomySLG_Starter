using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Game.Application;
using Game.Application.AI;
using Game.Application.Campaign;
using Game.Application.PvP;
using Game.Application.Platform;
using Game.Application.Session;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Economy;
using Game.Domain.Logistics;
using Game.Domain.Market;
using Game.Domain.Military;
using Game.Domain.Inventory;
using Game.Domain.Production;
using Game.Domain.Resources;
using Game.Domain.Technology;
using Game.Domain.World;

namespace Game.Tests
{
    public sealed class EconomyDomainTests
    {
        [Test]
        public void MatchmakingRequest_ValidatesHiveConnectionInputs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PvpMatchmakingRequest(0, 100, string.Empty));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new PvpMatchmakingRequest(1, -1, string.Empty));
            Assert.Throws<ArgumentException>(() =>
                new PvpMatchmakingRequest(
                    1,
                    100,
                    new string('가', 257)));

            var valid = new PvpMatchmakingRequest(7, 1200, "서울 서버");

            Assert.That(valid.MatchId, Is.EqualTo(7));
            Assert.That(valid.Point, Is.EqualTo(1200));
            Assert.That(valid.ExtraData, Is.EqualTo("서울 서버"));
        }

        [TestCase("matchingInProgress", "", PvpMatchmakingStatus.Searching)]
        [TestCase("matched", "", PvpMatchmakingStatus.Matched)]
        [TestCase("timeout", "", PvpMatchmakingStatus.TimedOut)]
        [TestCase("", "requested", PvpMatchmakingStatus.Searching)]
        [TestCase("", "notRequested", PvpMatchmakingStatus.Idle)]
        public void MatchmakingStatusMapper_MapsHiveStatuses(
            string matchingStatus,
            string requestStatus,
            PvpMatchmakingStatus expected)
        {
            Assert.That(
                PvpMatchmakingStatusMapper.FromExternalStatus(
                    matchingStatus,
                    requestStatus),
                Is.EqualTo(expected));
        }

        [Test]
        public void MatchmakingSnapshot_CopiesMatchedPlayerCollection()
        {
            var source = new List<PvpMatchedPlayer>
            {
                new PvpMatchedPlayer(10L, 900, "기업 A")
            };
            var snapshot = new PvpMatchmakingSnapshot(
                "HIVE Matchmaking",
                1,
                PvpMatchmakingStatus.Matched,
                "완료",
                "external-match",
                source);

            source.Clear();

            Assert.That(snapshot.IsTerminal, Is.True);
            Assert.That(snapshot.Players.Count, Is.EqualTo(1));
            Assert.That(snapshot.Players[0].PlayerId, Is.EqualTo(10L));
        }

        [Test]
        public void RealtimeClock_SupportsPauseAndOneToFourTimesSpeed()
        {
            var clock = new RealtimeSimulationClock(
                realSecondsPerGameDay: 60d,
                fixedRealStepSeconds: 1d,
                maxStepsPerAdvance: 100,
                initialSpeedMultiplier: 1);

            RealtimeAdvanceResult normal = clock.Advance(60d);

            Assert.That(normal.CompletedGameDays, Is.EqualTo(1));
            Assert.That(clock.CurrentDayNumber, Is.EqualTo(2));
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(1));

            Assert.That(clock.SetSpeed(4), Is.True);
            RealtimeAdvanceResult fourTimes = clock.Advance(15d);

            Assert.That(fourTimes.CompletedGameDays, Is.EqualTo(1));
            Assert.That(clock.CurrentDayNumber, Is.EqualTo(3));
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(4));

            Assert.That(clock.TogglePause(), Is.True);
            Assert.That(clock.IsPaused, Is.True);
            Assert.That(clock.Advance(60d).FixedStepCount, Is.EqualTo(0));
            Assert.That(clock.CurrentDayNumber, Is.EqualTo(3));

            Assert.That(clock.TogglePause(), Is.True);
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(4));
            Assert.That(clock.SetSpeed(5), Is.False);
            Assert.That(clock.SpeedMultiplier, Is.EqualTo(4));
        }

        [Test]
        public void RealtimeClock_DropsExcessCatchUpWork()
        {
            var clock = new RealtimeSimulationClock(
                realSecondsPerGameDay: 60d,
                fixedRealStepSeconds: 0.1d,
                maxStepsPerAdvance: 16,
                initialSpeedMultiplier: 4);

            RealtimeAdvanceResult result = clock.Advance(10d);

            Assert.That(result.FixedStepCount, Is.EqualTo(16));
            Assert.That(result.DroppedRealSeconds, Is.GreaterThan(8d));
            Assert.That(result.CompletedGameDays, Is.EqualTo(0));
        }

        [Test]
        public void GridMapLayout_UsesLargeWrappedWorldAndProtectsCompanyStarts()
        {
            var generator = new GridMapLayoutGenerator();
            var playerStart = new GridCoordinate(4, 24);
            var opponentStarts = new[]
            {
                new GridCoordinate(44, 23),
                new GridCoordinate(30, 32),
                new GridCoordinate(57, 16)
            };
            GridMapLayout layout = generator.Generate(
                80,
                48,
                160,
                12345,
                playerStart,
                opponentStarts,
                true,
                neutralCastleCount: 8);

            Assert.That(layout.Width, Is.EqualTo(80));
            Assert.That(layout.Height, Is.EqualTo(48));
            Assert.That(layout.WrapHorizontally, Is.True);
            Assert.That(layout.Terrain.Count, Is.EqualTo(80 * 48));
            Assert.That(layout.PlayerStart, Is.EqualTo(playerStart));
            Assert.That(layout.OpponentStarts.Count, Is.EqualTo(3));
            Assert.That(layout.NeutralCastles.Count, Is.EqualTo(8));
            Assert.That(layout.Mines.Count, Is.EqualTo(160));

            var uniqueCoordinates = new HashSet<GridCoordinate>
            {
                playerStart
            };
            for (int i = 0; i < opponentStarts.Length; i++)
                Assert.That(uniqueCoordinates.Add(opponentStarts[i]), Is.True);

            for (int i = 0; i < layout.NeutralCastles.Count; i++)
            {
                GridCoordinate castle = layout.NeutralCastles[i];
                Assert.That(uniqueCoordinates.Add(castle), Is.True);
                Assert.That(layout.IsLand(castle), Is.True);
                Assert.That(layout.IsNeutralCastle(castle), Is.True);
                Assert.That(
                    layout.ManhattanDistance(castle, playerStart),
                    Is.GreaterThanOrEqualTo(7));
                for (int opponentIndex = 0;
                     opponentIndex < opponentStarts.Length;
                     opponentIndex++)
                {
                    Assert.That(
                        layout.ManhattanDistance(
                            castle,
                            opponentStarts[opponentIndex]),
                        Is.GreaterThanOrEqualTo(7));
                }
                for (int otherIndex = 0; otherIndex < i; otherIndex++)
                {
                    Assert.That(
                        layout.ManhattanDistance(
                            castle,
                            layout.NeutralCastles[otherIndex]),
                        Is.GreaterThanOrEqualTo(6));
                }
            }

            for (int i = 0; i < layout.Mines.Count; i++)
            {
                Assert.That(
                    layout.Mines[i].Coordinate,
                    Is.Not.EqualTo(playerStart));
                for (int opponentIndex = 0;
                     opponentIndex < opponentStarts.Length;
                     opponentIndex++)
                {
                    Assert.That(
                        layout.Mines[i].Coordinate,
                        Is.Not.EqualTo(opponentStarts[opponentIndex]));
                }
                Assert.That(
                    uniqueCoordinates.Add(layout.Mines[i].Coordinate),
                    Is.True);
                Assert.That(
                    layout.Mines[i].Kind == MineKind.Normal ||
                    layout.Mines[i].Kind == MineKind.Gold,
                    Is.True);
                Assert.That(
                    layout.GetTerrain(layout.Mines[i].Coordinate),
                    Is.Not.EqualTo(GridTerrainKind.Ocean));
            }

            Assert.That(layout.IsLand(playerStart), Is.True);
            Assert.That(layout.Move(new GridCoordinate(79, 10), 1, 0),
                Is.EqualTo(new GridCoordinate(0, 10)));
            Assert.That(layout.Move(new GridCoordinate(0, 10), -1, 0),
                Is.EqualTo(new GridCoordinate(79, 10)));
            Assert.That(
                layout.ManhattanDistance(
                    new GridCoordinate(0, 10),
                    new GridCoordinate(79, 10)),
                Is.EqualTo(1));
            Assert.That(
                layout.TryNormalize(
                    new GridCoordinate(80, 10),
                    out GridCoordinate wrapped),
                Is.True);
            Assert.That(wrapped, Is.EqualTo(new GridCoordinate(0, 10)));
            Assert.That(
                layout.TryNormalize(
                    new GridCoordinate(10, -1),
                    out _),
                Is.False);
        }

        [Test]
        public void GridMapLayout_AllFactionStartsHaveLandExitsAcrossSeeds()
        {
            var generator = new GridMapLayoutGenerator();
            var playerStart = new GridCoordinate(4, 24);
            var opponentStarts = new[]
            {
                new GridCoordinate(44, 23),
                new GridCoordinate(30, 32),
                new GridCoordinate(57, 16)
            };

            for (int seed = 0; seed < 256; seed++)
            {
                GridMapLayout layout = generator.Generate(
                    80,
                    48,
                    128,
                    seed,
                    playerStart,
                    opponentStarts,
                    true,
                    neutralCastleCount: 8);

                AssertStartHasLandExit(layout, playerStart, seed);
                for (int i = 0; i < opponentStarts.Length; i++)
                    AssertStartHasLandExit(layout, opponentStarts[i], seed);
            }
        }

        [Test]
        public void RealtimeMapGameplay_MovesAcrossWrapAndCapturesMine()
        {
            var terrain = new GridTerrainKind[5 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var mineCoordinate = new GridCoordinate(4, 1);
            var layout = new GridMapLayout(
                5,
                3,
                7,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new[] { new MinePlacement(mineCoordinate, MineKind.Normal) },
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCapture: 2,
                    aiDecisionIntervalSteps: 100));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    mineCoordinate,
                    out _),
                Is.True);
            Assert.That(unit.IsMoving, Is.True);
            Assert.That(unit.PlannedPath.Count, Is.EqualTo(2));
            Assert.That(
                unit.PlannedPath[0],
                Is.EqualTo(new GridCoordinate(0, 1)));
            Assert.That(unit.PlannedPath[1], Is.EqualTo(mineCoordinate));

            service.AdvanceFixedSteps(2);

            Assert.That(unit.Coordinate, Is.EqualTo(mineCoordinate));
            Assert.That(unit.IsMoving, Is.False);
            Assert.That(unit.PlannedPath, Is.Empty);
            Assert.That(
                service.FindMine(mineCoordinate).OwnerFactionId,
                Is.EqualTo("player"));
            IReadOnlyList<MapMineProductionRecord> production =
                service.CreateDailyProduction();
            Assert.That(production.Count, Is.EqualTo(1));
            Assert.That(production[0].NormalMineCount, Is.EqualTo(1));
            Assert.That(production[0].IronAmount, Is.EqualTo(12m));
        }

        [Test]
        public void RealtimeMapGameplay_TracksMovementProgressAndRemainingRoute()
        {
            var terrain = new GridTerrainKind[5 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                5,
                3,
                17,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new MinePlacement[0],
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 2,
                    aiDecisionIntervalSteps: 100));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(2, 1),
                    out _),
                Is.True);
            Assert.That(unit.TotalMovementTileCount, Is.EqualTo(2));
            Assert.That(unit.CompletedMovementTileCount, Is.Zero);
            Assert.That(unit.RemainingMovementTileCount, Is.EqualTo(2));

            service.AdvanceFixedSteps(1);

            Assert.That(unit.MovementProgress, Is.EqualTo(1));
            Assert.That(
                service.GetRemainingMovementFixedSteps(unit),
                Is.EqualTo(3));
            Assert.That(
                service.TryGetMovementSegment(
                    unit,
                    out GridCoordinate from,
                    out GridCoordinate to,
                    out double progress),
                Is.True);
            Assert.That(from, Is.EqualTo(new GridCoordinate(0, 1)));
            Assert.That(to, Is.EqualTo(new GridCoordinate(1, 1)));
            Assert.That(progress, Is.EqualTo(0.5d).Within(0.0001d));

            service.AdvanceFixedSteps(1);

            Assert.That(unit.Coordinate, Is.EqualTo(new GridCoordinate(1, 1)));
            Assert.That(unit.CompletedMovementTileCount, Is.EqualTo(1));
            Assert.That(unit.RemainingMovementTileCount, Is.EqualTo(1));
            Assert.That(unit.PlannedPath.Count, Is.EqualTo(2));
            Assert.That(unit.PlannedPath[0], Is.EqualTo(unit.Coordinate));
            Assert.That(
                service.TryGetMovementSegment(
                    unit,
                    out from,
                    out to,
                    out progress),
                Is.True);
            Assert.That(from, Is.EqualTo(new GridCoordinate(1, 1)));
            Assert.That(to, Is.EqualTo(new GridCoordinate(2, 1)));
            Assert.That(progress, Is.Zero);
            Assert.That(
                service.GetRemainingMovementFixedSteps(unit),
                Is.EqualTo(2));
        }

        [Test]
        public void RealtimeMapGameplay_ReroutesWithoutExtraStaminaAndCancels()
        {
            var terrain = new GridTerrainKind[7 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                7,
                3,
                19,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new MinePlacement[0],
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 2,
                    aiDecisionIntervalSteps: 100));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            int initialStamina = unit.Stamina;
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(3, 1),
                    out _),
                Is.True);
            Assert.That(unit.Stamina, Is.EqualTo(initialStamina - 1));

            service.AdvanceFixedSteps(1);
            Assert.That(unit.MovementProgress, Is.EqualTo(1));

            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(6, 1),
                    out _),
                Is.True);
            Assert.That(unit.Stamina, Is.EqualTo(initialStamina - 1));
            Assert.That(
                unit.Destination,
                Is.EqualTo(new GridCoordinate(6, 1)));
            Assert.That(unit.MovementProgress, Is.Zero);
            Assert.That(unit.TotalMovementTileCount, Is.EqualTo(1));

            Assert.That(
                service.TryCancelMove("player", unit.Id, out _),
                Is.True);
            Assert.That(unit.IsMoving, Is.False);
            Assert.That(unit.Destination, Is.Null);
            Assert.That(unit.PlannedPath, Is.Empty);
            Assert.That(unit.Stamina, Is.EqualTo(initialStamina - 1));

            service.AdvanceFixedSteps(10);
            Assert.That(unit.Coordinate, Is.EqualTo(new GridCoordinate(0, 1)));
            Assert.That(
                service.TryCancelMove("player", unit.Id, out string reason),
                Is.False);
            Assert.That(reason, Does.Contain("이동 중이 아닙니다"));
        }

        [Test]
        public void RealtimeMapGameplay_AppendsWaypointAndKeepsCurrentProgress()
        {
            var terrain = new GridTerrainKind[9 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                9,
                3,
                23,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new MinePlacement[0],
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 2,
                    aiDecisionIntervalSteps: 100));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            int initialStamina = unit.Stamina;
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(2, 1),
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);

            Assert.That(
                service.TryAppendWaypoint(
                    "player",
                    unit.Id,
                    new GridCoordinate(4, 1),
                    out _),
                Is.True);
            Assert.That(unit.Stamina, Is.EqualTo(initialStamina - 1));
            Assert.That(unit.MovementProgress, Is.EqualTo(1));
            Assert.That(unit.TotalMovementTileCount, Is.EqualTo(4));
            Assert.That(unit.RemainingMovementTileCount, Is.EqualTo(4));
            Assert.That(unit.PlannedPath.Count, Is.EqualTo(5));
            Assert.That(
                unit.Destination,
                Is.EqualTo(new GridCoordinate(4, 1)));
            Assert.That(
                service.GetRemainingMovementFixedSteps(unit),
                Is.EqualTo(7));

            service.AdvanceFixedSteps(7);

            Assert.That(unit.Coordinate, Is.EqualTo(new GridCoordinate(4, 1)));
            Assert.That(unit.IsMoving, Is.False);
            Assert.That(unit.PlannedPath, Is.Empty);
        }

        [Test]
        public void RealtimeMapGameplay_HidesEnemyPathOutsideScoutingRange()
        {
            var terrain = new GridTerrainKind[20 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                20,
                3,
                29,
                new GridCoordinate(0, 1),
                new[] { new GridCoordinate(8, 1) },
                new MinePlacement[0],
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 100,
                    unitScoutingRange: 3));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState scout, out _),
                Is.True);
            Assert.That(
                service.TryCreateUnit("ai_1", out MapUnitState enemy, out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "ai_1",
                    enemy.Id,
                    new GridCoordinate(2, 1),
                    out _),
                Is.True);
            Assert.That(
                service.CanViewMovementPath("player", scout),
                Is.True);
            Assert.That(
                service.CanViewMovementPath("player", enemy),
                Is.False);

            service.AdvanceFixedSteps(5);

            Assert.That(enemy.Coordinate, Is.EqualTo(new GridCoordinate(3, 1)));
            Assert.That(enemy.IsMoving, Is.True);
            Assert.That(
                service.CanViewMovementPath("player", enemy),
                Is.True);
        }

        [Test]
        public void RealtimeMapGameplay_TracksCombatStatsMoraleAndFatigue()
        {
            var terrain = new GridTerrainKind[6 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                6,
                3,
                31,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new MinePlacement[0],
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 100,
                    initialSoldiersPerUnit: 100,
                    movementFatiguePerTile: 7m,
                    dailyFatigueRecovery: 5m));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(unit.Soldiers, Is.EqualTo(100));
            Assert.That(unit.Morale, Is.EqualTo(100m));
            Assert.That(unit.Fatigue, Is.Zero);
            Assert.That(unit.AttackPower, Is.EqualTo(100m));
            Assert.That(unit.DefensePower, Is.EqualTo(122m));

            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(1, 1),
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);

            Assert.That(unit.Fatigue, Is.EqualTo(7m));
            Assert.That(unit.AttackPower, Is.EqualTo(96.50m));
            Assert.That(unit.DefensePower, Is.EqualTo(117.73m));

            service.AdvanceEconomicDay(out _);

            Assert.That(unit.Fatigue, Is.EqualTo(2m));
            Assert.That(unit.AttackPower, Is.EqualTo(99m));
            Assert.That(unit.DefensePower, Is.EqualTo(120.78m));
        }

        [Test]
        public void RealtimeMapGameplay_FormsUnitBranchesAndAppliesCombatMix()
        {
            var terrain = new GridTerrainKind[4 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                4,
                2,
                35,
                new GridCoordinate(0, 0),
                new[] { new GridCoordinate(3, 1) },
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    aiDecisionIntervalSteps: 1000,
                    initialSoldiersPerUnit: 100));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(unit.Formation.Preset,
                Is.EqualTo(MapUnitFormationPreset.Frontline));
            Assert.That(unit.Formation.FrontlineSoldiers, Is.EqualTo(60));
            Assert.That(unit.Formation.RangedSoldiers, Is.EqualTo(25));
            Assert.That(unit.Formation.CavalrySoldiers, Is.EqualTo(15));
            Assert.That(unit.Formation.TotalSoldiers, Is.EqualTo(unit.Soldiers));
            Assert.That(unit.FormationAttackModifier, Is.EqualTo(1m));
            Assert.That(unit.FormationDefenseModifier, Is.EqualTo(1m));
            Assert.That(unit.WeightedBranchMobilityModifier,
                Is.EqualTo(1.07750m));
            Assert.That(unit.ArmorMobilityModifier, Is.EqualTo(0.92m));
            Assert.That(unit.MobilityModifier, Is.EqualTo(0.99130m));

            Assert.That(
                service.TrySetUnitFormationPreset(
                    "player",
                    unit.Id,
                    MapUnitFormationPreset.Ranged,
                    out _),
                Is.True);
            Assert.That(unit.Formation.FrontlineSoldiers, Is.EqualTo(40));
            Assert.That(unit.Formation.RangedSoldiers, Is.EqualTo(50));
            Assert.That(unit.Formation.CavalrySoldiers, Is.EqualTo(10));
            Assert.That(unit.FormationAttackModifier, Is.EqualTo(1.035m));
            Assert.That(unit.FormationDefenseModifier, Is.EqualTo(0.955m));
            Assert.That(unit.AttackPower, Is.EqualTo(103.50m));
            Assert.That(unit.DefensePower, Is.EqualTo(116.51m));
            Assert.That(unit.MobilityModifier, Is.EqualTo(0.943m));

            Assert.That(
                service.TrySetUnitFormationPreset(
                    "player",
                    unit.Id,
                    MapUnitFormationPreset.Cavalry,
                    out _),
                Is.True);
            Assert.That(unit.MobilityModifier, Is.EqualTo(1.23786m));

            Assert.That(
                service.TrySetUnitFormation(
                    "player",
                    unit.Id,
                    50,
                    30,
                    19,
                    out string invalidReason),
                Is.False);
            Assert.That(invalidReason, Does.Contain("총병력"));
            Assert.That(unit.Formation.Preset,
                Is.EqualTo(MapUnitFormationPreset.Cavalry));

            Assert.That(
                service.TrySetUnitFormation(
                    "player",
                    unit.Id,
                    50,
                    30,
                    20,
                    out _),
                Is.True);
            Assert.That(unit.Formation.Preset,
                Is.EqualTo(MapUnitFormationPreset.Custom));
            Assert.That(unit.Formation.TotalSoldiers, Is.EqualTo(100));
            Assert.That(
                service.TrySetUnitFormationPreset(
                    "ai_1",
                    unit.Id,
                    MapUnitFormationPreset.Cavalry,
                    out string ownershipReason),
                Is.False);
            Assert.That(ownershipReason, Does.Contain("다른 세력"));
        }

        [Test]
        public void MilitaryArchetypes_KeepConfiguredStatMultipliers()
        {
            MilitaryBalanceCatalog catalog =
                MilitaryBalanceCatalog.CreatePrototypeDefaults();
            UnitArchetypeDefinition spearman =
                catalog.Get(UnitArchetype.Spearman);
            Assert.That(spearman.BaseAttack, Is.EqualTo(0.90m));
            Assert.That(spearman.BaseDefense, Is.EqualTo(1.08m));
            Assert.That(spearman.Mobility, Is.EqualTo(0.88m));
            Assert.That(spearman.Morale, Is.EqualTo(1.05m));
            Assert.That(
                UnitEquipmentCatalog.GetArchetypeMobilityModifier(
                    UnitArchetype.Cavalry),
                Is.EqualTo(1.65m));

            var configuredDefinition = new UnitArchetypeDefinition(
                UnitArchetype.Cavalry,
                "설정 기병",
                1.8m,
                0.6m,
                2.2m,
                1.4m,
                0,
                0m,
                1m,
                0.9m,
                0m,
                0.12m,
                0.04m,
                2.1m,
                new DamageProfile(0m, 0m, 0m),
                new DamageProfile(0.48m, 0.38m, 0.14m));
            Assert.That(configuredDefinition.BaseAttack, Is.EqualTo(1.8m));
            Assert.That(configuredDefinition.BaseDefense, Is.EqualTo(0.6m));
            Assert.That(configuredDefinition.Mobility, Is.EqualTo(2.2m));
            Assert.That(configuredDefinition.Morale, Is.EqualTo(1.4m));
        }

        [Test]
        public void RealtimeMapGameplay_HiresCommanderAndAppliesLoyaltyEffects()
        {
            var terrain = new GridTerrainKind[4 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                4,
                2,
                36,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(fixedStepsPerMove: 1000));

            Assert.That(service.Commanders.Count, Is.EqualTo(5));
            Assert.That(
                service.Commanders
                    .Where(candidate => !candidate.IsProtagonist)
                    .Select(candidate => candidate.Personality),
                Is.EquivalentTo(new[]
                {
                    MapCommanderPersonality.Aggressive,
                    MapCommanderPersonality.Cautious,
                    MapCommanderPersonality.Opportunistic,
                    MapCommanderPersonality.Logistician
                }));
            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            decimal attackWithoutCommander = unit.AttackPower;
            decimal defenseWithoutCommander = unit.DefensePower;
            decimal mobilityWithoutCommander = unit.MobilityModifier;
            int movementStepsWithoutCommander =
                service.GetRequiredMovementStepsPerTile(unit);
            MapCommanderState commander = service.Commanders[0];

            Assert.That(commander.IsAvailable, Is.True);
            Assert.That(
                service.TryHireCommander(
                    "player",
                    commander.Id,
                    unit.Id,
                    out _),
                Is.True);
            Assert.That(unit.Commander, Is.SameAs(commander));
            Assert.That(commander.EmployerFactionId, Is.EqualTo("player"));
            Assert.That(commander.AssignedUnitId, Is.EqualTo(unit.Id));
            Assert.That(commander.IsAvailable, Is.False);
            Assert.That(unit.AttackPower, Is.GreaterThan(attackWithoutCommander));
            Assert.That(unit.DefensePower, Is.GreaterThan(defenseWithoutCommander));
            Assert.That(unit.MobilityModifier,
                Is.LessThan(mobilityWithoutCommander));
            Assert.That(
                service.GetRequiredMovementStepsPerTile(unit),
                Is.GreaterThan(movementStepsWithoutCommander));
            Assert.That(unit.CommanderAttackModifier,
                Is.EqualTo(1.088032m));
            Assert.That(unit.CommanderDefenseModifier,
                Is.EqualTo(1.017808m));

            Assert.That(
                service.TryHireCommander(
                    "player",
                    service.Commanders[1].Id,
                    unit.Id,
                    out string occupiedReason),
                Is.False);
            Assert.That(occupiedReason, Does.Contain("이미"));

            var lowLoyalty = new MapCommanderState(
                "low_loyalty",
                "낮은 충성",
                80,
                80,
                80,
                MapCommanderPersonality.Opportunistic,
                10,
                1m);
            var highLoyalty = new MapCommanderState(
                "high_loyalty",
                "높은 충성",
                80,
                80,
                80,
                MapCommanderPersonality.Opportunistic,
                90,
                1m);
            Assert.That(highLoyalty.AttackModifier,
                Is.GreaterThan(lowLoyalty.AttackModifier));
            Assert.That(highLoyalty.MobilityModifier,
                Is.GreaterThan(lowLoyalty.MobilityModifier));
        }

        [Test]
        public void CommanderBattleRules_UseThreeAndFivePercentAndProtectProtagonist()
        {
            Assert.That(
                MapCommanderBattleRules.ShouldGenerateCommanderAfterVictory(
                    299),
                Is.True);
            Assert.That(
                MapCommanderBattleRules.ShouldGenerateCommanderAfterVictory(
                    300),
                Is.False);

            var regular = new MapCommanderState(
                "regular",
                "일반 장수",
                70,
                70,
                70,
                MapCommanderPersonality.Cautious,
                70,
                1000m);
            var protagonist = new MapCommanderState(
                "protagonist",
                "주인공",
                80,
                80,
                80,
                MapCommanderPersonality.Cautious,
                100,
                0m,
                isProtagonist: true);

            Assert.That(
                MapCommanderBattleRules.ShouldCommanderDieAfterDefeat(
                    regular,
                    499),
                Is.True);
            Assert.That(
                MapCommanderBattleRules.ShouldCommanderDieAfterDefeat(
                    regular,
                    500),
                Is.False);
            Assert.That(
                MapCommanderBattleRules.ShouldCommanderDieAfterDefeat(
                    protagonist,
                    0),
                Is.False);
        }

        [Test]
        public void CommanderUpkeepRules_ContinuouslySurchargeExcessTroops()
        {
            var commander = new MapCommanderState(
                "upkeep_commander",
                "유지비 장수",
                50,
                70,
                70,
                MapCommanderPersonality.Cautious,
                70,
                1000m);

            Assert.That(
                MapCommanderUpkeepRules.GetCommandCapacity(commander),
                Is.EqualTo(200));
            Assert.That(
                MapCommanderUpkeepRules.CalculateBaseUpkeep(300),
                Is.EqualTo(600m));
            Assert.That(
                MapCommanderUpkeepRules.CalculateConcentrationSurcharge(
                    commander,
                    200),
                Is.EqualTo(0m));
            Assert.That(
                MapCommanderUpkeepRules.CalculateConcentrationSurcharge(
                    commander,
                    300),
                Is.EqualTo(22.50m));
            Assert.That(
                MapCommanderUpkeepRules.CalculateConcentrationSurcharge(
                    commander,
                    500),
                Is.EqualTo(337.50m));
            Assert.That(
                MapCommanderUpkeepRules.CalculateConcentrationSurcharge(
                    commander,
                    800),
                Is.EqualTo(2160m));
            Assert.That(
                MapCommanderUpkeepRules.CalculateConcentrationSurcharge(
                    null,
                    1000),
                Is.EqualTo(0m));
        }

        [Test]
        public void RealtimeMapGameplay_CreatesDailyCommanderUpkeepRecords()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                4 * 2).ToArray();
            var layout = new GridMapLayout(
                4,
                2,
                364,
                new GridCoordinate(0, 0),
                Array.Empty<GridCoordinate>(),
                Array.Empty<MinePlacement>(),
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1000,
                    initialSoldiersPerUnit: 500),
                enableAi: false);
            Assert.That(
                service.TryCreateUnit(
                    "player",
                    out MapUnitState unit,
                    out _),
                Is.True);
            MapCommanderState commander = service.Commanders[0];
            Assert.That(
                service.TryHireCommander(
                    "player",
                    commander.Id,
                    unit.Id,
                    out _),
                Is.True);

            MapMilitaryUpkeepRecord upkeep =
                service.CreateDailyMilitaryUpkeep().Single();

            Assert.That(upkeep.UnitId, Is.EqualTo(unit.Id));
            Assert.That(upkeep.CommanderId, Is.EqualTo(commander.Id));
            Assert.That(upkeep.Soldiers, Is.EqualTo(500));
            Assert.That(upkeep.CommandCapacity, Is.EqualTo(244));
            Assert.That(upkeep.BaseUpkeep, Is.EqualTo(1000m));
            Assert.That(
                upkeep.ConcentrationSurcharge,
                Is.EqualTo(165.1169m).Within(0.0001m));
            Assert.That(
                upkeep.TotalUpkeep,
                Is.EqualTo(1165.1169m).Within(0.0001m));
        }

        [Test]
        public void RealtimeMapGameplay_AiSummonsSharedCommanderAtFriendlyCastle()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                6 * 3).ToArray();
            var layout = new GridMapLayout(
                6,
                3,
                363,
                new GridCoordinate(0, 1),
                new[] { new GridCoordinate(5, 1) },
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1000,
                    aiDecisionIntervalSteps: 1));

            service.AdvanceFixedSteps(1);

            MapUnitState aiUnit = service.Units.Single(unit =>
                unit.OwnerFactionId == "ai_1");
            Assert.That(aiUnit.Commander, Is.Not.Null);
            Assert.That(aiUnit.Commander.IsProtagonist, Is.False);
            Assert.That(aiUnit.Commander.EmployerFactionId, Is.EqualTo("ai_1"));
            Assert.That(aiUnit.Commander.AssignedUnitId, Is.EqualTo(aiUnit.Id));
        }

        [Test]
        public void SubordinateMissionPlanner_UsesCommanderPersonalityAndUnitReadiness()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                4 * 2).ToArray();
            var layout = new GridMapLayout(
                4,
                2,
                362,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(fixedStepsPerMove: 1000));
            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            MapCommanderState commander = service.Commanders.Single(
                candidate => candidate.Personality ==
                    MapCommanderPersonality.Aggressive);
            Assert.That(
                service.TryHireCommander(
                    "player",
                    commander.Id,
                    unit.Id,
                    out _),
                Is.True);
            var opportunity = new WorldOpportunity(
                "bandit_contract",
                "bandit_event",
                WorldOpportunityKind.SuppressBandits,
                "도적 토벌",
                new RegionId("starter"),
                new TurnNumber(1),
                new TurnNumber(4),
                0.5m,
                2500m,
                3m);

            WorldOperationApproach recommended =
                SubordinateMissionPlanner.GetRecommendedApproach(
                    opportunity,
                    commander,
                    unit);
            Assert.That(
                recommended,
                Is.EqualTo(WorldOperationApproach.ArmedSecurity));
            Assert.That(
                SubordinateMissionPlanner.TryCreatePlan(
                    opportunity,
                    commander,
                    unit,
                    recommended,
                    out SubordinateMissionPlan plan,
                    out _),
                Is.True);
            Assert.That(plan.CommanderId, Is.EqualTo(commander.Id));
            Assert.That(plan.UnitId, Is.EqualTo(unit.Id));
            Assert.That(plan.UnitReadiness, Is.EqualTo(100m));
            Assert.That(plan.Capability, Is.GreaterThan(60m));
            Assert.That(plan.IsRecommendedApproach, Is.True);

            var unassigned = new MapCommanderState(
                "unassigned",
                "미배속 지휘관",
                90,
                90,
                90,
                MapCommanderPersonality.Logistician,
                90,
                0m);
            Assert.That(
                SubordinateMissionPlanner.TryCreatePlan(
                    opportunity,
                    unassigned,
                    unit,
                    recommended,
                    out _,
                    out string reason),
                Is.False);
            Assert.That(reason, Does.Contain("고용"));
        }

        [Test]
        public void MapGenerationSettings_ControlSizeResourcesWaterAndSeed()
        {
            var sparse = new MapGenerationSettings(
                MapSizePreset.Standard,
                MapResourceAbundance.Sparse,
                MapWaterLevel.Low,
                seed: 777);
            var rich = new MapGenerationSettings(
                MapSizePreset.Standard,
                MapResourceAbundance.Rich,
                MapWaterLevel.High,
                seed: 777);
            Assert.That(rich.MineCount, Is.GreaterThan(sparse.MineCount));

            var generator = new GridMapLayoutGenerator();
            var start = new GridCoordinate(4, 24);
            GridMapLayout lowWater = generator.Generate(
                sparse.Width,
                sparse.Height,
                sparse.MineCount,
                sparse.Seed,
                start,
                new GridCoordinate[0],
                sparse.WrapHorizontally,
                sparse.NeutralCastleCount,
                sparse.OceanThreshold);
            GridMapLayout highWater = generator.Generate(
                rich.Width,
                rich.Height,
                rich.MineCount,
                rich.Seed,
                start,
                new GridCoordinate[0],
                rich.WrapHorizontally,
                rich.NeutralCastleCount,
                rich.OceanThreshold);

            Assert.That(lowWater.Seed, Is.EqualTo(777));
            Assert.That(
                highWater.Terrain.Count(x => x == GridTerrainKind.Ocean),
                Is.GreaterThan(
                    lowWater.Terrain.Count(x => x == GridTerrainKind.Ocean)));
        }

        [Test]
        public void WorldMission_MovesUnitToBoundMapCoordinateBeforeReady()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                6 * 2).ToArray();
            var layout = new GridMapLayout(
                6,
                2,
                91,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new[]
                {
                    new MinePlacement(new GridCoordinate(4, 0), MineKind.Normal)
                },
                false,
                terrain);
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000),
                enableAi: false);
            Assert.That(
                gameplay.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            var opportunity = new WorldOpportunity(
                "survey_1",
                "vein_1",
                WorldOpportunityKind.SurveyVein,
                "광맥 정찰",
                new RegionId("north"),
                new TurnNumber(1),
                new TurnNumber(5),
                0.4m,
                100m,
                1m);
            Assert.That(
                WorldMissionMapBinder.TryBind(
                    opportunity,
                    layout,
                    gameplay,
                    out WorldMissionMapTarget target,
                    out _),
                Is.True);
            Assert.That(target.Coordinate, Is.EqualTo(new GridCoordinate(4, 0)));
            Assert.That(target.Action, Is.EqualTo(MapWorldMissionAction.Scout));

            int readyCount = 0;
            gameplay.WorldMissionReady += _ => readyCount++;
            Assert.That(
                gameplay.TryAssignWorldMission(
                    "player",
                    unit.Id,
                    opportunity.Id,
                    target.Coordinate,
                    target.Action,
                    out _),
                Is.True);
            gameplay.AdvanceFixedSteps(3);
            Assert.That(readyCount, Is.Zero);
            Assert.That(unit.Coordinate, Is.EqualTo(new GridCoordinate(3, 0)));
            gameplay.AdvanceFixedSteps(1);
            Assert.That(readyCount, Is.EqualTo(1));
            Assert.That(unit.Coordinate, Is.EqualTo(target.Coordinate));
        }

        [Test]
        public void CargoMission_LoadsRealCastleStockBeforeDelivery()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                6 * 2).ToArray();
            var layout = new GridMapLayout(
                6,
                2,
                93,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000),
                enableAi: false);
            Assert.That(
                gameplay.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                gameplay.TryStockFactionCapitalWarehouse(
                    "player",
                    MapSupplyKind.Food,
                    20m,
                    out decimal stored),
                Is.True);
            Assert.That(stored, Is.EqualTo(20m));
            var target = new WorldMissionMapTarget(
                new GridCoordinate(4, 0),
                MapWorldMissionAction.Deliver,
                MapSupplyKind.Food,
                12m);
            MapWorldMissionState ready = null;
            gameplay.WorldMissionReady += mission => ready = mission;

            Assert.That(
                gameplay.TryAssignWorldMission(
                    "player",
                    unit.Id,
                    "delivery_1",
                    target,
                    out string reason),
                Is.True,
                reason);
            Assert.That(
                gameplay.FindCapital("player").WarehouseFoodAmount,
                Is.EqualTo(8m));
            gameplay.AdvanceFixedSteps(4);

            Assert.That(ready, Is.Not.Null);
            Assert.That(ready.DeliveredCargo, Is.EqualTo(12m));
            Assert.That(ready.LoadedCargo, Is.Zero);
            Assert.That(ready.ExecutionBonus, Is.GreaterThan(0m));
        }

        [Test]
        public void MissionBinder_MapsApproachesToDeliverySmugglingAndSabotage()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                6 * 2).ToArray();
            var layout = new GridMapLayout(
                6,
                2,
                96,
                new GridCoordinate(0, 0),
                new[] { new GridCoordinate(4, 0) },
                new MinePlacement[0],
                false,
                terrain);
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                enableAi: false);

            WorldOpportunity Create(WorldOpportunityKind kind, string id) =>
                new WorldOpportunity(
                    id,
                    id + "_event",
                    kind,
                    id,
                    new RegionId("r0"),
                    new TurnNumber(1),
                    new TurnNumber(4),
                    0.5m,
                    100m,
                    1m);

            Assert.That(
                WorldMissionMapBinder.TryBind(
                    Create(WorldOpportunityKind.EmergencyDelivery, "delivery"),
                    layout,
                    gameplay,
                    out WorldMissionMapTarget delivery,
                    out _,
                    WorldOperationApproach.Logistics,
                    new ResourceId("food")),
                Is.True);
            Assert.That(delivery.Action, Is.EqualTo(MapWorldMissionAction.Deliver));
            Assert.That(delivery.CargoKind, Is.EqualTo(MapSupplyKind.Food));

            Assert.That(
                WorldMissionMapBinder.TryBind(
                    Create(WorldOpportunityKind.EscortSupply, "smuggle"),
                    layout,
                    gameplay,
                    out WorldMissionMapTarget smuggle,
                    out _,
                    WorldOperationApproach.CovertAction,
                    new ResourceId("steel")),
                Is.True);
            Assert.That(smuggle.Action, Is.EqualTo(MapWorldMissionAction.Smuggle));
            Assert.That(smuggle.CargoKind, Is.EqualTo(MapSupplyKind.Equipment));

            Assert.That(
                WorldMissionMapBinder.TryBind(
                    Create(WorldOpportunityKind.ProtectFacility, "sabotage"),
                    layout,
                    gameplay,
                    out WorldMissionMapTarget sabotage,
                    out _,
                    WorldOperationApproach.CovertAction),
                Is.True);
            Assert.That(sabotage.Action, Is.EqualTo(MapWorldMissionAction.Sabotage));
            Assert.That(sabotage.Coordinate, Is.EqualTo(new GridCoordinate(4, 0)));
        }

        [Test]
        public void SabotageMission_DamagesEnemyFacilityProxyAfterArrival()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                6 * 2).ToArray();
            var enemyStart = new GridCoordinate(4, 0);
            var layout = new GridMapLayout(
                6,
                2,
                94,
                new GridCoordinate(0, 0),
                new[] { enemyStart },
                new MinePlacement[0],
                false,
                terrain);
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000),
                enableAi: false);
            Assert.That(
                gameplay.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            MapCastleControlState targetCastle = gameplay.FindCastle(enemyStart);
            int wallBefore = targetCastle.WallDurability;
            MapWorldMissionState ready = null;
            gameplay.WorldMissionReady += mission => ready = mission;

            Assert.That(
                gameplay.TryAssignWorldMission(
                    "player",
                    unit.Id,
                    "sabotage_1",
                    new WorldMissionMapTarget(
                        enemyStart,
                        MapWorldMissionAction.Sabotage),
                    out string reason),
                Is.True,
                reason);
            gameplay.AdvanceFixedSteps(4);

            Assert.That(ready, Is.Not.Null);
            Assert.That(ready.SabotageDamage, Is.GreaterThan(0));
            Assert.That(targetCastle.WallDurability, Is.LessThan(wallBefore));
            Assert.That(unit.Fatigue, Is.GreaterThan(0m));
        }

        [Test]
        public void WorldMapOwnership_SynchronizesCapturedSitesAndRegionsBothWays()
        {
            var regions = new[]
            {
                new GeneratedRegionState(
                    new RegionId("r0"), "서부", TerrainType.Plains,
                    1000, 0.4m, 0.7m, 0.5m, 0.8m, 0.1m),
                new GeneratedRegionState(
                    new RegionId("r1"), "동부", TerrainType.Hills,
                    1000, 0.3m, 0.8m, 0.4m, 0.8m, 0.1m)
            };
            var factions = new[]
            {
                new WorldFactionState("wf_player", "청람", 1000m, 0.5m, 0.5m, 0.5m),
                new WorldFactionState("wf_ai", "적월", 1000m, 0.5m, 0.5m, 0.5m)
            };
            var facility = new WorldFacilityState(
                "facility_r1",
                WorldFacilityKind.Workshop,
                regions[1].Id,
                factions[1].Id,
                new ResourceId("iron"),
                new ResourceId("steel"),
                2m, 1m, 10m, 5m);
            var procedural = new ProceduralWorldState(
                95,
                regions,
                factions,
                new FactionRelationState[0],
                new WorldNpcState[0],
                new[] { facility },
                new RegionalEconomySeed[0],
                new ResourceSiteSeed[0]);
            var world = new AutonomousWorldState(procedural);
            var site = new ResourceExtractionSite(
                "site_r0",
                regions[0].Id,
                new ResourceId("iron"),
                new TurnNumber(1),
                10m,
                2m,
                0m);
            world.AddResourceSite(site);

            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                4 * 2).ToArray();
            var mineCoordinate = new GridCoordinate(1, 0);
            var castleCoordinate = new GridCoordinate(3, 1);
            var layout = new GridMapLayout(
                4,
                2,
                95,
                new GridCoordinate(0, 0),
                new[] { new GridCoordinate(3, 0) },
                new[] { new MinePlacement(mineCoordinate, MineKind.Normal) },
                false,
                terrain,
                new[] { castleCoordinate });
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                enableAi: false);
            var sync = new WorldMapOwnershipSynchronizer(world);

            Assert.That(
                sync.ApplyMineCapture(
                    layout,
                    gameplay,
                    new MapMineCaptureRecord(
                        mineCoordinate,
                        MineKind.Normal,
                        string.Empty,
                        "player")),
                Is.True);
            Assert.That(site.OwnerFactionId, Is.EqualTo("wf_player"));
            Assert.That(
                sync.ApplyCastleCapture(
                    layout,
                    gameplay,
                    new MapCastleCaptureRecord(
                        castleCoordinate,
                        string.Empty,
                        "player",
                        false)),
                Is.True);
            Assert.That(regions[1].OwnerFactionId, Is.EqualTo("wf_player"));
            Assert.That(facility.OwnerFactionId, Is.EqualTo("wf_player"));

            site.AssignOwner("wf_ai");
            regions[1].AssignOwner("wf_ai");
            Assert.That(
                sync.SynchronizeLinkedOwnershipToMap(layout, gameplay),
                Is.True);
            Assert.That(
                gameplay.FindMine(mineCoordinate).OwnerFactionId,
                Is.EqualTo("ai_1"));
            Assert.That(
                gameplay.FindCastle(castleCoordinate).OwnerFactionId,
                Is.EqualTo("ai_1"));
        }

        [Test]
        public void FactionStrategicAi_ChangesTargetsByEconomicAndMilitaryPlan()
        {
            var terrain = Enumerable.Repeat(
                GridTerrainKind.Plains,
                10 * 2).ToArray();
            var layout = new GridMapLayout(
                10,
                2,
                92,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new[]
                {
                    new MinePlacement(new GridCoordinate(1, 0), MineKind.Normal)
                },
                false,
                terrain,
                new[] { new GridCoordinate(3, 0) });
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                enableAi: false);
            Assert.That(
                gameplay.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);

            Assert.That(
                FactionStrategicAi.TryChooseObjective(
                    layout,
                    gameplay.Mines,
                    gameplay.Castles,
                    gameplay.Units,
                    "player",
                    unit,
                    FactionStrategyKind.EconomicExpansion,
                    null,
                    out StrategicObjective economic),
                Is.True);
            Assert.That(economic.Kind, Is.EqualTo(StrategicObjectiveKind.Mine));

            Assert.That(
                FactionStrategicAi.TryChooseObjective(
                    layout,
                    gameplay.Mines,
                    gameplay.Castles,
                    gameplay.Units,
                    "player",
                    unit,
                    FactionStrategyKind.MilitaryDominance,
                    null,
                    out StrategicObjective military),
                Is.True);
            Assert.That(
                military.Kind,
                Is.EqualTo(StrategicObjectiveKind.Castle));
        }

        [Test]
        public void DelegatedMissionCommand_AttributesResultToCommander()
        {
            var world = new RecordingAutonomousWorldService();
            var command = new InterveneWorldOpportunityTurnCommand(
                world,
                "mission_1",
                new CompanyId("player"),
                82m,
                "도적 토벌 · 윤서 위임",
                WorldOperationApproach.ArmedSecurity,
                "commander_yunseo",
                "윤서",
                true);

            command.Execute(new TurnCommandContext(null));

            Assert.That(world.ResolverId, Is.EqualTo("commander_yunseo"));
            Assert.That(world.ResolverDisplayName, Is.EqualTo("윤서"));
            Assert.That(world.WasDelegated, Is.True);
            Assert.That(world.Capability, Is.EqualTo(82m));
            Assert.That(
                world.Approach,
                Is.EqualTo(WorldOperationApproach.ArmedSecurity));
        }

        [Test]
        public void RealtimeMapGameplay_HiresCommanderOnlyAtFriendlyCastle()
        {
            var terrain = new GridTerrainKind[3 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                3,
                2,
                361,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(fixedStepsPerMove: 1));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(1, 0),
                    out _),
                Is.True);
            service.AdvanceFixedSteps(
                service.GetRequiredMovementStepsPerTile(unit));
            Assert.That(unit.Coordinate, Is.EqualTo(new GridCoordinate(1, 0)));

            Assert.That(
                service.TryHireCommander(
                    "player",
                    service.Commanders[0].Id,
                    unit.Id,
                    out string reason),
                Is.False);
            Assert.That(reason, Does.Contain("아군 성"));
            Assert.That(service.Commanders[0].IsAvailable, Is.True);
            Assert.That(unit.Commander, Is.Null);
        }

        [Test]
        public void RealtimeMapGameplay_SpawnsMinesAndDepletesOwnedYield()
        {
            var terrain = new GridTerrainKind[6 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var initialMine = new GridCoordinate(1, 1);
            var playerBase = new GridCoordinate(0, 1);
            var layout = new GridMapLayout(
                6,
                3,
                37,
                playerBase,
                new GridCoordinate[0],
                new[] { new MinePlacement(initialMine, MineKind.Normal) },
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCapture: 1,
                    aiDecisionIntervalSteps: 100,
                    normalMineIronPerDay: 100m,
                    goldMineCashPerDay: 1000m,
                    mineSpawnIntervalDays: 2,
                    mineDailyDepletionRate: 0.5m,
                    minimumMineYieldMultiplier: 0.25m));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                service.TryIssueMove("player", unit.Id, initialMine, out _),
                Is.True);
            service.AdvanceFixedSteps(1);

            MapMineControlState capturedMine = service.FindMine(initialMine);
            Assert.That(capturedMine.OwnerFactionId, Is.EqualTo("player"));
            Assert.That(
                service.CreateDailyProduction()[0].IronAmount,
                Is.EqualTo(100m));
            Assert.That(capturedMine.YieldMultiplier, Is.EqualTo(0.5m));
            Assert.That(
                service.CreateDailyProduction()[0].IronAmount,
                Is.EqualTo(50m));
            Assert.That(capturedMine.YieldMultiplier, Is.EqualTo(0.25m));
            Assert.That(
                service.CreateDailyProduction()[0].IronAmount,
                Is.EqualTo(25m));
            Assert.That(capturedMine.YieldMultiplier, Is.EqualTo(0.25m));

            Assert.That(service.AdvanceEconomicDay(out _), Is.False);
            Assert.That(
                service.AdvanceEconomicDay(out MapMineSpawnRecord ironSpawn),
                Is.True);
            Assert.That(ironSpawn.Kind, Is.EqualTo(MineKind.Normal));
            Assert.That(ironSpawn.EconomicDay, Is.EqualTo(2));
            Assert.That(ironSpawn.Coordinate, Is.Not.EqualTo(playerBase));
            Assert.That(ironSpawn.Coordinate, Is.Not.EqualTo(initialMine));
            Assert.That(service.FindMine(ironSpawn.Coordinate).IsDynamic, Is.True);

            Assert.That(service.AdvanceEconomicDay(out _), Is.False);
            Assert.That(
                service.AdvanceEconomicDay(out MapMineSpawnRecord goldSpawn),
                Is.True);
            Assert.That(goldSpawn.Kind, Is.EqualTo(MineKind.Gold));
            Assert.That(goldSpawn.EconomicDay, Is.EqualTo(4));
            Assert.That(goldSpawn.Coordinate, Is.Not.EqualTo(playerBase));
            Assert.That(goldSpawn.Coordinate, Is.Not.EqualTo(initialMine));
        }

        [Test]
        public void RealtimeMapGameplay_CapturesCastleStoresGarrisonAndRole()
        {
            var terrain = new GridTerrainKind[5 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var castleCoordinate = new GridCoordinate(2, 1);
            var layout = new GridMapLayout(
                5,
                3,
                43,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain,
                new[] { castleCoordinate });
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000,
                    fixedStepsToCaptureCastle: 2,
                    fixedStepsToSiegeUndefendedCastle: 3));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                service.TryIssueCastleOccupation(
                    "player",
                    unit.Id,
                    castleCoordinate,
                    out _),
                Is.True);

            service.AdvanceFixedSteps(3);

            MapCastleControlState castle = service.FindCastle(castleCoordinate);
            Assert.That(castle.OwnerFactionId, Is.EqualTo("player"));
            Assert.That(castle.GarrisonUnitIds, Does.Contain(unit.Id));
            Assert.That(castle.ConflictKind, Is.EqualTo(MapCastleConflictKind.None));
            Assert.That(castle.Role, Is.EqualTo(MapCastleRole.Unassigned));
            Assert.That(castle.WallDurability, Is.EqualTo(1000));
            Assert.That(castle.MaxWallDurability, Is.EqualTo(1000));
            Assert.That(castle.FoodSupply, Is.EqualTo(500));
            Assert.That(castle.MaxFoodSupply, Is.EqualTo(500));
            Assert.That(castle.DefenseBonus, Is.EqualTo(0.350m));
            Assert.That(
                castle.OccupationPolicy,
                Is.EqualTo(MapOccupationPolicy.None));
            Assert.That(castle.PublicOrder, Is.EqualTo(50));
            Assert.That(
                service.TrySetOccupationPolicy(
                    "player",
                    castleCoordinate,
                    MapOccupationPolicy.Preserve,
                    out _),
                Is.True);
            Assert.That(
                castle.OccupationPolicy,
                Is.EqualTo(MapOccupationPolicy.Preserve));
            Assert.That(castle.PublicOrder, Is.EqualTo(60));
            Assert.That(
                service.TrySetOccupationPolicy(
                    "player",
                    castleCoordinate,
                    MapOccupationPolicy.Loot,
                    out string policyReason),
                Is.False);
            Assert.That(policyReason, Does.Contain("이미 확정"));
            Assert.That(
                service.TrySetCastleRole(
                    "player",
                    castleCoordinate,
                    MapCastleRole.Port,
                    out string portReason),
                Is.False);
            Assert.That(portReason, Does.Contain("바다"));
            Assert.That(
                service.TrySetCastleRole(
                    "player",
                    castleCoordinate,
                    MapCastleRole.IndustrialCity,
                    out _),
                Is.True);
            Assert.That(castle.Role, Is.EqualTo(MapCastleRole.IndustrialCity));
            Assert.That(castle.WallDurability, Is.EqualTo(1200));
            Assert.That(castle.MaxWallDurability, Is.EqualTo(1200));
            Assert.That(castle.FoodSupply, Is.EqualTo(1000));
            Assert.That(castle.MaxFoodSupply, Is.EqualTo(1000));
            Assert.That(castle.DefenseBonus, Is.EqualTo(0.370m));
            Assert.That(
                service.TryCreateUnitAt(
                    "player",
                    castleCoordinate,
                    UnitArchetype.Spearman,
                    UnitWeaponType.Spear,
                    ArmorClass.Light,
                    out MapUnitState localRecruit,
                    out _),
                Is.True);
            Assert.That(localRecruit.Coordinate, Is.EqualTo(castleCoordinate));
            Assert.That(castle.GarrisonUnitCount, Is.EqualTo(2));
            Assert.That(castle.DefenseBonus, Is.EqualTo(0.420m));
            Assert.That(
                service.CanCreateUnitAt(
                    "player",
                    castleCoordinate,
                    out string fullCastleReason),
                Is.False);
            Assert.That(fullCastleReason, Does.Contain("주둔 한도"));

            Assert.That(
                service.TrySetCastleRole(
                    "player",
                    castleCoordinate,
                    MapCastleRole.MilitaryFortress,
                    out _),
                Is.True);
            Assert.That(castle.MaxWallDurability, Is.EqualTo(1800));
            Assert.That(castle.WallDurability, Is.EqualTo(1800));
            Assert.That(castle.MaxFoodSupply, Is.EqualTo(1200));
            Assert.That(castle.FoodSupply, Is.EqualTo(1200));
            Assert.That(castle.DefenseBonus, Is.EqualTo(0.700m));
            Assert.That(
                service.TryGetRecruitmentSiteSnapshot(
                    "player",
                    castleCoordinate,
                    out MapRecruitmentSiteSnapshot fortressSite),
                Is.True);
            Assert.That(fortressSite.GarrisonCapacity, Is.EqualTo(5));
            Assert.That(fortressSite.RecruitmentCapacity, Is.EqualTo(4));
            Assert.That(fortressSite.RecruitRecoveryDays, Is.EqualTo(1));

            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(3, 1),
                out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    localRecruit.Id,
                    new GridCoordinate(1, 1),
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);
            Assert.That(castle.GarrisonUnitCount, Is.EqualTo(0));
        }

        [Test]
        public void RealtimeMapGameplay_DefendedCastleBecomesSiegeTarget()
        {
            var terrain = new GridTerrainKind[6 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var castleCoordinate = new GridCoordinate(4, 1);
            var layout = new GridMapLayout(
                6,
                3,
                47,
                new GridCoordinate(0, 1),
                new[] { new GridCoordinate(5, 1) },
                new MinePlacement[0],
                false,
                terrain,
                new[] { castleCoordinate });
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000,
                    fixedStepsToCaptureCastle: 1,
                    fixedStepsToSiegeUndefendedCastle: 2));

            Assert.That(
                service.TryCreateUnit("ai_1", out MapUnitState defender, out _),
                Is.True);
            Assert.That(
                service.TryIssueCastleOccupation(
                    "ai_1",
                    defender.Id,
                    castleCoordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);
            MapCastleControlState castle = service.FindCastle(castleCoordinate);
            Assert.That(castle.OwnerFactionId, Is.EqualTo("ai_1"));
            Assert.That(castle.GarrisonUnitCount, Is.EqualTo(1));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState attacker, out _),
                Is.True);
            Assert.That(
                service.TryIssueCastleOccupation(
                    "player",
                    attacker.Id,
                    castleCoordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(4);

            Assert.That(castle.OwnerFactionId, Is.EqualTo("ai_1"));
            Assert.That(castle.IsUnderSiege, Is.True);
            Assert.That(castle.CapturingFactionId, Is.EqualTo("player"));
            Assert.That(castle.CaptureProgress, Is.EqualTo(0));
            Assert.That(
                castle.SiegeAction,
                Is.EqualTo(MapSiegeAction.Encirclement));
            Assert.That(
                service.TrySetSiegeAction(
                    "player",
                    attacker.Id,
                    castleCoordinate,
                    MapSiegeAction.Assault,
                    out _),
                Is.True);
            Assert.That(castle.SiegeAction, Is.EqualTo(MapSiegeAction.Assault));
            Assert.That(
                service.TrySetSiegeAction(
                    "player",
                    attacker.Id,
                    castleCoordinate,
                    MapSiegeAction.Blockade,
                    out _),
                Is.True);
            Assert.That(castle.SiegeAction, Is.EqualTo(MapSiegeAction.Blockade));
            Assert.That(
                service.TrySetSiegeAction(
                    "player",
                    attacker.Id,
                    castleCoordinate,
                    MapSiegeAction.Negotiation,
                    out _),
                Is.True);
            Assert.That(
                castle.SiegeAction,
                Is.EqualTo(MapSiegeAction.Negotiation));
            Assert.That(
                service.TrySetSiegeAction(
                    "player",
                    attacker.Id,
                    castleCoordinate,
                    MapSiegeAction.Assault,
                    out _),
                Is.True);
            MapSiegeDayResult siegeResult = default;
            service.SiegeDayResolved += result => siegeResult = result;

            service.AdvanceEconomicDay(out _);

            Assert.That(service.LastSiegeDayResults.Count, Is.EqualTo(1));
            Assert.That(siegeResult.Action, Is.EqualTo(MapSiegeAction.Assault));
            Assert.That(siegeResult.WallDamage, Is.EqualTo(25));
            Assert.That(siegeResult.AttackerCasualties, Is.EqualTo(18));
            Assert.That(siegeResult.DefenderCasualties, Is.EqualTo(9));
            Assert.That(siegeResult.FoodConsumed, Is.EqualTo(5));
            Assert.That(siegeResult.CastleCaptured, Is.False);
            Assert.That(castle.WallDurability, Is.EqualTo(1075));
            Assert.That(castle.FoodSupply, Is.EqualTo(1495));
            Assert.That(attacker.Soldiers, Is.EqualTo(82));
            Assert.That(defender.Soldiers, Is.EqualTo(91));
            Assert.That(attacker.Formation.FrontlineSoldiers, Is.EqualTo(49));
            Assert.That(attacker.Formation.RangedSoldiers, Is.EqualTo(20));
            Assert.That(attacker.Formation.CavalrySoldiers, Is.EqualTo(13));
            Assert.That(defender.Formation.FrontlineSoldiers, Is.EqualTo(54));
            Assert.That(defender.Formation.RangedSoldiers, Is.EqualTo(22));
            Assert.That(defender.Formation.CavalrySoldiers, Is.EqualTo(15));
            Assert.That(attacker.Fatigue, Is.EqualTo(10m));
            Assert.That(defender.Morale, Is.EqualTo(85m));

            MapCastleCaptureRecord captureRecord = default;
            service.CastleCaptured += record => captureRecord = record;
            Assert.That(
                service.TryIssueMove(
                    "ai_1",
                    defender.Id,
                    new GridCoordinate(5, 1),
                    out _),
                Is.True);
            service.AdvanceFixedSteps(2);

            Assert.That(castle.OwnerFactionId, Is.EqualTo("player"));
            Assert.That(castle.GarrisonUnitIds, Does.Contain(attacker.Id));
            Assert.That(captureRecord.WasSiege, Is.True);
            Assert.That(castle.Role, Is.EqualTo(MapCastleRole.Unassigned));
            Assert.That(castle.SiegeAction, Is.EqualTo(MapSiegeAction.None));
            Assert.That(
                castle.OccupationPolicy,
                Is.EqualTo(MapOccupationPolicy.None));
        }

        [Test]
        public void RealtimeMapGameplay_DecisiveSiegeRetreatsAndPursuesDefenders()
        {
            var terrain = new GridTerrainKind[10 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var playerBase = new GridCoordinate(0, 1);
            var aiBase = new GridCoordinate(9, 1);
            var castleCoordinate = new GridCoordinate(8, 1);
            var layout = new GridMapLayout(
                10,
                3,
                59,
                playerBase,
                new[] { aiBase },
                new MinePlacement[0],
                false,
                terrain,
                new[] { castleCoordinate });
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000,
                    maxUnitsPerFaction: 8,
                    fixedStepsToCaptureCastle: 1,
                    fixedStepsToSiegeUndefendedCastle: 2));

            Assert.That(
                service.TryCreateUnit("ai_1", out MapUnitState defender, out _),
                Is.True);
            Assert.That(
                service.TryIssueCastleOccupation(
                    "ai_1",
                    defender.Id,
                    castleCoordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);

            var attackers = new MapUnitState[4];
            for (int i = 0; i < attackers.Length; i++)
            {
                Assert.That(
                    service.TryCreateUnit(
                        "player",
                        out attackers[i],
                        out _),
                    Is.True);
                bool ordered = i == 0
                    ? service.TryIssueCastleOccupation(
                        "player",
                        attackers[i].Id,
                        castleCoordinate,
                        out _)
                    : service.TryIssueMove(
                        "player",
                        attackers[i].Id,
                        castleCoordinate,
                        out _);
                Assert.That(ordered, Is.True);
            }
            service.AdvanceFixedSteps(10);

            MapCastleControlState castle = service.FindCastle(castleCoordinate);
            Assert.That(castle.IsUnderSiege, Is.True);
            for (int i = 0; i < attackers.Length; i++)
            {
                Assert.That(
                    attackers[i].Coordinate,
                    Is.EqualTo(castleCoordinate));
            }
            Assert.That(
                service.TrySetSiegeAction(
                    "player",
                    attackers[0].Id,
                    castleCoordinate,
                    MapSiegeAction.Assault,
                    out _),
                Is.True);

            service.AdvanceEconomicDay(out _);

            MapSiegeDayResult result = service.LastSiegeDayResults[0];
            Assert.That(result.DefenderRetreated, Is.True);
            Assert.That(result.DefenderCasualties, Is.GreaterThan(0));
            Assert.That(result.PursuitCasualties, Is.GreaterThan(0));
            Assert.That(result.CastleCaptured, Is.False);
            Assert.That(defender.Coordinate, Is.EqualTo(aiBase));
            Assert.That(
                defender.Soldiers,
                Is.EqualTo(
                    100 - result.DefenderCasualties -
                    result.PursuitCasualties));
            Assert.That(castle.GarrisonUnitCount, Is.EqualTo(0));
        }

        [Test]
        public void RealtimeMapGameplay_DestroyedCapitalEliminatesCampaignOpponent()
        {
            var terrain = new GridTerrainKind[6 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var playerBase = new GridCoordinate(0, 1);
            var enemyBase = new GridCoordinate(4, 1);
            var layout = new GridMapLayout(
                6,
                3,
                61,
                playerBase,
                new[] { enemyBase },
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000,
                    initialSoldiersPerUnit: 10000));

            MapCastleControlState enemyCapital = service.FindCapital("ai_1");
            Assert.That(enemyCapital, Is.Not.Null);
            Assert.That(enemyCapital.IsCapital, Is.True);
            Assert.That(enemyCapital.OwnerFactionId, Is.EqualTo("ai_1"));
            Assert.That(
                enemyCapital.MaxWallDurability,
                Is.EqualTo(MapCastleRules.CapitalMaxWallDurability));

            MapCastleControlState playerCapital = service.FindCapital("player");
            int soldiers = 10000;
            playerCapital.StoreWeaponStock(
                UnitWeaponType.Sword,
                EquipmentQuality.Standard,
                soldiers - playerCapital.GetWeaponStock(UnitWeaponType.Sword));
            playerCapital.StoreArmorStock(
                ArmorClass.Light,
                EquipmentQuality.Standard,
                soldiers - playerCapital.GetArmorStock(ArmorClass.Light));
            Assert.That(
                playerCapital.GetWeaponStock(UnitWeaponType.Sword),
                Is.EqualTo(soldiers));
            Assert.That(
                playerCapital.GetArmorStock(ArmorClass.Light),
                Is.EqualTo(soldiers));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState attacker, out _),
                Is.True);
            Assert.That(
                playerCapital.GetWeaponStock(UnitWeaponType.Sword),
                Is.Zero);
            Assert.That(
                playerCapital.GetArmorStock(ArmorClass.Light),
                Is.Zero);
            Assert.That(
                service.TryIssueCastleOccupation(
                    "player",
                    attacker.Id,
                    enemyBase,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(8);
            Assert.That(attacker.Coordinate, Is.EqualTo(enemyBase));
            Assert.That(enemyCapital.IsUnderSiege, Is.True);
            Assert.That(
                service.TrySetSiegeAction(
                    "player",
                    attacker.Id,
                    enemyBase,
                    MapSiegeAction.Assault,
                    out _),
                Is.True);

            MapCapitalDestroyedRecord destruction = default;
            service.CapitalDestroyed += record => destruction = record;
            for (int day = 0; day < 3 && !enemyCapital.IsDestroyed; day++)
                service.AdvanceEconomicDay(out _);

            Assert.That(enemyCapital.IsDestroyed, Is.True);
            Assert.That(enemyCapital.OwnerFactionId, Is.Empty);
            Assert.That(destruction.DestroyedFactionId, Is.EqualTo("ai_1"));
            Assert.That(destruction.AttackingFactionId, Is.EqualTo("player"));
            Assert.That(service.LastSiegeDayResults.Count, Is.EqualTo(1));
            Assert.That(
                service.LastSiegeDayResults[0].CapitalDestroyed,
                Is.True);
            Assert.That(
                service.LastSiegeDayResults[0].CastleCaptured,
                Is.False);

            var player = new CampaignParticipantState(
                new Company(new CompanyId("player"), "플레이어", 500000m),
                true);
            var opponent = new CampaignParticipantState(
                new Company(new CompanyId("ai_1"), "경쟁 기업 1", 500000m),
                false);
            var campaign = new CampaignState(
                new[] { player, opponent });
            Assert.That(
                new CampaignCapitalDestructionService().Apply(
                    campaign,
                    destruction),
                Is.True);
            Assert.That(opponent.IsCapitalStanding, Is.False);

            CampaignTurnResult result = new CampaignVictoryEvaluator(
                new CampaignRuleSet()).Evaluate(new TurnNumber(1), campaign);
            Assert.That(result.Outcome, Is.EqualTo(CampaignOutcome.Victory));
            Assert.That(
                result.EndReason,
                Is.EqualTo(CampaignEndReason.LastCompanyStanding));
        }

        [Test]
        public void RealtimeMapGameplay_RecruitmentPoolsRecoverByEconomicDay()
        {
            var terrain = new GridTerrainKind[4 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var headquarters = new GridCoordinate(0, 0);
            var layout = new GridMapLayout(
                4,
                2,
                53,
                headquarters,
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    aiDecisionIntervalSteps: 1000,
                    maxUnitsPerFaction: 12));

            Assert.That(
                service.TryGetRecruitmentSiteSnapshot(
                    "player",
                    headquarters,
                    out MapRecruitmentSiteSnapshot initial),
                Is.True);
            Assert.That(initial.GarrisonCapacity, Is.EqualTo(6));
            Assert.That(initial.AvailableRecruits, Is.EqualTo(4));

            for (int i = 0; i < 4; i++)
            {
                Assert.That(
                    service.TryCreateUnit("player", out _, out _),
                    Is.True);
            }
            Assert.That(
                service.CanCreateUnit("player", out string emptyPoolReason),
                Is.False);
            Assert.That(emptyPoolReason, Does.Contain("징집 인력"));

            service.AdvanceEconomicDay(out _);
            Assert.That(service.TryCreateUnit("player", out _, out _), Is.True);
            Assert.That(
                service.TryGetRecruitmentSiteSnapshot(
                    "player",
                    headquarters,
                    out MapRecruitmentSiteSnapshot recovered),
                Is.True);
            Assert.That(recovered.GarrisonUnitCount, Is.EqualTo(5));
            Assert.That(recovered.AvailableRecruits, Is.EqualTo(0));
        }

        [Test]
        public void RealtimeMapGameplay_MineUsesOneGuardAndNeverRecruits()
        {
            var terrain = new GridTerrainKind[4 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var mineCoordinate = new GridCoordinate(1, 0);
            var layout = new GridMapLayout(
                4,
                2,
                59,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new[] { new MinePlacement(mineCoordinate, MineKind.Normal) },
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCapture: 1,
                    aiDecisionIntervalSteps: 1000));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState guard, out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    guard.Id,
                    mineCoordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);

            MapMineControlState mine = service.FindMine(mineCoordinate);
            Assert.That(mine.OwnerFactionId, Is.EqualTo("player"));
            Assert.That(mine.GuardUnitId, Is.EqualTo(guard.Id));
            Assert.That(
                service.CanCreateUnitAt(
                    "player",
                    mineCoordinate,
                    out string recruitmentReason),
                Is.False);
            Assert.That(recruitmentReason, Does.Contain("광산"));

            Assert.That(
                service.TryCreateUnit(
                    "player",
                    out MapUnitState secondUnit,
                    out _),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    secondUnit.Id,
                    mineCoordinate,
                    out string guardReason),
                Is.False);
            Assert.That(guardReason, Does.Contain("경비 부대 1개"));
        }

        [Test]
        public void MineProduction_AutomaticallyDepositsIntoHeadquartersWarehouse()
        {
            var iron = new ResourceDefinition(
                new ResourceId("iron"),
                "철광석",
                100m,
                ResourceRarity.Common,
                1m,
                false);
            var catalog = new ResourceCatalog();
            catalog.Register(iron);

            CampaignState campaign = CreateCampaign(1000m, 1000m);
            var warehouse = new Warehouse(
                new WarehouseId("player_headquarters"),
                campaign.Player.Company.Id,
                new RegionId("starter"),
                130m);
            Assert.That(warehouse.TryAdd(iron.Id, 100m), Is.True);

            var world = new WorldEconomyState();
            world.RegisterCompany(new CompanyEconomyRuntime(
                campaign.Player,
                warehouse,
                0,
                0m,
                0m));
            var depositService = new MapMineProductionDepositService(
                world,
                catalog);

            MapMineProductionDepositReport report = depositService.Deposit(
                new[]
                {
                    new MapMineProductionRecord(
                        "player",
                        normalMineCount: 1,
                        goldMineCount: 1,
                        ironAmount: 50m,
                        cashAmount: 250m)
                });

            Assert.That(report.StoredIronAmount, Is.EqualTo(30m));
            Assert.That(report.RejectedIronAmount, Is.EqualTo(20m));
            Assert.That(report.CreditedCashAmount, Is.EqualTo(250m));
            Assert.That(warehouse.GetAvailable(iron.Id), Is.EqualTo(130m));
            Assert.That(campaign.Player.Company.Cash, Is.EqualTo(1250m));

            HeadquartersInventorySnapshot inventory =
                new HeadquartersInventoryQuery(catalog).Execute(warehouse);
            Assert.That(inventory.UsedCapacity, Is.EqualTo(130m));
            Assert.That(inventory.AvailableCapacity, Is.EqualTo(0m));
            Assert.That(inventory.Items.Count, Is.EqualTo(1));
            Assert.That(inventory.Items[0].DisplayName, Is.EqualTo("철광석"));
            Assert.That(inventory.Items[0].OnHand, Is.EqualTo(130m));
        }

        [Test]
        public void MineProduction_UsesNearestFriendlyCastleWarehouseRoute()
        {
            var terrain = new GridTerrainKind[7 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var mineCoordinate = new GridCoordinate(4, 0);
            var castleCoordinate = new GridCoordinate(3, 0);
            var layout = new GridMapLayout(
                7,
                2,
                71,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new[] { new MinePlacement(mineCoordinate, MineKind.Normal) },
                false,
                terrain,
                new[] { castleCoordinate });
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCapture: 1,
                    fixedStepsToCaptureCastle: 1,
                    fixedStepsToSiegeUndefendedCastle: 1,
                    aiDecisionIntervalSteps: 1000,
                    normalMineIronPerDay: 25m,
                    mineDailyDepletionRate: 0m));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(
                service.TryIssueCastleOccupation(
                    "player",
                    unit.Id,
                    castleCoordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(4);
            Assert.That(
                service.FindCastle(castleCoordinate).OwnerFactionId,
                Is.EqualTo("player"));
            Assert.That(
                service.TrySetCastleRole(
                    "player",
                    castleCoordinate,
                    MapCastleRole.IndustrialCity,
                    out _),
                Is.True);

            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    mineCoordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(1);
            Assert.That(
                service.FindMine(mineCoordinate).OwnerFactionId,
                Is.EqualTo("player"));

            IReadOnlyList<MapMineProductionRecord> production =
                service.CreateDailyProduction();

            Assert.That(production.Count, Is.EqualTo(1));
            Assert.That(production[0].IronAmount, Is.EqualTo(31.25m));
            Assert.That(production[0].Transports.Count, Is.EqualTo(1));
            MapMineTransportRecord transport = production[0].Transports[0];
            Assert.That(transport.MineCoordinate, Is.EqualTo(mineCoordinate));
            Assert.That(
                transport.WarehouseCoordinate,
                Is.EqualTo(castleCoordinate));
            Assert.That(transport.Distance, Is.EqualTo(1));
            Assert.That(transport.Route[0], Is.EqualTo(castleCoordinate));
            Assert.That(transport.IronAmount, Is.EqualTo(31.25m));
            Assert.That(
                service.FindCastle(castleCoordinate).WarehouseIronAmount,
                Is.EqualTo(31.25m));
            Assert.That(
                service.FindCapital("player").WarehouseIronAmount,
                Is.EqualTo(0m));
        }

        [Test]
        public void SupplyLogistics_StocksCapitalThenRoutesThroughForwardDepot()
        {
            var terrain = new GridTerrainKind[6 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var depotCoordinate = new GridCoordinate(3, 0);
            var layout = new GridMapLayout(
                6,
                2,
                73,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain,
                new[] { depotCoordinate });
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCaptureCastle: 1,
                    fixedStepsToSiegeUndefendedCastle: 1,
                    aiDecisionIntervalSteps: 1000));

            Assert.That(
                gameplay.TryCreateUnit(
                    "player",
                    out MapUnitState unit,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryIssueCastleOccupation(
                    "player",
                    unit.Id,
                    depotCoordinate,
                    out _),
                Is.True);
            gameplay.AdvanceFixedSteps(4);
            Assert.That(
                gameplay.TrySetCastleRole(
                    "player",
                    depotCoordinate,
                    MapCastleRole.SupplyHub,
                    out _),
                Is.True);

            CampaignState campaign = CreateCampaign(1000m, 1000m);
            var warehouse = new Warehouse(
                new WarehouseId("player_supply_warehouse"),
                campaign.Player.Company.Id,
                new RegionId("starter"),
                1000m);
            var food = new ResourceId("food");
            var steel = new ResourceId("steel");
            var medicine = new ResourceId("medicine");
            Assert.That(warehouse.TryAdd(food, 100m), Is.True);
            Assert.That(warehouse.TryAdd(steel, 10m), Is.True);
            Assert.That(warehouse.TryAdd(medicine, 5m), Is.True);
            var world = new WorldEconomyState();
            world.RegisterCompany(new CompanyEconomyRuntime(
                campaign.Player,
                warehouse,
                0,
                0m,
                0m,
                vehicleCount: 4));

            MapCapitalSupplyStockReport stockReport =
                new MapSupplyStockingService(world)
                    .StockFactionCapitals(gameplay);
            IReadOnlyList<MapSupplyTransportRecord> transports =
                gameplay.CreateDailySupplyTransports();
            decimal transportCost = new MapSupplyStockingService(world)
                .SettleTransportCosts(transports);

            Assert.That(stockReport.FoodAmount, Is.EqualTo(100m));
            Assert.That(stockReport.EquipmentAmount, Is.EqualTo(10m));
            Assert.That(stockReport.MedicineAmount, Is.EqualTo(5m));
            Assert.That(warehouse.GetAvailable(food), Is.EqualTo(0m));
            Assert.That(warehouse.GetAvailable(steel), Is.EqualTo(0m));
            Assert.That(warehouse.GetAvailable(medicine), Is.EqualTo(0m));
            Assert.That(transports.Count, Is.EqualTo(3));
            Assert.That(
                transports[0].DestinationKind,
                Is.EqualTo(MapSupplyDestinationKind.ForwardDepot));
            Assert.That(transports[0].Distance, Is.EqualTo(3));
            Assert.That(transports[0].RoadTileCount, Is.EqualTo(3));
            Assert.That(transports[0].TerrainTravelWeight, Is.EqualTo(1.8m));
            Assert.That(transports[0].TravelDays, Is.EqualTo(1));
            Assert.That(transports[0].ArrivalEconomicDay, Is.EqualTo(1));
            Assert.That(transportCost, Is.GreaterThan(0m));
            Assert.That(campaign.Player.Company.Cash, Is.LessThan(1000m));
            Assert.That(gameplay.PendingSupplyTransportCount, Is.EqualTo(3));
            Assert.That(unit.SupplyRatio, Is.EqualTo(0m));

            gameplay.AdvanceEconomicDay(out _);
            Assert.That(gameplay.PendingSupplyTransportCount, Is.EqualTo(0));

            transports = gameplay.CreateDailySupplyTransports();
            Assert.That(transports.Count, Is.EqualTo(3));
            Assert.That(
                transports[0].DestinationKind,
                Is.EqualTo(MapSupplyDestinationKind.Unit));
            Assert.That(transports[0].DestinationUnitId, Is.EqualTo(unit.Id));
            Assert.That(transports[0].Distance, Is.EqualTo(0));
            Assert.That(transports[0].TravelDays, Is.EqualTo(0));
            Assert.That(transports[0].Cost, Is.EqualTo(3.57m));
            Assert.That(unit.FoodSupply, Is.EqualTo(21m));
            Assert.That(unit.EquipmentSupply, Is.EqualTo(2.8m));
            Assert.That(unit.MedicineSupply, Is.EqualTo(0.7m));
            Assert.That(unit.SupplyRatio, Is.EqualTo(1m));
            MapCastleControlState depot = gameplay.FindCastle(
                depotCoordinate);
            Assert.That(depot.WarehouseFoodAmount, Is.EqualTo(79m));
            Assert.That(depot.WarehouseEquipmentAmount, Is.EqualTo(7.2m));
            Assert.That(depot.WarehouseMedicineAmount, Is.EqualTo(4.3m));
            MapCastleControlState capital = gameplay.FindCapital("player");
            Assert.That(capital.WarehouseFoodAmount, Is.EqualTo(0m));
            Assert.That(capital.WarehouseEquipmentAmount, Is.EqualTo(0m));
            Assert.That(capital.WarehouseMedicineAmount, Is.EqualTo(0m));
            Assert.That(
                MapCastleRules.GetTransportSpeedMultiplier(
                    MapCastleRole.SupplyHub),
                Is.EqualTo(1.20m));
            Assert.That(
                MapCastleRules.GetTransportSpeedMultiplier(MapCastleRole.Port),
                Is.EqualTo(1.35m));
            Assert.That(
                MapCastleRules.GetTransportCostMultiplier(MapCastleRole.Port),
                Is.EqualTo(0.70m));
        }

        [Test]
        public void SupplyLogistics_RaidBlockadeAndEscortResolveOnRoute()
        {
            var terrain = new GridTerrainKind[6 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;

            var depotCoordinate = new GridCoordinate(3, 0);
            var layout = new GridMapLayout(
                6,
                2,
                79,
                new GridCoordinate(0, 0),
                new[] { new GridCoordinate(5, 1) },
                new MinePlacement[0],
                false,
                terrain,
                new[] { depotCoordinate });
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "opponent_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCaptureCastle: 1,
                    fixedStepsToSiegeUndefendedCastle: 1,
                    aiDecisionIntervalSteps: 1000));

            Assert.That(
                gameplay.TryCreateUnit(
                    "player",
                    out MapUnitState escort,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryCreateUnit(
                    "opponent_1",
                    out MapUnitState raider,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryCreateUnit(
                    "opponent_1",
                    UnitArchetype.Swordsman,
                    out MapUnitState blocker,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryIssueCastleOccupation(
                    "player",
                    escort.Id,
                    depotCoordinate,
                    out _),
                Is.True);
            gameplay.AdvanceFixedSteps(4);
            Assert.That(
                gameplay.TrySetCastleRole(
                    "player",
                    depotCoordinate,
                    MapCastleRole.SupplyHub,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryStockFactionCapitalWarehouse(
                    "player",
                    MapSupplyKind.Food,
                    100m,
                    out _),
                Is.True);
            gameplay.ConfigureFactionLogistics("player", 0);
            Assert.That(
                gameplay.TryProvisionUnitSupply(
                    "player",
                    escort.Id,
                    MapSupplyKind.Equipment,
                    escort.EquipmentSupplyCapacity,
                    out _),
                Is.True);
            IReadOnlyList<MapSupplyTransportRecord> transports =
                gameplay.CreateDailySupplyTransports();
            Assert.That(transports.Count, Is.EqualTo(1));
            Assert.That(transports[0].TravelDays, Is.EqualTo(4));

            var firstRouteTile = new GridCoordinate(1, 0);
            Assert.That(
                gameplay.TryAssignSupplyMission(
                    "player",
                    escort.Id,
                    firstRouteTile,
                    MapSupplyMissionKind.Escort,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryAssignSupplyMission(
                    "opponent_1",
                    raider.Id,
                    firstRouteTile,
                    MapSupplyMissionKind.Raid,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryAssignSupplyMission(
                    "opponent_1",
                    blocker.Id,
                    firstRouteTile,
                    MapSupplyMissionKind.Blockade,
                    out _),
                Is.True);
            gameplay.AdvanceFixedSteps(8);
            gameplay.AdvanceEconomicDay(out _);

            Assert.That(
                gameplay.LastSupplyInterdictionResults.Count,
                Is.EqualTo(1));
            MapSupplyInterdictionResult escortedResult =
                gameplay.LastSupplyInterdictionResults[0];
            Assert.That(escortedResult.WasRaided, Is.True);
            Assert.That(escortedResult.WasEscorted, Is.True);
            Assert.That(escortedResult.WasBlockaded, Is.False);
            Assert.That(escortedResult.CargoLost, Is.GreaterThan(0m));
            Assert.That(escortedResult.CargoLost, Is.LessThan(45m));

            var secondRouteTile = new GridCoordinate(2, 0);
            Assert.That(
                gameplay.TryAssignSupplyMission(
                    "player",
                    escort.Id,
                    firstRouteTile,
                    MapSupplyMissionKind.None,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryAssignSupplyMission(
                    "opponent_1",
                    raider.Id,
                    secondRouteTile,
                    MapSupplyMissionKind.Raid,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryAssignSupplyMission(
                    "opponent_1",
                    blocker.Id,
                    secondRouteTile,
                    MapSupplyMissionKind.Blockade,
                    out _),
                Is.True);
            gameplay.AdvanceFixedSteps(1);
            gameplay.AdvanceEconomicDay(out _);

            MapSupplyInterdictionResult unescortedResult =
                gameplay.LastSupplyInterdictionResults[0];
            Assert.That(unescortedResult.WasEscorted, Is.False);
            Assert.That(unescortedResult.WasBlockaded, Is.True);
            Assert.That(unescortedResult.DelayDays, Is.EqualTo(1));
            decimal expectedUnescortedLoss = Math.Round(
                escortedResult.CargoRemaining * 0.45m,
                2,
                MidpointRounding.AwayFromZero);
            decimal expectedDelivered =
                escortedResult.CargoRemaining - expectedUnescortedLoss;
            Assert.That(
                unescortedResult.CargoLost,
                Is.EqualTo(expectedUnescortedLoss));
            Assert.That(
                unescortedResult.CargoRemaining,
                Is.EqualTo(expectedDelivered));

            gameplay.AdvanceEconomicDay(out _);
            gameplay.AdvanceEconomicDay(out _);
            gameplay.AdvanceEconomicDay(out _);
            Assert.That(gameplay.PendingSupplyTransportCount, Is.EqualTo(0));
            Assert.That(
                gameplay.FindCastle(depotCoordinate).WarehouseFoodAmount,
                Is.EqualTo(expectedDelivered));
        }

        [Test]
        public void UnitSupplyShortagesReduceMoraleMovementAttackAndRecovery()
        {
            var terrain = new GridTerrainKind[8 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                8,
                2,
                83,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var gameplay = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 2,
                    aiDecisionIntervalSteps: 1000,
                    movementFatiguePerTile: 2m,
                    dailyFatigueRecovery: 10m));
            Assert.That(
                gameplay.TryCreateUnit(
                    "player",
                    out MapUnitState unit,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryProvisionUnitSupply(
                    "player",
                    unit.Id,
                    MapSupplyKind.Food,
                    unit.FoodSupplyCapacity,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryProvisionUnitSupply(
                    "player",
                    unit.Id,
                    MapSupplyKind.Equipment,
                    unit.EquipmentSupplyCapacity,
                    out _),
                Is.True);
            Assert.That(
                gameplay.TryProvisionUnitSupply(
                    "player",
                    unit.Id,
                    MapSupplyKind.Medicine,
                    unit.MedicineSupplyCapacity,
                    out _),
                Is.True);

            decimal suppliedAttack = unit.AttackPower;
            int suppliedMovementSteps =
                gameplay.GetRequiredMovementStepsPerTile(unit);
            for (int day = 0; day < 7; day++)
                gameplay.AdvanceEconomicDay(out _);

            Assert.That(unit.FoodSupply, Is.EqualTo(0m));
            Assert.That(unit.EquipmentSupply, Is.EqualTo(0m));
            Assert.That(unit.MedicineSupply, Is.EqualTo(0m));
            Assert.That(unit.AttackPower, Is.LessThan(suppliedAttack));
            Assert.That(
                gameplay.GetRequiredMovementStepsPerTile(unit),
                Is.GreaterThan(suppliedMovementSteps));
            Assert.That(unit.Morale, Is.EqualTo(100m));

            Assert.That(
                gameplay.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(5, 0),
                    out _),
                Is.True);
            gameplay.AdvanceFixedSteps(
                gameplay.GetRequiredMovementStepsPerTile(unit) * 5);
            Assert.That(unit.Fatigue, Is.EqualTo(10m));
            gameplay.AdvanceEconomicDay(out _);

            Assert.That(unit.Morale, Is.EqualTo(92m));
            Assert.That(unit.Fatigue, Is.GreaterThan(10m));
            Assert.That(unit.RecoverySupplyModifier, Is.EqualTo(0.25m));
        }

        [Test]
        public void RealtimeMapGameplay_UsesAndRegeneratesUnitStamina()
        {
            var terrain = new GridTerrainKind[3 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                3,
                2,
                19,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 100,
                    maxUnitStamina: 2,
                    moveStaminaCost: 1,
                    staminaRecoveryIntervalSteps: 2));

            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);
            Assert.That(unit.Archetype, Is.EqualTo(UnitArchetype.Swordsman));
            Assert.That(unit.ArchetypeDisplayName, Is.EqualTo("검병"));
            Assert.That(unit.Stamina, Is.EqualTo(2));
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    new GridCoordinate(1, 0),
                    out _),
                Is.True);
            Assert.That(unit.Stamina, Is.EqualTo(1));

            service.AdvanceFixedSteps(1);
            Assert.That(unit.Stamina, Is.EqualTo(1));
            service.AdvanceFixedSteps(1);
            Assert.That(unit.Stamina, Is.EqualTo(2));

            Assert.That(
                service.TryCreateUnit(
                    "player",
                    UnitArchetype.Cavalry,
                    out MapUnitState cavalry,
                    out _),
                Is.True);
            Assert.That(cavalry.ArchetypeDisplayName, Is.EqualTo("기마병"));
        }

        [Test]
        public void RealtimeMapGameplay_CreatesAndChangesConfiguredEquipment()
        {
            var terrain = new GridTerrainKind[3 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                3,
                2,
                29,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player");

            Assert.That(
                service.TryCreateUnit(
                    "player",
                    UnitArchetype.Cavalry,
                    UnitWeaponType.Lance,
                    ArmorClass.Heavy,
                    out MapUnitState unit,
                    out _),
                Is.True);
            Assert.That(unit.WeaponType, Is.EqualTo(UnitWeaponType.Lance));
            Assert.That(unit.ArmorClass, Is.EqualTo(ArmorClass.Heavy));
            Assert.That(unit.AttackModifier, Is.EqualTo(1.24m));
            Assert.That(unit.DefenseModifier, Is.EqualTo(1.58m));
            Assert.That(unit.MobilityModifier, Is.EqualTo(0.968760m));

            Assert.That(
                service.TryChangeEquipment(
                    "player",
                    unit.Id,
                    UnitWeaponType.Bow,
                    ArmorClass.Light,
                    out _),
                Is.True);
            Assert.That(unit.WeaponDisplayName, Is.EqualTo("장궁"));
            Assert.That(unit.ArmorDisplayName, Is.EqualTo("경갑"));
        }

        [Test]
        public void RealtimeMapGameplay_AiUsesSameMoveAndCaptureRules()
        {
            var terrain = new GridTerrainKind[6 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Plains;
            var mineCoordinate = new GridCoordinate(4, 1);
            var layout = new GridMapLayout(
                6,
                3,
                11,
                new GridCoordinate(0, 1),
                new[] { new GridCoordinate(5, 1) },
                new[] { new MinePlacement(mineCoordinate, MineKind.Gold) },
                true,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                new[] { "ai_1" },
                new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    fixedStepsToCapture: 2,
                    aiDecisionIntervalSteps: 1));

            service.AdvanceFixedSteps(2);

            Assert.That(service.Units.Count, Is.EqualTo(1));
            Assert.That(service.Units[0].OwnerFactionId, Is.EqualTo("ai_1"));
            Assert.That(service.Units[0].Coordinate, Is.EqualTo(mineCoordinate));
            Assert.That(
                service.FindMine(mineCoordinate).OwnerFactionId,
                Is.EqualTo("ai_1"));
            Assert.That(
                service.CreateDailyProduction()[0].CashAmount,
                Is.EqualTo(1500m));
        }

        [Test]
        public void EconomicSurvey_IsDeterministicForTheSameMapAndCoordinate()
        {
            var terrain = new GridTerrainKind[6 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Hills;
            var layout = new GridMapLayout(
                6,
                3,
                90210,
                new GridCoordinate(0, 1),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var coordinate = new GridCoordinate(3, 1);

            MapEconomicSurveyState first =
                MapEconomicDevelopmentRules.Evaluate(layout, coordinate, 1);
            MapEconomicSurveyState second =
                MapEconomicDevelopmentRules.Evaluate(layout, coordinate, 99);

            Assert.That(second.HasViableDeposit,
                Is.EqualTo(first.HasViableDeposit));
            Assert.That(second.DepositKind, Is.EqualTo(first.DepositKind));
            Assert.That(second.YieldMultiplier,
                Is.EqualTo(first.YieldMultiplier));
            Assert.That(second.SurveyedEconomicDay, Is.EqualTo(99));
        }

        [Test]
        public void EconomicSurveyAndConstruction_CreateOwnedProducingMine()
        {
            var terrain = new GridTerrainKind[10 * 2];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Hills;
            var layout = new GridMapLayout(
                10,
                2,
                417,
                new GridCoordinate(0, 0),
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain);
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    mineSpawnIntervalDays: 1000,
                    aiDecisionIntervalSteps: 1000),
                enableAi: false);
            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);

            GridCoordinate? target = null;
            for (int x = 1; x < layout.Width && !target.HasValue; x++)
            {
                for (int y = 0; y < layout.Height; y++)
                {
                    var candidate = new GridCoordinate(x, y);
                    if (MapEconomicDevelopmentRules
                        .Evaluate(layout, candidate, 0)
                        .HasViableDeposit)
                    {
                        target = candidate;
                        break;
                    }
                }
            }
            Assert.That(target.HasValue, Is.True);
            GridCoordinate coordinate = target.Value;
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    coordinate,
                    out _),
                Is.True);
            service.AdvanceFixedSteps(30);
            Assert.That(unit.Coordinate, Is.EqualTo(coordinate));

            int staminaBeforeSurvey = unit.Stamina;
            Assert.That(
                service.TrySurveyEconomicSite(
                    "player",
                    unit.Id,
                    coordinate,
                    out MapEconomicSurveyState survey,
                    out _),
                Is.True);
            Assert.That(survey.HasViableDeposit, Is.True);
            Assert.That(
                unit.Stamina,
                Is.EqualTo(staminaBeforeSurvey -
                    MapEconomicDevelopmentRules.SurveyStaminaCost));
            Assert.That(
                service.TrySurveyEconomicSite(
                    "player",
                    unit.Id,
                    coordinate,
                    out _,
                    out string duplicateReason),
                Is.False);
            Assert.That(duplicateReason, Does.Contain("이미"));

            Assert.That(
                service.TryStartMineConstruction(
                    "player",
                    unit.Id,
                    coordinate,
                    out MapMineConstructionState construction,
                    out _),
                Is.True);
            Assert.That(
                construction.Cost,
                Is.EqualTo(MapEconomicDevelopmentRules
                    .GetConstructionCost(construction.Kind)));
            for (int day = 1; day < construction.TotalDays; day++)
            {
                Assert.That(service.AdvanceEconomicDay(out _), Is.False);
                Assert.That(service.FindMine(coordinate), Is.Null);
            }

            Assert.That(
                service.AdvanceEconomicDay(out MapMineSpawnRecord completed),
                Is.True);
            Assert.That(completed.WasConstructed, Is.True);
            Assert.That(completed.OwnerFactionId, Is.EqualTo("player"));
            MapMineControlState mine = service.FindMine(coordinate);
            Assert.That(mine, Is.Not.Null);
            Assert.That(mine.OwnerFactionId, Is.EqualTo("player"));
            Assert.That(mine.YieldMultiplier,
                Is.EqualTo(survey.YieldMultiplier));

            IReadOnlyList<MapMineProductionRecord> production =
                service.CreateDailyProduction();
            Assert.That(production.Count, Is.EqualTo(1));
            Assert.That(production[0].OwnerFactionId, Is.EqualTo("player"));
            Assert.That(
                production[0].IronAmount + production[0].CashAmount,
                Is.GreaterThan(0m));
            Assert.That(production[0].Transports.Count, Is.EqualTo(1));
            Assert.That(
                production[0].Transports[0].MineCoordinate,
                Is.EqualTo(coordinate));
            Assert.That(
                production[0].Transports[0].WarehouseCoordinate,
                Is.EqualTo(layout.PlayerStart));
        }

        [Test]
        public void FriendlyCoastalCastles_ProvideAutomaticSeaTransport()
        {
            var terrain = new GridTerrainKind[7 * 3];
            for (int i = 0; i < terrain.Length; i++)
                terrain[i] = GridTerrainKind.Ocean;
            var origin = new GridCoordinate(0, 1);
            var destination = new GridCoordinate(6, 1);
            terrain[origin.Y * 7 + origin.X] = GridTerrainKind.Plains;
            terrain[destination.Y * 7 + destination.X] =
                GridTerrainKind.Plains;
            var layout = new GridMapLayout(
                7,
                3,
                301,
                origin,
                new GridCoordinate[0],
                new MinePlacement[0],
                false,
                terrain,
                new[] { destination });
            var service = new RealtimeMapGameplayService(
                layout,
                "player",
                tuning: new MapGameplayTuning(
                    fixedStepsPerMove: 1,
                    aiDecisionIntervalSteps: 1000),
                enableAi: false);
            Assert.That(
                service.TryRestoreAuthoritativeCastleState(
                    destination,
                    "player",
                    string.Empty,
                    0,
                    MapCastleRole.Port,
                    MapCastleConflictKind.None,
                    MapSiegeAction.None,
                    MapOccupationPolicy.Preserve,
                    false,
                    100,
                    100,
                    out _),
                Is.True);
            Assert.That(
                service.TryCreateUnit("player", out MapUnitState unit, out _),
                Is.True);

            Assert.That(service.IsCoastalPort(origin), Is.True);
            Assert.That(service.IsCoastalPort(destination), Is.True);
            Assert.That(
                service.WillUseSeaTransport(
                    "player",
                    unit.Id,
                    destination),
                Is.True);
            Assert.That(
                service.TryIssueMove(
                    "player",
                    unit.Id,
                    destination,
                    out _),
                Is.True);
            Assert.That(service.IsUsingSeaTransport(unit), Is.True);
            Assert.That(
                service.CanCancelMove("player", unit.Id, out string reason),
                Is.False);
            Assert.That(reason, Does.Contain("자동 하선"));

            service.AdvanceFixedSteps(20);

            Assert.That(unit.Coordinate, Is.EqualTo(destination));
            Assert.That(unit.IsMoving, Is.False);
            Assert.That(service.IsUsingSeaTransport(unit), Is.False);
        }

        [Test]
        public void HivePlatformExtensionSlot_DefaultsToInactiveNoSdkBoundary()
        {
            HivePlatformExtensionSlot.ResetToDisabled();

            Assert.That(HivePlatformExtensionSlot.HasActiveExtension, Is.False);
            Assert.That(
                HivePlatformExtensionSlot.Current.Capabilities,
                Is.EqualTo(HivePlatformCapability.None));
            HivePlatformResult result = HivePlatformExtensionSlot.Current
                .InitializeAsync(default)
                .GetAwaiter()
                .GetResult();
            Assert.That(result.IsAvailable, Is.False);
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Message, Does.Contain("비활성"));
        }

        [Test]
        public void GameModeSelection_AllowsOneModeUntilCleared()
        {
            var selection = new GameModeSelection();

            Assert.That(selection.HasSelection, Is.False);
            Assert.That(
                selection.TrySelect(GamePlayMode.SinglePlayer, out _),
                Is.True);
            Assert.That(selection.IsSinglePlayer, Is.True);
            Assert.That(
                selection.TrySelect(GamePlayMode.Multiplayer, out string reason),
                Is.False);
            Assert.That(reason, Is.Not.Empty);

            selection.Clear();

            Assert.That(
                selection.TrySelect(GamePlayMode.Multiplayer, out _),
                Is.True);
            Assert.That(selection.IsMultiplayer, Is.True);
        }

        [Test]
        public void GameModeSelection_RejectsNoneAsPlayableMode()
        {
            var selection = new GameModeSelection();

            bool selected = selection.TrySelect(
                GamePlayMode.None,
                out string reason);

            Assert.That(selected, Is.False);
            Assert.That(selection.HasSelection, Is.False);
            Assert.That(reason, Is.Not.Empty);
        }

        [Test]
        public void OrderBook_MatchesByPriceAndSupportsPartialFill()
        {
            var region = new RegionId("test");
            var resource = new ResourceId("iron");
            var book = new OrderBook();

            book.Add(new MarketOrder(
                "buy",
                new CompanyId("buyer"),
                region,
                resource,
                OrderSide.Buy,
                OrderPurpose.ProductionInput,
                10,
                120,
                1));

            book.Add(new MarketOrder(
                "sell",
                new CompanyId("seller"),
                region,
                resource,
                OrderSide.Sell,
                OrderPurpose.Export,
                4,
                100,
                1));

            var fills = new List<TradeFill>();
            book.Match(fills);

            Assert.That(fills.Count, Is.EqualTo(1));
            Assert.That(fills[0].Quantity, Is.EqualTo(4));
            Assert.That(fills[0].UnitPrice, Is.EqualTo(110));
        }

        [Test]
        public void PriceCalculator_LowersPriceWhenSupplyExceedsDemand()
        {
            var definition = new ResourceDefinition(
                new ResourceId("iron"),
                "철광석",
                100,
                ResourceRarity.Common,
                1,
                false);

            var state = new ResourceMarketState(
                definition.Id,
                100,
                100);

            var input = new PriceInput
            {
                PreviousPrice = 100,
                BasePrice = 100,
                EffectiveSupply = 200,
                EffectiveDemand = 100,
                EndingStock = 120,
                TargetStock = 100,
                RecentAverageVolume = 100,
                NetMarketAbsorption = 0
            };

            decimal result =
                new PriceCalculator().Calculate(
                    definition,
                    state,
                    input);

            Assert.That(result, Is.LessThan(100));
        }

        [Test]
        public void PriceCalculator_RespectsExactDailyChangeLimit()
        {
            var definition = new ResourceDefinition(
                new ResourceId("iron"),
                "철광석",
                100,
                ResourceRarity.Common,
                1,
                false);

            var state = new ResourceMarketState(
                definition.Id,
                100,
                0);

            var input = new PriceInput
            {
                PreviousPrice = 100,
                BasePrice = 100,
                EffectiveSupply = 0,
                EffectiveDemand = 100,
                EndingStock = 0,
                TargetStock = 1400,
                RecentAverageVolume = 100,
                NetMarketAbsorption = 100,
                MaxDailyChange = 0.15m
            };

            decimal result =
                new PriceCalculator().Calculate(
                    definition,
                    state,
                    input);

            Assert.That(result, Is.EqualTo(115));
        }

        [Test]
        public void Logistics_DeliversAfterTravelDays()
        {
            var route = new TradeRoute(
                "route",
                new RegionId("a"),
                new RegionId("b"),
                2,
                100,
                0.1m,
                2);

            var shipment = new Shipment(
                "shipment",
                new CompanyId("company"),
                new ResourceId("steel"),
                route,
                50);

            var service = new LogisticsService();
            var arrivals = new List<ShipmentArrival>();

            Assert.That(service.TryDispatch(route, shipment), Is.True);

            service.AdvanceDay(0, arrivals);
            Assert.That(arrivals.Count, Is.EqualTo(0));

            service.AdvanceDay(0, arrivals);
            Assert.That(arrivals.Count, Is.EqualTo(1));
            Assert.That(arrivals[0].Quantity, Is.EqualTo(45));
        }

        [Test]
        public void Technology_CompletesAtResearchCost()
        {
            var definition = new TechnologyDefinition(
                "advanced_steelmaking",
                "고급 제철 공정",
                100,
                System.Array.Empty<string>(),
                new[]
                {
                    new TechnologyEffect(
                        TechnologyEffectType.ProductionEfficiency,
                        0.1m)
                });

            var state = new TechnologyState();

            Assert.That(state.AddResearch(definition, 60), Is.False);
            Assert.That(state.AddResearch(definition, 40), Is.True);
            Assert.That(state.IsCompleted(definition.Id), Is.True);
        }

        [Test]
        public void FinanceSystem_ConvertsUnpaidCostIntoDebt()
        {
            var company = new Company(
                new CompanyId("company"),
                "테스트 회사",
                50);

            var policy = new OperatingCostPolicy(
                100,
                0,
                0,
                0,
                0,
                1000);

            var result = new CompanyFinanceSystem().ProcessDay(
                company,
                new DailyOperatingCosts(1, 0, 0, 0),
                policy);

            Assert.That(company.Cash, Is.EqualTo(0));
            Assert.That(company.Debt, Is.EqualTo(50));
            Assert.That(result.Bankrupt, Is.False);
        }

        [Test]
        public void TurnCommandQueue_RejectsOrderWhenActionPointsAreSpent()
        {
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var context = new TurnCommandContext(market);
            var queue = new TurnCommandQueue(1);
            var region = new RegionId("test");
            var resource = new ResourceId("iron");

            var first = new SubmitMarketOrderTurnCommand(
                new MarketOrder(
                    "first",
                    new CompanyId("player"),
                    region,
                    resource,
                    OrderSide.Buy,
                    OrderPurpose.ProductionInput,
                    10,
                    100,
                    1),
                "철광석 구매");

            var second = new SubmitMarketOrderTurnCommand(
                new MarketOrder(
                    "second",
                    new CompanyId("player"),
                    region,
                    resource,
                    OrderSide.Buy,
                    OrderPurpose.ProductionInput,
                    10,
                    100,
                    1),
                "추가 구매");

            Assert.That(
                queue.TryQueue(first, context, out _),
                Is.True);
            Assert.That(queue.RemainingActionPoints, Is.EqualTo(0));
            Assert.That(
                queue.TryQueue(second, context, out var reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("남은 행동력이 부족합니다."));
        }

        [Test]
        public void SimulationEngine_EndTurnAdvancesTurnAndCalendar()
        {
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(5, 2),
                new TurnNumber(1),
                new GameDay(0));

            TurnReport report = engine.EndTurn();

            Assert.That(report.Turn.Value, Is.EqualTo(1));
            Assert.That(engine.CurrentTurn.Value, Is.EqualTo(2));
            Assert.That(engine.CurrentCalendarDay.Value, Is.EqualTo(2));
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.PlayerPlanning));
        }

        [Test]
        public void Calendar_ConvertsA360DayCampaignIntoTwelveMonths()
        {
            Assert.That(
                GameCalendarDate.FromDayNumber(1).ToString(),
                Is.EqualTo("1월 1일"));
            Assert.That(
                GameCalendarDate.FromDayNumber(30).ToString(),
                Is.EqualTo("1월 30일"));
            Assert.That(
                GameCalendarDate.FromDayNumber(31).ToString(),
                Is.EqualTo("2월 1일"));
            Assert.That(
                GameCalendarDate.FromDayNumber(360).ToString(),
                Is.EqualTo("12월 30일"));
        }

        [Test]
        public void Operations_OfferThreeDifferentApproachesPerMissionKind()
        {
            foreach (WorldOpportunityKind kind in
                Enum.GetValues(typeof(WorldOpportunityKind)))
            {
                var approaches = WorldOperationCatalog.GetApproaches(kind);

                Assert.That(approaches.Count, Is.EqualTo(3));
                Assert.That(
                    approaches[0].Approach,
                    Is.Not.EqualTo(approaches[1].Approach));
                Assert.That(
                    approaches[1].Approach,
                    Is.Not.EqualTo(approaches[2].Approach));
                Assert.That(
                    approaches[0].UpfrontCostMultiplier,
                    Is.GreaterThanOrEqualTo(0m));
            }
        }

        [Test]
        public void Campaign_DominanceRequiresTwoFullMonthsFromMonthSeven()
        {
            CampaignState state = CreateCampaign(
                300,
                50,
                50);
            var evaluator = new CampaignVictoryEvaluator(
                new CampaignRuleSet(
                    maxTurns: 360,
                    dominanceCheckStartTurn: 181,
                    dominanceMultiplier: 3,
                    dominanceRequiredConsecutiveTurns: 60));

            CampaignTurnResult turn180 = evaluator.Evaluate(
                new TurnNumber(180),
                state);
            CampaignTurnResult result = null;
            for (int day = 181; day <= 240; day++)
            {
                result = evaluator.Evaluate(
                    new TurnNumber(day),
                    state);
                if (day < 240)
                {
                    Assert.That(
                        result.Outcome,
                        Is.EqualTo(CampaignOutcome.InProgress));
                }
            }

            Assert.That(turn180.Outcome, Is.EqualTo(CampaignOutcome.InProgress));
            Assert.That(turn180.DominanceConsecutiveTurns, Is.EqualTo(0));
            Assert.That(result, Is.Not.Null);
            Assert.That(result.Outcome, Is.EqualTo(CampaignOutcome.Victory));
            Assert.That(result.DominanceConsecutiveTurns, Is.EqualTo(60));
            Assert.That(
                result.EndReason,
                Is.EqualTo(CampaignEndReason.EconomicDominance));
        }

        [Test]
        public void EconomicPower_IncludesAssetsProfitDebtAndUnpaidCosts()
        {
            var company = new Company(
                new CompanyId("player"),
                "플레이어 기업",
                100);
            company.AddDebt(30);
            var participant = new CampaignParticipantState(
                company,
                true);
            participant.UpdateAssetValues(
                inventoryValue: 10,
                facilityValue: 10,
                logisticsValue: 10,
                territoryValue: 10,
                technologyValue: 10,
                unpaidCosts: 20);
            participant.RecordOperatingProfit(20);

            decimal power = new EconomicPowerCalculator().Calculate(
                participant,
                new CampaignRuleSet(recentProfitMultiplier: 5));

            Assert.That(power, Is.EqualTo(200));
        }

        [Test]
        public void Campaign_DominanceStreakResetsWhenRatioDropsBelowThree()
        {
            CampaignState state = CreateCampaign(
                300,
                50,
                50);
            var evaluator = new CampaignVictoryEvaluator(
                new CampaignRuleSet());

            CampaignTurnResult turn181 = evaluator.Evaluate(
                new TurnNumber(181),
                state);
            state.Participants[1].Company.Receive(100);
            CampaignTurnResult turn182 = evaluator.Evaluate(
                new TurnNumber(182),
                state);

            Assert.That(turn181.DominanceConsecutiveTurns, Is.EqualTo(1));
            Assert.That(turn182.Outcome, Is.EqualTo(CampaignOutcome.InProgress));
            Assert.That(turn182.DominanceConsecutiveTurns, Is.EqualTo(0));
        }

        [Test]
        public void Campaign_BankruptcyAndCapitalDestructionAreImmediateDefeats()
        {
            CampaignState bankruptState = CreateCampaign(100, 100);
            bankruptState.Player.Company.MarkBankrupt();
            CampaignTurnResult bankruptResult =
                new CampaignVictoryEvaluator(new CampaignRuleSet())
                    .Evaluate(new TurnNumber(1), bankruptState);

            CampaignState capitalState = CreateCampaign(100, 100);
            capitalState.Player.DestroyCapital();
            CampaignTurnResult capitalResult =
                new CampaignVictoryEvaluator(new CampaignRuleSet())
                    .Evaluate(new TurnNumber(1), capitalState);

            Assert.That(
                bankruptResult.EndReason,
                Is.EqualTo(CampaignEndReason.Bankruptcy));
            Assert.That(
                capitalResult.EndReason,
                Is.EqualTo(CampaignEndReason.CapitalDestroyed));
            Assert.That(bankruptResult.Outcome, Is.EqualTo(CampaignOutcome.Defeat));
            Assert.That(capitalResult.Outcome, Is.EqualTo(CampaignOutcome.Defeat));
        }

        [Test]
        public void Campaign_MapCapitalDestructionCausesImmediatePlayerDefeat()
        {
            CampaignState campaign = CreateCampaign(500000m, 500000m);
            var destruction = new MapCapitalDestroyedRecord(
                new GridCoordinate(0, 0),
                "player",
                "ai_1");

            Assert.That(
                new CampaignCapitalDestructionService().Apply(
                    campaign,
                    destruction),
                Is.True);

            CampaignTurnResult result = new CampaignVictoryEvaluator(
                new CampaignRuleSet()).Evaluate(new TurnNumber(1), campaign);
            Assert.That(result.Outcome, Is.EqualTo(CampaignOutcome.Defeat));
            Assert.That(
                result.EndReason,
                Is.EqualTo(CampaignEndReason.CapitalDestroyed));
        }

        [Test]
        public void Campaign_Month12AwardsVictoryToHighestEconomicPower()
        {
            CampaignState state = CreateCampaign(
                101,
                100,
                90);
            CampaignTurnResult result =
                new CampaignVictoryEvaluator(new CampaignRuleSet())
                    .Evaluate(new TurnNumber(360), state);

            Assert.That(result.Outcome, Is.EqualTo(CampaignOutcome.Victory));
            Assert.That(
                result.EndReason,
                Is.EqualTo(CampaignEndReason.TurnLimitVictory));
        }

        [Test]
        public void SimulationEngine_DoesNotAdvanceAfterCampaignDefeat()
        {
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            CampaignState state = CreateCampaign(100, 100);
            state.Player.DestroyCapital();
            var campaign = new CampaignSession(
                state,
                new CampaignVictoryEvaluator(new CampaignRuleSet()));
            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(),
                new TurnNumber(1),
                new GameDay(0),
                campaignSession: campaign);

            TurnReport report = engine.EndTurn();

            Assert.That(report.CampaignResult.IsFinished, Is.True);
            Assert.That(engine.CurrentTurn.Value, Is.EqualTo(1));
            Assert.That(engine.Phase, Is.EqualTo(TurnPhase.Completed));
            Assert.That(engine.IsCampaignFinished, Is.True);
        }

        [Test]
        public void FullTurn_ProductionFinanceAndEconomicPowerAreIntegrated()
        {
            var region = new RegionId("starter");
            var iron = CreateResource("iron", 100);
            var coal = CreateResource("coal", 80);
            var steel = CreateResource("steel", 220);
            CampaignState campaignState = CreateCampaign(1000, 1000);
            var world = new WorldEconomyState();

            RegisterMarket(world, region, iron, 80, 60);
            RegisterMarket(world, region, coal, 70, 60);
            RegisterMarket(world, region, steel, 70, 60);

            var playerWarehouse = new Warehouse(
                new WarehouseId("player_warehouse"),
                campaignState.Player.Company.Id,
                region,
                1000);
            playerWarehouse.TryAdd(iron.Id, 2, iron.StorageVolume);
            playerWarehouse.TryAdd(coal.Id, 1, coal.StorageVolume);

            var playerRuntime = new CompanyEconomyRuntime(
                campaignState.Player,
                playerWarehouse,
                0,
                10,
                5);
            playerRuntime.AddFactory(new Factory(
                new FactoryId("steel_factory"),
                campaignState.Player.Company.Id,
                region,
                new RecipeDefinition(
                    "steel_recipe",
                    new[]
                    {
                        new ResourceAmount(iron.Id, 2),
                        new ResourceAmount(coal.Id, 1)
                    },
                    new[] { new ResourceAmount(steel.Id, 1) },
                    10,
                    5,
                    1,
                    "강철 생산")));
            world.RegisterCompany(playerRuntime);

            var opponent = campaignState.Participants[1];
            world.RegisterCompany(new CompanyEconomyRuntime(
                opponent,
                new Warehouse(
                    new WarehouseId("opponent_warehouse"),
                    opponent.Company.Id,
                    region,
                    1000),
                0,
                0,
                0));

            var campaignRules = new CampaignRuleSet();
            var worldService = new WorldEconomyTurnService(
                world,
                new WorldEconomyTuning(new OperatingCostPolicy(
                    100,
                    30,
                    0,
                    0,
                    0,
                    100000)),
                campaignRules);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market, worldService),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(),
                new TurnNumber(1),
                new GameDay(0),
                campaignSession: new CampaignSession(
                    campaignState,
                    new CampaignVictoryEvaluator(campaignRules)));

            TurnReport report = engine.EndTurn();

            Assert.That(playerWarehouse.GetAvailable(iron.Id), Is.EqualTo(0));
            Assert.That(playerWarehouse.GetAvailable(coal.Id), Is.EqualTo(0));
            Assert.That(playerWarehouse.GetAvailable(steel.Id), Is.EqualTo(1));
            Assert.That(campaignState.Player.Company.Cash, Is.EqualTo(870));
            Assert.That(report.WorldReport.Production.Count, Is.EqualTo(1));
            Assert.That(report.WorldReport.Production[0].Produced, Is.True);
            Assert.That(report.WorldReport.Finances.Count, Is.EqualTo(2));
            Assert.That(campaignState.Player.InventoryValue, Is.GreaterThan(0));
            Assert.That(report.CampaignResult.Outcome,
                Is.EqualTo(CampaignOutcome.InProgress));
        }

        [Test]
        public void FullTurn_MarketFillTransfersCashAndWarehouseStock()
        {
            var region = new RegionId("starter");
            var steel = CreateResource("steel", 100);
            CampaignState campaignState = CreateCampaign(1000, 100);
            var world = new WorldEconomyState();
            RegisterMarket(world, region, steel, 10, 10);

            var buyerWarehouse = new Warehouse(
                new WarehouseId("buyer_warehouse"),
                campaignState.Player.Company.Id,
                region,
                1000);
            var buyer = new CompanyEconomyRuntime(
                campaignState.Player,
                buyerWarehouse,
                0,
                0,
                0);
            world.RegisterCompany(buyer);

            var sellerState = campaignState.Participants[1];
            var sellerWarehouse = new Warehouse(
                new WarehouseId("seller_warehouse"),
                sellerState.Company.Id,
                region,
                1000);
            sellerWarehouse.TryAdd(steel.Id, 5, steel.StorageVolume);
            world.RegisterCompany(new CompanyEconomyRuntime(
                sellerState,
                sellerWarehouse,
                0,
                0,
                0));

            var campaignRules = new CampaignRuleSet();
            var worldService = new WorldEconomyTurnService(
                world,
                new WorldEconomyTuning(new OperatingCostPolicy(
                    0,
                    0,
                    0,
                    0,
                    0,
                    100000)),
                campaignRules);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            market.SubmitOrder(new MarketOrder(
                "buy",
                campaignState.Player.Company.Id,
                region,
                steel.Id,
                OrderSide.Buy,
                OrderPurpose.ProductionInput,
                2,
                120,
                0));
            market.SubmitOrder(new MarketOrder(
                "sell",
                sellerState.Company.Id,
                region,
                steel.Id,
                OrderSide.Sell,
                OrderPurpose.Export,
                2,
                80,
                0));

            var engine = new SimulationEngine(
                new TurnResolutionOrchestrator(market, worldService),
                market,
                _ => System.Array.Empty<PhysicalFlow>(),
                new TurnRuleSet(),
                new TurnNumber(1),
                new GameDay(0),
                campaignSession: new CampaignSession(
                    campaignState,
                    new CampaignVictoryEvaluator(campaignRules)));

            TurnReport report = engine.EndTurn();

            Assert.That(campaignState.Player.Company.Cash, Is.EqualTo(800));
            Assert.That(sellerState.Company.Cash, Is.EqualTo(300));
            Assert.That(buyerWarehouse.GetAvailable(steel.Id), Is.EqualTo(2));
            Assert.That(sellerWarehouse.GetAvailable(steel.Id), Is.EqualTo(3));
            Assert.That(report.WorldReport.Trades.Count, Is.EqualTo(1));
            Assert.That(report.WorldReport.Trades[0].Settled, Is.True);
        }

        [Test]
        public void Warehouse_RejectsStockBeyondCapacity()
        {
            var warehouse = new Warehouse(
                new WarehouseId("warehouse"),
                new CompanyId("company"),
                new RegionId("region"),
                10);

            Assert.That(
                warehouse.TryAdd("steel", 4, 2),
                Is.True);
            Assert.That(warehouse.UsedCapacity, Is.EqualTo(8));
            Assert.That(
                warehouse.TryAdd("iron", 3, 1),
                Is.False);
            Assert.That(warehouse.GetAvailable("iron"), Is.EqualTo(0));
        }

        [Test]
        public void Market_BuyOrderRaisesDemandAndDerivedPrice()
        {
            var region = new RegionId("region");
            var definition = CreateResource("iron", 100);
            var state = new ResourceMarketState(
                definition.Id,
                100,
                1000);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            market.SubmitOrder(new MarketOrder(
                "player_buy",
                new CompanyId("player"),
                region,
                definition.Id,
                OrderSide.Buy,
                OrderPurpose.ProductionInput,
                100,
                120,
                0));

            market.ProcessMarketPhase(
                new GameDay(0),
                new[]
                {
                    new PhysicalFlow(
                        region,
                        definition.Id,
                        definition,
                        state,
                        100,
                        100,
                        0)
                });

            Assert.That(state.DailyDemand, Is.EqualTo(200));
            Assert.That(state.CurrentPrice, Is.GreaterThan(100));
        }

        [Test]
        public void Market_PhysicalFlowChangesStockAndRecordsRealShortage()
        {
            var region = new RegionId("region");
            ResourceDefinition iron = CreateResource("iron", 100m);
            var state = new ResourceMarketState(iron.Id, 100m, 10m);
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());

            market.ProcessMarketPhase(
                new GameDay(0),
                new[]
                {
                    new PhysicalFlow(
                        region,
                        iron.Id,
                        iron,
                        state,
                        supply: 5m,
                        demand: 12m,
                        marketStockChange: 0m)
                });

            Assert.That(state.MarketStock, Is.EqualTo(3m));
            Assert.That(state.UnmetDemand, Is.EqualTo(0m));

            market.ProcessMarketPhase(
                new GameDay(1),
                new[]
                {
                    new PhysicalFlow(
                        region,
                        iron.Id,
                        iron,
                        state,
                        supply: 0m,
                        demand: 10m,
                        marketStockChange: 0m)
                });

            Assert.That(state.MarketStock, Is.EqualTo(0m));
            Assert.That(state.UnmetDemand, Is.EqualTo(7m));
        }

        [Test]
        public void CompanyAI_SubmitsDeterministicSellOrderFromMarketSurplus()
        {
            var region = new RegionId("region");
            var iron = CreateResource("iron", 100);
            var marketState = new ResourceMarketState(
                iron.Id,
                100,
                1000);
            marketState.BeginDay();
            marketState.RecordSupply(200);
            marketState.RecordDemand(100);

            CampaignState campaign = CreateCampaign(1000, 1000);
            var world = new WorldEconomyState();
            world.RegisterMarket(new MarketRuntimeState(
                region,
                iron,
                marketState,
                100,
                100));

            var player = campaign.Player;
            world.RegisterCompany(new CompanyEconomyRuntime(
                player,
                new Warehouse(
                    new WarehouseId("player_warehouse"),
                    player.Company.Id,
                    region,
                    1000),
                0,
                0,
                0));

            var opponent = campaign.Participants[1];
            var aiWarehouse = new Warehouse(
                new WarehouseId("ai_warehouse"),
                opponent.Company.Id,
                region,
                1000);
            aiWarehouse.TryAdd(iron.Id, 10, iron.StorageVolume);
            world.RegisterCompany(new CompanyEconomyRuntime(
                opponent,
                aiWarehouse,
                0,
                0,
                0));

            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());
            var ai = new AICompanyTurnService(
                world,
                market,
                maxActionsPerCompany: 2);

            ai.ResolveTurn(new TurnNumber(2), new GameDay(1));

            Assert.That(ai.LastSubmittedOrderCount, Is.EqualTo(1));
            Assert.That(market.SubmittedOrderCount, Is.EqualTo(1));
        }

        [Test]
        public void PvpCoordinator_RejectsWrongTurnOwnershipReplayAndSequence()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "wrong_turn",
                    "player_1",
                    "company_1",
                    2,
                    1)).Code,
                Is.EqualTo(PvpOperationCode.WrongTurn));

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "wrong_owner",
                    "player_1",
                    "company_2",
                    1,
                    1)).Code,
                Is.EqualTo(PvpOperationCode.CompanyOwnershipMismatch));

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "command_1",
                    "player_1",
                    "company_1",
                    1,
                    1)).Success,
                Is.True);

            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "command_1",
                    "player_1",
                    "company_1",
                    1,
                    2)).Code,
                Is.EqualTo(PvpOperationCode.DuplicateCommand));

            PvpOperationResult sequence = coordinator.SubmitCommand(
                CreatePvpMarketCommand(
                    "command_3",
                    "player_1",
                    "company_1",
                    1,
                    3));
            Assert.That(sequence.Code, Is.EqualTo(PvpOperationCode.SequenceMismatch));
            Assert.That(sequence.ExpectedSequence, Is.EqualTo(2));
        }

        [Test]
        public void PvpCoordinator_EnforcesPerPlayerActionPointBudget()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var matchId = new PvpMatchId("match");
            var playerId = new PvpPlayerId("player_1");
            var companyId = new CompanyId("company_1");

            var first = new PvpCommandEnvelope(
                "build_1",
                matchId,
                playerId,
                companyId,
                new TurnNumber(1),
                1,
                PvpCommandKind.BuildFacility,
                new PvpCommandPayload(
                    new RegionId("region"),
                    targetId: "factory_a"));
            var second = new PvpCommandEnvelope(
                "build_2",
                matchId,
                playerId,
                companyId,
                new TurnNumber(1),
                2,
                PvpCommandKind.BuildFacility,
                new PvpCommandPayload(
                    new RegionId("region"),
                    targetId: "factory_b"));

            Assert.That(coordinator.SubmitCommand(first).Success, Is.True);
            Assert.That(
                coordinator.SubmitCommand(second).Code,
                Is.EqualTo(PvpOperationCode.InsufficientActionPoints));
        }

        [Test]
        public void PvpCoordinator_LocksSortsHashesAndAdvancesTurn()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();

            Assert.That(coordinator.SubmitCommand(CreatePvpMarketCommand(
                "slot_1",
                "player_2",
                "company_2",
                1,
                1)).Success, Is.True);
            Assert.That(coordinator.SubmitCommand(CreatePvpMarketCommand(
                "slot_0",
                "player_1",
                "company_1",
                1,
                1)).Success, Is.True);

            Assert.That(
                coordinator.MarkReady(new PvpPlayerId("player_1")).Success,
                Is.True);
            Assert.That(
                coordinator.MarkReady(new PvpPlayerId("player_2")).Success,
                Is.True);
            Assert.That(coordinator.Phase, Is.EqualTo(PvpMatchPhase.Locked));

            PvpOperationResult begin = coordinator.TryBeginResolution(
                out var package);
            Assert.That(begin.Success, Is.True);
            Assert.That(package.Commands.Count, Is.EqualTo(2));
            Assert.That(package.Commands[0].PlayerId.Value, Is.EqualTo("player_1"));
            Assert.That(package.CommandHash.Length, Is.EqualTo(64));

            var duplicatePackage = new PvpTurnPackage(
                package.MatchId,
                package.Turn,
                package.Commands);
            Assert.That(
                duplicatePackage.CommandHash,
                Is.EqualTo(package.CommandHash));

            Assert.That(
                coordinator.CompleteResolution("authoritative_hash", false).Success,
                Is.True);
            Assert.That(coordinator.CurrentTurn.Value, Is.EqualTo(2));
            Assert.That(coordinator.Phase, Is.EqualTo(PvpMatchPhase.Planning));
            Assert.That(coordinator.Revision, Is.EqualTo(1));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(0));
            Assert.That(
                coordinator.CreateSnapshot().Players[0].SpentActionPoints,
                Is.EqualTo(0));
        }

        [Test]
        public void PvpCoordinator_CancelRefundsPointsAndRestoresSequence()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            PvpCommandEnvelope command = CreatePvpMarketCommand(
                "cancel_me",
                "player_1",
                "company_1",
                1,
                1);

            Assert.That(coordinator.SubmitCommand(command).Success, Is.True);
            PvpOperationResult cancelled = coordinator.CancelLastCommand(
                new PvpPlayerId("player_1"),
                "cancel_me");

            Assert.That(cancelled.Success, Is.True);
            Assert.That(cancelled.ExpectedSequence, Is.EqualTo(1));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(0));
            Assert.That(
                coordinator.CreateSnapshot().Players[0].SpentActionPoints,
                Is.EqualTo(0));
            Assert.That(coordinator.SubmitCommand(command).Success, Is.True);
        }

        [Test]
        public void PvpMarketTranslator_CreatesExistingTurnCommand()
        {
            var translator = new PvpMarketCommandTranslator();
            bool created = translator.TryCreateTurnCommand(
                CreatePvpMarketCommand(
                    "translate",
                    "player_1",
                    "company_1",
                    1,
                    1),
                out var command,
                out var code);

            Assert.That(created, Is.True);
            Assert.That(code, Is.EqualTo(PvpOperationCode.Accepted));
            Assert.That(command, Is.TypeOf<SubmitMarketOrderTurnCommand>());
            Assert.That(command.ActorId, Is.EqualTo(new CompanyId("company_1")));
        }

        [Test]
        public void PvpCoordinator_ReconnectRestoresOnlyOwnPendingCommands()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var player1 = new PvpPlayerId("player_1");

            Assert.That(
                coordinator.SetConnected(player1, false).Success,
                Is.True);
            Assert.That(
                coordinator.SubmitCommand(CreatePvpMarketCommand(
                    "offline",
                    "player_1",
                    "company_1",
                    1,
                    1)).Code,
                Is.EqualTo(PvpOperationCode.PlayerDisconnected));

            coordinator.SetConnected(player1, true);
            coordinator.SubmitCommand(CreatePvpMarketCommand(
                "own_command",
                "player_1",
                "company_1",
                1,
                1));
            coordinator.SubmitCommand(CreatePvpMarketCommand(
                "opponent_command",
                "player_2",
                "company_2",
                1,
                1));

            IReadOnlyList<PvpCommandEnvelope> restored =
                coordinator.GetPendingCommands(player1);

            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(restored[0].CommandId, Is.EqualTo("own_command"));
            Assert.That(
                coordinator.CreateSnapshot().Players[0].IsConnected,
                Is.True);
        }

        [Test]
        public void PvpGateway_ReplayedRequestIsIdempotent()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            var peer = new PvpPeerContext(
                "connection_1",
                new PvpPlayerId("player_1"));
            PvpClientRequest request = CreatePvpSubmitRequest(
                "request_1",
                "command_1",
                "player_1",
                "company_1",
                0,
                1);

            PvpServerResponse first = gateway.Handle(peer, request);
            PvpServerResponse replay = gateway.Handle(peer, request);

            Assert.That(first.Result.Success, Is.True);
            Assert.That(replay.Result.Success, Is.True);
            Assert.That(replay.IsReplay, Is.True);
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(1));
            Assert.That(replay.OwnPendingCommands.Count, Is.EqualTo(1));
        }

        [Test]
        public void PvpGateway_RejectsSpoofedAuthenticatedPlayer()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            gateway.Handle(
                new PvpPeerContext(
                    "connection_1",
                    new PvpPlayerId("player_1")),
                CreatePvpSubmitRequest(
                    "victim_request",
                    "victim_command",
                    "player_1",
                    "company_1",
                    0,
                    1));
            var peer = new PvpPeerContext(
                "connection_2",
                new PvpPlayerId("player_2"));
            PvpClientRequest spoofed = CreatePvpSubmitRequest(
                "spoofed_request",
                "spoofed_command",
                "player_1",
                "company_1",
                0,
                1);

            PvpServerResponse response = gateway.Handle(peer, spoofed);

            Assert.That(
                response.Result.Code,
                Is.EqualTo(PvpOperationCode.AuthenticationMismatch));
            Assert.That(response.OwnPendingCommands.Count, Is.EqualTo(0));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(1));
        }

        [Test]
        public void PvpGateway_RejectsRequestIdReuseWithDifferentPayload()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            var peer = new PvpPeerContext(
                "connection_1",
                new PvpPlayerId("player_1"));

            gateway.Handle(peer, CreatePvpSubmitRequest(
                "same_request",
                "command_1",
                "player_1",
                "company_1",
                0,
                1));
            PvpServerResponse conflict = gateway.Handle(
                peer,
                CreatePvpSubmitRequest(
                    "same_request",
                    "command_2",
                    "player_1",
                    "company_1",
                    0,
                    2));

            Assert.That(
                conflict.Result.Code,
                Is.EqualTo(PvpOperationCode.DuplicateRequestConflict));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(1));
        }

        [Test]
        public void PvpGateway_RejectsStaleRevisionBeforeMutation()
        {
            PvpTurnCoordinator coordinator = CreatePvpCoordinator();
            var gateway = new PvpAuthoritativeGateway(coordinator);
            var peer = new PvpPeerContext(
                "connection_1",
                new PvpPlayerId("player_1"));
            PvpClientRequest stale = CreatePvpSubmitRequest(
                "stale_request",
                "stale_command",
                "player_1",
                "company_1",
                1,
                1);

            PvpServerResponse response = gateway.Handle(peer, stale);

            Assert.That(
                response.Result.Code,
                Is.EqualTo(PvpOperationCode.StaleRevision));
            Assert.That(response.Snapshot.Revision, Is.EqualTo(0));
            Assert.That(coordinator.PendingCommandCount, Is.EqualTo(0));
        }

        [Test]
        public void ResourceExtractionSite_OutputDeclinesButNeverBelowMinimum()
        {
            var site = new ResourceExtractionSite(
                "iron_site",
                new RegionId("starter"),
                new ResourceId("iron"),
                new TurnNumber(5),
                initialOutput: 100m,
                minimumOutput: 20m,
                declineRatePerTurn: 0.5m);

            Assert.That(site.GetOutput(new TurnNumber(4)), Is.EqualTo(0m));
            Assert.That(site.GetOutput(new TurnNumber(5)), Is.EqualTo(100m));
            Assert.That(site.GetOutput(new TurnNumber(6)), Is.EqualTo(50m));
            Assert.That(site.GetOutput(new TurnNumber(7)), Is.EqualTo(25m));
            Assert.That(site.GetOutput(new TurnNumber(8)), Is.EqualTo(20m));
            Assert.That(site.GetOutput(new TurnNumber(30)), Is.EqualTo(20m));
        }

        [Test]
        public void ProceduralWorld_SameSeedRecreatesInitialConditions()
        {
            var resources = new[]
            {
                new ResourceId("food"),
                new ResourceId("wood"),
                new ResourceId("iron"),
                new ResourceId("coal")
            };
            var settings = new WorldGenerationSettings(
                regionCount: 6,
                factionCount: 3,
                settlementCount: 5,
                npcCount: 12,
                initialResourceSiteCount: 8);
            var generator = new ProceduralWorldGenerator();

            ProceduralWorldState first = generator.Generate(
                12345,
                "world",
                settings,
                resources);
            ProceduralWorldState second = generator.Generate(
                12345,
                "world",
                settings,
                resources);

            Assert.That(first.Regions.Count, Is.EqualTo(6));
            Assert.That(first.Factions.Count, Is.EqualTo(3));
            Assert.That(first.Npcs.Count, Is.EqualTo(12));
            Assert.That(first.ResourceSiteSeeds.Count, Is.EqualTo(8));
            Assert.That(first.Regions[0].Terrain,
                Is.EqualTo(second.Regions[0].Terrain));
            Assert.That(first.Relations[0].Score,
                Is.EqualTo(second.Relations[0].Score));
        }

        [Test]
        public void ResourceExtraction_UsesReserveAndSupportsDeepDevelopment()
        {
            var site = new ResourceExtractionSite(
                "deep_iron",
                new RegionId("mountain"),
                new ResourceId("iron"),
                new TurnNumber(1),
                100m,
                20m,
                0.10m,
                1000m,
                1m,
                100m,
                100m,
                "faction",
                ExtractionMethod.Surface);

            decimal initialReserve = site.RemainingReserve;
            site.Extract(new TurnNumber(1));
            Assert.That(site.RemainingReserve, Is.LessThan(initialReserve));

            decimal depletedReserve = site.RemainingReserve;
            site.DevelopDeepLayer(500m, 0.15m);
            Assert.That(site.RemainingReserve,
                Is.EqualTo(depletedReserve + 500m));
            Assert.That(site.Method, Is.EqualTo(ExtractionMethod.DeepMining));
            Assert.That(site.ExtractionEfficiency, Is.EqualTo(1.15m));
        }

        [Test]
        public void Military_RangedApproachArmorAndRecruitDilutionAreApplied()
        {
            var catalog = MilitaryBalanceCatalog.CreatePrototypeDefaults();
            var archer = new MilitaryUnit(
                "archer",
                "attacker",
                catalog.Get(UnitArchetype.Archer),
                new EquipmentLoadout(
                    "light",
                    "경장비",
                    ArmorProfile.Light),
                100,
                averageExperience: 80m);
            decimal experiencedAverage = archer.AverageExperience;
            archer.Recruit(100);

            Assert.That(
                archer.AverageExperience,
                Is.LessThan(experiencedAverage));
            Assert.That(
                new DamageProfile(0m, 0m, 1m)
                    .ResolveAgainst(ArmorProfile.Heavy),
                Is.GreaterThan(
                    new DamageProfile(1m, 0m, 0m)
                        .ResolveAgainst(ArmorProfile.Heavy)));
            var logistics = new MilitaryLogisticsTuning();
            Assert.That(
                logistics.GetReplacementSpeed(1m),
                Is.GreaterThan(logistics.GetReplacementSpeed(0m)));

            var attackers = new ArmyState(
                "attackers",
                "attacker",
                new RegionId("field"));
            attackers.AddUnit(archer);
            var defenders = new ArmyState(
                "defenders",
                "defender",
                new RegionId("field"));
            defenders.AddUnit(new MilitaryUnit(
                "spear",
                "defender",
                catalog.Get(UnitArchetype.Spearman),
                new EquipmentLoadout(
                    "heavy",
                    "중장비",
                    ArmorProfile.Heavy),
                180));

            BattleReport report = new BattleResolver(
                logistics).Resolve(
                    attackers,
                    defenders,
                    77);
            bool sawRangedApproach = false;
            bool sawMelee = false;
            for (int i = 0; i < report.Phases.Count; i++)
            {
                sawRangedApproach |= report.Phases[i].Phase ==
                    BattlePhase.RangedApproach;
                sawMelee |= report.Phases[i].Phase == BattlePhase.Melee;
            }

            Assert.That(sawRangedApproach, Is.True);
            Assert.That(sawMelee, Is.True);
        }

        [Test]
        public void ResourceSiteEvent_EveryFiveTurnsAddsDecliningMarketSupply()
        {
            var region = new RegionId("starter");
            var iron = CreateResource("iron", 100m);
            var world = new WorldEconomyState();
            RegisterMarket(world, region, iron, supply: 10m, demand: 10m);

            var service = new WorldEconomyTurnService(
                world,
                new WorldEconomyTuning(new OperatingCostPolicy(
                    0m,
                    0m,
                    0m,
                    0m,
                    0m,
                    100000m)),
                new CampaignRuleSet(),
                new ResourceSiteEventSettings(
                    spawnIntervalTurns: 5,
                    initialOutput: 100m,
                    minimumOutput: 20m,
                    declineRatePerTurn: 0.5m,
                    allowedResourceIds: new[] { "iron" }));
            var market = new MarketManager(
                new SupplyDemandLedger(new MarketTuning()),
                new PriceCalculator());

            decimal turn5Supply = 0m;
            decimal turn6Supply = 0m;
            decimal turn8Supply = 0m;
            decimal turn10Supply = 0m;
            int turn5SpawnCount = 0;
            int turn10SpawnCount = 0;

            for (int turn = 1; turn <= 10; turn++)
            {
                var turnNumber = new TurnNumber(turn);
                var day = new GameDay(turn - 1);
                IReadOnlyList<PhysicalFlow> flows =
                    service.PrepareTurn(turnNumber, day);
                decimal supply = flows[0].Supply;
                MarketTickReport marketReport =
                    market.ProcessMarketPhase(day, flows);
                WorldTurnReport report = service.CompleteTurn(
                    turnNumber,
                    day,
                    marketReport);

                if (turn == 5)
                {
                    turn5Supply = supply;
                    turn5SpawnCount = report.ResourceSites
                        .SpawnedSites.Count;
                }
                else if (turn == 6)
                {
                    turn6Supply = supply;
                }
                else if (turn == 8)
                {
                    turn8Supply = supply;
                }
                else if (turn == 10)
                {
                    turn10Supply = supply;
                    turn10SpawnCount = report.ResourceSites
                        .SpawnedSites.Count;
                }
            }

            Assert.That(turn5Supply, Is.EqualTo(110m));
            Assert.That(turn6Supply, Is.EqualTo(60m));
            Assert.That(turn8Supply, Is.EqualTo(30m));
            Assert.That(turn10Supply, Is.EqualTo(130m));
            Assert.That(turn5SpawnCount, Is.EqualTo(1));
            Assert.That(turn10SpawnCount, Is.EqualTo(1));
            Assert.That(world.ResourceSites.Count, Is.EqualTo(2));
        }

        private static PvpTurnCoordinator CreatePvpCoordinator()
        {
            return new PvpTurnCoordinator(
                new PvpMatchId("match"),
                new[]
                {
                    new PvpPlayerSlot(
                        0,
                        new PvpPlayerId("player_1"),
                        new CompanyId("company_1"),
                        "플레이어 1"),
                    new PvpPlayerSlot(
                        1,
                        new PvpPlayerId("player_2"),
                        new CompanyId("company_2"),
                        "플레이어 2")
                },
                new PvpMatchRules(
                    minPlayers: 2,
                    maxPlayers: 2,
                    maxActionPointsPerPlayer: 5,
                    maxCommandsPerPlayer: 8));
        }

        private static PvpCommandEnvelope CreatePvpMarketCommand(
            string commandId,
            string playerId,
            string companyId,
            int turn,
            int sequence)
        {
            return new PvpCommandEnvelope(
                commandId,
                new PvpMatchId("match"),
                new PvpPlayerId(playerId),
                new CompanyId(companyId),
                new TurnNumber(turn),
                sequence,
                PvpCommandKind.MarketBuy,
                PvpCommandPayload.MarketOrder(
                    new RegionId("region"),
                    new ResourceId("iron"),
                    10,
                    100));
        }

        private static PvpClientRequest CreatePvpSubmitRequest(
            string requestId,
            string commandId,
            string playerId,
            string companyId,
            int expectedRevision,
            int sequence)
        {
            return new PvpClientRequest(
                PvpProtocol.CurrentVersion,
                requestId,
                PvpClientRequestKind.SubmitCommand,
                new PvpMatchId("match"),
                new PvpPlayerId(playerId),
                expectedRevision,
                CreatePvpMarketCommand(
                    commandId,
                    playerId,
                    companyId,
                    1,
                    sequence));
        }

        private static ResourceDefinition CreateResource(
            string id,
            decimal price)
        {
            return new ResourceDefinition(
                new ResourceId(id),
                id,
                price,
                ResourceRarity.Common,
                1,
                false);
        }

        private static void RegisterMarket(
            WorldEconomyState world,
            RegionId region,
            ResourceDefinition definition,
            decimal supply,
            decimal demand)
        {
            world.RegisterMarket(new MarketRuntimeState(
                region,
                definition,
                new ResourceMarketState(
                    definition.Id,
                    definition.BasePrice,
                    1000),
                supply,
                demand));
        }

        private static void AssertStartHasLandExit(
            GridMapLayout layout,
            GridCoordinate start,
            int seed)
        {
            var offsets = new[]
            {
                new GridCoordinate(1, 0),
                new GridCoordinate(-1, 0),
                new GridCoordinate(0, 1),
                new GridCoordinate(0, -1)
            };
            int landExitCount = 0;
            for (int i = 0; i < offsets.Length; i++)
            {
                var candidate = new GridCoordinate(
                    start.X + offsets[i].X,
                    start.Y + offsets[i].Y);
                if (layout.TryNormalize(candidate, out GridCoordinate normalized) &&
                    layout.IsLand(normalized))
                {
                    landExitCount++;
                }
            }

            Assert.That(
                landExitCount,
                Is.GreaterThanOrEqualTo(2),
                $"seed {seed} 시작점 {start}에는 이동 가능한 육지 출구가 필요합니다.");
        }

        private static CampaignState CreateCampaign(
            decimal playerCash,
            params decimal[] opponentCash)
        {
            var participants = new List<CampaignParticipantState>
            {
                new CampaignParticipantState(
                    new Company(
                        new CompanyId("player"),
                        "플레이어 기업",
                        playerCash),
                    true)
            };

            for (int i = 0; i < opponentCash.Length; i++)
            {
                participants.Add(new CampaignParticipantState(
                    new Company(
                        new CompanyId($"opponent_{i + 1}"),
                        $"경쟁 기업 {i + 1}",
                        opponentCash[i]),
                    false));
            }

            return new CampaignState(participants);
        }

        private sealed class RecordingAutonomousWorldService :
            IAutonomousWorldTurnService
        {
            public AutonomousWorldState State => null;
            public decimal Capability { get; private set; }
            public WorldOperationApproach Approach { get; private set; }
            public string ResolverId { get; private set; }
            public string ResolverDisplayName { get; private set; }
            public bool WasDelegated { get; private set; }

            public void SynchronizeResourceSites()
            {
            }

            public AutonomousWorldTurnReport PrepareTurn(
                TurnNumber turn,
                GameDay calendarDay) => AutonomousWorldTurnReport.Empty;

            public void CompleteTurn(
                TurnNumber turn,
                GameDay calendarDay,
                MarketTickReport marketReport)
            {
            }

            public bool CanPlayerIntervene(
                string opportunityId,
                out string reason)
            {
                reason = string.Empty;
                return true;
            }

            public bool CanPlayerIntervene(
                string opportunityId,
                WorldOperationApproach approach,
                out string reason)
            {
                reason = string.Empty;
                return true;
            }

            public PlayerInterventionResult TryPlayerIntervention(
                string opportunityId,
                decimal playerCapability,
                TurnNumber turn) => new PlayerInterventionResult(
                    true,
                    true,
                    "직접 수행",
                    0m,
                    0m);

            public PlayerInterventionResult TryPlayerIntervention(
                string opportunityId,
                decimal playerCapability,
                WorldOperationApproach approach,
                TurnNumber turn) => new PlayerInterventionResult(
                    true,
                    true,
                    "직접 수행",
                    0m,
                    0m,
                    approach: approach);

            public PlayerInterventionResult TryIntervention(
                string opportunityId,
                decimal capability,
                WorldOperationApproach approach,
                string resolverId,
                string resolverDisplayName,
                bool wasDelegated,
                TurnNumber turn)
            {
                Capability = capability;
                Approach = approach;
                ResolverId = resolverId;
                ResolverDisplayName = resolverDisplayName;
                WasDelegated = wasDelegated;
                return new PlayerInterventionResult(
                    true,
                    true,
                    "위임 수행",
                    0m,
                    0m,
                    approach: approach,
                    resolverId: resolverId,
                    resolverDisplayName: resolverDisplayName,
                    wasDelegated: wasDelegated);
            }
        }
    }
}
