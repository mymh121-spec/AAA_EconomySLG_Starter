using System;
using System.Collections.Generic;
using Game.Domain.Common;
using Game.Domain.Resources;
using Game.Domain.World;

namespace Game.Application.World
{
    public sealed class WorldMapOwnershipSynchronizer
    {
        private readonly AutonomousWorldState _world;
        private readonly Dictionary<GridCoordinate, string> _mineSiteLinks =
            new Dictionary<GridCoordinate, string>();
        private readonly Dictionary<GridCoordinate, RegionId> _castleLinks =
            new Dictionary<GridCoordinate, RegionId>();

        public WorldMapOwnershipSynchronizer(AutonomousWorldState world)
        {
            _world = world ?? throw new ArgumentNullException(nameof(world));
        }

        public RegionId GetRegion(
            GridMapLayout layout,
            GridCoordinate coordinate)
        {
            if (_world.World.Regions.Count == 0)
                return new RegionId("unknown");
            GridCoordinate normalized = layout.Normalize(coordinate);
            long flat = (long)normalized.Y * layout.Width + normalized.X;
            long cellCount = (long)layout.Width * layout.Height;
            int index = Math.Min(
                _world.World.Regions.Count - 1,
                (int)(flat * _world.World.Regions.Count /
                    Math.Max(1L, cellCount)));
            return _world.World.Regions[index].Id;
        }

        public bool ApplyMineCapture(
            GridMapLayout layout,
            RealtimeMapGameplayService gameplay,
            MapMineCaptureRecord capture)
        {
            RegionId regionId = GetRegion(layout, capture.Coordinate);
            MapMineControlState mine = gameplay.FindMine(capture.Coordinate);
            ResourceId preferred = new ResourceId(
                mine != null && mine.Kind == MineKind.Gold ? "gold" : "iron");
            ResourceExtractionSite linked = FindSite(regionId, preferred) ??
                FindSite(regionId, null);
            if (linked == null)
                return false;

            linked.AssignOwner(ToWorldFactionId(
                gameplay,
                capture.NewOwnerFactionId));
            _mineSiteLinks[capture.Coordinate] = linked.Id;
            return true;
        }

        public bool ApplyCastleCapture(
            GridMapLayout layout,
            RealtimeMapGameplayService gameplay,
            MapCastleCaptureRecord capture)
        {
            GeneratedRegionState region = _world.World.FindRegion(
                GetRegion(layout, capture.Coordinate));
            if (region == null)
                return false;

            string owner = ToWorldFactionId(
                gameplay,
                capture.NewOwnerFactionId);
            region.AssignOwner(owner);
            for (int i = 0; i < _world.World.Facilities.Count; i++)
            {
                WorldFacilityState facility = _world.World.Facilities[i];
                if (facility.RegionId.Equals(region.Id))
                    facility.AssignOwner(owner);
            }
            _castleLinks[capture.Coordinate] = region.Id;
            return true;
        }

        public bool SynchronizeLinkedOwnershipToMap(
            GridMapLayout layout,
            RealtimeMapGameplayService gameplay)
        {
            bool changed = false;
            foreach (var pair in _mineSiteLinks)
            {
                ResourceExtractionSite site =
                    _world.FindResourceSite(pair.Value);
                MapMineControlState mine = gameplay.FindMine(pair.Key);
                if (site == null || mine == null)
                    continue;
                string mapOwner = ToMapFactionId(
                    gameplay,
                    site.OwnerFactionId);
                if (string.Equals(
                    mine.OwnerFactionId,
                    mapOwner,
                    StringComparison.Ordinal))
                    continue;
                gameplay.TryRestoreAuthoritativeMineState(
                    pair.Key,
                    mapOwner,
                    string.Empty,
                    0,
                    out _);
                changed = true;
            }

            foreach (var pair in _castleLinks)
            {
                GeneratedRegionState region =
                    _world.World.FindRegion(pair.Value);
                MapCastleControlState castle = gameplay.FindCastle(pair.Key);
                if (region == null || castle == null || castle.IsCapital)
                    continue;
                string mapOwner = ToMapFactionId(
                    gameplay,
                    region.OwnerFactionId);
                if (string.Equals(
                    castle.OwnerFactionId,
                    mapOwner,
                    StringComparison.Ordinal))
                    continue;
                gameplay.TryRestoreAuthoritativeCastleState(
                    castle.Coordinate,
                    mapOwner,
                    string.Empty,
                    0,
                    castle.Role,
                    MapCastleConflictKind.None,
                    MapSiegeAction.None,
                    castle.OccupationPolicy,
                    castle.IsDestroyed,
                    castle.WallDurability,
                    castle.FoodSupply,
                    out _);
                changed = true;
            }
            return changed;
        }

        private ResourceExtractionSite FindSite(
            RegionId regionId,
            ResourceId? resourceId)
        {
            for (int i = 0; i < _world.ResourceSites.Count; i++)
            {
                ResourceExtractionSite site = _world.ResourceSites[i];
                if (site.RegionId.Equals(regionId) &&
                    (!resourceId.HasValue ||
                     site.ResourceId.Equals(resourceId.Value)))
                    return site;
            }
            return null;
        }

        private string ToWorldFactionId(
            RealtimeMapGameplayService gameplay,
            string mapFactionId)
        {
            IReadOnlyList<string> mapFactions = GetMapFactionIds(gameplay);
            for (int i = 0; i < mapFactions.Count; i++)
            {
                if (string.Equals(
                        mapFactions[i],
                        mapFactionId,
                        StringComparison.Ordinal) &&
                    i < _world.World.Factions.Count)
                    return _world.World.Factions[i].Id;
            }
            return string.Empty;
        }

        private string ToMapFactionId(
            RealtimeMapGameplayService gameplay,
            string worldFactionId)
        {
            IReadOnlyList<string> mapFactions = GetMapFactionIds(gameplay);
            for (int i = 0;
                 i < _world.World.Factions.Count && i < mapFactions.Count;
                 i++)
            {
                if (string.Equals(
                    _world.World.Factions[i].Id,
                    worldFactionId,
                    StringComparison.Ordinal))
                    return mapFactions[i];
            }
            return string.Empty;
        }

        private static IReadOnlyList<string> GetMapFactionIds(
            RealtimeMapGameplayService gameplay)
        {
            var ids = new List<string> { gameplay.PlayerFactionId };
            for (int i = 0; i < gameplay.Castles.Count; i++)
            {
                MapCastleControlState castle = gameplay.Castles[i];
                if (!castle.IsCapital || string.IsNullOrEmpty(
                        castle.OriginalOwnerFactionId) || ids.Contains(
                        castle.OriginalOwnerFactionId))
                    continue;
                ids.Add(castle.OriginalOwnerFactionId);
            }
            ids.Sort(1, ids.Count - 1, StringComparer.Ordinal);
            return ids;
        }
    }
}
