using UnityEngine;
using Game.Domain.Missions;

namespace Game.Data
{
    [CreateAssetMenu(
        fileName = "MissionDefinition",
        menuName = "게임/미션/미션 정의")]
    public sealed class MissionDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private MissionType type;
        [SerializeField] private int durationDays = 1;
        [SerializeField, Range(0f, 1f)] private float baseRisk = 0.2f;
        [SerializeField] private float requiredPower = 1f;
        [SerializeField] private float reward = 1000f;

        public MissionDefinition ToDomain()
        {
            return new MissionDefinition(
                id,
                type,
                durationDays,
                (decimal)baseRisk,
                (decimal)requiredPower,
                (decimal)reward,
                displayName);
        }
    }
}
