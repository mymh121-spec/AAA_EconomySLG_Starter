using UnityEngine;
using Game.Domain.Common;
using Game.Domain.Logistics;

namespace Game.Data
{
    [CreateAssetMenu(
        fileName = "TradeRoute",
        menuName = "게임/물류/무역로 정의")]
    public sealed class TradeRouteAsset : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private string displayName;
        [SerializeField] private string originRegionId;
        [SerializeField] private string destinationRegionId;
        [SerializeField, Min(1)] private int travelDays = 1;
        [SerializeField, Min(0f)] private float dailyCapacity = 100f;
        [SerializeField, Range(0f, 1f)] private float baseLossRate = 0.02f;
        [SerializeField, Min(0f)] private float tollPerUnit = 1f;

        public string DisplayName => displayName;

        public TradeRoute ToDomain()
        {
            return new TradeRoute(
                id,
                new RegionId(originRegionId),
                new RegionId(destinationRegionId),
                travelDays,
                (decimal)dailyCapacity,
                (decimal)baseLossRate,
                (decimal)tollPerUnit);
        }
    }
}
