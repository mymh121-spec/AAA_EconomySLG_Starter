using System;
using System.Collections.Generic;
using Game.Application.Turn;
using Game.Domain.Common;
using Game.Domain.Military;
using Game.Domain.World;

namespace Game.Application.World
{
    public sealed class MapGameplayTuning
    {
        public int FixedStepsPerMove { get; }
        public int FixedStepsToCapture { get; }
        public int FixedStepsToCaptureCastle { get; }
        public int FixedStepsToSiegeUndefendedCastle { get; }
        public int AiDecisionIntervalSteps { get; }
        public int MaxUnitsPerFaction { get; }
        public int MaxUnitStamina { get; }
        public int MoveStaminaCost { get; }
        public int StaminaRecoveryIntervalSteps { get; }
        public decimal NormalMineIronPerDay { get; }
        public decimal GoldMineCashPerDay { get; }
        public int MineSpawnIntervalDays { get; }
        public decimal MineDailyDepletionRate { get; }
        public decimal MinimumMineYieldMultiplier { get; }
        public int UnitScoutingRange { get; }
        public int InitialSoldiersPerUnit { get; }
        public decimal MovementFatiguePerTile { get; }
        public decimal DailyFatigueRecovery { get; }

        public MapGameplayTuning(
            int fixedStepsPerMove = 8,
            int fixedStepsToCapture = 30,
            int aiDecisionIntervalSteps = 20,
            int maxUnitsPerFaction = 12,
            int maxUnitStamina = 10,
            int moveStaminaCost = 1,
            int staminaRecoveryIntervalSteps = 150,
            decimal normalMineIronPerDay = 12m,
            decimal goldMineCashPerDay = 1500m,
            int mineSpawnIntervalDays = 5,
            decimal mineDailyDepletionRate = 0.03m,
            decimal minimumMineYieldMultiplier = 0.25m,
            int fixedStepsToCaptureCastle = 60,
            int fixedStepsToSiegeUndefendedCastle = 120,
            int unitScoutingRange = 5,
            int initialSoldiersPerUnit = 100,
            decimal movementFatiguePerTile = 2m,
            decimal dailyFatigueRecovery = 10m)
        {
            FixedStepsPerMove = Math.Max(1, fixedStepsPerMove);
            FixedStepsToCapture = Math.Max(1, fixedStepsToCapture);
            FixedStepsToCaptureCastle = Math.Max(
                1,
                fixedStepsToCaptureCastle);
            FixedStepsToSiegeUndefendedCastle = Math.Max(
                FixedStepsToCaptureCastle,
                fixedStepsToSiegeUndefendedCastle);
            AiDecisionIntervalSteps = Math.Max(1, aiDecisionIntervalSteps);
            MaxUnitsPerFaction = Math.Max(1, maxUnitsPerFaction);
            MaxUnitStamina = Math.Max(1, maxUnitStamina);
            MoveStaminaCost = Math.Max(1, moveStaminaCost);
            StaminaRecoveryIntervalSteps = Math.Max(
                1,
                staminaRecoveryIntervalSteps);
            NormalMineIronPerDay = Math.Max(0m, normalMineIronPerDay);
            GoldMineCashPerDay = Math.Max(0m, goldMineCashPerDay);
            MineSpawnIntervalDays = Math.Max(1, mineSpawnIntervalDays);
            MineDailyDepletionRate = Math.Clamp(
                mineDailyDepletionRate,
                0m,
                0.95m);
            MinimumMineYieldMultiplier = Math.Clamp(
                minimumMineYieldMultiplier,
                0.01m,
                1m);
            UnitScoutingRange = Math.Max(1, unitScoutingRange);
            InitialSoldiersPerUnit = Math.Max(1, initialSoldiersPerUnit);
            MovementFatiguePerTile = Math.Clamp(
                movementFatiguePerTile,
                0m,
                100m);
            DailyFatigueRecovery = Math.Clamp(
                dailyFatigueRecovery,
                0m,
                100m);
        }
    }

    public sealed class MapUnitState
    {
        private static readonly MilitaryBalanceCatalog CombatBalance =
            MilitaryBalanceCatalog.CreatePrototypeDefaults();
        private readonly Queue<GridCoordinate> _path =
            new Queue<GridCoordinate>();
        private readonly List<GridCoordinate> _plannedPath =
            new List<GridCoordinate>();

        public string Id { get; }
        public string OwnerFactionId { get; }
        public UnitArchetype Archetype { get; }
        public string ArchetypeDisplayName => GetArchetypeDisplayName(Archetype);
        public UnitWeaponType WeaponType { get; private set; }
        public ArmorClass ArmorClass { get; private set; }
        public string WeaponDisplayName =>
            UnitEquipmentCatalog.GetWeaponDisplayName(WeaponType);
        public string ArmorDisplayName =>
            UnitEquipmentCatalog.GetArmorDisplayName(ArmorClass);
        public decimal AttackModifier =>
            UnitEquipmentCatalog.GetAttackModifier(WeaponType);
        public decimal DefenseModifier =>
            UnitEquipmentCatalog.GetDefenseModifier(ArmorClass);
        public decimal MobilityModifier =>
            UnitEquipmentCatalog.GetMobilityModifier(Archetype, ArmorClass);
        public GridCoordinate Coordinate { get; internal set; }
        public GridCoordinate? Destination { get; internal set; }
        public int MovementProgress { get; internal set; }
        public int TotalMovementTileCount { get; private set; }
        public int CompletedMovementTileCount { get; private set; }
        public int RemainingMovementTileCount => _path.Count;
        public int MaxStamina { get; }
        public int Stamina { get; private set; }
        public int StaminaRecoveryProgress { get; private set; }
        public int Soldiers { get; private set; }
        public decimal Morale { get; private set; }
        public decimal Fatigue { get; private set; }
        public decimal FoodSupply { get; private set; }
        public decimal EquipmentSupply { get; private set; }
        public decimal MedicineSupply { get; private set; }
        public bool UsesSupplySystem { get; private set; }
        public MapSupplyMissionKind SupplyMissionKind { get; private set; }
        public GridCoordinate? SupplyMissionCoordinate { get; private set; }
        public decimal FoodSupplyCapacity => Soldiers * 0.21m;
        public decimal EquipmentSupplyCapacity => Soldiers * 0.028m;
        public decimal MedicineSupplyCapacity => Soldiers * 0.007m;
        public decimal FoodSupplyRatio => GetSupplyRatio(
            FoodSupply,
            FoodSupplyCapacity);
        public decimal EquipmentSupplyRatio => GetSupplyRatio(
            EquipmentSupply,
            EquipmentSupplyCapacity);
        public decimal MedicineSupplyRatio => GetSupplyRatio(
            MedicineSupply,
            MedicineSupplyCapacity);
        public decimal MovementSupplyModifier => UsesSupplySystem
            ? 0.50m + FoodSupplyRatio * 0.50m
            : 1m;
        public decimal AttackSupplyModifier => UsesSupplySystem
            ? 0.40m + EquipmentSupplyRatio * 0.60m
            : 1m;
        public decimal RecoverySupplyModifier => UsesSupplySystem
            ? 0.25m + MedicineSupplyRatio * 0.75m
            : 1m;
        public decimal SupplyRatio => Math.Min(
            FoodSupplyRatio,
            Math.Min(
                EquipmentSupplyRatio,
                MedicineSupplyRatio));
        public decimal AttackPower => CalculateCombatPower(true);
        public decimal DefensePower => CalculateCombatPower(false);
        public bool IsMoving => _path.Count > 0;
        public IReadOnlyList<GridCoordinate> PlannedPath => _plannedPath;

        internal MapUnitState(
            string id,
            string ownerFactionId,
            GridCoordinate coordinate,
            UnitArchetype archetype,
            int maxStamina,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            int initialSoldiers,
            decimal movementFatiguePerTile)
        {
            Id = id;
            OwnerFactionId = ownerFactionId;
            Coordinate = coordinate;
            Archetype = archetype;
            WeaponType = weaponType;
            ArmorClass = armorClass;
            MaxStamina = Math.Max(1, maxStamina);
            Stamina = MaxStamina;
            Soldiers = Math.Max(1, initialSoldiers);
            Morale = 100m;
            Fatigue = 0m;
            FoodSupply = 0m;
            EquipmentSupply = 0m;
            MedicineSupply = 0m;
            UsesSupplySystem = false;
            SupplyMissionKind = MapSupplyMissionKind.None;
            SupplyMissionCoordinate = null;
            MovementFatiguePerTile = Math.Clamp(
                movementFatiguePerTile,
                0m,
                100m);
        }

        private decimal MovementFatiguePerTile { get; }

        private decimal CalculateCombatPower(bool attack)
        {
            UnitArchetypeDefinition definition = CombatBalance.Get(Archetype);
            decimal basePower = attack
                ? definition.BaseAttack * AttackModifier
                : definition.BaseDefense * DefenseModifier;
            decimal moraleFactor = Math.Clamp(Morale / 100m, 0.25m, 1.25m);
            decimal fatigueFactor = Math.Clamp(
                1m - Fatigue / 200m,
                0.50m,
                1m);
            decimal supplyFactor = attack ? AttackSupplyModifier : 1m;
            return Math.Round(
                Soldiers * basePower * moraleFactor * fatigueFactor *
                supplyFactor,
                2,
                MidpointRounding.AwayFromZero);
        }

        internal int ApplyCasualties(int casualties)
        {
            int applied = Math.Min(Soldiers, Math.Max(0, casualties));
            Soldiers -= applied;
            if (applied > 0)
            {
                Morale = Math.Max(
                    0m,
                    Morale - applied * 100m /
                    Math.Max(1, Soldiers + applied));
            }
            return applied;
        }

        internal void AdjustMorale(decimal amount)
        {
            Morale = Math.Clamp(Morale + amount, 0m, 125m);
        }

        internal void AdjustFatigue(decimal amount)
        {
            Fatigue = Math.Clamp(Fatigue + amount, 0m, 100m);
        }

        internal bool RecoverFatigue(decimal amount)
        {
            decimal previous = Fatigue;
            AdjustFatigue(-Math.Max(0m, amount));
            return Fatigue != previous;
        }

        public decimal GetSupply(MapSupplyKind kind)
        {
            switch (kind)
            {
                case MapSupplyKind.Food: return FoodSupply;
                case MapSupplyKind.Equipment: return EquipmentSupply;
                case MapSupplyKind.Medicine: return MedicineSupply;
                default: return 0m;
            }
        }

        public decimal GetSupplyCapacity(MapSupplyKind kind)
        {
            switch (kind)
            {
                case MapSupplyKind.Food: return FoodSupplyCapacity;
                case MapSupplyKind.Equipment:
                    return EquipmentSupplyCapacity;
                case MapSupplyKind.Medicine:
                    return MedicineSupplyCapacity;
                default: return 0m;
            }
        }

        internal decimal StoreSupply(MapSupplyKind kind, decimal amount)
        {
            UsesSupplySystem = true;
            decimal stored = Math.Min(
                Math.Max(0m, amount),
                Math.Max(0m, GetSupplyCapacity(kind) - GetSupply(kind)));
            switch (kind)
            {
                case MapSupplyKind.Food:
                    FoodSupply += stored;
                    break;
                case MapSupplyKind.Equipment:
                    EquipmentSupply += stored;
                    break;
                case MapSupplyKind.Medicine:
                    MedicineSupply += stored;
                    break;
            }
            return stored;
        }

        internal void EnableSupplySystem()
        {
            UsesSupplySystem = true;
        }

        internal bool ConsumeDailySupplies()
        {
            if (Soldiers <= 0)
                return false;

            decimal foodNeed = Soldiers * 0.03m;
            decimal equipmentNeed = Soldiers * 0.004m;
            decimal medicineNeed = Soldiers * 0.001m;
            decimal foodConsumed = TakeSupply(
                MapSupplyKind.Food,
                foodNeed);
            TakeSupply(MapSupplyKind.Equipment, equipmentNeed);
            TakeSupply(MapSupplyKind.Medicine, medicineNeed);
            decimal foodFulfillment = foodNeed <= 0m
                ? 1m
                : Math.Clamp(foodConsumed / foodNeed, 0m, 1m);
            if (foodFulfillment < 1m)
            {
                AdjustMorale(-8m * (1m - foodFulfillment));
                AdjustFatigue(4m * (1m - foodFulfillment));
            }
            return foodNeed > 0m || equipmentNeed > 0m || medicineNeed > 0m;
        }

        private decimal TakeSupply(MapSupplyKind kind, decimal amount)
        {
            decimal taken = Math.Min(GetSupply(kind), Math.Max(0m, amount));
            switch (kind)
            {
                case MapSupplyKind.Food:
                    FoodSupply -= taken;
                    break;
                case MapSupplyKind.Equipment:
                    EquipmentSupply -= taken;
                    break;
                case MapSupplyKind.Medicine:
                    MedicineSupply -= taken;
                    break;
            }
            return taken;
        }

        internal void AssignSupplyMission(
            MapSupplyMissionKind kind,
            GridCoordinate coordinate)
        {
            SupplyMissionKind = kind;
            SupplyMissionCoordinate = kind == MapSupplyMissionKind.None
                ? (GridCoordinate?)null
                : coordinate;
        }

        private static decimal GetSupplyRatio(
            decimal amount,
            decimal capacity) =>
            capacity <= 0m
                ? 1m
                : Math.Clamp(amount / capacity, 0m, 1m);

        internal void ChangeEquipment(
            UnitWeaponType weaponType,
            ArmorClass armorClass)
        {
            WeaponType = weaponType;
            ArmorClass = armorClass;
        }

        public static string GetArchetypeDisplayName(UnitArchetype archetype)
        {
            switch (archetype)
            {
                case UnitArchetype.Swordsman: return "검병";
                case UnitArchetype.Spearman: return "창병";
                case UnitArchetype.Maceman: return "둔기병";
                case UnitArchetype.Archer: return "궁병";
                case UnitArchetype.Slinger: return "투석병";
                case UnitArchetype.Cavalry: return "기마병";
                default: return archetype.ToString();
            }
        }

        internal bool TrySpendStamina(int amount, out string reason)
        {
            int safeAmount = Math.Max(1, amount);
            if (Stamina < safeAmount)
            {
                reason = $"유닛 체력이 부족합니다. 필요 {safeAmount}, 현재 {Stamina}/{MaxStamina}";
                return false;
            }

            Stamina -= safeAmount;
            reason = string.Empty;
            return true;
        }

        internal bool AdvanceStaminaRecovery(int recoveryIntervalSteps)
        {
            if (Stamina >= MaxStamina)
            {
                StaminaRecoveryProgress = 0;
                return false;
            }

            StaminaRecoveryProgress++;
            if (StaminaRecoveryProgress < recoveryIntervalSteps)
                return false;

            StaminaRecoveryProgress = 0;
            Stamina = Math.Min(MaxStamina, Stamina + 1);
            return true;
        }

        internal void SetPath(IReadOnlyList<GridCoordinate> path)
        {
            _path.Clear();
            _plannedPath.Clear();
            if (path.Count > 0)
                _plannedPath.Add(Coordinate);

            for (int i = 0; i < path.Count; i++)
            {
                _path.Enqueue(path[i]);
                _plannedPath.Add(path[i]);
            }

            Destination = path.Count > 0
                ? path[path.Count - 1]
                : (GridCoordinate?)null;
            MovementProgress = 0;
            TotalMovementTileCount = path.Count;
            CompletedMovementTileCount = 0;
        }

        internal bool CancelMovement()
        {
            if (_path.Count == 0)
                return false;

            _path.Clear();
            _plannedPath.Clear();
            Destination = null;
            MovementProgress = 0;
            TotalMovementTileCount = 0;
            CompletedMovementTileCount = 0;
            return true;
        }

        internal void ForceRetreat(GridCoordinate destination)
        {
            CancelMovement();
            Coordinate = destination;
            Morale = Math.Max(35m, Morale);
            AdjustFatigue(15m);
        }

        internal bool AppendPath(IReadOnlyList<GridCoordinate> path)
        {
            if (_path.Count == 0 || path == null || path.Count == 0)
                return false;

            for (int i = 0; i < path.Count; i++)
            {
                _path.Enqueue(path[i]);
                _plannedPath.Add(path[i]);
            }

            Destination = path[path.Count - 1];
            TotalMovementTileCount += path.Count;
            return true;
        }

        internal bool TryAdvanceOneTile()
        {
            if (_path.Count == 0)
                return false;

            Coordinate = _path.Dequeue();
            AdjustFatigue(MovementFatiguePerTile);
            CompletedMovementTileCount++;
            if (_plannedPath.Count > 0)
                _plannedPath.RemoveAt(0);
            if (_path.Count == 0)
            {
                Destination = null;
                _plannedPath.Clear();
            }
            return true;
        }
    }

    public sealed class MapMineControlState
    {
        public GridCoordinate Coordinate { get; }
        public MineKind Kind { get; }
        public string OwnerFactionId { get; internal set; }
        public string CapturingFactionId { get; internal set; }
        public int CaptureProgress { get; internal set; }
        public int SpawnedEconomicDay { get; }
        public bool IsDynamic { get; }
        public decimal YieldMultiplier { get; private set; }
        public string GuardUnitId { get; internal set; }
        public bool HasGuard => !string.IsNullOrEmpty(GuardUnitId);

        public MapMineControlState(
            MinePlacement placement,
            int spawnedEconomicDay = 0,
            bool isDynamic = false)
        {
            Coordinate = placement.Coordinate;
            Kind = placement.Kind;
            OwnerFactionId = string.Empty;
            CapturingFactionId = string.Empty;
            SpawnedEconomicDay = Math.Max(0, spawnedEconomicDay);
            IsDynamic = isDynamic;
            YieldMultiplier = 1m;
            GuardUnitId = string.Empty;
        }

        internal void Deplete(
            decimal dailyDepletionRate,
            decimal minimumYieldMultiplier)
        {
            decimal minimum = Math.Clamp(
                minimumYieldMultiplier,
                0.01m,
                1m);
            decimal rate = Math.Clamp(dailyDepletionRate, 0m, 0.95m);
            YieldMultiplier = Math.Max(
                minimum,
                YieldMultiplier * (1m - rate));
        }
    }

    public readonly struct MapMineSpawnRecord
    {
        public GridCoordinate Coordinate { get; }
        public MineKind Kind { get; }
        public int EconomicDay { get; }

        public MapMineSpawnRecord(
            GridCoordinate coordinate,
            MineKind kind,
            int economicDay)
        {
            Coordinate = coordinate;
            Kind = kind;
            EconomicDay = Math.Max(1, economicDay);
        }
    }

    public readonly struct MapMineCaptureRecord
    {
        public GridCoordinate Coordinate { get; }
        public MineKind Kind { get; }
        public string PreviousOwnerFactionId { get; }
        public string NewOwnerFactionId { get; }

        public MapMineCaptureRecord(
            GridCoordinate coordinate,
            MineKind kind,
            string previousOwnerFactionId,
            string newOwnerFactionId)
        {
            Coordinate = coordinate;
            Kind = kind;
            PreviousOwnerFactionId = previousOwnerFactionId ?? string.Empty;
            NewOwnerFactionId = newOwnerFactionId ?? string.Empty;
        }
    }

    public readonly struct MapMineProductionRecord
    {
        public string OwnerFactionId { get; }
        public int NormalMineCount { get; }
        public int GoldMineCount { get; }
        public decimal IronAmount { get; }
        public decimal CashAmount { get; }
        public IReadOnlyList<MapMineTransportRecord> Transports { get; }

        public MapMineProductionRecord(
            string ownerFactionId,
            int normalMineCount,
            int goldMineCount,
            decimal ironAmount,
            decimal cashAmount)
            : this(
                ownerFactionId,
                normalMineCount,
                goldMineCount,
                ironAmount,
                cashAmount,
                Array.Empty<MapMineTransportRecord>())
        {
        }

        public MapMineProductionRecord(
            string ownerFactionId,
            int normalMineCount,
            int goldMineCount,
            decimal ironAmount,
            decimal cashAmount,
            IReadOnlyList<MapMineTransportRecord> transports)
        {
            OwnerFactionId = ownerFactionId ?? string.Empty;
            NormalMineCount = Math.Max(0, normalMineCount);
            GoldMineCount = Math.Max(0, goldMineCount);
            IronAmount = Math.Max(0m, ironAmount);
            CashAmount = Math.Max(0m, cashAmount);
            if (transports == null || transports.Count == 0)
            {
                Transports = Array.Empty<MapMineTransportRecord>();
                return;
            }

            var copy = new MapMineTransportRecord[transports.Count];
            for (int i = 0; i < transports.Count; i++)
                copy[i] = transports[i];
            Transports = copy;
        }
    }

    public readonly struct MapMineTransportRecord
    {
        public GridCoordinate MineCoordinate { get; }
        public GridCoordinate WarehouseCoordinate { get; }
        public MineKind MineKind { get; }
        public decimal IronAmount { get; }
        public decimal CashAmount { get; }
        public IReadOnlyList<GridCoordinate> Route { get; }
        public int Distance => Route.Count;

        public MapMineTransportRecord(
            GridCoordinate mineCoordinate,
            GridCoordinate warehouseCoordinate,
            MineKind mineKind,
            decimal ironAmount,
            decimal cashAmount,
            IReadOnlyList<GridCoordinate> route)
        {
            MineCoordinate = mineCoordinate;
            WarehouseCoordinate = warehouseCoordinate;
            MineKind = mineKind;
            IronAmount = Math.Max(0m, ironAmount);
            CashAmount = Math.Max(0m, cashAmount);
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

    public sealed class RealtimeMapGameplayService
    {
        private sealed class PendingSupplyTransport
        {
            public MapSupplyTransportRecord Record;
            public MapCastleControlState DestinationCastle;
            public string DestinationUnitId;
            public decimal RemainingAmount;
            public int EffectiveArrivalEconomicDay;
            public int LastInterdictionEconomicDay;
        }

        private sealed class MineProductionAccumulator
        {
            public int NormalMineCount;
            public int GoldMineCount;
            public decimal IronAmount;
            public decimal CashAmount;
            public readonly List<MapMineTransportRecord> Transports =
                new List<MapMineTransportRecord>();
        }

        private static readonly GridCoordinate[] NeighborOffsets =
        {
            new GridCoordinate(1, 0),
            new GridCoordinate(-1, 0),
            new GridCoordinate(0, 1),
            new GridCoordinate(0, -1)
        };
        private static readonly MapSupplyKind[] SupplyKinds =
        {
            MapSupplyKind.Food,
            MapSupplyKind.Equipment,
            MapSupplyKind.Medicine
        };

        private readonly GridMapLayout _layout;
        private readonly MapGameplayTuning _tuning;
        private readonly Dictionary<string, GridCoordinate> _factionBases =
            new Dictionary<string, GridCoordinate>(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _factionVehicleCounts =
            new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly HashSet<string> _supplyEnabledFactionIds =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<GridCoordinate> _roadTiles =
            new HashSet<GridCoordinate>();
        private readonly List<string> _aiFactionIds = new List<string>();
        private readonly List<MapUnitState> _units = new List<MapUnitState>();
        private readonly List<MapMineControlState> _mines =
            new List<MapMineControlState>();
        private readonly List<MapCastleControlState> _castles =
            new List<MapCastleControlState>();
        private readonly Dictionary<GridCoordinate, MapRecruitmentSiteState>
            _recruitmentSites =
                new Dictionary<GridCoordinate, MapRecruitmentSiteState>();
        private readonly List<MapSiegeDayResult> _lastSiegeDayResults =
            new List<MapSiegeDayResult>();
        private readonly List<MapSupplyTransportRecord>
            _lastSupplyTransportRecords =
                new List<MapSupplyTransportRecord>();
        private readonly List<PendingSupplyTransport>
            _pendingSupplyTransports = new List<PendingSupplyTransport>();
        private readonly List<MapSupplyInterdictionResult>
            _lastSupplyInterdictionResults =
                new List<MapSupplyInterdictionResult>();
        private int _unitSequence;
        private int _fixedStepSequence;
        private int _economicDaySequence;

        public string PlayerFactionId { get; }
        public IReadOnlyList<MapUnitState> Units => _units;
        public IReadOnlyList<MapMineControlState> Mines => _mines;
        public IReadOnlyList<MapCastleControlState> Castles => _castles;
        public int FixedStepsToCapture => _tuning.FixedStepsToCapture;
        public int FixedStepsPerMove => _tuning.FixedStepsPerMove;
        public int UnitScoutingRange => _tuning.UnitScoutingRange;
        public int FixedStepsToCaptureCastle =>
            _tuning.FixedStepsToCaptureCastle;
        public int FixedStepsToSiegeUndefendedCastle =>
            _tuning.FixedStepsToSiegeUndefendedCastle;
        public int CurrentEconomicDay => _economicDaySequence;
        public IReadOnlyList<MapSiegeDayResult> LastSiegeDayResults =>
            _lastSiegeDayResults;
        public IReadOnlyList<MapSupplyTransportRecord>
            LastSupplyTransportRecords => _lastSupplyTransportRecords;
        public int PendingSupplyTransportCount =>
            _pendingSupplyTransports.Count;
        public IReadOnlyList<MapSupplyInterdictionResult>
            LastSupplyInterdictionResults => _lastSupplyInterdictionResults;

        public event Action StateChanged;
        public event Action<MapMineCaptureRecord> MineCaptured;
        public event Action<MapMineSpawnRecord> MineSpawned;
        public event Action<MapCastleCaptureRecord> CastleCaptured;
        public event Action<MapCapitalDestroyedRecord> CapitalDestroyed;
        public event Action<MapCastleRoleChangedRecord> CastleRoleChanged;
        public event Action<MapSiegeDayResult> SiegeDayResolved;
        public event Action<MapSupplyInterdictionResult>
            SupplyInterdictionResolved;

        public RealtimeMapGameplayService(
            GridMapLayout layout,
            string playerFactionId,
            IReadOnlyList<string> aiFactionIds = null,
            MapGameplayTuning tuning = null)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            PlayerFactionId = RequireFactionId(playerFactionId);
            _tuning = tuning ?? new MapGameplayTuning();
            _factionBases.Add(PlayerFactionId, layout.PlayerStart);

            int opponentCount = Math.Min(
                layout.OpponentStarts.Count,
                aiFactionIds?.Count ?? 0);
            for (int i = 0; i < opponentCount; i++)
            {
                string factionId = RequireFactionId(aiFactionIds[i]);
                if (_factionBases.ContainsKey(factionId))
                    throw new ArgumentException("세력 ID가 중복되었습니다.", nameof(aiFactionIds));

                _factionBases.Add(factionId, layout.OpponentStarts[i]);
                _aiFactionIds.Add(factionId);
            }

            for (int i = 0; i < layout.Mines.Count; i++)
                _mines.Add(new MapMineControlState(layout.Mines[i]));
            for (int i = 0; i < layout.NeutralCastles.Count; i++)
            {
                GridCoordinate coordinate = layout.NeutralCastles[i];
                _castles.Add(new MapCastleControlState(coordinate));
                _recruitmentSites.Add(
                    coordinate,
                    new MapRecruitmentSiteState(
                        coordinate,
                        MapRecruitmentSiteKind.Castle,
                        string.Empty,
                        0,
                        0,
                        int.MaxValue));
            }

            foreach (KeyValuePair<string, GridCoordinate> entry in _factionBases)
            {
                _castles.Add(new MapCastleControlState(
                    entry.Value,
                    entry.Key,
                    true));
                _recruitmentSites.Add(
                    entry.Value,
                    new MapRecruitmentSiteState(
                        entry.Value,
                        MapRecruitmentSiteKind.Headquarters,
                        entry.Key,
                        MapCastleRules.HeadquartersRecruitmentCapacity,
                        MapCastleRules.HeadquartersInitialRecruits,
                        MapCastleRules.HeadquartersRecruitRecoveryDays));
            }

            BuildRoadNetwork();
        }

        public MapUnitState FindUnit(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return null;

            for (int i = 0; i < _units.Count; i++)
            {
                if (string.Equals(_units[i].Id, unitId, StringComparison.Ordinal))
                    return _units[i];
            }

            return null;
        }

        public MapUnitState FindOwnedUnitAt(
            string ownerFactionId,
            GridCoordinate coordinate)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        ownerFactionId,
                        StringComparison.Ordinal))
                {
                    return unit;
                }
            }

            return null;
        }

        public MapUnitState FindUnitAt(GridCoordinate coordinate)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (_units[i].Coordinate.Equals(coordinate))
                    return _units[i];
            }

            return null;
        }

        public bool CanViewMovementPath(
            string viewerFactionId,
            MapUnitState unit)
        {
            if (unit == null || string.IsNullOrWhiteSpace(viewerFactionId))
                return false;

            if (string.Equals(
                viewerFactionId,
                unit.OwnerFactionId,
                StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState scout = _units[i];
                if (!string.Equals(
                    scout.OwnerFactionId,
                    viewerFactionId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                if (_layout.ManhattanDistance(
                    scout.Coordinate,
                    unit.Coordinate) <= _tuning.UnitScoutingRange)
                {
                    return true;
                }
            }

            return false;
        }

        public MapMineControlState FindMine(GridCoordinate coordinate)
        {
            for (int i = 0; i < _mines.Count; i++)
            {
                if (_mines[i].Coordinate.Equals(coordinate))
                    return _mines[i];
            }

            return null;
        }

        public MapCastleControlState FindCastle(GridCoordinate coordinate)
        {
            for (int i = 0; i < _castles.Count; i++)
            {
                if (_castles[i].Coordinate.Equals(coordinate))
                    return _castles[i];
            }

            return null;
        }

        public MapCastleControlState FindCapital(string factionId)
        {
            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState castle = _castles[i];
                if (castle.IsCapital && string.Equals(
                        castle.OriginalOwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    return castle;
                }
            }

            return null;
        }

        public int GetCastleCaptureRequired(MapCastleControlState castle)
        {
            if (castle == null)
                return 0;

            return castle.ConflictKind == MapCastleConflictKind.Siege ||
                   !string.IsNullOrEmpty(castle.OwnerFactionId)
                ? _tuning.FixedStepsToSiegeUndefendedCastle
                : _tuning.FixedStepsToCaptureCastle;
        }

        public bool CanCreateUnit(string ownerFactionId, out string reason)
        {
            string normalizedOwnerFactionId = ownerFactionId ?? string.Empty;
            if (!_factionBases.TryGetValue(
                normalizedOwnerFactionId,
                out GridCoordinate headquarters))
            {
                reason = "유닛을 만들 수 있는 본사가 없습니다.";
                return false;
            }

            return CanCreateUnitAt(
                normalizedOwnerFactionId,
                headquarters,
                out reason);
        }

        public bool CanCreateUnitAt(
            string ownerFactionId,
            GridCoordinate origin,
            out string reason)
        {
            if (!_recruitmentSites.TryGetValue(
                origin,
                out MapRecruitmentSiteState recruitmentSite) ||
                !string.Equals(
                    recruitmentSite.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = FindMine(origin) != null
                    ? "광산에서는 징병할 수 없습니다. 소유한 본사나 성을 선택하세요."
                    : "이 위치에는 사용할 수 있는 징병소가 없습니다.";
                return false;
            }

            MapCastleControlState castle = FindCastle(origin);
            if (castle != null && castle.IsUnderSiege)
            {
                reason = "공성 중인 성에서는 징병할 수 없습니다.";
                return false;
            }

            if (recruitmentSite.RecruitmentCapacity <= 0)
            {
                reason = "점령 성의 역할을 먼저 지정해야 징병할 수 있습니다.";
                return false;
            }

            int ownedUnitCount = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (string.Equals(
                    _units[i].OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
                {
                    ownedUnitCount++;
                }
            }

            if (ownedUnitCount >= _tuning.MaxUnitsPerFaction)
            {
                reason = $"세력당 유닛은 최대 {_tuning.MaxUnitsPerFaction}개입니다.";
                return false;
            }

            int garrisonCount = CountOwnedUnitsAt(ownerFactionId, origin);
            int garrisonCapacity = GetFriendlyGarrisonCapacity(
                ownerFactionId,
                origin);
            if (garrisonCount >= garrisonCapacity)
            {
                reason = $"이 거점의 주둔 한도가 가득 찼습니다. " +
                    $"현재 {garrisonCount}/{garrisonCapacity}";
                return false;
            }

            if (recruitmentSite.AvailableRecruits <= 0)
            {
                reason = $"지역 징집 인력이 부족합니다. " +
                    $"{recruitmentSite.RecruitRecoveryDays}일마다 1명분이 회복됩니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryCreateUnit(
            string ownerFactionId,
            out MapUnitState unit,
            out string reason)
        {
            return TryCreateUnit(
                ownerFactionId,
                SelectNextArchetype(ownerFactionId),
                out unit,
                out reason);
        }

        public bool TryCreateUnit(
            string ownerFactionId,
            UnitArchetype archetype,
            out MapUnitState unit,
            out string reason)
        {
            return TryCreateUnit(
                ownerFactionId,
                archetype,
                UnitEquipmentCatalog.GetDefaultWeapon(archetype),
                ArmorClass.Light,
                out unit,
                out reason);
        }

        public bool TryCreateUnit(
            string ownerFactionId,
            UnitArchetype archetype,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            out MapUnitState unit,
            out string reason)
        {
            string normalizedOwnerFactionId = ownerFactionId ?? string.Empty;
            if (!_factionBases.TryGetValue(
                normalizedOwnerFactionId,
                out GridCoordinate headquarters))
            {
                unit = null;
                reason = "유닛을 만들 수 있는 본사가 없습니다.";
                return false;
            }

            return TryCreateUnitAt(
                normalizedOwnerFactionId,
                headquarters,
                archetype,
                weaponType,
                armorClass,
                out unit,
                out reason);
        }

        public bool TryCreateUnitAt(
            string ownerFactionId,
            GridCoordinate origin,
            UnitArchetype archetype,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            out MapUnitState unit,
            out string reason)
        {
            unit = null;
            if (!CanCreateUnitAt(ownerFactionId, origin, out reason))
                return false;

            MapRecruitmentSiteState recruitmentSite =
                _recruitmentSites[origin];
            if (!recruitmentSite.TryConsumeRecruit())
            {
                reason = "지역 징집 인력이 부족합니다.";
                return false;
            }

            unit = new MapUnitState(
                $"unit_{ownerFactionId}_{++_unitSequence}",
                ownerFactionId,
                origin,
                archetype,
                _tuning.MaxUnitStamina,
                weaponType,
                armorClass,
                _tuning.InitialSoldiersPerUnit,
                _tuning.MovementFatiguePerTile);
            if (_supplyEnabledFactionIds.Contains(ownerFactionId))
                unit.EnableSupplySystem();
            _units.Add(unit);
            MapCastleControlState castle = FindCastle(origin);
            if (castle != null)
                RefreshCastleGarrison(castle);
            StateChanged?.Invoke();
            return true;
        }

        public bool TryChangeEquipment(
            string ownerFactionId,
            string unitId,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (unit == null)
            {
                reason = "장비를 변경할 부대를 찾을 수 없습니다.";
                return false;
            }

            if (!string.Equals(
                unit.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "다른 세력의 부대 장비는 변경할 수 없습니다.";
                return false;
            }

            unit.ChangeEquipment(weaponType, armorClass);
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        private UnitArchetype SelectNextArchetype(string ownerFactionId)
        {
            int ownedUnitCount = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                if (string.Equals(
                    _units[i].OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
                {
                    ownedUnitCount++;
                }
            }

            UnitArchetype[] order =
            {
                UnitArchetype.Swordsman,
                UnitArchetype.Spearman,
                UnitArchetype.Archer,
                UnitArchetype.Cavalry,
                UnitArchetype.Maceman,
                UnitArchetype.Slinger
            };
            return order[ownedUnitCount % order.Length];
        }

        public bool CanIssueMove(
            string ownerFactionId,
            string unitId,
            GridCoordinate destination,
            out IReadOnlyList<GridCoordinate> path,
            out string reason)
        {
            path = Array.Empty<GridCoordinate>();
            MapUnitState unit = FindUnit(unitId);
            if (unit == null)
            {
                reason = "선택한 유닛을 찾을 수 없습니다.";
                return false;
            }

            if (!string.Equals(
                unit.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "다른 세력의 유닛에는 명령할 수 없습니다.";
                return false;
            }

            if (!_layout.TryNormalize(destination, out GridCoordinate normalized))
            {
                reason = "목적지가 지도 범위를 벗어났습니다.";
                return false;
            }

            if (!_layout.IsLand(normalized))
            {
                reason = "지상 유닛은 바다로 이동할 수 없습니다.";
                return false;
            }

            if (unit.Coordinate.Equals(normalized))
            {
                reason = "이미 해당 지역에 있습니다.";
                return false;
            }

            if (!CanEnterFriendlySite(
                ownerFactionId,
                unit.Id,
                normalized,
                out reason))
            {
                return false;
            }

            List<GridCoordinate> route = FindShortestLandPath(
                unit.Coordinate,
                normalized);
            if (route.Count == 0)
            {
                reason = "이동 가능한 육지 경로가 없습니다.";
                return false;
            }

            if (!unit.IsMoving && unit.Stamina < _tuning.MoveStaminaCost)
            {
                reason = $"유닛 체력이 부족합니다. 필요 {_tuning.MoveStaminaCost}, " +
                    $"현재 {unit.Stamina}/{unit.MaxStamina}";
                return false;
            }

            path = route;
            reason = string.Empty;
            return true;
        }

        public int GetRequiredMovementStepsPerTile(MapUnitState unit)
        {
            if (unit == null)
                return _tuning.FixedStepsPerMove;

            double rawSteps = _tuning.FixedStepsPerMove /
                (double)(unit.MobilityModifier *
                    GetMovementSupplyModifier(unit));
            return Math.Max(
                1,
                IsSupplyEnabled(unit)
                    ? (int)Math.Ceiling(rawSteps)
                    : (int)Math.Round(
                        rawSteps,
                        MidpointRounding.AwayFromZero));
        }

        public int GetRemainingMovementFixedSteps(MapUnitState unit)
        {
            if (unit == null || !unit.IsMoving)
                return 0;

            int stepsPerTile = GetRequiredMovementStepsPerTile(unit);
            return Math.Max(
                0,
                unit.RemainingMovementTileCount * stepsPerTile -
                unit.MovementProgress);
        }

        public bool TryGetMovementSegment(
            MapUnitState unit,
            out GridCoordinate from,
            out GridCoordinate to,
            out double progress)
        {
            from = default;
            to = default;
            progress = 0d;
            if (unit == null ||
                !unit.IsMoving ||
                unit.PlannedPath.Count < 2)
            {
                return false;
            }

            from = unit.PlannedPath[0];
            to = unit.PlannedPath[1];
            int requiredSteps = GetRequiredMovementStepsPerTile(unit);
            progress = Math.Clamp(
                unit.MovementProgress / (double)requiredSteps,
                0d,
                1d);
            return true;
        }

        public bool TryIssueMove(
            string ownerFactionId,
            string unitId,
            GridCoordinate destination,
            out string reason)
        {
            if (!CanIssueMove(
                ownerFactionId,
                unitId,
                destination,
                out IReadOnlyList<GridCoordinate> path,
                out reason))
            {
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            bool isReroute = unit.IsMoving;
            if (!isReroute &&
                !unit.TrySpendStamina(_tuning.MoveStaminaCost, out reason))
            {
                return false;
            }

            unit.SetPath(path);
            StateChanged?.Invoke();
            return true;
        }

        public bool CanAppendWaypoint(
            string ownerFactionId,
            string unitId,
            GridCoordinate destination,
            out IReadOnlyList<GridCoordinate> path,
            out string reason)
        {
            path = Array.Empty<GridCoordinate>();
            MapUnitState unit = FindUnit(unitId);
            if (unit == null)
            {
                reason = "선택한 유닛을 찾을 수 없습니다.";
                return false;
            }

            if (!string.Equals(
                unit.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "다른 세력의 유닛에는 명령할 수 없습니다.";
                return false;
            }

            if (!unit.IsMoving || !unit.Destination.HasValue)
            {
                reason = "경유지는 이동 중인 유닛에만 추가할 수 있습니다.";
                return false;
            }

            if (!_layout.TryNormalize(destination, out GridCoordinate normalized))
            {
                reason = "목적지가 지도 범위를 벗어났습니다.";
                return false;
            }

            if (!_layout.IsLand(normalized))
            {
                reason = "지상 유닛은 바다로 이동할 수 없습니다.";
                return false;
            }

            GridCoordinate pathOrigin = unit.Destination.Value;
            if (pathOrigin.Equals(normalized))
            {
                reason = "이미 마지막 경유지로 예약된 지역입니다.";
                return false;
            }

            if (!CanEnterFriendlySite(
                ownerFactionId,
                unit.Id,
                normalized,
                out reason))
            {
                return false;
            }

            List<GridCoordinate> route = FindShortestLandPath(
                pathOrigin,
                normalized);
            if (route.Count == 0)
            {
                reason = "경유지까지 이동 가능한 육지 경로가 없습니다.";
                return false;
            }

            path = route;
            reason = string.Empty;
            return true;
        }

        public bool TryAppendWaypoint(
            string ownerFactionId,
            string unitId,
            GridCoordinate destination,
            out string reason)
        {
            if (!CanAppendWaypoint(
                ownerFactionId,
                unitId,
                destination,
                out IReadOnlyList<GridCoordinate> path,
                out reason))
            {
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (!unit.AppendPath(path))
            {
                reason = "경유지를 이동 경로에 추가하지 못했습니다.";
                return false;
            }

            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool CanCancelMove(
            string ownerFactionId,
            string unitId,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (unit == null)
            {
                reason = "선택한 유닛을 찾을 수 없습니다.";
                return false;
            }

            if (!string.Equals(
                unit.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "다른 세력의 유닛에는 명령할 수 없습니다.";
                return false;
            }

            if (!unit.IsMoving)
            {
                reason = "선택한 유닛은 현재 이동 중이 아닙니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryCancelMove(
            string ownerFactionId,
            string unitId,
            out string reason)
        {
            if (!CanCancelMove(ownerFactionId, unitId, out reason))
                return false;

            MapUnitState unit = FindUnit(unitId);
            if (!unit.CancelMovement())
            {
                reason = "이동 명령을 취소하지 못했습니다.";
                return false;
            }

            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool CanIssueCastleOccupation(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            out string reason)
        {
            MapCastleControlState castle = FindCastle(coordinate);
            if (castle == null)
            {
                reason = "해당 위치에는 점령할 성이 없습니다.";
                return false;
            }

            if (castle.IsDestroyed)
            {
                reason = "이미 멸망한 수도입니다.";
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (unit == null)
            {
                reason = "먼저 점령에 사용할 부대를 선택하세요.";
                return false;
            }

            if (!string.Equals(
                unit.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "다른 세력의 부대에는 명령할 수 없습니다.";
                return false;
            }

            if (string.Equals(
                castle.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "이미 우리 세력이 소유한 성입니다.";
                return false;
            }

            if (!unit.Coordinate.Equals(castle.Coordinate))
            {
                return CanIssueMove(
                    ownerFactionId,
                    unitId,
                    castle.Coordinate,
                    out _,
                    out reason);
            }

            reason = string.Empty;
            return true;
        }

        public bool TryIssueCastleOccupation(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            out string reason)
        {
            if (!CanIssueCastleOccupation(
                ownerFactionId,
                unitId,
                coordinate,
                out reason))
            {
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (!unit.Coordinate.Equals(coordinate))
            {
                return TryIssueMove(
                    ownerFactionId,
                    unitId,
                    coordinate,
                    out reason);
            }

            MapCastleControlState castle = FindCastle(coordinate);
            BeginCastleConflict(castle, ownerFactionId);
            reason = castle.IsUnderSiege
                ? "적성 공성을 시작했습니다. 수비대가 있으면 전투 판정이 필요합니다."
                : "빈 성 점령을 시작했습니다. 부대가 성에 머무는 동안 진행됩니다.";
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySetCastleRole(
            string ownerFactionId,
            GridCoordinate coordinate,
            MapCastleRole role,
            out string reason)
        {
            if (!CanSetCastleRole(
                ownerFactionId,
                coordinate,
                role,
                out reason))
            {
                return false;
            }

            MapCastleControlState castle = FindCastle(coordinate);
            MapCastleRole previousRole = castle.Role;
            castle.SetRole(role);
            ConfigureCastleRecruitmentSite(castle);
            reason = string.Empty;
            CastleRoleChanged?.Invoke(new MapCastleRoleChangedRecord(
                castle.Coordinate,
                castle.OwnerFactionId,
                previousRole,
                castle.Role));
            StateChanged?.Invoke();
            return true;
        }

        public bool CanSetCastleRole(
            string ownerFactionId,
            GridCoordinate coordinate,
            MapCastleRole role,
            out string reason)
        {
            MapCastleControlState castle = FindCastle(coordinate);
            if (castle == null)
            {
                reason = "해당 위치에는 역할을 지정할 성이 없습니다.";
                return false;
            }

            if (castle.IsCapital)
            {
                reason = "수도는 거점 역할을 변경할 수 없습니다.";
                return false;
            }

            if (!string.Equals(
                castle.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "우리 세력이 소유한 성만 역할을 지정할 수 있습니다.";
                return false;
            }

            if (castle.IsUnderSiege)
            {
                reason = "공성 중인 성은 역할을 변경할 수 없습니다.";
                return false;
            }

            if (role == MapCastleRole.Unassigned)
            {
                reason = "보급 거점·산업 도시·군사 요새·항구 중 하나를 선택하세요.";
                return false;
            }

            if (role == MapCastleRole.Port &&
                !IsCoastalCastle(castle.Coordinate))
            {
                reason = "항구는 바다와 맞닿은 성에만 지정할 수 있습니다.";
                return false;
            }

            if (castle.Role == role)
            {
                reason = "이미 같은 역할로 운영 중입니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool CanSetSiegeAction(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            MapSiegeAction action,
            out string reason)
        {
            if (action == MapSiegeAction.None)
            {
                reason = "공성 행동을 선택하세요.";
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                unit.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "공성 행동을 지시할 아군 부대를 선택하세요.";
                return false;
            }

            MapCastleControlState castle = FindCastle(coordinate);
            if (castle == null || !castle.IsUnderSiege)
            {
                reason = "이 위치에서는 진행 중인 공성이 없습니다.";
                return false;
            }

            if (!unit.Coordinate.Equals(coordinate))
            {
                reason = "선택한 부대가 공성 중인 성에 도착해야 합니다.";
                return false;
            }

            if (!string.Equals(
                castle.CapturingFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "다른 세력이 진행하는 공성 행동은 변경할 수 없습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TrySetSiegeAction(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            MapSiegeAction action,
            out string reason)
        {
            if (!CanSetSiegeAction(
                ownerFactionId,
                unitId,
                coordinate,
                action,
                out reason))
            {
                return false;
            }

            MapCastleControlState castle = FindCastle(coordinate);
            if (castle.SiegeAction == action)
            {
                reason = "이미 선택한 공성 행동입니다.";
                return false;
            }

            castle.SiegeAction = action;
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool CanSetOccupationPolicy(
            string ownerFactionId,
            GridCoordinate coordinate,
            MapOccupationPolicy policy,
            out string reason)
        {
            MapCastleControlState castle = FindCastle(coordinate);
            if (castle != null && castle.IsCapital)
            {
                reason = "수도에는 점령 정책을 적용할 수 없습니다.";
                return false;
            }

            if (castle == null || !string.Equals(
                castle.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                reason = "우리 세력이 소유한 성만 점령 정책을 선택할 수 있습니다.";
                return false;
            }

            if (policy == MapOccupationPolicy.None)
            {
                reason = "약탈·보존·자치 중 하나를 선택하세요.";
                return false;
            }

            if (castle.OccupationPolicy != MapOccupationPolicy.None)
            {
                reason = "이 성의 점령 정책은 이미 확정되었습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TrySetOccupationPolicy(
            string ownerFactionId,
            GridCoordinate coordinate,
            MapOccupationPolicy policy,
            out string reason)
        {
            if (!CanSetOccupationPolicy(
                ownerFactionId,
                coordinate,
                policy,
                out reason))
            {
                return false;
            }

            MapCastleControlState castle = FindCastle(coordinate);
            if (!castle.ApplyOccupationPolicy(policy))
            {
                reason = "점령 정책을 적용하지 못했습니다.";
                return false;
            }

            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool TryGetRecruitmentSiteSnapshot(
            string ownerFactionId,
            GridCoordinate coordinate,
            out MapRecruitmentSiteSnapshot snapshot)
        {
            snapshot = default;
            if (!_recruitmentSites.TryGetValue(
                coordinate,
                out MapRecruitmentSiteState site) ||
                !string.Equals(
                    site.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            snapshot = new MapRecruitmentSiteSnapshot(
                coordinate,
                site.Kind,
                site.OwnerFactionId,
                CountOwnedUnitsAt(ownerFactionId, coordinate),
                GetFriendlyGarrisonCapacity(ownerFactionId, coordinate),
                site.AvailableRecruits,
                site.RecruitmentCapacity,
                site.RecruitRecoveryDays);
            return true;
        }

        public void AdvanceFixedSteps(int fixedStepCount)
        {
            int safeStepCount = Math.Max(0, fixedStepCount);
            bool anyChanged = false;
            for (int i = 0; i < safeStepCount; i++)
            {
                _fixedStepSequence++;
                bool changed = false;
                if (_fixedStepSequence % _tuning.AiDecisionIntervalSteps == 0)
                    changed |= RunAiDecisions();
                changed |= MoveUnitsOneFixedStep();
                changed |= AdvanceCastleCaptures();
                changed |= AdvanceMineCaptures();
                changed |= RefreshMineGuards();
                changed |= RecoverUnitStamina();
                anyChanged |= changed;
            }

            if (anyChanged)
                StateChanged?.Invoke();
        }

        public bool AdvanceEconomicDay(out MapMineSpawnRecord spawnedMine)
        {
            _economicDaySequence++;
            bool supplyChanged = AdvancePendingSupplyTransports();
            supplyChanged |= ConsumeDailyUnitSupplies();
            bool recruitmentChanged = AdvanceRecruitmentPools();
            bool fatigueChanged = RecoverDailyUnitFatigue();
            bool siegeChanged = ResolveDailySieges();
            spawnedMine = default;
            if (_economicDaySequence % _tuning.MineSpawnIntervalDays != 0 ||
                !TryFindDynamicMineCoordinate(
                    _economicDaySequence,
                    out GridCoordinate coordinate))
            {
                if (supplyChanged || recruitmentChanged || fatigueChanged ||
                    siegeChanged)
                {
                    StateChanged?.Invoke();
                }
                return false;
            }

            int spawnSequence =
                _economicDaySequence / _tuning.MineSpawnIntervalDays;
            MineKind kind = spawnSequence % 2 == 0
                ? MineKind.Gold
                : MineKind.Normal;
            var placement = new MinePlacement(coordinate, kind);
            _mines.Add(new MapMineControlState(
                placement,
                _economicDaySequence,
                true));
            spawnedMine = new MapMineSpawnRecord(
                coordinate,
                kind,
                _economicDaySequence);
            MineSpawned?.Invoke(spawnedMine);
            StateChanged?.Invoke();
            return true;
        }

        public void ConfigureFactionLogistics(
            string factionId,
            int vehicleCount)
        {
            if (string.IsNullOrWhiteSpace(factionId))
                return;
            _factionVehicleCounts[factionId] = Math.Max(0, vehicleCount);
            _supplyEnabledFactionIds.Add(factionId);
            for (int i = 0; i < _units.Count; i++)
            {
                if (string.Equals(
                    _units[i].OwnerFactionId,
                    factionId,
                    StringComparison.Ordinal))
                {
                    _units[i].EnableSupplySystem();
                }
            }
        }

        public bool TryProvisionUnitSupply(
            string ownerFactionId,
            string unitId,
            MapSupplyKind kind,
            decimal amount,
            out decimal storedAmount)
        {
            storedAmount = 0m;
            MapUnitState unit = FindUnit(unitId);
            if (unit == null || amount <= 0m || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            _supplyEnabledFactionIds.Add(ownerFactionId);
            storedAmount = unit.StoreSupply(kind, amount);
            if (storedAmount <= 0m)
                return false;
            StateChanged?.Invoke();
            return true;
        }

        public bool IsRoad(GridCoordinate coordinate) =>
            _roadTiles.Contains(_layout.Normalize(coordinate));

        public bool TryGetPendingSupplyRouteOwnerAt(
            GridCoordinate coordinate,
            out string ownerFactionId)
        {
            GridCoordinate normalized = _layout.Normalize(coordinate);
            for (int i = 0; i < _pendingSupplyTransports.Count; i++)
            {
                MapSupplyTransportRecord record =
                    _pendingSupplyTransports[i].Record;
                if (record.SourceCastleCoordinate.Equals(normalized) ||
                    record.DestinationCoordinate.Equals(normalized) ||
                    RouteContains(record.Route, normalized))
                {
                    ownerFactionId = record.OwnerFactionId;
                    return true;
                }
            }

            ownerFactionId = string.Empty;
            return false;
        }

        public bool TryAssignSupplyMission(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            MapSupplyMissionKind missionKind,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "임무를 수행할 아군 부대를 찾을 수 없습니다.";
                return false;
            }
            if (missionKind == MapSupplyMissionKind.None)
            {
                unit.AssignSupplyMission(missionKind, coordinate);
                reason = string.Empty;
                StateChanged?.Invoke();
                return true;
            }

            GridCoordinate normalized = _layout.Normalize(coordinate);
            if (!TryGetPendingSupplyRouteOwnerAt(
                    normalized,
                    out string transportOwner))
            {
                reason = "이 위치를 지나는 예약 수송대가 없습니다.";
                return false;
            }

            bool friendlyTransport = string.Equals(
                transportOwner,
                ownerFactionId,
                StringComparison.Ordinal);
            if (missionKind == MapSupplyMissionKind.Escort &&
                !friendlyTransport)
            {
                reason = "호위 임무는 아군 수송대에만 지정할 수 있습니다.";
                return false;
            }
            if ((missionKind == MapSupplyMissionKind.Raid ||
                 missionKind == MapSupplyMissionKind.Blockade) &&
                friendlyTransport)
            {
                reason = "습격과 봉쇄는 적 수송대에만 지정할 수 있습니다.";
                return false;
            }

            if (!unit.Coordinate.Equals(normalized) &&
                !TryIssueMove(
                    ownerFactionId,
                    unitId,
                    normalized,
                    out reason))
            {
                return false;
            }

            unit.AssignSupplyMission(missionKind, normalized);
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        private bool ResolveDailySieges()
        {
            _lastSiegeDayResults.Clear();
            bool changed = false;
            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState castle = _castles[i];
                if (!castle.IsUnderSiege ||
                    string.IsNullOrEmpty(castle.CapturingFactionId) ||
                    castle.SiegeAction == MapSiegeAction.None)
                {
                    continue;
                }

                string attackerFaction = castle.CapturingFactionId;
                MapSiegeAction resolvedAction = castle.SiegeAction;
                decimal attackerPower = SumCombatPowerAt(
                    castle.Coordinate,
                    attackerFaction,
                    true);
                decimal defenderPower = SumCombatPowerAt(
                    castle.Coordinate,
                    castle.OwnerFactionId,
                    false);
                int defenderSoldiers = SumSoldiersAt(
                    castle.Coordinate,
                    castle.OwnerFactionId);
                if (attackerPower <= 0m)
                    continue;

                decimal defenseFactor = 1m + castle.DefenseBonus;
                int wallDamage;
                int attackerCasualties;
                int defenderCasualties;
                int foodMultiplier;
                decimal attackerFatigue;
                decimal defenderMoraleLoss;
                switch (castle.SiegeAction)
                {
                    case MapSiegeAction.Assault:
                        wallDamage = Math.Max(
                            20,
                            RoundToInt(attackerPower * 0.35m / defenseFactor));
                        defenderCasualties = RoundToInt(
                            attackerPower / (8m * defenseFactor));
                        attackerCasualties = RoundToInt(
                            (defenderPower * defenseFactor +
                             castle.WallDurability /
                             (decimal)Math.Max(1, castle.MaxWallDurability) *
                             50m) / 12m);
                        foodMultiplier = 1;
                        attackerFatigue = 10m;
                        defenderMoraleLoss = 6m;
                        break;
                    case MapSiegeAction.Blockade:
                        wallDamage = Math.Max(
                            4,
                            RoundToInt(attackerPower * 0.06m / defenseFactor));
                        defenderCasualties = castle.FoodSupply == 0
                            ? Math.Max(1, defenderSoldiers / 20)
                            : 0;
                        attackerCasualties = 0;
                        foodMultiplier = 2;
                        attackerFatigue = 3m;
                        defenderMoraleLoss = 4m;
                        break;
                    case MapSiegeAction.Negotiation:
                        wallDamage = 0;
                        defenderCasualties = 0;
                        attackerCasualties = 0;
                        foodMultiplier = 1;
                        attackerFatigue = 1m;
                        defenderMoraleLoss = 2m;
                        break;
                    default:
                        wallDamage = Math.Max(
                            8,
                            RoundToInt(attackerPower * 0.12m / defenseFactor));
                        defenderCasualties = castle.FoodSupply == 0
                            ? Math.Max(1, defenderSoldiers / 25)
                            : 0;
                        attackerCasualties = 0;
                        foodMultiplier = 3;
                        attackerFatigue = 4m;
                        defenderMoraleLoss = 3m;
                        break;
                }

                int appliedWallDamage = castle.ApplyWallDamage(wallDamage);
                int foodNeed = Math.Max(
                    5,
                    (int)Math.Ceiling(defenderSoldiers * 0.05d));
                int foodConsumed = castle.ConsumeFood(
                    foodNeed * foodMultiplier);
                int appliedDefenderCasualties = ApplyCasualtiesAt(
                    castle.Coordinate,
                    castle.OwnerFactionId,
                    defenderCasualties);
                int appliedAttackerCasualties = ApplyCasualtiesAt(
                    castle.Coordinate,
                    attackerFaction,
                    attackerCasualties);
                AdjustFactionMoraleAt(
                    castle.Coordinate,
                    castle.OwnerFactionId,
                    -defenderMoraleLoss);
                AdjustFactionFatigueAt(
                    castle.Coordinate,
                    attackerFaction,
                    attackerFatigue);
                RefreshCastleGarrison(castle);

                bool captured = false;
                bool capitalDestroyed = false;
                bool defenderRetreated = false;
                int pursuitCasualties = 0;
                int remainingDefenders = SumSoldiersAt(
                    castle.Coordinate,
                    castle.OwnerFactionId);
                decimal defenderMorale = AverageMoraleAt(
                    castle.Coordinate,
                    castle.OwnerFactionId);
                bool sufferedDecisiveLoss = remainingDefenders > 0 &&
                    (appliedDefenderCasualties * 4 >=
                         Math.Max(1, defenderSoldiers) ||
                     defenderMorale <= 20m);
                if (sufferedDecisiveLoss)
                {
                    if (TryFindRetreatDestination(
                            castle.Coordinate,
                            castle.OwnerFactionId,
                            out GridCoordinate retreatDestination))
                    {
                        decimal pursuitRate = resolvedAction ==
                            MapSiegeAction.Assault
                                ? 0.10m
                                : resolvedAction == MapSiegeAction.Encirclement
                                    ? 0.06m
                                    : resolvedAction == MapSiegeAction.Blockade
                                        ? 0.03m
                                        : 0m;
                        pursuitCasualties = ApplyCasualtiesAt(
                            castle.Coordinate,
                            castle.OwnerFactionId,
                            RoundToInt(remainingDefenders * pursuitRate));
                        defenderRetreated = RetreatFactionUnitsAt(
                            castle.Coordinate,
                            castle.OwnerFactionId,
                            retreatDestination);
                        RefreshCastleGarrison(castle);
                        remainingDefenders = SumSoldiersAt(
                            castle.Coordinate,
                            castle.OwnerFactionId);
                    }
                }
                if ((castle.SiegeAction == MapSiegeAction.Negotiation &&
                     (castle.FoodSupply == 0 || defenderMorale <= 20m)) ||
                    (remainingDefenders == 0 && castle.WallDurability == 0))
                {
                    if (castle.IsCapital)
                    {
                        CompleteCapitalDestruction(castle);
                        capitalDestroyed = true;
                    }
                    else
                    {
                        CompleteCastleCapture(castle, wasSiege: true);
                        captured = true;
                    }
                }

                var result = new MapSiegeDayResult(
                    castle.Coordinate,
                    resolvedAction,
                    _economicDaySequence,
                    appliedWallDamage,
                    appliedAttackerCasualties,
                    appliedDefenderCasualties,
                    foodConsumed,
                    captured,
                    defenderRetreated,
                    pursuitCasualties,
                    capitalDestroyed);
                _lastSiegeDayResults.Add(result);
                SiegeDayResolved?.Invoke(result);
                changed = true;
            }

            return changed;
        }

        private decimal SumCombatPowerAt(
            GridCoordinate coordinate,
            string factionId,
            bool attack)
        {
            decimal total = 0m;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers > 0 &&
                    unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    total += attack ? unit.AttackPower : unit.DefensePower;
                }
            }
            return total;
        }

        private bool RetreatFactionUnitsAt(
            GridCoordinate coordinate,
            string factionId,
            GridCoordinate retreatDestination)
        {
            if (string.IsNullOrEmpty(factionId) ||
                retreatDestination.Equals(coordinate))
                return false;

            bool retreated = false;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers <= 0 ||
                    !unit.Coordinate.Equals(coordinate) ||
                    !string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                unit.ForceRetreat(retreatDestination);
                retreated = true;
            }

            return retreated;
        }

        private bool TryFindRetreatDestination(
            GridCoordinate origin,
            string factionId,
            out GridCoordinate destination)
        {
            bool found = false;
            destination = default;
            int bestDistance = int.MaxValue;
            if (_factionBases.TryGetValue(
                    factionId,
                    out GridCoordinate headquarters) &&
                !headquarters.Equals(origin))
            {
                destination = headquarters;
                bestDistance = _layout.ManhattanDistance(
                    origin,
                    headquarters);
                found = true;
            }

            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState castle = _castles[i];
                if (castle.IsCapital || castle.IsDestroyed ||
                    castle.IsUnderSiege ||
                    castle.Coordinate.Equals(origin) ||
                    !string.Equals(
                        castle.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int distance = _layout.ManhattanDistance(
                    origin,
                    castle.Coordinate);
                if (distance >= bestDistance)
                    continue;

                destination = castle.Coordinate;
                bestDistance = distance;
                found = true;
            }

            return found;
        }

        private int SumSoldiersAt(
            GridCoordinate coordinate,
            string factionId)
        {
            int total = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    total += unit.Soldiers;
                }
            }
            return total;
        }

        private int ApplyCasualtiesAt(
            GridCoordinate coordinate,
            string factionId,
            int casualties)
        {
            int remaining = Math.Max(0, casualties);
            int applied = 0;
            for (int i = 0; i < _units.Count && remaining > 0; i++)
            {
                MapUnitState unit = _units[i];
                if (!unit.Coordinate.Equals(coordinate) ||
                    !string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int unitLoss = unit.ApplyCasualties(remaining);
                applied += unitLoss;
                remaining -= unitLoss;
            }
            return applied;
        }

        private void AdjustFactionMoraleAt(
            GridCoordinate coordinate,
            string factionId,
            decimal amount)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers > 0 &&
                    unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    unit.AdjustMorale(amount);
                }
            }
        }

        private void AdjustFactionFatigueAt(
            GridCoordinate coordinate,
            string factionId,
            decimal amount)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers > 0 &&
                    unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    unit.AdjustFatigue(amount);
                }
            }
        }

        private decimal AverageMoraleAt(
            GridCoordinate coordinate,
            string factionId)
        {
            decimal total = 0m;
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers > 0 &&
                    unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    total += unit.Morale;
                    count++;
                }
            }
            return count == 0 ? 0m : total / count;
        }

        private static int RoundToInt(decimal value) =>
            Math.Max(
                0,
                (int)Math.Round(
                    value,
                    MidpointRounding.AwayFromZero));

        private bool RecoverDailyUnitFatigue()
        {
            bool changed = false;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                decimal recoveryModifier = IsSupplyEnabled(unit)
                    ? unit.RecoverySupplyModifier
                    : 1m;
                changed |= unit.RecoverFatigue(
                    _tuning.DailyFatigueRecovery * recoveryModifier);
            }
            return changed;
        }

        private bool ConsumeDailyUnitSupplies()
        {
            bool changed = false;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (IsSupplyEnabled(unit))
                    changed |= unit.ConsumeDailySupplies();
            }
            return changed;
        }

        private bool IsSupplyEnabled(MapUnitState unit) =>
            unit?.UsesSupplySystem == true;

        private decimal GetMovementSupplyModifier(MapUnitState unit) =>
            IsSupplyEnabled(unit) ? unit.MovementSupplyModifier : 1m;

        public IReadOnlyList<MapMineProductionRecord> CreateDailyProduction()
        {
            var counts = new Dictionary<string, MineProductionAccumulator>(
                StringComparer.Ordinal);
            var ownerOrder = new List<string>();
            bool depletedAnyMine = false;
            bool transportedAnyMine = false;
            for (int i = 0; i < _mines.Count; i++)
            {
                MapMineControlState mine = _mines[i];
                if (string.IsNullOrWhiteSpace(mine.OwnerFactionId))
                    continue;

                if (!TryFindNearestFriendlyCastleWarehouse(
                        mine.OwnerFactionId,
                        mine.Coordinate,
                        out MapCastleControlState warehouseCastle,
                        out IReadOnlyList<GridCoordinate> route))
                {
                    continue;
                }

                if (!counts.TryGetValue(
                    mine.OwnerFactionId,
                    out MineProductionAccumulator value))
                {
                    value = new MineProductionAccumulator();
                    counts.Add(mine.OwnerFactionId, value);
                    ownerOrder.Add(mine.OwnerFactionId);
                }

                if (mine.Kind == MineKind.Gold)
                {
                    decimal cashAmount =
                        _tuning.GoldMineCashPerDay * mine.YieldMultiplier;
                    value.GoldMineCount++;
                    value.CashAmount += cashAmount;
                    value.Transports.Add(new MapMineTransportRecord(
                        mine.Coordinate,
                        warehouseCastle.Coordinate,
                        mine.Kind,
                        0m,
                        cashAmount,
                        route));
                    transportedAnyMine = cashAmount > 0m || transportedAnyMine;
                }
                else
                {
                    decimal ironAmount =
                        _tuning.NormalMineIronPerDay * mine.YieldMultiplier;
                    value.NormalMineCount++;
                    value.IronAmount += ironAmount;
                    warehouseCastle.StoreMineIron(ironAmount);
                    value.Transports.Add(new MapMineTransportRecord(
                        mine.Coordinate,
                        warehouseCastle.Coordinate,
                        mine.Kind,
                        ironAmount,
                        0m,
                        route));
                    transportedAnyMine = ironAmount > 0m || transportedAnyMine;
                }

                decimal previousYield = mine.YieldMultiplier;
                mine.Deplete(
                    _tuning.MineDailyDepletionRate,
                    _tuning.MinimumMineYieldMultiplier);
                depletedAnyMine |= mine.YieldMultiplier != previousYield;
            }

            var records = new List<MapMineProductionRecord>(counts.Count);
            for (int i = 0; i < ownerOrder.Count; i++)
            {
                string ownerFactionId = ownerOrder[i];
                MineProductionAccumulator value = counts[ownerFactionId];
                records.Add(new MapMineProductionRecord(
                    ownerFactionId,
                    value.NormalMineCount,
                    value.GoldMineCount,
                    value.IronAmount,
                    value.CashAmount,
                    value.Transports));
            }

            if (depletedAnyMine || transportedAnyMine)
                StateChanged?.Invoke();
            return records;
        }

        public bool TryFindNearestFriendlyCastleWarehouse(
            string factionId,
            GridCoordinate origin,
            out MapCastleControlState warehouseCastle,
            out IReadOnlyList<GridCoordinate> route)
        {
            warehouseCastle = null;
            route = Array.Empty<GridCoordinate>();
            if (string.IsNullOrWhiteSpace(factionId))
                return false;

            List<GridCoordinate> bestRoute = null;
            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState candidate = _castles[i];
                if (candidate.IsDestroyed || !string.Equals(
                        candidate.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                List<GridCoordinate> candidateRoute = FindShortestLandPath(
                    origin,
                    candidate.Coordinate);
                if (candidateRoute.Count == 0)
                    continue;

                if (bestRoute != null &&
                    candidateRoute.Count >= bestRoute.Count)
                {
                    continue;
                }

                warehouseCastle = candidate;
                bestRoute = candidateRoute;
            }

            if (warehouseCastle == null)
                return false;

            route = bestRoute;
            return true;
        }

        public bool TryStockFactionCapitalWarehouse(
            string factionId,
            MapSupplyKind kind,
            decimal amount,
            out decimal storedAmount)
        {
            storedAmount = 0m;
            if (amount <= 0m)
                return false;

            MapCastleControlState capital = FindCapital(factionId);
            if (capital == null || capital.IsDestroyed)
                return false;

            storedAmount = capital.StoreWarehouseSupply(kind, amount);
            if (storedAmount <= 0m)
                return false;

            StateChanged?.Invoke();
            return true;
        }

        public IReadOnlyList<MapSupplyTransportRecord>
            CreateDailySupplyTransports()
        {
            _lastSupplyTransportRecords.Clear();
            TransferSuppliesToForwardDepots();
            TransferSuppliesToUnits();
            if (_lastSupplyTransportRecords.Count > 0)
                StateChanged?.Invoke();
            return _lastSupplyTransportRecords;
        }

        private void TransferSuppliesToForwardDepots()
        {
            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState depot = _castles[i];
                if (depot.IsDestroyed || depot.IsUnderSiege ||
                    depot.Role != MapCastleRole.SupplyHub ||
                    string.IsNullOrEmpty(depot.OwnerFactionId))
                {
                    continue;
                }

                for (int kindIndex = 0;
                     kindIndex < SupplyKinds.Length;
                     kindIndex++)
                {
                    MapSupplyKind kind = SupplyKinds[kindIndex];
                    decimal need = Math.Max(
                        0m,
                        GetForwardDepotTarget(kind) -
                        depot.GetWarehouseSupply(kind));
                    if (need <= 0m || !TryFindNearestStockedCastle(
                            depot.OwnerFactionId,
                            depot.Coordinate,
                            kind,
                            depot,
                            out MapCastleControlState source,
                            out IReadOnlyList<GridCoordinate> route))
                    {
                        continue;
                    }

                    decimal amount = source.TakeWarehouseSupply(kind, need);
                    ScheduleSupplyTransport(
                        source,
                        depot.Coordinate,
                        depot,
                        MapSupplyDestinationKind.ForwardDepot,
                        string.Empty,
                        kind,
                        amount,
                        route);
                }
            }
        }

        private void TransferSuppliesToUnits()
        {
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers <= 0)
                    continue;

                for (int kindIndex = 0;
                     kindIndex < SupplyKinds.Length;
                     kindIndex++)
                {
                    MapSupplyKind kind = SupplyKinds[kindIndex];
                    decimal need = Math.Max(
                        0m,
                        unit.GetSupplyCapacity(kind) - unit.GetSupply(kind));
                    if (need <= 0m || !TryFindNearestStockedCastle(
                            unit.OwnerFactionId,
                            unit.Coordinate,
                            kind,
                            null,
                            out MapCastleControlState source,
                            out IReadOnlyList<GridCoordinate> route))
                    {
                        continue;
                    }

                    decimal amount = source.TakeWarehouseSupply(kind, need);
                    ScheduleSupplyTransport(
                        source,
                        unit.Coordinate,
                        null,
                        MapSupplyDestinationKind.Unit,
                        unit.Id,
                        kind,
                        amount,
                        route);
                }
            }
        }

        private bool TryFindNearestStockedCastle(
            string factionId,
            GridCoordinate destination,
            MapSupplyKind kind,
            MapCastleControlState excludedCastle,
            out MapCastleControlState source,
            out IReadOnlyList<GridCoordinate> route)
        {
            source = null;
            route = Array.Empty<GridCoordinate>();
            List<GridCoordinate> bestRoute = null;
            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState candidate = _castles[i];
                if (ReferenceEquals(candidate, excludedCastle) ||
                    candidate.IsDestroyed || candidate.IsUnderSiege ||
                    candidate.GetWarehouseSupply(kind) <= 0m ||
                    !string.Equals(
                        candidate.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                List<GridCoordinate> candidateRoute;
                if (candidate.Coordinate.Equals(destination))
                {
                    candidateRoute = new List<GridCoordinate>();
                }
                else
                {
                    candidateRoute = FindShortestLandPath(
                        candidate.Coordinate,
                        destination);
                    if (candidateRoute.Count == 0)
                        continue;
                }

                if (bestRoute != null &&
                    candidateRoute.Count >= bestRoute.Count)
                {
                    continue;
                }

                source = candidate;
                bestRoute = candidateRoute;
            }

            if (source == null)
                return false;

            route = bestRoute;
            return true;
        }

        private void ScheduleSupplyTransport(
            MapCastleControlState source,
            GridCoordinate destination,
            MapCastleControlState destinationCastle,
            MapSupplyDestinationKind destinationKind,
            string destinationUnitId,
            MapSupplyKind supplyKind,
            decimal amount,
            IReadOnlyList<GridCoordinate> route)
        {
            if (amount <= 0m)
                return;

            int roadTileCount = 0;
            decimal terrainWeight = 0m;
            for (int i = 0; i < route.Count; i++)
            {
                GridCoordinate coordinate = route[i];
                bool isRoad = IsRoad(coordinate);
                if (isRoad)
                    roadTileCount++;
                terrainWeight += GetTerrainTravelWeight(
                    _layout.GetTerrain(coordinate),
                    isRoad);
            }

            int vehicleCount = _factionVehicleCounts.TryGetValue(
                source.OwnerFactionId,
                out int configuredVehicles)
                ? configuredVehicles
                : 0;
            decimal dailyRange = vehicleCount <= 0
                ? 0.5m
                : Math.Min(4m, 1m + (decimal)Math.Sqrt(vehicleCount));
            int travelDays = route.Count == 0
                ? 0
                : Math.Max(1, (int)Math.Ceiling(terrainWeight / dailyRange));
            decimal cost = Math.Round(
                amount * 0.20m + terrainWeight * 5m /
                Math.Max(1m, 1m + vehicleCount * 0.15m),
                2,
                MidpointRounding.AwayFromZero);
            var record = new MapSupplyTransportRecord(
                source.Coordinate,
                destination,
                source.OwnerFactionId,
                destinationKind,
                destinationUnitId,
                supplyKind,
                amount,
                route,
                roadTileCount,
                terrainWeight,
                _economicDaySequence,
                _economicDaySequence + travelDays,
                cost);
            _lastSupplyTransportRecords.Add(record);
            if (travelDays == 0)
            {
                DeliverSupplyTransport(
                    record,
                    destinationCastle,
                    destinationUnitId);
                return;
            }

            _pendingSupplyTransports.Add(new PendingSupplyTransport
            {
                Record = record,
                DestinationCastle = destinationCastle,
                DestinationUnitId = destinationUnitId,
                RemainingAmount = record.Amount,
                EffectiveArrivalEconomicDay = record.ArrivalEconomicDay,
                LastInterdictionEconomicDay = -1
            });
        }

        private bool AdvancePendingSupplyTransports()
        {
            _lastSupplyInterdictionResults.Clear();
            bool changed = false;
            for (int i = _pendingSupplyTransports.Count - 1; i >= 0; i--)
            {
                PendingSupplyTransport pending = _pendingSupplyTransports[i];
                changed |= ResolveSupplyInterdiction(pending);
                if (pending.RemainingAmount <= 0m)
                {
                    _pendingSupplyTransports.RemoveAt(i);
                    changed = true;
                    continue;
                }
                if (pending.EffectiveArrivalEconomicDay >
                    _economicDaySequence)
                    continue;

                DeliverSupplyTransport(
                    pending.Record,
                    pending.DestinationCastle,
                    pending.DestinationUnitId,
                    pending.RemainingAmount);
                _pendingSupplyTransports.RemoveAt(i);
                changed = true;
            }
            return changed;
        }

        private bool ResolveSupplyInterdiction(PendingSupplyTransport pending)
        {
            if (pending.LastInterdictionEconomicDay == _economicDaySequence ||
                pending.Record.Route.Count == 0)
            {
                return false;
            }

            GridCoordinate convoyCoordinate = GetTransportCoordinate(
                pending);
            decimal raidPower = SumSupplyMissionPowerAt(
                convoyCoordinate,
                pending.Record.OwnerFactionId,
                MapSupplyMissionKind.Raid,
                false);
            decimal blockadePower = SumSupplyMissionPowerAt(
                convoyCoordinate,
                pending.Record.OwnerFactionId,
                MapSupplyMissionKind.Blockade,
                false);
            decimal escortPower = SumSupplyMissionPowerAt(
                convoyCoordinate,
                pending.Record.OwnerFactionId,
                MapSupplyMissionKind.Escort,
                true);
            if (raidPower <= 0m && blockadePower <= 0m)
                return false;

            pending.LastInterdictionEconomicDay = _economicDaySequence;
            bool escorted = escortPower > 0m;
            decimal cargoBefore = pending.RemainingAmount;
            decimal cargoLost = 0m;
            if (raidPower > 0m)
            {
                decimal attackShare = raidPower /
                    Math.Max(1m, raidPower + escortPower);
                decimal lossRate = escorted
                    ? 0.10m + attackShare * 0.25m
                    : 0.45m;
                cargoLost = Math.Round(
                    cargoBefore * Math.Clamp(lossRate, 0m, 0.75m),
                    2,
                    MidpointRounding.AwayFromZero);
                pending.RemainingAmount = Math.Max(
                    0m,
                    pending.RemainingAmount - cargoLost);
            }

            bool blockaded = blockadePower > escortPower;
            int delayDays = blockaded ? 1 : 0;
            pending.EffectiveArrivalEconomicDay += delayDays;
            var result = new MapSupplyInterdictionResult(
                convoyCoordinate,
                pending.Record.OwnerFactionId,
                pending.Record.SupplyKind,
                cargoBefore,
                cargoLost,
                raidPower > 0m,
                blockaded,
                escorted,
                delayDays);
            _lastSupplyInterdictionResults.Add(result);
            SupplyInterdictionResolved?.Invoke(result);
            return true;
        }

        private GridCoordinate GetTransportCoordinate(
            PendingSupplyTransport pending)
        {
            int totalDays = Math.Max(1, pending.Record.TravelDays);
            int elapsedDays = Math.Clamp(
                _economicDaySequence - pending.Record.DispatchEconomicDay,
                0,
                totalDays);
            int index = Math.Clamp(
                (int)Math.Floor(
                    elapsedDays / (decimal)totalDays *
                    pending.Record.Route.Count),
                0,
                pending.Record.Route.Count - 1);
            return pending.Record.Route[index];
        }

        private decimal SumSupplyMissionPowerAt(
            GridCoordinate coordinate,
            string transportOwnerFactionId,
            MapSupplyMissionKind missionKind,
            bool friendly)
        {
            decimal power = 0m;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers <= 0 ||
                    unit.SupplyMissionKind != missionKind ||
                    !unit.SupplyMissionCoordinate.HasValue ||
                    !unit.SupplyMissionCoordinate.Value.Equals(coordinate) ||
                    !unit.Coordinate.Equals(coordinate))
                {
                    continue;
                }

                bool sameFaction = string.Equals(
                    unit.OwnerFactionId,
                    transportOwnerFactionId,
                    StringComparison.Ordinal);
                if (sameFaction == friendly)
                    power += unit.AttackPower;
            }
            return power;
        }

        private void DeliverSupplyTransport(
            MapSupplyTransportRecord record,
            MapCastleControlState destinationCastle,
            string destinationUnitId,
            decimal amount = -1m)
        {
            decimal deliveredAmount = amount < 0m
                ? record.Amount
                : Math.Min(record.Amount, Math.Max(0m, amount));
            if (destinationCastle != null &&
                !destinationCastle.IsDestroyed &&
                string.Equals(
                    destinationCastle.OwnerFactionId,
                    record.OwnerFactionId,
                    StringComparison.Ordinal))
            {
                destinationCastle.StoreWarehouseSupply(
                    record.SupplyKind,
                    deliveredAmount);
                return;
            }

            MapUnitState unit = FindUnit(destinationUnitId);
            if (unit != null && string.Equals(
                    unit.OwnerFactionId,
                    record.OwnerFactionId,
                    StringComparison.Ordinal))
            {
                unit.StoreSupply(record.SupplyKind, deliveredAmount);
            }
        }

        private static bool RouteContains(
            IReadOnlyList<GridCoordinate> route,
            GridCoordinate coordinate)
        {
            if (route == null)
                return false;
            for (int i = 0; i < route.Count; i++)
            {
                if (route[i].Equals(coordinate))
                    return true;
            }
            return false;
        }

        private static decimal GetTerrainTravelWeight(
            GridTerrainKind terrain,
            bool isRoad)
        {
            if (isRoad)
                return 0.60m;

            switch (terrain)
            {
                case GridTerrainKind.Forest: return 1.40m;
                case GridTerrainKind.Desert: return 1.30m;
                case GridTerrainKind.Hills: return 1.60m;
                case GridTerrainKind.Tundra: return 1.50m;
                default: return 1m;
            }
        }

        private static decimal GetForwardDepotTarget(MapSupplyKind kind)
        {
            switch (kind)
            {
                case MapSupplyKind.Food: return 250m;
                case MapSupplyKind.Equipment: return 50m;
                case MapSupplyKind.Medicine: return 25m;
                default: return 0m;
            }
        }

        private bool TryFindDynamicMineCoordinate(
            int economicDay,
            out GridCoordinate coordinate)
        {
            int cellCount = _layout.Width * _layout.Height;
            int startIndex = PositiveModulo(
                unchecked(_layout.Seed * 31 + economicDay * 997),
                cellCount);
            for (int offset = 0; offset < cellCount; offset++)
            {
                int index = (startIndex + offset) % cellCount;
                var candidate = new GridCoordinate(
                    index % _layout.Width,
                    index / _layout.Width);
                if (!_layout.IsLand(candidate) ||
                    FindMine(candidate) != null ||
                    IsFactionBase(candidate) ||
                    _layout.IsNeutralCastle(candidate))
                {
                    continue;
                }

                coordinate = candidate;
                return true;
            }

            coordinate = default;
            return false;
        }

        private bool IsFactionBase(GridCoordinate coordinate)
        {
            foreach (GridCoordinate factionBase in _factionBases.Values)
            {
                if (factionBase.Equals(coordinate))
                    return true;
            }

            return false;
        }

        private bool CanEnterFriendlySite(
            string ownerFactionId,
            string movingUnitId,
            GridCoordinate destination,
            out string reason)
        {
            MapCastleControlState castle = FindCastle(destination);
            if (castle != null && string.Equals(
                castle.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                int capacity = MapCastleRules.GetGarrisonCapacity(castle.Role);
                int count = CountOwnedUnitsAt(
                    ownerFactionId,
                    destination,
                    movingUnitId);
                if (count >= capacity)
                {
                    reason = $"성 주둔 한도가 가득 찼습니다. 현재 {count}/{capacity}";
                    return false;
                }
            }

            MapMineControlState mine = FindMine(destination);
            if (mine != null && string.Equals(
                mine.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                int count = CountOwnedUnitsAt(
                    ownerFactionId,
                    destination,
                    movingUnitId);
                if (count >= MapCastleRules.MineGuardCapacity)
                {
                    reason = "광산에는 공식 경비 부대 1개만 배치할 수 있습니다.";
                    return false;
                }
            }

            if (_recruitmentSites.TryGetValue(
                    destination,
                    out MapRecruitmentSiteState site) &&
                site.Kind == MapRecruitmentSiteKind.Headquarters &&
                string.Equals(
                    site.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                int count = CountOwnedUnitsAt(
                    ownerFactionId,
                    destination,
                    movingUnitId);
                if (count >= MapCastleRules.HeadquartersGarrisonCapacity)
                {
                    reason = $"본사 주둔 한도가 가득 찼습니다. " +
                        $"현재 {count}/{MapCastleRules.HeadquartersGarrisonCapacity}";
                    return false;
                }
            }

            reason = string.Empty;
            return true;
        }

        private int GetFriendlyGarrisonCapacity(
            string ownerFactionId,
            GridCoordinate coordinate)
        {
            if (_recruitmentSites.TryGetValue(
                    coordinate,
                    out MapRecruitmentSiteState site) &&
                site.Kind == MapRecruitmentSiteKind.Headquarters &&
                string.Equals(
                    site.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                return MapCastleRules.HeadquartersGarrisonCapacity;
            }

            MapCastleControlState castle = FindCastle(coordinate);
            if (castle != null && string.Equals(
                castle.OwnerFactionId,
                ownerFactionId,
                StringComparison.Ordinal))
            {
                return MapCastleRules.GetGarrisonCapacity(castle.Role);
            }

            return 0;
        }

        private int CountOwnedUnitsAt(
            string ownerFactionId,
            GridCoordinate coordinate,
            string excludedUnitId = "")
        {
            int count = 0;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Coordinate.Equals(coordinate) &&
                    string.Equals(
                        unit.OwnerFactionId,
                        ownerFactionId,
                        StringComparison.Ordinal) &&
                    !string.Equals(
                        unit.Id,
                        excludedUnitId,
                        StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int PositiveModulo(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private bool RunAiDecisions()
        {
            bool changed = false;
            for (int i = 0; i < _aiFactionIds.Count; i++)
            {
                string factionId = _aiFactionIds[i];
                MapUnitState unit = FindFirstOwnedUnit(factionId);
                if (unit == null)
                {
                    if (!TryCreateUnit(factionId, out unit, out _))
                        continue;
                    changed = true;
                }

                if (unit.IsMoving || IsStandingOnCapturableObjective(unit))
                    continue;

                MapCastleControlState castleTarget =
                    FindClosestTargetCastle(factionId, unit.Coordinate);
                MapMineControlState target = FindClosestTargetMine(
                    factionId,
                    unit.Coordinate);
                int castleDistance = castleTarget == null
                    ? int.MaxValue
                    : _layout.ManhattanDistance(
                        unit.Coordinate,
                        castleTarget.Coordinate);
                int mineDistance = target == null
                    ? int.MaxValue
                    : _layout.ManhattanDistance(
                        unit.Coordinate,
                        target.Coordinate);

                if (castleTarget != null && castleDistance < mineDistance)
                {
                    if (TryIssueCastleOccupation(
                        factionId,
                        unit.Id,
                        castleTarget.Coordinate,
                        out _))
                    {
                        changed = true;
                    }
                }
                else if (target != null && TryIssueMove(
                    factionId,
                    unit.Id,
                    target.Coordinate,
                    out _))
                {
                    changed = true;
                }
            }

            return changed;
        }

        private MapUnitState FindFirstOwnedUnit(string ownerFactionId)
        {
            for (int i = 0; i < _units.Count; i++)
            {
                if (string.Equals(
                    _units[i].OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
                {
                    return _units[i];
                }
            }

            return null;
        }

        private bool IsStandingOnCapturableObjective(MapUnitState unit)
        {
            MapMineControlState mine = FindMine(unit.Coordinate);
            if (mine != null && !string.Equals(
                    mine.OwnerFactionId,
                    unit.OwnerFactionId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            MapCastleControlState castle = FindCastle(unit.Coordinate);
            return castle != null && !string.Equals(
                    castle.OwnerFactionId,
                    unit.OwnerFactionId,
                    StringComparison.Ordinal);
        }

        private MapCastleControlState FindClosestTargetCastle(
            string factionId,
            GridCoordinate origin)
        {
            MapCastleControlState best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _castles.Count; i++)
            {
                MapCastleControlState castle = _castles[i];
                if (castle.IsDestroyed)
                    continue;
                if (string.Equals(
                    castle.OwnerFactionId,
                    factionId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                int distance = _layout.ManhattanDistance(
                    origin,
                    castle.Coordinate);
                if (distance < bestDistance)
                {
                    best = castle;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private MapMineControlState FindClosestTargetMine(
            string factionId,
            GridCoordinate origin)
        {
            MapMineControlState best = null;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < _mines.Count; i++)
            {
                MapMineControlState mine = _mines[i];
                if (string.Equals(
                    mine.OwnerFactionId,
                    factionId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                int distance = _layout.ManhattanDistance(
                    origin,
                    mine.Coordinate);
                if (distance < bestDistance)
                {
                    best = mine;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool MoveUnitsOneFixedStep()
        {
            bool changed = false;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (!unit.IsMoving)
                    continue;

                unit.MovementProgress++;
                int requiredSteps = GetRequiredMovementStepsPerTile(unit);
                if (unit.MovementProgress < requiredSteps)
                    continue;

                unit.MovementProgress = 0;
                changed |= unit.TryAdvanceOneTile();
            }

            return changed;
        }

        private bool RecoverUnitStamina()
        {
            bool changed = false;
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                decimal recoveryModifier = IsSupplyEnabled(unit)
                    ? unit.RecoverySupplyModifier
                    : 1m;
                int recoverySteps = Math.Max(
                    1,
                    (int)Math.Ceiling(
                        _tuning.StaminaRecoveryIntervalSteps /
                        (double)recoveryModifier));
                changed |= unit.AdvanceStaminaRecovery(recoverySteps);
            }

            return changed;
        }

        private bool AdvanceCastleCaptures()
        {
            bool changed = false;
            for (int castleIndex = 0; castleIndex < _castles.Count; castleIndex++)
            {
                MapCastleControlState castle = _castles[castleIndex];
                if (castle.IsDestroyed)
                    continue;
                changed |= RefreshCastleGarrison(castle);

                var occupyingFactions = new List<string>();
                for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
                {
                    MapUnitState unit = _units[unitIndex];
                    if (!unit.Coordinate.Equals(castle.Coordinate) ||
                        occupyingFactions.Contains(unit.OwnerFactionId))
                    {
                        continue;
                    }

                    occupyingFactions.Add(unit.OwnerFactionId);
                }

                if (occupyingFactions.Count == 0)
                {
                    changed |= ClearCastleConflict(castle);
                    continue;
                }

                if (castle.IsNeutral)
                {
                    if (occupyingFactions.Count != 1)
                    {
                        changed |= ClearCastleConflict(castle);
                        continue;
                    }

                    changed |= BeginCastleConflict(
                        castle,
                        occupyingFactions[0]);
                    castle.CaptureProgress++;
                    changed = true;
                    if (castle.CaptureProgress >=
                        _tuning.FixedStepsToCaptureCastle)
                    {
                        CompleteCastleCapture(castle, wasSiege: false);
                    }
                    continue;
                }

                bool ownerPresent = occupyingFactions.Contains(
                    castle.OwnerFactionId);
                var attackingFactions = new List<string>();
                for (int i = 0; i < occupyingFactions.Count; i++)
                {
                    if (!string.Equals(
                        occupyingFactions[i],
                        castle.OwnerFactionId,
                        StringComparison.Ordinal))
                    {
                        attackingFactions.Add(occupyingFactions[i]);
                    }
                }

                if (attackingFactions.Count == 0)
                {
                    changed |= ClearCastleConflict(castle);
                    continue;
                }

                if (attackingFactions.Count > 1)
                {
                    changed |= SetContestedSiege(castle);
                    continue;
                }

                changed |= BeginCastleConflict(castle, attackingFactions[0]);
                if (castle.IsCapital)
                {
                    if (castle.CaptureProgress != 0)
                    {
                        castle.CaptureProgress = 0;
                        changed = true;
                    }
                    continue;
                }

                if (ownerPresent || castle.GarrisonUnitCount > 0)
                {
                    if (castle.CaptureProgress != 0)
                    {
                        castle.CaptureProgress = 0;
                        changed = true;
                    }
                    continue;
                }

                castle.CaptureProgress++;
                changed = true;
                if (castle.CaptureProgress >=
                    _tuning.FixedStepsToSiegeUndefendedCastle)
                {
                    CompleteCastleCapture(castle, wasSiege: true);
                }
            }

            return changed;
        }

        private bool BeginCastleConflict(
            MapCastleControlState castle,
            string attackingFactionId)
        {
            MapCastleConflictKind kind = castle.IsNeutral
                ? MapCastleConflictKind.Occupation
                : MapCastleConflictKind.Siege;
            bool changed = castle.ConflictKind != kind ||
                !string.Equals(
                    castle.CapturingFactionId,
                    attackingFactionId,
                    StringComparison.Ordinal);
            if (!changed)
                return false;

            castle.ConflictKind = kind;
            castle.CapturingFactionId = attackingFactionId ?? string.Empty;
            castle.CaptureProgress = 0;
            castle.SiegeAction = kind == MapCastleConflictKind.Siege
                ? MapSiegeAction.Encirclement
                : MapSiegeAction.None;
            return true;
        }

        private static bool ClearCastleConflict(MapCastleControlState castle)
        {
            if (castle.ConflictKind == MapCastleConflictKind.None &&
                string.IsNullOrEmpty(castle.CapturingFactionId) &&
                castle.CaptureProgress == 0)
            {
                return false;
            }

            castle.ConflictKind = MapCastleConflictKind.None;
            castle.CapturingFactionId = string.Empty;
            castle.CaptureProgress = 0;
            castle.SiegeAction = MapSiegeAction.None;
            return true;
        }

        private static bool SetContestedSiege(MapCastleControlState castle)
        {
            bool changed = castle.ConflictKind != MapCastleConflictKind.Siege ||
                !string.IsNullOrEmpty(castle.CapturingFactionId) ||
                castle.CaptureProgress != 0;
            castle.ConflictKind = MapCastleConflictKind.Siege;
            castle.CapturingFactionId = string.Empty;
            castle.CaptureProgress = 0;
            castle.SiegeAction = MapSiegeAction.None;
            return changed;
        }

        private void CompleteCastleCapture(
            MapCastleControlState castle,
            bool wasSiege)
        {
            string previousOwner = castle.OwnerFactionId;
            string newOwner = castle.CapturingFactionId;
            castle.OwnerFactionId = newOwner;
            castle.PrepareForNewOwner();
            castle.SetRole(string.Equals(
                newOwner,
                PlayerFactionId,
                StringComparison.Ordinal)
                ? MapCastleRole.Unassigned
                : SelectAiCastleRole(castle.Coordinate));
            if (!string.Equals(
                    newOwner,
                    PlayerFactionId,
                    StringComparison.Ordinal))
            {
                castle.ApplyOccupationPolicy(MapOccupationPolicy.Preserve);
            }
            ConfigureCastleRecruitmentSite(castle);
            ClearCastleConflict(castle);
            RefreshCastleGarrison(castle);
            CastleCaptured?.Invoke(new MapCastleCaptureRecord(
                castle.Coordinate,
                previousOwner,
                castle.OwnerFactionId,
                wasSiege));
        }

        private void CompleteCapitalDestruction(MapCastleControlState capital)
        {
            string destroyedFaction = capital.OriginalOwnerFactionId;
            string attackingFaction = capital.CapturingFactionId;
            if (!capital.MarkCapitalDestroyed())
                return;

            ClearCastleConflict(capital);
            RefreshCastleGarrison(capital);
            if (_recruitmentSites.TryGetValue(
                    capital.Coordinate,
                    out MapRecruitmentSiteState site))
            {
                site.Configure(string.Empty, 0, int.MaxValue, 0);
            }
            CapitalDestroyed?.Invoke(new MapCapitalDestroyedRecord(
                capital.Coordinate,
                destroyedFaction,
                attackingFaction));
        }

        private void ConfigureCastleRecruitmentSite(
            MapCastleControlState castle)
        {
            if (castle == null ||
                !_recruitmentSites.TryGetValue(
                    castle.Coordinate,
                    out MapRecruitmentSiteState site))
            {
                return;
            }

            site.Configure(
                castle.OwnerFactionId,
                MapCastleRules.GetRecruitmentCapacity(castle.Role),
                MapCastleRules.GetRecruitRecoveryDays(castle.Role));
        }

        private bool AdvanceRecruitmentPools()
        {
            bool changed = false;
            foreach (MapRecruitmentSiteState site in _recruitmentSites.Values)
                changed |= site.AdvanceDay();
            return changed;
        }

        private bool RefreshCastleGarrison(MapCastleControlState castle)
        {
            var garrison = new List<string>();
            if (!string.IsNullOrEmpty(castle.OwnerFactionId))
            {
                for (int i = 0; i < _units.Count; i++)
                {
                    MapUnitState unit = _units[i];
                    if (unit.Coordinate.Equals(castle.Coordinate) &&
                        unit.Soldiers > 0 &&
                        string.Equals(
                            unit.OwnerFactionId,
                            castle.OwnerFactionId,
                            StringComparison.Ordinal))
                    {
                        garrison.Add(unit.Id);
                    }
                }
            }

            return castle.SetGarrison(garrison);
        }

        private MapCastleRole SelectAiCastleRole(GridCoordinate coordinate)
        {
            MapCastleRole[] roles = IsCoastalCastle(coordinate)
                ? new[]
                {
                    MapCastleRole.SupplyHub,
                    MapCastleRole.IndustrialCity,
                    MapCastleRole.MilitaryFortress,
                    MapCastleRole.Port
                }
                : new[]
                {
                    MapCastleRole.SupplyHub,
                    MapCastleRole.IndustrialCity,
                    MapCastleRole.MilitaryFortress
                };
            int index = PositiveModulo(
                coordinate.X * 17 + coordinate.Y * 31,
                roles.Length);
            return roles[index];
        }

        private bool IsCoastalCastle(GridCoordinate coordinate)
        {
            for (int i = 0; i < NeighborOffsets.Length; i++)
            {
                GridCoordinate offset = NeighborOffsets[i];
                try
                {
                    GridCoordinate neighbor = _layout.Move(
                        coordinate,
                        offset.X,
                        offset.Y);
                    if (!_layout.IsLand(neighbor))
                        return true;
                }
                catch (ArgumentOutOfRangeException)
                {
                    // 세로 경계 밖은 지도 외부이지 바다가 아니다.
                }
            }

            return false;
        }

        private bool AdvanceMineCaptures()
        {
            bool changed = false;
            for (int mineIndex = 0; mineIndex < _mines.Count; mineIndex++)
            {
                MapMineControlState mine = _mines[mineIndex];
                string occupyingFaction = string.Empty;
                bool contested = false;

                for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
                {
                    MapUnitState unit = _units[unitIndex];
                    if (!unit.Coordinate.Equals(mine.Coordinate))
                        continue;

                    if (string.IsNullOrEmpty(occupyingFaction))
                    {
                        occupyingFaction = unit.OwnerFactionId;
                    }
                    else if (!string.Equals(
                        occupyingFaction,
                        unit.OwnerFactionId,
                        StringComparison.Ordinal))
                    {
                        contested = true;
                        break;
                    }
                }

                if (contested || string.IsNullOrEmpty(occupyingFaction))
                {
                    if (!string.IsNullOrEmpty(mine.CapturingFactionId) ||
                        mine.CaptureProgress != 0)
                    {
                        mine.CapturingFactionId = string.Empty;
                        mine.CaptureProgress = 0;
                        changed = true;
                    }
                    continue;
                }

                if (string.Equals(
                    mine.OwnerFactionId,
                    occupyingFaction,
                    StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(mine.CapturingFactionId) ||
                        mine.CaptureProgress != 0)
                    {
                        mine.CapturingFactionId = string.Empty;
                        mine.CaptureProgress = 0;
                        changed = true;
                    }
                    continue;
                }

                if (!string.Equals(
                    mine.CapturingFactionId,
                    occupyingFaction,
                    StringComparison.Ordinal))
                {
                    mine.CapturingFactionId = occupyingFaction;
                    mine.CaptureProgress = 0;
                }

                mine.CaptureProgress++;
                changed = true;
                if (mine.CaptureProgress < _tuning.FixedStepsToCapture)
                    continue;

                string previousOwner = mine.OwnerFactionId;
                mine.OwnerFactionId = occupyingFaction;
                mine.CapturingFactionId = string.Empty;
                mine.CaptureProgress = 0;
                MineCaptured?.Invoke(new MapMineCaptureRecord(
                    mine.Coordinate,
                    mine.Kind,
                    previousOwner,
                    mine.OwnerFactionId));
            }

            return changed;
        }

        private bool RefreshMineGuards()
        {
            bool changed = false;
            for (int mineIndex = 0; mineIndex < _mines.Count; mineIndex++)
            {
                MapMineControlState mine = _mines[mineIndex];
                string guardUnitId = string.Empty;
                if (!string.IsNullOrEmpty(mine.OwnerFactionId))
                {
                    for (int unitIndex = 0;
                         unitIndex < _units.Count;
                         unitIndex++)
                    {
                        MapUnitState unit = _units[unitIndex];
                        if (unit.Coordinate.Equals(mine.Coordinate) &&
                            string.Equals(
                                unit.OwnerFactionId,
                                mine.OwnerFactionId,
                                StringComparison.Ordinal))
                        {
                            guardUnitId = unit.Id;
                            break;
                        }
                    }
                }

                if (string.Equals(
                    mine.GuardUnitId,
                    guardUnitId,
                    StringComparison.Ordinal))
                {
                    continue;
                }

                mine.GuardUnitId = guardUnitId;
                changed = true;
            }

            return changed;
        }

        private void BuildRoadNetwork()
        {
            foreach (KeyValuePair<string, GridCoordinate> entry in
                     _factionBases)
            {
                _roadTiles.Add(entry.Value);
                for (int i = 0; i < _castles.Count; i++)
                {
                    MapCastleControlState castle = _castles[i];
                    if (castle.IsCapital)
                        continue;

                    List<GridCoordinate> route = FindShortestLandPath(
                        entry.Value,
                        castle.Coordinate);
                    for (int routeIndex = 0;
                         routeIndex < route.Count;
                         routeIndex++)
                    {
                        _roadTiles.Add(route[routeIndex]);
                    }
                }
            }
        }

        private List<GridCoordinate> FindShortestLandPath(
            GridCoordinate origin,
            GridCoordinate destination)
        {
            var frontier = new Queue<GridCoordinate>();
            var visited = new HashSet<GridCoordinate>();
            var previous = new Dictionary<GridCoordinate, GridCoordinate>();
            frontier.Enqueue(origin);
            visited.Add(origin);

            while (frontier.Count > 0)
            {
                GridCoordinate current = frontier.Dequeue();
                for (int i = 0; i < NeighborOffsets.Length; i++)
                {
                    GridCoordinate offset = NeighborOffsets[i];
                    GridCoordinate neighbor;
                    try
                    {
                        neighbor = _layout.Move(current, offset.X, offset.Y);
                    }
                    catch (ArgumentOutOfRangeException)
                    {
                        continue;
                    }

                    if (!_layout.IsLand(neighbor) || !visited.Add(neighbor))
                        continue;

                    previous.Add(neighbor, current);
                    if (neighbor.Equals(destination))
                        return ReconstructPath(origin, destination, previous);
                    frontier.Enqueue(neighbor);
                }
            }

            return new List<GridCoordinate>();
        }

        private static List<GridCoordinate> ReconstructPath(
            GridCoordinate origin,
            GridCoordinate destination,
            IReadOnlyDictionary<GridCoordinate, GridCoordinate> previous)
        {
            var reversed = new List<GridCoordinate>();
            GridCoordinate current = destination;
            while (!current.Equals(origin))
            {
                reversed.Add(current);
                current = previous[current];
            }

            reversed.Reverse();
            return reversed;
        }

        private static string RequireFactionId(string factionId)
        {
            if (string.IsNullOrWhiteSpace(factionId))
                throw new ArgumentException("세력 ID가 필요합니다.", nameof(factionId));
            return factionId.Trim();
        }
    }

    public sealed class MapActionReservationTurnCommand : ITurnCommand
    {
        public CompanyId ActorId { get; }
        public string DisplayName { get; }
        public int ActionPointCost { get; }

        public MapActionReservationTurnCommand(
            CompanyId actorId,
            string displayName,
            int actionPointCost)
        {
            ActorId = actorId;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? "지도 행동"
                : displayName;
            ActionPointCost = Math.Max(1, actionPointCost);
        }

        public bool CanExecute(
            TurnCommandContext context,
            out string reason)
        {
            reason = string.Empty;
            return true;
        }

        public void Execute(TurnCommandContext context)
        {
            // 지도 행동은 명령 시점부터 고정 스텝으로 진행된다.
            // 이 명령은 일일 행동력 소비와 턴 보고서 기록만 담당한다.
        }
    }
}
