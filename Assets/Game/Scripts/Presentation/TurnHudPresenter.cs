using UnityEngine;
using UnityEngine.UI;
using Game.Application.Turn;
using Game.Domain.Localization;

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

        private void Start()
        {
            Refresh();
        }

        // Unity UI Button.onClick에 연결한다.
        public void OnEndTurnPressed()
        {
            if (simulation == null)
                return;

            if (simulation.IsCampaignFinished)
            {
                Refresh();
                return;
            }

            simulation.ResolveCurrentTurn();
            Refresh();
        }

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
                    $"{KoreanFormat.Turn(simulation.CurrentTurn)} / " +
                    $"{simulation.MaxCampaignTurns}턴";
            }

            if (actionPointLabel != null)
            {
                actionPointLabel.text =
                    $"남은 행동력 {simulation.RemainingActionPoints}";
            }

            if (phaseLabel != null)
                phaseLabel.text = GetPhaseName(
                    simulation.CurrentPhase);

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

        private static string GetPhaseName(TurnPhase phase)
        {
            switch (phase)
            {
                case TurnPhase.PlayerPlanning:
                    return "계획 단계";
                case TurnPhase.PlayerResolution:
                    return "플레이어 명령 처리";
                case TurnPhase.AIResolution:
                    return "AI 행동 처리";
                case TurnPhase.WorldResolution:
                    return "세계 정산";
                case TurnPhase.CampaignResolution:
                    return "승패 판정";
                case TurnPhase.Report:
                    return "결과 보고";
                case TurnPhase.Completed:
                    return "턴 완료";
                default:
                    return "알 수 없음";
            }
        }
    }
}
