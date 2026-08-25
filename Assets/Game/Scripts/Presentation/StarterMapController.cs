using System;
using System.Collections.Generic;
using System.Linq;
using Game.Application.World;
using Game.Domain.Military;
using Game.Domain.World;
using UnityEngine;

namespace Game.Presentation
{
    public enum MapCellContent
    {
        Empty,
        PlayerBase,
        EnemyBase,
        NeutralCastle,
        PlayerCastle,
        EnemyCastle,
        NormalMine,
        GoldMine
    }

    public readonly struct MapCellSelection
    {
        public GridCoordinate Coordinate { get; }
        public MapCellContent Content { get; }
        public string DisplayName { get; }
        public string InteractionHint { get; }
        public string UnitId { get; }
        public string UnitOwnerFactionId { get; }
        public string MineOwnerFactionId { get; }
        public string CapturingFactionId { get; }
        public int CaptureProgress { get; }
        public int CaptureRequired { get; }
        public string CastleOwnerFactionId { get; }
        public MapCastleRole CastleRole { get; }
        public MapCastleConflictKind CastleConflictKind { get; }
        public int CastleGarrisonUnitCount { get; }

        public MapCellSelection(
            GridCoordinate coordinate,
            MapCellContent content,
            string displayName,
            string interactionHint,
            string unitId = "",
            string unitOwnerFactionId = "",
            string mineOwnerFactionId = "",
            string capturingFactionId = "",
            int captureProgress = 0,
            int captureRequired = 0,
            string castleOwnerFactionId = "",
            MapCastleRole castleRole = MapCastleRole.Unassigned,
            MapCastleConflictKind castleConflictKind =
                MapCastleConflictKind.None,
            int castleGarrisonUnitCount = 0)
        {
            Coordinate = coordinate;
            Content = content;
            DisplayName = displayName ?? string.Empty;
            InteractionHint = interactionHint ?? string.Empty;
            UnitId = unitId ?? string.Empty;
            UnitOwnerFactionId = unitOwnerFactionId ?? string.Empty;
            MineOwnerFactionId = mineOwnerFactionId ?? string.Empty;
            CapturingFactionId = capturingFactionId ?? string.Empty;
            CaptureProgress = Math.Max(0, captureProgress);
            CaptureRequired = Math.Max(0, captureRequired);
            CastleOwnerFactionId = castleOwnerFactionId ?? string.Empty;
            CastleRole = castleRole;
            CastleConflictKind = castleConflictKind;
            CastleGarrisonUnitCount = Math.Max(0, castleGarrisonUnitCount);
        }

        public override string ToString() =>
            $"{DisplayName} {Coordinate}\n{InteractionHint}";
    }

    /// <summary>
    /// 문명식 평면 월드 맵을 표시한다.
    /// 논리 좌표와 카메라는 가로로 래핑되고 세로는 극지 경계에서 막힌다.
    /// 화면 경계가 비지 않도록 동일한 맵 표면과 표식을 좌우에 한 장씩 복제한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StarterMapController : MonoBehaviour
    {
        private const int OpponentCount = 3;
        private const int SurfaceCopyRadius = 1;
        private const int PixelsPerTile = 8;

        [Header("문명식 대형 평면 맵")]
        [SerializeField, Range(40, 160)] private int mapWidth = 80;
        [SerializeField, Range(24, 100)] private int mapHeight = 48;
        [SerializeField, Range(20, 400)] private int mineCount = 160;
        [SerializeField, Range(0, 24)] private int neutralCastleCount = 8;
        [SerializeField, Min(0.5f)] private float tileSize = 1.15f;
        [SerializeField] private int playerStartX = 4;
        [SerializeField] private int playerStartY = 24;

        [Header("카메라")]
        [SerializeField, Min(1f)] private float cameraPanSpeed = 20f;
        [SerializeField, Min(0.1f)] private float mousePanSensitivity = 1f;
        [SerializeField, Min(1f)] private float minimumZoom = 7f;
        [SerializeField, Min(2f)] private float maximumZoom = 24f;
        [SerializeField, Min(1f)] private float startingZoom = 16f;
        [SerializeField] private bool createCameraIfMissing = true;
        [SerializeField] private bool createLightIfMissing = true;

        [Header("유닛 이동 경로")]
        [SerializeField, Min(0.03f)] private float movementPathWidth = 0.12f;
        [SerializeField, Min(0.01f)] private float movementPathHeight = 0.13f;
        [SerializeField, Range(0f, 1f)] private float movementPathAlpha = 0.92f;
        [SerializeField, Min(1f)] private float movementInterpolationSharpness =
            18f;
        [Header("플레이어 유닛 가시성")]
        [SerializeField, Min(1f)] private float playerUnitVisualScale = 1.38f;
        [SerializeField, Min(0f)] private float castleUnitElevation = 1.18f;
        [SerializeField, Min(0f)] private float mineUnitElevation = 0.52f;
        [SerializeField] private Color playerUnitHighlightColor =
            new Color(1.00f, 0.82f, 0.08f, 1f);
        [SerializeField, Min(0.5f)] private float protagonistPortraitWidth =
            1.55f;
        [SerializeField, Min(0.5f)] private float aiCommanderPortraitWidth =
            1.25f;
        [SerializeField, Min(0f)] private float commanderPortraitHeight =
            2.05f;

        [Header("세력과 거점 색상")]
        [SerializeField] private Color playerFactionColor =
            new Color(0.05f, 0.42f, 1.00f, 1f);
        [SerializeField] private Color[] enemyFactionColors =
        {
            new Color(0.92f, 0.10f, 0.12f, 1f),
            new Color(0.68f, 0.16f, 0.88f, 1f),
            new Color(1.00f, 0.38f, 0.04f, 1f)
        };
        [SerializeField] private Color neutralFactionColor =
            new Color(0.72f, 0.72f, 0.72f, 1f);
        [SerializeField] private Color ironMineStructureColor =
            new Color(0.30f, 0.36f, 0.46f, 1f);
        [SerializeField] private Color goldMineStructureColor =
            new Color(1.00f, 0.65f, 0.03f, 1f);
        [SerializeField] private Color unclaimedIronMineFloorColor =
            new Color(0.22f, 0.32f, 0.46f, 1f);
        [SerializeField] private Color unclaimedGoldMineFloorColor =
            new Color(0.56f, 0.35f, 0.04f, 1f);

        private readonly GridMapLayoutGenerator _layoutGenerator =
            new GridMapLayoutGenerator();
        private readonly HashSet<Collider> _mapSurfaceColliders =
            new HashSet<Collider>();
        private readonly List<Transform> _iconBillboards =
            new List<Transform>();
        private readonly Dictionary<string, List<Transform>> _unitMarkerRoots =
            new Dictionary<string, List<Transform>>(StringComparer.Ordinal);
        private readonly Dictionary<string, Vector3> _unitVisualPositions =
            new Dictionary<string, Vector3>(StringComparer.Ordinal);
        private readonly Dictionary<string, string>
            _authoritativeServerUnitIdByLocalId =
                new Dictionary<string, string>(StringComparer.Ordinal);

        private Transform _generatedRoot;
        private Transform _gameplayMarkerRoot;
        private Camera _mapCamera;
        private Sprite _normalMineSprite;
        private Sprite _goldMineSprite;
        private Sprite _protagonistCommanderSprite;
        private Sprite _aiCommanderSprite;
        private Sprite _commanderSquareSprite;
        private Sprite _commanderArrowSprite;
        private Texture2D _mapTexture;
        private Texture2D _movementPathTexture;
        private Texture2D _commanderSquareTexture;
        private Texture2D _commanderArrowTexture;
        private Material _mapMaterial;
        private Material _blockMaterial;
        private Material _movementPathMaterial;
        private Mesh _mapMesh;
        private Vector3 _cameraFocus;
        private Vector3 _lastMousePosition;
        private bool _isMousePanning;
        private int _generationSequence;
        private RealtimeMapGameplayService _gameplayService;
        private string _selectedPlayerUnitId = string.Empty;
        private string _trackedEnemyUnitId = string.Empty;

        public GridMapLayout CurrentLayout { get; private set; }
        public MapCellSelection? CurrentSelection { get; private set; }
        public RealtimeMapGameplayService GameplayService => _gameplayService;
        public string SelectedPlayerUnitId => _selectedPlayerUnitId;
        public string TrackedEnemyUnitId => _trackedEnemyUnitId;
        public string SelectedAuthoritativeServerUnitId =>
            _authoritativeServerUnitIdByLocalId.TryGetValue(
                _selectedPlayerUnitId,
                out string serverUnitId)
                ? serverUnitId
                : string.Empty;
        public bool IsAuthoritativeMap { get; private set; }
        public bool PointerSelectionBlocked { get; set; }
        public MapUnitState SelectedPlayerUnit =>
            _gameplayService?.FindUnit(_selectedPlayerUnitId);
        public IReadOnlyList<MapCommanderState> Commanders =>
            _gameplayService?.Commanders ?? Array.Empty<MapCommanderState>();
        public Color GetFactionDisplayColor(string factionId) =>
            GetFactionColor(factionId);
        public MapGenerationSettings GenerationSettings { get; private set; }
        public event Action<MapCellSelection> CellSelected;
        public event Action<MapCellSelection> CellMoveRequested;
        public event Action PrimaryCellSelected;
        public event Action<MapCellSelection, Vector2> CellActionRequested;
        public event Action GameplayStateChanged;
        public event Action<MapMineCaptureRecord> MineCaptured;
        public event Action<MapMineSpawnRecord> MineSpawned;
        public event Action<MapMineConstructionCompletedRecord>
            MineConstructionCompleted;
        public event Action<MapCastleCaptureRecord> CastleCaptured;
        public event Action<MapCapitalDestroyedRecord> CapitalDestroyed;
        public event Action<MapCastleRoleChangedRecord> CastleRoleChanged;
        public event Action<MapSiegeDayResult> SiegeDayResolved;
        public event Action<MapFieldBattleResult> FieldBattleResolved;
        public event Action<MapCommanderGeneratedRecord> CommanderGenerated;
        public event Action<MapCommanderDeathRecord> CommanderDied;
        public event Action<MapSupplyInterdictionResult>
            SupplyInterdictionResolved;
        public event Action<MapWorldMissionState> WorldMissionReady;

        public void Initialize()
        {
            EnsureLight();
            if (_generatedRoot == null)
                GenerateNewMap();
            EnsureCamera();
        }

        public void ResetMap()
        {
            RemoveGeneratedMap();
            EnsureLight();
            GenerateNewMap();
            EnsureCamera();
        }

        public void PanMap(float horizontal, float vertical)
        {
            if (CurrentLayout == null)
                return;

            _cameraFocus.x += horizontal;
            _cameraFocus.z += vertical;
            ClampAndWrapCameraFocus();
            ApplyCameraTransform();
        }

        public void FocusPlayerFaction()
        {
            if (CurrentLayout != null)
                FocusCameraOn(CurrentLayout.PlayerStart);
        }

        public bool TryGetCoordinate(
            Vector3 worldPosition,
            out GridCoordinate coordinate)
        {
            coordinate = default;
            if (CurrentLayout == null)
                return false;

            float worldWidth = CurrentLayout.Width * tileSize;
            float worldHeight = CurrentLayout.Height * tileSize;
            float wrappedX = WrapCentered(worldPosition.x, worldWidth);
            float fromLeft = wrappedX + worldWidth * 0.5f;
            float fromBottom = worldPosition.z + worldHeight * 0.5f;
            int x = Mathf.FloorToInt(fromLeft / tileSize);
            int y = Mathf.FloorToInt(fromBottom / tileSize);

            if (y < 0 || y >= CurrentLayout.Height)
                return false;

            x = PositiveModulo(x, CurrentLayout.Width);
            coordinate = new GridCoordinate(x, y);
            return true;
        }

        public bool CanCreatePlayerUnit(out string reason)
        {
            if (CurrentLayout == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return CanCreatePlayerUnitAt(CurrentLayout.PlayerStart, out reason);
        }

        public bool CanCreatePlayerUnitAt(
            GridCoordinate origin,
            out string reason)
        {
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.CanCreateUnitAt(
                _gameplayService.PlayerFactionId,
                origin,
                out reason);
        }

        public bool TryGetPlayerRecruitmentSite(
            GridCoordinate coordinate,
            out MapRecruitmentSiteSnapshot snapshot)
        {
            snapshot = default;
            return _gameplayService != null &&
                _gameplayService.TryGetRecruitmentSiteSnapshot(
                    _gameplayService.PlayerFactionId,
                    coordinate,
                    out snapshot);
        }

        public bool TryCreatePlayerUnit(out string reason)
        {
            return TryCreatePlayerUnit(UnitArchetype.Swordsman, out reason);
        }

        public bool TryCreatePlayerUnit(
            UnitArchetype archetype,
            out string reason)
        {
            return TryCreatePlayerUnit(
                archetype,
                UnitEquipmentCatalog.GetDefaultWeapon(archetype),
                ArmorClass.Light,
                out reason);
        }

        public bool TryCreatePlayerUnit(
            UnitArchetype archetype,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            out string reason)
        {
            if (CurrentLayout == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return TryCreatePlayerUnitAt(
                CurrentLayout.PlayerStart,
                archetype,
                weaponType,
                armorClass,
                out reason);
        }

        public bool TryCreatePlayerUnitAt(
            GridCoordinate origin,
            UnitArchetype archetype,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            out string reason)
        {
            if (!CanCreatePlayerUnitAt(origin, out reason))
                return false;

            if (!_gameplayService.TryCreateUnitAt(
                _gameplayService.PlayerFactionId,
                origin,
                archetype,
                weaponType,
                armorClass,
                out MapUnitState unit,
                out reason))
            {
                return false;
            }

            _selectedPlayerUnitId = unit.Id;
            _trackedEnemyUnitId = string.Empty;
            RefreshGameplayMarkers();
            RefreshCurrentSelection();
            return true;
        }

        public bool TryEquipSelectedPlayerUnit(
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 장비를 변경할 플레이어 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.TryChangeEquipment(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                weaponType,
                armorClass,
                out reason);
        }

        public void ConfigureMapGeneration(MapGenerationSettings settings)
        {
            GenerationSettings = settings ?? new MapGenerationSettings();
            mapWidth = GenerationSettings.Width;
            mapHeight = GenerationSettings.Height;
            mineCount = GenerationSettings.MineCount;
            neutralCastleCount = GenerationSettings.NeutralCastleCount;
            playerStartX = Math.Max(2, GenerationSettings.Width / 20);
            playerStartY = GenerationSettings.Height / 2;
        }

        public bool TrySetSelectedPlayerUnitFormation(
            MapUnitFormationPreset preset,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 편성을 변경할 플레이어 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.TrySetUnitFormationPreset(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                preset,
                out reason);
        }

        public bool TryHireCommanderForSelectedPlayerUnit(
            string commanderId,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 지휘관을 배속할 플레이어 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.TryHireCommander(
                _gameplayService.PlayerFactionId,
                commanderId,
                _selectedPlayerUnitId,
                out reason);
        }

        public bool CanHireCommanderForSelectedPlayerUnit(
            string commanderId,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 지휘관을 배속할 플레이어 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.CanHireCommander(
                _gameplayService.PlayerFactionId,
                commanderId,
                _selectedPlayerUnitId,
                out reason);
        }

        public MapUnitState FindUnitAt(GridCoordinate coordinate)
        {
            return _gameplayService?.FindUnitAt(coordinate);
        }

        public bool TrySelectMapCell(
            GridCoordinate coordinate,
            out MapCellSelection selection)
        {
            selection = default;
            if (CurrentLayout == null ||
                !CurrentLayout.TryNormalize(
                    coordinate,
                    out GridCoordinate normalized))
            {
                return false;
            }

            selection = DescribeCell(CurrentLayout, normalized);
            ApplyMapSelection(selection);
            CellSelected?.Invoke(selection);
            return true;
        }

        public bool CanSelectPlayerUnitAt(
            GridCoordinate coordinate,
            out string reason)
        {
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            MapUnitState unit = _gameplayService.FindOwnedUnitAt(
                _gameplayService.PlayerFactionId,
                coordinate);
            if (unit == null)
            {
                reason = "이 칸에 선택할 수 있는 아군 유닛이 없습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TrySelectPlayerUnitAt(
            GridCoordinate coordinate,
            out string reason)
        {
            if (!CanSelectPlayerUnitAt(coordinate, out reason))
                return false;

            MapUnitState unit = _gameplayService.FindOwnedUnitAt(
                _gameplayService.PlayerFactionId,
                coordinate);
            _selectedPlayerUnitId = unit.Id;
            _trackedEnemyUnitId = string.Empty;
            RefreshGameplayMarkers();
            RefreshCurrentSelection();
            return true;
        }

        public bool CanMoveSelectedPlayerUnit(
            GridCoordinate destination,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 아군 유닛을 선택하세요.";
                return false;
            }

            return _gameplayService.CanIssueMove(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                destination,
                out _,
                out reason);
        }

        public bool WillSelectedMoveUseSeaTransport(
            GridCoordinate destination)
        {
            return _gameplayService != null &&
                SelectedPlayerUnit != null &&
                _gameplayService.WillUseSeaTransport(
                    _gameplayService.PlayerFactionId,
                    _selectedPlayerUnitId,
                    destination);
        }

        public bool TryMoveSelectedPlayerUnit(
            GridCoordinate destination,
            out string reason)
        {
            if (!CanMoveSelectedPlayerUnit(destination, out reason))
                return false;

            bool moved = _gameplayService.TryIssueMove(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                destination,
                out reason);
            if (moved)
            {
                RefreshGameplayMarkers();
            }
            return moved;
        }

        public bool CanAttackSelectedEnemyUnit(
            string targetUnitId,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 추적·공격할 아군 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.CanIssueAttackUnit(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                targetUnitId,
                out reason);
        }

        public bool TryAttackSelectedEnemyUnit(
            string targetUnitId,
            out string reason)
        {
            if (!CanAttackSelectedEnemyUnit(targetUnitId, out reason))
                return false;

            bool ordered = _gameplayService.TryIssueAttackUnit(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                targetUnitId,
                out reason);
            if (ordered)
                RefreshGameplayMarkers();
            return ordered;
        }

        public bool TryGetSelectedPlayerAttackTarget(
            out MapUnitState target)
        {
            target = null;
            return _gameplayService != null &&
                SelectedPlayerUnit != null &&
                _gameplayService.TryGetAttackTarget(
                    _selectedPlayerUnitId,
                    out target);
        }

        public bool CanCancelSelectedPlayerUnitMove(out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 아군 유닛을 선택하세요.";
                return false;
            }

            return _gameplayService.CanCancelMove(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                out reason);
        }

        public bool TryCancelSelectedPlayerUnitMove(out string reason)
        {
            if (!CanCancelSelectedPlayerUnitMove(out reason))
                return false;

            bool cancelled = _gameplayService.TryCancelMove(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                out reason);
            if (cancelled)
            {
                RefreshGameplayMarkers();
            }
            return cancelled;
        }

        public MapCastleControlState FindCastleAt(GridCoordinate coordinate)
        {
            return _gameplayService?.FindCastle(coordinate);
        }

        public bool CanCaptureOrSiegeSelectedCastle(
            GridCoordinate coordinate,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "먼저 점령에 사용할 플레이어 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.CanIssueCastleOccupation(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                out reason);
        }

        public bool TryCaptureOrSiegeSelectedCastle(
            GridCoordinate coordinate,
            out string reason)
        {
            if (!CanCaptureOrSiegeSelectedCastle(coordinate, out reason))
                return false;

            bool ordered = _gameplayService.TryIssueCastleOccupation(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                out reason);
            if (ordered)
            {
                RefreshGameplayMarkers();
            }
            return ordered;
        }

        public bool TrySetPlayerCastleRole(
            GridCoordinate coordinate,
            MapCastleRole role,
            out string reason)
        {
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.TrySetCastleRole(
                _gameplayService.PlayerFactionId,
                coordinate,
                role,
                out reason);
        }

        public bool CanSetSelectedPlayerSiegeAction(
            GridCoordinate coordinate,
            MapSiegeAction action,
            out string reason)
        {
            if (_gameplayService == null || SelectedPlayerUnit == null)
            {
                reason = "공성 행동을 지시할 아군 부대를 선택하세요.";
                return false;
            }

            return _gameplayService.CanSetSiegeAction(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                action,
                out reason);
        }

        public bool TrySetSelectedPlayerSiegeAction(
            GridCoordinate coordinate,
            MapSiegeAction action,
            out string reason)
        {
            if (!CanSetSelectedPlayerSiegeAction(
                coordinate,
                action,
                out reason))
            {
                return false;
            }

            return _gameplayService.TrySetSiegeAction(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                action,
                out reason);
        }

        public bool TrySetPlayerOccupationPolicy(
            GridCoordinate coordinate,
            MapOccupationPolicy policy,
            out string reason)
        {
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.TrySetOccupationPolicy(
                _gameplayService.PlayerFactionId,
                coordinate,
                policy,
                out reason);
        }

        public void AdvanceGameplayFixedSteps(int fixedStepCount)
        {
            _gameplayService?.AdvanceFixedSteps(fixedStepCount);
        }

        public IReadOnlyList<MapMineProductionRecord> CreateDailyMineProduction()
        {
            return _gameplayService?.CreateDailyProduction() ??
                Array.Empty<MapMineProductionRecord>();
        }

        public IReadOnlyList<MapMilitaryUpkeepRecord>
            CreateDailyMilitaryUpkeep()
        {
            return _gameplayService?.CreateDailyMilitaryUpkeep() ??
                Array.Empty<MapMilitaryUpkeepRecord>();
        }

        public bool AdvanceEconomicDay(out MapMineSpawnRecord spawnedMine)
        {
            if (_gameplayService == null)
            {
                spawnedMine = default;
                return false;
            }

            return _gameplayService.AdvanceEconomicDay(out spawnedMine);
        }

        public bool CanSurveySelectedEconomicSite(
            GridCoordinate coordinate,
            out string reason)
        {
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.CanSurveyEconomicSite(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                out reason);
        }

        public bool TrySurveySelectedEconomicSite(
            GridCoordinate coordinate,
            out MapEconomicSurveyState survey,
            out string reason)
        {
            survey = null;
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.TrySurveyEconomicSite(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                out survey,
                out reason);
        }

        public bool CanStartSelectedMineConstruction(
            GridCoordinate coordinate,
            out MapEconomicSurveyState survey,
            out string reason)
        {
            survey = null;
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.CanStartMineConstruction(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                out survey,
                out reason);
        }

        public bool TryStartSelectedMineConstruction(
            GridCoordinate coordinate,
            out MapMineConstructionState construction,
            out string reason)
        {
            construction = null;
            if (_gameplayService == null)
            {
                reason = "지도 게임플레이가 아직 준비되지 않았습니다.";
                return false;
            }

            return _gameplayService.TryStartMineConstruction(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                out construction,
                out reason);
        }

        public IReadOnlyList<MapSupplyTransportRecord>
            AdvanceDailySupplyLogistics(
                SimulationBootstrapper simulation)
        {
            if (_gameplayService == null || simulation == null)
                return Array.Empty<MapSupplyTransportRecord>();

            simulation.StockMapCapitalSupplies(_gameplayService);
            IReadOnlyList<MapSupplyTransportRecord> transports =
                _gameplayService.CreateDailySupplyTransports();
            simulation.SettleMapSupplyTransportCosts(transports);
            return transports;
        }

        public bool TryGetPendingSupplyRouteOwnerAt(
            GridCoordinate coordinate,
            out string ownerFactionId)
        {
            if (_gameplayService == null)
            {
                ownerFactionId = string.Empty;
                return false;
            }
            return _gameplayService.TryGetPendingSupplyRouteOwnerAt(
                coordinate,
                out ownerFactionId);
        }

        public bool TryAssignSelectedPlayerSupplyMission(
            GridCoordinate coordinate,
            MapSupplyMissionKind missionKind,
            out string reason)
        {
            if (_gameplayService == null ||
                string.IsNullOrEmpty(_selectedPlayerUnitId))
            {
                reason = "먼저 아군 부대를 선택하세요.";
                return false;
            }
            return _gameplayService.TryAssignSupplyMission(
                _gameplayService.PlayerFactionId,
                _selectedPlayerUnitId,
                coordinate,
                missionKind,
                out reason);
        }

        public bool TryAssignWorldMission(
            SubordinateMissionPlan plan,
            WorldMissionMapTarget target,
            out string reason)
        {
            if (_gameplayService == null)
            {
                reason = "실시간 지도가 준비되지 않았습니다.";
                return false;
            }
            return _gameplayService.TryAssignWorldMission(
                _gameplayService.PlayerFactionId,
                plan.UnitId,
                plan.OpportunityId,
                target,
                out reason);
        }

        public bool CompleteWorldMission(
            string unitId,
            string opportunityId,
            bool cancelled = false) =>
            _gameplayService != null &&
            _gameplayService.CompleteWorldMission(
                unitId,
                opportunityId,
                cancelled);

        private void GenerateNewMap()
        {
            IsAuthoritativeMap = false;
            int width = Mathf.Clamp(mapWidth, 40, 160);
            int height = Mathf.Clamp(mapHeight, 24, 100);
            int startX = PositiveModulo(playerStartX, width);
            int startY = Mathf.Clamp(playerStartY, 0, height - 1);
            var playerStart = new GridCoordinate(startX, startY);
            IReadOnlyList<GridCoordinate> opponentStarts =
                CreateOpponentStarts(width, height, playerStart);
            int seed = GenerationSettings != null
                ? GenerationSettings.Seed
                : unchecked(
                    Environment.TickCount ^
                    GetInstanceID() ^
                    (++_generationSequence * 397));
            int minimumMineCount = Mathf.RoundToInt(width * height * 0.03f);
            int requestedMines = Mathf.Max(mineCount, minimumMineCount);

            CurrentLayout = _layoutGenerator.Generate(
                width,
                height,
                requestedMines,
                seed,
                playerStart,
                opponentStarts,
                GenerationSettings?.WrapHorizontally ?? true,
                Mathf.Clamp(neutralCastleCount, 0, 24),
                GenerationSettings?.OceanThreshold ?? 0.34d);

            var rootObject = new GameObject(
                $"대형 평면 경제 월드_{width}x{height}");
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;

            CreateGameplayService(CurrentLayout);
            LoadMapIcons();
            BuildFlatMapCopies(CurrentLayout);
            BuildPlayerStart(CurrentLayout.PlayerStart);
            BuildOpponentStarts(CurrentLayout.OpponentStarts);
            BuildNeutralCastles(CurrentLayout.NeutralCastles);
            BuildMines(CurrentLayout);
            CurrentSelection = DescribeCell(
                CurrentLayout,
                CurrentLayout.PlayerStart);
            RefreshGameplayMarkers();
            FocusCameraOn(CurrentLayout.PlayerStart);
        }

        public bool ApplyAuthoritativeSnapshot(
            PvpMapWorldStateDto snapshot,
            string ownCompanyId,
            out string reason)
        {
            if (snapshot == null ||
                snapshot.width < 2 ||
                snapshot.height < 2 ||
                snapshot.terrain == null ||
                snapshot.terrain.Length != snapshot.width * snapshot.height ||
                snapshot.units == null ||
                snapshot.mines == null ||
                snapshot.castles == null ||
                string.IsNullOrWhiteSpace(ownCompanyId))
            {
                reason = "서버 지도 스냅샷이 올바르지 않습니다.";
                return false;
            }

            PvpMapCastleStateDto ownCapital = null;
            var opponentCapitals = new List<PvpMapCastleStateDto>();
            var neutralCastles = new List<GridCoordinate>();
            for (int i = 0; i < snapshot.castles.Length; i++)
            {
                PvpMapCastleStateDto castle = snapshot.castles[i];
                if (castle.isCapital)
                {
                    if (string.Equals(
                            castle.originalOwnerCompanyId,
                            ownCompanyId,
                            StringComparison.Ordinal))
                    {
                        ownCapital = castle;
                    }
                    else
                    {
                        opponentCapitals.Add(castle);
                    }
                }
                else
                {
                    neutralCastles.Add(new GridCoordinate(
                        castle.x,
                        castle.y));
                }
            }
            if (ownCapital == null)
            {
                reason = "서버 지도에서 내 수도를 찾을 수 없습니다.";
                return false;
            }

            opponentCapitals.Sort((left, right) =>
                string.Compare(
                    left.originalOwnerCompanyId,
                    right.originalOwnerCompanyId,
                    StringComparison.Ordinal));
            var opponentStarts = new List<GridCoordinate>(
                opponentCapitals.Count);
            var opponentFactionIds = new List<string>(
                opponentCapitals.Count);
            for (int i = 0; i < opponentCapitals.Count; i++)
            {
                opponentStarts.Add(new GridCoordinate(
                    opponentCapitals[i].x,
                    opponentCapitals[i].y));
                opponentFactionIds.Add(
                    opponentCapitals[i].originalOwnerCompanyId);
            }

            var terrain = new GridTerrainKind[snapshot.terrain.Length];
            for (int i = 0; i < terrain.Length; i++)
            {
                terrain[i] = Enum.IsDefined(
                    typeof(GridTerrainKind),
                    snapshot.terrain[i])
                    ? (GridTerrainKind)snapshot.terrain[i]
                    : GridTerrainKind.Plains;
            }
            var mines = new MinePlacement[snapshot.mines.Length];
            for (int i = 0; i < snapshot.mines.Length; i++)
            {
                PvpMapMineStateDto mine = snapshot.mines[i];
                if (!Enum.TryParse(
                        mine.kind,
                        true,
                        out MineKind mineKind))
                {
                    mineKind = MineKind.Normal;
                }
                mines[i] = new MinePlacement(
                    new GridCoordinate(mine.x, mine.y),
                    mineKind);
            }

            RemoveGeneratedMap();
            CurrentLayout = new GridMapLayout(
                snapshot.width,
                snapshot.height,
                snapshot.seed,
                new GridCoordinate(ownCapital.x, ownCapital.y),
                opponentStarts,
                mines,
                snapshot.wrapHorizontally,
                terrain,
                neutralCastles);
            IsAuthoritativeMap = true;

            var rootObject = new GameObject(
                $"서버 권위 경제 월드_{snapshot.width}x{snapshot.height}");
            rootObject.transform.SetParent(transform, false);
            _generatedRoot = rootObject.transform;

            DetachGameplayService();
            _gameplayService = new RealtimeMapGameplayService(
                CurrentLayout,
                ownCompanyId,
                opponentFactionIds,
                enableAi: false);
            _gameplayService.RestoreAuthoritativeEconomicDay(
                snapshot.currentEconomicDay);
            BindGameplayServiceEvents();
            _authoritativeServerUnitIdByLocalId.Clear();

            PvpMapUnitStateDto[] orderedUnits = snapshot.units
                .OrderBy(unit => unit.unitId, StringComparer.Ordinal)
                .ToArray();
            for (int i = 0; i < orderedUnits.Length; i++)
            {
                PvpMapUnitStateDto serverUnit = orderedUnits[i];
                if (!Enum.TryParse(
                        serverUnit.archetype,
                        true,
                        out UnitArchetype archetype))
                {
                    archetype = UnitArchetype.Swordsman;
                }
                if (!_gameplayService.TryCreateUnit(
                        serverUnit.ownerCompanyId,
                        archetype,
                        out MapUnitState localUnit,
                        out _))
                {
                    continue;
                }

                var path = new List<GridCoordinate>();
                if (serverUnit.plannedPath != null &&
                    serverUnit.plannedPath.Length > 1)
                {
                    path = new List<GridCoordinate>(
                        serverUnit.plannedPath.Length - 1);
                    for (int pathIndex = 1;
                         pathIndex < serverUnit.plannedPath.Length;
                         pathIndex++)
                    {
                        path.Add(new GridCoordinate(
                            serverUnit.plannedPath[pathIndex].x,
                            serverUnit.plannedPath[pathIndex].y));
                    }
                }
                if (!_gameplayService.TryRestoreAuthoritativeUnitState(
                        localUnit.Id,
                        new GridCoordinate(serverUnit.x, serverUnit.y),
                        path,
                        serverUnit.movementProgress,
                        serverUnit.soldiers,
                        serverUnit.stamina,
                        (decimal)serverUnit.morale,
                        (decimal)serverUnit.fatigue,
                        out reason))
                {
                    return false;
                }
                _authoritativeServerUnitIdByLocalId[localUnit.Id] =
                    serverUnit.unitId;
                if (string.IsNullOrEmpty(_selectedPlayerUnitId) &&
                    string.Equals(
                        serverUnit.ownerCompanyId,
                        ownCompanyId,
                        StringComparison.Ordinal))
                {
                    _selectedPlayerUnitId = localUnit.Id;
                    _trackedEnemyUnitId = string.Empty;
                }
            }

            for (int i = 0; i < snapshot.mines.Length; i++)
            {
                PvpMapMineStateDto source = snapshot.mines[i];
                if (!_gameplayService.TryRestoreAuthoritativeMineState(
                        new GridCoordinate(source.x, source.y),
                        source.ownerCompanyId,
                        source.capturingCompanyId,
                        source.captureProgress,
                        out reason))
                {
                    return false;
                }
            }
            for (int i = 0; i < snapshot.castles.Length; i++)
            {
                PvpMapCastleStateDto source = snapshot.castles[i];
                Enum.TryParse(
                    source.role,
                    true,
                    out MapCastleRole role);
                Enum.TryParse(
                    source.conflictKind,
                    true,
                    out MapCastleConflictKind conflictKind);
                Enum.TryParse(
                    source.siegeAction,
                    true,
                    out MapSiegeAction siegeAction);
                Enum.TryParse(
                    source.occupationPolicy,
                    true,
                    out MapOccupationPolicy occupationPolicy);
                if (!_gameplayService.TryRestoreAuthoritativeCastleState(
                        new GridCoordinate(source.x, source.y),
                        source.ownerCompanyId,
                        source.capturingCompanyId,
                        source.captureProgress,
                        role,
                        conflictKind,
                        siegeAction,
                        occupationPolicy,
                        source.isDestroyed,
                        source.wallDurability,
                        source.foodSupply,
                        out reason))
                {
                    return false;
                }
            }

            LoadMapIcons();
            BuildFlatMapCopies(CurrentLayout);
            BuildPlayerStart(CurrentLayout.PlayerStart);
            BuildOpponentStarts(CurrentLayout.OpponentStarts);
            BuildNeutralCastles(CurrentLayout.NeutralCastles);
            BuildMines(CurrentLayout);
            GridCoordinate focus = SelectedPlayerUnit?.Coordinate ??
                CurrentLayout.PlayerStart;
            CurrentSelection = DescribeCell(CurrentLayout, focus);
            RefreshGameplayMarkers();
            FocusCameraOn(focus);
            EnsureCamera();
            reason = string.Empty;
            return true;
        }

        private static IReadOnlyList<GridCoordinate> CreateOpponentStarts(
            int width,
            int height,
            GridCoordinate playerStart)
        {
            var anchors = new[]
            {
                new GridCoordinate(
                    PositiveModulo(playerStart.X + width / 2, width),
                    height - 1 - playerStart.Y),
                new GridCoordinate(
                    PositiveModulo(playerStart.X + width / 3, width),
                    Mathf.Clamp(height * 2 / 3, 0, height - 1)),
                new GridCoordinate(
                    PositiveModulo(playerStart.X + width * 2 / 3, width),
                    Mathf.Clamp(height / 3, 0, height - 1))
            };

            var opponents = new List<GridCoordinate>(OpponentCount);
            for (int i = 0; i < anchors.Length; i++)
            {
                if (!anchors[i].Equals(playerStart) &&
                    !opponents.Contains(anchors[i]))
                {
                    opponents.Add(anchors[i]);
                }
            }

            return opponents;
        }

        private void CreateGameplayService(GridMapLayout layout)
        {
            DetachGameplayService();
            var aiFactionIds = new List<string>(layout.OpponentStarts.Count);
            for (int i = 0; i < layout.OpponentStarts.Count; i++)
                aiFactionIds.Add($"ai_{i + 1}");

            _gameplayService = new RealtimeMapGameplayService(
                layout,
                "player",
                aiFactionIds);
            BindGameplayServiceEvents();

            if (_gameplayService.TryCreateUnit(
                _gameplayService.PlayerFactionId,
                UnitArchetype.Swordsman,
                out MapUnitState startingUnit,
                out _))
            {
                _selectedPlayerUnitId = startingUnit.Id;
                _trackedEnemyUnitId = string.Empty;
                _gameplayService.TryHireCommander(
                    _gameplayService.PlayerFactionId,
                    RealtimeMapGameplayService.ProtagonistCommanderId,
                    startingUnit.Id,
                    out _);
            }
            else
            {
                _selectedPlayerUnitId = string.Empty;
                _trackedEnemyUnitId = string.Empty;
            }
        }

        private void BindGameplayServiceEvents()
        {
            if (_gameplayService == null)
                return;

            _gameplayService.StateChanged += HandleGameplayStateChanged;
            _gameplayService.MineCaptured += HandleMineCaptured;
            _gameplayService.MineSpawned += HandleMineSpawned;
            _gameplayService.MineConstructionCompleted +=
                HandleMineConstructionCompleted;
            _gameplayService.CastleCaptured += HandleCastleCaptured;
            _gameplayService.CapitalDestroyed += HandleCapitalDestroyed;
            _gameplayService.CastleRoleChanged += HandleCastleRoleChanged;
            _gameplayService.SiegeDayResolved += HandleSiegeDayResolved;
            _gameplayService.FieldBattleResolved +=
                HandleFieldBattleResolved;
            _gameplayService.CommanderGenerated += HandleCommanderGenerated;
            _gameplayService.CommanderDied += HandleCommanderDied;
            _gameplayService.SupplyInterdictionResolved +=
                HandleSupplyInterdictionResolved;
            _gameplayService.WorldMissionReady += HandleWorldMissionReady;
        }

        private void DetachGameplayService()
        {
            if (_gameplayService != null)
            {
                _gameplayService.StateChanged -= HandleGameplayStateChanged;
                _gameplayService.MineCaptured -= HandleMineCaptured;
                _gameplayService.MineSpawned -= HandleMineSpawned;
                _gameplayService.MineConstructionCompleted -=
                    HandleMineConstructionCompleted;
                _gameplayService.CastleCaptured -= HandleCastleCaptured;
                _gameplayService.CapitalDestroyed -= HandleCapitalDestroyed;
                _gameplayService.CastleRoleChanged -= HandleCastleRoleChanged;
                _gameplayService.SiegeDayResolved -= HandleSiegeDayResolved;
                _gameplayService.FieldBattleResolved -=
                    HandleFieldBattleResolved;
                _gameplayService.CommanderGenerated -= HandleCommanderGenerated;
                _gameplayService.CommanderDied -= HandleCommanderDied;
                _gameplayService.SupplyInterdictionResolved -=
                    HandleSupplyInterdictionResolved;
                _gameplayService.WorldMissionReady -= HandleWorldMissionReady;
            }

            _gameplayService = null;
            _selectedPlayerUnitId = string.Empty;
            _trackedEnemyUnitId = string.Empty;
            _authoritativeServerUnitIdByLocalId.Clear();
            _unitMarkerRoots.Clear();
            _unitVisualPositions.Clear();
        }

        private void HandleWorldMissionReady(MapWorldMissionState mission)
        {
            WorldMissionReady?.Invoke(mission);
        }

        private void BuildFlatMapCopies(GridMapLayout layout)
        {
            _mapTexture = CreateMapTexture(layout);
            _mapMesh = CreateMapMesh(layout);
            _mapMaterial = CreateMapMaterial(_mapTexture);
            float worldWidth = layout.Width * tileSize;
            float worldHeight = layout.Height * tileSize;

            for (int copy = -SurfaceCopyRadius;
                 copy <= SurfaceCopyRadius;
                 copy++)
            {
                var surface = new GameObject($"월드 표면 복제 {copy:+0;-0;0}");
                surface.transform.SetParent(_generatedRoot, false);
                surface.transform.localPosition =
                    new Vector3(copy * worldWidth, 0f, 0f);

                var filter = surface.AddComponent<MeshFilter>();
                filter.sharedMesh = _mapMesh;
                var renderer = surface.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _mapMaterial;

                var collider = surface.AddComponent<BoxCollider>();
                collider.center = new Vector3(0f, -0.04f, 0f);
                collider.size = new Vector3(worldWidth, 0.08f, worldHeight);
                _mapSurfaceColliders.Add(collider);
            }
        }

        private Texture2D CreateMapTexture(GridMapLayout layout)
        {
            int textureWidth = layout.Width * PixelsPerTile;
            int textureHeight = layout.Height * PixelsPerTile;
            var texture = new Texture2D(
                textureWidth,
                textureHeight,
                TextureFormat.RGBA32,
                false,
                true)
            {
                name = $"월드 맵 텍스처_{layout.Width}x{layout.Height}",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color32[textureWidth * textureHeight];

            for (int tileY = 0; tileY < layout.Height; tileY++)
            {
                for (int tileX = 0; tileX < layout.Width; tileX++)
                {
                    var coordinate = new GridCoordinate(tileX, tileY);
                    Color baseColor = GetTerrainColor(
                        layout.GetTerrain(coordinate),
                        tileX,
                        tileY);
                    Color borderColor = Color.Lerp(
                        baseColor,
                        Color.black,
                        0.24f);

                    for (int pixelY = 0;
                         pixelY < PixelsPerTile;
                         pixelY++)
                    {
                        for (int pixelX = 0;
                             pixelX < PixelsPerTile;
                             pixelX++)
                        {
                            bool border = pixelX == 0 || pixelY == 0;
                            int x = tileX * PixelsPerTile + pixelX;
                            int y = tileY * PixelsPerTile + pixelY;
                            pixels[y * textureWidth + x] =
                                border ? borderColor : baseColor;
                        }
                    }
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Color GetTerrainColor(
            GridTerrainKind terrain,
            int x,
            int y)
        {
            float variation = ((x * 17 + y * 31) & 3) * 0.012f;
            Color color;
            switch (terrain)
            {
                case GridTerrainKind.Ocean:
                    color = new Color(0.055f, 0.20f, 0.35f);
                    break;
                case GridTerrainKind.Forest:
                    color = new Color(0.11f, 0.31f, 0.16f);
                    break;
                case GridTerrainKind.Desert:
                    color = new Color(0.66f, 0.53f, 0.27f);
                    break;
                case GridTerrainKind.Hills:
                    color = new Color(0.32f, 0.34f, 0.25f);
                    break;
                case GridTerrainKind.Tundra:
                    color = new Color(0.60f, 0.67f, 0.68f);
                    break;
                default:
                    color = new Color(0.25f, 0.46f, 0.23f);
                    break;
            }

            return new Color(
                Mathf.Clamp01(color.r + variation),
                Mathf.Clamp01(color.g + variation),
                Mathf.Clamp01(color.b + variation),
                1f);
        }

        private Mesh CreateMapMesh(GridMapLayout layout)
        {
            float halfWidth = layout.Width * tileSize * 0.5f;
            float halfHeight = layout.Height * tileSize * 0.5f;
            var mesh = new Mesh
            {
                name = "대형 평면 월드 메시"
            };
            mesh.vertices = new[]
            {
                new Vector3(-halfWidth, 0f, -halfHeight),
                new Vector3(-halfWidth, 0f, halfHeight),
                new Vector3(halfWidth, 0f, halfHeight),
                new Vector3(halfWidth, 0f, -halfHeight)
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f),
                new Vector2(0f, 1f),
                new Vector2(1f, 1f),
                new Vector2(1f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Material CreateMapMaterial(Texture texture)
        {
            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Texture") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");
            if (shader == null)
                throw new InvalidOperationException("맵 표시용 셰이더를 찾지 못했습니다.");

            var material = new Material(shader)
            {
                name = "대형 평면 월드 재질"
            };
            if (material.HasProperty("_BaseMap"))
                material.SetTexture("_BaseMap", texture);
            if (material.HasProperty("_MainTex"))
                material.SetTexture("_MainTex", texture);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", Color.white);
            return material;
        }

        private MapCellSelection DescribeCell(
            GridMapLayout layout,
            GridCoordinate coordinate)
        {
            if (coordinate.Equals(layout.PlayerStart))
            {
                return CreateSelection(
                    coordinate,
                    MapCellContent.PlayerBase,
                    "플레이어 본사",
                    "본사 방어, 건설, 창고 관리 명령을 연결할 수 있습니다.");
            }

            for (int i = 0; i < layout.OpponentStarts.Count; i++)
            {
                if (coordinate.Equals(layout.OpponentStarts[i]))
                {
                    MapCastleControlState capital =
                        _gameplayService?.FindCapital($"ai_{i + 1}");
                    return CreateSelection(
                        coordinate,
                        MapCellContent.EnemyBase,
                        $"경쟁 기업 {i + 1} 본사",
                        capital?.IsDestroyed == true
                            ? "이미 멸망한 수도입니다."
                            : "정찰·봉쇄·공격 미션 또는 수도 공성의 대상입니다.");
                }
            }

            MapMineControlState runtimeMine =
                _gameplayService?.FindMine(coordinate);
            MineKind? mineKind = runtimeMine?.Kind;
            if (!mineKind.HasValue)
            {
                for (int i = 0; i < layout.Mines.Count; i++)
                {
                    if (coordinate.Equals(layout.Mines[i].Coordinate))
                    {
                        mineKind = layout.Mines[i].Kind;
                        break;
                    }
                }
            }

            if (layout.IsNeutralCastle(coordinate))
            {
                MapCastleControlState castle =
                    _gameplayService?.FindCastle(coordinate);
                string ownerFactionId = castle?.OwnerFactionId ?? string.Empty;
                if (string.IsNullOrEmpty(ownerFactionId))
                {
                    return CreateSelection(
                        coordinate,
                        MapCellContent.NeutralCastle,
                        "주인 없는 빈 성",
                        "선택한 부대를 이동시키면 점령을 시작합니다. " +
                        "점령 중에는 부대가 성에 머물러야 합니다.");
                }

                string roleName = MapCastleRoleNames.GetKoreanName(
                    castle.Role);
                if (string.Equals(
                    ownerFactionId,
                    _gameplayService.PlayerFactionId,
                    StringComparison.Ordinal))
                {
                    return CreateSelection(
                        coordinate,
                        MapCellContent.PlayerCastle,
                        $"플레이어 {roleName}",
                        "소유한 성입니다. 역할을 선택하고 주둔군을 배치해 " +
                        "거점과 보급로를 방어할 수 있습니다.");
                }

                return CreateSelection(
                    coordinate,
                    MapCellContent.EnemyCastle,
                    $"{GetFactionDisplayName(ownerFactionId)} {roleName}",
                    castle.IsUnderSiege
                        ? "현재 공성 중인 적성입니다. 수비대가 남아 있으면 " +
                          "향후 전투 판정으로 제거해야 합니다."
                        : "적 세력이 점령한 성입니다. 부대를 이동시키면 " +
                          "공성 대상으로 전환됩니다.");
            }

            if (mineKind.HasValue)
            {
                return mineKind.Value == MineKind.Gold
                    ? CreateSelection(
                        coordinate,
                        MapCellContent.GoldMine,
                        "금광",
                        "점령 후 금을 채굴합니다. 채굴할수록 생산성이 감소합니다.")
                    : CreateSelection(
                        coordinate,
                        MapCellContent.NormalMine,
                        "철광산",
                        "점령 후 철을 채굴합니다. 채굴할수록 생산성이 감소합니다.");
            }

            GridTerrainKind terrain = layout.GetTerrain(coordinate);
            return CreateSelection(
                coordinate,
                MapCellContent.Empty,
                GetTerrainName(terrain),
                terrain == GridTerrainKind.Ocean
                    ? "해상 운송과 해상 임무용 지역입니다. 지상 시설은 건설할 수 없습니다."
                    : "공장, 창고, 전초기지 건설 후보지입니다.");
        }

        private MapCellSelection CreateSelection(
            GridCoordinate coordinate,
            MapCellContent content,
            string displayName,
            string interactionHint)
        {
            MapUnitState unit = null;
            MapMineControlState mine = null;
            MapCastleControlState castle = null;
            if (_gameplayService != null)
            {
                unit = _gameplayService.FindOwnedUnitAt(
                    _gameplayService.PlayerFactionId,
                    coordinate);
                if (unit == null)
                {
                    for (int i = 0; i < _gameplayService.Units.Count; i++)
                    {
                        if (_gameplayService.Units[i].Coordinate.Equals(coordinate))
                        {
                            unit = _gameplayService.Units[i];
                            break;
                        }
                    }
                }
                mine = _gameplayService.FindMine(coordinate);
                castle = _gameplayService.FindCastle(coordinate);
            }

            string detail = interactionHint;
            if (unit != null)
            {
                string ownerName = GetFactionDisplayName(unit.OwnerFactionId);
                detail += $"\n{ownerName} {unit.ArchetypeDisplayName}" +
                          $" · 병력 {unit.Soldiers:N0}" +
                          $" · {unit.WeaponDisplayName} / {unit.ArmorDisplayName}" +
                          $" · 편성 " +
                          MapUnitFormationPresetNames.GetKoreanName(
                              unit.Formation.Preset) +
                          $" · 체력 {unit.Stamina}/{unit.MaxStamina}" +
                          $" · 보급 {unit.SupplyRatio:P0}" +
                          $" (식량 {unit.FoodSupply:N1}, " +
                          $"무기 {unit.EquipmentSupply:N1}, " +
                          $"의약품 {unit.MedicineSupply:N1}" +
                          (unit.RequiredHorseCount > 0
                              ? $", 말 {unit.HorseSupply:N1}/" +
                                $"{unit.RequiredHorseCount:N0}"
                              : string.Empty) + ")" +
                          $" · 이동 보급 {unit.MovementSupplyModifier:P0}" +
                          $" / 공격 보급 {unit.AttackSupplyModifier:P0}" +
                          $" / 회복 보급 {unit.RecoverySupplyModifier:P0}";
                if (unit.Commander != null)
                {
                    detail += $" · 지휘관 {unit.Commander.DisplayName}" +
                              $" ({MapCommanderPersonalityNames.GetKoreanName(unit.Commander.Personality)}, " +
                              $"통솔 {unit.Commander.Command}, " +
                              $"전술 {unit.Commander.Tactics}, " +
                              $"병참 {unit.Commander.Logistics}, " +
                              $"충성 {unit.Commander.Loyalty})";
                }
                if (unit.Destination.HasValue)
                    detail += $" · 이동 중 → {unit.Destination.Value}";
                if (_gameplayService != null &&
                    _gameplayService.TryGetAttackTarget(
                        unit.Id,
                        out MapUnitState attackTarget))
                {
                    string targetName = attackTarget.Commander?.DisplayName ??
                        attackTarget.ArchetypeDisplayName;
                    detail += $" · 추적·공격 중 → {targetName} " +
                              $"({attackTarget.Coordinate})";
                }
                if (_gameplayService != null &&
                    _gameplayService.IsUsingSeaTransport(unit))
                {
                    detail += " · 해상 수송 중 (자동 승선·하선)";
                }
                if (unit.SupplyMissionKind != MapSupplyMissionKind.None)
                {
                    detail += " · " +
                        MapSupplyMissionNames.GetKoreanName(
                            unit.SupplyMissionKind) +
                        $" → {unit.SupplyMissionCoordinate}";
                }
            }
            if (mine != null)
            {
                string ownerName = string.IsNullOrEmpty(mine.OwnerFactionId)
                    ? "미점령"
                    : GetFactionDisplayName(mine.OwnerFactionId) + " 소유";
                detail += "\n광산 상태: " + ownerName +
                          $" · 생산성 {mine.YieldMultiplier:P0}";
                detail += mine.HasGuard
                    ? $" · 경비대 {mine.GuardUnitId} (1/1)"
                    : " · 경비대 없음 (0/1)";
                detail += " · 현지 징병 불가";
                if (mine.IsDynamic)
                    detail += $" · {mine.SpawnedEconomicDay}일 발견";
                if (!string.IsNullOrEmpty(mine.CapturingFactionId))
                {
                    detail += $" · {GetFactionDisplayName(mine.CapturingFactionId)} " +
                              $"점령 {mine.CaptureProgress}/" +
                              _gameplayService.FixedStepsToCapture;
                }
                if (!string.IsNullOrEmpty(mine.OwnerFactionId) &&
                    _gameplayService.TryFindNearestFriendlyCastleWarehouse(
                        mine.OwnerFactionId,
                        mine.Coordinate,
                        out MapCastleControlState destinationCastle,
                        out IReadOnlyList<GridCoordinate> transportRoute))
                {
                    detail += $" · 운송 → {destinationCastle.Coordinate}" +
                              $" ({transportRoute.Count}칸)";
                }
            }
            else if (_gameplayService != null)
            {
                MapMineConstructionState construction =
                    _gameplayService.FindMineConstruction(coordinate);
                MapEconomicSurveyState survey =
                    _gameplayService.FindEconomicSurvey(coordinate);
                if (construction != null)
                {
                    string mineName = construction.Kind == MineKind.Gold
                        ? "금광"
                        : "철광산";
                    detail += $"\n{mineName} 건설 중 · 남은 " +
                              $"{construction.RemainingDays}/" +
                              $"{construction.TotalDays}일 · " +
                              $"투입 비용 {construction.Cost:N0}원 · " +
                              $"예상 생산성 {construction.YieldMultiplier:P0}";
                }
                else if (survey != null)
                {
                    if (!survey.HasViableDeposit)
                    {
                        detail += "\n경제 탐사 완료 · 채굴 가치가 있는 " +
                                  "매장지를 찾지 못했습니다.";
                    }
                    else
                    {
                        MineKind kind = survey.DepositKind.Value;
                        string mineName = kind == MineKind.Gold
                            ? "금광"
                            : "철광산";
                        detail += $"\n경제 탐사 완료 · {mineName} 후보지 · " +
                                  $"예상 생산성 {survey.YieldMultiplier:P0} · " +
                                  $"건설비 {MapEconomicDevelopmentRules.GetConstructionCost(kind):N0}원 · " +
                                  $"공기 {MapEconomicDevelopmentRules.GetConstructionDays(kind)}일";
                    }
                }
            }
            if (castle?.IsCapital == true)
            {
                string capitalOwner = castle.IsDestroyed
                    ? "멸망"
                    : GetFactionDisplayName(castle.OwnerFactionId) + " 소유";
                detail += "\n수도 상태: " + capitalOwner +
                          $" · 성벽 {castle.WallDurability:N0}/" +
                          $"{castle.MaxWallDurability:N0}" +
                          $" · 식량 {castle.FoodSupply:N0}/" +
                          $"{castle.MaxFoodSupply:N0}" +
                          $" · 창고 철광석 {castle.WarehouseIronAmount:N1}" +
                          $" · 보급품 식량 {castle.WarehouseFoodAmount:N1}" +
                          $" / 무기 {castle.WarehouseEquipmentAmount:N1}" +
                          $" / 의약품 {castle.WarehouseMedicineAmount:N1}" +
                          $" / 말 {castle.WarehouseHorseAmount:N1}" +
                          $" · 방어 보너스 +{castle.DefenseBonus:P0}";
            }
            if (castle != null &&
                _gameplayService != null &&
                _gameplayService.IsCoastalPort(castle.Coordinate))
            {
                detail += "\n해안 성 · 간이 항구 사용 가능";
            }
            if (castle != null && !castle.IsCapital)
            {
                string ownerName = string.IsNullOrEmpty(castle.OwnerFactionId)
                    ? "중립"
                    : GetFactionDisplayName(castle.OwnerFactionId) + " 소유";
                detail += "\n성 상태: " + ownerName +
                          " · 역할 " +
                          MapCastleRoleNames.GetKoreanName(castle.Role) +
                          $" · 주둔군 {castle.GarrisonUnitCount}/" +
                          MapCastleRules.GetGarrisonCapacity(castle.Role) +
                          $" · 성벽 {castle.WallDurability:N0}/" +
                          $"{castle.MaxWallDurability:N0}" +
                          $" · 식량 {castle.FoodSupply:N0}/" +
                          $"{castle.MaxFoodSupply:N0}" +
                          $" · 창고 철광석 {castle.WarehouseIronAmount:N1}" +
                          $" · 보급품 식량 {castle.WarehouseFoodAmount:N1}" +
                          $" / 무기 {castle.WarehouseEquipmentAmount:N1}" +
                          $" / 의약품 {castle.WarehouseMedicineAmount:N1}" +
                          $" / 말 {castle.WarehouseHorseAmount:N1}" +
                          $" · 방어 보너스 +{castle.DefenseBonus:P0}" +
                          $" · 점령 정책 " +
                          MapOccupationPolicyNames.GetKoreanName(
                              castle.OccupationPolicy) +
                          $" · 치안 {castle.PublicOrder}";
                if (_gameplayService.TryGetRecruitmentSiteSnapshot(
                    _gameplayService.PlayerFactionId,
                    coordinate,
                    out MapRecruitmentSiteSnapshot castleRecruitment))
                {
                    detail += $" · 징집 인력 " +
                              $"{castleRecruitment.AvailableRecruits}/" +
                              castleRecruitment.RecruitmentCapacity;
                }
                if (castle.ConflictKind != MapCastleConflictKind.None)
                {
                    string conflictName = castle.IsUnderSiege
                        ? "공성"
                        : "점령";
                    string attacker = string.IsNullOrEmpty(
                        castle.CapturingFactionId)
                        ? "여러 세력 경합"
                        : GetFactionDisplayName(castle.CapturingFactionId);
                    detail += $" · {conflictName} {attacker} " +
                              $"{castle.CaptureProgress}/" +
                              _gameplayService.GetCastleCaptureRequired(castle);
                    if (castle.IsUnderSiege)
                    {
                        detail += " · 행동 " +
                            MapSiegeActionNames.GetKoreanName(
                                castle.SiegeAction);
                    }
                }
            }
            if (content == MapCellContent.PlayerBase &&
                _gameplayService != null &&
                _gameplayService.TryGetRecruitmentSiteSnapshot(
                    _gameplayService.PlayerFactionId,
                    coordinate,
                    out MapRecruitmentSiteSnapshot headquartersRecruitment))
            {
                detail += $"\n본사 주둔군 " +
                          $"{headquartersRecruitment.GarrisonUnitCount}/" +
                          headquartersRecruitment.GarrisonCapacity +
                          $" · 징집 인력 " +
                          $"{headquartersRecruitment.AvailableRecruits}/" +
                          headquartersRecruitment.RecruitmentCapacity +
                          " · 하루마다 1명분 회복";
            }
            if (_gameplayService != null &&
                _gameplayService.TryGetPendingSupplyRouteOwnerAt(
                    coordinate,
                    out string supplyRouteOwner))
            {
                bool friendlyRoute = string.Equals(
                    supplyRouteOwner,
                    _gameplayService.PlayerFactionId,
                    StringComparison.Ordinal);
                detail += friendlyRoute
                    ? "\n아군 예약 수송 경로 · 우클릭으로 호위 임무 지정"
                    : "\n적 예약 수송 경로 · 우클릭으로 습격·봉쇄 임무 지정";
            }

            string capturingFactionId = castle?.CapturingFactionId ??
                mine?.CapturingFactionId;
            int captureProgress = castle?.CaptureProgress ??
                mine?.CaptureProgress ?? 0;
            int captureRequired = castle != null
                ? _gameplayService.GetCastleCaptureRequired(castle)
                : _gameplayService?.FixedStepsToCapture ?? 0;

            return new MapCellSelection(
                coordinate,
                content,
                displayName,
                detail,
                unit?.Id,
                unit?.OwnerFactionId,
                mine?.OwnerFactionId,
                capturingFactionId,
                captureProgress,
                captureRequired,
                castle?.OwnerFactionId,
                castle?.Role ?? MapCastleRole.Unassigned,
                castle?.ConflictKind ?? MapCastleConflictKind.None,
                castle?.GarrisonUnitCount ?? 0);
        }

        private string GetFactionDisplayName(string factionId)
        {
            if (string.Equals(
                    factionId,
                    _gameplayService?.PlayerFactionId,
                    StringComparison.Ordinal) ||
                (_gameplayService == null && string.Equals(
                    factionId,
                    "player",
                    StringComparison.Ordinal)))
            {
                return "플레이어";
            }
            if (factionId != null && factionId.StartsWith("ai_", StringComparison.Ordinal))
                return "경쟁 기업 " + factionId.Substring(3);
            IReadOnlyList<string> opponents = _gameplayService?.AiFactionIds;
            if (opponents != null)
            {
                for (int i = 0; i < opponents.Count; i++)
                {
                    if (string.Equals(
                            factionId,
                            opponents[i],
                            StringComparison.Ordinal))
                    {
                        return $"경쟁 세력 {i + 1}";
                    }
                }
            }
            return string.IsNullOrWhiteSpace(factionId) ? "중립" : factionId;
        }

        private static string GetTerrainName(GridTerrainKind terrain)
        {
            switch (terrain)
            {
                case GridTerrainKind.Ocean: return "바다";
                case GridTerrainKind.Forest: return "숲";
                case GridTerrainKind.Desert: return "사막";
                case GridTerrainKind.Hills: return "구릉";
                case GridTerrainKind.Tundra: return "툰드라";
                default: return "평원";
            }
        }

        private void BuildPlayerStart(GridCoordinate coordinate)
        {
            ForEachSurfaceCopy(xOffset => CreateCastle(
                $"플레이어 성_{coordinate.X}_{coordinate.Y}",
                ToWorldPosition(coordinate, xOffset),
                playerFactionColor));
        }

        private void BuildOpponentStarts(
            IReadOnlyList<GridCoordinate> opponentStarts)
        {
            for (int i = 0; i < opponentStarts.Count; i++)
            {
                int opponentIndex = i;
                GridCoordinate coordinate = opponentStarts[i];
                Color color = GetEnemyFactionColor(i);
                ForEachSurfaceCopy(xOffset => CreateCastle(
                    $"경쟁 기업 {opponentIndex + 1} 성_{coordinate.X}_{coordinate.Y}",
                    ToWorldPosition(coordinate, xOffset),
                    color));
            }
        }

        private void BuildNeutralCastles(
            IReadOnlyList<GridCoordinate> neutralCastles)
        {
            for (int i = 0; i < neutralCastles.Count; i++)
            {
                int castleIndex = i;
                GridCoordinate coordinate = neutralCastles[i];
                ForEachSurfaceCopy(xOffset => CreateCastle(
                    $"빈 성 {castleIndex + 1}_{coordinate.X}_{coordinate.Y}",
                    ToWorldPosition(coordinate, xOffset),
                    neutralFactionColor,
                    showBanner: false));
            }
        }

        private void CreateCastle(
            string name,
            Vector3 position,
            Color color,
            bool showBanner = true)
        {
            // Castle architecture and ownership are separate visual layers.
            // The wide floor plate remains visible even when zoomed out and
            // makes each faction readable without inspecting the castle.
            CreateSiteFloorPlate(
                name + "_세력 바닥",
                position,
                color,
                tileSize * 1.08f);

            Color wallColor = Color.Lerp(color, Color.black, 0.18f);
            CreateBlock(
                name + "_성벽",
                position + new Vector3(0f, 0.16f, 0f),
                new Vector3(tileSize * 0.82f, 0.26f, tileSize * 0.82f),
                wallColor,
                _generatedRoot,
                false);
            CreateBlock(
                name + "_중앙성채",
                position + new Vector3(0f, 0.58f, 0f),
                new Vector3(tileSize * 0.46f, 0.72f, tileSize * 0.46f),
                wallColor,
                _generatedRoot,
                false);

            float towerOffset = tileSize * 0.32f;
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    CreateBlock(
                        name + $"_망루_{x}_{z}",
                        position + new Vector3(
                            x * towerOffset,
                            0.53f,
                            z * towerOffset),
                        new Vector3(
                            tileSize * 0.20f,
                            0.68f,
                            tileSize * 0.20f),
                        color,
                        _generatedRoot,
                        false);
                }
            }

            if (showBanner)
            {
                Color accent = Color.Lerp(color, Color.white, 0.45f);
                CreateBlock(
                    name + "_깃대",
                    position + new Vector3(0f, 1.24f, 0f),
                    new Vector3(tileSize * 0.04f, 0.62f, tileSize * 0.04f),
                    accent,
                    _generatedRoot,
                    false);
                CreateBlock(
                    name + "_깃발",
                    position + new Vector3(tileSize * 0.13f, 1.43f, 0f),
                    new Vector3(tileSize * 0.24f, 0.16f, tileSize * 0.04f),
                    color,
                    _generatedRoot,
                    false);
            }
        }

        private void BuildMines(GridMapLayout layout)
        {
            for (int i = 0; i < layout.Mines.Count; i++)
                BuildMineVisual(layout.Mines[i]);
        }

        private void BuildMineVisual(MinePlacement mine)
        {
            bool isGold = mine.Kind == MineKind.Gold;
            string mineName = isGold ? "금광" : "철광산";
            Color mineColor = isGold
                ? goldMineStructureColor
                : ironMineStructureColor;
            ForEachSurfaceCopy(xOffset =>
            {
                Vector3 position = ToWorldPosition(
                    mine.Coordinate,
                    xOffset);
                CreateBlock(
                    $"{mineName}_{mine.Coordinate.X}_{mine.Coordinate.Y}",
                    position + new Vector3(0f, 0.22f, 0f),
                    new Vector3(
                        tileSize * 0.52f,
                        0.32f,
                        tileSize * 0.52f),
                    mineColor,
                    _generatedRoot,
                    false);
                CreateMineIcon(
                    mineName + " 아이콘",
                    position + new Vector3(0f, 0.92f, 0f),
                    isGold ? _goldMineSprite : _normalMineSprite);
            });
        }

        private void RefreshGameplayMarkers()
        {
            if (_generatedRoot == null || _gameplayService == null)
                return;

            if (_gameplayMarkerRoot != null)
            {
                _iconBillboards.RemoveAll(icon =>
                    icon == null || icon.IsChildOf(_gameplayMarkerRoot));
                GameObject previous = _gameplayMarkerRoot.gameObject;
                _gameplayMarkerRoot = null;
                if (UnityEngine.Application.isPlaying)
                    Destroy(previous);
                else
                    DestroyImmediate(previous);
            }
            _unitMarkerRoots.Clear();

            var markerRoot = new GameObject("실시간 유닛과 점령 표식");
            markerRoot.transform.SetParent(_generatedRoot, false);
            _gameplayMarkerRoot = markerRoot.transform;

            for (int i = 0; i < _gameplayService.Castles.Count; i++)
            {
                MapCastleControlState castle = _gameplayService.Castles[i];
                if (castle.IsCapital)
                    continue;
                ForEachSurfaceCopy(xOffset => CreateCastleControlMarker(
                    castle,
                    ToWorldPosition(castle.Coordinate, xOffset)));
            }

            for (int i = 0; i < _gameplayService.Mines.Count; i++)
            {
                MapMineControlState mine = _gameplayService.Mines[i];
                Color color = GetMineControlColor(mine);
                ForEachSurfaceCopy(xOffset => CreateBlock(
                    $"광산 소유권_{mine.Coordinate.X}_{mine.Coordinate.Y}",
                    ToWorldPosition(mine.Coordinate, xOffset) +
                    new Vector3(0f, 0.045f, 0f),
                    new Vector3(
                        tileSize * 0.78f,
                        0.07f,
                        tileSize * 0.78f),
                    color,
                    _gameplayMarkerRoot,
                    false));
            }

            for (int i = 0; i < _gameplayService.MineConstructions.Count; i++)
            {
                MapMineConstructionState construction =
                    _gameplayService.MineConstructions[i];
                ForEachSurfaceCopy(xOffset => CreateBlock(
                    $"채굴소 건설_{construction.Coordinate.X}_{construction.Coordinate.Y}",
                    ToWorldPosition(construction.Coordinate, xOffset) +
                    new Vector3(0f, 0.16f, 0f),
                    new Vector3(
                        tileSize * 0.58f,
                        0.24f,
                        tileSize * 0.58f),
                    new Color(0.95f, 0.58f, 0.12f, 1f),
                    _gameplayMarkerRoot,
                    false));
            }

            for (int i = 0; i < _gameplayService.Units.Count; i++)
            {
                MapUnitState unit = _gameplayService.Units[i];
                Color color = GetFactionColor(unit.OwnerFactionId);
                bool selected = string.Equals(
                    unit.Id,
                    _selectedPlayerUnitId,
                    StringComparison.Ordinal) || string.Equals(
                    unit.Id,
                    _trackedEnemyUnitId,
                    StringComparison.Ordinal);
                if (unit.IsMoving &&
                    unit.PlannedPath.Count > 1 &&
                    _gameplayService.CanViewMovementPath(
                        _gameplayService.PlayerFactionId,
                        unit))
                {
                    CreateMovementPath(unit, color);
                }

                ForEachSurfaceCopy(xOffset =>
                {
                    Vector3 position = ToWorldPosition(unit.Coordinate, xOffset);
                    var unitRootObject = new GameObject(
                        $"{unit.Id}_화면표식_{xOffset:F1}");
                    unitRootObject.transform.SetParent(
                        _gameplayMarkerRoot,
                        false);
                    RegisterUnitMarkerRoot(
                        unit.Id,
                        unitRootObject.transform);
                    CreateUnitMarker(
                        unit,
                        position,
                        color,
                        selected,
                        unitRootObject.transform);
                });
            }

            UpdateUnitMarkerInterpolation();
        }

        private void RegisterUnitMarkerRoot(
            string unitId,
            Transform markerRoot)
        {
            if (!_unitMarkerRoots.TryGetValue(
                unitId,
                out List<Transform> roots))
            {
                roots = new List<Transform>(SurfaceCopyRadius * 2 + 1);
                _unitMarkerRoots.Add(unitId, roots);
            }
            roots.Add(markerRoot);
        }

        private void UpdateUnitMarkerInterpolation()
        {
            if (_gameplayService == null || CurrentLayout == null)
                return;

            float smoothing = 1f - Mathf.Exp(
                -movementInterpolationSharpness *
                Mathf.Max(0f, Time.unscaledDeltaTime));
            float worldWidth = CurrentLayout.Width * tileSize;
            for (int i = 0; i < _gameplayService.Units.Count; i++)
            {
                MapUnitState unit = _gameplayService.Units[i];
                Vector3 basePosition = ToWorldPosition(unit.Coordinate, 0f);
                Vector3 desiredPosition = basePosition;
                if (_gameplayService.TryGetMovementSegment(
                    unit,
                    out GridCoordinate from,
                    out GridCoordinate to,
                    out double progress))
                {
                    Vector3 fromPosition = ToWorldPosition(from, 0f);
                    int deltaX = to.X - from.X;
                    int wrapThreshold = CurrentLayout.Width / 2;
                    if (deltaX > wrapThreshold)
                        deltaX -= CurrentLayout.Width;
                    else if (deltaX < -wrapThreshold)
                        deltaX += CurrentLayout.Width;

                    Vector3 targetPosition = ToWorldPosition(to, 0f);
                    targetPosition.x = fromPosition.x + deltaX * tileSize;
                    desiredPosition = Vector3.Lerp(
                        fromPosition,
                        targetPosition,
                        Mathf.Clamp01((float)progress));
                }

                if (!_unitVisualPositions.TryGetValue(
                    unit.Id,
                    out Vector3 visualPosition))
                {
                    visualPosition = desiredPosition;
                }
                else
                {
                    float xDistance = visualPosition.x - basePosition.x;
                    if (xDistance > worldWidth * 0.5f)
                        visualPosition.x -= worldWidth;
                    else if (xDistance < -worldWidth * 0.5f)
                        visualPosition.x += worldWidth;

                    visualPosition = Vector3.Lerp(
                        visualPosition,
                        desiredPosition,
                        smoothing);
                }

                _unitVisualPositions[unit.Id] = visualPosition;
                if (!_unitMarkerRoots.TryGetValue(
                    unit.Id,
                    out List<Transform> roots))
                {
                    continue;
                }

                Vector3 offset = visualPosition - basePosition;
                for (int rootIndex = 0; rootIndex < roots.Count; rootIndex++)
                {
                    if (roots[rootIndex] != null)
                        roots[rootIndex].localPosition = offset;
                }
            }
        }

        private void CreateMovementPath(MapUnitState unit, Color factionColor)
        {
            CreateMovementPath(
                unit.Id,
                unit.PlannedPath,
                Color.Lerp(factionColor, Color.white, 0.35f),
                movementPathWidth,
                movementPathAlpha);
        }

        private void CreateMovementPath(
            string pathId,
            IReadOnlyList<GridCoordinate> path,
            Color color,
            float width,
            float alpha)
        {
            ForEachSurfaceCopy(xOffset => CreateMovementPathCopy(
                pathId,
                path,
                color,
                width,
                alpha,
                xOffset));
        }

        private void CreateMovementPathCopy(
            string pathId,
            IReadOnlyList<GridCoordinate> path,
            Color color,
            float width,
            float alpha,
            float xOffset)
        {
            if (path.Count < 2 || CurrentLayout == null)
                return;

            var positions = new Vector3[path.Count];
            Vector3 first = ToWorldPosition(path[0], xOffset);
            positions[0] = new Vector3(
                first.x,
                movementPathHeight,
                first.z);

            float unwrappedX = first.x;
            int previousX = path[0].X;
            int wrapThreshold = CurrentLayout.Width / 2;
            for (int i = 1; i < path.Count; i++)
            {
                int deltaX = path[i].X - previousX;
                if (deltaX > wrapThreshold)
                    deltaX -= CurrentLayout.Width;
                else if (deltaX < -wrapThreshold)
                    deltaX += CurrentLayout.Width;

                unwrappedX += deltaX * tileSize;
                Vector3 tilePosition = ToWorldPosition(path[i], xOffset);
                positions[i] = new Vector3(
                    unwrappedX,
                    movementPathHeight,
                    tilePosition.z);
                previousX = path[i].X;
            }

            var pathObject = new GameObject(pathId + "_이동 점선");
            pathObject.transform.SetParent(_gameplayMarkerRoot, false);
            var line = pathObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.widthMultiplier = tileSize * width;
            line.numCornerVertices = 2;
            line.numCapVertices = 2;
            line.sharedMaterial = GetOrCreateMovementPathMaterial();

            Color pathColor = color;
            pathColor.a = alpha;
            line.startColor = pathColor;
            line.endColor = pathColor;
            line.positionCount = positions.Length;
            line.SetPositions(positions);
        }

        private void CreateCastleControlMarker(
            MapCastleControlState castle,
            Vector3 position)
        {
            if (castle.IsNeutral &&
                castle.ConflictKind == MapCastleConflictKind.None)
            {
                return;
            }

            Color controlColor = GetCastleControlColor(castle);
            CreateBlock(
                $"성 소유권_{castle.Coordinate.X}_{castle.Coordinate.Y}",
                position + new Vector3(0f, 0.12f, 0f),
                new Vector3(tileSize * 1.13f, 0.08f, tileSize * 1.13f),
                controlColor,
                _gameplayMarkerRoot,
                false);

            if (string.IsNullOrEmpty(castle.OwnerFactionId))
                return;

            Color ownerColor = GetFactionColor(castle.OwnerFactionId);
            Color accent = Color.Lerp(ownerColor, Color.white, 0.45f);
            CreateBlock(
                $"성 깃대_{castle.Coordinate.X}_{castle.Coordinate.Y}",
                position + new Vector3(0f, 1.24f, 0f),
                new Vector3(tileSize * 0.04f, 0.62f, tileSize * 0.04f),
                accent,
                _gameplayMarkerRoot,
                false);
            CreateBlock(
                $"성 깃발_{castle.Coordinate.X}_{castle.Coordinate.Y}",
                position + new Vector3(tileSize * 0.13f, 1.43f, 0f),
                new Vector3(tileSize * 0.24f, 0.16f, tileSize * 0.04f),
                ownerColor,
                _gameplayMarkerRoot,
                false);

            Vector3 roleScale;
            switch (castle.Role)
            {
                case MapCastleRole.SupplyHub:
                    roleScale = new Vector3(
                        tileSize * 0.34f,
                        0.10f,
                        tileSize * 0.34f);
                    break;
                case MapCastleRole.IndustrialCity:
                    roleScale = new Vector3(
                        tileSize * 0.28f,
                        0.30f,
                        tileSize * 0.28f);
                    break;
                case MapCastleRole.MilitaryFortress:
                    roleScale = new Vector3(
                        tileSize * 0.42f,
                        0.18f,
                        tileSize * 0.42f);
                    break;
                case MapCastleRole.Port:
                    roleScale = new Vector3(
                        tileSize * 0.52f,
                        0.09f,
                        tileSize * 0.20f);
                    break;
                default:
                    return;
            }

            CreateBlock(
                $"성 역할_{MapCastleRoleNames.GetKoreanName(castle.Role)}_" +
                $"{castle.Coordinate.X}_{castle.Coordinate.Y}",
                position + new Vector3(0f, 1.02f, -tileSize * 0.25f),
                roleScale,
                accent,
                _gameplayMarkerRoot,
                false);
        }

        private void CreateUnitMarker(
            MapUnitState unit,
            Vector3 position,
            Color color,
            bool selected,
            Transform markerRoot)
        {
            bool isPlayerUnit = string.Equals(
                unit.OwnerFactionId,
                _gameplayService?.PlayerFactionId,
                StringComparison.Ordinal);
            position += Vector3.up * GetUnitSiteElevation(unit.Coordinate);

            float width = 0.38f;
            float height = 0.52f;
            float depth = 0.38f;
            switch (unit.Archetype)
            {
                case UnitArchetype.Spearman:
                    width = 0.30f;
                    height = 0.62f;
                    depth = 0.30f;
                    break;
                case UnitArchetype.Maceman:
                    width = 0.48f;
                    height = 0.54f;
                    depth = 0.44f;
                    break;
                case UnitArchetype.Archer:
                    width = 0.42f;
                    height = 0.46f;
                    depth = 0.30f;
                    break;
                case UnitArchetype.Slinger:
                    width = 0.32f;
                    height = 0.44f;
                    depth = 0.32f;
                    break;
                case UnitArchetype.Cavalry:
                    width = 0.62f;
                    height = 0.44f;
                    depth = 0.34f;
                    break;
            }

            float selectionScale = selected ? 1.16f : 1f;
            float ownershipScale = isPlayerUnit
                ? Mathf.Max(1f, playerUnitVisualScale)
                : 1f;
            float visualScale = selectionScale * ownershipScale;
            if (isPlayerUnit)
            {
                CreateBlock(
                    unit.Id + "_아군식별표시",
                    position + new Vector3(0f, 0.36f, 0f),
                    new Vector3(
                        tileSize * 0.88f,
                        0.075f,
                        tileSize * 0.88f),
                    playerUnitHighlightColor,
                    markerRoot,
                    false);
            }
            if (selected)
            {
                CreateBlock(
                    unit.Id + "_선택표시",
                    position + new Vector3(0f, 0.40f, 0f),
                    new Vector3(tileSize * 0.68f, 0.065f, tileSize * 0.68f),
                    Color.white,
                    markerRoot,
                    false);
            }

            CreateBlock(
                $"{unit.Id}_{unit.ArchetypeDisplayName}",
                position + new Vector3(0f, 0.72f, 0f),
                new Vector3(
                    tileSize * width * visualScale,
                    height * visualScale,
                    tileSize * depth * visualScale),
                color,
                markerRoot,
                false);

            if (unit.ArmorClass != ArmorClass.Unarmored)
            {
                bool heavyArmor = unit.ArmorClass == ArmorClass.Heavy;
                Color armorColor = heavyArmor
                    ? new Color(0.30f, 0.34f, 0.40f)
                    : new Color(0.58f, 0.63f, 0.70f);
                CreateBlock(
                    unit.Id + "_갑옷",
                    position + new Vector3(0f, 0.78f, 0f),
                    new Vector3(
                        tileSize * width * (heavyArmor ? 1.12f : 1.03f),
                        heavyArmor ? 0.22f : 0.12f,
                        tileSize * depth * (heavyArmor ? 1.12f : 1.03f)) *
                    ownershipScale,
                    armorColor,
                    markerRoot,
                    false);
            }

            Color accent = selected
                ? Color.white
                : Color.Lerp(color, Color.white, 0.55f);
            Vector3 accentPosition = position + new Vector3(0f, 1.05f, 0f);
            Vector3 accentScale;
            switch (unit.WeaponType)
            {
                case UnitWeaponType.Spear:
                    accentPosition += new Vector3(tileSize * 0.18f, 0.08f, 0f);
                    accentScale = new Vector3(tileSize * 0.045f, 0.90f, tileSize * 0.045f);
                    break;
                case UnitWeaponType.Mace:
                    accentPosition += new Vector3(tileSize * 0.18f, 0.02f, 0f);
                    accentScale = new Vector3(tileSize * 0.20f, 0.22f, tileSize * 0.20f);
                    break;
                case UnitWeaponType.Bow:
                    accentScale = new Vector3(tileSize * 0.56f, 0.07f, tileSize * 0.07f);
                    break;
                case UnitWeaponType.Sling:
                    accentPosition += new Vector3(tileSize * 0.20f, 0f, 0f);
                    accentScale = new Vector3(tileSize * 0.16f, 0.16f, tileSize * 0.16f);
                    break;
                case UnitWeaponType.Lance:
                    accentPosition += new Vector3(tileSize * 0.20f, 0f, 0f);
                    accentScale = new Vector3(tileSize * 0.54f, 0.08f, tileSize * 0.08f);
                    break;
                default:
                    accentPosition += new Vector3(tileSize * 0.17f, 0.02f, 0f);
                    accentScale = new Vector3(tileSize * 0.06f, 0.62f, tileSize * 0.06f);
                    break;
            }

            CreateBlock(
                unit.Id + "_병종표식",
                accentPosition,
                accentScale,
                accent,
                markerRoot,
                false);

            if (isPlayerUnit)
            {
                CreateBlock(
                    unit.Id + "_아군위치표식",
                    position + new Vector3(0f, 1.52f, 0f),
                    new Vector3(
                        tileSize * 0.34f,
                        0.09f,
                        tileSize * 0.34f),
                    playerUnitHighlightColor,
                    markerRoot,
                    false);
            }

            if (unit.Commander != null)
            {
                CreateCommanderPortrait(
                    unit,
                    position,
                    markerRoot);
            }
        }

        private void CreateCommanderPortrait(
            MapUnitState unit,
            Vector3 position,
            Transform markerRoot)
        {
            MapCommanderState commander = unit.Commander;
            Sprite portrait = commander.IsProtagonist
                ? _protagonistCommanderSprite
                : _aiCommanderSprite;
            if (portrait == null ||
                _commanderSquareSprite == null ||
                _commanderArrowSprite == null)
            {
                return;
            }

            var badgeObject = new GameObject(
                $"{unit.Id}_{commander.DisplayName}_장수초상");
            badgeObject.transform.SetParent(markerRoot, false);
            badgeObject.transform.position = position +
                new Vector3(
                    0f,
                    commanderPortraitHeight +
                    (commander.IsProtagonist ? 0.14f : 0f),
                    0f);

            float badgeWidth = commander.IsProtagonist
                ? protagonistPortraitWidth
                : aiCommanderPortraitWidth;
            Color factionColor = GetFactionColor(unit.OwnerFactionId);
            Color innerColor = new Color(0.035f, 0.045f, 0.06f, 1f);
            string badgeName = badgeObject.name;

            CreateCommanderBadgeLayer(
                badgeName + "_세력색사각형",
                badgeObject.transform,
                _commanderSquareSprite,
                factionColor,
                badgeWidth,
                Vector3.zero,
                55);
            CreateCommanderBadgeLayer(
                badgeName + "_안쪽사각형",
                badgeObject.transform,
                _commanderSquareSprite,
                innerColor,
                badgeWidth * 0.86f,
                Vector3.zero,
                56);
            CreateCommanderBadgeLayer(
                badgeName + "_초상",
                badgeObject.transform,
                portrait,
                Color.white,
                badgeWidth * 0.75f,
                Vector3.zero,
                57);
            CreateCommanderBadgeLayer(
                badgeName + "_아래화살표",
                badgeObject.transform,
                _commanderArrowSprite,
                factionColor,
                badgeWidth * 0.48f,
                new Vector3(
                    0f,
                    -tileSize * badgeWidth * 0.64f,
                    0f),
                58);

            _iconBillboards.Add(badgeObject.transform);
        }

        private static void CreateCommanderBadgeLayer(
            string objectName,
            Transform parent,
            Sprite sprite,
            Color color,
            float scale,
            Vector3 localPosition,
            int sortingOrder)
        {
            var layerObject = new GameObject(objectName);
            layerObject.transform.SetParent(parent, false);
            layerObject.transform.localPosition = localPosition;
            layerObject.transform.localScale = new Vector3(scale, scale, 1f);
            var renderer = layerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
        }

        private float GetUnitSiteElevation(GridCoordinate coordinate)
        {
            if (_gameplayService?.FindCastle(coordinate) != null)
                return Mathf.Max(0f, castleUnitElevation);
            if (_gameplayService?.FindMine(coordinate) != null)
                return Mathf.Max(0f, mineUnitElevation);
            return 0f;
        }

        private Color GetMineControlColor(MapMineControlState mine)
        {
            Color currentColor = string.IsNullOrEmpty(mine.OwnerFactionId)
                ? mine.Kind == MineKind.Gold
                    ? unclaimedGoldMineFloorColor
                    : unclaimedIronMineFloorColor
                : GetFactionColor(mine.OwnerFactionId);
            if (string.IsNullOrEmpty(mine.CapturingFactionId))
                return currentColor;

            float progress = Mathf.Clamp01(
                mine.CaptureProgress /
                Mathf.Max(1f, _gameplayService.FixedStepsToCapture));
            return Color.Lerp(
                currentColor,
                GetFactionColor(mine.CapturingFactionId),
                0.35f + progress * 0.65f);
        }

        private Color GetCastleControlColor(MapCastleControlState castle)
        {
            Color currentColor = string.IsNullOrEmpty(castle.OwnerFactionId)
                ? neutralFactionColor
                : GetFactionColor(castle.OwnerFactionId);
            if (string.IsNullOrEmpty(castle.CapturingFactionId))
                return currentColor;

            float progress = Mathf.Clamp01(
                castle.CaptureProgress /
                Mathf.Max(
                    1f,
                    _gameplayService.GetCastleCaptureRequired(castle)));
            return Color.Lerp(
                currentColor,
                GetFactionColor(castle.CapturingFactionId),
                0.30f + progress * 0.70f);
        }

        private Color GetFactionColor(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
                return neutralFactionColor;
            if (string.Equals(
                    factionId,
                    _gameplayService?.PlayerFactionId,
                    StringComparison.Ordinal) ||
                (_gameplayService == null && string.Equals(
                    factionId,
                    "player",
                    StringComparison.Ordinal)))
            {
                return playerFactionColor;
            }

            IReadOnlyList<string> opponents = _gameplayService?.AiFactionIds;
            if (opponents != null)
            {
                for (int i = 0; i < opponents.Count; i++)
                {
                    if (string.Equals(
                            factionId,
                            opponents[i],
                            StringComparison.Ordinal))
                    {
                        return GetEnemyFactionColor(i);
                    }
                }
            }

            if (factionId.StartsWith("ai_", StringComparison.Ordinal) &&
                int.TryParse(factionId.Substring(3), out int aiNumber))
            {
                return GetEnemyFactionColor(Math.Max(0, aiNumber - 1));
            }

            uint stableHash = 2166136261u;
            for (int i = 0; i < factionId.Length; i++)
            {
                stableHash ^= factionId[i];
                stableHash *= 16777619u;
            }
            return GetEnemyFactionColor((int)(stableHash & 0x7fffffffu));
        }

        private Color GetEnemyFactionColor(int enemyIndex)
        {
            if (enemyFactionColors == null || enemyFactionColors.Length == 0)
            {
                return new Color(0.92f, 0.10f, 0.12f, 1f);
            }

            int safeIndex = PositiveModulo(
                enemyIndex,
                enemyFactionColors.Length);
            return enemyFactionColors[safeIndex];
        }

        private void CreateSiteFloorPlate(
            string objectName,
            Vector3 position,
            Color color,
            float size)
        {
            CreateBlock(
                objectName,
                position + new Vector3(0f, 0.055f, 0f),
                new Vector3(size, 0.09f, size),
                color,
                _generatedRoot,
                false);
        }

        private void HandleGameplayStateChanged()
        {
            if (SelectedPlayerUnit == null)
                _selectedPlayerUnitId = string.Empty;
            RefreshGameplayMarkers();
            RefreshCurrentSelection();
            GameplayStateChanged?.Invoke();
        }

        private void HandleMineCaptured(MapMineCaptureRecord record)
        {
            MineCaptured?.Invoke(record);
        }

        private void HandleMineSpawned(MapMineSpawnRecord record)
        {
            BuildMineVisual(new MinePlacement(record.Coordinate, record.Kind));
            RefreshCurrentSelection();
            MineSpawned?.Invoke(record);
        }

        private void HandleMineConstructionCompleted(
            MapMineConstructionCompletedRecord record)
        {
            RefreshCurrentSelection();
            MineConstructionCompleted?.Invoke(record);
        }

        private void HandleCastleCaptured(MapCastleCaptureRecord record)
        {
            RefreshCurrentSelection();
            CastleCaptured?.Invoke(record);
        }

        private void HandleCapitalDestroyed(MapCapitalDestroyedRecord record)
        {
            RefreshCurrentSelection();
            CapitalDestroyed?.Invoke(record);
        }

        private void HandleCastleRoleChanged(MapCastleRoleChangedRecord record)
        {
            RefreshCurrentSelection();
            CastleRoleChanged?.Invoke(record);
        }

        private void HandleSiegeDayResolved(MapSiegeDayResult result)
        {
            RefreshCurrentSelection();
            SiegeDayResolved?.Invoke(result);
        }

        private void HandleFieldBattleResolved(MapFieldBattleResult result)
        {
            RefreshGameplayMarkers();
            RefreshCurrentSelection();
            FieldBattleResolved?.Invoke(result);
        }

        private void HandleCommanderGenerated(
            MapCommanderGeneratedRecord record)
        {
            CommanderGenerated?.Invoke(record);
        }

        private void HandleCommanderDied(MapCommanderDeathRecord record)
        {
            CommanderDied?.Invoke(record);
        }

        private void HandleSupplyInterdictionResolved(
            MapSupplyInterdictionResult result)
        {
            RefreshCurrentSelection();
            SupplyInterdictionResolved?.Invoke(result);
        }

        private void RefreshCurrentSelection()
        {
            if (!CurrentSelection.HasValue || CurrentLayout == null)
                return;

            GridCoordinate coordinate = CurrentSelection.Value.Coordinate;
            MapUnitState trackedEnemy = null;
            if (!string.IsNullOrEmpty(_trackedEnemyUnitId))
            {
                trackedEnemy = _gameplayService?.FindUnit(
                    _trackedEnemyUnitId);
                if (trackedEnemy == null || trackedEnemy.Soldiers <= 0 ||
                    string.Equals(
                        trackedEnemy.OwnerFactionId,
                        _gameplayService?.PlayerFactionId,
                        StringComparison.Ordinal))
                {
                    _trackedEnemyUnitId = string.Empty;
                    trackedEnemy = null;
                }
                else
                {
                    coordinate = trackedEnemy.Coordinate;
                }
            }

            MapCellSelection selection = DescribeCell(
                CurrentLayout,
                coordinate);
            if (trackedEnemy != null && !string.Equals(
                    selection.UnitId,
                    trackedEnemy.Id,
                    StringComparison.Ordinal))
            {
                selection = WithTrackedUnit(selection, trackedEnemy);
            }
            CurrentSelection = selection;
            CellSelected?.Invoke(selection);
        }

        private void ApplyMapSelection(MapCellSelection selection)
        {
            CurrentSelection = selection;
            string previousSelectedPlayerUnitId = _selectedPlayerUnitId;
            string previousTrackedEnemyUnitId = _trackedEnemyUnitId;
            bool selectsPlayerUnit =
                !string.IsNullOrEmpty(selection.UnitId) &&
                string.Equals(
                    selection.UnitOwnerFactionId,
                    _gameplayService?.PlayerFactionId,
                    StringComparison.Ordinal);
            bool tracksEnemy = !string.IsNullOrEmpty(selection.UnitId) &&
                !string.Equals(
                    selection.UnitOwnerFactionId,
                    _gameplayService?.PlayerFactionId,
                    StringComparison.Ordinal);
            if (selectsPlayerUnit)
            {
                _selectedPlayerUnitId = selection.UnitId;
            }
            _trackedEnemyUnitId = tracksEnemy
                ? selection.UnitId
                : string.Empty;
            if (!string.Equals(
                    previousSelectedPlayerUnitId,
                    _selectedPlayerUnitId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    previousTrackedEnemyUnitId,
                    _trackedEnemyUnitId,
                    StringComparison.Ordinal))
            {
                RefreshGameplayMarkers();
            }
        }

        private static MapCellSelection WithTrackedUnit(
            MapCellSelection selection,
            MapUnitState unit)
        {
            return new MapCellSelection(
                selection.Coordinate,
                selection.Content,
                selection.DisplayName,
                selection.InteractionHint,
                unit.Id,
                unit.OwnerFactionId,
                selection.MineOwnerFactionId,
                selection.CapturingFactionId,
                selection.CaptureProgress,
                selection.CaptureRequired,
                selection.CastleOwnerFactionId,
                selection.CastleRole,
                selection.CastleConflictKind,
                selection.CastleGarrisonUnitCount);
        }

        private void ForEachSurfaceCopy(Action<float> action)
        {
            float worldWidth = CurrentLayout.Width * tileSize;
            for (int copy = -SurfaceCopyRadius;
                 copy <= SurfaceCopyRadius;
                 copy++)
            {
                action(copy * worldWidth);
            }
        }

        private void LoadMapIcons()
        {
            if (_normalMineSprite == null)
            {
                _normalMineSprite = CreateRuntimeSprite(
                    Resources.Load<Texture2D>("MapIcons/mining_pickaxe"));
            }

            if (_goldMineSprite == null)
            {
                _goldMineSprite = CreateRuntimeSprite(
                    Resources.Load<Texture2D>("MapIcons/gold_coins"));
            }

            if (_protagonistCommanderSprite == null)
            {
                _protagonistCommanderSprite = CreateRuntimeSprite(
                    Resources.Load<Texture2D>(
                        "CommanderPortraits/protagonist_commander"),
                    1f);
            }

            if (_aiCommanderSprite == null)
            {
                _aiCommanderSprite = CreateRuntimeSprite(
                    Resources.Load<Texture2D>(
                        "CommanderPortraits/ai_commander"),
                    1f);
            }

            if (_commanderSquareSprite == null)
            {
                _commanderSquareTexture =
                    CreateSolidCommanderMarkerTexture();
                _commanderSquareSprite = CreateRuntimeSprite(
                    _commanderSquareTexture,
                    1f);
            }

            if (_commanderArrowSprite == null)
            {
                _commanderArrowTexture =
                    CreateDownArrowCommanderMarkerTexture();
                _commanderArrowSprite = CreateRuntimeSprite(
                    _commanderArrowTexture,
                    1f);
            }
        }

        private Sprite CreateRuntimeSprite(
            Texture2D texture,
            float widthInTiles = 0.90f)
        {
            if (texture == null)
                return null;

            float pixelsPerUnit = texture.width /
                (tileSize * Mathf.Max(0.1f, widthInTiles));
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                pixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
        }

        private static Texture2D CreateSolidCommanderMarkerTexture()
        {
            const int size = 8;
            var texture = new Texture2D(
                size,
                size,
                TextureFormat.RGBA32,
                false)
            {
                name = "장수 세력색 사각형"
            };
            var pixels = new Color32[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = new Color32(255, 255, 255, 255);
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private static Texture2D CreateDownArrowCommanderMarkerTexture()
        {
            const int width = 32;
            const int height = 22;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "장수 아래 화살표"
            };
            var pixels = new Color32[width * height];
            Color32 transparent = new Color32(255, 255, 255, 0);
            Color32 white = new Color32(255, 255, 255, 255);
            for (int y = 0; y < height; y++)
            {
                float progress = y / (float)(height - 1);
                int halfWidth = Mathf.RoundToInt(
                    progress * (width - 1) * 0.5f);
                int center = width / 2;
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] =
                        Mathf.Abs(x - center) <= halfWidth
                            ? white
                            : transparent;
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private void CreateMineIcon(
            string objectName,
            Vector3 position,
            Sprite sprite)
        {
            if (sprite == null)
                return;

            var iconObject = new GameObject(objectName);
            iconObject.transform.SetParent(_generatedRoot, false);
            iconObject.transform.position = position;
            var renderer = iconObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = 20;
            _iconBillboards.Add(iconObject.transform);
        }

        private Vector3 ToWorldPosition(
            GridCoordinate coordinate,
            float xOffset = 0f)
        {
            float widthOffset =
                (CurrentLayout.Width - 1) * tileSize * 0.5f;
            float heightOffset =
                (CurrentLayout.Height - 1) * tileSize * 0.5f;
            return new Vector3(
                coordinate.X * tileSize - widthOffset + xOffset,
                0f,
                coordinate.Y * tileSize - heightOffset);
        }

        private Material GetOrCreateBlockMaterial()
        {
            if (_blockMaterial != null)
                return _blockMaterial;

            Shader shader =
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Unlit/Color") ??
                Shader.Find("Sprites/Default") ??
                Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "거점 색상 표시용 셰이더를 찾지 못했습니다.");
            }

            _blockMaterial = new Material(shader)
            {
                name = "세력 색상 공유 재질"
            };
            if (_blockMaterial.HasProperty("_BaseColor"))
                _blockMaterial.SetColor("_BaseColor", Color.white);
            if (_blockMaterial.HasProperty("_Color"))
                _blockMaterial.SetColor("_Color", Color.white);
            return _blockMaterial;
        }

        private Material GetOrCreateMovementPathMaterial()
        {
            if (_movementPathMaterial != null)
                return _movementPathMaterial;

            _movementPathTexture = CreateMovementPathTexture();
            Shader shader =
                Shader.Find("Sprites/Default") ??
                Shader.Find("Unlit/Transparent") ??
                Shader.Find("Universal Render Pipeline/Unlit") ??
                Shader.Find("Standard");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "유닛 이동 점선 표시용 셰이더를 찾지 못했습니다.");
            }

            _movementPathMaterial = new Material(shader)
            {
                name = "유닛 이동 점선 공유 재질",
                mainTexture = _movementPathTexture
            };
            if (_movementPathMaterial.HasProperty("_BaseMap"))
            {
                _movementPathMaterial.SetTexture(
                    "_BaseMap",
                    _movementPathTexture);
            }
            if (_movementPathMaterial.HasProperty("_BaseColor"))
                _movementPathMaterial.SetColor("_BaseColor", Color.white);
            if (_movementPathMaterial.HasProperty("_Color"))
                _movementPathMaterial.SetColor("_Color", Color.white);
            return _movementPathMaterial;
        }

        private static Texture2D CreateMovementPathTexture()
        {
            const int width = 16;
            const int height = 8;
            var texture = new Texture2D(
                width,
                height,
                TextureFormat.RGBA32,
                false)
            {
                name = "유닛 이동 점선 무늬",
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            var pixels = new Color32[width * height];
            const float centerX = 3.5f;
            const float centerY = 3.5f;
            const float radiusSquared = 10.5f;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = x - centerX;
                    float dy = y - centerY;
                    byte alpha = dx * dx + dy * dy <= radiusSquared
                        ? (byte)255
                        : (byte)0;
                    pixels[y * width + x] =
                        new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private GameObject CreateBlock(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            Transform parent,
            bool colliderEnabled)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = objectName;
            block.transform.SetParent(parent, false);
            block.transform.position = position;
            block.transform.localScale = scale;

            Renderer renderer = block.GetComponent<Renderer>();
            renderer.sharedMaterial = GetOrCreateBlockMaterial();
            var properties = new MaterialPropertyBlock();
            properties.SetColor("_Color", color);
            properties.SetColor("_BaseColor", color);
            renderer.SetPropertyBlock(properties);
            Collider collider = block.GetComponent<Collider>();
            if (colliderEnabled)
            {
                collider.enabled = true;
            }
            else if (UnityEngine.Application.isPlaying)
            {
                UnityEngine.Object.Destroy(collider);
            }
            else
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
            return block;
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            HandleCameraInput();
            HandleSelectionInput();
#endif
            UpdateUnitMarkerInterpolation();
        }

#if ENABLE_LEGACY_INPUT_MANAGER
        private void HandleCameraInput()
        {
            if (_mapCamera == null || CurrentLayout == null)
                return;

            float horizontal = UnityEngine.Input.GetAxisRaw("Horizontal");
            float vertical = UnityEngine.Input.GetAxisRaw("Vertical");
            if (!Mathf.Approximately(horizontal, 0f) ||
                !Mathf.Approximately(vertical, 0f))
            {
                float zoomFactor =
                    _mapCamera.orthographicSize / Mathf.Max(1f, startingZoom);
                float distance =
                    cameraPanSpeed * zoomFactor * Time.unscaledDeltaTime;
                PanMap(horizontal * distance, vertical * distance);
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.L))
                FocusPlayerFaction();

            if (UnityEngine.Input.GetMouseButtonDown(2))
            {
                _lastMousePosition = UnityEngine.Input.mousePosition;
                _isMousePanning = true;
            }
            else if (UnityEngine.Input.GetMouseButtonUp(2))
            {
                _isMousePanning = false;
            }

            if (_isMousePanning && UnityEngine.Input.GetMouseButton(2))
            {
                Vector3 currentMousePosition = UnityEngine.Input.mousePosition;
                Vector3 pointerDelta = currentMousePosition - _lastMousePosition;
                _lastMousePosition = currentMousePosition;
                float worldUnitsPerPixel =
                    (_mapCamera.orthographicSize * 2f) /
                    Mathf.Max(1f, Screen.height);
                PanMap(
                    -pointerDelta.x * worldUnitsPerPixel * mousePanSensitivity,
                    -pointerDelta.y * worldUnitsPerPixel * mousePanSensitivity);
            }

            float wheel = UnityEngine.Input.mouseScrollDelta.y;
            if (!Mathf.Approximately(wheel, 0f))
            {
                _mapCamera.orthographicSize = Mathf.Clamp(
                    _mapCamera.orthographicSize - wheel * 1.5f,
                    minimumZoom,
                    maximumZoom);
                ClampAndWrapCameraFocus();
                ApplyCameraTransform();
            }
        }

        private void HandleSelectionInput()
        {
            bool leftClicked = UnityEngine.Input.GetMouseButtonDown(0);
            bool rightClicked = UnityEngine.Input.GetMouseButtonDown(1);
            if (PointerSelectionBlocked || (!leftClicked && !rightClicked))
                return;

            Camera camera = _mapCamera != null ? _mapCamera : Camera.main;
            if (camera == null)
                return;

            Ray ray = camera.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 2000f) ||
                !_mapSurfaceColliders.Contains(hit.collider) ||
                !TryGetCoordinate(hit.point, out GridCoordinate coordinate))
            {
                return;
            }

            MapCellSelection selection = DescribeCell(
                CurrentLayout,
                coordinate);
            ApplyMapSelection(selection);
            if (leftClicked)
            {
                CellMoveRequested?.Invoke(selection);
                PrimaryCellSelected?.Invoke();
            }
            CellSelected?.Invoke(selection);
            if (rightClicked)
                CellActionRequested?.Invoke(
                    selection,
                    UnityEngine.Input.mousePosition);
            Debug.Log($"지도 선택: {selection}");
        }
#endif

        private void LateUpdate()
        {
            Camera camera = _mapCamera != null ? _mapCamera : Camera.main;
            if (camera == null)
                return;

            Quaternion billboardRotation = camera.transform.rotation;
            for (int i = 0; i < _iconBillboards.Count; i++)
            {
                if (_iconBillboards[i] != null)
                    _iconBillboards[i].rotation = billboardRotation;
            }
        }

        private void FocusCameraOn(GridCoordinate coordinate)
        {
            _cameraFocus = ToWorldPosition(coordinate);
            ClampAndWrapCameraFocus();
            ApplyCameraTransform();
        }

        private void ClampAndWrapCameraFocus()
        {
            if (CurrentLayout == null)
                return;

            float worldWidth = CurrentLayout.Width * tileSize;
            float halfHeight = CurrentLayout.Height * tileSize * 0.5f;
            float zoom = _mapCamera != null
                ? _mapCamera.orthographicSize
                : startingZoom;
            float visibleGroundHalfHeight = zoom / Mathf.Sin(55f * Mathf.Deg2Rad);
            float verticalLimit = Mathf.Max(
                0f,
                halfHeight - visibleGroundHalfHeight);

            _cameraFocus.x = WrapCentered(_cameraFocus.x, worldWidth);
            _cameraFocus.z = Mathf.Clamp(
                _cameraFocus.z,
                -verticalLimit,
                verticalLimit);
        }

        private void ApplyCameraTransform()
        {
            if (_mapCamera == null)
                return;

            _mapCamera.transform.position =
                _cameraFocus + new Vector3(0f, 26f, -18.2f);
            _mapCamera.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
        }

        private void RemoveGeneratedMap()
        {
            DetachGameplayService();
            if (_generatedRoot != null)
            {
                GameObject generatedObject = _generatedRoot.gameObject;
                _generatedRoot = null;
                if (UnityEngine.Application.isPlaying)
                    Destroy(generatedObject);
                else
                    DestroyImmediate(generatedObject);
            }

            CurrentLayout = null;
            CurrentSelection = null;
            _trackedEnemyUnitId = string.Empty;
            _gameplayMarkerRoot = null;
            _mapSurfaceColliders.Clear();
            _iconBillboards.Clear();
            DestroyRuntimeAsset(_mapMaterial);
            DestroyRuntimeAsset(_blockMaterial);
            DestroyRuntimeAsset(_movementPathMaterial);
            DestroyRuntimeAsset(_mapTexture);
            DestroyRuntimeAsset(_movementPathTexture);
            DestroyRuntimeAsset(_mapMesh);
            _mapMaterial = null;
            _blockMaterial = null;
            _movementPathMaterial = null;
            _mapTexture = null;
            _movementPathTexture = null;
            _mapMesh = null;
        }

        private void EnsureCamera()
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                if (!createCameraIfMissing)
                    return;

                var cameraObject = new GameObject("경제 월드 카메라");
                camera = cameraObject.AddComponent<Camera>();
                cameraObject.tag = "MainCamera";
            }

            _mapCamera = camera;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Clamp(
                startingZoom,
                minimumZoom,
                maximumZoom);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.025f, 0.035f, 0.055f);
            ClampAndWrapCameraFocus();
            ApplyCameraTransform();
        }

        private void EnsureLight()
        {
            if (!createLightIfMissing || FindAnyObjectByType<Light>() != null)
                return;

            var lightObject = new GameObject("경제 월드 조명");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = new Color(1f, 0.93f, 0.82f);
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        private static float WrapCentered(float value, float length)
        {
            if (length <= 0f)
                return value;
            return Mathf.Repeat(value + length * 0.5f, length) -
                   length * 0.5f;
        }

        private static int PositiveModulo(int value, int divisor)
        {
            int remainder = value % divisor;
            return remainder < 0 ? remainder + divisor : remainder;
        }

        private static void DestroyRuntimeAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return;
            if (UnityEngine.Application.isPlaying)
                UnityEngine.Object.Destroy(asset);
            else
                UnityEngine.Object.DestroyImmediate(asset);
        }

        private void OnDestroy()
        {
            DetachGameplayService();
            DestroyRuntimeAsset(_normalMineSprite);
            DestroyRuntimeAsset(_goldMineSprite);
            DestroyRuntimeAsset(_mapMaterial);
            DestroyRuntimeAsset(_blockMaterial);
            DestroyRuntimeAsset(_movementPathMaterial);
            DestroyRuntimeAsset(_mapTexture);
            DestroyRuntimeAsset(_movementPathTexture);
            DestroyRuntimeAsset(_mapMesh);
        }
    }
}
