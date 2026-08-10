using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Resources
{
    public enum ExtractionMethod
    {
        Surface,
        DeepMining,
        ManagedForestry,
        Mechanized,
        AdvancedSurvey
    }

    public sealed class ResourceExtractionSite
    {
        public string Id { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public TurnNumber DiscoveryTurn { get; }
        public decimal InitialOutput { get; }
        public decimal MinimumOutput { get; }
        public decimal DeclineRatePerTurn { get; }
        public decimal TotalReserve { get; private set; }
        public decimal RemainingReserve { get; private set; }
        public decimal ExtractionEfficiency { get; private set; }
        public decimal TemporaryEfficiencyModifier { get; private set; }
        public decimal Labor { get; private set; }
        public decimal RequiredLabor { get; }
        public string OwnerFactionId { get; private set; }
        public ExtractionMethod Method { get; private set; }
        public decimal LastExtractedOutput { get; private set; }
        public bool IsActive { get; private set; }

        public ResourceExtractionSite(
            string id,
            RegionId regionId,
            ResourceId resourceId,
            TurnNumber discoveryTurn,
            decimal initialOutput,
            decimal minimumOutput,
            decimal declineRatePerTurn)
            : this(
                id,
                regionId,
                resourceId,
                discoveryTurn,
                initialOutput,
                minimumOutput,
                declineRatePerTurn,
                initialOutput * 10000m,
                1m,
                100m,
                100m,
                string.Empty,
                ExtractionMethod.Surface)
        {
        }

        public ResourceExtractionSite(
            string id,
            RegionId regionId,
            ResourceId resourceId,
            TurnNumber discoveryTurn,
            decimal initialOutput,
            decimal minimumOutput,
            decimal declineRatePerTurn,
            decimal totalReserve,
            decimal extractionEfficiency,
            decimal labor,
            decimal requiredLabor,
            string ownerFactionId,
            ExtractionMethod method)
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
            TotalReserve = Math.Max(initialOutput, totalReserve);
            RemainingReserve = TotalReserve;
            ExtractionEfficiency = Math.Clamp(
                extractionEfficiency,
                0.05m,
                3m);
            TemporaryEfficiencyModifier = 1m;
            Labor = Math.Max(0m, labor);
            RequiredLabor = Math.Max(0.01m, requiredLabor);
            OwnerFactionId = ownerFactionId ?? string.Empty;
            Method = method;
            IsActive = true;
        }

        public decimal GetOutput(TurnNumber currentTurn)
        {
            if (!IsActive ||
                RemainingReserve <= 0m ||
                currentTurn.Value < DiscoveryTurn.Value)
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
                {
                    output = MinimumOutput;
                    break;
                }
            }

            decimal reserveRatio = TotalReserve <= 0m
                ? 0m
                : RemainingReserve / TotalReserve;
            // 상층 매장량이 충분할 때는 기존 생산곡선을 보존하고,
            // 75% 아래부터 채굴 난도가 점진적으로 증가한다.
            decimal reserveEfficiency = reserveRatio >= 0.75m
                ? 1m
                : 0.35m + 0.65m * (decimal)Math.Sqrt(
                    (double)(reserveRatio / 0.75m));
            decimal laborEfficiency = Math.Clamp(
                Labor / RequiredLabor,
                0m,
                1.25m);
            decimal methodEfficiency = GetMethodEfficiency(Method);
            decimal adjustedOutput = output *
                reserveEfficiency *
                laborEfficiency *
                ExtractionEfficiency *
                TemporaryEfficiencyModifier *
                methodEfficiency;
            decimal adjustedMinimum = MinimumOutput *
                laborEfficiency *
                Math.Min(1m, ExtractionEfficiency) *
                TemporaryEfficiencyModifier;

            return Math.Min(
                RemainingReserve,
                Math.Max(adjustedMinimum, adjustedOutput));
        }

        public decimal Extract(TurnNumber currentTurn)
        {
            decimal output = GetOutput(currentTurn);
            RemainingReserve = Math.Max(0m, RemainingReserve - output);
            LastExtractedOutput = output;
            return output;
        }

        public void AssignOwner(string ownerFactionId)
        {
            OwnerFactionId = ownerFactionId ?? string.Empty;
        }

        public void SetLabor(decimal labor)
        {
            Labor = Math.Max(0m, labor);
        }

        public void ChangeMethod(ExtractionMethod method)
        {
            Method = method;
        }

        public void ImproveEfficiency(decimal additiveBonus)
        {
            ExtractionEfficiency = Math.Clamp(
                ExtractionEfficiency + additiveBonus,
                0.05m,
                3m);
        }

        public void ApplyTemporaryEfficiency(decimal multiplier)
        {
            TemporaryEfficiencyModifier = Math.Clamp(
                multiplier,
                0.05m,
                3m);
        }

        public void RestoreTemporaryEfficiency()
        {
            TemporaryEfficiencyModifier = 1m;
        }

        public void DiscoverAdditionalReserve(decimal amount)
        {
            amount = Math.Max(0m, amount);
            TotalReserve += amount;
            RemainingReserve += amount;
        }

        public void DevelopDeepLayer(
            decimal additionalReserve,
            decimal efficiencyBonus)
        {
            DiscoverAdditionalReserve(additionalReserve);
            ImproveEfficiency(efficiencyBonus);
            Method = ExtractionMethod.DeepMining;
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }

        private static decimal GetMethodEfficiency(ExtractionMethod method)
        {
            switch (method)
            {
                case ExtractionMethod.DeepMining:
                    return 0.92m;
                case ExtractionMethod.ManagedForestry:
                    return 1.04m;
                case ExtractionMethod.Mechanized:
                    return 1.28m;
                case ExtractionMethod.AdvancedSurvey:
                    return 1.16m;
                default:
                    return 1m;
            }
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
