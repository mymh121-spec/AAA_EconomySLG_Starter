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

    public static class MapCastleRules
    {
        public const int HeadquartersGarrisonCapacity = 6;
        public const int HeadquartersRecruitmentCapacity = 6;
        public const int HeadquartersInitialRecruits = 4;
        public const int HeadquartersRecruitRecoveryDays = 1;
        public const int MineGuardCapacity = 1;

        public static int GetGarrisonCapacity(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return 3;
                case MapCastleRole.IndustrialCity: return 2;
                case MapCastleRole.MilitaryFortress: return 5;
                case MapCastleRole.Port: return 2;
                default: return 1;
            }
        }

        public static int GetRecruitmentCapacity(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return 2;
                case MapCastleRole.IndustrialCity: return 1;
                case MapCastleRole.MilitaryFortress: return 4;
                case MapCastleRole.Port: return 2;
                default: return 0;
            }
        }

        public static int GetRecruitRecoveryDays(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return 2;
                case MapCastleRole.IndustrialCity: return 3;
                case MapCastleRole.MilitaryFortress: return 1;
                case MapCastleRole.Port: return 2;
                default: return int.MaxValue;
            }
        }
    }

    public enum MapRecruitmentSiteKind
    {
        Headquarters,
        Castle
    }

    public sealed class MapRecruitmentSiteState
    {
        public GridCoordinate Coordinate { get; }
        public MapRecruitmentSiteKind Kind { get; }
        public string OwnerFactionId { get; private set; } = string.Empty;
        public int AvailableRecruits { get; private set; }
        public int RecruitmentCapacity { get; private set; }
        public int RecruitRecoveryDays { get; private set; }
        public int RecoveryProgressDays { get; private set; }

        public MapRecruitmentSiteState(
            GridCoordinate coordinate,
            MapRecruitmentSiteKind kind,
            string ownerFactionId,
            int recruitmentCapacity,
            int initialRecruits,
            int recruitRecoveryDays)
        {
            Coordinate = coordinate;
            Kind = kind;
            Configure(
                ownerFactionId,
                recruitmentCapacity,
                recruitRecoveryDays,
                initialRecruits);
        }

        internal void Configure(
            string ownerFactionId,
            int recruitmentCapacity,
            int recruitRecoveryDays,
            int initialRecruits = -1)
        {
            string normalizedOwner = ownerFactionId ?? string.Empty;
            bool ownershipChanged = !string.Equals(
                OwnerFactionId,
                normalizedOwner,
                StringComparison.Ordinal);
            int previousCapacity = RecruitmentCapacity;

            OwnerFactionId = normalizedOwner;
            RecruitmentCapacity = Math.Max(0, recruitmentCapacity);
            RecruitRecoveryDays = RecruitmentCapacity == 0
                ? int.MaxValue
                : Math.Max(1, recruitRecoveryDays);
            RecoveryProgressDays = 0;

            if (string.IsNullOrEmpty(OwnerFactionId) ||
                RecruitmentCapacity == 0)
            {
                AvailableRecruits = 0;
                return;
            }

            if (initialRecruits >= 0)
            {
                AvailableRecruits = Math.Min(
                    RecruitmentCapacity,
                    initialRecruits);
            }
            else if (ownershipChanged || previousCapacity == 0)
            {
                AvailableRecruits = Math.Min(1, RecruitmentCapacity);
            }
            else
            {
                AvailableRecruits = Math.Min(
                    AvailableRecruits,
                    RecruitmentCapacity);
            }
        }

        internal bool TryConsumeRecruit()
        {
            if (AvailableRecruits <= 0)
                return false;

            AvailableRecruits--;
            return true;
        }

        internal bool AdvanceDay()
        {
            if (RecruitmentCapacity <= 0 ||
                AvailableRecruits >= RecruitmentCapacity)
            {
                RecoveryProgressDays = 0;
                return false;
            }

            RecoveryProgressDays++;
            if (RecoveryProgressDays < RecruitRecoveryDays)
                return false;

            RecoveryProgressDays = 0;
            AvailableRecruits++;
            return true;
        }
    }

    public readonly struct MapRecruitmentSiteSnapshot
    {
        public GridCoordinate Coordinate { get; }
        public MapRecruitmentSiteKind Kind { get; }
        public string OwnerFactionId { get; }
        public int GarrisonUnitCount { get; }
        public int GarrisonCapacity { get; }
        public int AvailableRecruits { get; }
        public int RecruitmentCapacity { get; }
        public int RecruitRecoveryDays { get; }

        public MapRecruitmentSiteSnapshot(
            GridCoordinate coordinate,
            MapRecruitmentSiteKind kind,
            string ownerFactionId,
            int garrisonUnitCount,
            int garrisonCapacity,
            int availableRecruits,
            int recruitmentCapacity,
            int recruitRecoveryDays)
        {
            Coordinate = coordinate;
            Kind = kind;
            OwnerFactionId = ownerFactionId ?? string.Empty;
            GarrisonUnitCount = Math.Max(0, garrisonUnitCount);
            GarrisonCapacity = Math.Max(0, garrisonCapacity);
            AvailableRecruits = Math.Max(0, availableRecruits);
            RecruitmentCapacity = Math.Max(0, recruitmentCapacity);
            RecruitRecoveryDays = Math.Max(1, recruitRecoveryDays);
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
