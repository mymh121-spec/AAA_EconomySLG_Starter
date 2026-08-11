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
            int fixedStepsToSiegeUndefendedCastle = 120)
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
        }
    }

    public sealed class MapUnitState
    {
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
        public int MaxStamina { get; }
        public int Stamina { get; private set; }
        public int StaminaRecoveryProgress { get; private set; }
        public bool IsMoving => _path.Count > 0;
        public IReadOnlyList<GridCoordinate> PlannedPath => _plannedPath;

        internal MapUnitState(
            string id,
            string ownerFactionId,
            GridCoordinate coordinate,
            UnitArchetype archetype,
            int maxStamina,
            UnitWeaponType weaponType,
            ArmorClass armorClass)
        {
            Id = id;
            OwnerFactionId = ownerFactionId;
            Coordinate = coordinate;
            Archetype = archetype;
            WeaponType = weaponType;
            ArmorClass = armorClass;
            MaxStamina = Math.Max(1, maxStamina);
            Stamina = MaxStamina;
        }

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
        }

        internal bool TryAdvanceOneTile()
        {
            if (_path.Count == 0)
                return false;

            Coordinate = _path.Dequeue();
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

        public MapMineProductionRecord(
            string ownerFactionId,
            int normalMineCount,
            int goldMineCount,
            decimal ironAmount,
            decimal cashAmount)
        {
            OwnerFactionId = ownerFactionId ?? string.Empty;
            NormalMineCount = Math.Max(0, normalMineCount);
            GoldMineCount = Math.Max(0, goldMineCount);
            IronAmount = Math.Max(0m, ironAmount);
            CashAmount = Math.Max(0m, cashAmount);
        }
    }

    public sealed class RealtimeMapGameplayService
    {
        private sealed class MineProductionAccumulator
        {
            public int NormalMineCount;
            public int GoldMineCount;
            public decimal IronAmount;
            public decimal CashAmount;
        }

        private static readonly GridCoordinate[] NeighborOffsets =
        {
            new GridCoordinate(1, 0),
            new GridCoordinate(-1, 0),
            new GridCoordinate(0, 1),
            new GridCoordinate(0, -1)
        };

        private readonly GridMapLayout _layout;
        private readonly MapGameplayTuning _tuning;
        private readonly Dictionary<string, GridCoordinate> _factionBases =
            new Dictionary<string, GridCoordinate>(StringComparer.Ordinal);
        private readonly List<string> _aiFactionIds = new List<string>();
        private readonly List<MapUnitState> _units = new List<MapUnitState>();
        private readonly List<MapMineControlState> _mines =
            new List<MapMineControlState>();
        private readonly List<MapCastleControlState> _castles =
            new List<MapCastleControlState>();
        private readonly Dictionary<GridCoordinate, MapRecruitmentSiteState>
            _recruitmentSites =
                new Dictionary<GridCoordinate, MapRecruitmentSiteState>();
        private int _unitSequence;
        private int _fixedStepSequence;
        private int _economicDaySequence;

        public string PlayerFactionId { get; }
        public IReadOnlyList<MapUnitState> Units => _units;
        public IReadOnlyList<MapMineControlState> Mines => _mines;
        public IReadOnlyList<MapCastleControlState> Castles => _castles;
        public int FixedStepsToCapture => _tuning.FixedStepsToCapture;
        public int FixedStepsToCaptureCastle =>
            _tuning.FixedStepsToCaptureCastle;
        public int FixedStepsToSiegeUndefendedCastle =>
            _tuning.FixedStepsToSiegeUndefendedCastle;
        public int CurrentEconomicDay => _economicDaySequence;

        public event Action StateChanged;
        public event Action<MapMineCaptureRecord> MineCaptured;
        public event Action<MapMineSpawnRecord> MineSpawned;
        public event Action<MapCastleCaptureRecord> CastleCaptured;
        public event Action<MapCastleRoleChangedRecord> CastleRoleChanged;

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
                armorClass);
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

            if (unit.Stamina < _tuning.MoveStaminaCost)
            {
                reason = $"유닛 체력이 부족합니다. 필요 {_tuning.MoveStaminaCost}, " +
                    $"현재 {unit.Stamina}/{unit.MaxStamina}";
                return false;
            }

            path = route;
            reason = string.Empty;
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
            if (!unit.TrySpendStamina(_tuning.MoveStaminaCost, out reason))
                return false;

            unit.SetPath(path);
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
            castle.Role = role;
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
            bool recruitmentChanged = AdvanceRecruitmentPools();
            spawnedMine = default;
            if (_economicDaySequence % _tuning.MineSpawnIntervalDays != 0 ||
                !TryFindDynamicMineCoordinate(
                    _economicDaySequence,
                    out GridCoordinate coordinate))
            {
                if (recruitmentChanged)
                    StateChanged?.Invoke();
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

        public IReadOnlyList<MapMineProductionRecord> CreateDailyProduction()
        {
            var counts = new Dictionary<string, MineProductionAccumulator>(
                StringComparer.Ordinal);
            var ownerOrder = new List<string>();
            bool depletedAnyMine = false;
            for (int i = 0; i < _mines.Count; i++)
            {
                MapMineControlState mine = _mines[i];
                if (string.IsNullOrWhiteSpace(mine.OwnerFactionId))
                    continue;

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
                    value.GoldMineCount++;
                    value.CashAmount +=
                        _tuning.GoldMineCashPerDay * mine.YieldMultiplier;
                }
                else
                {
                    value.NormalMineCount++;
                    value.IronAmount +=
                        _tuning.NormalMineIronPerDay * mine.YieldMultiplier;
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
                    value.CashAmount));
            }

            if (depletedAnyMine)
                StateChanged?.Invoke();
            return records;
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

                if (castleTarget != null && castleDistance <= mineDistance)
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
                int requiredSteps = Math.Max(
                    1,
                    (int)Math.Round(
                        _tuning.FixedStepsPerMove /
                        (double)unit.MobilityModifier,
                        MidpointRounding.AwayFromZero));
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
                changed |= _units[i].AdvanceStaminaRecovery(
                    _tuning.StaminaRecoveryIntervalSteps);
            }

            return changed;
        }

        private bool AdvanceCastleCaptures()
        {
            bool changed = false;
            for (int castleIndex = 0; castleIndex < _castles.Count; castleIndex++)
            {
                MapCastleControlState castle = _castles[castleIndex];
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
            return changed;
        }

        private void CompleteCastleCapture(
            MapCastleControlState castle,
            bool wasSiege)
        {
            string previousOwner = castle.OwnerFactionId;
            string newOwner = castle.CapturingFactionId;
            castle.OwnerFactionId = newOwner;
            castle.Role = string.Equals(
                newOwner,
                PlayerFactionId,
                StringComparison.Ordinal)
                ? MapCastleRole.Unassigned
                : SelectAiCastleRole(castle.Coordinate);
            ConfigureCastleRecruitmentSite(castle);
            ClearCastleConflict(castle);
            RefreshCastleGarrison(castle);
            CastleCaptured?.Invoke(new MapCastleCaptureRecord(
                castle.Coordinate,
                previousOwner,
                castle.OwnerFactionId,
                wasSiege));
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
