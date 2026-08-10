using UnityEngine;
using UnityEngine.UI;

namespace Game.Presentation
{
    public sealed class TurnHudPresenter : MonoBehaviour
    {
        [SerializeField] private SimulationBootstrapper simulation;
        [SerializeField] private Text turnLabel;
        [SerializeField] private Text actionPointLabel;
        [SerializeField] private Text phaseLabel;
        [SerializeField] private Text commandCountLabel;
        [SerializeField] private Text campaignResultLabel;

        private void OnEnable()
        {
            if (simulation != null)
                simulation.RealtimeStateChanged += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            if (simulation != null)
                simulation.RealtimeStateChanged -= Refresh;
        }

        // 기존 씬의 턴 종료 버튼 연결을 유지하면서 동작은 일시정지로 전환한다.
        public void OnEndTurnPressed() => OnPausePressed();

        public void OnPausePressed()
        {
            if (simulation == null)
                return;
            simulation.ToggleRealtimePause();
            Refresh();
        }

        public void OnSpeed1Pressed() => SetSpeed(1);
        public void OnSpeed2Pressed() => SetSpeed(2);
        public void OnSpeed3Pressed() => SetSpeed(3);
        public void OnSpeed4Pressed() => SetSpeed(4);
        public void OnSpeed5Pressed() => SetSpeed(5);

        public void OnCancelLastCommandPressed()
        {
            if (simulation == null)
                return;

            simulation.CancelLastCommand();
            Refresh();
        }

        public void Refresh()
        {
            if (simulation == null)
                return;

            if (turnLabel != null)
            {
                turnLabel.text =
                    $"{simulation.RealtimeDayNumber}일 " +
                    $"{simulation.RealtimeHour:D2}:" +
                    $"{simulation.RealtimeMinute:D2} / " +
                    $"총 {simulation.MaxCampaignTurns}일";
            }

            if (actionPointLabel != null)
            {
                actionPointLabel.text =
                    $"남은 일일 행동력 {simulation.RemainingActionPoints}";
            }

            if (phaseLabel != null)
            {
                phaseLabel.text = simulation.IsRealtimePaused
                    ? "일시정지"
                    : $"{simulation.RealtimeSpeedMultiplier}배속 진행 중";
            }

            if (commandCountLabel != null)
            {
                commandCountLabel.text =
                    $"예약 명령 {simulation.QueuedCommandCount}개";
            }

            if (campaignResultLabel != null)
            {
                campaignResultLabel.text =
                    CampaignResultKoreanFormatter.Format(
                        simulation.CampaignResult);
            }
        }

        private void SetSpeed(int speedMultiplier)
        {
            if (simulation == null)
                return;
            simulation.SetRealtimeSpeed(speedMultiplier);
            Refresh();
        }
    }
}
