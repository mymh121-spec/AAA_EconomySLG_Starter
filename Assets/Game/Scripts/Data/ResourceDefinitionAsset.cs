using UnityEngine;
using Game.Domain.Common;
using Game.Domain.Resources;

namespace Game.Data
{
    [CreateAssetMenu(
        fileName = "ResourceDefinition",
        menuName = "게임/경제/자원 정의")]
    public sealed class ResourceDefinitionAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private float basePrice = 100f;
        [SerializeField] private ResourceRarity rarity;
        [SerializeField] private float storageVolume = 1f;
        [SerializeField] private bool isPerishable;

        public ResourceDefinition ToDomain()
        {
            return new ResourceDefinition(
                new ResourceId(id),
                displayName,
                (decimal)basePrice,
                rarity,
                (decimal)storageVolume,
                isPerishable);
        }
    }
}
