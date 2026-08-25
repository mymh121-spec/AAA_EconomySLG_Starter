using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Game.Application.PvP;
using Game.Application.Session;
using Game.Application.Turn;
using Game.Application.World;
using Game.Domain.Campaign;
using Game.Domain.Common;
using Game.Domain.Military;
using Game.Domain.World;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.Presentation
{
    [DisallowMultipleComponent]
    public sealed class GameModeSelectionController : MonoBehaviour
    {
        [Header("게임 모드 실행 대상")]
        [SerializeField] private SimulationBootstrapper singlePlayerSimulation;
        [SerializeField] private PvpOnlineSessionController multiplayerSession;
        [SerializeField] private StarterMapController gameplayMap;
        [SerializeField] private bool keepAcrossScenes = false;

        private const string DefaultServerEndpoint = "http://127.0.0.1:5200";
        private static readonly Color SinglePlayerSetupBackgroundColor =
            new Color(0.90f, 0.84f, 0.72f, 1f);
        private static readonly Color SinglePlayerSetupTextColor =
            new Color(0.07f, 0.06f, 0.05f, 1f);

        private readonly GameModeSelection _selection =
            new GameModeSelection();

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private VisualElement _uiRoot;
        private VisualElement _modeView;
        private VisualElement _singlePlayerSetupView;
        private VisualElement _connectionView;
        private VisualElement _singlePlayerView;
        private VisualElement _singlePlayerResultView;
        private VisualElement _multiplayerView;
        private ScrollView _singlePlayerScroll;
        private TextField _endpointField;
        private TextField _displayNameField;
        private TextField _roomCodeField;
        private TextField _tokenField;
        private TextField _hiveMatchIdField;
        private TextField _hivePointField;
        private TextField _hiveExtraDataField;
        private EnumField _mapSizeField;
        private EnumField _mapResourceField;
        private EnumField _mapWaterField;
        private IntegerField _mapSeedField;
        private IntegerField _neutralCastleField;
        private Toggle _mapWrapField;
        private Label _connectionStatus;
        private Label _roomStatus;
        private Label _singlePlayerStatus;
        private Label _singleMapSelectionStatus;
        private Button _singleStatusToggleButton;
        private VisualElement _singleStatusContent;
        private bool _singleStatusExpanded = true;
        private VisualElement _singleMapActionPanel;
        private Label _singleMapActionTitle;
        private Label _singleMapActionFeedback;
        private Button _createUnitButton;
        private Button _unitTypeButton;
        private Button _selectUnitButton;
        private Button _inspectUnitButton;
        private Button _moveUnitButton;
        private Button _cancelMoveButton;
        private VisualElement _mapContextMenu;
        private ScrollView _mapContextOptionsScroll;
        private VisualElement _contextUnitSection;
        private VisualElement _contextEconomySection;
        private VisualElement _contextSiegeSection;
        private VisualElement _contextMissionSection;
        private Label _mapContextTitle;
        private Label _mapContextHint;
        private Button _contextCreateUnitButton;
        private Button _contextUnitTypeButton;
        private Button _contextSelectUnitButton;
        private Button _contextInspectUnitButton;
        private Button _contextMoveUnitButton;
        private Button _contextCancelMoveButton;
        private Button _contextCaptureMineButton;
        private Button _contextEconomicSurveyButton;
        private Button _contextBuildMineButton;
        private Button _contextCastleActionButton;
        private Button _contextCastleRoleButton;
        private Button _contextSiegeActionButton;
        private Button _contextLootButton;
        private Button _contextPreserveButton;
        private Button _contextAutonomyButton;
        private Button _contextMissionButton;
        private Button _contextSupplyRaidButton;
        private Button _contextSupplyBlockadeButton;
        private Button _contextSupplyEscortButton;
        private Button _neutralNpcTopButton;
        private VisualElement _neutralNpcView;
        private VisualElement _npcCommanderPortrait;
        private Label _neutralNpcSelectionStatus;
        private Label _neutralNpcFeedback;
        private Button _npcArchetypeButton;
        private Button _npcWeaponButton;
        private Button _npcArmorButton;
        private Button _npcRecruitButton;
        private Button _npcEquipButton;
        private Button _npcCommanderButton;
        private Button _npcHireCommanderButton;
        private Button _operationBoardTopButton;
        private VisualElement _operationBoardView;
        private Label _operationBoardSummary;
        private Label _operationBoardFeedback;
        private Button _nextOperationButton;
        private Button _operationAgentButton;
        private Button _operationApproachButton;
        private Button _acceptOperationButton;
        private VisualElement _timeHudView;
        private Label _timeHudLabel;
        private Label _campaignHudLabel;
        private VisualElement _pauseMenuOverlay;
        private VisualElement _pauseMenuView;
        private VisualElement _keySettingsView;
        private bool _resumeRealtimeAfterPauseMenu;
        private Label _singlePlayerResultText;
        private Label _multiplayerStatus;
        private Label _multiplayerMapSelectionStatus;
        private Label _multiplayerMapActionFeedback;
        private Button _multiplayerMapOrderButton;
        private Button _multiplayerSiegeButton;
        private Button _multiplayerCancelOrderButton;
        private bool _singlePlayerEventsBound;
        private bool _multiplayerEventsBound;
        private bool _mapEventsBound;
        private UnitArchetype _pendingUnitArchetype = UnitArchetype.Swordsman;
        private UnitWeaponType _pendingWeaponType = UnitWeaponType.Sword;
        private ArmorClass _pendingArmorClass = ArmorClass.Light;
        private GridCoordinate? _pendingRecruitmentOrigin;
        private int _pendingCommanderIndex;
        private int _selectedOperationIndex;
        private int _selectedOperationAgentIndex;
        private int _selectedOperationApproachIndex;
        private string _queuedOperationId = string.Empty;
        private SubordinateMissionPlan _pendingDelegatedMissionPlan;
        private bool _hasPendingDelegatedMission;
        private float _nextMultiplayerStatusRefreshAt;
        private int _lastCampaignResultTurn = -1;
        private int _lastDominanceStreak;
        private string _campaignTransitionAlert = string.Empty;
        private int _lastEscapeHandledFrame = -1;

        private const int AutomaticRoomCapacity = 4;

        public GamePlayMode CurrentMode => _selection.CurrentMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallIfMissing()
        {
            var existing = FindAnyObjectByType<GameModeSelectionController>(
                FindObjectsInactive.Include);
            if (existing != null)
                return;

            var root = new GameObject("게임 모드 선택");
            root.AddComponent<GameModeSelectionController>();
        }

        private void Awake()
        {
            if (keepAcrossScenes)
                DontDestroyOnLoad(gameObject);

            CaptureExistingModeServices();
            SetServiceActive(singlePlayerSimulation, false);
            SetServiceActive(multiplayerSession, false);
            SetServiceActive(gameplayMap, false);
        }

        private void Start()
        {
            BuildUserInterface();
            ShowModeSelection();
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_selection.HasSelection &&
                UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapeInput();
            }
            else if (_selection.IsSinglePlayer &&
                !IsPauseMenuOpen() &&
                singlePlayerSimulation != null &&
                UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                ToggleSinglePlayerPause();
            }
#endif
            if (_selection.IsMultiplayer &&
                multiplayerSession?.CurrentState != null &&
                Time.unscaledTime >= _nextMultiplayerStatusRefreshAt)
            {
                _nextMultiplayerStatusRefreshAt = Time.unscaledTime + 0.25f;
                RefreshMultiplayerStatus(multiplayerSession.CurrentState);
            }
        }

        private void OnGUI()
        {
            Event currentEvent = Event.current;
            if (!_selection.HasSelection ||
                currentEvent == null ||
                currentEvent.type != EventType.KeyDown ||
                currentEvent.keyCode != KeyCode.Escape)
            {
                return;
            }

            HandleEscapeInput();
            currentEvent.Use();
        }

        private void HandleEscapeInput()
        {
            if (_lastEscapeHandledFrame == Time.frameCount)
                return;

            _lastEscapeHandledFrame = Time.frameCount;
            HandleEscapePressed();
        }

        private void CaptureExistingModeServices()
        {
            if (singlePlayerSimulation == null)
            {
                singlePlayerSimulation = FindAnyObjectByType<SimulationBootstrapper>(
                    FindObjectsInactive.Include);
            }

            if (multiplayerSession == null)
            {
                multiplayerSession = FindAnyObjectByType<PvpOnlineSessionController>(
                    FindObjectsInactive.Include);
            }

            if (gameplayMap == null)
            {
                gameplayMap = FindAnyObjectByType<StarterMapController>(
                    FindObjectsInactive.Include);
            }

            BindSinglePlayerEvents();
            BindMultiplayerEvents();
            BindMapEvents();
        }

        private SimulationBootstrapper EnsureSinglePlayerSimulation()
        {
            if (singlePlayerSimulation != null)
            {
                BindSinglePlayerEvents();
                return singlePlayerSimulation;
            }

            var localRoot = new GameObject("1인 플레이 시뮬레이션");
            localRoot.SetActive(false);
            if (keepAcrossScenes)
                localRoot.transform.SetParent(transform, false);
            singlePlayerSimulation =
                localRoot.AddComponent<SimulationBootstrapper>();
            BindSinglePlayerEvents();
            return singlePlayerSimulation;
        }

        private void BindSinglePlayerEvents()
        {
            if (singlePlayerSimulation == null || _singlePlayerEventsBound)
                return;

            singlePlayerSimulation.RealtimeStateChanged +=
                HandleSinglePlayerRealtimeStateChanged;
            singlePlayerSimulation.RealtimeFixedStepsAdvanced +=
                HandleRealtimeFixedStepsAdvanced;
            singlePlayerSimulation.RealtimeDayBoundaryReached +=
                HandleRealtimeDayBoundaryReached;
            _singlePlayerEventsBound = true;
        }

        private PvpOnlineSessionController EnsureMultiplayerSession()
        {
            if (multiplayerSession == null)
            {
                var onlineRoot = new GameObject("여러 명 플레이 세션");
                onlineRoot.SetActive(false);
                if (keepAcrossScenes)
                    onlineRoot.transform.SetParent(transform, false);
                multiplayerSession =
                    onlineRoot.AddComponent<PvpOnlineSessionController>();
            }

            BindMultiplayerEvents();
            return multiplayerSession;
        }

        private void BindMultiplayerEvents()
        {
            if (multiplayerSession == null || _multiplayerEventsBound)
                return;

            multiplayerSession.StateChanged += HandleMultiplayerStateChanged;
            multiplayerSession.RoomChanged += HandleMultiplayerRoomChanged;
            multiplayerSession.MatchmakingChanged +=
                HandleMultiplayerMatchmakingChanged;
            multiplayerSession.ErrorRaised += HandleMultiplayerError;
            _multiplayerEventsBound = true;
        }

        private void EnsureGameplayWorld(MapGenerationSettings settings = null)
        {
            if (gameplayMap == null)
            {
                var mapRoot = new GameObject("경제 지도");
                mapRoot.SetActive(false);
                gameplayMap = mapRoot.AddComponent<StarterMapController>();
            }

            SetServiceActive(gameplayMap, true);
            gameplayMap.PointerSelectionBlocked = false;
            if (settings != null)
            {
                gameplayMap.ConfigureMapGeneration(settings);
                if (gameplayMap.CurrentLayout == null)
                    gameplayMap.Initialize();
                else
                    gameplayMap.ResetMap();
            }
            else
            {
                gameplayMap.Initialize();
            }
            BindMapEvents();
        }

        private void BindMapEvents()
        {
            if (gameplayMap == null || _mapEventsBound)
                return;

            gameplayMap.CellSelected += HandleMapCellSelected;
            gameplayMap.CellMoveRequested += HandleMapMoveRequested;
            gameplayMap.PrimaryCellSelected += HideMapContextMenu;
            gameplayMap.CellActionRequested += HandleMapActionRequested;
            gameplayMap.GameplayStateChanged += HandleMapGameplayStateChanged;
            gameplayMap.MineCaptured += HandleMineCaptured;
            gameplayMap.MineSpawned += HandleMineSpawned;
            gameplayMap.MineConstructionCompleted +=
                HandleMineConstructionCompleted;
            gameplayMap.CastleCaptured += HandleCastleCaptured;
            gameplayMap.CapitalDestroyed += HandleCapitalDestroyed;
            gameplayMap.CastleRoleChanged += HandleCastleRoleChanged;
            gameplayMap.SiegeDayResolved += HandleSiegeDayResolved;
            gameplayMap.CommanderGenerated += HandleCommanderGenerated;
            gameplayMap.CommanderDied += HandleCommanderDied;
            gameplayMap.SupplyInterdictionResolved +=
                HandleSupplyInterdictionResolved;
            gameplayMap.WorldMissionReady += HandleWorldMissionReady;
            _mapEventsBound = true;
        }

        private void BuildUserInterface()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.name = "게임 모드 선택 패널 설정";
            _panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            _panelSettings.referenceResolution = new Vector2Int(1920, 1080);
            _panelSettings.themeStyleSheet =
                UnityEngine.Resources.Load<ThemeStyleSheet>(
                    "UnityDefaultRuntimeTheme");

            _document = gameObject.AddComponent<UIDocument>();
            _document.panelSettings = _panelSettings;
            _document.sortingOrder = 1000;

            _uiRoot = _document.rootVisualElement;
            _uiRoot.style.position = Position.Absolute;
            _uiRoot.style.left = 0;
            _uiRoot.style.right = 0;
            _uiRoot.style.top = 0;
            _uiRoot.style.bottom = 0;
            _uiRoot.style.alignItems = Align.Center;
            _uiRoot.style.justifyContent = Justify.Center;

            Font koreanFont = UnityEngine.Resources.Load<Font>(
                "Fonts/NotoSansKR");
            if (koreanFont != null)
            {
                // UI Toolkit font settings inherit from the root. Embedding an
                // OFL Korean font is required because WebGL cannot rely on a
                // Windows system font being available in the browser player.
                _uiRoot.style.unityFontDefinition =
                    FontDefinition.FromFont(koreanFont);
            }
            else
            {
                Debug.LogWarning(
                    "내장 한글 폰트 Fonts/NotoSansKR를 찾지 못했습니다.");
            }

            _modeView = CreateCard(
                _uiRoot,
                "기업의 시대",
                string.Empty);
            AddButton(_modeView, "1인이서 하기", ShowSinglePlayerSetup);
            AddButton(_modeView, "여러 명이서 하기", SelectMultiplayer);

            _singlePlayerSetupView = CreateCard(
                _uiRoot,
                "새 세계 설정",
                string.Empty);
            _singlePlayerSetupView.name = "single-player-setup-view";
            if (_singlePlayerSetupView.childCount > 0 &&
                _singlePlayerSetupView[0] is Label setupTitle)
            {
                setupTitle.name = "single-player-setup-title";
            }
            _mapSizeField = new EnumField("지도 크기", MapSizePreset.Standard);
            _mapResourceField = new EnumField(
                "자원량",
                MapResourceAbundance.Standard);
            _mapWaterField = new EnumField("바다 비율", MapWaterLevel.Standard);
            _mapSeedField = new IntegerField("지도 시드") { value = 42 };
            _neutralCastleField = new IntegerField("중립 성 수") { value = 8 };
            _mapWrapField = new Toggle("가로 세계 순환") { value = true };
            StyleSetupField(_mapSizeField);
            StyleSetupField(_mapResourceField);
            StyleSetupField(_mapWaterField);
            StyleSetupField(_mapSeedField);
            StyleSetupField(_neutralCastleField);
            StyleSetupField(_mapWrapField);
            _singlePlayerSetupView.Add(_mapSizeField);
            _singlePlayerSetupView.Add(_mapResourceField);
            _singlePlayerSetupView.Add(_mapWaterField);
            _singlePlayerSetupView.Add(_mapSeedField);
            _singlePlayerSetupView.Add(_neutralCastleField);
            _singlePlayerSetupView.Add(_mapWrapField);
            AddButton(
                _singlePlayerSetupView,
                "무작위 시드",
                () => _mapSeedField.value = unchecked(
                    Environment.TickCount ^ DateTime.UtcNow.Millisecond));
            AddButton(_singlePlayerSetupView, "이 설정으로 시작", SelectSinglePlayer);
            AddButton(_singlePlayerSetupView, "뒤로", ShowModeSelection);
            MakeCardVerticallyScrollable(
                _singlePlayerSetupView,
                "single-player-setup-scroll");
            ApplySinglePlayerSetupTheme();

            _connectionView = CreateCard(
                _uiRoot,
                "여러 명 플레이 연결",
                string.Empty);
            _endpointField = new TextField("서버 주소")
            {
                value = multiplayerSession != null
                    ? multiplayerSession.ServerEndpoint
                    : DefaultServerEndpoint
            };
            StyleInput(_endpointField);
            _connectionView.Add(_endpointField);

            _displayNameField = new TextField("표시 이름")
            {
                value = "플레이어"
            };
            StyleInput(_displayNameField);
            _connectionView.Add(_displayNameField);

            AddButton(_connectionView, "새 방 만들기", CreateMultiplayerRoom);

            _roomCodeField = new TextField("6자리 초대 코드");
            StyleInput(_roomCodeField);
            _connectionView.Add(_roomCodeField);
            AddButton(_connectionView, "초대 코드로 참가", JoinMultiplayerRoom);
            AddButton(
                _connectionView,
                "방 상태 갱신 / 방장 경기 시작",
                RefreshOrStartMultiplayerRoom);
            _roomStatus = AddStatus(_connectionView);
            _roomStatus.text = "방을 만들거나 초대 코드로 참가하세요.";

            var developerTitle = new Label("개발용 직접 세션 연결");
            developerTitle.style.fontSize = 16;
            developerTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            developerTitle.style.color = new Color(0.72f, 0.76f, 0.82f);
            developerTitle.style.marginTop = 8;
            _connectionView.Add(developerTitle);

            _tokenField = new TextField("접속 토큰")
            {
                isPasswordField = true
            };
            StyleInput(_tokenField);
            _connectionView.Add(_tokenField);

            var hiveTitle = new Label("HIVE 자동 매칭 · 선택 기능");
            hiveTitle.style.fontSize = 18;
            hiveTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            hiveTitle.style.color = new Color(0.39f, 0.78f, 1f);
            hiveTitle.style.marginTop = 8;
            hiveTitle.style.marginBottom = 6;
            _connectionView.Add(hiveTitle);

            _hiveMatchIdField = new TextField("HIVE 매치 ID")
            {
                value = "1"
            };
            StyleInput(_hiveMatchIdField);
            _connectionView.Add(_hiveMatchIdField);

            _hivePointField = new TextField("매칭 점수")
            {
                value = "1000"
            };
            StyleInput(_hivePointField);
            _connectionView.Add(_hivePointField);

            _hiveExtraDataField = new TextField("추가 정보")
            {
                value = "플레이어"
            };
            StyleInput(_hiveExtraDataField);
            _connectionView.Add(_hiveExtraDataField);
            _connectionStatus = AddStatus(_connectionView);
            AddButton(
                _connectionView,
                "HIVE에서 상대 찾고 서버 연결",
                ConnectMultiplayerThroughHive);
            AddButton(
                _connectionView,
                "HIVE 매칭 취소",
                CancelHiveMatchmaking);
            AddButton(_connectionView, "직접 서버 연결", ConnectMultiplayer);
            AddButton(_connectionView, "뒤로", ShowModeSelection);
            MakeCardVerticallyScrollable(
                _connectionView,
                "multiplayer-connection-scroll");

            _singlePlayerView = CreateCard(
                _uiRoot,
                "1인 플레이",
                string.Empty);
            _singlePlayerView.name = "single-player-hud";
            _singleStatusToggleButton = CreateMapActionButton(
                "상태 정보 접기 ▲",
                ToggleSinglePlayerStatus);
            _singleStatusToggleButton.name = "single-status-toggle";
            _singleStatusToggleButton.style.marginBottom = 8;
            _singlePlayerView.Add(_singleStatusToggleButton);
            _singleStatusContent = new VisualElement
            {
                name = "single-status-content"
            };
            _singlePlayerView.Add(_singleStatusContent);
            _singlePlayerStatus = AddStatus(_singleStatusContent);
            _singlePlayerStatus.name = "single-player-status";
            _campaignHudLabel = new Label
            {
                name = "campaign-status-label"
            };
            _campaignHudLabel.style.fontSize = 13;
            _campaignHudLabel.style.color = new Color(
                0.84f,
                0.90f,
                0.98f);
            _campaignHudLabel.style.whiteSpace = WhiteSpace.Normal;
            _campaignHudLabel.style.marginBottom = 9;
            _singleStatusContent.Add(_campaignHudLabel);
            _singleMapSelectionStatus = AddStatus(_singleStatusContent);
            _singleMapSelectionStatus.name = "single-map-selection-status";
            _singleMapSelectionStatus.text =
                "지도 칸을 선택하세요.";
            BuildSinglePlayerMapActionPanel(_singleStatusContent);
            _singlePlayerScroll = MakeCardVerticallyScrollable(
                _singlePlayerView,
                "single-player-scroll");
            StyleGameplayHud(_singlePlayerView);
            ConfigureDraggableGameplayPanel(
                _singlePlayerView,
                _singlePlayerScroll,
                "single-player-drag-handle");
            RegisterMapInputGuard(_singlePlayerView);

            _singlePlayerResultView = CreateCard(
                _uiRoot,
                "1인 플레이 최종 결과",
                string.Empty);
            _singlePlayerResultText = AddStatus(_singlePlayerResultView);
            _singlePlayerResultText.style.minHeight = 220;
            AddButton(
                _singlePlayerResultView,
                "확인하고 새 게임 시작",
                ConfirmSinglePlayerResult);
            MakeCardVerticallyScrollable(
                _singlePlayerResultView,
                "single-player-result-scroll");

            _multiplayerView = CreateCard(
                _uiRoot,
                "여러 명 플레이",
                string.Empty);
            _multiplayerStatus = AddStatus(_multiplayerView);
            _multiplayerMapSelectionStatus = AddStatus(_multiplayerView);
            _multiplayerMapSelectionStatus.name =
                "multiplayer-map-selection-status";
            _multiplayerMapSelectionStatus.text =
                "지도 칸을 클릭하면 지역 정보와 가능한 행동을 확인합니다.";
            _multiplayerMapOrderButton = AddButton(
                _multiplayerView,
                "선택 부대 이동 / 목표 점령",
                IssueMultiplayerMapOrder);
            _multiplayerSiegeButton = AddButton(
                _multiplayerView,
                "선택한 적 성 강습",
                IssueMultiplayerSiege);
            _multiplayerCancelOrderButton = AddButton(
                _multiplayerView,
                "선택 부대 이동 취소",
                CancelMultiplayerMapOrder);
            _multiplayerMapActionFeedback = AddStatus(_multiplayerView);
            _multiplayerMapActionFeedback.text =
                "서버 지도에서 아군 부대를 선택한 뒤 목표 칸을 선택하세요.";
            AddButton(_multiplayerView, "서버 상태 새로고침", RefreshMultiplayer);
            AddButton(_multiplayerView, "연결 종료 후 모드 선택", ShowModeSelection);
            ScrollView multiplayerScroll = MakeCardVerticallyScrollable(
                _multiplayerView,
                "multiplayer-hud-scroll");
            StyleGameplayHud(_multiplayerView);
            ConfigureDraggableGameplayPanel(
                _multiplayerView,
                multiplayerScroll,
                "multiplayer-drag-handle");
            RegisterMapInputGuard(_multiplayerView);
            BuildMapContextMenu(_uiRoot);
            BuildNeutralNpcInterface(_uiRoot);
            BuildOperationBoardInterface(_uiRoot);
            BuildTimeHudAndPauseMenu(_uiRoot);
            _uiRoot.RegisterCallback<GeometryChangedEvent>(
                HandleRootGeometryChanged);
        }

        private void SelectSinglePlayer()
        {
            _selection.Clear();
            if (!_selection.TrySelect(GamePlayMode.SinglePlayer, out string reason))
            {
                Debug.LogWarning(reason);
                return;
            }

            var mapSettings = new MapGenerationSettings(
                (MapSizePreset)_mapSizeField.value,
                (MapResourceAbundance)_mapResourceField.value,
                (MapWaterLevel)_mapWaterField.value,
                _mapSeedField.value,
                Math.Clamp(_neutralCastleField.value, 0, 24),
                _mapWrapField.value);
            EnsureGameplayWorld(mapSettings);
            EnsureSinglePlayerSimulation();
            multiplayerSession?.Disconnect();
            SetServiceActive(multiplayerSession, false);
            SetServiceActive(singlePlayerSimulation, true);
            SetVisible(_modeView, false);
            SetVisible(_singlePlayerSetupView, false);
            SetVisible(_connectionView, false);
            SetVisible(_multiplayerView, false);
            SetVisible(_singlePlayerResultView, false);
            ShowGameplayHud();
            if (singlePlayerSimulation.IsCampaignFinished)
                ShowSinglePlayerResult();
            else
            {
                SetVisible(_singlePlayerView, true);
                RefreshSinglePlayerStatus();
                if (gameplayMap.CurrentSelection.HasValue)
                    HandleMapCellSelected(gameplayMap.CurrentSelection.Value);
            }
        }

        private void SelectMultiplayer()
        {
            _selection.Clear();
            if (!_selection.TrySelect(GamePlayMode.Multiplayer, out string reason))
            {
                Debug.LogWarning(reason);
                return;
            }

            EnsureGameplayWorld();
            EnsureMultiplayerSession();
            SetServiceActive(singlePlayerSimulation, false);
            SetServiceActive(multiplayerSession, true);
            _connectionStatus.text = multiplayerSession.IsHiveMatchmakingAvailable
                ? "HIVE 자동 매칭 또는 직접 서버 연결을 선택하세요. " +
                  "토큰은 저장하지 않습니다."
                : "직접 서버 연결은 지금 사용할 수 있습니다. HIVE 자동 매칭은 " +
                  "SDK 설치와 HIVE 콘솔 설정 후 활성화됩니다.";
            SetVisible(_modeView, false);
            SetVisible(_singlePlayerSetupView, false);
            SetVisible(_singlePlayerView, false);
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_multiplayerView, false);
            SetVisible(_connectionView, true);
            ShowConnectionOverlay();
        }

        private async void CreateMultiplayerRoom()
        {
            if (!PrepareRoomRequest(out string displayName))
                return;

            _roomStatus.text = "새 방을 만드는 중입니다...";
            bool created = await multiplayerSession.CreateRoomAsync(
                displayName,
                AutomaticRoomCapacity);
            if (!created)
            {
                _roomStatus.text = multiplayerSession.LastError;
                return;
            }

            _roomCodeField.value = multiplayerSession.RoomCode;
            _roomStatus.text = FormatRoomStatus(multiplayerSession.CurrentRoom) +
                "\n초대 코드를 다른 플레이어에게 알려주세요.";
        }

        private async void JoinMultiplayerRoom()
        {
            if (!PrepareRoomRequest(out string displayName))
                return;
            string roomCode = _roomCodeField.value?.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            {
                _roomStatus.text = "6자리 초대 코드를 입력하세요.";
                return;
            }

            _roomStatus.text = "방에 참가하는 중입니다...";
            bool joined = await multiplayerSession.JoinRoomAsync(
                roomCode,
                displayName);
            if (!joined)
            {
                _roomStatus.text = multiplayerSession.LastError;
                return;
            }

            _roomCodeField.value = multiplayerSession.RoomCode;
            _roomStatus.text = FormatRoomStatus(multiplayerSession.CurrentRoom) +
                "\n방장이 경기를 시작하면 상태 갱신을 누르세요.";
        }

        private async void RefreshOrStartMultiplayerRoom()
        {
            if (multiplayerSession.CurrentRoomSession == null)
            {
                _roomStatus.text = "먼저 방을 만들거나 참가하세요.";
                return;
            }

            _roomStatus.text = "방 상태를 확인하는 중입니다...";
            bool success = multiplayerSession.IsRoomHost &&
                string.Equals(
                    multiplayerSession.CurrentRoom?.status,
                    "Lobby",
                    StringComparison.Ordinal)
                ? await multiplayerSession.StartRoomAsync()
                : await multiplayerSession.RefreshRoomAsync();
            if (!success)
            {
                _roomStatus.text = multiplayerSession.LastError;
                return;
            }

            _roomStatus.text = FormatRoomStatus(multiplayerSession.CurrentRoom);
            if (multiplayerSession.IsConnected)
                EnterConnectedMultiplayer();
        }

        private bool PrepareRoomRequest(out string displayName)
        {
            displayName = _displayNameField.value?.Trim();
            string endpoint = _endpointField.value?.Trim();
            if (string.IsNullOrWhiteSpace(displayName) || displayName.Length > 24)
            {
                _roomStatus.text = "표시 이름을 1~24자로 입력하세요.";
                return false;
            }
            if (!multiplayerSession.ConfigureServerEndpoint(endpoint))
            {
                _roomStatus.text = multiplayerSession.LastError;
                return false;
            }
            return true;
        }

        private static string FormatRoomStatus(PvpRoomStateDto room)
        {
            if (room == null)
                return "방 상태를 받지 못했습니다.";

            var builder = new StringBuilder(160)
                .Append("초대 코드: ")
                .Append(room.roomCode)
                .Append("\n상태: ")
                .Append(room.status);
            if (room.players != null)
            {
                if (room.players.Length > 0)
                    builder.Append("\n참가자");
                for (int i = 0; i < room.players.Length; i++)
                {
                    PvpRoomPlayerDto player = room.players[i];
                    builder.Append("\nP")
                        .Append(player.slot + 1)
                        .Append(' ')
                        .Append(player.displayName);
                    if (player.isHost)
                        builder.Append(" (방장)");
                }
            }
            return builder.ToString();
        }

        private async void ConnectMultiplayer()
        {
            if (multiplayerSession.IsRequestRunning)
                return;

            string endpoint = _endpointField.value?.Trim();
            string roomCode = _roomCodeField.value?.Trim();
            string token = _tokenField.value;
            _tokenField.value = string.Empty;

            if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            {
                _connectionStatus.text =
                    "게임 서버가 발급한 32자 이상의 접속 토큰이 필요합니다.";
                return;
            }
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            {
                _connectionStatus.text =
                    "개발용 직접 연결에도 6자리 방 코드가 필요합니다.";
                return;
            }

            if (!multiplayerSession.ConfigureServerEndpoint(endpoint))
            {
                _connectionStatus.text = multiplayerSession.LastError;
                return;
            }

            _connectionStatus.text = "서버에 연결하는 중입니다...";
            try
            {
                bool connected = await multiplayerSession.ConnectAsync(
                    roomCode,
                    token);
                if (!connected)
                {
                    _connectionStatus.text = multiplayerSession.LastError;
                    return;
                }

                EnterConnectedMultiplayer();
            }
            finally
            {
                token = string.Empty;
            }
        }

        private async void ConnectMultiplayerThroughHive()
        {
            if (multiplayerSession.IsRequestRunning ||
                multiplayerSession.IsMatchmakingRequestRunning)
            {
                return;
            }

            string endpoint = _endpointField.value?.Trim();
            string roomCode = _roomCodeField.value?.Trim();
            string token = _tokenField.value;
            if (string.IsNullOrWhiteSpace(token) || token.Length < 32)
            {
                _connectionStatus.text =
                    "HIVE 매칭 뒤 게임 서버에 들어갈 32자 이상의 접속 " +
                    "토큰이 필요합니다.";
                return;
            }
            if (string.IsNullOrWhiteSpace(roomCode) || roomCode.Length != 6)
            {
                _connectionStatus.text =
                    "HIVE 매칭과 연결할 6자리 게임 방 코드를 입력하세요.";
                return;
            }
            if (!int.TryParse(_hiveMatchIdField.value, out int matchId) ||
                matchId <= 0)
            {
                _connectionStatus.text =
                    "HIVE 콘솔에 등록한 1 이상의 매치 ID를 입력하세요.";
                return;
            }
            if (!int.TryParse(_hivePointField.value, out int point) ||
                point < 0)
            {
                _connectionStatus.text = "매칭 점수는 0 이상이어야 합니다.";
                return;
            }
            string extraData = _hiveExtraDataField.value ?? string.Empty;
            if (extraData.Length > PvpMatchmakingRequest.MaxExtraDataLength)
            {
                _connectionStatus.text =
                    $"추가 정보는 {PvpMatchmakingRequest.MaxExtraDataLength}자 " +
                    "이하여야 합니다.";
                return;
            }
            if (!multiplayerSession.ConfigureServerEndpoint(endpoint))
            {
                _connectionStatus.text = multiplayerSession.LastError;
                return;
            }

            _connectionStatus.text = "HIVE 자동 매칭을 시작합니다...";
            try
            {
                PvpMatchmakingSnapshot snapshot =
                    await multiplayerSession.FindHiveMatchAsync(
                        matchId,
                        point,
                        extraData);
                if (snapshot == null ||
                    snapshot.Status != PvpMatchmakingStatus.Matched)
                {
                    _connectionStatus.text = snapshot?.Message ??
                        "HIVE 매칭 결과를 받지 못했습니다.";
                    return;
                }

                _connectionStatus.text =
                    $"HIVE 매칭 완료({snapshot.Players.Count}명). " +
                    "게임 서버에 접속합니다...";
                bool connected = await multiplayerSession.ConnectAsync(
                    roomCode,
                    token);
                if (!connected)
                {
                    _connectionStatus.text = multiplayerSession.LastError;
                    return;
                }

                _tokenField.value = string.Empty;
                EnterConnectedMultiplayer();
            }
            catch (Exception exception)
            {
                _connectionStatus.text = exception.Message;
            }
            finally
            {
                token = string.Empty;
            }
        }

        private async void CancelHiveMatchmaking()
        {
            if (multiplayerSession == null)
                return;

            _connectionStatus.text = "HIVE 매칭을 취소하는 중입니다...";
            PvpMatchmakingSnapshot snapshot =
                await multiplayerSession.CancelHiveMatchmakingAsync();
            _connectionStatus.text = snapshot.Message;
        }

        private void EnterConnectedMultiplayer()
        {
            SetVisible(_connectionView, false);
            SetVisible(_multiplayerView, true);
            ShowGameplayHud();
            HandleMultiplayerStateChanged(multiplayerSession.CurrentState);
        }

        private void ToggleSinglePlayerPause()
        {
            if (singlePlayerSimulation.IsCampaignFinished)
            {
                ShowSinglePlayerResult();
                return;
            }

            singlePlayerSimulation.ToggleRealtimePause();
        }

        private void SetSinglePlayerSpeed(int speedMultiplier)
        {
            if (singlePlayerSimulation.IsCampaignFinished)
            {
                ShowSinglePlayerResult();
                return;
            }

            singlePlayerSimulation.SetRealtimeSpeed(speedMultiplier);
        }

        private void ShowSinglePlayerResult()
        {
            if (_singlePlayerResultText == null ||
                singlePlayerSimulation?.CampaignResult == null)
            {
                return;
            }

            _singlePlayerResultText.text = BuildSinglePlayerResultText();
            SetVisible(_modeView, false);
            SetVisible(_connectionView, false);
            SetVisible(_singlePlayerView, false);
            SetVisible(_multiplayerView, false);
            SetVisible(_singlePlayerResultView, true);
            ShowResultOverlay();
        }

        private string BuildSinglePlayerResultText()
        {
            return CampaignResultKoreanFormatter.FormatFinalSummary(
                singlePlayerSimulation.CampaignResult,
                singlePlayerSimulation.CurrentCampaignState);
        }

        private void ConfirmSinglePlayerResult()
        {
            _queuedOperationId = string.Empty;
            _hasPendingDelegatedMission = false;
            _selectedOperationIndex = 0;
            _selectedOperationApproachIndex = 0;
            ResetCampaignHudTracking();
            singlePlayerSimulation.RestartSimulation();
            gameplayMap?.ResetMap();
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_singlePlayerView, true);
            ShowGameplayHud();
            RefreshSinglePlayerStatus();
            if (gameplayMap?.CurrentSelection.HasValue == true)
                HandleMapCellSelected(gameplayMap.CurrentSelection.Value);
        }

        private async void MarkMultiplayerReady()
        {
            await RunMultiplayerRequest(
                () => multiplayerSession.MarkReadyAsync());
        }

        private async void RefreshMultiplayer()
        {
            if (!multiplayerSession.IsConnected || multiplayerSession.IsRequestRunning)
                return;

            try
            {
                _multiplayerStatus.text = "서버 상태를 갱신하는 중입니다...";
                await multiplayerSession.RefreshAsync();
            }
            catch (Exception exception)
            {
                _multiplayerStatus.text = exception.Message;
            }
        }

        private async Task RunMultiplayerRequest(Func<Task<bool>> request)
        {
            if (!multiplayerSession.IsConnected || multiplayerSession.IsRequestRunning)
                return;

            _multiplayerStatus.text = "서버 응답을 기다리는 중입니다...";
            bool succeeded = await request();
            if (!succeeded)
                _multiplayerStatus.text = multiplayerSession.LastError;
        }

        private void ShowModeSelection()
        {
            _hasPendingDelegatedMission = false;
            HideMapContextMenu();
            multiplayerSession?.Disconnect();
            SetServiceActive(singlePlayerSimulation, false);
            SetServiceActive(multiplayerSession, false);
            SetServiceActive(gameplayMap, false);
            _selection.Clear();

            SetVisible(_modeView, true);
            SetVisible(_singlePlayerSetupView, false);
            SetVisible(_connectionView, false);
            SetVisible(_singlePlayerView, false);
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_multiplayerView, false);
            ShowMenuOverlay();
        }

        private void ShowSinglePlayerSetup()
        {
            SetVisible(_modeView, false);
            SetVisible(_singlePlayerSetupView, true);
            SetVisible(_connectionView, false);
            SetVisible(_singlePlayerView, false);
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_multiplayerView, false);
            ShowMenuOverlay();
            ApplySinglePlayerSetupTheme();
            SetVisible(_singlePlayerSetupView, true);
        }

        private void ApplySinglePlayerSetupTheme()
        {
            if (_uiRoot != null)
                _uiRoot.style.backgroundColor = SinglePlayerSetupBackgroundColor;
            if (_singlePlayerSetupView == null)
                return;

            _singlePlayerSetupView.style.backgroundColor =
                SinglePlayerSetupBackgroundColor;
            Label title = _singlePlayerSetupView.Q<Label>(
                "single-player-setup-title");
            if (title != null)
                title.style.color = SinglePlayerSetupTextColor;

            StyleSetupField(_mapSizeField);
            StyleSetupField(_mapResourceField);
            StyleSetupField(_mapWaterField);
            StyleSetupField(_mapSeedField);
            StyleSetupField(_neutralCastleField);
            StyleSetupField(_mapWrapField);
        }

        private void ShowMenuOverlay()
        {
            if (_uiRoot == null)
                return;

            HidePauseMenuWithoutResuming();
            CloseNeutralNpcView();
            CloseOperationBoard();
            SetVisible(_neutralNpcTopButton, false);
            SetVisible(_operationBoardTopButton, false);
            SetVisible(_timeHudView, false);
            _uiRoot.style.backgroundColor =
                new Color(0.035f, 0.047f, 0.07f, 0.98f);
            _uiRoot.style.alignItems = Align.Center;
            _uiRoot.style.justifyContent = Justify.Center;
        }

        private void ShowConnectionOverlay()
        {
            if (_uiRoot == null)
                return;

            HidePauseMenuWithoutResuming();
            CloseNeutralNpcView();
            CloseOperationBoard();
            SetVisible(_neutralNpcTopButton, false);
            SetVisible(_operationBoardTopButton, false);
            SetVisible(_timeHudView, false);
            _uiRoot.style.backgroundColor =
                new Color(0.025f, 0.035f, 0.055f, 0.68f);
            _uiRoot.style.alignItems = Align.Center;
            _uiRoot.style.justifyContent = Justify.Center;
        }

        private void ShowResultOverlay()
        {
            if (_uiRoot == null)
                return;

            HidePauseMenuWithoutResuming();
            CloseNeutralNpcView();
            CloseOperationBoard();
            SetVisible(_neutralNpcTopButton, false);
            SetVisible(_operationBoardTopButton, false);
            SetVisible(_timeHudView, false);
            _uiRoot.style.backgroundColor =
                new Color(0.025f, 0.035f, 0.055f, 0.76f);
            _uiRoot.style.alignItems = Align.Center;
            _uiRoot.style.justifyContent = Justify.Center;
        }

        private void ShowGameplayHud()
        {
            if (_uiRoot == null)
                return;

            // 게임 중에는 전체 화면 UI를 투명하게 하여 대형 평면 월드가
            // 그대로 보이고, 조작 카드만 좌측 상단 HUD로 남는다.
            _uiRoot.style.backgroundColor = Color.clear;
            _uiRoot.style.alignItems = Align.FlexStart;
            _uiRoot.style.justifyContent = Justify.FlexStart;
            SetVisible(_neutralNpcTopButton, _selection.IsSinglePlayer);
            SetVisible(_operationBoardTopButton, _selection.IsSinglePlayer);
            SetVisible(_timeHudView, _selection.IsSinglePlayer);
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_keySettingsView, false);
            _resumeRealtimeAfterPauseMenu = false;
            _uiRoot.schedule.Execute(UpdateResponsiveLayoutFromRoot);
        }

        private void ToggleSinglePlayerStatus()
        {
            _singleStatusExpanded = !_singleStatusExpanded;
            SetVisible(_singleStatusContent, _singleStatusExpanded);
            if (_singlePlayerView != null)
            {
                if (_singleStatusExpanded)
                {
                    _singlePlayerView.style.height =
                        new Length(94, LengthUnit.Percent);
                }
                else
                {
                    _singlePlayerView.style.height = StyleKeyword.Auto;
                }
            }
            if (_singlePlayerScroll != null)
            {
                _singlePlayerScroll.style.flexGrow = _singleStatusExpanded
                    ? 1f
                    : 0f;
                _singlePlayerScroll.verticalScrollerVisibility =
                    _singleStatusExpanded
                        ? ScrollerVisibility.Auto
                        : ScrollerVisibility.Hidden;
            }
            if (_singleStatusToggleButton != null)
            {
                _singleStatusToggleButton.text = _singleStatusExpanded
                    ? "상태 정보 접기 ▲"
                    : "상태 정보 펼치기 ▼";
                _singleStatusToggleButton.style.marginBottom =
                    _singleStatusExpanded ? 8f : 0f;
            }
        }

        private void RefreshSinglePlayerStatus()
        {
            if (_singlePlayerStatus == null || singlePlayerSimulation == null)
                return;

            GameCalendarDate currentDate = GameCalendarDate.FromDayNumber(
                singlePlayerSimulation.RealtimeDayNumber);

            if (_timeHudLabel != null)
            {
                _timeHudLabel.text = new StringBuilder(64)
                    .Append(currentDate)
                    .Append(' ')
                    .Append(singlePlayerSimulation.RealtimeHour.ToString("D2"))
                    .Append(':')
                    .Append(singlePlayerSimulation.RealtimeMinute.ToString("D2"))
                    .Append(" · ")
                    .Append(singlePlayerSimulation.IsRealtimePaused
                        ? "일시정지"
                        : singlePlayerSimulation.RealtimeSpeedMultiplier + "배속")
                    .ToString();
            }

            RefreshCampaignHud();

            var builder = new StringBuilder(128);
            builder.Append("자금 ")
                .Append(singlePlayerSimulation.PlayerCash.ToString("N0"))
                .Append("원");

            MapUnitState selectedUnit = gameplayMap?.SelectedPlayerUnit;
            if (selectedUnit != null)
            {
                builder.Append("\n")
                    .Append(selectedUnit.ArchetypeDisplayName)
                    .Append(" · 병력 ")
                    .Append(selectedUnit.Soldiers.ToString("N0"))
                    .Append(" · 체력 ")
                    .Append(selectedUnit.Stamina)
                    .Append('/')
                    .Append(selectedUnit.MaxStamina)
                    .Append(" · 사기 ")
                    .Append(selectedUnit.Morale.ToString("N0"));
                if (selectedUnit.Commander != null)
                {
                    builder.Append("\n장수 ")
                        .Append(selectedUnit.Commander.DisplayName)
                        .Append(selectedUnit.Commander.IsProtagonist
                            ? " · 불사"
                            : string.Empty);
                }

                MapMilitaryUpkeepRecord upkeep =
                    MapCommanderUpkeepRules.Calculate(selectedUnit);
                builder.Append("\n유지비 ")
                    .Append(upkeep.TotalUpkeep.ToString("N0"))
                    .Append("원/일");
                if (upkeep.HasConcentrationSurcharge)
                {
                    builder.Append(" · 병력 집중 +")
                        .Append(upkeep.ConcentrationSurcharge.ToString("N0"))
                        .Append(" (한도 ")
                        .Append(upkeep.CommandCapacity.ToString("N0"))
                        .Append(')');
                }

                if (selectedUnit.IsMoving && gameplayMap.GameplayService != null)
                {
                    RealtimeMapGameplayService mapService =
                        gameplayMap.GameplayService;
                    if (mapService.IsUsingSeaTransport(selectedUnit))
                        builder.Append("\n간이 해상 수송 · 자동 승선/하선");
                    int stepsPerTile =
                        mapService.GetRequiredMovementStepsPerTile(selectedUnit);
                    int remainingSteps =
                        mapService.GetRemainingMovementFixedSteps(selectedUnit);
                    double completedTiles = selectedUnit.CompletedMovementTileCount +
                        selectedUnit.MovementProgress / (double)stepsPerTile;
                    int progressPercent = selectedUnit.TotalMovementTileCount > 0
                        ? Mathf.Clamp(
                            Mathf.RoundToInt(
                                (float)(completedTiles /
                                    selectedUnit.TotalMovementTileCount * 100d)),
                            0,
                            100)
                        : 0;
                    builder.Append("\n이동 진행 ")
                        .Append(progressPercent)
                        .Append("% · 남은 ")
                        .Append(selectedUnit.RemainingMovementTileCount)
                        .Append("칸 · 도착 예상 ")
                        .Append(FormatMovementArrival(remainingSteps));
                }
            }

            _singlePlayerStatus.text = builder.ToString();
        }

        private void RefreshCampaignHud()
        {
            if (_campaignHudLabel == null || singlePlayerSimulation == null)
                return;

            CampaignTurnResult result = singlePlayerSimulation.CampaignResult;
            UpdateCampaignTransitionAlert(result);

            CampaignState campaign =
                singlePlayerSimulation.CurrentCampaignState;
            MapCastleControlState capital = null;
            RealtimeMapGameplayService mapService = gameplayMap?.GameplayService;
            if (mapService != null)
                capital = mapService.FindCapital(mapService.PlayerFactionId);

            decimal bankruptcyLimit =
                singlePlayerSimulation.BankruptcyDebtLimit;
            bool hasRisk = !string.IsNullOrEmpty(_campaignTransitionAlert) ||
                campaign?.Player?.Company?.IsBankrupt == true ||
                campaign?.Player?.IsCapitalStanding == false ||
                (campaign?.Player?.Company != null &&
                 bankruptcyLimit > 0m &&
                 campaign.Player.Company.Debt >= bankruptcyLimit * 0.8m) ||
                capital?.IsUnderSiege == true ||
                (capital != null &&
                 capital.MaxWallDurability > 0 &&
                 capital.WallDurability * 100 <=
                 capital.MaxWallDurability * 30);

            if (result == null)
            {
                _campaignHudLabel.text = "경제력 집계 대기";
            }
            else
            {
                var builder = new StringBuilder(96)
                    .Append("경제력 ")
                    .Append(result.PlayerEconomicPower.ToString("N0"))
                    .Append(" · 상대 ")
                    .Append(result.OpponentCombinedEconomicPower.ToString("N0"));
                if (hasRisk)
                {
                    string risk = !string.IsNullOrEmpty(
                            _campaignTransitionAlert)
                        ? _campaignTransitionAlert
                        : campaign?.Player?.Company?.IsBankrupt == true
                            ? "파산"
                            : campaign?.Player?.IsCapitalStanding == false
                                ? "수도 멸망"
                                : capital?.IsUnderSiege == true
                                    ? "수도 공성 중"
                                    : capital != null &&
                                      capital.MaxWallDurability > 0 &&
                                      capital.WallDurability * 100 <=
                                      capital.MaxWallDurability * 30
                                        ? "수도 성벽 위험"
                                        : "부채 위험";
                    builder.Append("\n주의 · ").Append(risk);
                }
                _campaignHudLabel.text = builder.ToString();
            }
            _campaignHudLabel.style.color = hasRisk
                ? new Color(1f, 0.68f, 0.34f)
                : new Color(0.84f, 0.90f, 0.98f);
        }

        private void UpdateCampaignTransitionAlert(CampaignTurnResult result)
        {
            if (result == null ||
                result.ResolvedTurn.Value == _lastCampaignResultTurn)
            {
                return;
            }

            _campaignTransitionAlert =
                _lastCampaignResultTurn >= 0 &&
                _lastDominanceStreak > 0 &&
                result.DominanceConsecutiveTurns == 0
                    ? "3배 패권 유지 중단 · 0일부터 다시 계산"
                    : string.Empty;
            _lastCampaignResultTurn = result.ResolvedTurn.Value;
            _lastDominanceStreak = result.DominanceConsecutiveTurns;
        }

        private void ResetCampaignHudTracking()
        {
            _lastCampaignResultTurn = -1;
            _lastDominanceStreak = 0;
            _campaignTransitionAlert = string.Empty;
        }

        private void HandleSinglePlayerRealtimeStateChanged()
        {
            if (!_selection.IsSinglePlayer)
                return;

            RefreshSinglePlayerStatus();
            RefreshSelectedHeadquartersInventory();
            RefreshOperationBoard();
            if (singlePlayerSimulation != null &&
                singlePlayerSimulation.IsCampaignFinished)
            {
                ShowSinglePlayerResult();
            }
        }

        private void HandleMultiplayerStateChanged(PvpReconnectDto state)
        {
            if (_selection.IsMultiplayer &&
                gameplayMap != null &&
                state?.world?.map != null &&
                state.world.ownCompany != null)
            {
                if (gameplayMap.ApplyAuthoritativeSnapshot(
                        state.world.map,
                        state.world.ownCompany.companyId,
                        out string reason))
                {
                    if (gameplayMap.CurrentSelection.HasValue)
                    {
                        HandleMapCellSelected(
                            gameplayMap.CurrentSelection.Value);
                    }
                }
                else if (_multiplayerMapActionFeedback != null)
                {
                    _multiplayerMapActionFeedback.text = reason;
                }
            }

            RefreshMultiplayerStatus(state);
            RefreshMultiplayerMapActions(
                gameplayMap?.CurrentSelection);
        }

        private void HandleMultiplayerRoomChanged(PvpRoomStateDto room)
        {
            if (!_selection.IsMultiplayer || _roomStatus == null)
                return;
            _roomStatus.text = FormatRoomStatus(room);
        }

        private void HandleMultiplayerMatchmakingChanged(
            PvpMatchmakingSnapshot snapshot)
        {
            if (!_selection.IsMultiplayer ||
                _connectionStatus == null ||
                snapshot == null)
            {
                return;
            }

            var builder = new StringBuilder(160)
                .Append(snapshot.Message);
            if (!string.IsNullOrWhiteSpace(snapshot.ExternalMatchingId))
            {
                builder.Append("\nHIVE 매칭 번호: ")
                    .Append(snapshot.ExternalMatchingId);
            }
            if (snapshot.Players.Count > 0)
            {
                builder.Append("\n확인된 참가자: ")
                    .Append(snapshot.Players.Count)
                    .Append("명");
            }

            _connectionStatus.text = builder.ToString();
        }

        private void HandleMapCellSelected(MapCellSelection selection)
        {
            ApplySelectionOwnerTheme(selection);
            string description =
                $"선택: {selection.DisplayName} " +
                $"({selection.Coordinate.X}, {selection.Coordinate.Y})\n" +
                BuildMapInteractionDetails(selection);

            if (_singleMapSelectionStatus != null)
                _singleMapSelectionStatus.text = description;
            if (_multiplayerMapSelectionStatus != null)
                _multiplayerMapSelectionStatus.text = description;

            if (_selection.IsMultiplayer && gameplayMap != null)
            {
                string ownCompanyId =
                    multiplayerSession?.CurrentState?.world?.ownCompany?.companyId;
                if (!string.IsNullOrWhiteSpace(selection.UnitId) &&
                    !string.Equals(
                        gameplayMap.SelectedPlayerUnitId,
                        selection.UnitId,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        selection.UnitOwnerFactionId,
                        ownCompanyId,
                        StringComparison.Ordinal))
                {
                    gameplayMap.TrySelectPlayerUnitAt(
                        selection.Coordinate,
                        out _);
                }
                RefreshMultiplayerMapActions(selection);
            }

            RefreshSinglePlayerMapActions(selection);
        }

        private async void IssueMultiplayerMapOrder()
        {
            if (!TryGetMultiplayerMapOrderContext(
                    out string unitId,
                    out MapCellSelection selection))
            {
                return;
            }

            PvpCommandKind kind;
            switch (selection.Content)
            {
                case MapCellContent.NormalMine:
                case MapCellContent.GoldMine:
                    kind = PvpCommandKind.OccupyResourceSite;
                    break;
                case MapCellContent.PlayerBase:
                case MapCellContent.EnemyBase:
                case MapCellContent.NeutralCastle:
                case MapCellContent.PlayerCastle:
                case MapCellContent.EnemyCastle:
                    kind = PvpCommandKind.OccupyCastle;
                    break;
                default:
                    kind = PvpCommandKind.MoveUnit;
                    break;
            }

            await SubmitMultiplayerMapOrder(
                kind,
                unitId,
                selection.Coordinate,
                string.Empty,
                "지도 명령이 서버에 반영되었습니다.");
        }

        private async void IssueMultiplayerSiege()
        {
            if (!TryGetMultiplayerMapOrderContext(
                    out string unitId,
                    out MapCellSelection selection))
            {
                return;
            }

            await SubmitMultiplayerMapOrder(
                PvpCommandKind.StartSiege,
                unitId,
                selection.Coordinate,
                "Assault",
                "강습 명령이 서버에 반영되었습니다.");
        }

        private async void CancelMultiplayerMapOrder()
        {
            if (!TryGetMultiplayerMapOrderContext(
                    out string unitId,
                    out _))
            {
                return;
            }

            GridCoordinate coordinate = gameplayMap.SelectedPlayerUnit.Coordinate;
            await SubmitMultiplayerMapOrder(
                PvpCommandKind.CancelOrder,
                unitId,
                coordinate,
                string.Empty,
                "이동 취소가 서버에 반영되었습니다.");
        }

        private bool TryGetMultiplayerMapOrderContext(
            out string unitId,
            out MapCellSelection selection)
        {
            unitId = gameplayMap?.SelectedAuthoritativeServerUnitId ??
                string.Empty;
            selection = gameplayMap?.CurrentSelection ?? default;
            if (!_selection.IsMultiplayer ||
                multiplayerSession == null ||
                !multiplayerSession.IsConnected ||
                multiplayerSession.IsRequestRunning ||
                gameplayMap == null ||
                !gameplayMap.IsAuthoritativeMap ||
                string.IsNullOrWhiteSpace(unitId) ||
                !gameplayMap.CurrentSelection.HasValue)
            {
                if (_multiplayerMapActionFeedback != null)
                {
                    _multiplayerMapActionFeedback.text =
                        "서버 지도에서 아군 부대와 목표 칸을 선택하세요.";
                }
                return false;
            }

            return true;
        }

        private async Task SubmitMultiplayerMapOrder(
            PvpCommandKind kind,
            string unitId,
            GridCoordinate coordinate,
            string action,
            string successMessage)
        {
            _multiplayerMapActionFeedback.text =
                "지도 명령을 서버에 전송하는 중입니다...";
            bool succeeded = await multiplayerSession.SubmitMapOrderAsync(
                kind,
                unitId,
                coordinate.X,
                coordinate.Y,
                action);
            _multiplayerMapActionFeedback.text = succeeded
                ? successMessage
                : multiplayerSession.LastError;
            RefreshMultiplayerMapActions(gameplayMap?.CurrentSelection);
        }

        private void RefreshMultiplayerMapActions(
            MapCellSelection? selection)
        {
            bool canIssue = _selection.IsMultiplayer &&
                multiplayerSession != null &&
                multiplayerSession.IsConnected &&
                !multiplayerSession.IsRequestRunning &&
                gameplayMap != null &&
                gameplayMap.IsAuthoritativeMap &&
                !string.IsNullOrWhiteSpace(
                    gameplayMap.SelectedAuthoritativeServerUnitId) &&
                selection.HasValue;
            _multiplayerMapOrderButton?.SetEnabled(canIssue);

            bool isCastle = selection.HasValue &&
                (selection.Value.Content == MapCellContent.EnemyBase ||
                 selection.Value.Content == MapCellContent.EnemyCastle ||
                 selection.Value.Content == MapCellContent.NeutralCastle);
            _multiplayerSiegeButton?.SetEnabled(canIssue && isCastle);
            _multiplayerCancelOrderButton?.SetEnabled(
                canIssue && gameplayMap.SelectedPlayerUnit?.IsMoving == true);
        }

        private void HandleMapMoveRequested(MapCellSelection selection)
        {
            if (!_selection.IsSinglePlayer || gameplayMap == null)
                return;

            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            if (selectedUnit == null ||
                selectedUnit.Coordinate.Equals(selection.Coordinate) ||
                gameplayMap.CanSelectPlayerUnitAt(selection.Coordinate, out _))
            {
                return;
            }

            MoveSelectedPlayerUnit();
        }

        private string FormatMovementArrival(int remainingFixedSteps)
        {
            if (singlePlayerSimulation == null)
                return "계산 중";

            long currentMinute =
                (long)(singlePlayerSimulation.RealtimeDayNumber - 1) * 24L * 60L +
                singlePlayerSimulation.RealtimeHour * 60L +
                singlePlayerSimulation.RealtimeMinute;
            long travelMinutes = Math.Max(
                1L,
                (long)Math.Ceiling(
                    remainingFixedSteps *
                    singlePlayerSimulation.GameMinutesPerRealtimeFixedStep));
            long arrivalMinute = currentMinute + travelMinutes;
            int arrivalDay = (int)(arrivalMinute / (24L * 60L)) + 1;
            int minuteOfDay = (int)(arrivalMinute % (24L * 60L));
            int hour = minuteOfDay / 60;
            int minute = minuteOfDay % 60;
            return $"{GameCalendarDate.FromDayNumber(arrivalDay)} " +
                $"{hour:D2}:{minute:D2}";
        }

        private string BuildMapInteractionDetails(MapCellSelection selection)
        {
            if (!_selection.IsSinglePlayer ||
                selection.Content != MapCellContent.PlayerBase ||
                singlePlayerSimulation == null)
            {
                return selection.InteractionHint;
            }

            HeadquartersInventorySnapshot inventory =
                singlePlayerSimulation.GetPlayerHeadquartersInventory();
            var builder = new StringBuilder(96);
            builder.Append(selection.InteractionHint)
                .Append("\n창고 ")
                .Append(inventory.UsedCapacity.ToString("N2"))
                .Append(" / ")
                .Append(inventory.Capacity.ToString("N2"))
                .Append(" · 품목 ")
                .Append(inventory.Items.Count);
            return builder.ToString();
        }

        private void RefreshSelectedHeadquartersInventory()
        {
            if (gameplayMap == null ||
                !gameplayMap.CurrentSelection.HasValue)
            {
                return;
            }

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            if (selection.Content != MapCellContent.PlayerBase)
                return;

            if (_singleMapSelectionStatus != null)
            {
                _singleMapSelectionStatus.text =
                    $"선택: {selection.DisplayName} " +
                    $"({selection.Coordinate.X}, {selection.Coordinate.Y})\n" +
                    BuildMapInteractionDetails(selection);
            }

            if (_mapContextHint != null &&
                _mapContextMenu != null &&
                _mapContextMenu.resolvedStyle.display == DisplayStyle.Flex)
            {
                _mapContextHint.text = BuildMapInteractionDetails(selection);
            }
        }

        private void HandleMapActionRequested(
            MapCellSelection selection,
            Vector2 screenPosition)
        {
            if (!_selection.IsSinglePlayer || _mapContextMenu == null)
                return;

            _mapContextTitle.text =
                $"{selection.DisplayName} ({selection.Coordinate.X}, " +
                $"{selection.Coordinate.Y})";
            _mapContextHint.text = BuildMapInteractionDetails(selection);
            ConfigureMapActionButtons(
                selection,
                _contextCreateUnitButton,
                _contextSelectUnitButton,
                _contextMoveUnitButton);
            ConfigureCancelMoveButton(_contextCancelMoveButton);
            SetVisible(
                _contextUnitTypeButton,
                false);
            SetVisible(
                _contextInspectUnitButton,
                !string.IsNullOrEmpty(selection.UnitId));
            ConfigureCaptureMineButton(selection);
            ConfigureEconomicDevelopmentButtons(selection);
            ConfigureCastleButtons(selection);

            bool missionTarget =
                selection.Content == MapCellContent.EnemyBase;
            SetVisible(_contextMissionButton, missionTarget);
            _contextMissionButton.SetEnabled(missionTarget);
            _contextMissionButton.text = "미션 정보 · 정찰 / 봉쇄 / 공격";

            bool hasSupplyRoute = gameplayMap.TryGetPendingSupplyRouteOwnerAt(
                selection.Coordinate,
                out string supplyRouteOwner);
            bool hasSelectedUnit = gameplayMap.SelectedPlayerUnit != null;
            bool friendlySupplyRoute = hasSupplyRoute && string.Equals(
                supplyRouteOwner,
                gameplayMap.GameplayService.PlayerFactionId,
                StringComparison.Ordinal);
            SetVisible(
                _contextSupplyEscortButton,
                hasSelectedUnit && friendlySupplyRoute);
            SetVisible(
                _contextSupplyRaidButton,
                hasSelectedUnit && hasSupplyRoute && !friendlySupplyRoute);
            SetVisible(
                _contextSupplyBlockadeButton,
                hasSelectedUnit && hasSupplyRoute && !friendlySupplyRoute);

            RefreshMapContextSections();
            PositionMapContextMenu(screenPosition);
            SetVisible(_mapContextMenu, true);
        }

        private void HandleMapGameplayStateChanged()
        {
            if (!_selection.IsSinglePlayer || gameplayMap == null)
                return;

            RefreshSinglePlayerStatus();
            if (gameplayMap.CurrentSelection.HasValue)
                RefreshSinglePlayerMapActions(gameplayMap.CurrentSelection.Value);
        }

        private void HandleMineCaptured(MapMineCaptureRecord record)
        {
            if (!_selection.IsSinglePlayer)
                return;

            singlePlayerSimulation?.ApplyMapMineOwnership(
                gameplayMap.CurrentLayout,
                gameplayMap.GameplayService,
                record);
            if (_singleMapActionFeedback == null)
                return;

            string owner = string.Equals(
                record.NewOwnerFactionId,
                "player",
                StringComparison.Ordinal)
                ? "플레이어"
                : "경쟁 기업";
            _singleMapActionFeedback.text =
                $"{owner}이(가) {record.Coordinate} 광산을 점령했습니다.";
        }

        private void HandleMineSpawned(MapMineSpawnRecord record)
        {
            if (!_selection.IsSinglePlayer || _singleMapActionFeedback == null)
                return;
            if (record.WasConstructed)
                return;

            string mineName = record.Kind == MineKind.Gold
                ? "금광"
                : "철광산";
            _singleMapActionFeedback.text =
                $"{record.EconomicDay}일: 새로운 {mineName}이(가) " +
                $"{record.Coordinate}에서 발견되었습니다.";
        }

        private void HandleMineConstructionCompleted(
            MapMineConstructionCompletedRecord record)
        {
            if (!_selection.IsSinglePlayer || _singleMapActionFeedback == null)
                return;

            string mineName = record.Kind == MineKind.Gold
                ? "금광"
                : "철광산";
            _singleMapActionFeedback.text =
                $"{record.EconomicDay}일: {record.Coordinate}의 {mineName} 건설이 " +
                $"완료되었습니다. 생산성 {record.YieldMultiplier:P0}로 기존 " +
                "광산 생산·수송망에 합류합니다.";
        }

        private void HandleCastleCaptured(MapCastleCaptureRecord record)
        {
            if (!_selection.IsSinglePlayer)
                return;

            singlePlayerSimulation?.ApplyMapCastleOwnership(
                gameplayMap.CurrentLayout,
                gameplayMap.GameplayService,
                record);
            if (_singleMapActionFeedback == null)
                return;

            bool playerCaptured = string.Equals(
                record.NewOwnerFactionId,
                "player",
                StringComparison.Ordinal);
            string owner = playerCaptured
                ? "플레이어"
                : "경쟁 세력 " + record.NewOwnerFactionId;
            string action = record.WasSiege
                ? "공성을 마치고 성을 점령했습니다"
                : "빈 성을 점령했습니다";
            _singleMapActionFeedback.text =
                $"{owner}이(가) {record.Coordinate}에서 {action}." +
                (playerCaptured
                    ? " 우클릭 메뉴에서 거점 역할을 선택하세요."
                    : string.Empty);
        }

        private void HandleCapitalDestroyed(MapCapitalDestroyedRecord record)
        {
            if (!_selection.IsSinglePlayer || singlePlayerSimulation == null)
                return;

            singlePlayerSimulation.ApplyMapCapitalDestruction(record);
            string destroyedName = string.Equals(
                record.DestroyedFactionId,
                "player",
                StringComparison.Ordinal)
                ? "플레이어"
                : record.DestroyedFactionId;
            SetMapActionFeedback(
                $"{record.Coordinate}의 {destroyedName} 수도가 멸망했습니다.");
        }

        private void HandleCastleRoleChanged(
            MapCastleRoleChangedRecord record)
        {
            if (!_selection.IsSinglePlayer || _singleMapActionFeedback == null)
                return;

            _singleMapActionFeedback.text =
                $"{record.Coordinate} 성의 역할을 " +
                $"{MapCastleRoleNames.GetKoreanName(record.NewRole)}(으)로 " +
                "지정했습니다.";
        }

        private void HandleSiegeDayResolved(MapSiegeDayResult result)
        {
            if (!_selection.IsSinglePlayer)
                return;

            SetMapActionFeedback(
                $"{result.EconomicDay}일차 {result.Coordinate} " +
                $"{MapSiegeActionNames.GetKoreanName(result.Action)} 결과 · " +
                $"성벽 -{result.WallDamage:N0} · " +
                $"공격측 피해 {result.AttackerCasualties:N0} · " +
                $"수비측 피해 {result.DefenderCasualties:N0} · " +
                $"식량 -{result.FoodConsumed:N0}" +
                (result.DefenderRetreated
                    ? $" · 수비대 후퇴 · 추격 피해 {result.PursuitCasualties:N0}"
                    : string.Empty) +
                (result.CapitalDestroyed ? " · 수도 멸망" : string.Empty) +
                (result.CastleCaptured ? " · 성 함락" : string.Empty));
        }

        private void HandleCommanderGenerated(
            MapCommanderGeneratedRecord record)
        {
            if (!_selection.IsSinglePlayer || record.Commander == null)
                return;

            string winner = string.Equals(
                record.WinningFactionId,
                "player",
                StringComparison.Ordinal)
                ? "플레이어"
                : record.WinningFactionId;
            SetMapActionFeedback(
                $"{winner} 승전 · 3% 장수 생성 발동! " +
                $"{record.Commander.DisplayName} 장수가 공용 소환 후보에 합류했습니다.");
            RefreshNeutralNpcView();
        }

        private void HandleCommanderDied(MapCommanderDeathRecord record)
        {
            if (!_selection.IsSinglePlayer)
                return;

            string defeated = string.Equals(
                record.DefeatedFactionId,
                "player",
                StringComparison.Ordinal)
                ? "플레이어"
                : record.DefeatedFactionId;
            SetMapActionFeedback(
                $"{defeated} 패전 · 5% 전사 판정으로 " +
                $"{record.CommanderDisplayName} 장수가 전사했습니다.");
            RefreshNeutralNpcView();
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void HandleSupplyInterdictionResolved(
            MapSupplyInterdictionResult result)
        {
            if (!_selection.IsSinglePlayer)
                return;

            SetMapActionFeedback(
                $"{result.Coordinate} 보급로 판정 · " +
                (result.WasRaided
                    ? $"화물 손실 {result.CargoLost:N1}/" +
                      $"{result.CargoBefore:N1}"
                    : "습격 없음") +
                (result.WasBlockaded
                    ? $" · 봉쇄 성공, {result.DelayDays}일 지연"
                    : string.Empty) +
                (result.WasEscorted ? " · 호위대 대응" : string.Empty));
        }

        private void HandleRealtimeFixedStepsAdvanced(int fixedStepCount)
        {
            if (_selection.IsSinglePlayer)
                gameplayMap?.AdvanceGameplayFixedSteps(fixedStepCount);
        }

        private void HandleRealtimeDayBoundaryReached()
        {
            if (!_selection.IsSinglePlayer ||
                gameplayMap == null ||
                singlePlayerSimulation == null)
            {
                return;
            }

            singlePlayerSimulation.ApplyMapMineProduction(
                gameplayMap.CreateDailyMineProduction());
            MapMilitaryUpkeepSettlementReport upkeep =
                singlePlayerSimulation.SettleMapMilitaryUpkeep(
                    gameplayMap.CreateDailyMilitaryUpkeep());
            IReadOnlyList<MapSupplyTransportRecord> supplyTransports =
                gameplayMap.AdvanceDailySupplyLogistics(
                    singlePlayerSimulation);
            RefreshSelectedHeadquartersInventory();
            gameplayMap.AdvanceEconomicDay(out _);
            singlePlayerSimulation.SynchronizeWorldOwnershipToMap(
                gameplayMap.CurrentLayout,
                gameplayMap.GameplayService);
            if (supplyTransports.Count > 0)
            {
                decimal totalCost = 0m;
                int latestArrivalDay = 0;
                for (int i = 0; i < supplyTransports.Count; i++)
                {
                    totalCost += supplyTransports[i].Cost;
                    latestArrivalDay = Math.Max(
                        latestArrivalDay,
                        supplyTransports[i].ArrivalEconomicDay);
                }
                SetMapActionFeedback(
                    $"일일 보급 수송 {supplyTransports.Count:N0}건을 " +
                    $"예약했습니다. 비용 {totalCost:N0}원 · " +
                    $"최장 {latestArrivalDay:N0}일 도착 · " +
                    $"군 유지비 {upkeep.PlayerAssessed:N0}원" +
                    (upkeep.PlayerConcentrationSurcharge > 0m
                        ? $"(집중 할증 +" +
                          $"{upkeep.PlayerConcentrationSurcharge:N0})"
                        : string.Empty));
            }
            else if (upkeep.PlayerConcentrationSurcharge > 0m)
            {
                SetMapActionFeedback(
                    $"군 유지비 {upkeep.PlayerAssessed:N0}원 정산 · " +
                    $"장수 병력 집중 할증 +" +
                    $"{upkeep.PlayerConcentrationSurcharge:N0}원");
            }
        }

        private void CreatePlayerUnit()
        {
            if (gameplayMap == null || singlePlayerSimulation == null)
                return;

            GridCoordinate recruitOrigin = GetRecruitmentOrigin();
            if (!gameplayMap.CanCreatePlayerUnitAt(
                recruitOrigin,
                out string reason))
            {
                SetMapActionFeedback(reason);
                SetNeutralNpcFeedback(reason);
                return;
            }

            decimal cost = UnitEquipmentCatalog.GetRecruitmentCost(
                _pendingUnitArchetype,
                _pendingWeaponType,
                _pendingArmorClass);
            if (!singlePlayerSimulation.CanAffordPlayerCash(cost))
            {
                SetNeutralNpcFeedback(
                    $"자금이 부족합니다. 필요 {cost:N0}, " +
                    $"보유 {singlePlayerSimulation.PlayerCash:N0}");
                return;
            }

            if (!gameplayMap.TryCreatePlayerUnitAt(
                recruitOrigin,
                _pendingUnitArchetype,
                _pendingWeaponType,
                _pendingArmorClass,
                out reason))
            {
                SetNeutralNpcFeedback(reason);
                return;
            }

            if (!singlePlayerSimulation.TrySpendPlayerCash(cost, out reason))
            {
                SetNeutralNpcFeedback(reason);
                return;
            }

            string result =
                $"{MapUnitState.GetArchetypeDisplayName(_pendingUnitArchetype)} · " +
                $"{UnitEquipmentCatalog.GetWeaponDisplayName(_pendingWeaponType)} · " +
                $"{UnitEquipmentCatalog.GetArmorDisplayName(_pendingArmorClass)} " +
                $"구성으로 {recruitOrigin}에서 징병했습니다. 비용 {cost:N0}";
            SetMapActionFeedback(result);
            SetNeutralNpcFeedback(result);
            CloseNeutralNpcView();
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void EquipSelectedPlayerUnit()
        {
            if (gameplayMap == null || singlePlayerSimulation == null)
                return;

            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            if (selectedUnit == null)
            {
                SetNeutralNpcFeedback("장비를 변경할 플레이어 부대를 먼저 선택하세요.");
                return;
            }

            decimal cost = UnitEquipmentCatalog.GetEquipmentCost(
                _pendingWeaponType,
                _pendingArmorClass);
            if (!singlePlayerSimulation.CanAffordPlayerCash(cost))
            {
                SetNeutralNpcFeedback(
                    $"자금이 부족합니다. 필요 {cost:N0}, " +
                    $"보유 {singlePlayerSimulation.PlayerCash:N0}");
                return;
            }

            if (!gameplayMap.TryEquipSelectedPlayerUnit(
                _pendingWeaponType,
                _pendingArmorClass,
                out string reason))
            {
                SetNeutralNpcFeedback(reason);
                return;
            }

            if (!singlePlayerSimulation.TrySpendPlayerCash(cost, out reason))
            {
                SetNeutralNpcFeedback(reason);
                return;
            }

            string result =
                $"{selectedUnit.Id} 장비를 " +
                $"{UnitEquipmentCatalog.GetWeaponDisplayName(_pendingWeaponType)} / " +
                $"{UnitEquipmentCatalog.GetArmorDisplayName(_pendingArmorClass)}(으)로 " +
                $"변경했습니다. 비용 {cost:N0}";
            SetMapActionFeedback(result);
            SetNeutralNpcFeedback(result);
            CloseNeutralNpcView();
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private MapCommanderState GetPendingCommander()
        {
            List<MapCommanderState> commanders = GetCommanderCandidates();
            if (commanders.Count == 0)
                return null;

            _pendingCommanderIndex = Math.Clamp(
                _pendingCommanderIndex,
                0,
                commanders.Count - 1);
            return commanders[_pendingCommanderIndex];
        }

        private List<MapCommanderState> GetCommanderCandidates()
        {
            var candidates = new List<MapCommanderState>();
            IReadOnlyList<MapCommanderState> commanders =
                gameplayMap?.Commanders;
            if (commanders == null)
                return candidates;

            for (int i = 0; i < commanders.Count; i++)
            {
                MapCommanderState commander = commanders[i];
                if (commander.IsAlive && !commander.IsProtagonist)
                    candidates.Add(commander);
            }
            return candidates;
        }

        private void CyclePendingCommander()
        {
            List<MapCommanderState> commanders = GetCommanderCandidates();
            if (commanders.Count == 0)
            {
                SetNeutralNpcFeedback("소환 가능한 AI 장수가 없습니다.");
                return;
            }

            _pendingCommanderIndex =
                (_pendingCommanderIndex + 1) % commanders.Count;
            RefreshNeutralNpcView();
        }

        private string BuildCommanderCandidateList()
        {
            List<MapCommanderState> commanders = GetCommanderCandidates();
            if (commanders.Count == 0)
                return "공용 AI 장수 후보 없음";

            var builder = new StringBuilder("공용 AI 장수");
            for (int i = 0; i < commanders.Count; i++)
            {
                MapCommanderState commander = commanders[i];
                builder.Append('\n')
                    .Append(i == _pendingCommanderIndex ? "▶ " : "  ")
                    .Append(commander.DisplayName)
                    .Append(" · ")
                    .Append(MapCommanderPersonalityNames.GetKoreanName(
                        commander.Personality))
                    .Append(commander.IsAvailable
                        ? " · 소환 가능"
                        : $" · {commander.AssignedUnitId} 배속 중");
            }

            return builder.ToString();
        }

        private void HirePendingCommander()
        {
            if (gameplayMap == null || singlePlayerSimulation == null)
                return;

            MapCommanderState commander = GetPendingCommander();
            MapUnitState unit = gameplayMap.SelectedPlayerUnit;
            if (commander == null || unit == null)
            {
                SetNeutralNpcFeedback(
                    "AI 장수와 배속할 플레이어 부대를 선택하세요.");
                return;
            }
            if (!singlePlayerSimulation.CanAffordPlayerCash(
                    commander.HiringCost))
            {
                SetNeutralNpcFeedback(
                    $"자금이 부족합니다. 필요 {commander.HiringCost:N0}, " +
                    $"보유 {singlePlayerSimulation.PlayerCash:N0}");
                return;
            }

            if (!gameplayMap.TryHireCommanderForSelectedPlayerUnit(
                    commander.Id,
                    out string reason))
            {
                SetNeutralNpcFeedback(reason);
                return;
            }
            if (!singlePlayerSimulation.TrySpendPlayerCash(
                    commander.HiringCost,
                    out reason))
            {
                SetNeutralNpcFeedback(reason);
                return;
            }

            string result =
                $"{commander.DisplayName} 장수를 {unit.Id}에 소환·배속했습니다. " +
                $"성향 {MapCommanderPersonalityNames.GetKoreanName(commander.Personality)} · " +
                $"충성도 {commander.Loyalty} · 소환비 {commander.HiringCost:N0}";
            SetMapActionFeedback(result);
            SetNeutralNpcFeedback(result);
            RefreshNeutralNpcView();
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void CyclePendingUnitArchetype()
        {
            UnitArchetype[] order =
            {
                UnitArchetype.Swordsman,
                UnitArchetype.Spearman,
                UnitArchetype.Maceman,
                UnitArchetype.Archer,
                UnitArchetype.Slinger,
                UnitArchetype.Cavalry
            };
            int next = 0;
            for (int i = 0; i < order.Length; i++)
            {
                if (order[i] == _pendingUnitArchetype)
                {
                    next = (i + 1) % order.Length;
                    break;
                }
            }

            _pendingUnitArchetype = order[next];
            _pendingWeaponType =
                UnitEquipmentCatalog.GetDefaultWeapon(_pendingUnitArchetype);
            RefreshUnitTypeButtonLabels();
            RefreshNeutralNpcView();
        }

        private void CyclePendingWeapon()
        {
            UnitWeaponType[] order =
            {
                UnitWeaponType.Sword,
                UnitWeaponType.Spear,
                UnitWeaponType.Mace,
                UnitWeaponType.Bow,
                UnitWeaponType.Sling,
                UnitWeaponType.Lance
            };
            int next = Array.IndexOf(order, _pendingWeaponType) + 1;
            _pendingWeaponType = order[next % order.Length];
            RefreshNeutralNpcView();
        }

        private void CyclePendingArmor()
        {
            ArmorClass[] order =
            {
                ArmorClass.Unarmored,
                ArmorClass.Light,
                ArmorClass.Heavy
            };
            int next = Array.IndexOf(order, _pendingArmorClass) + 1;
            _pendingArmorClass = order[next % order.Length];
            RefreshNeutralNpcView();
        }

        private void RefreshUnitTypeButtonLabels()
        {
            string label = "창설 병종: " +
                MapUnitState.GetArchetypeDisplayName(_pendingUnitArchetype) +
                " · 클릭해 변경";
            if (_unitTypeButton != null)
                _unitTypeButton.text = label;
            if (_contextUnitTypeButton != null)
                _contextUnitTypeButton.text = label;
        }

        private void InspectUnitAtCurrentSelection()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            MapUnitState unit = gameplayMap.FindUnitAt(
                gameplayMap.CurrentSelection.Value.Coordinate);
            if (unit == null)
            {
                SetMapActionFeedback("이 칸에는 확인할 부대가 없습니다.");
                return;
            }

            string owner = string.Equals(
                unit.OwnerFactionId,
                "player",
                StringComparison.Ordinal)
                ? "플레이어"
                : "경쟁 세력 " + unit.OwnerFactionId;
            string movement = unit.Destination.HasValue
                ? $"이동 중 → {unit.Destination.Value}"
                : "대기 중";
            string commander = unit.Commander == null
                ? "장수 없음"
                : $"장수 {unit.Commander.DisplayName}" +
                  (unit.Commander.IsProtagonist ? "(불사)" : string.Empty);
            SetMapActionFeedback(
                $"{owner} · {unit.ArchetypeDisplayName} · " +
                $"병력 {unit.Soldiers:N0} · 사기 {unit.Morale:N0} · " +
                $"체력 {unit.Stamina}/{unit.MaxStamina}\n" +
                $"능력 배율: 공격 x{unit.EffectiveAttackModifier:F2} · " +
                $"방어 x{unit.EffectiveDefenseModifier:F2} · " +
                $"기동 x{unit.MobilityModifier:F2}\n" +
                $"{commander} · {unit.Coordinate} · {movement}");
        }

        private void SelectPlayerUnit()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            if (!gameplayMap.TrySelectPlayerUnitAt(
                gameplayMap.CurrentSelection.Value.Coordinate,
                out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            SetMapActionFeedback(
                $"{gameplayMap.SelectedPlayerUnitId}을(를) 선택했습니다. " +
                "목적지 칸을 클릭하세요.");
            RefreshSelectedMapActions();
        }

        private void MoveSelectedPlayerUnit()
        {
            if (gameplayMap == null ||
                singlePlayerSimulation == null ||
                !gameplayMap.CurrentSelection.HasValue)
            {
                return;
            }

            GridCoordinate destination =
                gameplayMap.CurrentSelection.Value.Coordinate;
            bool isReroute =
                gameplayMap.SelectedPlayerUnit?.IsMoving == true;
            bool usesSeaTransport =
                gameplayMap.WillSelectedMoveUseSeaTransport(destination);
            if (!gameplayMap.CanMoveSelectedPlayerUnit(
                destination,
                out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            if (!gameplayMap.TryMoveSelectedPlayerUnit(destination, out reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            bool isMine = selection.Content == MapCellContent.NormalMine ||
                          selection.Content == MapCellContent.GoldMine;
            SetMapActionFeedback(usesSeaTransport
                ? $"{destination} 아군 항구로 해상 수송을 시작합니다. " +
                  "승선과 하선은 자동 처리됩니다."
                : isMine
                ? $"{destination} 광산으로 이동합니다. 도착하면 점령을 시작합니다."
                : isReroute
                    ? $"이동 경로를 {destination}(으)로 변경했습니다. " +
                      "추가 체력은 소모하지 않습니다."
                    : $"{destination}(으)로 이동 명령을 내렸습니다.");
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void CancelSelectedPlayerUnitMove()
        {
            if (gameplayMap == null)
                return;

            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            GridCoordinate? previousDestination = selectedUnit?.Destination;
            if (!gameplayMap.TryCancelSelectedPlayerUnitMove(
                out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            SetMapActionFeedback(previousDestination.HasValue
                ? $"{previousDestination.Value} 이동 명령을 취소했습니다. " +
                  "이미 사용한 체력은 반환되지 않습니다."
                : "이동 명령을 취소했습니다.");
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void CaptureSelectedMine()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            bool isMine = selection.Content == MapCellContent.NormalMine ||
                          selection.Content == MapCellContent.GoldMine;
            if (!isMine)
            {
                SetMapActionFeedback("이 위치는 점령 가능한 자원 거점이 아닙니다.");
                return;
            }

            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            if (selectedUnit == null)
            {
                SetMapActionFeedback("먼저 플레이어 부대를 선택하세요.");
                return;
            }

            bool alreadyOwned = string.Equals(
                selection.MineOwnerFactionId,
                "player",
                StringComparison.Ordinal);
            if (selectedUnit.Coordinate.Equals(selection.Coordinate))
            {
                SetMapActionFeedback(alreadyOwned
                    ? $"{selection.DisplayName}에서 채광 거점을 방어하고 있습니다."
                    : $"{selection.DisplayName} 점령을 진행합니다. " +
                      "부대를 이 위치에 유지하면 점령이 완료됩니다.");
                return;
            }

            if (!gameplayMap.TryMoveSelectedPlayerUnit(
                selection.Coordinate,
                out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            SetMapActionFeedback(alreadyOwned
                ? $"소유 중인 {selection.DisplayName}(으)로 이동 명령을 내렸습니다."
                : $"{selection.DisplayName} 점령 명령을 내렸습니다. " +
                  "부대가 도착하면 점령이 시작됩니다.");
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void SurveySelectedEconomicSite()
        {
            if (gameplayMap == null ||
                singlePlayerSimulation == null ||
                !gameplayMap.CurrentSelection.HasValue)
            {
                return;
            }

            GridCoordinate coordinate =
                gameplayMap.CurrentSelection.Value.Coordinate;
            if (!gameplayMap.CanSurveySelectedEconomicSite(
                    coordinate,
                    out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }
            decimal cost = MapEconomicDevelopmentRules.SurveyCost;
            if (!singlePlayerSimulation.CanAffordPlayerCash(cost))
            {
                SetMapActionFeedback(
                    $"경제 탐사 비용이 부족합니다. 필요 {cost:N0}원 · " +
                    $"보유 {singlePlayerSimulation.PlayerCash:N0}원");
                return;
            }
            if (!singlePlayerSimulation.TrySpendPlayerCash(cost, out reason) ||
                !gameplayMap.TrySurveySelectedEconomicSite(
                    coordinate,
                    out MapEconomicSurveyState survey,
                    out reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            if (!survey.HasViableDeposit)
            {
                SetMapActionFeedback(
                    $"{coordinate} 경제 탐사 완료 · {cost:N0}원 지출 · " +
                    "채굴 가치가 있는 매장지를 찾지 못했습니다.");
            }
            else
            {
                MineKind kind = survey.DepositKind.Value;
                string mineName = kind == MineKind.Gold ? "금광" : "철광산";
                SetMapActionFeedback(
                    $"{coordinate} 경제 탐사 완료 · {mineName} 후보 발견 · " +
                    $"예상 생산성 {survey.YieldMultiplier:P0} · " +
                    $"건설비 {MapEconomicDevelopmentRules.GetConstructionCost(kind):N0}원");
            }
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void StartSelectedMineConstruction()
        {
            if (gameplayMap == null ||
                singlePlayerSimulation == null ||
                !gameplayMap.CurrentSelection.HasValue)
            {
                return;
            }

            GridCoordinate coordinate =
                gameplayMap.CurrentSelection.Value.Coordinate;
            if (!gameplayMap.CanStartSelectedMineConstruction(
                    coordinate,
                    out MapEconomicSurveyState survey,
                    out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            MineKind kind = survey.DepositKind.Value;
            decimal cost =
                MapEconomicDevelopmentRules.GetConstructionCost(kind);
            if (!singlePlayerSimulation.CanAffordPlayerCash(cost))
            {
                SetMapActionFeedback(
                    $"채굴소 건설비가 부족합니다. 필요 {cost:N0}원 · " +
                    $"보유 {singlePlayerSimulation.PlayerCash:N0}원");
                return;
            }
            if (!singlePlayerSimulation.TrySpendPlayerCash(cost, out reason) ||
                !gameplayMap.TryStartSelectedMineConstruction(
                    coordinate,
                    out MapMineConstructionState construction,
                    out reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            string mineName = kind == MineKind.Gold ? "금광" : "철광산";
            SetMapActionFeedback(
                $"{coordinate} {mineName} 건설 시작 · {cost:N0}원 지출 · " +
                $"완공까지 {construction.TotalDays}일");
            RefreshSinglePlayerStatus();
            RefreshSelectedMapActions();
        }

        private void RefreshSelectedMapActions()
        {
            if (gameplayMap != null && gameplayMap.CurrentSelection.HasValue)
                RefreshSinglePlayerMapActions(gameplayMap.CurrentSelection.Value);
        }

        private void RefreshSinglePlayerMapActions(MapCellSelection selection)
        {
            if (_singleMapActionPanel == null)
                return;

            bool singlePlayer = _selection.IsSinglePlayer;
            SetVisible(_singleMapActionPanel, singlePlayer);
            if (!singlePlayer || gameplayMap == null)
                return;

            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            _singleMapActionTitle.text = selectedUnit == null
                ? "지도 행동 · 선택된 유닛 없음"
                : $"지도 행동 · {selectedUnit.ArchetypeDisplayName} · " +
                  $"병력 {selectedUnit.Soldiers:N0}";

            ConfigureMapActionButtons(
                selection,
                _createUnitButton,
                _selectUnitButton,
                _moveUnitButton);
            ConfigureCancelMoveButton(_cancelMoveButton);
            bool atPlayerBase =
                selection.Content == MapCellContent.PlayerBase;
            bool hasUnit = !string.IsNullOrEmpty(selection.UnitId);
            SetVisible(_unitTypeButton, false);
            SetVisible(_inspectUnitButton, hasUnit);
            if (_mapContextMenu != null &&
                _mapContextMenu.resolvedStyle.display == DisplayStyle.Flex)
            {
                ConfigureMapActionButtons(
                    selection,
                    _contextCreateUnitButton,
                    _contextSelectUnitButton,
                    _contextMoveUnitButton);
                ConfigureCancelMoveButton(_contextCancelMoveButton);
                SetVisible(_contextUnitTypeButton, false);
                SetVisible(_contextInspectUnitButton, hasUnit);
                ConfigureCaptureMineButton(selection);
                ConfigureEconomicDevelopmentButtons(selection);
                ConfigureCastleButtons(selection);
            }

            if (_neutralNpcView != null &&
                _neutralNpcView.resolvedStyle.display == DisplayStyle.Flex)
            {
                RefreshNeutralNpcView();
            }
        }

        private void ConfigureCaptureMineButton(MapCellSelection selection)
        {
            if (_contextCaptureMineButton == null)
                return;

            bool isMine = selection.Content == MapCellContent.NormalMine ||
                          selection.Content == MapCellContent.GoldMine;
            bool alreadyOwned = string.Equals(
                selection.MineOwnerFactionId,
                "player",
                StringComparison.Ordinal);
            MapUnitState selectedUnit = gameplayMap?.SelectedPlayerUnit;
            bool hasSelectedUnit = selectedUnit != null;
            bool unitAlreadyAtMine = hasSelectedUnit &&
                selectedUnit.Coordinate.Equals(selection.Coordinate);
            bool canMoveToMine = hasSelectedUnit &&
                gameplayMap.CanMoveSelectedPlayerUnit(
                    selection.Coordinate,
                    out _);

            SetVisible(_contextCaptureMineButton, isMine);
            _contextCaptureMineButton.SetEnabled(
                isMine && hasSelectedUnit &&
                ((!alreadyOwned && unitAlreadyAtMine) || canMoveToMine));
            _contextCaptureMineButton.text = alreadyOwned
                ? unitAlreadyAtMine
                    ? "채광 거점 방어 중"
                    : "소유 광산으로 이동"
                : "채광·점령한다";
        }

        private void ConfigureCastleButtons(MapCellSelection selection)
        {
            if (_contextCastleActionButton == null ||
                _contextCastleRoleButton == null ||
                _contextSiegeActionButton == null ||
                _contextLootButton == null ||
                _contextPreserveButton == null ||
                _contextAutonomyButton == null)
            {
                return;
            }

            bool isNeutralCastle =
                selection.Content == MapCellContent.NeutralCastle;
            bool isPlayerCastle =
                selection.Content == MapCellContent.PlayerCastle;
            bool isEnemyCastle =
                selection.Content == MapCellContent.EnemyCastle;
            bool isEnemyCapital =
                selection.Content == MapCellContent.EnemyBase;
            bool isPlayerCapital =
                selection.Content == MapCellContent.PlayerBase;
            bool isCastle = isNeutralCastle || isPlayerCastle ||
                isEnemyCastle || isEnemyCapital || isPlayerCapital;
            if (!isCastle)
            {
                SetVisible(_contextCastleActionButton, false);
                SetVisible(_contextCastleRoleButton, false);
                SetVisible(_contextSiegeActionButton, false);
                SetVisible(_contextLootButton, false);
                SetVisible(_contextPreserveButton, false);
                SetVisible(_contextAutonomyButton, false);
                return;
            }

            bool canOrder = !isPlayerCastle && !isPlayerCapital &&
                gameplayMap.CanCaptureOrSiegeSelectedCastle(
                    selection.Coordinate,
                    out _);
            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            bool unitAtCastle = selectedUnit != null &&
                selectedUnit.Coordinate.Equals(selection.Coordinate);

            SetVisible(
                _contextCastleActionButton,
                !isPlayerCastle && !isPlayerCapital);
            _contextCastleActionButton.SetEnabled(canOrder);
            if (isNeutralCastle)
            {
                _contextCastleActionButton.text = unitAtCastle
                    ? "빈 성 점령 시작"
                    : "빈 성으로 이동·점령";
            }
            else if (isEnemyCastle)
            {
                _contextCastleActionButton.text = unitAtCastle
                    ? "적성 공성전 시작"
                    : "적성으로 이동·공성 준비";
            }

            if (isEnemyCapital)
            {
                _contextCastleActionButton.text = unitAtCastle
                    ? "적 수도 공성 시작"
                    : "적 수도로 이동·공성 준비";
            }

            SetVisible(_contextCastleRoleButton, isPlayerCastle);
            _contextCastleRoleButton.SetEnabled(
                isPlayerCastle &&
                selection.CastleConflictKind != MapCastleConflictKind.Siege);
            _contextCastleRoleButton.text =
                "거점 역할: " +
                MapCastleRoleNames.GetKoreanName(selection.CastleRole) +
                " · 클릭해 변경";

            MapCastleControlState castle = gameplayMap.FindCastleAt(
                selection.Coordinate);
            bool canChooseSiegeAction = castle != null &&
                castle.IsUnderSiege &&
                selectedUnit != null &&
                selectedUnit.Coordinate.Equals(selection.Coordinate) &&
                string.Equals(
                    castle.CapturingFactionId,
                    "player",
                    StringComparison.Ordinal);
            SetVisible(_contextSiegeActionButton, canChooseSiegeAction);
            _contextSiegeActionButton.SetEnabled(canChooseSiegeAction);
            _contextSiegeActionButton.text = canChooseSiegeAction
                ? "공성 행동: " +
                  MapSiegeActionNames.GetKoreanName(castle.SiegeAction) +
                  " · 클릭해 변경"
                : "공성 행동 선택";

            bool canChooseOccupationPolicy = isPlayerCastle &&
                castle != null &&
                castle.OccupationPolicy == MapOccupationPolicy.None;
            SetVisible(_contextLootButton, canChooseOccupationPolicy);
            SetVisible(_contextPreserveButton, canChooseOccupationPolicy);
            SetVisible(_contextAutonomyButton, canChooseOccupationPolicy);
            _contextLootButton.SetEnabled(canChooseOccupationPolicy);
            _contextPreserveButton.SetEnabled(canChooseOccupationPolicy);
            _contextAutonomyButton.SetEnabled(canChooseOccupationPolicy);
        }

        private void ConfigureEconomicDevelopmentButtons(
            MapCellSelection selection)
        {
            if (_contextEconomicSurveyButton == null ||
                _contextBuildMineButton == null ||
                gameplayMap?.GameplayService == null ||
                gameplayMap.CurrentLayout == null)
            {
                return;
            }

            GridCoordinate coordinate = selection.Coordinate;
            bool emptyLand = selection.Content == MapCellContent.Empty &&
                gameplayMap.CurrentLayout.IsLand(coordinate);
            MapEconomicSurveyState survey =
                gameplayMap.GameplayService.FindEconomicSurvey(coordinate);
            MapMineConstructionState construction =
                gameplayMap.GameplayService.FindMineConstruction(coordinate);

            bool showSurvey = emptyLand && survey == null && construction == null;
            SetVisible(_contextEconomicSurveyButton, showSurvey);
            bool canSurvey = showSurvey &&
                gameplayMap.CanSurveySelectedEconomicSite(coordinate, out _) &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(
                    MapEconomicDevelopmentRules.SurveyCost);
            _contextEconomicSurveyButton.SetEnabled(canSurvey);
            _contextEconomicSurveyButton.text =
                $"경제 탐사 · {MapEconomicDevelopmentRules.SurveyCost:N0}원 · 체력 " +
                $"{MapEconomicDevelopmentRules.SurveyStaminaCost}";

            bool showBuild = emptyLand &&
                (construction != null || survey?.HasViableDeposit == true);
            SetVisible(_contextBuildMineButton, showBuild);
            if (!showBuild)
                return;

            if (construction != null)
            {
                string buildingName = construction.Kind == MineKind.Gold
                    ? "금광"
                    : "철광산";
                _contextBuildMineButton.text =
                    $"{buildingName} 건설 중 · 남은 " +
                    $"{construction.RemainingDays}/{construction.TotalDays}일";
                _contextBuildMineButton.SetEnabled(false);
                return;
            }

            MineKind kind = survey.DepositKind.Value;
            decimal cost =
                MapEconomicDevelopmentRules.GetConstructionCost(kind);
            int days = MapEconomicDevelopmentRules.GetConstructionDays(kind);
            string mineName = kind == MineKind.Gold ? "금광" : "철광산";
            bool canBuild = gameplayMap.CanStartSelectedMineConstruction(
                    coordinate,
                    out _,
                    out _) &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(cost);
            _contextBuildMineButton.text =
                $"{mineName} 건설 · {cost:N0}원 · {days}일";
            _contextBuildMineButton.SetEnabled(canBuild);
        }

        private void SetSelectedOccupationPolicy(MapOccupationPolicy policy)
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            GridCoordinate coordinate =
                gameplayMap.CurrentSelection.Value.Coordinate;
            if (!gameplayMap.TrySetPlayerOccupationPolicy(
                    coordinate,
                    policy,
                    out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            SetMapActionFeedback(
                $"{coordinate} 성의 점령 정책을 " +
                $"{MapOccupationPolicyNames.GetKoreanName(policy)}(으)로 " +
                "확정했습니다.");
            RefreshSelectedMapActions();
            HideMapContextMenu();
        }

        private void CycleSelectedSiegeAction()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            GridCoordinate coordinate =
                gameplayMap.CurrentSelection.Value.Coordinate;
            MapCastleControlState castle = gameplayMap.FindCastleAt(coordinate);
            if (castle == null)
                return;

            MapSiegeAction[] order =
            {
                MapSiegeAction.Assault,
                MapSiegeAction.Encirclement,
                MapSiegeAction.Blockade,
                MapSiegeAction.Negotiation
            };
            int currentIndex = Array.IndexOf(order, castle.SiegeAction);
            for (int offset = 1; offset <= order.Length; offset++)
            {
                MapSiegeAction next = order[
                    (Math.Max(-1, currentIndex) + offset) % order.Length];
                if (!gameplayMap.TrySetSelectedPlayerSiegeAction(
                    coordinate,
                    next,
                    out string reason))
                {
                    if (!string.IsNullOrEmpty(reason) &&
                        !reason.Contains("이미 선택"))
                    {
                        SetMapActionFeedback(reason);
                        return;
                    }
                    continue;
                }

                SetMapActionFeedback(
                    $"{coordinate} 공성 행동을 " +
                    $"{MapSiegeActionNames.GetKoreanName(next)}(으)로 변경했습니다.");
                RefreshSelectedMapActions();
                return;
            }
        }

        private void ConfigureMapActionButtons(
            MapCellSelection selection,
            Button createButton,
            Button selectButton,
            Button moveButton)
        {
            if (gameplayMap == null || createButton == null ||
                selectButton == null || moveButton == null)
            {
                return;
            }

            bool atRecruitmentSite =
                selection.Content == MapCellContent.PlayerBase ||
                selection.Content == MapCellContent.PlayerCastle;
            bool canCreate = atRecruitmentSite &&
                gameplayMap.CanCreatePlayerUnitAt(
                    selection.Coordinate,
                    out _);
            bool canSelect = gameplayMap.CanSelectPlayerUnitAt(
                selection.Coordinate,
                out _);
            bool canMove = gameplayMap.CanMoveSelectedPlayerUnit(
                selection.Coordinate,
                out _);
            bool hasSelectedUnit = gameplayMap.SelectedPlayerUnit != null;
            bool isMine = selection.Content == MapCellContent.NormalMine ||
                          selection.Content == MapCellContent.GoldMine;
            bool isCastle =
                selection.Content == MapCellContent.NeutralCastle ||
                selection.Content == MapCellContent.PlayerCastle ||
                selection.Content == MapCellContent.EnemyCastle;
            bool isFriendlyCastle =
                selection.Content == MapCellContent.PlayerCastle ||
                selection.Content == MapCellContent.PlayerBase;
            bool usesSeaTransport = canMove &&
                gameplayMap.WillSelectedMoveUseSeaTransport(
                    selection.Coordinate);

            SetVisible(createButton, atRecruitmentSite);
            createButton.SetEnabled(canCreate);
            createButton.text = selection.Content == MapCellContent.PlayerCastle
                ? "이 성에서 징병 · 병종/장비 선택"
                : "본사에서 징병 · 병종/장비 선택";
            SetVisible(selectButton, canSelect);
            selectButton.SetEnabled(canSelect);
            // A mine uses the single consolidated capture button in the
            // right-click menu. Showing the generic move action as well made
            // both buttons issue effectively the same order.
            SetVisible(
                moveButton,
                hasSelectedUnit && !canSelect && !isMine &&
                (!isCastle || isFriendlyCastle));
            moveButton.SetEnabled(canMove);
            moveButton.text = usesSeaTransport
                    ? "아군 항구로 해상 이동 · 자동 승선/하선"
                : gameplayMap.SelectedPlayerUnit?.IsMoving == true
                    ? "새 목적지로 변경 · 추가 체력 없음"
                : "이 칸으로 이동 · 체력 1";
        }

        private void ConfigureCancelMoveButton(Button cancelButton)
        {
            if (cancelButton == null || gameplayMap == null)
                return;

            bool canCancel = gameplayMap.CanCancelSelectedPlayerUnitMove(
                out _);
            SetVisible(cancelButton, canCancel);
            cancelButton.SetEnabled(canCancel);
            cancelButton.text = "현재 이동 명령 취소";
        }

        private void CaptureOrSiegeSelectedCastle()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            MapUnitState selectedUnit = gameplayMap.SelectedPlayerUnit;
            bool alreadyAtCastle = selectedUnit != null &&
                selectedUnit.Coordinate.Equals(selection.Coordinate);
            if (!gameplayMap.TryCaptureOrSiegeSelectedCastle(
                selection.Coordinate,
                out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            if (!string.IsNullOrEmpty(reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            bool siege = selection.Content == MapCellContent.EnemyCastle;
            SetMapActionFeedback(alreadyAtCastle
                ? siege
                    ? "적성 공성을 시작했습니다. 수비대가 있으면 전투 판정을 기다립니다."
                    : "빈 성 점령을 시작했습니다. 점령이 끝날 때까지 주둔하세요."
                : siege
                    ? $"{selection.Coordinate} 적성으로 이동합니다. 도착하면 공성 대상으로 전환됩니다."
                    : $"{selection.Coordinate} 빈 성으로 이동합니다. 도착하면 점령을 시작합니다.");
        }

        private void CycleSelectedCastleRole()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            MapCastleRole[] order =
            {
                MapCastleRole.SupplyHub,
                MapCastleRole.IndustrialCity,
                MapCastleRole.MilitaryFortress,
                MapCastleRole.Port
            };
            int currentIndex = Array.IndexOf(order, selection.CastleRole);
            string lastReason = string.Empty;
            for (int offset = 1; offset <= order.Length; offset++)
            {
                int nextIndex = (currentIndex + offset) % order.Length;
                MapCastleRole nextRole = order[nextIndex];
                if (!gameplayMap.TrySetPlayerCastleRole(
                    selection.Coordinate,
                    nextRole,
                    out lastReason))
                {
                    continue;
                }

                SetMapActionFeedback(
                    $"{selection.Coordinate} 성을 " +
                    $"{MapCastleRoleNames.GetKoreanName(nextRole)}(으)로 " +
                    "운영합니다.");
                if (gameplayMap.CurrentSelection.HasValue)
                    ConfigureCastleButtons(gameplayMap.CurrentSelection.Value);
                return;
            }

            SetMapActionFeedback(lastReason);
        }

        private void ShowSelectedMissionInformation()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            bool neutralCastle =
                selection.Content == MapCellContent.NeutralCastle;
            SetMapActionFeedback(neutralCastle
                ? $"{selection.DisplayName}: 현재 주인이 없는 확장 거점입니다. " +
                  "유닛을 이동시켜 정찰할 수 있으며, 실제 점령·주둔·보급 " +
                  "전환은 공성 시스템 단계에서 연결합니다."
                : $"{selection.DisplayName}: 정찰·봉쇄·공격 미션 대상입니다. " +
                  "유닛을 선택한 뒤 우클릭 메뉴에서 목표 위치로 이동하세요.");
            if (_mapContextHint != null)
            {
                _mapContextHint.text = neutralCastle
                    ? "빈 성 확보 준비: 유닛 선택 → 빈 성으로 이동 → " +
                      "향후 점령·주둔 명령"
                    : "미션 준비: 유닛 선택 → 목표로 이동 → 도착 후 임무 수행";
            }
        }

        private void AssignSelectedSupplyMission(
            MapSupplyMissionKind missionKind)
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            GridCoordinate coordinate =
                gameplayMap.CurrentSelection.Value.Coordinate;
            if (!gameplayMap.TryAssignSelectedPlayerSupplyMission(
                    coordinate,
                    missionKind,
                    out string reason))
            {
                SetMapActionFeedback(reason);
                return;
            }

            SetMapActionFeedback(
                $"선택 부대에 {coordinate} " +
                $"{MapSupplyMissionNames.GetKoreanName(missionKind)} 임무를 " +
                "지정했습니다. 목표 칸 도착 후 일일 물류 판정에 참여합니다.");
            HideMapContextMenu();
        }

        private void SetMapActionFeedback(string message)
        {
            if (_singleMapActionFeedback != null)
                _singleMapActionFeedback.text = message ?? string.Empty;
        }

        private void HandleMultiplayerError(string message)
        {
            if (_selection.IsMultiplayer)
            {
                if (_multiplayerView != null &&
                    _multiplayerView.resolvedStyle.display == DisplayStyle.Flex)
                {
                    _multiplayerStatus.text = message;
                }
                else if (_connectionStatus != null)
                {
                    _connectionStatus.text = message;
                }
            }
        }

        private void RefreshMultiplayerStatus(PvpReconnectDto state)
        {
            if (_multiplayerStatus == null || state == null)
                return;

            string phase = GetMultiplayerPhaseName(state.phase);
            string cash = state.world?.ownCompany != null
                ? $"\n보유 현금 {state.world.ownCompany.cash:N0}"
                : string.Empty;
            string remaining = FormatMultiplayerTurnRemaining(state);
            string realtime = GetRealtimeConnectionName(
                multiplayerSession?.RealtimeConnectionState ??
                PvpRealtimeConnectionState.Stopped);
            string realtimeError =
                multiplayerSession?.RealtimeConnectionState ==
                    PvpRealtimeConnectionState.Reconnecting &&
                !string.IsNullOrWhiteSpace(
                    multiplayerSession.LastRealtimeError)
                    ? " · 자동 재접속 중"
                    : string.Empty;
            _multiplayerStatus.text =
                $"{state.turn}턴 · {phase}\n" +
                $"{FormatMultiplayerPlayerSlots(state.players)} · " +
                $"남은 시간 {remaining}\n" +
                $"실시간 {realtime}{realtimeError} · 상태 버전 {state.revision}" +
                cash;
        }

        private static string FormatMultiplayerPlayerSlots(
            PvpPlayerStateDto[] players)
        {
            if (players == null || players.Length == 0)
                return "P-";

            var orderedPlayers = new List<PvpPlayerStateDto>(players);
            orderedPlayers.Sort((left, right) => left.slot.CompareTo(right.slot));
            var builder = new StringBuilder(players.Length * 10);
            for (int i = 0; i < orderedPlayers.Count; i++)
            {
                if (i > 0)
                    builder.Append(" · ");

                PvpPlayerStateDto player = orderedPlayers[i];
                builder.Append('P')
                    .Append(player.slot + 1);
            }
            return builder.ToString();
        }

        private static string FormatMultiplayerTurnRemaining(
            PvpReconnectDto state)
        {
            if (string.Equals(
                    state.phase,
                    "Finished",
                    StringComparison.Ordinal))
            {
                return "종료";
            }
            if (!DateTimeOffset.TryParse(
                    state.turnDeadlineUtc,
                    out DateTimeOffset deadline))
            {
                return "확인 중";
            }

            int seconds = Math.Max(
                0,
                (int)Math.Ceiling(
                    (deadline - DateTimeOffset.UtcNow).TotalSeconds));
            return $"{seconds / 60:D2}:{seconds % 60:D2}";
        }

        private static string GetRealtimeConnectionName(
            PvpRealtimeConnectionState state)
        {
            switch (state)
            {
                case PvpRealtimeConnectionState.Connected:
                    return "연결됨";
                case PvpRealtimeConnectionState.Connecting:
                    return "연결 중";
                case PvpRealtimeConnectionState.Reconnecting:
                    return "재접속 중";
                default:
                    return "꺼짐";
            }
        }

        private static string GetMultiplayerPhaseName(string phase)
        {
            switch (phase)
            {
                case "Lobby": return "대전 대기실";
                case "Planning": return "명령 계획";
                case "Locked": return "명령 잠금";
                case "Resolving": return "턴 정산";
                case "Finished": return "대전 종료";
                default: return string.IsNullOrWhiteSpace(phase)
                    ? "상태 확인 중"
                    : phase;
            }
        }

        private static VisualElement CreateCard(
            VisualElement root,
            string title,
            string subtitle)
        {
            var card = new VisualElement();
            card.style.width = 640;
            card.style.maxWidth = new Length(92, LengthUnit.Percent);
            card.style.paddingLeft = 42;
            card.style.paddingRight = 42;
            card.style.paddingTop = 36;
            card.style.paddingBottom = 36;
            card.style.backgroundColor = new Color(0.09f, 0.12f, 0.17f, 1f);
            card.style.borderTopLeftRadius = 12;
            card.style.borderTopRightRadius = 12;
            card.style.borderBottomLeftRadius = 12;
            card.style.borderBottomRightRadius = 12;

            var titleLabel = new Label(title);
            titleLabel.style.fontSize = 34;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new Color(0.93f, 0.96f, 1f);
            titleLabel.style.marginBottom = 8;
            card.Add(titleLabel);

            if (!string.IsNullOrWhiteSpace(subtitle))
            {
                var subtitleLabel = new Label(subtitle);
                subtitleLabel.style.fontSize = 17;
                subtitleLabel.style.color = new Color(0.68f, 0.74f, 0.83f);
                subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
                subtitleLabel.style.marginBottom = 24;
                card.Add(subtitleLabel);
            }
            else
            {
                titleLabel.style.marginBottom = 24;
            }
            root.Add(card);
            return card;
        }

        private static Button AddButton(
            VisualElement parent,
            string text,
            Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.focusable = false;
            button.style.height = 52;
            button.style.marginTop = 6;
            button.style.marginBottom = 6;
            button.style.fontSize = 18;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.16f, 0.39f, 0.68f);
            button.style.color = Color.white;
            parent.Add(button);
            return button;
        }

        private void BuildSinglePlayerMapActionPanel(VisualElement parent)
        {
            _singleMapActionPanel = new VisualElement();
            _singleMapActionPanel.name = "single-map-action-panel";
            _singleMapActionPanel.style.paddingLeft = 12;
            _singleMapActionPanel.style.paddingRight = 12;
            _singleMapActionPanel.style.paddingTop = 10;
            _singleMapActionPanel.style.paddingBottom = 10;
            _singleMapActionPanel.style.marginBottom = 12;
            _singleMapActionPanel.style.backgroundColor =
                new Color(0.08f, 0.13f, 0.20f, 0.96f);

            _singleMapActionTitle = new Label("지도 행동 · 선택된 유닛 없음");
            _singleMapActionTitle.style.fontSize = 15;
            _singleMapActionTitle.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            _singleMapActionTitle.style.color = Color.white;
            _singleMapActionTitle.style.marginBottom = 6;
            _singleMapActionPanel.Add(_singleMapActionTitle);

            _createUnitButton = CreateMapActionButton(
                "유닛 생산 · 병종과 장비 선택",
                OpenRecruitmentAtNeutralNpc);
            _unitTypeButton = CreateMapActionButton(
                "창설 병종: 검병 · 클릭해 변경",
                CyclePendingUnitArchetype);
            _selectUnitButton = CreateMapActionButton(
                "이 칸의 아군 유닛 선택",
                SelectPlayerUnit);
            _inspectUnitButton = CreateMapActionButton(
                "이 칸의 부대 자세히 보기",
                InspectUnitAtCurrentSelection);
            _moveUnitButton = CreateMapActionButton(
                "이 칸으로 이동 · 체력 1",
                MoveSelectedPlayerUnit);
            _cancelMoveButton = CreateMapActionButton(
                "현재 이동 명령 취소",
                CancelSelectedPlayerUnitMove);
            _singleMapActionPanel.Add(_unitTypeButton);
            _singleMapActionPanel.Add(_createUnitButton);
            _singleMapActionPanel.Add(_selectUnitButton);
            _singleMapActionPanel.Add(_inspectUnitButton);
            _singleMapActionPanel.Add(_moveUnitButton);
            _singleMapActionPanel.Add(_cancelMoveButton);

            _singleMapActionFeedback = new Label(
                "지도에서 행동할 칸을 선택하세요.");
            _singleMapActionFeedback.name = "single-map-action-feedback";
            _singleMapActionFeedback.style.fontSize = 13;
            _singleMapActionFeedback.style.color =
                new Color(0.70f, 0.80f, 0.92f);
            _singleMapActionFeedback.style.whiteSpace = WhiteSpace.Normal;
            _singleMapActionFeedback.style.marginTop = 7;
            _singleMapActionPanel.Add(_singleMapActionFeedback);
            parent.Add(_singleMapActionPanel);

            SetVisible(_createUnitButton, false);
            SetVisible(_unitTypeButton, false);
            SetVisible(_selectUnitButton, false);
            SetVisible(_inspectUnitButton, false);
            SetVisible(_moveUnitButton, false);
            SetVisible(_cancelMoveButton, false);
        }

        private void BuildMapContextMenu(VisualElement root)
        {
            _mapContextMenu = new VisualElement();
            _mapContextMenu.name = "map-context-menu";
            _mapContextMenu.style.position = Position.Absolute;
            _mapContextMenu.style.width = 320;
            _mapContextMenu.style.paddingLeft = 14;
            _mapContextMenu.style.paddingRight = 14;
            _mapContextMenu.style.paddingTop = 12;
            _mapContextMenu.style.paddingBottom = 12;
            _mapContextMenu.style.backgroundColor =
                new Color(0.035f, 0.065f, 0.105f, 0.98f);
            _mapContextMenu.style.borderTopWidth = 1;
            _mapContextMenu.style.borderBottomWidth = 1;
            _mapContextMenu.style.borderLeftWidth = 1;
            _mapContextMenu.style.borderRightWidth = 1;
            Color borderColor = new Color(0.24f, 0.50f, 0.82f, 1f);
            _mapContextMenu.style.borderTopColor = borderColor;
            _mapContextMenu.style.borderBottomColor = borderColor;
            _mapContextMenu.style.borderLeftColor = borderColor;
            _mapContextMenu.style.borderRightColor = borderColor;

            _mapContextTitle = new Label("지도 행동");
            _mapContextTitle.style.fontSize = 16;
            _mapContextTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
            _mapContextTitle.style.color = Color.white;
            _mapContextTitle.style.marginBottom = 5;
            _mapContextMenu.Add(_mapContextTitle);

            _mapContextHint = new Label();
            _mapContextHint.style.fontSize = 13;
            _mapContextHint.style.color = new Color(0.72f, 0.82f, 0.94f);
            _mapContextHint.style.whiteSpace = WhiteSpace.Normal;
            _mapContextHint.style.marginBottom = 8;
            _mapContextMenu.Add(_mapContextHint);

            _mapContextOptionsScroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = "map-context-options-scroll",
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            _mapContextOptionsScroll.style.flexGrow = 1;
            _mapContextOptionsScroll.style.flexShrink = 1;
            _mapContextOptionsScroll.style.minHeight = 0;
            _mapContextMenu.Add(_mapContextOptionsScroll);

            _contextCreateUnitButton = CreateMapActionButton(
                "유닛 생산 · 병종과 장비 선택",
                () =>
                {
                    HideMapContextMenu();
                    OpenRecruitmentAtNeutralNpc();
                });
            _contextUnitTypeButton = CreateMapActionButton(
                "창설 병종: 검병 · 클릭해 변경",
                CyclePendingUnitArchetype);
            _contextSelectUnitButton = CreateMapActionButton(
                "이 칸의 아군 유닛 선택",
                () =>
                {
                    SelectPlayerUnit();
                    HideMapContextMenu();
                });
            _contextInspectUnitButton = CreateMapActionButton(
                "이 칸의 부대 자세히 보기",
                () =>
                {
                    InspectUnitAtCurrentSelection();
                    HideMapContextMenu();
                });
            _contextMoveUnitButton = CreateMapActionButton(
                "이 칸으로 이동 · 체력 1",
                () =>
                {
                    MoveSelectedPlayerUnit();
                    HideMapContextMenu();
                });
            _contextCancelMoveButton = CreateMapActionButton(
                "현재 이동 명령 취소",
                () =>
                {
                    CancelSelectedPlayerUnitMove();
                    HideMapContextMenu();
                });
            _contextCaptureMineButton = CreateMapActionButton(
                "점령한다",
                () =>
                {
                    CaptureSelectedMine();
                    HideMapContextMenu();
                });
            _contextEconomicSurveyButton = CreateMapActionButton(
                $"경제 탐사 · {MapEconomicDevelopmentRules.SurveyCost:N0}원",
                () =>
                {
                    SurveySelectedEconomicSite();
                    HideMapContextMenu();
                });
            _contextEconomicSurveyButton.name = "map-economic-survey-button";
            _contextBuildMineButton = CreateMapActionButton(
                "채굴소 건설",
                () =>
                {
                    StartSelectedMineConstruction();
                    HideMapContextMenu();
                });
            _contextBuildMineButton.name = "map-build-mine-button";
            _contextCastleActionButton = CreateMapActionButton(
                "빈 성으로 이동·점령",
                () =>
                {
                    CaptureOrSiegeSelectedCastle();
                    HideMapContextMenu();
                });
            _contextCastleRoleButton = CreateMapActionButton(
                "성 역할 선택",
                CycleSelectedCastleRole);
            _contextSiegeActionButton = CreateMapActionButton(
                "공성 행동 선택",
                CycleSelectedSiegeAction);
            _contextLootButton = CreateMapActionButton(
                "점령 정책: 약탈 (성벽·식량 손실)",
                () => SetSelectedOccupationPolicy(MapOccupationPolicy.Loot));
            _contextPreserveButton = CreateMapActionButton(
                "점령 정책: 보존 (치안 안정)",
                () => SetSelectedOccupationPolicy(MapOccupationPolicy.Preserve));
            _contextAutonomyButton = CreateMapActionButton(
                "점령 정책: 자치 (치안·방어 강화)",
                () => SetSelectedOccupationPolicy(MapOccupationPolicy.Autonomy));
            _contextMissionButton = CreateMapActionButton(
                "미션 정보 · 정찰 / 봉쇄 / 공격",
                ShowSelectedMissionInformation);
            _contextSupplyRaidButton = CreateMapActionButton(
                "적 수송대 습격",
                () => AssignSelectedSupplyMission(
                    MapSupplyMissionKind.Raid));
            _contextSupplyBlockadeButton = CreateMapActionButton(
                "적 보급로 차단",
                () => AssignSelectedSupplyMission(
                    MapSupplyMissionKind.Blockade));
            _contextSupplyEscortButton = CreateMapActionButton(
                "아군 수송대 호위",
                () => AssignSelectedSupplyMission(
                    MapSupplyMissionKind.Escort));
            Button closeButton = CreateMapActionButton(
                "닫기",
                HideMapContextMenu);
            closeButton.style.backgroundColor =
                new Color(0.18f, 0.22f, 0.29f, 1f);

            _contextUnitSection = CreateContextMenuSection(
                _mapContextOptionsScroll,
                "context-unit-section",
                "부대 명령",
                _contextUnitTypeButton,
                _contextCreateUnitButton,
                _contextSelectUnitButton,
                _contextInspectUnitButton,
                _contextMoveUnitButton,
                _contextCancelMoveButton);
            _contextEconomySection = CreateContextMenuSection(
                _mapContextOptionsScroll,
                "context-economy-section",
                "소유·경제 거점",
                _contextCaptureMineButton,
                _contextEconomicSurveyButton,
                _contextBuildMineButton,
                _contextCastleActionButton,
                _contextCastleRoleButton);
            _contextSiegeSection = CreateContextMenuSection(
                _mapContextOptionsScroll,
                "context-siege-section",
                "공성·점령 정책",
                _contextSiegeActionButton,
                _contextLootButton,
                _contextPreserveButton,
                _contextAutonomyButton);
            _contextMissionSection = CreateContextMenuSection(
                _mapContextOptionsScroll,
                "context-mission-section",
                "미션·보급",
                _contextMissionButton,
                _contextSupplyRaidButton,
                _contextSupplyBlockadeButton,
                _contextSupplyEscortButton);
            _mapContextMenu.Add(closeButton);
            root.Add(_mapContextMenu);
            RegisterMapInputGuard(_mapContextMenu);
            SetVisible(_mapContextMenu, false);
        }

        private void BuildNeutralNpcInterface(VisualElement root)
        {
            _neutralNpcTopButton = new Button(ToggleNeutralNpcView)
            {
                text = "병영 · 부대/장비/장수"
            };
            _neutralNpcTopButton.focusable = false;
            _neutralNpcTopButton.style.position = Position.Absolute;
            _neutralNpcTopButton.style.top = 16;
            _neutralNpcTopButton.style.left = 440;
            _neutralNpcTopButton.style.right = StyleKeyword.Auto;
            _neutralNpcTopButton.style.width = 310;
            _neutralNpcTopButton.style.height = 50;
            _neutralNpcTopButton.style.fontSize = 17;
            _neutralNpcTopButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            _neutralNpcTopButton.style.backgroundColor =
                new Color(0.42f, 0.27f, 0.12f, 0.98f);
            _neutralNpcTopButton.style.color = Color.white;
            root.Add(_neutralNpcTopButton);

            _neutralNpcView = CreateCard(
                root,
                "병영 · 부대/장비/장수",
                "부대를 편성하고 공용 AI 장수를 소환합니다.");
            _neutralNpcView.name = "neutral-npc-view";
            _neutralNpcView.style.position = Position.Absolute;
            _neutralNpcView.style.top = 78;
            _neutralNpcView.style.left = 440;
            _neutralNpcView.style.right = StyleKeyword.Auto;
            _neutralNpcView.style.width = 500;
            _neutralNpcView.style.maxWidth = new Length(48, LengthUnit.Percent);
            _neutralNpcView.style.paddingLeft = 24;
            _neutralNpcView.style.paddingRight = 24;
            _neutralNpcView.style.paddingTop = 22;
            _neutralNpcView.style.paddingBottom = 22;
            _neutralNpcView.style.backgroundColor =
                new Color(0.075f, 0.085f, 0.105f, 0.98f);

            _npcCommanderPortrait = new VisualElement
            {
                name = "npc-commander-portrait"
            };
            _npcCommanderPortrait.style.width = 118;
            _npcCommanderPortrait.style.height = 118;
            _npcCommanderPortrait.style.alignSelf = Align.Center;
            _npcCommanderPortrait.style.marginBottom = 8;
            _neutralNpcView.Add(_npcCommanderPortrait);

            _neutralNpcSelectionStatus = AddStatus(_neutralNpcView);
            _neutralNpcSelectionStatus.name =
                "neutral-npc-selection-status";
            _npcArchetypeButton = CreateMapActionButton(
                "병종 선택",
                CyclePendingUnitArchetype);
            _npcWeaponButton = CreateMapActionButton(
                "무기 선택",
                CyclePendingWeapon);
            _npcArmorButton = CreateMapActionButton(
                "갑옷 선택",
                CyclePendingArmor);
            _npcRecruitButton = CreateMapActionButton(
                "선택 구성으로 유닛 생산",
                CreatePlayerUnit);
            _npcRecruitButton.style.backgroundColor =
                new Color(0.14f, 0.45f, 0.24f, 1f);
            _npcEquipButton = CreateMapActionButton(
                "선택 부대 장비 변경",
                EquipSelectedPlayerUnit);
            _npcEquipButton.style.backgroundColor =
                new Color(0.45f, 0.31f, 0.12f, 1f);
            _npcCommanderButton = CreateMapActionButton(
                "AI 장수 선택",
                CyclePendingCommander);
            _npcHireCommanderButton = CreateMapActionButton(
                "선택 부대에 장수 소환",
                HirePendingCommander);
            _npcHireCommanderButton.style.backgroundColor =
                new Color(0.34f, 0.20f, 0.50f, 1f);

            _neutralNpcView.Add(_npcArchetypeButton);
            _neutralNpcView.Add(_npcWeaponButton);
            _neutralNpcView.Add(_npcArmorButton);
            _neutralNpcView.Add(_npcRecruitButton);
            _neutralNpcView.Add(_npcEquipButton);
            _neutralNpcView.Add(_npcCommanderButton);
            _neutralNpcView.Add(_npcHireCommanderButton);
            _neutralNpcFeedback = AddStatus(_neutralNpcView);
            AddButton(_neutralNpcView, "닫기", CloseNeutralNpcView);
            MakeCardVerticallyScrollable(
                _neutralNpcView,
                "neutral-npc-scroll");

            RegisterMapInputGuard(_neutralNpcTopButton);
            RegisterMapInputGuard(_neutralNpcView);
            SetVisible(_neutralNpcTopButton, false);
            SetVisible(_neutralNpcView, false);
        }

        private void BuildOperationBoardInterface(VisualElement root)
        {
            _operationBoardTopButton = new Button(ToggleOperationBoard)
            {
                text = "미션·경제 작전 게시판"
            };
            _operationBoardTopButton.focusable = false;
            _operationBoardTopButton.style.position = Position.Absolute;
            _operationBoardTopButton.style.top = 16;
            _operationBoardTopButton.style.left = 770;
            _operationBoardTopButton.style.right = StyleKeyword.Auto;
            _operationBoardTopButton.style.width = 270;
            _operationBoardTopButton.style.height = 50;
            _operationBoardTopButton.style.fontSize = 17;
            _operationBoardTopButton.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            _operationBoardTopButton.style.backgroundColor =
                new Color(0.12f, 0.39f, 0.34f, 0.98f);
            _operationBoardTopButton.style.color = Color.white;
            root.Add(_operationBoardTopButton);

            _operationBoardView = CreateCard(
                root,
                "미션·경제 작전 게시판",
                "세계에서 실제로 발생한 위기와 기회입니다. 해결 방식에 따라 비용·위험·보상과 후속 경제가 달라집니다.");
            _operationBoardView.name = "operation-board-view";
            _operationBoardView.style.position = Position.Absolute;
            _operationBoardView.style.top = 78;
            _operationBoardView.style.left = 610;
            _operationBoardView.style.right = StyleKeyword.Auto;
            _operationBoardView.style.width = 610;
            _operationBoardView.style.maxWidth =
                new Length(52, LengthUnit.Percent);
            _operationBoardView.style.paddingLeft = 24;
            _operationBoardView.style.paddingRight = 24;
            _operationBoardView.style.paddingTop = 22;
            _operationBoardView.style.paddingBottom = 22;
            _operationBoardView.style.backgroundColor =
                new Color(0.055f, 0.105f, 0.105f, 0.98f);

            _operationBoardSummary = AddStatus(_operationBoardView);
            _operationBoardSummary.style.minHeight = 150;
            _nextOperationButton = CreateMapActionButton(
                "다음 작전 보기",
                CycleSelectedOperation);
            _operationAgentButton = CreateMapActionButton(
                "실행 주체: 직접 수행",
                CycleSelectedOperationAgent);
            _operationAgentButton.name = "operation-agent-button";
            _operationApproachButton = CreateMapActionButton(
                "해결 방식 선택",
                CycleSelectedOperationApproach);
            _acceptOperationButton = CreateMapActionButton(
                "선택한 방식으로 작전 준비",
                QueueSelectedOperation);
            _acceptOperationButton.style.backgroundColor =
                new Color(0.10f, 0.48f, 0.30f, 1f);
            _operationBoardView.Add(_nextOperationButton);
            _operationBoardView.Add(_operationAgentButton);
            _operationBoardView.Add(_operationApproachButton);
            _operationBoardView.Add(_acceptOperationButton);
            _operationBoardFeedback = AddStatus(_operationBoardView);
            AddButton(_operationBoardView, "닫기", CloseOperationBoard);
            MakeCardVerticallyScrollable(
                _operationBoardView,
                "operation-board-scroll");

            RegisterMapInputGuard(_operationBoardTopButton);
            RegisterMapInputGuard(_operationBoardView);
            SetVisible(_operationBoardTopButton, false);
            SetVisible(_operationBoardView, false);
        }

        private void OpenOperationBoard()
        {
            if (!_selection.IsSinglePlayer || _operationBoardView == null)
                return;

            CloseNeutralNpcView();
            HideMapContextMenu();
            SetVisible(_operationBoardView, true);
            _operationBoardView.BringToFront();
            if (gameplayMap != null)
                gameplayMap.PointerSelectionBlocked = true;
            RefreshOperationBoard();
            _uiRoot?.schedule.Execute(UpdateResponsiveLayoutFromRoot);
        }

        private void CloseOperationBoard()
        {
            SetVisible(_operationBoardView, false);
            if (gameplayMap != null && !IsPauseMenuOpen())
                gameplayMap.PointerSelectionBlocked = false;
        }

        private void CycleSelectedOperation()
        {
            int offeredCount = CountOfferedOperations();
            if (offeredCount <= 0)
                return;

            _selectedOperationIndex =
                (_selectedOperationIndex + 1) % offeredCount;
            _selectedOperationApproachIndex = 0;
            SelectRecommendedApproachForCurrentAgent();
            RefreshOperationBoard();
        }

        private void ToggleOperationBoard()
        {
            if (IsOperationBoardOpen())
                CloseOperationBoard();
            else
                OpenOperationBoard();
        }

        private void CycleSelectedOperationAgent()
        {
            int agentCount = CountDelegatableCommanders();
            _selectedOperationAgentIndex =
                (_selectedOperationAgentIndex + 1) % (agentCount + 1);
            SelectRecommendedApproachForCurrentAgent();
            RefreshOperationBoard();
        }

        private void CycleSelectedOperationApproach()
        {
            WorldOpportunity opportunity = GetSelectedOfferedOperation();
            if (opportunity == null)
                return;

            var approaches = WorldOperationCatalog.GetApproaches(
                opportunity.Kind);
            _selectedOperationApproachIndex =
                (_selectedOperationApproachIndex + 1) % approaches.Count;
            RefreshOperationBoard();
        }

        private void QueueSelectedOperation()
        {
            if (_hasPendingDelegatedMission)
            {
                SetOperationFeedback(
                    "이미 휘하 지휘관 부대가 지도 미션을 수행 중입니다. " +
                    "도착·수행 결과가 나온 뒤 다음 미션을 위임하세요.");
                return;
            }

            WorldOpportunity opportunity = GetSelectedOfferedOperation();
            if (opportunity == null || singlePlayerSimulation == null)
            {
                SetOperationFeedback("현재 수락할 수 있는 경제 작전이 없습니다.");
                return;
            }

            var approaches = WorldOperationCatalog.GetApproaches(
                opportunity.Kind);
            int approachIndex = Math.Clamp(
                _selectedOperationApproachIndex,
                0,
                approaches.Count - 1);
            WorldOperationApproachProfile profile = approaches[approachIndex];
            MapCommanderState commander = GetSelectedOperationCommander();
            bool queued;
            string reason;
            string executorMessage;
            if (commander == null)
            {
                queued = singlePlayerSimulation.TryQueueWorldIntervention(
                    opportunity.Id,
                    profile.Approach,
                    out reason);
                executorMessage = "직접 지휘";
            }
            else
            {
                MapUnitState unit = GetCommanderUnit(commander);
                if (!SubordinateMissionPlanner.TryCreatePlan(
                    opportunity,
                    commander,
                    unit,
                    profile.Approach,
                    out SubordinateMissionPlan plan,
                    out reason))
                {
                    SetOperationFeedback(reason);
                    RefreshOperationBoard(false);
                    return;
                }

                WorldEventInstance operationEvent = singlePlayerSimulation
                    .CurrentAutonomousWorld?.FindEvent(opportunity.EventId);
                if (!WorldMissionMapBinder.TryBind(
                        opportunity,
                        gameplayMap?.CurrentLayout,
                        gameplayMap?.GameplayService,
                        out WorldMissionMapTarget mapTarget,
                        out reason,
                        profile.Approach,
                        operationEvent?.ResourceId))
                {
                    SetOperationFeedback(reason);
                    RefreshOperationBoard(false);
                    return;
                }

                _pendingDelegatedMissionPlan = plan;
                _hasPendingDelegatedMission = true;
                queued = gameplayMap.TryAssignWorldMission(
                    plan,
                    mapTarget,
                    out reason);
                if (!queued)
                    _hasPendingDelegatedMission = false;
                executorMessage =
                    $"휘하 AI {commander.DisplayName}에게 위임 · " +
                    $"지도 {mapTarget.Coordinate}로 {mapTarget.Action}";
            }

            if (queued)
            {
                _queuedOperationId = opportunity.Id;
                SetOperationFeedback(
                    $"{executorMessage} · {profile.DisplayName} 준비 명령을 " +
                    "등록했습니다. " +
                    (commander == null
                        ? "다음 경제 정산 때 결과가 확정됩니다."
                        : "부대가 실제 목표에서 임무를 수행한 뒤 경제 정산됩니다."));
            }
            else
            {
                SetOperationFeedback(reason);
            }

            RefreshOperationBoard(false);
            RefreshSinglePlayerStatus();
        }

        private void HandleWorldMissionReady(MapWorldMissionState mission)
        {
            if (!_selection.IsSinglePlayer ||
                !_hasPendingDelegatedMission ||
                !string.Equals(
                    mission.OpportunityId,
                    _pendingDelegatedMissionPlan.OpportunityId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    mission.UnitId,
                    _pendingDelegatedMissionPlan.UnitId,
                    StringComparison.Ordinal))
                return;

            bool queued = singlePlayerSimulation
                .TryQueueDelegatedWorldIntervention(
                    mission.OpportunityId,
                    AddMapExecutionBonus(
                        _pendingDelegatedMissionPlan,
                        mission.ExecutionBonus),
                    out string reason);
            gameplayMap.CompleteWorldMission(
                mission.UnitId,
                mission.OpportunityId,
                !queued);
            _hasPendingDelegatedMission = false;
            SetOperationFeedback(queued
                ? $"{mission.Target} 도착 · {mission.Action} 수행 완료. " +
                  (mission.DeliveredCargo > 0m
                      ? $"화물 {mission.DeliveredCargo:N1} 전달 · "
                      : mission.SabotageDamage > 0
                          ? $"시설 피해 {mission.SabotageDamage:N0} · "
                          : string.Empty) +
                  "다음 경제 정산에 보급·생산·시장 결과가 반영됩니다."
                : $"지도 임무 도착 후 경제 명령 등록 실패: {reason}");
            RefreshOperationBoard(false);
        }

        private static SubordinateMissionPlan AddMapExecutionBonus(
            SubordinateMissionPlan plan,
            decimal bonus)
        {
            return new SubordinateMissionPlan(
                plan.OpportunityId,
                plan.CommanderId,
                plan.CommanderDisplayName,
                plan.UnitId,
                plan.Approach,
                Math.Min(150m, plan.Capability + Math.Max(0m, bonus)),
                plan.UnitReadiness,
                plan.IsRecommendedApproach);
        }

        private void RefreshOperationBoard(bool refreshFeedback = true)
        {
            if (_operationBoardSummary == null)
                return;

            WorldOpportunity queuedOperation = singlePlayerSimulation?
                .CurrentAutonomousWorld?.FindOpportunity(_queuedOperationId);
            if (queuedOperation == null ||
                queuedOperation.Status != WorldOpportunityStatus.Offered)
            {
                _queuedOperationId = string.Empty;
            }

            int offeredCount = CountOfferedOperations();
            WorldOpportunity opportunity = GetSelectedOfferedOperation();
            if (opportunity == null)
            {
                _operationBoardSummary.text =
                    "현재 공개된 작전이 없습니다.\n" +
                    "시장 부족·광산 사고·도적·공장 장애 같은 실제 세계 문제가 " +
                    "발생하면 계약이 올라옵니다.";
                _nextOperationButton.SetEnabled(false);
                _operationAgentButton.SetEnabled(false);
                _operationApproachButton.SetEnabled(false);
                _acceptOperationButton.SetEnabled(false);
                if (refreshFeedback)
                    RefreshLastOperationFeedback();
                return;
            }

            var approaches = WorldOperationCatalog.GetApproaches(
                opportunity.Kind);
            _selectedOperationApproachIndex = Math.Clamp(
                _selectedOperationApproachIndex,
                0,
                approaches.Count - 1);
            WorldOperationApproachProfile profile =
                approaches[_selectedOperationApproachIndex];
            int commanderCount = CountDelegatableCommanders();
            _selectedOperationAgentIndex = Math.Clamp(
                _selectedOperationAgentIndex,
                0,
                commanderCount);
            MapCommanderState commander = GetSelectedOperationCommander();
            MapUnitState commanderUnit = GetCommanderUnit(commander);
            SubordinateMissionPlan delegationPlan = default;
            bool delegationValid = commander == null ||
                SubordinateMissionPlanner.TryCreatePlan(
                    opportunity,
                    commander,
                    commanderUnit,
                    profile.Approach,
                    out delegationPlan,
                    out _);
            string executorSummary;
            if (commander == null)
            {
                executorSummary = "실행 주체: 플레이어 직접 지휘";
            }
            else
            {
                WorldOperationApproach recommended =
                    SubordinateMissionPlanner.GetRecommendedApproach(
                        opportunity,
                        commander,
                        commanderUnit);
                WorldOperationCatalog.TryGet(
                    opportunity.Kind,
                    recommended,
                    out WorldOperationApproachProfile recommendedProfile);
                executorSummary = delegationValid
                    ? $"실행 주체: 휘하 AI {commander.DisplayName} · " +
                      $"{commanderUnit.Id}\n" +
                      $"성향 {MapCommanderPersonalityNames.GetKoreanName(commander.Personality)} · " +
                      $"충성 {commander.Loyalty} · 부대 준비도 " +
                      $"{delegationPlan.UnitReadiness:F0} · 작전 능력 " +
                      $"{delegationPlan.Capability:F0}\n" +
                      $"AI 권장 방식: {recommendedProfile.DisplayName}" +
                      (delegationPlan.IsRecommendedApproach
                          ? " · 현재 선택과 일치"
                          : " · 현재 선택은 지휘관 판단과 다름")
                    : $"실행 주체: {commander.DisplayName} · 위임 불가";
            }
            decimal upfrontCost = WorldOperationCatalog.CalculateUpfrontCost(
                opportunity,
                profile);
            decimal estimatedReward = opportunity.MoneyReward *
                profile.MoneyRewardMultiplier;
            GameCalendarDate deadline = GameCalendarDate.FromDayNumber(
                opportunity.NpcResolveTurn.Value);

            _operationBoardSummary.text =
                $"공개 작전 {offeredCount}건 · 선택 " +
                $"{_selectedOperationIndex + 1}/{offeredCount}\n" +
                $"{opportunity.DisplayName} · {opportunity.RegionId}\n" +
                $"마감 {deadline} · 난도 {opportunity.Difficulty:P0}\n" +
                $"{executorSummary}\n" +
                $"해결 방식: {profile.DisplayName}\n" +
                $"{profile.Description}\n" +
                $"준비금 {upfrontCost:N0}원 · 기본 예상 보상 " +
                $"{estimatedReward:N0}원 · 평판 배율 " +
                $"x{profile.ReputationRewardMultiplier:F2}";
            _nextOperationButton.SetEnabled(offeredCount > 1);
            _operationAgentButton.SetEnabled(commanderCount > 0);
            _operationAgentButton.text = commander == null
                ? commanderCount > 0
                    ? $"실행 주체: 직접 수행 · 휘하 지휘관 {commanderCount}명"
                    : "실행 주체: 직접 수행 · 고용 지휘관 없음"
                : $"실행 주체: {commander.DisplayName} AI · 클릭해 변경";
            _operationApproachButton.SetEnabled(approaches.Count > 1);
            _operationApproachButton.text =
                $"해결 방식: {profile.DisplayName} · 클릭해 변경";
            bool alreadyQueued = string.Equals(
                _queuedOperationId,
                opportunity.Id,
                StringComparison.Ordinal);
            _acceptOperationButton.text = alreadyQueued
                ? "이 작전은 다음 정산에 실행됩니다"
                : $"{profile.DisplayName} 준비 · {upfrontCost:N0}원";
            _acceptOperationButton.SetEnabled(
                !alreadyQueued &&
                delegationValid &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.PlayerCash >= upfrontCost &&
                singlePlayerSimulation.RemainingActionPoints >= 2);
            if (refreshFeedback)
                RefreshLastOperationFeedback();
        }

        private int CountOfferedOperations()
        {
            var world = singlePlayerSimulation?.CurrentAutonomousWorld;
            if (world == null)
                return 0;

            int count = 0;
            for (int i = 0; i < world.Opportunities.Count; i++)
            {
                if (world.Opportunities[i].Status ==
                    WorldOpportunityStatus.Offered)
                {
                    count++;
                }
            }
            return count;
        }

        private WorldOpportunity GetSelectedOfferedOperation()
        {
            var world = singlePlayerSimulation?.CurrentAutonomousWorld;
            if (world == null)
                return null;

            int count = CountOfferedOperations();
            if (count <= 0)
            {
                _selectedOperationIndex = 0;
                return null;
            }
            _selectedOperationIndex = Math.Clamp(
                _selectedOperationIndex,
                0,
                count - 1);

            int offeredIndex = 0;
            for (int i = 0; i < world.Opportunities.Count; i++)
            {
                WorldOpportunity opportunity = world.Opportunities[i];
                if (opportunity.Status != WorldOpportunityStatus.Offered)
                    continue;
                if (offeredIndex == _selectedOperationIndex)
                    return opportunity;
                offeredIndex++;
            }
            return null;
        }

        private int CountDelegatableCommanders()
        {
            IReadOnlyList<MapCommanderState> commanders =
                gameplayMap?.Commanders;
            RealtimeMapGameplayService service = gameplayMap?.GameplayService;
            if (commanders == null || service == null)
                return 0;

            int count = 0;
            for (int i = 0; i < commanders.Count; i++)
            {
                MapCommanderState commander = commanders[i];
                MapUnitState unit = GetCommanderUnit(commander);
                if (unit != null && string.Equals(
                    commander.EmployerFactionId,
                    service.PlayerFactionId,
                    StringComparison.Ordinal))
                {
                    count++;
                }
            }
            return count;
        }

        private MapCommanderState GetSelectedOperationCommander()
        {
            if (_selectedOperationAgentIndex <= 0)
                return null;

            IReadOnlyList<MapCommanderState> commanders =
                gameplayMap?.Commanders;
            RealtimeMapGameplayService service = gameplayMap?.GameplayService;
            if (commanders == null || service == null)
                return null;

            int candidateIndex = 0;
            for (int i = 0; i < commanders.Count; i++)
            {
                MapCommanderState commander = commanders[i];
                if (GetCommanderUnit(commander) == null || !string.Equals(
                    commander.EmployerFactionId,
                    service.PlayerFactionId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                candidateIndex++;
                if (candidateIndex == _selectedOperationAgentIndex)
                    return commander;
            }
            return null;
        }

        private MapUnitState GetCommanderUnit(MapCommanderState commander)
        {
            if (commander == null || string.IsNullOrEmpty(
                commander.AssignedUnitId))
            {
                return null;
            }
            MapUnitState unit = gameplayMap?.GameplayService?.FindUnit(
                commander.AssignedUnitId);
            return unit?.Commander == commander ? unit : null;
        }

        private void SelectRecommendedApproachForCurrentAgent()
        {
            WorldOpportunity opportunity = GetSelectedOfferedOperation();
            MapCommanderState commander = GetSelectedOperationCommander();
            MapUnitState unit = GetCommanderUnit(commander);
            if (opportunity == null || commander == null || unit == null)
                return;

            WorldOperationApproach recommended =
                SubordinateMissionPlanner.GetRecommendedApproach(
                    opportunity,
                    commander,
                    unit);
            IReadOnlyList<WorldOperationApproachProfile> approaches =
                WorldOperationCatalog.GetApproaches(opportunity.Kind);
            for (int i = 0; i < approaches.Count; i++)
            {
                if (approaches[i].Approach == recommended)
                {
                    _selectedOperationApproachIndex = i;
                    return;
                }
            }
        }

        private void RefreshLastOperationFeedback()
        {
            PlayerInterventionResult? result =
                singlePlayerSimulation?.LastPlayerIntervention;
            if (result.HasValue &&
                !string.IsNullOrWhiteSpace(result.Value.Message))
            {
                SetOperationFeedback(
                    $"최근 결과 · {result.Value.ResolverDisplayName}: " +
                    $"{result.Value.Message}\n" +
                    $"보상 {result.Value.MoneyReward:N0}원 · 평판 " +
                    $"{result.Value.ReputationReward:N1} · 준비금 " +
                    $"{result.Value.UpfrontCost:N0}원" +
                    (result.Value.WasDelegated
                        ? " · 휘하 AI 위임 수행"
                        : string.Empty));
            }
            else
            {
                SetOperationFeedback(
                    "전투 외에도 협상·물류·기술·비밀공작으로 해결할 수 있습니다.");
            }
        }

        private void SetOperationFeedback(string message)
        {
            if (_operationBoardFeedback != null)
                _operationBoardFeedback.text = message ?? string.Empty;
        }

        private void OpenRecruitmentAtNeutralNpc()
        {
            if (!_selection.IsSinglePlayer)
                return;

            if (gameplayMap?.CurrentSelection.HasValue == true)
            {
                MapCellSelection selection =
                    gameplayMap.CurrentSelection.Value;
                if (selection.Content == MapCellContent.PlayerBase ||
                    selection.Content == MapCellContent.PlayerCastle)
                {
                    _pendingRecruitmentOrigin = selection.Coordinate;
                }
            }
            if (!_pendingRecruitmentOrigin.HasValue)
                _pendingRecruitmentOrigin = GetRecruitmentOrigin();

            _pendingWeaponType =
                UnitEquipmentCatalog.GetDefaultWeapon(_pendingUnitArchetype);
            SetNeutralNpcFeedback(
                $"{GetRecruitmentOrigin()} 징병소입니다. 병종·무기·갑옷을 " +
                "고른 뒤 유닛 생산을 누르세요.");
            OpenNeutralNpcView(false);
        }

        private void OpenNeutralNpcView()
        {
            _pendingRecruitmentOrigin = gameplayMap?.CurrentLayout?.PlayerStart;
            OpenNeutralNpcView(true);
        }

        private void ToggleNeutralNpcView()
        {
            if (IsNeutralNpcViewOpen())
                CloseNeutralNpcView();
            else
                OpenNeutralNpcView();
        }

        private GridCoordinate GetRecruitmentOrigin()
        {
            if (_pendingRecruitmentOrigin.HasValue)
                return _pendingRecruitmentOrigin.Value;
            return gameplayMap?.CurrentLayout?.PlayerStart ?? default;
        }

        private void OpenNeutralNpcView(bool copySelectedEquipment)
        {
            if (!_selection.IsSinglePlayer || _neutralNpcView == null)
                return;

            MapUnitState selectedUnit = gameplayMap?.SelectedPlayerUnit;
            if (copySelectedEquipment && selectedUnit != null)
            {
                _pendingUnitArchetype = selectedUnit.Archetype;
                _pendingWeaponType = selectedUnit.WeaponType;
                _pendingArmorClass = selectedUnit.ArmorClass;
                SetNeutralNpcFeedback(
                    "선택 부대의 현재 장비를 불러왔습니다. 변경할 장비를 고르세요.");
            }
            else if (copySelectedEquipment)
            {
                SetNeutralNpcFeedback(
                    "선택 부대가 없습니다. 새 용병을 모집할 수 있습니다.");
            }

            CloseOperationBoard();
            HideMapContextMenu();
            SetVisible(_neutralNpcView, true);
            _neutralNpcView.BringToFront();
            if (gameplayMap != null)
                gameplayMap.PointerSelectionBlocked = true;
            RefreshNeutralNpcView();
            _uiRoot?.schedule.Execute(UpdateResponsiveLayoutFromRoot);
        }

        private void CloseNeutralNpcView()
        {
            SetVisible(_neutralNpcView, false);
            if (gameplayMap != null && !IsPauseMenuOpen())
                gameplayMap.PointerSelectionBlocked = false;
        }

        private void RefreshNeutralNpcView()
        {
            if (_neutralNpcSelectionStatus == null)
                return;

            string archetypeName =
                MapUnitState.GetArchetypeDisplayName(_pendingUnitArchetype);
            string weaponName =
                UnitEquipmentCatalog.GetWeaponDisplayName(_pendingWeaponType);
            string armorName =
                UnitEquipmentCatalog.GetArmorDisplayName(_pendingArmorClass);
            decimal recruitCost = UnitEquipmentCatalog.GetRecruitmentCost(
                _pendingUnitArchetype,
                _pendingWeaponType,
                _pendingArmorClass);
            decimal equipmentCost = UnitEquipmentCatalog.GetEquipmentCost(
                _pendingWeaponType,
                _pendingArmorClass);
            decimal attackModifier =
                MapUnitState.GetArchetypeAttackModifier(
                    _pendingUnitArchetype) *
                UnitEquipmentCatalog.GetAttackModifier(_pendingWeaponType);
            decimal defenseModifier =
                MapUnitState.GetArchetypeDefenseModifier(
                    _pendingUnitArchetype) *
                UnitEquipmentCatalog.GetDefenseModifier(_pendingArmorClass);
            decimal mobilityModifier =
                UnitEquipmentCatalog.GetMobilityModifier(
                    _pendingUnitArchetype,
                    _pendingArmorClass);
            MapUnitState selectedUnit = gameplayMap?.SelectedPlayerUnit;
            MapCommanderState pendingCommander = GetPendingCommander();
            MapCommanderState portraitCommander =
                selectedUnit?.Commander ?? pendingCommander;
            if (_npcCommanderPortrait != null)
            {
                if (portraitCommander == null)
                {
                    _npcCommanderPortrait.style.display = DisplayStyle.None;
                    _npcCommanderPortrait.tooltip = string.Empty;
                }
                else
                {
                    string portraitPath = portraitCommander.IsProtagonist
                        ? "CommanderPortraits/protagonist_commander"
                        : "CommanderPortraits/ai_commander";
                    Texture2D portraitTexture =
                        Resources.Load<Texture2D>(portraitPath);
                    _npcCommanderPortrait.style.backgroundImage =
                        new StyleBackground(portraitTexture);
                    _npcCommanderPortrait.style.display = DisplayStyle.Flex;
                    _npcCommanderPortrait.tooltip =
                        portraitCommander.DisplayName + " · " +
                        MapCommanderPersonalityNames.GetKoreanName(
                            portraitCommander.Personality);
                }
            }
            string commanderCandidateList = BuildCommanderCandidateList();
            GridCoordinate recruitOrigin = GetRecruitmentOrigin();
            string recruitmentStatus = "징병소 정보 없음";
            MapCastleControlState recruitmentCastle =
                gameplayMap?.FindCastleAt(recruitOrigin);
            int requiredHorses = _pendingUnitArchetype ==
                UnitArchetype.Cavalry
                    ? gameplayMap?.GameplayService?
                        .InitialSoldiersPerUnit ?? 100
                    : 0;
            bool hasRequiredHorses = requiredHorses == 0 ||
                recruitmentCastle?.WarehouseHorseAmount >= requiredHorses;
            if (gameplayMap != null &&
                gameplayMap.TryGetPlayerRecruitmentSite(
                    recruitOrigin,
                    out MapRecruitmentSiteSnapshot recruitmentSite))
            {
                recruitmentStatus =
                    $"주둔 {recruitmentSite.GarrisonUnitCount}/" +
                    $"{recruitmentSite.GarrisonCapacity} · 징집 인력 " +
                    $"{recruitmentSite.AvailableRecruits}/" +
                    recruitmentSite.RecruitmentCapacity +
                    $" · 말 {recruitmentCastle?.WarehouseHorseAmount ?? 0m:N0}";
            }

            _neutralNpcSelectionStatus.text =
                $"징병 위치: {recruitOrigin} · {recruitmentStatus}\n" +
                $"구성: {archetypeName} · {weaponName} · {armorName}\n" +
                $"능력 배율: 공격 x{attackModifier:F2} · " +
                $"방어 x{defenseModifier:F2} · " +
                $"기동 x{mobilityModifier:F2}\n" +
                $"모집비 {recruitCost:N0} · 장비 구입비 {equipmentCost:N0}\n" +
                (selectedUnit == null
                    ? "장비 변경 대상: 선택된 부대 없음"
                    : $"장비 변경 대상: {selectedUnit.ArchetypeDisplayName} {selectedUnit.Id}") +
                (pendingCommander == null
                    ? "\n공용 AI 장수 후보 없음"
                    : $"\n장수 후보: {pendingCommander.DisplayName} · " +
                      $"통솔 {pendingCommander.Command} / 전술 {pendingCommander.Tactics} / " +
                      $"병참 {pendingCommander.Logistics} · " +
                      $"{MapCommanderPersonalityNames.GetKoreanName(pendingCommander.Personality)} · " +
                      $"충성 {pendingCommander.Loyalty} · " +
                      $"소환비 {pendingCommander.HiringCost:N0}") +
                "\n" + commanderCandidateList;

            _npcArchetypeButton.text = $"병종: {archetypeName} · 클릭해 변경";
            _npcWeaponButton.text = $"무기: {weaponName} · 클릭해 변경";
            _npcArmorButton.text = $"갑옷: {armorName} · 클릭해 변경";
            _npcRecruitButton.text = $"유닛 생산 · {recruitCost:N0}";
            _npcEquipButton.text = selectedUnit == null
                ? "선택 부대 장비 변경"
                : $"{selectedUnit.Id} 장비 변경 · {equipmentCost:N0}";
            _npcCommanderButton.text = pendingCommander == null
                ? "공용 AI 장수 후보 없음"
                : $"장수: {pendingCommander.DisplayName} · 클릭해 변경";
            _npcHireCommanderButton.text = pendingCommander == null
                ? "소환할 장수 없음"
                : selectedUnit?.Commander != null
                    ? $"{selectedUnit.Commander.DisplayName} 지휘 중"
                    : $"{pendingCommander.DisplayName} 소환 · " +
                      $"{pendingCommander.HiringCost:N0}";

            bool canCreate = gameplayMap != null &&
                gameplayMap.CanCreatePlayerUnitAt(recruitOrigin, out _) &&
                hasRequiredHorses &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(recruitCost);
            bool sameEquipment = selectedUnit != null &&
                selectedUnit.WeaponType == _pendingWeaponType &&
                selectedUnit.ArmorClass == _pendingArmorClass;
            bool canEquip = selectedUnit != null &&
                !sameEquipment &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(equipmentCost);
            bool commanderAssignmentAllowed = pendingCommander != null &&
                gameplayMap != null &&
                gameplayMap.CanHireCommanderForSelectedPlayerUnit(
                    pendingCommander.Id,
                    out _);
            bool canHireCommander = commanderAssignmentAllowed &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(
                    pendingCommander.HiringCost);
            _npcRecruitButton.SetEnabled(canCreate);
            _npcEquipButton.SetEnabled(canEquip);
            _npcCommanderButton.SetEnabled(
                GetCommanderCandidates().Count > 1);
            _npcHireCommanderButton.SetEnabled(canHireCommander);
        }

        private void SetNeutralNpcFeedback(string message)
        {
            if (_neutralNpcFeedback != null)
                _neutralNpcFeedback.text = message ?? string.Empty;
        }

        private void BuildTimeHudAndPauseMenu(VisualElement root)
        {
            _timeHudView = new VisualElement
            {
                name = "time-hud"
            };
            _timeHudView.style.position = Position.Absolute;
            _timeHudView.style.top = 16;
            _timeHudView.style.right = 24;
            _timeHudView.style.width = 400;
            _timeHudView.style.paddingLeft = 14;
            _timeHudView.style.paddingRight = 14;
            _timeHudView.style.paddingTop = 12;
            _timeHudView.style.paddingBottom = 12;
            _timeHudView.style.backgroundColor =
                new Color(0.045f, 0.065f, 0.095f, 0.96f);
            _timeHudView.style.borderTopLeftRadius = 10;
            _timeHudView.style.borderTopRightRadius = 10;
            _timeHudView.style.borderBottomLeftRadius = 10;
            _timeHudView.style.borderBottomRightRadius = 10;

            _timeHudLabel = new Label
            {
                name = "time-label"
            };
            _timeHudLabel.style.fontSize = 16;
            _timeHudLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _timeHudLabel.style.color = Color.white;
            _timeHudLabel.style.whiteSpace = WhiteSpace.Normal;
            _timeHudLabel.style.marginBottom = 7;
            _timeHudView.Add(_timeHudLabel);
            AddRealtimeSpeedControls(_timeHudView);
            root.Add(_timeHudView);

            _pauseMenuOverlay = new VisualElement
            {
                name = "pause-menu-overlay"
            };
            _pauseMenuOverlay.style.position = Position.Absolute;
            _pauseMenuOverlay.style.left = 0;
            _pauseMenuOverlay.style.right = 0;
            _pauseMenuOverlay.style.top = 0;
            _pauseMenuOverlay.style.bottom = 0;
            _pauseMenuOverlay.style.flexDirection = FlexDirection.Column;
            _pauseMenuOverlay.style.alignItems = Align.Center;
            _pauseMenuOverlay.style.justifyContent = Justify.Center;
            _pauseMenuOverlay.style.backgroundColor =
                new Color(0.015f, 0.022f, 0.035f, 0.82f);

            _pauseMenuView = CreateCard(
                _pauseMenuOverlay,
                "일시정지",
                string.Empty);
            _pauseMenuView.name = "pause-menu";
            _pauseMenuView.style.width = 460;
            AddButton(_pauseMenuView, "계속하기", ClosePauseMenu);
            AddButton(_pauseMenuView, "키 설정", OpenKeySettings);
            AddButton(
                _pauseMenuView,
                "모드 선택으로 돌아가기",
                ReturnToModeSelectionFromPause);

            _keySettingsView = CreateCard(
                _pauseMenuOverlay,
                "키 설정",
                string.Empty);
            _keySettingsView.name = "key-settings-menu";
            _keySettingsView.style.width = 560;
            AddDescription(
                _keySettingsView,
                "일시정지  Esc\n" +
                "시간 정지  Space\n" +
                "지도 이동  WASD / 방향키\n" +
                "확대·축소  마우스 휠\n" +
                "본사 이동  L\n" +
                "선택  좌클릭\n" +
                "행동 메뉴  우클릭");
            AddButton(_keySettingsView, "뒤로", CloseKeySettings);

            root.Add(_pauseMenuOverlay);
            RegisterMapInputGuard(_timeHudView);
            RegisterMapInputGuard(_pauseMenuOverlay);
            SetVisible(_timeHudView, false);
            SetVisible(_keySettingsView, false);
            SetVisible(_pauseMenuOverlay, false);
        }

        private void HandleEscapePressed()
        {
            if (IsKeySettingsOpen())
            {
                CloseKeySettings();
                return;
            }

            if (IsOperationBoardOpen())
            {
                CloseOperationBoard();
                return;
            }

            if (IsNeutralNpcViewOpen())
            {
                CloseNeutralNpcView();
                return;
            }

            if (_mapContextMenu != null &&
                _mapContextMenu.resolvedStyle.display == DisplayStyle.Flex)
            {
                HideMapContextMenu();
                return;
            }

            if (IsPauseMenuOpen())
                ClosePauseMenu();
            else
                OpenPauseMenu();
        }

        private void OpenPauseMenu()
        {
            if (_pauseMenuOverlay == null || !_selection.HasSelection)
                return;

            HideMapContextMenu();
            CloseNeutralNpcView();
            _resumeRealtimeAfterPauseMenu =
                singlePlayerSimulation != null &&
                !singlePlayerSimulation.IsRealtimePaused;
            if (_resumeRealtimeAfterPauseMenu)
                singlePlayerSimulation.ToggleRealtimePause();

            SetVisible(_pauseMenuView, true);
            SetVisible(_keySettingsView, false);
            SetVisible(_pauseMenuOverlay, true);
            _pauseMenuOverlay.BringToFront();
            if (gameplayMap != null)
                gameplayMap.PointerSelectionBlocked = true;
        }

        private void ClosePauseMenu()
        {
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_pauseMenuView, true);
            SetVisible(_keySettingsView, false);
            if (gameplayMap != null)
                gameplayMap.PointerSelectionBlocked = false;

            if (_resumeRealtimeAfterPauseMenu &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.IsRealtimePaused &&
                !singlePlayerSimulation.IsCampaignFinished)
            {
                singlePlayerSimulation.ToggleRealtimePause();
            }
            _resumeRealtimeAfterPauseMenu = false;
        }

        private void HidePauseMenuWithoutResuming()
        {
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_pauseMenuView, true);
            SetVisible(_keySettingsView, false);
            _resumeRealtimeAfterPauseMenu = false;
        }

        private void ReturnToModeSelectionFromPause()
        {
            _resumeRealtimeAfterPauseMenu = false;
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_pauseMenuView, true);
            SetVisible(_keySettingsView, false);
            ShowModeSelection();
        }

        private void OpenKeySettings()
        {
            if (_keySettingsView == null)
                return;

            SetVisible(_pauseMenuView, false);
            SetVisible(_keySettingsView, true);
        }

        private void CloseKeySettings()
        {
            SetVisible(_keySettingsView, false);
            SetVisible(_pauseMenuView, true);
        }

        private bool IsPauseMenuOpen()
        {
            return _pauseMenuOverlay != null &&
                _pauseMenuOverlay.resolvedStyle.display == DisplayStyle.Flex;
        }

        private bool IsKeySettingsOpen()
        {
            return IsPauseMenuOpen() &&
                _keySettingsView != null &&
                _keySettingsView.resolvedStyle.display == DisplayStyle.Flex;
        }

        private bool IsOperationBoardOpen()
        {
            return _operationBoardView != null &&
                _operationBoardView.resolvedStyle.display == DisplayStyle.Flex;
        }

        private void ApplySelectionOwnerTheme(MapCellSelection selection)
        {
            if (gameplayMap == null)
                return;

            string ownerFactionId = !string.IsNullOrWhiteSpace(
                    selection.UnitOwnerFactionId)
                ? selection.UnitOwnerFactionId
                : !string.IsNullOrWhiteSpace(selection.CastleOwnerFactionId)
                    ? selection.CastleOwnerFactionId
                    : !string.IsNullOrWhiteSpace(selection.MineOwnerFactionId)
                        ? selection.MineOwnerFactionId
                        : selection.CapturingFactionId;
            Color ownerColor = gameplayMap.GetFactionDisplayColor(ownerFactionId);
            Color readableAccent = Color.Lerp(ownerColor, Color.white, 0.22f);

            ApplyOwnerAccent(_singleMapSelectionStatus, ownerColor);
            ApplyOwnerAccent(_multiplayerMapSelectionStatus, ownerColor);
            ApplyOwnerAccent(_singleMapActionPanel, ownerColor);
            if (_singleMapActionTitle != null)
                _singleMapActionTitle.style.color = readableAccent;
            if (_mapContextTitle != null)
                _mapContextTitle.style.color = readableAccent;
            if (_mapContextMenu != null)
            {
                _mapContextMenu.style.borderTopColor = ownerColor;
                _mapContextMenu.style.borderBottomColor = ownerColor;
                _mapContextMenu.style.borderLeftColor = ownerColor;
                _mapContextMenu.style.borderRightColor = ownerColor;
            }
        }

        private static void ApplyOwnerAccent(
            VisualElement element,
            Color ownerColor)
        {
            if (element == null)
                return;

            element.style.borderLeftWidth = 6;
            element.style.borderLeftColor = ownerColor;
        }

        private void RefreshMapContextSections()
        {
            SetVisible(
                _contextUnitSection,
                HasVisibleContextOption(
                    _contextUnitTypeButton,
                    _contextCreateUnitButton,
                    _contextSelectUnitButton,
                    _contextInspectUnitButton,
                    _contextMoveUnitButton,
                    _contextCancelMoveButton));
            SetVisible(
                _contextEconomySection,
                HasVisibleContextOption(
                    _contextCaptureMineButton,
                    _contextEconomicSurveyButton,
                    _contextBuildMineButton,
                    _contextCastleActionButton,
                    _contextCastleRoleButton));
            SetVisible(
                _contextSiegeSection,
                HasVisibleContextOption(
                    _contextSiegeActionButton,
                    _contextLootButton,
                    _contextPreserveButton,
                    _contextAutonomyButton));
            SetVisible(
                _contextMissionSection,
                HasVisibleContextOption(
                    _contextMissionButton,
                    _contextSupplyRaidButton,
                    _contextSupplyBlockadeButton,
                    _contextSupplyEscortButton));
        }

        private static bool HasVisibleContextOption(
            params VisualElement[] options)
        {
            for (int i = 0; i < options.Length; i++)
            {
                if (options[i] == null)
                    continue;

                StyleEnum<DisplayStyle> display = options[i].style.display;
                if (display.keyword == StyleKeyword.Null ||
                    display.value != DisplayStyle.None)
                {
                    return true;
                }
            }
            return false;
        }

        private void PositionMapContextMenu(Vector2 screenPosition)
        {
            if (_uiRoot == null || _mapContextMenu == null)
                return;

            float rootWidth = _uiRoot.resolvedStyle.width;
            float rootHeight = _uiRoot.resolvedStyle.height;
            if (float.IsNaN(rootWidth) || rootWidth <= 0f)
                rootWidth = Screen.width;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f)
                rootHeight = Screen.height;

            float x = screenPosition.x * rootWidth / Mathf.Max(1f, Screen.width);
            float y = (Screen.height - screenPosition.y) * rootHeight /
                      Mathf.Max(1f, Screen.height);
            float menuWidth = Mathf.Min(320f, Mathf.Max(180f, rootWidth - 20f));
            float menuHeight = Mathf.Max(180f, rootHeight - 20f);
            _mapContextMenu.style.width = menuWidth;
            _mapContextMenu.style.maxHeight = menuHeight;
            if (_mapContextOptionsScroll != null)
            {
                _mapContextOptionsScroll.style.maxHeight =
                    Mathf.Max(80f, menuHeight - 150f);
            }
            float estimatedMenuHeight = Mathf.Min(menuHeight, 620f);
            _mapContextMenu.style.left = Mathf.Clamp(
                x + 10f,
                10f,
                Mathf.Max(10f, rootWidth - menuWidth - 10f));
            _mapContextMenu.style.top = Mathf.Clamp(
                y + 10f,
                10f,
                Mathf.Max(10f, rootHeight - estimatedMenuHeight - 10f));
        }

        private void HideMapContextMenu()
        {
            SetVisible(_mapContextMenu, false);
            if (gameplayMap != null && !IsPauseMenuOpen())
                gameplayMap.PointerSelectionBlocked = false;
        }

        private static Button CreateMapActionButton(
            string text,
            Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.focusable = false;
            button.style.height = 38;
            button.style.marginTop = 3;
            button.style.marginBottom = 3;
            button.style.fontSize = 14;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.18f, 0.42f, 0.70f);
            button.style.color = Color.white;
            return button;
        }

        private static VisualElement CreateContextMenuSection(
            ScrollView parent,
            string sectionName,
            string title,
            params VisualElement[] controls)
        {
            var section = new VisualElement
            {
                name = sectionName
            };
            section.style.marginBottom = 7;
            var label = new Label(title);
            label.style.fontSize = 12;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = new Color(0.66f, 0.78f, 0.92f, 1f);
            label.style.marginTop = 3;
            label.style.marginBottom = 3;
            section.Add(label);
            for (int i = 0; i < controls.Length; i++)
            {
                if (controls[i] != null)
                    section.Add(controls[i]);
            }
            parent.Add(section);
            return section;
        }

        private void AddRealtimeSpeedControls(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 8;
            AddSpeedButton(row, "일시정지", ToggleSinglePlayerPause);
            for (int speed = 1;
                 speed <= RealtimeSimulationClock.MaximumSpeedMultiplier;
                 speed++)
            {
                int capturedSpeed = speed;
                AddSpeedButton(
                    row,
                    capturedSpeed + "배",
                    () => SetSinglePlayerSpeed(capturedSpeed));
            }
            parent.Add(row);
        }

        private static void AddSpeedButton(
            VisualElement parent,
            string text,
            Action clicked)
        {
            var button = new Button(clicked) { text = text };
            button.focusable = false;
            button.style.height = 42;
            button.style.minWidth = 58;
            button.style.flexGrow = 1;
            button.style.marginLeft = 3;
            button.style.marginRight = 3;
            button.style.marginTop = 3;
            button.style.marginBottom = 3;
            button.style.fontSize = 14;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.12f, 0.32f, 0.56f);
            button.style.color = Color.white;
            parent.Add(button);
        }

        private static void StyleGameplayHud(VisualElement card)
        {
            card.style.position = Position.Absolute;
            card.style.left = 20;
            card.style.top = 20;
            card.style.right = StyleKeyword.Auto;
            card.style.bottom = StyleKeyword.Auto;
            card.style.width = 420;
            card.style.maxWidth = new Length(46, LengthUnit.Percent);
            card.style.height = new Length(94, LengthUnit.Percent);
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 18;
            card.style.marginLeft = 0;
            card.style.marginTop = 0;
            card.style.backgroundColor =
                new Color(0.07f, 0.095f, 0.14f, 0.92f);
        }

        private static ScrollView MakeCardVerticallyScrollable(
            VisualElement card,
            string scrollName)
        {
            var children = new List<VisualElement>(card.childCount);
            for (int i = 0; i < card.childCount; i++)
                children.Add(card[i]);

            var scroll = new ScrollView(ScrollViewMode.Vertical)
            {
                name = scrollName,
                verticalScrollerVisibility = ScrollerVisibility.Auto
            };
            scroll.style.flexGrow = 1;
            scroll.style.flexShrink = 1;
            scroll.style.minHeight = 0;
            card.style.maxHeight = new Length(92, LengthUnit.Percent);
            card.style.minHeight = 0;
            for (int i = 0; i < children.Count; i++)
            {
                children[i].RemoveFromHierarchy();
                scroll.Add(children[i]);
            }
            card.Add(scroll);
            return scroll;
        }

        private void ConfigureDraggableGameplayPanel(
            VisualElement panel,
            ScrollView scroll,
            string handleName)
        {
            if (panel == null || scroll?.contentContainer == null ||
                scroll.contentContainer.childCount == 0 ||
                !(scroll.contentContainer[0] is Label dragHandle))
            {
                return;
            }

            dragHandle.RemoveFromHierarchy();
            panel.Insert(0, dragHandle);
            dragHandle.name = handleName;
            dragHandle.text += " · 드래그 이동";
            dragHandle.style.paddingTop = 4;
            dragHandle.style.paddingBottom = 8;
            dragHandle.style.marginBottom = 4;
            dragHandle.style.borderBottomWidth = 1;
            dragHandle.style.borderBottomColor =
                new Color(0.24f, 0.50f, 0.82f, 0.85f);

            Vector2 pointerOrigin = Vector2.zero;
            Vector2 panelOrigin = Vector2.zero;
            int capturedPointerId = -1;
            dragHandle.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (evt.button != 0)
                    return;

                capturedPointerId = evt.pointerId;
                pointerOrigin = new Vector2(evt.position.x, evt.position.y);
                float currentLeft = panel.resolvedStyle.left;
                float currentTop = panel.resolvedStyle.top;
                panelOrigin = new Vector2(
                    float.IsNaN(currentLeft) ? 20f : currentLeft,
                    float.IsNaN(currentTop) ? 20f : currentTop);
                panel.BringToFront();
                dragHandle.CapturePointer(capturedPointerId);
                evt.StopPropagation();
            });
            dragHandle.RegisterCallback<PointerMoveEvent>(evt =>
            {
                if (capturedPointerId != evt.pointerId ||
                    !dragHandle.HasPointerCapture(evt.pointerId))
                {
                    return;
                }

                Vector2 current = new Vector2(evt.position.x, evt.position.y);
                SetFloatingPanelPosition(
                    panel,
                    panelOrigin + current - pointerOrigin);
                evt.StopPropagation();
            });
            dragHandle.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (capturedPointerId != evt.pointerId)
                    return;

                if (dragHandle.HasPointerCapture(evt.pointerId))
                    dragHandle.ReleasePointer(evt.pointerId);
                capturedPointerId = -1;
                evt.StopPropagation();
            });
        }

        private void HandleRootGeometryChanged(GeometryChangedEvent evt)
        {
            UpdateResponsiveLayout(evt.newRect.width, evt.newRect.height);
        }

        private void UpdateResponsiveLayoutFromRoot()
        {
            if (_uiRoot == null)
                return;
            UpdateResponsiveLayout(
                _uiRoot.resolvedStyle.width,
                _uiRoot.resolvedStyle.height);
        }

        private void UpdateResponsiveLayout(float rootWidth, float rootHeight)
        {
            if (rootWidth <= 0f || rootHeight <= 0f)
                return;

            bool compact = rootWidth < 1200f;
            float setupPadding = rootWidth < 720f ? 18f : 42f;
            if (_singlePlayerSetupView != null)
            {
                _singlePlayerSetupView.style.paddingLeft = setupPadding;
                _singlePlayerSetupView.style.paddingRight = setupPadding;
            }

            if (_neutralNpcTopButton != null)
            {
                if (compact)
                {
                    _neutralNpcTopButton.style.left = StyleKeyword.Auto;
                    _neutralNpcTopButton.style.right = 16;
                    _neutralNpcTopButton.style.width =
                        Mathf.Min(280f, Mathf.Max(120f, rootWidth - 32f));
                }
                else
                {
                    _neutralNpcTopButton.style.left = 440;
                    _neutralNpcTopButton.style.right = StyleKeyword.Auto;
                    _neutralNpcTopButton.style.width = 310;
                }
            }
            if (_operationBoardTopButton != null)
            {
                if (compact)
                {
                    _operationBoardTopButton.style.left = StyleKeyword.Auto;
                    _operationBoardTopButton.style.right = 16;
                    _operationBoardTopButton.style.top = 74;
                    _operationBoardTopButton.style.width =
                        Mathf.Min(280f, Mathf.Max(120f, rootWidth - 32f));
                }
                else
                {
                    _operationBoardTopButton.style.left = 770;
                    _operationBoardTopButton.style.right = StyleKeyword.Auto;
                    _operationBoardTopButton.style.top = 16;
                    _operationBoardTopButton.style.width = 270;
                }
            }
            if (_timeHudView != null)
            {
                _timeHudView.style.top = compact ? 132 : 16;
                _timeHudView.style.width =
                    Mathf.Min(400f, Mathf.Max(120f, rootWidth - 32f));
            }

            float overlayMaxHeight = Mathf.Max(180f, rootHeight - 98f);
            if (_neutralNpcView != null)
                _neutralNpcView.style.maxHeight = overlayMaxHeight;
            if (_operationBoardView != null)
                _operationBoardView.style.maxHeight = overlayMaxHeight;

            if (_selection.IsSinglePlayer)
                ClampFloatingPanelToRoot(_singlePlayerView);
            else if (_selection.IsMultiplayer)
                ClampFloatingPanelToRoot(_multiplayerView);
            ClampFloatingPanelToRoot(_neutralNpcView);
            ClampFloatingPanelToRoot(_operationBoardView);
            ClampFloatingPanelToRoot(_mapContextMenu);
        }

        private void ClampFloatingPanelToRoot(VisualElement panel)
        {
            if (panel == null ||
                panel.resolvedStyle.display == DisplayStyle.None)
            {
                return;
            }

            float left = panel.resolvedStyle.left;
            float top = panel.resolvedStyle.top;
            SetFloatingPanelPosition(
                panel,
                new Vector2(
                    float.IsNaN(left) ? 8f : left,
                    float.IsNaN(top) ? 8f : top));
        }

        private void SetFloatingPanelPosition(
            VisualElement panel,
            Vector2 proposedPosition)
        {
            if (_uiRoot == null || panel == null)
                return;

            float rootWidth = _uiRoot.resolvedStyle.width;
            float rootHeight = _uiRoot.resolvedStyle.height;
            if (float.IsNaN(rootWidth) || rootWidth <= 0f)
                rootWidth = Screen.width;
            if (float.IsNaN(rootHeight) || rootHeight <= 0f)
                rootHeight = Screen.height;

            float panelWidth = panel.resolvedStyle.width;
            float panelHeight = panel.resolvedStyle.height;
            if (float.IsNaN(panelWidth) || panelWidth <= 0f)
                panelWidth = Mathf.Min(420f, rootWidth - 16f);
            if (float.IsNaN(panelHeight) || panelHeight <= 0f)
                panelHeight = Mathf.Min(rootHeight * 0.94f, rootHeight - 16f);

            panel.style.right = StyleKeyword.Auto;
            panel.style.bottom = StyleKeyword.Auto;
            panel.style.left = Mathf.Clamp(
                proposedPosition.x,
                8f,
                Mathf.Max(8f, rootWidth - panelWidth - 8f));
            panel.style.top = Mathf.Clamp(
                proposedPosition.y,
                8f,
                Mathf.Max(8f, rootHeight - panelHeight - 8f));
        }

        private void RegisterMapInputGuard(VisualElement element)
        {
            element.RegisterCallback<PointerEnterEvent>(
                _ =>
                {
                    if (gameplayMap != null)
                        gameplayMap.PointerSelectionBlocked = true;
                });
            element.RegisterCallback<PointerLeaveEvent>(
                _ =>
                {
                    if (gameplayMap != null &&
                        !IsNeutralNpcViewOpen() &&
                        !IsOperationBoardOpen() &&
                        !IsPauseMenuOpen())
                    {
                        gameplayMap.PointerSelectionBlocked = false;
                    }
                });
        }

        private bool IsNeutralNpcViewOpen()
        {
            return _neutralNpcView != null &&
                _neutralNpcView.resolvedStyle.display == DisplayStyle.Flex;
        }

        private static void AddDescription(VisualElement parent, string text)
        {
            var label = new Label(text);
            label.style.fontSize = 14;
            label.style.color = new Color(0.63f, 0.69f, 0.78f);
            label.style.marginBottom = 12;
            label.style.whiteSpace = WhiteSpace.Normal;
            parent.Add(label);
        }

        private static Label AddStatus(VisualElement parent)
        {
            var label = new Label();
            label.style.minHeight = 70;
            label.style.paddingLeft = 14;
            label.style.paddingRight = 14;
            label.style.paddingTop = 12;
            label.style.paddingBottom = 12;
            label.style.marginBottom = 14;
            label.style.fontSize = 16;
            label.style.whiteSpace = WhiteSpace.Normal;
            label.style.backgroundColor = new Color(0.055f, 0.075f, 0.11f, 1f);
            label.style.color = new Color(0.84f, 0.89f, 0.96f);
            parent.Add(label);
            return label;
        }

        private static void StyleInput(TextField input)
        {
            input.style.marginTop = 6;
            input.style.marginBottom = 12;
            input.style.fontSize = 16;
            input.style.color = Color.white;
        }

        private static void StyleSetupField<TValue>(BaseField<TValue> field)
        {
            if (field == null)
                return;

            field.style.minHeight = 36;
            field.style.marginTop = 3;
            field.style.marginBottom = 3;
            field.style.fontSize = 14;
            field.style.color = new Color(0.08f, 0.10f, 0.14f, 1f);
            field.labelElement.style.minWidth = 106;
            field.labelElement.style.marginRight = 10;
            field.labelElement.style.fontSize = 14;
            field.labelElement.style.unityFontStyleAndWeight =
                FontStyle.Bold;
            field.labelElement.style.color = SinglePlayerSetupTextColor;
        }

        private static void SetVisible(VisualElement element, bool visible)
        {
            if (element != null)
                element.style.display = visible
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
        }

        private static void SetServiceActive(Component service, bool active)
        {
            if (service != null && service.gameObject.activeSelf != active)
                service.gameObject.SetActive(active);
        }

        private void OnDestroy()
        {
            if (singlePlayerSimulation != null && _singlePlayerEventsBound)
            {
                singlePlayerSimulation.RealtimeStateChanged -=
                    HandleSinglePlayerRealtimeStateChanged;
                singlePlayerSimulation.RealtimeFixedStepsAdvanced -=
                    HandleRealtimeFixedStepsAdvanced;
                singlePlayerSimulation.RealtimeDayBoundaryReached -=
                    HandleRealtimeDayBoundaryReached;
                _singlePlayerEventsBound = false;
            }
            if (multiplayerSession != null && _multiplayerEventsBound)
            {
                multiplayerSession.StateChanged -= HandleMultiplayerStateChanged;
                multiplayerSession.RoomChanged -= HandleMultiplayerRoomChanged;
                multiplayerSession.MatchmakingChanged -=
                    HandleMultiplayerMatchmakingChanged;
                multiplayerSession.ErrorRaised -= HandleMultiplayerError;
                multiplayerSession.Disconnect();
                _multiplayerEventsBound = false;
            }
            if (gameplayMap != null && _mapEventsBound)
            {
                gameplayMap.CellSelected -= HandleMapCellSelected;
                gameplayMap.CellMoveRequested -= HandleMapMoveRequested;
                gameplayMap.PrimaryCellSelected -= HideMapContextMenu;
                gameplayMap.CellActionRequested -= HandleMapActionRequested;
                gameplayMap.GameplayStateChanged -= HandleMapGameplayStateChanged;
                gameplayMap.MineCaptured -= HandleMineCaptured;
                gameplayMap.MineSpawned -= HandleMineSpawned;
                gameplayMap.MineConstructionCompleted -=
                    HandleMineConstructionCompleted;
                gameplayMap.CastleCaptured -= HandleCastleCaptured;
                gameplayMap.CapitalDestroyed -= HandleCapitalDestroyed;
                gameplayMap.CastleRoleChanged -= HandleCastleRoleChanged;
                gameplayMap.SiegeDayResolved -= HandleSiegeDayResolved;
                gameplayMap.CommanderGenerated -= HandleCommanderGenerated;
                gameplayMap.CommanderDied -= HandleCommanderDied;
                gameplayMap.SupplyInterdictionResolved -=
                    HandleSupplyInterdictionResolved;
                _mapEventsBound = false;
            }
            if (_panelSettings != null)
                Destroy(_panelSettings);
        }
    }
}
