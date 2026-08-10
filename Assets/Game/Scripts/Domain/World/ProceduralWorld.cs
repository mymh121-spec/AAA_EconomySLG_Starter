using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Resources;

namespace Game.Domain.World
{
    public enum TerrainType
    {
        Plains,
        Forest,
        Hills,
        Mountains,
        Drylands,
        Coast
    }

    public enum SettlementKind
    {
        None,
        Village,
        Town,
        City,
        Capital
    }

    public enum WorldFacilityKind
    {
        Farm,
        LumberCamp,
        Mine,
        Workshop,
        Arsenal,
        Barracks,
        Market,
        Warehouse
    }

    public enum WorldNpcRole
    {
        Governor,
        Merchant,
        Commander,
        Engineer,
        Scout,
        LocalLeader
    }

    public enum DiplomaticStance
    {
        Allied,
        Friendly,
        Neutral,
        Rival,
        Hostile,
        War
    }

    public sealed class WorldGenerationSettings
    {
        public int RegionCount { get; }
        public int FactionCount { get; }
        public int SettlementCount { get; }
        public int NpcCount { get; }
        public int InitialResourceSiteCount { get; }
        public int MinimumPopulation { get; }
        public int MaximumPopulation { get; }
        public decimal InitialResourceReserve { get; }
        public decimal InitialSiteOutput { get; }
        public decimal MinimumSiteOutput { get; }
        public decimal SiteDeclineRate { get; }

        public WorldGenerationSettings(
            int regionCount = 6,
            int factionCount = 3,
            int settlementCount = 5,
            int npcCount = 12,
            int initialResourceSiteCount = 8,
            int minimumPopulation = 800,
            int maximumPopulation = 8000,
            decimal initialResourceReserve = 12000m,
            decimal initialSiteOutput = 85m,
            decimal minimumSiteOutput = 12m,
            decimal siteDeclineRate = 0.015m)
        {
            RegionCount = Math.Max(2, regionCount);
            FactionCount = Math.Clamp(
                factionCount,
                2,
                RegionCount);
            SettlementCount = Math.Clamp(
                settlementCount,
                FactionCount,
                RegionCount);
            NpcCount = Math.Max(FactionCount, npcCount);
            InitialResourceSiteCount = Math.Max(
                1,
                initialResourceSiteCount);
            MinimumPopulation = Math.Max(100, minimumPopulation);
            MaximumPopulation = Math.Max(
                MinimumPopulation,
                maximumPopulation);
            InitialResourceReserve = Math.Max(
                100m,
                initialResourceReserve);
            InitialSiteOutput = Math.Max(0.1m, initialSiteOutput);
            MinimumSiteOutput = Math.Clamp(
                minimumSiteOutput,
                0.1m,
                InitialSiteOutput);
            SiteDeclineRate = Math.Clamp(siteDeclineRate, 0m, 0.5m);
        }
    }

    public sealed class GeneratedRegionState
    {
        public RegionId Id { get; }
        public string DisplayName { get; }
        public TerrainType Terrain { get; }
        public SettlementKind Settlement { get; internal set; }
        public string OwnerFactionId { get; internal set; }
        public int Population { get; private set; }
        public decimal ForestDensity { get; }
        public decimal MineralPotential { get; }
        public decimal Fertility { get; }
        public decimal Stability { get; private set; }
        public decimal BanditThreat { get; private set; }

        public GeneratedRegionState(
            RegionId id,
            string displayName,
            TerrainType terrain,
            int population,
            decimal forestDensity,
            decimal mineralPotential,
            decimal fertility,
            decimal stability,
            decimal banditThreat)
        {
            Id = id;
            DisplayName = displayName ?? id.Value;
            Terrain = terrain;
            Population = Math.Max(0, population);
            ForestDensity = Math.Clamp(forestDensity, 0m, 1m);
            MineralPotential = Math.Clamp(mineralPotential, 0m, 1m);
            Fertility = Math.Clamp(fertility, 0m, 1m);
            Stability = Math.Clamp(stability, 0m, 1m);
            BanditThreat = Math.Clamp(banditThreat, 0m, 1m);
            Settlement = SettlementKind.None;
            OwnerFactionId = string.Empty;
        }

        public void AdjustStability(decimal delta)
        {
            Stability = Math.Clamp(Stability + delta, 0m, 1m);
        }

        public void AdjustBanditThreat(decimal delta)
        {
            BanditThreat = Math.Clamp(BanditThreat + delta, 0m, 1m);
        }

        public void AdjustPopulation(int delta)
        {
            Population = Math.Max(0, Population + delta);
        }
    }

    public sealed class WorldFactionState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public decimal Treasury { get; private set; }
        public decimal IndustrialFocus { get; }
        public decimal MilitaryFocus { get; }
        public decimal ExpansionDrive { get; }

        public WorldFactionState(
            string id,
            string displayName,
            decimal treasury,
            decimal industrialFocus,
            decimal militaryFocus,
            decimal expansionDrive)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? Id;
            Treasury = Math.Max(0m, treasury);
            IndustrialFocus = Math.Clamp(industrialFocus, 0m, 1m);
            MilitaryFocus = Math.Clamp(militaryFocus, 0m, 1m);
            ExpansionDrive = Math.Clamp(expansionDrive, 0m, 1m);
        }

        public bool TrySpend(decimal amount)
        {
            amount = Math.Max(0m, amount);
            if (Treasury < amount)
                return false;
            Treasury -= amount;
            return true;
        }

        public void AddTreasury(decimal amount)
        {
            Treasury += Math.Max(0m, amount);
        }
    }

    public sealed class FactionRelationState
    {
        public string FirstFactionId { get; }
        public string SecondFactionId { get; }
        public decimal Score { get; private set; }
        public DiplomaticStance Stance => Score >= 70m
            ? DiplomaticStance.Allied
            : Score >= 30m
                ? DiplomaticStance.Friendly
                : Score > -30m
                    ? DiplomaticStance.Neutral
                    : Score > -60m
                        ? DiplomaticStance.Rival
                        : Score > -85m
                            ? DiplomaticStance.Hostile
                            : DiplomaticStance.War;

        public FactionRelationState(
            string firstFactionId,
            string secondFactionId,
            decimal score)
        {
            FirstFactionId = firstFactionId ?? string.Empty;
            SecondFactionId = secondFactionId ?? string.Empty;
            Score = Math.Clamp(score, -100m, 100m);
        }

        public void Adjust(decimal delta)
        {
            Score = Math.Clamp(Score + delta, -100m, 100m);
        }
    }

    public sealed class WorldNpcState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public WorldNpcRole Role { get; }
        public string FactionId { get; }
        public RegionId RegionId { get; }
        public decimal Competence { get; }
        public decimal Initiative { get; }
        public bool IsAlive { get; private set; }

        public WorldNpcState(
            string id,
            string displayName,
            WorldNpcRole role,
            string factionId,
            RegionId regionId,
            decimal competence,
            decimal initiative)
        {
            Id = id ?? string.Empty;
            DisplayName = displayName ?? Id;
            Role = role;
            FactionId = factionId ?? string.Empty;
            RegionId = regionId;
            Competence = Math.Clamp(competence, 0m, 1m);
            Initiative = Math.Clamp(initiative, 0m, 1m);
            IsAlive = true;
        }

        public void Kill()
        {
            IsAlive = false;
        }
    }

    public sealed class WorldFacilityState
    {
        public string Id { get; }
        public WorldFacilityKind Kind { get; }
        public RegionId RegionId { get; }
        public string OwnerFactionId { get; }
        public ResourceId? InputResourceId { get; }
        public ResourceId? OutputResourceId { get; }
        public decimal InputPerTurn { get; }
        public decimal OutputPerTurn { get; }
        public decimal LaborDemand { get; }
        public decimal MaintenanceCost { get; }
        public decimal OperatingRatio { get; private set; }
        public bool IsOperational { get; private set; }

        public WorldFacilityState(
            string id,
            WorldFacilityKind kind,
            RegionId regionId,
            string ownerFactionId,
            ResourceId? inputResourceId,
            ResourceId? outputResourceId,
            decimal inputPerTurn,
            decimal outputPerTurn,
            decimal laborDemand,
            decimal maintenanceCost,
            decimal operatingRatio = 1m)
        {
            Id = id ?? string.Empty;
            Kind = kind;
            RegionId = regionId;
            OwnerFactionId = ownerFactionId ?? string.Empty;
            InputResourceId = inputResourceId;
            OutputResourceId = outputResourceId;
            InputPerTurn = Math.Max(0m, inputPerTurn);
            OutputPerTurn = Math.Max(0m, outputPerTurn);
            LaborDemand = Math.Max(0m, laborDemand);
            MaintenanceCost = Math.Max(0m, maintenanceCost);
            OperatingRatio = Math.Clamp(operatingRatio, 0m, 1m);
            IsOperational = true;
        }

        public void SetOperational(bool operational)
        {
            IsOperational = operational;
        }

        public void SetOperatingRatio(decimal ratio)
        {
            OperatingRatio = Math.Clamp(ratio, 0m, 1m);
        }
    }

    public readonly struct RegionalEconomySeed
    {
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public decimal SupplyMultiplier { get; }
        public decimal DemandMultiplier { get; }
        public decimal StockMultiplier { get; }

        public RegionalEconomySeed(
            RegionId regionId,
            ResourceId resourceId,
            decimal supplyMultiplier,
            decimal demandMultiplier,
            decimal stockMultiplier)
        {
            RegionId = regionId;
            ResourceId = resourceId;
            SupplyMultiplier = Math.Clamp(supplyMultiplier, 0.1m, 3m);
            DemandMultiplier = Math.Clamp(demandMultiplier, 0.1m, 3m);
            StockMultiplier = Math.Clamp(stockMultiplier, 0.1m, 3m);
        }
    }

    public readonly struct ResourceSiteSeed
    {
        public string Id { get; }
        public RegionId RegionId { get; }
        public ResourceId ResourceId { get; }
        public string OwnerFactionId { get; }
        public decimal TotalReserve { get; }
        public decimal InitialOutput { get; }
        public decimal MinimumOutput { get; }
        public decimal DeclineRate { get; }
        public decimal ExtractionEfficiency { get; }
        public decimal Labor { get; }
        public ExtractionMethod Method { get; }

        public ResourceSiteSeed(
            string id,
            RegionId regionId,
            ResourceId resourceId,
            string ownerFactionId,
            decimal totalReserve,
            decimal initialOutput,
            decimal minimumOutput,
            decimal declineRate,
            decimal extractionEfficiency,
            decimal labor,
            ExtractionMethod method)
        {
            Id = id;
            RegionId = regionId;
            ResourceId = resourceId;
            OwnerFactionId = ownerFactionId;
            TotalReserve = totalReserve;
            InitialOutput = initialOutput;
            MinimumOutput = minimumOutput;
            DeclineRate = declineRate;
            ExtractionEfficiency = extractionEfficiency;
            Labor = labor;
            Method = method;
        }
    }

    public sealed class ProceduralWorldState
    {
        public int Seed { get; }
        public IReadOnlyList<GeneratedRegionState> Regions { get; }
        public IReadOnlyList<WorldFactionState> Factions { get; }
        public IReadOnlyList<FactionRelationState> Relations { get; }
        public IReadOnlyList<WorldNpcState> Npcs { get; }
        public IReadOnlyList<WorldFacilityState> Facilities { get; }
        public IReadOnlyList<RegionalEconomySeed> EconomySeeds { get; }
        public IReadOnlyList<ResourceSiteSeed> ResourceSiteSeeds { get; }

        public ProceduralWorldState(
            int seed,
            IReadOnlyList<GeneratedRegionState> regions,
            IReadOnlyList<WorldFactionState> factions,
            IReadOnlyList<FactionRelationState> relations,
            IReadOnlyList<WorldNpcState> npcs,
            IReadOnlyList<WorldFacilityState> facilities,
            IReadOnlyList<RegionalEconomySeed> economySeeds,
            IReadOnlyList<ResourceSiteSeed> resourceSiteSeeds)
        {
            Seed = seed;
            Regions = regions ?? Array.Empty<GeneratedRegionState>();
            Factions = factions ?? Array.Empty<WorldFactionState>();
            Relations = relations ?? Array.Empty<FactionRelationState>();
            Npcs = npcs ?? Array.Empty<WorldNpcState>();
            Facilities = facilities ?? Array.Empty<WorldFacilityState>();
            EconomySeeds = economySeeds ?? Array.Empty<RegionalEconomySeed>();
            ResourceSiteSeeds = resourceSiteSeeds ??
                Array.Empty<ResourceSiteSeed>();
        }

        public GeneratedRegionState FindRegion(RegionId regionId)
        {
            for (int i = 0; i < Regions.Count; i++)
            {
                if (Regions[i].Id.Equals(regionId))
                    return Regions[i];
            }

            return null;
        }

        public WorldFactionState FindFaction(string factionId)
        {
            for (int i = 0; i < Factions.Count; i++)
            {
                if (string.Equals(
                    Factions[i].Id,
                    factionId,
                    StringComparison.Ordinal))
                {
                    return Factions[i];
                }
            }

            return null;
        }
    }

    public sealed class ProceduralWorldGenerator
    {
        private static readonly string[] RegionNames =
        {
            "서녘 평원", "검은숲", "바람 고원", "백철 산맥",
            "남부 건조지", "푸른 해안", "붉은 구릉", "은빛 분지"
        };

        private static readonly string[] FactionNames =
        {
            "청람 연맹", "적월 공국", "황금 상단", "북부 자유령"
        };

        private static readonly string[] NpcNames =
        {
            "라온", "세린", "도윤", "미라", "하진", "이안",
            "유나", "태오", "리안", "소라", "카일", "나린"
        };

        public ProceduralWorldState Generate(
            int seed,
            string regionIdPrefix,
            WorldGenerationSettings settings,
            IReadOnlyList<ResourceId> availableResources)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));
            if (availableResources == null)
                throw new ArgumentNullException(nameof(availableResources));

            var random = new Random(seed);
            string prefix = string.IsNullOrWhiteSpace(regionIdPrefix)
                ? "region"
                : regionIdPrefix.Trim().ToLowerInvariant();
            var regions = GenerateRegions(random, prefix, settings);
            var factions = GenerateFactions(random, settings);
            AssignRegionOwners(regions, factions, settings);
            var relations = GenerateRelations(random, factions);
            var npcs = GenerateNpcs(random, regions, factions, settings);
            var facilities = GenerateFacilities(
                random,
                regions,
                availableResources);
            var economySeeds = GenerateEconomySeeds(
                random,
                regions,
                availableResources);
            var siteSeeds = GenerateResourceSites(
                random,
                regions,
                availableResources,
                settings);

            return new ProceduralWorldState(
                seed,
                regions,
                factions,
                relations,
                npcs,
                facilities,
                economySeeds,
                siteSeeds);
        }

        private static List<GeneratedRegionState> GenerateRegions(
            Random random,
            string prefix,
            WorldGenerationSettings settings)
        {
            var regions = new List<GeneratedRegionState>(
                settings.RegionCount);
            int terrainCount = Enum.GetValues(typeof(TerrainType)).Length;
            for (int i = 0; i < settings.RegionCount; i++)
            {
                TerrainType terrain = (TerrainType)random.Next(terrainCount);
                decimal forest = TerrainValue(
                    terrain,
                    TerrainType.Forest,
                    0.55m,
                    random);
                decimal mineral =
                    terrain == TerrainType.Mountains ||
                    terrain == TerrainType.Hills
                        ? NextDecimal(random, 0.65m, 1m)
                        : NextDecimal(random, 0.15m, 0.70m);
                decimal fertility =
                    terrain == TerrainType.Plains ||
                    terrain == TerrainType.Coast
                        ? NextDecimal(random, 0.60m, 1m)
                        : NextDecimal(random, 0.15m, 0.70m);
                int population = random.Next(
                    settings.MinimumPopulation,
                    settings.MaximumPopulation + 1);
                regions.Add(new GeneratedRegionState(
                    new RegionId($"{prefix}_{i + 1}"),
                    RegionNames[i % RegionNames.Length],
                    terrain,
                    population,
                    forest,
                    mineral,
                    fertility,
                    NextDecimal(random, 0.45m, 0.90m),
                    NextDecimal(random, 0.05m, 0.45m)));
            }

            for (int i = 0; i < settings.SettlementCount; i++)
            {
                regions[i].Settlement = i < settings.FactionCount
                    ? SettlementKind.Capital
                    : i % 2 == 0
                        ? SettlementKind.City
                        : SettlementKind.Town;
            }

            return regions;
        }

        private static List<WorldFactionState> GenerateFactions(
            Random random,
            WorldGenerationSettings settings)
        {
            var factions = new List<WorldFactionState>(
                settings.FactionCount);
            for (int i = 0; i < settings.FactionCount; i++)
            {
                factions.Add(new WorldFactionState(
                    $"faction_{i + 1}",
                    FactionNames[i % FactionNames.Length],
                    NextDecimal(random, 50000m, 180000m),
                    NextDecimal(random, 0.25m, 0.90m),
                    NextDecimal(random, 0.25m, 0.90m),
                    NextDecimal(random, 0.10m, 0.85m)));
            }

            return factions;
        }

        private static void AssignRegionOwners(
            List<GeneratedRegionState> regions,
            List<WorldFactionState> factions,
            WorldGenerationSettings settings)
        {
            for (int i = 0; i < regions.Count; i++)
            {
                regions[i].OwnerFactionId =
                    factions[i % factions.Count].Id;
            }
        }

        private static List<FactionRelationState> GenerateRelations(
            Random random,
            List<WorldFactionState> factions)
        {
            var relations = new List<FactionRelationState>();
            for (int i = 0; i < factions.Count; i++)
            {
                for (int j = i + 1; j < factions.Count; j++)
                {
                    relations.Add(new FactionRelationState(
                        factions[i].Id,
                        factions[j].Id,
                        NextDecimal(random, -75m, 55m)));
                }
            }

            return relations;
        }

        private static List<WorldNpcState> GenerateNpcs(
            Random random,
            List<GeneratedRegionState> regions,
            List<WorldFactionState> factions,
            WorldGenerationSettings settings)
        {
            var npcs = new List<WorldNpcState>(settings.NpcCount);
            int roleCount = Enum.GetValues(typeof(WorldNpcRole)).Length;
            for (int i = 0; i < settings.NpcCount; i++)
            {
                GeneratedRegionState region = regions[i % regions.Count];
                npcs.Add(new WorldNpcState(
                    $"npc_{i + 1}",
                    NpcNames[i % NpcNames.Length],
                    (WorldNpcRole)random.Next(roleCount),
                    region.OwnerFactionId,
                    region.Id,
                    NextDecimal(random, 0.30m, 0.95m),
                    NextDecimal(random, 0.20m, 0.90m)));
            }

            return npcs;
        }

        private static List<WorldFacilityState> GenerateFacilities(
            Random random,
            List<GeneratedRegionState> regions,
            IReadOnlyList<ResourceId> resources)
        {
            var facilities = new List<WorldFacilityState>(
                regions.Count * 2);
            ResourceId? food = FindResource(resources, "food");
            ResourceId? wood = FindResource(resources, "wood");
            ResourceId? iron = FindResource(resources, "iron");
            ResourceId? steel = FindResource(resources, "steel");

            for (int i = 0; i < regions.Count; i++)
            {
                GeneratedRegionState region = regions[i];
                if (food.HasValue)
                {
                    facilities.Add(new WorldFacilityState(
                        $"facility_farm_{i + 1}",
                        WorldFacilityKind.Farm,
                        region.Id,
                        region.OwnerFactionId,
                        null,
                        food,
                        0m,
                        25m + 45m * region.Fertility,
                        12m,
                        45m));
                }
                if (wood.HasValue && region.ForestDensity >= 0.45m)
                {
                    facilities.Add(new WorldFacilityState(
                        $"facility_lumber_{i + 1}",
                        WorldFacilityKind.LumberCamp,
                        region.Id,
                        region.OwnerFactionId,
                        null,
                        wood,
                        0m,
                        18m + 42m * region.ForestDensity,
                        10m,
                        38m));
                }
                if (iron.HasValue && steel.HasValue &&
                    region.Settlement >= SettlementKind.Town)
                {
                    facilities.Add(new WorldFacilityState(
                        $"facility_workshop_{i + 1}",
                        WorldFacilityKind.Workshop,
                        region.Id,
                        region.OwnerFactionId,
                        iron,
                        steel,
                        16m,
                        9m,
                        18m,
                        90m,
                        NextDecimal(random, 0.55m, 0.95m)));
                }
            }

            return facilities;
        }

        private static List<RegionalEconomySeed> GenerateEconomySeeds(
            Random random,
            List<GeneratedRegionState> regions,
            IReadOnlyList<ResourceId> resources)
        {
            var seeds = new List<RegionalEconomySeed>(
                regions.Count * resources.Count);
            for (int i = 0; i < regions.Count; i++)
            {
                GeneratedRegionState region = regions[i];
                decimal populationFactor = Math.Clamp(
                    region.Population / 4000m,
                    0.45m,
                    1.8m);
                for (int j = 0; j < resources.Count; j++)
                {
                    decimal naturalSupply = 0.75m;
                    string id = resources[j].Value;
                    if (id == "wood")
                        naturalSupply += region.ForestDensity;
                    else if (id == "iron" || id == "coal" || id == "oil")
                        naturalSupply += region.MineralPotential;
                    else if (id == "food")
                        naturalSupply += region.Fertility;

                    seeds.Add(new RegionalEconomySeed(
                        region.Id,
                        resources[j],
                        naturalSupply * NextDecimal(random, 0.75m, 1.15m),
                        populationFactor * NextDecimal(random, 0.80m, 1.20m),
                        NextDecimal(random, 0.65m, 1.35m)));
                }
            }

            return seeds;
        }

        private static List<ResourceSiteSeed> GenerateResourceSites(
            Random random,
            List<GeneratedRegionState> regions,
            IReadOnlyList<ResourceId> resources,
            WorldGenerationSettings settings)
        {
            var sites = new List<ResourceSiteSeed>(
                settings.InitialResourceSiteCount);
            if (resources.Count == 0)
                return sites;

            for (int i = 0; i < settings.InitialResourceSiteCount; i++)
            {
                GeneratedRegionState region = regions[i % regions.Count];
                ResourceId resource = SelectNaturalResource(
                    random,
                    region,
                    resources);
                decimal potential = resource.Value == "wood"
                    ? region.ForestDensity
                    : resource.Value == "food"
                        ? region.Fertility
                        : region.MineralPotential;
                sites.Add(new ResourceSiteSeed(
                    $"initial_site_{i + 1:D2}",
                    region.Id,
                    resource,
                    region.OwnerFactionId,
                    settings.InitialResourceReserve *
                        NextDecimal(random, 0.65m, 1.50m),
                    settings.InitialSiteOutput *
                        (0.65m + potential * 0.70m),
                    settings.MinimumSiteOutput,
                    settings.SiteDeclineRate,
                    NextDecimal(random, 0.70m, 1.05m),
                    NextDecimal(random, 30m, 100m),
                    region.Terrain == TerrainType.Mountains
                        ? ExtractionMethod.DeepMining
                        : resource.Value == "wood"
                            ? ExtractionMethod.ManagedForestry
                            : ExtractionMethod.Surface));
            }

            return sites;
        }

        private static ResourceId SelectNaturalResource(
            Random random,
            GeneratedRegionState region,
            IReadOnlyList<ResourceId> resources)
        {
            string preferred = region.ForestDensity > 0.65m
                ? "wood"
                : region.Fertility > 0.72m
                    ? "food"
                    : random.Next(2) == 0
                        ? "iron"
                        : "coal";
            ResourceId? selected = FindResource(resources, preferred);
            return selected ?? resources[random.Next(resources.Count)];
        }

        private static ResourceId? FindResource(
            IReadOnlyList<ResourceId> resources,
            string id)
        {
            for (int i = 0; i < resources.Count; i++)
            {
                if (resources[i].Value == id)
                    return resources[i];
            }

            return null;
        }

        private static decimal TerrainValue(
            TerrainType terrain,
            TerrainType preferred,
            decimal preferredMinimum,
            Random random)
        {
            return terrain == preferred
                ? NextDecimal(random, preferredMinimum, 1m)
                : NextDecimal(random, 0.05m, 0.60m);
        }

        private static decimal NextDecimal(
            Random random,
            decimal minimum,
            decimal maximum)
        {
            return minimum +
                (maximum - minimum) * (decimal)random.NextDouble();
        }
    }
}
