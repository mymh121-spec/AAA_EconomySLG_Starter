using System.Collections;
using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    public sealed class TurnBatchRunner : MonoBehaviour
    {
        [SerializeField] private SimulationBootstrapper simulation;
        [SerializeField] private SimulationSettingsAsset settings;
        [SerializeField, Min(1)] private int turnsPerFrame = 4;

        private Coroutine _running;

        public bool IsRunning => _running != null;

        public void RunTurns(int totalTurns)
        {
            if (_running != null || simulation == null || totalTurns <= 0)
                return;

            _running = StartCoroutine(RunTurnsRoutine(totalTurns));
        }

        public void Cancel()
        {
            if (_running == null)
                return;

            StopCoroutine(_running);
            _running = null;
        }

        private IEnumerator RunTurnsRoutine(int totalTurns)
        {
            int processed = 0;
            double totalMilliseconds = 0;

            while (processed < totalTurns &&
                !simulation.IsCampaignFinished)
            {
                int frameBudget = settings != null
                    ? settings.TurnsPerFrame
                    : turnsPerFrame;

                int batch = Mathf.Min(
                    frameBudget,
                    totalTurns - processed);

                for (int i = 0; i < batch; i++)
                {
                    if (simulation.IsCampaignFinished)
                        break;

                    totalMilliseconds +=
                        simulation.ResolveCurrentTurn(false)
                            .Performance.ElapsedMilliseconds;
                    processed++;
                }

                yield return null;
            }

            _running = null;
            Debug.Log(
                $"{processed}턴 배치 처리 완료. " +
                $"순수 시뮬레이션 시간 {totalMilliseconds:F3}ms");
        }
    }
}
