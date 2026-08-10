using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Resources;

namespace Game.Application.World
{
    public readonly struct ResourceSiteSpawnRecord
    {
        public string SiteId { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public decimal InitialOutput { get; }
        public decimal MinimumOutput { get; }

        public ResourceSiteSpawnRecord(ResourceExtractionSite site)
        {
            SiteId = site.Id;
            RegionId = site.RegionId;
            ResourceId = site.ResourceId;
            InitialOutput = site.InitialOutput;
            MinimumOutput = site.MinimumOutput;
        }
    }

    public readonly struct ResourceSiteProductionRecord
    {
        public string SiteId { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public decimal Output { get; }

        public ResourceSiteProductionRecord(
            ResourceExtractionSite site,
            decimal output)
        {
            SiteId = site.Id;
            RegionId = site.RegionId;
            ResourceId = site.ResourceId;
            Output = Math.Max(0m, output);
        }
    }

    public sealed class ResourceSiteTurnReport
    {
        public static ResourceSiteTurnReport Empty { get; } =
            new ResourceSiteTurnReport(
                Array.Empty<ResourceSiteSpawnRecord>(),
                Array.Empty<ResourceSiteProductionRecord>());

        public IReadOnlyList<ResourceSiteSpawnRecord> SpawnedSites { get; }
        public IReadOnlyList<ResourceSiteProductionRecord> Production { get; }

        public ResourceSiteTurnReport(
            IReadOnlyList<ResourceSiteSpawnRecord> spawnedSites,
            IReadOnlyList<ResourceSiteProductionRecord> production)
        {
            SpawnedSites = spawnedSites ??
                Array.Empty<ResourceSiteSpawnRecord>();
            Production = production ??
                Array.Empty<ResourceSiteProductionRecord>();
        }
    }

    public sealed class ResourceSiteEventSystem
    {
        private readonly WorldEconomyState _world;
        private readonly ResourceSiteEventSettings _settings;
        private readonly List<MarketRuntimeState> _candidateBuffer =
            new List<MarketRuntimeState>(16);

        public ResourceSiteEventSystem(
            WorldEconomyState world,
            ResourceSiteEventSettings settings)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
            _settings = settings ??
                throw new ArgumentNullException(nameof(settings));
        }

        public ResourceSiteTurnReport ProcessTurn(TurnNumber turn)
        {
            var spawned = new List<ResourceSiteSpawnRecord>(1);
            TrySpawnSite(turn, spawned);

            var production = new List<ResourceSiteProductionRecord>(
                _world.ResourceSites.Count);

            for (int i = 0; i < _world.ResourceSites.Count; i++)
            {
                ResourceExtractionSite site = _world.ResourceSites[i];
                decimal output = site.GetOutput(turn);
                if (output <= 0m)
                    continue;

                production.Add(new ResourceSiteProductionRecord(
                    site,
                    output));
            }

            return new ResourceSiteTurnReport(spawned, production);
        }

        private void TrySpawnSite(
            TurnNumber turn,
            List<ResourceSiteSpawnRecord> spawned)
        {
            if (turn.Value % _settings.SpawnIntervalTurns != 0)
                return;

            string sitePrefix = $"resource_site_{turn.Value:D3}_";
            for (int i = 0; i < _world.ResourceSites.Count; i++)
            {
                if (_world.ResourceSites[i].Id.StartsWith(
                    sitePrefix,
                    StringComparison.Ordinal))
                {
                    return;
                }
            }

            _candidateBuffer.Clear();
            for (int i = 0; i < _world.Markets.Count; i++)
            {
                MarketRuntimeState market = _world.Markets[i];
                if (_settings.Allows(market.Definition.Id))
                    _candidateBuffer.Add(market);
            }

            if (_candidateBuffer.Count == 0)
                return;

            _candidateBuffer.Sort(CompareMarkets);
            int eventIndex = turn.Value / _settings.SpawnIntervalTurns - 1;
            MarketRuntimeState selected =
                _candidateBuffer[eventIndex % _candidateBuffer.Count];
            string siteId = sitePrefix +
                selected.RegionId.Value + "_" +
                selected.Definition.Id.Value;

            var site = new ResourceExtractionSite(
                siteId,
                selected.RegionId,
                selected.Definition.Id,
                turn,
                _settings.InitialOutput,
                _settings.MinimumOutput,
                _settings.DeclineRatePerTurn);

            if (_world.RegisterResourceSite(site))
                spawned.Add(new ResourceSiteSpawnRecord(site));
        }

        private static int CompareMarkets(
            MarketRuntimeState left,
            MarketRuntimeState right)
        {
            int regionComparison = string.CompareOrdinal(
                left.RegionId.Value,
                right.RegionId.Value);
            if (regionComparison != 0)
                return regionComparison;

            return string.CompareOrdinal(
                left.Definition.Id.Value,
                right.Definition.Id.Value);
        }
    }
}
