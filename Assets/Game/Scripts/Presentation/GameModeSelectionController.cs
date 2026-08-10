using System;
using System.Text;
using System.Threading.Tasks;
using Game.Application.Session;
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
        private Label _singlePlayerResultText;
        private Label _multiplayerStatus;
        private Label _multiplayerMapSelectionStatus;
        private bool _multiplayerEventsBound;
        private bool _mapEventsBound;

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

            BindMultiplayerEvents();
            BindMapEvents();
        }

        private SimulationBootstrapper EnsureSinglePlayerSimulation()
        {
            if (singlePlayerSimulation != null)
                return singlePlayerSimulation;

            var localRoot = new GameObject("1인 플레이 시뮬레이션");
            localRoot.SetActive(false);
            if (keepAcrossScenes)
                localRoot.transform.SetParent(transform, false);
            singlePlayerSimulation =
                localRoot.AddComponent<SimulationBootstrapper>();
            return singlePlayerSimulation;
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
            gameplayMap.Initialize();
            BindMapEvents();
        }

        private void BindMapEvents()
        {
            if (gameplayMap == null || _mapEventsBound)
                return;

            gameplayMap.CellSelected += HandleMapCellSelected;
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
            AddButton(_singlePlayerView, "턴 종료", EndSinglePlayerTurn);
            AddButton(_singlePlayerView, "모드 선택으로", ShowModeSelection);
            StyleGameplayHud(_singlePlayerView);

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

        private void EndSinglePlayerTurn()
        {
            if (singlePlayerSimulation.IsCampaignFinished)
            {
                ShowSinglePlayerResult();
                return;
            }

            singlePlayerSimulation.ResolveCurrentTurn(false);
            RefreshSinglePlayerStatus();
            if (singlePlayerSimulation.IsCampaignFinished)
                ShowSinglePlayerResult();
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

            _uiRoot.style.backgroundColor =
                new Color(0.035f, 0.047f, 0.07f, 0.98f);
            _uiRoot.style.alignItems = Align.Center;
            _uiRoot.style.justifyContent = Justify.Center;
        }

        private void ShowConnectionOverlay()
        {
            if (_uiRoot == null)
                return;

            _uiRoot.style.backgroundColor =
                new Color(0.025f, 0.035f, 0.055f, 0.68f);
            _uiRoot.style.alignItems = Align.Center;
            _uiRoot.style.justifyContent = Justify.Center;
        }

        private void ShowResultOverlay()
        {
            if (_uiRoot == null)
                return;

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
        }

        private void RefreshSinglePlayerStatus()
        {
            if (_singlePlayerStatus == null || singlePlayerSimulation == null)
                return;

            var builder = new StringBuilder(160);
            builder.Append("현재 ")
                .Append(singlePlayerSimulation.CurrentTurn.Value)
                .Append("턴 / ")
                .Append(singlePlayerSimulation.MaxCampaignTurns)
                .Append("턴\n남은 행동력 ")
                .Append(singlePlayerSimulation.RemainingActionPoints)
                .Append(" · 예약 명령 ")
                .Append(singlePlayerSimulation.QueuedCommandCount)
                .Append("개\n")
                .Append(CampaignResultKoreanFormatter.Format(
                    singlePlayerSimulation.CampaignResult));
            _singlePlayerStatus.text = builder.ToString();
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
            button.style.height = 52;
            button.style.marginTop = 6;
            button.style.marginBottom = 6;
            button.style.fontSize = 18;
            button.style.unityFontStyleAndWeight = FontStyle.Bold;
            button.style.backgroundColor = new Color(0.16f, 0.39f, 0.68f);
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
                _mapEventsBound = false;
            }
            if (_panelSettings != null)
                Destroy(_panelSettings);
        }
    }
}
