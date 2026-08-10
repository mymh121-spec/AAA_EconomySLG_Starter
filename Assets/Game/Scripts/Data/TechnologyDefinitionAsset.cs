using UnityEngine;
using Game.Domain.Technology;

namespace Game.Data
{
    [System.Serializable]
    public sealed class TechnologyEffectAsset
    {
        public TechnologyEffectType type;
        public float value;
        public string targetId;

        public TechnologyEffect ToDomain()
        {
            return new TechnologyEffect(
                type,
                (decimal)value,
                targetId);
        }
    }

    [CreateAssetMenu(
        fileName = "TechnologyDefinition",
        menuName = "게임/기술/기술 정의")]
    public sealed class TechnologyDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField, Min(1f)] private float researchCost = 100f;
        [SerializeField] private string[] prerequisites;
        [SerializeField] private TechnologyEffectAsset[] effects;

        public TechnologyDefinition ToDomain()
        {
            var domainEffects =
                new TechnologyEffect[effects?.Length ?? 0];

            for (int i = 0; i < domainEffects.Length; i++)
                domainEffects[i] = effects[i].ToDomain();

            return new TechnologyDefinition(
                id,
                displayName,
                (decimal)researchCost,
                prerequisites,
                domainEffects);
        }
    }
}
