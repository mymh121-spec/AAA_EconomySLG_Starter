using System;
using System.Collections.Generic;
using Game.Domain.World;

namespace Game.Application.World
{
    public enum MapCastleRole
    {
        Unassigned,
        SupplyHub,
        IndustrialCity,
        MilitaryFortress,
        Port
    }

    public enum MapCastleConflictKind
    {
        None,
        Occupation,
        Siege
    }

    public static class MapCastleRoleNames
    {
        public static string GetKoreanName(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return "보급 거점";
                case MapCastleRole.IndustrialCity: return "산업 도시";
                case MapCastleRole.MilitaryFortress: return "군사 요새";
                case MapCastleRole.Port: return "항구";
                default: return "미지정 거점";
            }
        }
    }

    public sealed class MapCastleControlState
    {
        private readonly List<string> _garrisonUnitIds = new List<string>();

        public GridCoordinate Coordinate { get; }
        public string OwnerFactionId { get; internal set; }
        public string CapturingFactionId { get; internal set; }
        public int CaptureProgress { get; internal set; }
        public MapCastleRole Role { get; internal set; }
        public MapCastleConflictKind ConflictKind { get; internal set; }
        public IReadOnlyList<string> GarrisonUnitIds => _garrisonUnitIds;
        public int GarrisonUnitCount => _garrisonUnitIds.Count;
        public bool IsNeutral => string.IsNullOrEmpty(OwnerFactionId);
        public bool IsUnderSiege => ConflictKind == MapCastleConflictKind.Siege;

        public MapCastleControlState(GridCoordinate coordinate)
        {
            Coordinate = coordinate;
            OwnerFactionId = string.Empty;
            CapturingFactionId = string.Empty;
            Role = MapCastleRole.Unassigned;
            ConflictKind = MapCastleConflictKind.None;
        }

        internal bool SetGarrison(IReadOnlyList<string> unitIds)
        {
            if (unitIds == null)
            {
                if (_garrisonUnitIds.Count == 0)
                    return false;

                _garrisonUnitIds.Clear();
                return true;
            }

            int count = unitIds.Count;
            bool changed = count != _garrisonUnitIds.Count;
            if (!changed)
            {
                for (int i = 0; i < count; i++)
                {
                    if (!string.Equals(
                        _garrisonUnitIds[i],
                        unitIds[i],
                        StringComparison.Ordinal))
                    {
                        changed = true;
                        break;
                    }
                }
            }

            if (!changed)
                return false;

            _garrisonUnitIds.Clear();
            for (int i = 0; i < count; i++)
                _garrisonUnitIds.Add(unitIds[i]);
            return true;
        }
    }

    public readonly struct MapCastleCaptureRecord
    {
        public GridCoordinate Coordinate { get; }
        public string PreviousOwnerFactionId { get; }
        public string NewOwnerFactionId { get; }
        public bool WasSiege { get; }

        public MapCastleCaptureRecord(
            GridCoordinate coordinate,
            string previousOwnerFactionId,
            string newOwnerFactionId,
            bool wasSiege)
        {
            Coordinate = coordinate;
            PreviousOwnerFactionId = previousOwnerFactionId ?? string.Empty;
            NewOwnerFactionId = newOwnerFactionId ?? string.Empty;
            WasSiege = wasSiege;
        }
    }

    public readonly struct MapCastleRoleChangedRecord
    {
        public GridCoordinate Coordinate { get; }
        public string OwnerFactionId { get; }
        public MapCastleRole PreviousRole { get; }
        public MapCastleRole NewRole { get; }

        public MapCastleRoleChangedRecord(
            GridCoordinate coordinate,
            string ownerFactionId,
            MapCastleRole previousRole,
            MapCastleRole newRole)
        {
            Coordinate = coordinate;
            OwnerFactionId = ownerFactionId ?? string.Empty;
            PreviousRole = previousRole;
            NewRole = newRole;
        }
    }
}
