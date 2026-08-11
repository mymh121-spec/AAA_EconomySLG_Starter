using System;
using System.Text;
using System.Threading.Tasks;
using Game.Application.Session;
using Game.Application.World;
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

        private readonly GameModeSelection _selection =
            new GameModeSelection();

        private UIDocument _document;
        private PanelSettings _panelSettings;
        private VisualElement _uiRoot;
        private VisualElement _modeView;
        private VisualElement _connectionView;
        private VisualElement _singlePlayerView;
        private VisualElement _singlePlayerResultView;
        private VisualElement _multiplayerView;
        private TextField _endpointField;
        private TextField _tokenField;
        private Label _connectionStatus;
        private Label _singlePlayerStatus;
        private Label _singleMapSelectionStatus;
        private VisualElement _singleMapActionPanel;
        private Label _singleMapActionTitle;
        private Label _singleMapActionFeedback;
        private Button _createUnitButton;
        private Button _unitTypeButton;
        private Button _selectUnitButton;
        private Button _inspectUnitButton;
        private Button _moveUnitButton;
        private VisualElement _mapContextMenu;
        private Label _mapContextTitle;
        private Label _mapContextHint;
        private Button _contextCreateUnitButton;
        private Button _contextUnitTypeButton;
        private Button _contextSelectUnitButton;
        private Button _contextInspectUnitButton;
        private Button _contextMoveUnitButton;
        private Button _contextCaptureMineButton;
        private Button _contextMissionButton;
        private Button _neutralNpcTopButton;
        private VisualElement _neutralNpcView;
        private Label _neutralNpcSelectionStatus;
        private Label _neutralNpcFeedback;
        private Button _npcArchetypeButton;
        private Button _npcWeaponButton;
        private Button _npcArmorButton;
        private Button _npcRecruitButton;
        private Button _npcEquipButton;
        private VisualElement _timeHudView;
        private Label _timeHudLabel;
        private VisualElement _pauseMenuOverlay;
        private VisualElement _keyGuideView;
        private bool _resumeRealtimeAfterPauseMenu;
        private Label _singlePlayerResultText;
        private Label _multiplayerStatus;
        private Label _multiplayerMapSelectionStatus;
        private bool _singlePlayerEventsBound;
        private bool _multiplayerEventsBound;
        private bool _mapEventsBound;
        private UnitArchetype _pendingUnitArchetype = UnitArchetype.Swordsman;
        private UnitWeaponType _pendingWeaponType = UnitWeaponType.Sword;
        private ArmorClass _pendingArmorClass = ArmorClass.Light;

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
            if (_selection.IsSinglePlayer &&
                UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                HandleEscapePressed();
            }
            else if (_selection.IsSinglePlayer &&
                !IsPauseMenuOpen() &&
                singlePlayerSimulation != null &&
                UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                ToggleSinglePlayerPause();
            }
#endif
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
            multiplayerSession.ErrorRaised += HandleMultiplayerError;
            _multiplayerEventsBound = true;
        }

        private void EnsureGameplayWorld()
        {
            if (gameplayMap == null)
            {
                var mapRoot = new GameObject("경제 지도");
                mapRoot.SetActive(false);
                gameplayMap = mapRoot.AddComponent<StarterMapController>();
            }

            SetServiceActive(gameplayMap, true);
            gameplayMap.PointerSelectionBlocked = false;
            gameplayMap.Initialize();
            BindMapEvents();
        }

        private void BindMapEvents()
        {
            if (gameplayMap == null || _mapEventsBound)
                return;

            gameplayMap.CellSelected += HandleMapCellSelected;
            gameplayMap.PrimaryCellSelected += HideMapContextMenu;
            gameplayMap.CellActionRequested += HandleMapActionRequested;
            gameplayMap.GameplayStateChanged += HandleMapGameplayStateChanged;
            gameplayMap.MineCaptured += HandleMineCaptured;
            gameplayMap.MineSpawned += HandleMineSpawned;
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

            _modeView = CreateCard(
                _uiRoot,
                "기업의 시대",
                "플레이할 방식을 선택하세요.");
            AddButton(_modeView, "1인이서 하기", SelectSinglePlayer);
            AddButton(_modeView, "여러 명이서 하기", SelectMultiplayer);

            _connectionView = CreateCard(
                _uiRoot,
                "여러 명 플레이 연결",
                "서버 주소와 발급받은 접속 토큰을 입력하세요.");
            _endpointField = new TextField("서버 주소")
            {
                value = multiplayerSession != null
                    ? multiplayerSession.ServerEndpoint
                    : DefaultServerEndpoint
            };
            StyleInput(_endpointField);
            _connectionView.Add(_endpointField);

            _tokenField = new TextField("접속 토큰")
            {
                isPasswordField = true
            };
            StyleInput(_tokenField);
            _connectionView.Add(_tokenField);
            _connectionStatus = AddStatus(_connectionView);
            AddButton(_connectionView, "서버 연결", ConnectMultiplayer);
            AddButton(_connectionView, "뒤로", ShowModeSelection);

            _singlePlayerView = CreateCard(
                _uiRoot,
                "1인 플레이",
                "로컬 경제와 AI 기업의 행동을 이 PC에서 정산합니다.");
            _singlePlayerStatus = AddStatus(_singlePlayerView);
            _singleMapSelectionStatus = AddStatus(_singlePlayerView);
            _singleMapSelectionStatus.text =
                "지도 칸을 클릭하면 지역 정보와 가능한 행동을 확인합니다.";
            BuildSinglePlayerMapActionPanel(_singlePlayerView);
            StyleGameplayHud(_singlePlayerView);
            RegisterMapInputGuard(_singlePlayerView);

            _singlePlayerResultView = CreateCard(
                _uiRoot,
                "1인 플레이 최종 결과",
                "승자와 패자를 확인한 뒤 새 게임을 시작할 수 있습니다.");
            _singlePlayerResultText = AddStatus(_singlePlayerResultView);
            _singlePlayerResultText.style.minHeight = 220;
            AddButton(
                _singlePlayerResultView,
                "확인하고 새 게임 시작",
                ConfirmSinglePlayerResult);

            _multiplayerView = CreateCard(
                _uiRoot,
                "여러 명 플레이",
                "모든 참가자가 준비하면 서버에서 다음 턴을 정산합니다.");
            _multiplayerStatus = AddStatus(_multiplayerView);
            _multiplayerMapSelectionStatus = AddStatus(_multiplayerView);
            _multiplayerMapSelectionStatus.text =
                "지도 칸을 클릭하면 지역 정보와 가능한 행동을 확인합니다.";
            AddButton(_multiplayerView, "이번 턴 준비 완료", MarkMultiplayerReady);
            AddButton(_multiplayerView, "서버 상태 새로고침", RefreshMultiplayer);
            AddButton(_multiplayerView, "연결 종료 후 모드 선택", ShowModeSelection);
            StyleGameplayHud(_multiplayerView);
            RegisterMapInputGuard(_multiplayerView);
            BuildMapContextMenu(_uiRoot);
            BuildNeutralNpcInterface(_uiRoot);
            BuildTimeHudAndPauseMenu(_uiRoot);
        }

        private void SelectSinglePlayer()
        {
            _selection.Clear();
            if (!_selection.TrySelect(GamePlayMode.SinglePlayer, out string reason))
            {
                Debug.LogWarning(reason);
                return;
            }

            EnsureGameplayWorld();
            EnsureSinglePlayerSimulation();
            multiplayerSession?.Disconnect();
            SetServiceActive(multiplayerSession, false);
            SetServiceActive(singlePlayerSimulation, true);
            SetVisible(_modeView, false);
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
            _connectionStatus.text = "토큰은 연결에만 사용되며 저장하지 않습니다.";
            SetVisible(_modeView, false);
            SetVisible(_singlePlayerView, false);
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_multiplayerView, false);
            SetVisible(_connectionView, true);
            ShowConnectionOverlay();
        }

        private async void ConnectMultiplayer()
        {
            if (multiplayerSession.IsRequestRunning)
                return;

            string endpoint = _endpointField.value?.Trim();
            string token = _tokenField.value;
            _tokenField.value = string.Empty;

            if (!multiplayerSession.ConfigureServerEndpoint(endpoint))
            {
                _connectionStatus.text = multiplayerSession.LastError;
                return;
            }

            _connectionStatus.text = "서버에 연결하는 중입니다...";
            try
            {
                bool connected = await multiplayerSession.ConnectAsync(token);
                if (!connected)
                {
                    _connectionStatus.text = multiplayerSession.LastError;
                    return;
                }

                SetVisible(_connectionView, false);
                SetVisible(_multiplayerView, true);
                ShowGameplayHud();
                RefreshMultiplayerStatus(multiplayerSession.CurrentState);
            }
            finally
            {
                token = string.Empty;
            }
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
            var result = singlePlayerSimulation.CampaignResult;
            decimal winningPower = decimal.MinValue;
            for (int i = 0; i < result.Rankings.Count; i++)
            {
                var ranking = result.Rankings[i];
                if (!ranking.IsEliminated &&
                    ranking.EconomicPower > winningPower)
                {
                    winningPower = ranking.EconomicPower;
                }
            }

            var winners = new StringBuilder(80);
            var losers = new StringBuilder(160);
            for (int i = 0; i < result.Rankings.Count; i++)
            {
                var ranking = result.Rankings[i];
                bool isWinner = !ranking.IsEliminated &&
                    ranking.EconomicPower == winningPower;
                StringBuilder target = isWinner ? winners : losers;
                if (target.Length > 0)
                    target.Append(", ");
                target.Append(ranking.CompanyName)
                    .Append(" (")
                    .Append(ranking.EconomicPower.ToString("N0"))
                    .Append(")");
            }

            if (winners.Length == 0)
                winners.Append("없음");
            if (losers.Length == 0)
                losers.Append("없음");

            return new StringBuilder(320)
                .Append("최종 판정: ")
                .Append(CampaignResultKoreanFormatter.GetOutcomeName(
                    result.Outcome))
                .Append('\n')
                .Append("종료 사유: ")
                .Append(CampaignResultKoreanFormatter.GetReasonName(
                    result.EndReason))
                .Append('\n')
                .Append("종료 턴: ")
                .Append(result.ResolvedTurn.Value)
                .Append(" / ")
                .Append(singlePlayerSimulation.MaxCampaignTurns)
                .Append('\n')
                .Append("승자: ")
                .Append(winners)
                .Append('\n')
                .Append("패자: ")
                .Append(losers)
                .ToString();
        }

        private void ConfirmSinglePlayerResult()
        {
            singlePlayerSimulation.RestartSimulation();
            gameplayMap?.ResetMap();
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_singlePlayerView, true);
            ShowGameplayHud();
            RefreshSinglePlayerStatus();
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
            HideMapContextMenu();
            multiplayerSession?.Disconnect();
            SetServiceActive(singlePlayerSimulation, false);
            SetServiceActive(multiplayerSession, false);
            SetServiceActive(gameplayMap, false);
            _selection.Clear();

            SetVisible(_modeView, true);
            SetVisible(_connectionView, false);
            SetVisible(_singlePlayerView, false);
            SetVisible(_singlePlayerResultView, false);
            SetVisible(_multiplayerView, false);
            ShowMenuOverlay();
        }

        private void ShowMenuOverlay()
        {
            if (_uiRoot == null)
                return;

            HidePauseMenuWithoutResuming();
            CloseNeutralNpcView();
            SetVisible(_neutralNpcTopButton, false);
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
            SetVisible(_neutralNpcTopButton, false);
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
            SetVisible(_neutralNpcTopButton, false);
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
            SetVisible(_timeHudView, _selection.IsSinglePlayer);
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_keyGuideView, false);
            _resumeRealtimeAfterPauseMenu = false;
        }

        private void RefreshSinglePlayerStatus()
        {
            if (_singlePlayerStatus == null || singlePlayerSimulation == null)
                return;

            if (_timeHudLabel != null)
            {
                _timeHudLabel.text = new StringBuilder(96)
                    .Append("현재 ")
                    .Append(singlePlayerSimulation.RealtimeDayNumber)
                    .Append("일 ")
                    .Append(singlePlayerSimulation.RealtimeHour.ToString("D2"))
                    .Append(':')
                    .Append(singlePlayerSimulation.RealtimeMinute.ToString("D2"))
                    .Append(" · ")
                    .Append(singlePlayerSimulation.IsRealtimePaused
                        ? "일시정지"
                        : singlePlayerSimulation.RealtimeSpeedMultiplier + "배속")
                    .Append("\n다음 경제 정산 ")
                    .Append(singlePlayerSimulation.CurrentTurn.Value)
                    .Append("일 / 총 ")
                    .Append(singlePlayerSimulation.MaxCampaignTurns)
                    .Append("일")
                    .ToString();
            }

            var builder = new StringBuilder(180);
            builder.Append("보유 자금 ")
                .Append(singlePlayerSimulation.PlayerCash.ToString("N0"))
                .Append("원");

            MapUnitState selectedUnit = gameplayMap?.SelectedPlayerUnit;
            if (selectedUnit != null)
            {
                builder.Append("\n선택 유닛 체력 ")
                    .Append(selectedUnit.Stamina)
                    .Append('/')
                    .Append(selectedUnit.MaxStamina)
                    .Append(" · ")
                    .Append(selectedUnit.WeaponDisplayName)
                    .Append(" / ")
                    .Append(selectedUnit.ArmorDisplayName)
                    .Append(" · 게임 시간 6시간마다 1 회복");
            }

            builder.Append('\n')
                .Append(CampaignResultKoreanFormatter.Format(
                    singlePlayerSimulation.CampaignResult));
            _singlePlayerStatus.text = builder.ToString();
        }

        private void HandleSinglePlayerRealtimeStateChanged()
        {
            if (!_selection.IsSinglePlayer)
                return;

            RefreshSinglePlayerStatus();
            if (singlePlayerSimulation != null &&
                singlePlayerSimulation.IsCampaignFinished)
            {
                ShowSinglePlayerResult();
            }
        }

        private void HandleMultiplayerStateChanged(PvpReconnectDto state)
        {
            RefreshMultiplayerStatus(state);
        }

        private void HandleMapCellSelected(MapCellSelection selection)
        {
            string description =
                $"선택: {selection.DisplayName} " +
                $"({selection.Coordinate.X}, {selection.Coordinate.Y})\n" +
                selection.InteractionHint;

            if (_singleMapSelectionStatus != null)
                _singleMapSelectionStatus.text = description;
            if (_multiplayerMapSelectionStatus != null)
                _multiplayerMapSelectionStatus.text = description;

            RefreshSinglePlayerMapActions(selection);
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
            _mapContextHint.text = selection.InteractionHint;
            ConfigureMapActionButtons(
                selection,
                _contextCreateUnitButton,
                _contextSelectUnitButton,
                _contextMoveUnitButton);
            SetVisible(
                _contextUnitTypeButton,
                false);
            SetVisible(
                _contextInspectUnitButton,
                !string.IsNullOrEmpty(selection.UnitId));
            ConfigureCaptureMineButton(selection);

            bool missionTarget = selection.Content == MapCellContent.EnemyBase;
            SetVisible(_contextMissionButton, missionTarget);
            _contextMissionButton.SetEnabled(missionTarget);

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
            if (!_selection.IsSinglePlayer || _singleMapActionFeedback == null)
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

            string mineName = record.Kind == MineKind.Gold
                ? "금광"
                : "철광산";
            _singleMapActionFeedback.text =
                $"{record.EconomicDay}일: 새로운 {mineName}이(가) " +
                $"{record.Coordinate}에서 발견되었습니다.";
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
            gameplayMap.AdvanceEconomicDay(out _);
        }

        private void CreatePlayerUnit()
        {
            if (gameplayMap == null || singlePlayerSimulation == null)
                return;

            if (!gameplayMap.CanCreatePlayerUnit(out string reason))
            {
                SetMapActionFeedback(reason);
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

            if (!gameplayMap.TryCreatePlayerUnit(
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
                $"구성으로 생산했습니다. 비용 {cost:N0}";
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
            SetMapActionFeedback(
                $"부대 정보 | {owner} | {unit.ArchetypeDisplayName} | " +
                $"{unit.WeaponDisplayName} / {unit.ArmorDisplayName} | " +
                $"공격 x{unit.AttackModifier:F2} · 방어 x{unit.DefenseModifier:F2} · " +
                $"기동 x{unit.MobilityModifier:F2} | " +
                $"체력 {unit.Stamina}/{unit.MaxStamina} | " +
                $"위치 {unit.Coordinate} | {movement}");
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
            SetMapActionFeedback(isMine
                ? $"{destination} 광산으로 이동합니다. 도착하면 점령을 시작합니다."
                : $"{destination}(으)로 이동 명령을 내렸습니다.");
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
                : $"지도 행동 · {selectedUnit.ArchetypeDisplayName} " +
                  $"{selectedUnit.Id} {selectedUnit.Coordinate} · " +
                  $"{selectedUnit.WeaponDisplayName}/{selectedUnit.ArmorDisplayName} · " +
                  $"체력 {selectedUnit.Stamina}/{selectedUnit.MaxStamina}";

            ConfigureMapActionButtons(
                selection,
                _createUnitButton,
                _selectUnitButton,
                _moveUnitButton);
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
                SetVisible(_contextUnitTypeButton, false);
                SetVisible(_contextInspectUnitButton, hasUnit);
                ConfigureCaptureMineButton(selection);
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

            bool atPlayerBase = selection.Content == MapCellContent.PlayerBase;
            bool canCreate = atPlayerBase &&
                gameplayMap.CanCreatePlayerUnit(out _);
            bool canSelect = gameplayMap.CanSelectPlayerUnitAt(
                selection.Coordinate,
                out _);
            bool canMove = gameplayMap.CanMoveSelectedPlayerUnit(
                selection.Coordinate,
                out _);
            bool hasSelectedUnit = gameplayMap.SelectedPlayerUnit != null;
            bool isMine = selection.Content == MapCellContent.NormalMine ||
                          selection.Content == MapCellContent.GoldMine;

            SetVisible(createButton, atPlayerBase);
            createButton.SetEnabled(canCreate);
            SetVisible(selectButton, canSelect);
            selectButton.SetEnabled(canSelect);
            // A mine uses the single consolidated capture button in the
            // right-click menu. Showing the generic move action as well made
            // both buttons issue effectively the same order.
            SetVisible(
                moveButton,
                hasSelectedUnit && !canSelect && !isMine);
            moveButton.SetEnabled(canMove);
            moveButton.text = "이 칸으로 이동 · 체력 1";
        }

        private void ShowSelectedMissionInformation()
        {
            if (gameplayMap == null || !gameplayMap.CurrentSelection.HasValue)
                return;

            MapCellSelection selection = gameplayMap.CurrentSelection.Value;
            SetMapActionFeedback(
                $"{selection.DisplayName}: 정찰·봉쇄·공격 미션 대상입니다. " +
                "유닛을 선택한 뒤 우클릭 메뉴에서 목표 위치로 이동하세요.");
            if (_mapContextHint != null)
            {
                _mapContextHint.text =
                    "미션 준비: 유닛 선택 → 목표로 이동 → 도착 후 임무 수행";
            }
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

            int readyCount = 0;
            int playerCount = state.players?.Length ?? 0;
            for (int i = 0; i < playerCount; i++)
            {
                if (state.players[i].ready)
                    readyCount++;
            }

            string phase = GetMultiplayerPhaseName(state.phase);
            string cash = state.world?.ownCompany != null
                ? $"\n보유 현금 {state.world.ownCompany.cash:N0}"
                : string.Empty;
            _multiplayerStatus.text =
                $"{state.turn}턴 · {phase}\n" +
                $"준비 {readyCount}/{playerCount} · 상태 버전 {state.revision}" +
                cash;
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

            var subtitleLabel = new Label(subtitle);
            subtitleLabel.style.fontSize = 17;
            subtitleLabel.style.color = new Color(0.68f, 0.74f, 0.83f);
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.marginBottom = 24;
            card.Add(subtitleLabel);
            root.Add(card);
            return card;
        }

        private static void AddButton(
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
        }

        private void BuildSinglePlayerMapActionPanel(VisualElement parent)
        {
            _singleMapActionPanel = new VisualElement();
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
                "이 칸의 부대 정보 확인",
                InspectUnitAtCurrentSelection);
            _moveUnitButton = CreateMapActionButton(
                "이 칸으로 이동 · 체력 1",
                MoveSelectedPlayerUnit);
            _singleMapActionPanel.Add(_unitTypeButton);
            _singleMapActionPanel.Add(_createUnitButton);
            _singleMapActionPanel.Add(_selectUnitButton);
            _singleMapActionPanel.Add(_inspectUnitButton);
            _singleMapActionPanel.Add(_moveUnitButton);

            _singleMapActionFeedback = new Label(
                "본사를 선택해 첫 유닛을 창설하세요.");
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
                "이 칸의 부대 정보 확인",
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
            _contextCaptureMineButton = CreateMapActionButton(
                "점령한다",
                () =>
                {
                    CaptureSelectedMine();
                    HideMapContextMenu();
                });
            _contextMissionButton = CreateMapActionButton(
                "미션 정보 · 정찰 / 봉쇄 / 공격",
                ShowSelectedMissionInformation);
            Button closeButton = CreateMapActionButton(
                "닫기",
                HideMapContextMenu);
            closeButton.style.backgroundColor =
                new Color(0.18f, 0.22f, 0.29f, 1f);

            _mapContextMenu.Add(_contextUnitTypeButton);
            _mapContextMenu.Add(_contextCreateUnitButton);
            _mapContextMenu.Add(_contextSelectUnitButton);
            _mapContextMenu.Add(_contextInspectUnitButton);
            _mapContextMenu.Add(_contextMoveUnitButton);
            _mapContextMenu.Add(_contextCaptureMineButton);
            _mapContextMenu.Add(_contextMissionButton);
            _mapContextMenu.Add(closeButton);
            root.Add(_mapContextMenu);
            RegisterMapInputGuard(_mapContextMenu);
            SetVisible(_mapContextMenu, false);
        }

        private void BuildNeutralNpcInterface(VisualElement root)
        {
            _neutralNpcTopButton = new Button(OpenNeutralNpcView)
            {
                text = "중립 NPC · 용병/장비 상인"
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
                "중립 용병·장비 상인",
                "병종과 장비를 직접 구성해 모집하거나 선택 부대의 장비를 변경합니다.");
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

            _neutralNpcSelectionStatus = AddStatus(_neutralNpcView);
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

            _neutralNpcView.Add(_npcArchetypeButton);
            _neutralNpcView.Add(_npcWeaponButton);
            _neutralNpcView.Add(_npcArmorButton);
            _neutralNpcView.Add(_npcRecruitButton);
            _neutralNpcView.Add(_npcEquipButton);
            _neutralNpcFeedback = AddStatus(_neutralNpcView);
            AddButton(_neutralNpcView, "닫기", CloseNeutralNpcView);

            RegisterMapInputGuard(_neutralNpcTopButton);
            RegisterMapInputGuard(_neutralNpcView);
            SetVisible(_neutralNpcTopButton, false);
            SetVisible(_neutralNpcView, false);
        }

        private void OpenRecruitmentAtNeutralNpc()
        {
            if (!_selection.IsSinglePlayer)
                return;

            _pendingWeaponType =
                UnitEquipmentCatalog.GetDefaultWeapon(_pendingUnitArchetype);
            SetNeutralNpcFeedback(
                "병종을 고른 뒤 무기와 갑옷을 선택하고 유닛 생산을 누르세요.");
            OpenNeutralNpcView(false);
        }

        private void OpenNeutralNpcView()
        {
            OpenNeutralNpcView(true);
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

            HideMapContextMenu();
            SetVisible(_neutralNpcView, true);
            _neutralNpcView.BringToFront();
            if (gameplayMap != null)
                gameplayMap.PointerSelectionBlocked = true;
            RefreshNeutralNpcView();
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
            MapUnitState selectedUnit = gameplayMap?.SelectedPlayerUnit;

            _neutralNpcSelectionStatus.text =
                $"구성: {archetypeName} · {weaponName} · {armorName}\n" +
                $"능력: 공격 x{UnitEquipmentCatalog.GetAttackModifier(_pendingWeaponType):F2} · " +
                $"방어 x{UnitEquipmentCatalog.GetDefenseModifier(_pendingArmorClass):F2} · " +
                $"기동 x{UnitEquipmentCatalog.GetMobilityModifier(_pendingUnitArchetype, _pendingArmorClass):F2}\n" +
                $"모집비 {recruitCost:N0} · 장비 구입비 {equipmentCost:N0}\n" +
                (selectedUnit == null
                    ? "장비 변경 대상: 선택된 부대 없음"
                    : $"장비 변경 대상: {selectedUnit.ArchetypeDisplayName} {selectedUnit.Id}");

            _npcArchetypeButton.text = $"병종: {archetypeName} · 클릭해 변경";
            _npcWeaponButton.text = $"무기: {weaponName} · 클릭해 변경";
            _npcArmorButton.text = $"갑옷: {armorName} · 클릭해 변경";
            _npcRecruitButton.text = $"유닛 생산 · {recruitCost:N0}";
            _npcEquipButton.text = selectedUnit == null
                ? "선택 부대 장비 변경"
                : $"{selectedUnit.Id} 장비 변경 · {equipmentCost:N0}";

            bool canCreate = gameplayMap != null &&
                gameplayMap.CanCreatePlayerUnit(out _) &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(recruitCost);
            bool sameEquipment = selectedUnit != null &&
                selectedUnit.WeaponType == _pendingWeaponType &&
                selectedUnit.ArmorClass == _pendingArmorClass;
            bool canEquip = selectedUnit != null &&
                !sameEquipment &&
                singlePlayerSimulation != null &&
                singlePlayerSimulation.CanAffordPlayerCash(equipmentCost);
            _npcRecruitButton.SetEnabled(canCreate);
            _npcEquipButton.SetEnabled(canEquip);
        }

        private void SetNeutralNpcFeedback(string message)
        {
            if (_neutralNpcFeedback != null)
                _neutralNpcFeedback.text = message ?? string.Empty;
        }

        private void BuildTimeHudAndPauseMenu(VisualElement root)
        {
            _timeHudView = new VisualElement();
            _timeHudView.style.position = Position.Absolute;
            _timeHudView.style.top = 16;
            _timeHudView.style.right = 24;
            _timeHudView.style.width = 350;
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

            _timeHudLabel = new Label();
            _timeHudLabel.style.fontSize = 16;
            _timeHudLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _timeHudLabel.style.color = Color.white;
            _timeHudLabel.style.whiteSpace = WhiteSpace.Normal;
            _timeHudLabel.style.marginBottom = 7;
            _timeHudView.Add(_timeHudLabel);
            AddRealtimeSpeedControls(_timeHudView);
            root.Add(_timeHudView);

            _pauseMenuOverlay = new VisualElement();
            _pauseMenuOverlay.style.position = Position.Absolute;
            _pauseMenuOverlay.style.left = 0;
            _pauseMenuOverlay.style.right = 0;
            _pauseMenuOverlay.style.top = 0;
            _pauseMenuOverlay.style.bottom = 0;
            _pauseMenuOverlay.style.flexDirection = FlexDirection.Row;
            _pauseMenuOverlay.style.alignItems = Align.Center;
            _pauseMenuOverlay.style.justifyContent = Justify.Center;
            _pauseMenuOverlay.style.backgroundColor =
                new Color(0.015f, 0.022f, 0.035f, 0.82f);

            VisualElement pauseCard = CreateCard(
                _pauseMenuOverlay,
                "일시정지",
                "게임을 계속하거나 조작법을 확인할 수 있습니다.");
            pauseCard.style.width = 460;
            pauseCard.style.marginRight = 12;
            AddButton(pauseCard, "계속하기", ClosePauseMenu);
            AddButton(pauseCard, "키 설명", ToggleKeyGuide);
            AddButton(
                pauseCard,
                "모드 선택으로 돌아가기",
                ReturnToModeSelectionFromPause);

            _keyGuideView = CreateCard(
                _pauseMenuOverlay,
                "키 설명",
                "지도와 시간 조작");
            _keyGuideView.style.width = 560;
            _keyGuideView.style.marginLeft = 12;
            AddDescription(
                _keyGuideView,
                "ESC: 일시정지 메뉴 열기/닫기\n" +
                "Space: 시간 일시정지/재개\n" +
                "WASD / 방향키: 지도 이동\n" +
                "마우스 가운데 버튼 드래그: 지도 이동\n" +
                "마우스 휠: 확대/축소\n" +
                "L: 플레이어 본사로 이동\n" +
                "좌클릭: 지도 칸 선택\n" +
                "우클릭: 이동·점령·미션 메뉴\n" +
                "경제와 광산 생산: 매일 자정 정산");
            AddButton(_keyGuideView, "키 설명 닫기", ToggleKeyGuide);

            root.Add(_pauseMenuOverlay);
            RegisterMapInputGuard(_timeHudView);
            RegisterMapInputGuard(_pauseMenuOverlay);
            SetVisible(_timeHudView, false);
            SetVisible(_keyGuideView, false);
            SetVisible(_pauseMenuOverlay, false);
        }

        private void HandleEscapePressed()
        {
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
            if (_pauseMenuOverlay == null || !_selection.IsSinglePlayer)
                return;

            HideMapContextMenu();
            CloseNeutralNpcView();
            _resumeRealtimeAfterPauseMenu =
                singlePlayerSimulation != null &&
                !singlePlayerSimulation.IsRealtimePaused;
            if (_resumeRealtimeAfterPauseMenu)
                singlePlayerSimulation.ToggleRealtimePause();

            SetVisible(_keyGuideView, false);
            SetVisible(_pauseMenuOverlay, true);
            _pauseMenuOverlay.BringToFront();
            if (gameplayMap != null)
                gameplayMap.PointerSelectionBlocked = true;
        }

        private void ClosePauseMenu()
        {
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_keyGuideView, false);
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
            SetVisible(_keyGuideView, false);
            _resumeRealtimeAfterPauseMenu = false;
        }

        private void ReturnToModeSelectionFromPause()
        {
            _resumeRealtimeAfterPauseMenu = false;
            SetVisible(_pauseMenuOverlay, false);
            SetVisible(_keyGuideView, false);
            ShowModeSelection();
        }

        private void ToggleKeyGuide()
        {
            if (_keyGuideView == null)
                return;

            bool visible =
                _keyGuideView.resolvedStyle.display == DisplayStyle.Flex;
            SetVisible(_keyGuideView, !visible);
        }

        private bool IsPauseMenuOpen()
        {
            return _pauseMenuOverlay != null &&
                _pauseMenuOverlay.resolvedStyle.display == DisplayStyle.Flex;
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
            const float menuWidth = 320f;
            const float estimatedMenuHeight = 410f;
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

        private void AddRealtimeSpeedControls(VisualElement parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;
            row.style.marginBottom = 8;
            AddSpeedButton(row, "일시정지", ToggleSinglePlayerPause);
            for (int speed = 1; speed <= 5; speed++)
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
            card.style.width = 420;
            card.style.maxWidth = new Length(46, LengthUnit.Percent);
            card.style.paddingLeft = 20;
            card.style.paddingRight = 20;
            card.style.paddingTop = 18;
            card.style.paddingBottom = 18;
            card.style.marginLeft = 20;
            card.style.marginTop = 20;
            card.style.backgroundColor =
                new Color(0.07f, 0.095f, 0.14f, 0.92f);
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
                multiplayerSession.ErrorRaised -= HandleMultiplayerError;
                multiplayerSession.Disconnect();
                _multiplayerEventsBound = false;
            }
            if (gameplayMap != null && _mapEventsBound)
            {
                gameplayMap.CellSelected -= HandleMapCellSelected;
                gameplayMap.PrimaryCellSelected -= HideMapContextMenu;
                gameplayMap.CellActionRequested -= HandleMapActionRequested;
                gameplayMap.GameplayStateChanged -= HandleMapGameplayStateChanged;
                gameplayMap.MineCaptured -= HandleMineCaptured;
                gameplayMap.MineSpawned -= HandleMineSpawned;
                _mapEventsBound = false;
            }
            if (_panelSettings != null)
                Destroy(_panelSettings);
        }
    }
}
