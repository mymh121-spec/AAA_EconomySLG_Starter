using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Resources
{
    public sealed class ResourceExtractionSite
    {
        public string Id { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public TurnNumber DiscoveryTurn { get; }
        public decimal InitialOutput { get; }
        public decimal MinimumOutput { get; }
        public decimal DeclineRatePerTurn { get; }
        public bool IsActive { get; private set; }

        public ResourceExtractionSite(
            string id,
            RegionId regionId,
            ResourceId resourceId,
            TurnNumber discoveryTurn,
            decimal initialOutput,
            decimal minimumOutput,
            decimal declineRatePerTurn)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("Site ID cannot be empty.", nameof(id));
            if (initialOutput <= 0m)
                throw new ArgumentOutOfRangeException(nameof(initialOutput));

            Id = id.Trim();
            RegionId = regionId;
            ResourceId = resourceId;
            DiscoveryTurn = discoveryTurn;
            InitialOutput = initialOutput;
            MinimumOutput = Math.Clamp(minimumOutput, 0.01m, initialOutput);
            DeclineRatePerTurn = Math.Clamp(declineRatePerTurn, 0m, 1m);
            IsActive = true;
        }

        public decimal GetOutput(TurnNumber currentTurn)
        {
            if (!IsActive || currentTurn.Value < DiscoveryTurn.Value)
                return 0m;

            decimal output = InitialOutput;
            decimal retentionRate = 1m - DeclineRatePerTurn;
            int elapsedTurns = currentTurn.Value - DiscoveryTurn.Value;

            for (int i = 0; i < elapsedTurns; i++)
            {
                output = decimal.Round(
                    output * retentionRate,
                    4,
                    MidpointRounding.AwayFromZero);

                if (output <= MinimumOutput)
                    return MinimumOutput;
            }

            return Math.Max(MinimumOutput, output);
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }
    }

    public sealed class ResourceSiteEventSettings
    {
        private static readonly string[] DefaultResourceIds =
        {
            "iron",
            "coal",
            "wood",
            "oil"
        };

        private readonly List<ResourceId> _allowedResourceIds =
            new List<ResourceId>(4);

        public int SpawnIntervalTurns { get; }
        public decimal InitialOutput { get; }
        public decimal MinimumOutput { get; }
        public decimal DeclineRatePerTurn { get; }
        public IReadOnlyList<ResourceId> AllowedResourceIds =>
            _allowedResourceIds;

        public ResourceSiteEventSettings(
            int spawnIntervalTurns = 5,
            decimal initialOutput = 100m,
            decimal minimumOutput = 20m,
            decimal declineRatePerTurn = 0.10m,
            IEnumerable<string> allowedResourceIds = null)
        {
            SpawnIntervalTurns = Math.Max(1, spawnIntervalTurns);
            InitialOutput = Math.Max(0.01m, initialOutput);
            MinimumOutput = Math.Clamp(
                minimumOutput,
                0.01m,
                InitialOutput);
            DeclineRatePerTurn = Math.Clamp(
                declineRatePerTurn,
                0m,
                1m);

            IEnumerable<string> source =
                allowedResourceIds ?? DefaultResourceIds;
            var unique = new HashSet<ResourceId>();

            foreach (string id in source)
            {
                if (string.IsNullOrWhiteSpace(id))
                    continue;

                var resourceId = new ResourceId(id);
                if (unique.Add(resourceId))
                    _allowedResourceIds.Add(resourceId);
            }
        }

        public bool Allows(ResourceId resourceId)
        {
            for (int i = 0; i < _allowedResourceIds.Count; i++)
            {
                if (_allowedResourceIds[i].Equals(resourceId))
                    return true;
            }

            return false;
        }
    }
}
