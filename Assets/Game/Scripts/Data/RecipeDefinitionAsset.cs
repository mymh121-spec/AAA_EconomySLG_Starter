using UnityEngine;
using Game.Domain.Common;
using Game.Domain.Production;

namespace Game.Data
{
    [System.Serializable]
    public sealed class ResourceAmountAsset
    {
        public string resourceId;
        public float amount = 1f;

        public ResourceAmount ToDomain()
        {
            return new ResourceAmount(
                new ResourceId(resourceId),
                (decimal)amount);
        }
    }

    [CreateAssetMenu(
        fileName = "RecipeDefinition",
        menuName = "게임/경제/생산 레시피")]
    public sealed class RecipeDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private ResourceAmountAsset[] inputs;
        [SerializeField] private ResourceAmountAsset[] outputs;
        [SerializeField] private float laborRequired = 10f;
        [SerializeField] private float powerRequired = 5f;
        [SerializeField] private int daysPerCycle = 1;

        public RecipeDefinition ToDomain()
        {
            var domainInputs = new ResourceAmount[inputs?.Length ?? 0];
            var domainOutputs = new ResourceAmount[outputs?.Length ?? 0];

            for (int i = 0; i < domainInputs.Length; i++)
                domainInputs[i] = inputs[i].ToDomain();

            for (int i = 0; i < domainOutputs.Length; i++)
                domainOutputs[i] = outputs[i].ToDomain();

            return new RecipeDefinition(
                id,
                domainInputs,
                domainOutputs,
                (decimal)laborRequired,
                (decimal)powerRequired,
                daysPerCycle,
                displayName);
        }
    }
}
