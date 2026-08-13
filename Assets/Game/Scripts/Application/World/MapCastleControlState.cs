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

    public enum MapSupplyKind
    {
        Food,
        Equipment,
        Medicine
    }

    public enum MapSupplyDestinationKind
    {
        ForwardDepot,
        Unit
    }

    public enum MapSiegeAction
    {
        None,
        Assault,
        Encirclement,
        Blockade,
        Negotiation
    }

    public static class MapSiegeActionNames
    {
        public static string GetKoreanName(MapSiegeAction action)
        {
            switch (action)
            {
                case MapSiegeAction.Assault: return "강습";
                case MapSiegeAction.Encirclement: return "포위";
                case MapSiegeAction.Blockade: return "봉쇄";
                case MapSiegeAction.Negotiation: return "협상";
                default: return "미지정";
            }
        }
    }

    public enum MapOccupationPolicy
    {
        None,
        Loot,
        Preserve,
        Autonomy
    }

    public static class MapOccupationPolicyNames
    {
        public static string GetKoreanName(MapOccupationPolicy policy)
        {
            switch (policy)
            {
                case MapOccupationPolicy.Loot: return "약탈";
                case MapOccupationPolicy.Preserve: return "보존";
                case MapOccupationPolicy.Autonomy: return "자치";
                default: return "미결정";
            }
        }
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
        public const int CapitalMaxWallDurability = 2500;
        public const int CapitalMaxFoodSupply = 3000;
        public const decimal CapitalDefenseBonus = 0.35m;
        public const int MineGuardCapacity = 1;

        public static int GetMaxWallDurability(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return 1100;
                case MapCastleRole.IndustrialCity: return 1200;
                case MapCastleRole.MilitaryFortress: return 1800;
                case MapCastleRole.Port: return 1300;
                default: return 1000;
            }
        }

        public static int GetMaxFoodSupply(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return 1500;
                case MapCastleRole.IndustrialCity: return 1000;
                case MapCastleRole.MilitaryFortress: return 1200;
                case MapCastleRole.Port: return 1300;
                default: return 500;
            }
        }

        public static decimal GetRoleDefenseBonus(MapCastleRole role)
        {
            switch (role)
            {
                case MapCastleRole.SupplyHub: return 0.15m;
                case MapCastleRole.IndustrialCity: return 0.12m;
                case MapCastleRole.MilitaryFortress: return 0.40m;
                case MapCastleRole.Port: return 0.18m;
                default: return 0.10m;
            }
        }

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
        public bool IsCapital { get; }
        public bool IsDestroyed { get; private set; }
        public string OriginalOwnerFactionId { get; }
        public string OwnerFactionId { get; internal set; }
        public string CapturingFactionId { get; internal set; }
        public int CaptureProgress { get; internal set; }
        public MapCastleRole Role { get; internal set; }
        public MapCastleConflictKind ConflictKind { get; internal set; }
        public IReadOnlyList<string> GarrisonUnitIds => _garrisonUnitIds;
        public int GarrisonUnitCount => _garrisonUnitIds.Count;
        public bool IsNeutral => string.IsNullOrEmpty(OwnerFactionId);
        public bool IsUnderSiege => ConflictKind == MapCastleConflictKind.Siege;
        public MapSiegeAction SiegeAction { get; internal set; }
        public MapOccupationPolicy OccupationPolicy { get; private set; }
        public int PublicOrder { get; private set; }
        public decimal WarehouseIronAmount { get; private set; }
        public decimal WarehouseFoodAmount { get; private set; }
        public decimal WarehouseEquipmentAmount { get; private set; }
        public decimal WarehouseMedicineAmount { get; private set; }
        public int WallDurability { get; private set; }
        public int MaxWallDurability => IsCapital
            ? MapCastleRules.CapitalMaxWallDurability
            : MapCastleRules.GetMaxWallDurability(Role);
        public int FoodSupply { get; private set; }
        public int MaxFoodSupply => IsCapital
            ? MapCastleRules.CapitalMaxFoodSupply
            : MapCastleRules.GetMaxFoodSupply(Role);
        public decimal DefenseBonus
        {
            get
            {
                decimal wallRatio = MaxWallDurability <= 0
                    ? 0m
                    : WallDurability / (decimal)MaxWallDurability;
                decimal garrisonBonus = Math.Min(
                    0.25m,
                    GarrisonUnitCount * 0.05m);
                decimal policyBonus = OccupationPolicy ==
                    MapOccupationPolicy.Autonomy
                    ? 0.05m
                    : OccupationPolicy == MapOccupationPolicy.Loot
                        ? -0.05m
                        : 0m;
                return Math.Round(
                    (IsCapital
                        ? MapCastleRules.CapitalDefenseBonus
                        : MapCastleRules.GetRoleDefenseBonus(Role)) +
                    wallRatio * 0.20m +
                    garrisonBonus +
                    policyBonus,
                    3,
                    MidpointRounding.AwayFromZero);
            }
        }

        public MapCastleControlState(GridCoordinate coordinate)
            : this(coordinate, string.Empty, false)
        {
        }

        internal MapCastleControlState(
            GridCoordinate coordinate,
            string ownerFactionId,
            bool isCapital)
        {
            Coordinate = coordinate;
            IsCapital = isCapital;
            OriginalOwnerFactionId = ownerFactionId ?? string.Empty;
            OwnerFactionId = OriginalOwnerFactionId;
            CapturingFactionId = string.Empty;
            Role = MapCastleRole.Unassigned;
            ConflictKind = MapCastleConflictKind.None;
            SiegeAction = MapSiegeAction.None;
            OccupationPolicy = MapOccupationPolicy.None;
            PublicOrder = 50;
            WarehouseIronAmount = 0m;
            WarehouseFoodAmount = 0m;
            WarehouseEquipmentAmount = 0m;
            WarehouseMedicineAmount = 0m;
            WallDurability = MaxWallDurability;
            FoodSupply = MaxFoodSupply;
        }

        internal bool MarkCapitalDestroyed()
        {
            if (!IsCapital || IsDestroyed)
                return false;

            IsDestroyed = true;
            OwnerFactionId = string.Empty;
            WallDurability = 0;
            FoodSupply = 0;
            return true;
        }

        internal bool SetRole(MapCastleRole role)
        {
            if (Role == role)
                return false;

            decimal wallRatio = MaxWallDurability <= 0
                ? 0m
                : WallDurability / (decimal)MaxWallDurability;
            decimal foodRatio = MaxFoodSupply <= 0
                ? 0m
                : FoodSupply / (decimal)MaxFoodSupply;
            Role = role;
            WallDurability = Math.Clamp(
                (int)Math.Round(
                    MaxWallDurability * wallRatio,
                    MidpointRounding.AwayFromZero),
                0,
                MaxWallDurability);
            FoodSupply = Math.Clamp(
                (int)Math.Round(
                    MaxFoodSupply * foodRatio,
                    MidpointRounding.AwayFromZero),
                0,
                MaxFoodSupply);
            return true;
        }

        internal int ApplyWallDamage(int damage)
        {
            int applied = Math.Min(
                WallDurability,
                Math.Max(0, damage));
            WallDurability -= applied;
            return applied;
        }

        internal int ConsumeFood(int amount)
        {
            int consumed = Math.Min(FoodSupply, Math.Max(0, amount));
            FoodSupply -= consumed;
            return consumed;
        }

        internal int AddFood(int amount)
        {
            int added = Math.Min(
                MaxFoodSupply - FoodSupply,
                Math.Max(0, amount));
            FoodSupply += added;
            return added;
        }

        internal void StoreMineIron(decimal amount)
        {
            WarehouseIronAmount += Math.Max(0m, amount);
        }

        public decimal GetWarehouseSupply(MapSupplyKind kind)
        {
            switch (kind)
            {
                case MapSupplyKind.Food: return WarehouseFoodAmount;
                case MapSupplyKind.Equipment:
                    return WarehouseEquipmentAmount;
                case MapSupplyKind.Medicine:
                    return WarehouseMedicineAmount;
                default: return 0m;
            }
        }

        internal decimal StoreWarehouseSupply(
            MapSupplyKind kind,
            decimal amount)
        {
            decimal stored = Math.Max(0m, amount);
            switch (kind)
            {
                case MapSupplyKind.Food:
                    WarehouseFoodAmount += stored;
                    break;
                case MapSupplyKind.Equipment:
                    WarehouseEquipmentAmount += stored;
                    break;
                case MapSupplyKind.Medicine:
                    WarehouseMedicineAmount += stored;
                    break;
            }
            return stored;
        }

        internal decimal TakeWarehouseSupply(
            MapSupplyKind kind,
            decimal amount)
        {
            decimal available = GetWarehouseSupply(kind);
            decimal taken = Math.Min(available, Math.Max(0m, amount));
            switch (kind)
            {
                case MapSupplyKind.Food:
                    WarehouseFoodAmount -= taken;
                    break;
                case MapSupplyKind.Equipment:
                    WarehouseEquipmentAmount -= taken;
                    break;
                case MapSupplyKind.Medicine:
                    WarehouseMedicineAmount -= taken;
                    break;
            }
            return taken;
        }

        internal void PrepareForNewOwner()
        {
            OccupationPolicy = MapOccupationPolicy.None;
            PublicOrder = 50;
        }

        internal bool ApplyOccupationPolicy(MapOccupationPolicy policy)
        {
            if (IsCapital || policy == MapOccupationPolicy.None ||
                OccupationPolicy != MapOccupationPolicy.None)
            {
                return false;
            }

            OccupationPolicy = policy;
            switch (policy)
            {
                case MapOccupationPolicy.Loot:
                    ApplyWallDamage(MaxWallDurability / 5);
                    ConsumeFood(FoodSupply);
                    PublicOrder = 25;
                    break;
                case MapOccupationPolicy.Autonomy:
                    PublicOrder = 80;
                    break;
                default:
                    PublicOrder = 60;
                    break;
            }
            return true;
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

    public readonly struct MapCapitalDestroyedRecord
    {
        public GridCoordinate Coordinate { get; }
        public string DestroyedFactionId { get; }
        public string AttackingFactionId { get; }

        public MapCapitalDestroyedRecord(
            GridCoordinate coordinate,
            string destroyedFactionId,
            string attackingFactionId)
        {
            Coordinate = coordinate;
            DestroyedFactionId = destroyedFactionId ?? string.Empty;
            AttackingFactionId = attackingFactionId ?? string.Empty;
        }
    }

    public readonly struct MapSupplyTransportRecord
    {
        public GridCoordinate SourceCastleCoordinate { get; }
        public GridCoordinate DestinationCoordinate { get; }
        public MapSupplyDestinationKind DestinationKind { get; }
        public string DestinationUnitId { get; }
        public MapSupplyKind SupplyKind { get; }
        public decimal Amount { get; }
        public IReadOnlyList<GridCoordinate> Route { get; }
        public int Distance => Route.Count;

        public MapSupplyTransportRecord(
            GridCoordinate sourceCastleCoordinate,
            GridCoordinate destinationCoordinate,
            MapSupplyDestinationKind destinationKind,
            string destinationUnitId,
            MapSupplyKind supplyKind,
            decimal amount,
            IReadOnlyList<GridCoordinate> route)
        {
            SourceCastleCoordinate = sourceCastleCoordinate;
            DestinationCoordinate = destinationCoordinate;
            DestinationKind = destinationKind;
            DestinationUnitId = destinationUnitId ?? string.Empty;
            SupplyKind = supplyKind;
            Amount = Math.Max(0m, amount);
            if (route == null || route.Count == 0)
            {
                Route = Array.Empty<GridCoordinate>();
                return;
            }

            var copy = new GridCoordinate[route.Count];
            for (int i = 0; i < route.Count; i++)
                copy[i] = route[i];
            Route = copy;
        }
    }

    public readonly struct MapSiegeDayResult
    {
        public GridCoordinate Coordinate { get; }
        public MapSiegeAction Action { get; }
        public int EconomicDay { get; }
        public int WallDamage { get; }
        public int AttackerCasualties { get; }
        public int DefenderCasualties { get; }
        public int FoodConsumed { get; }
        public bool CastleCaptured { get; }
        public bool CapitalDestroyed { get; }
        public bool DefenderRetreated { get; }
        public int PursuitCasualties { get; }

        public MapSiegeDayResult(
            GridCoordinate coordinate,
            MapSiegeAction action,
            int economicDay,
            int wallDamage,
            int attackerCasualties,
            int defenderCasualties,
            int foodConsumed,
            bool castleCaptured,
            bool defenderRetreated = false,
            int pursuitCasualties = 0,
            bool capitalDestroyed = false)
        {
            Coordinate = coordinate;
            Action = action;
            EconomicDay = Math.Max(1, economicDay);
            WallDamage = Math.Max(0, wallDamage);
            AttackerCasualties = Math.Max(0, attackerCasualties);
            DefenderCasualties = Math.Max(0, defenderCasualties);
            FoodConsumed = Math.Max(0, foodConsumed);
            CastleCaptured = castleCaptured;
            CapitalDestroyed = capitalDestroyed;
            DefenderRetreated = defenderRetreated;
            PursuitCasualties = Math.Max(0, pursuitCasualties);
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
