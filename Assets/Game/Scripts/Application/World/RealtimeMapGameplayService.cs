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

    public enum MapUnitFormationPreset
    {
        Custom,
        Balanced,
        Frontline,
        Ranged,
        Cavalry
    }

    public static class MapUnitFormationPresetNames
    {
        public static string GetKoreanName(MapUnitFormationPreset preset)
        {
            switch (preset)
            {
                case MapUnitFormationPreset.Frontline: return "전열 중심";
                case MapUnitFormationPreset.Ranged: return "원거리 중심";
                case MapUnitFormationPreset.Cavalry: return "기병 중심";
                case MapUnitFormationPreset.Balanced: return "균형 편성";
                default: return "사용자 편성";
            }
        }
    }

    public enum MapCommanderPersonality
    {
        Aggressive,
        Cautious,
        Opportunistic,
        Logistician
    }

    public static class MapCommanderPersonalityNames
    {
        public static string GetKoreanName(MapCommanderPersonality personality)
        {
            switch (personality)
            {
                case MapCommanderPersonality.Aggressive: return "공격적";
                case MapCommanderPersonality.Cautious: return "신중함";
                case MapCommanderPersonality.Opportunistic: return "기회주의";
                case MapCommanderPersonality.Logistician: return "병참 중시";
                default: return personality.ToString();
            }
        }
    }

    public static class MapCommanderBattleRules
    {
        public const int RollScale = 10000;
        public const int VictoryGenerationThreshold = 300;
        public const int DefeatDeathThreshold = 500;
        public const decimal VictoryGenerationChance = 0.03m;
        public const decimal DefeatDeathChance = 0.05m;

        public static bool ShouldGenerateCommanderAfterVictory(int roll) =>
            NormalizeRoll(roll) < VictoryGenerationThreshold;

        public static bool ShouldCommanderDieAfterDefeat(
            MapCommanderState commander,
            int roll) =>
            commander != null &&
            commander.IsAlive &&
            !commander.IsProtagonist &&
            NormalizeRoll(roll) < DefeatDeathThreshold;

        private static int NormalizeRoll(int roll)
        {
            int normalized = roll % RollScale;
            return normalized < 0 ? normalized + RollScale : normalized;
        }
    }

    public static class MapCommanderUpkeepRules
    {
        public const decimal BaseDailyCostPerSoldier = 2m;
        public const int BaseCommandCapacity = 100;
        public const int CommandCapacityPerSkill = 2;
        public const decimal OverloadCurveCoefficient = 0.15m;

        public static int GetCommandCapacity(MapCommanderState commander) =>
            commander == null
                ? 0
                : BaseCommandCapacity +
                  commander.Command * CommandCapacityPerSkill;

        public static decimal CalculateBaseUpkeep(int soldiers) =>
            Math.Max(0, soldiers) * BaseDailyCostPerSoldier;

        public static decimal CalculateConcentrationSurcharge(
            MapCommanderState commander,
            int soldiers)
        {
            if (commander == null || !commander.IsAlive)
                return 0m;

            int capacity = GetCommandCapacity(commander);
            decimal overloadRatio = capacity <= 0
                ? 0m
                : Math.Max(0, soldiers - capacity) /
                  (decimal)capacity;
            decimal surchargeRate = OverloadCurveCoefficient *
                overloadRatio * overloadRatio;
            return CalculateBaseUpkeep(soldiers) * surchargeRate;
        }

        public static MapMilitaryUpkeepRecord Calculate(
            MapUnitState unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            decimal baseUpkeep = CalculateBaseUpkeep(unit.Soldiers);
            decimal concentrationSurcharge =
                CalculateConcentrationSurcharge(
                    unit.Commander,
                    unit.Soldiers);
            return new MapMilitaryUpkeepRecord(
                unit.OwnerFactionId,
                unit.Id,
                unit.Commander?.Id ?? string.Empty,
                unit.Commander?.DisplayName ?? string.Empty,
                unit.Soldiers,
                GetCommandCapacity(unit.Commander),
                baseUpkeep,
                concentrationSurcharge);
        }
    }

    public readonly struct MapMilitaryUpkeepRecord
    {
        public string OwnerFactionId { get; }
        public string UnitId { get; }
        public string CommanderId { get; }
        public string CommanderDisplayName { get; }
        public int Soldiers { get; }
        public int CommandCapacity { get; }
        public decimal BaseUpkeep { get; }
        public decimal ConcentrationSurcharge { get; }
        public decimal TotalUpkeep => BaseUpkeep + ConcentrationSurcharge;
        public bool HasConcentrationSurcharge =>
            ConcentrationSurcharge > 0m;

        public MapMilitaryUpkeepRecord(
            string ownerFactionId,
            string unitId,
            string commanderId,
            string commanderDisplayName,
            int soldiers,
            int commandCapacity,
            decimal baseUpkeep,
            decimal concentrationSurcharge)
        {
            OwnerFactionId = ownerFactionId ?? string.Empty;
            UnitId = unitId ?? string.Empty;
            CommanderId = commanderId ?? string.Empty;
            CommanderDisplayName = commanderDisplayName ?? string.Empty;
            Soldiers = Math.Max(0, soldiers);
            CommandCapacity = Math.Max(0, commandCapacity);
            BaseUpkeep = Math.Max(0m, baseUpkeep);
            ConcentrationSurcharge = Math.Max(
                0m,
                concentrationSurcharge);
        }
    }

    public readonly struct MapMilitaryUpkeepSettlementReport
    {
        public decimal TotalAssessed { get; }
        public decimal TotalPaid { get; }
        public decimal TotalNewDebt { get; }
        public decimal PlayerAssessed { get; }
        public decimal PlayerConcentrationSurcharge { get; }

        public MapMilitaryUpkeepSettlementReport(
            decimal totalAssessed,
            decimal totalPaid,
            decimal totalNewDebt,
            decimal playerAssessed,
            decimal playerConcentrationSurcharge)
        {
            TotalAssessed = Math.Max(0m, totalAssessed);
            TotalPaid = Math.Max(0m, totalPaid);
            TotalNewDebt = Math.Max(0m, totalNewDebt);
            PlayerAssessed = Math.Max(0m, playerAssessed);
            PlayerConcentrationSurcharge = Math.Max(
                0m,
                playerConcentrationSurcharge);
        }
    }

    public sealed class MapCommanderState
    {
        public string Id { get; }
        public string DisplayName { get; }
        public int Command { get; }
        public int Tactics { get; }
        public int Logistics { get; }
        public MapCommanderPersonality Personality { get; }
        public int Loyalty { get; private set; }
        public decimal HiringCost { get; }
        public string EmployerFactionId { get; private set; }
        public string AssignedUnitId { get; private set; }
        public bool IsProtagonist { get; }
        public bool IsAlive { get; private set; }
        public bool IsAvailable =>
            IsAlive && string.IsNullOrEmpty(EmployerFactionId);
        public decimal AttackModifier => GetModifier(
            Command * 0.55m + Tactics * 0.45m,
            Personality == MapCommanderPersonality.Aggressive ? 0.05m :
            Personality == MapCommanderPersonality.Opportunistic ? 0.03m :
            Personality == MapCommanderPersonality.Cautious ? -0.02m : 0m);
        public decimal DefenseModifier => GetModifier(
            Command * 0.70m + Tactics * 0.30m,
            Personality == MapCommanderPersonality.Cautious ? 0.05m :
            Personality == MapCommanderPersonality.Aggressive ? -0.03m :
            Personality == MapCommanderPersonality.Logistician ? 0.01m : 0m);
        public decimal MobilityModifier => GetModifier(
            Logistics,
            Personality == MapCommanderPersonality.Logistician ? 0.06m :
            Personality == MapCommanderPersonality.Opportunistic ? 0.04m : 0m);

        public MapCommanderState(
            string id,
            string displayName,
            int command,
            int tactics,
            int logistics,
            MapCommanderPersonality personality,
            int loyalty,
            decimal hiringCost,
            bool isProtagonist = false)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("지휘관 ID가 필요합니다.", nameof(id))
                : id;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? Id
                : displayName;
            Command = Math.Clamp(command, 0, 100);
            Tactics = Math.Clamp(tactics, 0, 100);
            Logistics = Math.Clamp(logistics, 0, 100);
            Personality = personality;
            Loyalty = Math.Clamp(loyalty, 0, 100);
            HiringCost = Math.Max(0m, hiringCost);
            IsProtagonist = isProtagonist;
            IsAlive = true;
            EmployerFactionId = string.Empty;
            AssignedUnitId = string.Empty;
        }

        internal void Hire(string factionId, string unitId)
        {
            EmployerFactionId = factionId ?? string.Empty;
            AssignedUnitId = unitId ?? string.Empty;
        }

        internal void AdjustLoyalty(int amount)
        {
            Loyalty = Math.Clamp(Loyalty + amount, 0, 100);
        }

        internal bool MarkKilled()
        {
            if (!IsAlive || IsProtagonist)
                return false;

            IsAlive = false;
            EmployerFactionId = string.Empty;
            AssignedUnitId = string.Empty;
            return true;
        }

        private decimal GetModifier(decimal skill, decimal personalityBonus)
        {
            decimal loyaltyScale = 0.50m + Loyalty / 200m;
            decimal skillBonus = (skill - 50m) / 500m;
            return Math.Clamp(
                1m + (skillBonus + personalityBonus) * loyaltyScale,
                0.80m,
                1.20m);
        }
    }

    public readonly struct MapCommanderGeneratedRecord
    {
        public MapCommanderState Commander { get; }
        public string WinningFactionId { get; }
        public GridCoordinate Coordinate { get; }
        public int EconomicDay { get; }

        public MapCommanderGeneratedRecord(
            MapCommanderState commander,
            string winningFactionId,
            GridCoordinate coordinate,
            int economicDay)
        {
            Commander = commander;
            WinningFactionId = winningFactionId ?? string.Empty;
            Coordinate = coordinate;
            EconomicDay = Math.Max(1, economicDay);
        }
    }

    public readonly struct MapCommanderDeathRecord
    {
        public string CommanderId { get; }
        public string CommanderDisplayName { get; }
        public string DefeatedFactionId { get; }
        public string UnitId { get; }
        public GridCoordinate Coordinate { get; }
        public int EconomicDay { get; }

        public MapCommanderDeathRecord(
            string commanderId,
            string commanderDisplayName,
            string defeatedFactionId,
            string unitId,
            GridCoordinate coordinate,
            int economicDay)
        {
            CommanderId = commanderId ?? string.Empty;
            CommanderDisplayName = commanderDisplayName ?? string.Empty;
            DefeatedFactionId = defeatedFactionId ?? string.Empty;
            UnitId = unitId ?? string.Empty;
            Coordinate = coordinate;
            EconomicDay = Math.Max(1, economicDay);
        }
    }

    public readonly struct MapUnitFormation
    {
        public MapUnitFormationPreset Preset { get; }
        public int FrontlineSoldiers { get; }
        public int RangedSoldiers { get; }
        public int CavalrySoldiers { get; }
        public int TotalSoldiers =>
            FrontlineSoldiers + RangedSoldiers + CavalrySoldiers;
        public decimal FrontlineRatio => GetRatio(FrontlineSoldiers);
        public decimal RangedRatio => GetRatio(RangedSoldiers);
        public decimal CavalryRatio => GetRatio(CavalrySoldiers);

        private MapUnitFormation(
            MapUnitFormationPreset preset,
            int frontlineSoldiers,
            int rangedSoldiers,
            int cavalrySoldiers)
        {
            Preset = preset;
            FrontlineSoldiers = Math.Max(0, frontlineSoldiers);
            RangedSoldiers = Math.Max(0, rangedSoldiers);
            CavalrySoldiers = Math.Max(0, cavalrySoldiers);
        }

        public static MapUnitFormation CreateDefault(
            UnitArchetype archetype,
            int totalSoldiers)
        {
            switch (archetype)
            {
                case UnitArchetype.Archer:
                case UnitArchetype.Slinger:
                    return CreatePreset(
                        MapUnitFormationPreset.Ranged,
                        totalSoldiers);
                case UnitArchetype.Cavalry:
                    return CreatePreset(
                        MapUnitFormationPreset.Cavalry,
                        totalSoldiers);
                default:
                    return CreatePreset(
                        MapUnitFormationPreset.Frontline,
                        totalSoldiers);
            }
        }

        public static MapUnitFormation CreatePreset(
            MapUnitFormationPreset preset,
            int totalSoldiers)
        {
            int total = Math.Max(0, totalSoldiers);
            switch (preset)
            {
                case MapUnitFormationPreset.Ranged:
                    return CreateFromPercentages(preset, total, 40, 50);
                case MapUnitFormationPreset.Cavalry:
                    return CreateFromPercentages(preset, total, 30, 15);
                case MapUnitFormationPreset.Balanced:
                    return CreateFromPercentages(preset, total, 50, 30);
                default:
                    return CreateFromPercentages(
                        MapUnitFormationPreset.Frontline,
                        total,
                        60,
                        25);
            }
        }

        public static MapUnitFormation CreateCustom(
            int frontlineSoldiers,
            int rangedSoldiers,
            int cavalrySoldiers) =>
            new MapUnitFormation(
                MapUnitFormationPreset.Custom,
                frontlineSoldiers,
                rangedSoldiers,
                cavalrySoldiers);

        public MapUnitFormation ScaleTo(int totalSoldiers)
        {
            int total = Math.Max(0, totalSoldiers);
            if (Preset != MapUnitFormationPreset.Custom)
                return CreatePreset(Preset, total);

            if (total == 0 || TotalSoldiers == 0)
            {
                return new MapUnitFormation(Preset, 0, 0, 0);
            }

            int frontline = (int)Math.Floor(
                total * FrontlineSoldiers / (decimal)TotalSoldiers);
            int ranged = (int)Math.Floor(
                total * RangedSoldiers / (decimal)TotalSoldiers);
            return new MapUnitFormation(
                Preset,
                frontline,
                ranged,
                total - frontline - ranged);
        }

        private decimal GetRatio(int soldiers) => TotalSoldiers <= 0
            ? 0m
            : soldiers / (decimal)TotalSoldiers;

        private static MapUnitFormation CreateFromPercentages(
            MapUnitFormationPreset preset,
            int total,
            int frontlinePercent,
            int rangedPercent)
        {
            int frontline = total * frontlinePercent / 100;
            int ranged = total * rangedPercent / 100;
            return new MapUnitFormation(
                preset,
                frontline,
                ranged,
                total - frontline - ranged);
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
        public EquipmentQuality WeaponQuality { get; private set; }
        public EquipmentQuality ArmorQuality { get; private set; }
        public decimal WeaponDurability { get; private set; }
        public decimal ArmorDurability { get; private set; }
        public decimal MaximumWeaponDurability =>
            UnitEquipmentCatalog.GetMaximumDurability(WeaponQuality);
        public decimal MaximumArmorDurability =>
            UnitEquipmentCatalog.GetMaximumDurability(ArmorQuality);
        public decimal WeaponDurabilityRatio => MaximumWeaponDurability <= 0m
            ? 0m
            : Math.Clamp(
                WeaponDurability / MaximumWeaponDurability,
                0m,
                1m);
        public decimal ArmorDurabilityRatio => ArmorClass == ArmorClass.Unarmored
            ? 1m
            : MaximumArmorDurability <= 0m
                ? 0m
                : Math.Clamp(
                    ArmorDurability / MaximumArmorDurability,
                    0m,
                    1m);
        public string WeaponDisplayName =>
            UnitEquipmentCatalog.GetWeaponDisplayName(WeaponType);
        public string ArmorDisplayName =>
            UnitEquipmentCatalog.GetArmorDisplayName(ArmorClass);
        public decimal AttackModifier =>
            UnitEquipmentCatalog.GetAttackModifier(WeaponType) *
            UnitEquipmentCatalog.GetQualityCombatModifier(WeaponQuality) *
            UnitEquipmentCatalog.GetDurabilityCombatModifier(
                WeaponDurability,
                WeaponQuality);
        public decimal DefenseModifier =>
            UnitEquipmentCatalog.GetDefenseModifier(ArmorClass) *
            UnitEquipmentCatalog.GetQualityCombatModifier(ArmorQuality) *
            (ArmorClass == ArmorClass.Unarmored
                ? 1m
                : UnitEquipmentCatalog.GetDurabilityCombatModifier(
                    ArmorDurability,
                    ArmorQuality));
        public MapCommanderState Commander { get; private set; }
        public decimal CommanderAttackModifier =>
            Commander?.AttackModifier ?? 1m;
        public decimal CommanderDefenseModifier =>
            Commander?.DefenseModifier ?? 1m;
        public decimal CommanderMobilityModifier =>
            Commander?.MobilityModifier ?? 1m;
        public decimal ArmorMobilityModifier =>
            UnitEquipmentCatalog.GetMobilityModifier(ArmorClass);
        public decimal ArchetypeAttackModifier =>
            CombatBalance.Get(Archetype).BaseAttack;
        public decimal ArchetypeDefenseModifier =>
            CombatBalance.Get(Archetype).BaseDefense;
        public decimal EffectiveAttackModifier =>
            ArchetypeAttackModifier *
            AttackModifier *
            FormationAttackModifier *
            CommanderAttackModifier;
        public decimal EffectiveDefenseModifier =>
            ArchetypeDefenseModifier *
            DefenseModifier *
            FormationDefenseModifier *
            CommanderDefenseModifier;
        public decimal WeightedBranchMobilityModifier
        {
            get
            {
                decimal frontlineMobility =
                    UnitEquipmentCatalog.GetArchetypeMobilityModifier(
                        GetFrontlineArchetype());
                decimal rangedMobility =
                    UnitEquipmentCatalog.GetArchetypeMobilityModifier(
                        GetRangedArchetype());
                decimal cavalryMobility =
                    UnitEquipmentCatalog.GetArchetypeMobilityModifier(
                        UnitArchetype.Cavalry);
                if (Archetype == UnitArchetype.Cavalry)
                {
                    cavalryMobility = 1m +
                        (cavalryMobility - 1m) * HorseSupplyRatio;
                }
                return Formation.FrontlineRatio * frontlineMobility +
                    Formation.RangedRatio * rangedMobility +
                    Formation.CavalryRatio * cavalryMobility;
            }
        }
        public decimal MobilityModifier => Math.Clamp(
            WeightedBranchMobilityModifier *
            ArmorMobilityModifier *
            CommanderMobilityModifier,
            0.25m,
            2.50m);
        public GridCoordinate Coordinate { get; internal set; }
        public GridCoordinate? Destination { get; internal set; }
        public int MovementProgress { get; internal set; }
        public int TotalMovementTileCount { get; private set; }
        public int CompletedMovementTileCount { get; private set; }
        public int RemainingMovementTileCount => _path.Count;
        public int MaxStamina { get; }
        public int Stamina { get; private set; }
        public int StaminaRecoveryProgress { get; private set; }
        public MapUnitFormation Formation { get; private set; }
        public int Soldiers => Formation.TotalSoldiers;
        public decimal FormationAttackModifier => Math.Clamp(
            1m + (Formation.RangedRatio - 0.25m) * 0.20m +
            (Formation.CavalryRatio - 0.15m) * 0.30m,
            0.75m,
            1.25m);
        public decimal FormationDefenseModifier => Math.Clamp(
            1m + (Formation.FrontlineRatio - 0.60m) * 0.25m -
            (Formation.CavalryRatio - 0.15m) * 0.10m,
            0.75m,
            1.25m);
        public decimal Morale { get; private set; }
        public decimal Fatigue { get; private set; }
        public decimal FoodSupply { get; private set; }
        public decimal EquipmentSupply { get; private set; }
        public decimal MedicineSupply { get; private set; }
        public decimal HorseSupply { get; private set; }
        public bool UsesSupplySystem { get; private set; }
        public MapSupplyMissionKind SupplyMissionKind { get; private set; }
        public GridCoordinate? SupplyMissionCoordinate { get; private set; }
        public decimal FoodSupplyCapacity => Soldiers * 0.21m;
        public decimal EquipmentSupplyCapacity => Soldiers * 0.028m;
        public decimal MedicineSupplyCapacity => Soldiers * 0.007m;
        public int RequiredHorseCount => Archetype == UnitArchetype.Cavalry
            ? Soldiers
            : 0;
        public decimal HorseSupplyCapacity => RequiredHorseCount;
        public decimal FoodSupplyRatio => GetSupplyRatio(
            FoodSupply,
            FoodSupplyCapacity);
        public decimal EquipmentSupplyRatio => GetSupplyRatio(
            EquipmentSupply,
            EquipmentSupplyCapacity);
        public decimal MedicineSupplyRatio => GetSupplyRatio(
            MedicineSupply,
            MedicineSupplyCapacity);
        public decimal HorseSupplyRatio => RequiredHorseCount <= 0
            ? 1m
            : GetSupplyRatio(HorseSupply, HorseSupplyCapacity);
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
            WeaponQuality = EquipmentQuality.Standard;
            ArmorQuality = EquipmentQuality.Standard;
            WeaponDurability = MaximumWeaponDurability;
            ArmorDurability = MaximumArmorDurability;
            MaxStamina = Math.Max(1, maxStamina);
            Stamina = MaxStamina;
            Formation = MapUnitFormation.CreateDefault(
                archetype,
                Math.Max(1, initialSoldiers));
            Morale = 100m;
            Fatigue = 0m;
            FoodSupply = 0m;
            EquipmentSupply = 0m;
            MedicineSupply = 0m;
            HorseSupply = 0m;
            UsesSupplySystem = false;
            SupplyMissionKind = MapSupplyMissionKind.None;
            SupplyMissionCoordinate = null;
            Commander = null;
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
                supplyFactor * (attack
                    ? FormationAttackModifier
                    : FormationDefenseModifier) * (attack
                    ? CommanderAttackModifier
                    : CommanderDefenseModifier),
                2,
                MidpointRounding.AwayFromZero);
        }

        internal int ApplyCasualties(int casualties)
        {
            int applied = Math.Min(Soldiers, Math.Max(0, casualties));
            int previousSoldiers = Soldiers;
            Formation = Formation.ScaleTo(Soldiers - applied);
            HorseSupply = Math.Min(HorseSupply, HorseSupplyCapacity);
            if (applied > 0)
            {
                decimal casualtyRatio = applied /
                    (decimal)Math.Max(1, previousSoldiers);
                DamageEquipment(2m + casualtyRatio * 15m);
                Morale = Math.Max(
                    0m,
                    Morale - applied * 100m /
                    Math.Max(1, previousSoldiers));
            }
            return applied;
        }

        internal void SetFormation(MapUnitFormation formation)
        {
            Formation = formation;
        }

        internal void RestoreAuthoritativeDisplayState(
            int soldiers,
            int stamina,
            decimal morale,
            decimal fatigue)
        {
            Formation = Formation.ScaleTo(Math.Max(0, soldiers));
            HorseSupply = Math.Min(HorseSupply, HorseSupplyCapacity);
            Stamina = Math.Clamp(stamina, 0, MaxStamina);
            Morale = Math.Clamp(morale, 0m, 125m);
            Fatigue = Math.Clamp(fatigue, 0m, 100m);
        }

        internal void AssignCommander(MapCommanderState commander)
        {
            Commander = commander;
        }

        private UnitArchetype GetFrontlineArchetype()
        {
            switch (Archetype)
            {
                case UnitArchetype.Spearman:
                case UnitArchetype.Maceman:
                case UnitArchetype.Swordsman:
                    return Archetype;
                default:
                    return UnitArchetype.Swordsman;
            }
        }

        private UnitArchetype GetRangedArchetype()
        {
            switch (Archetype)
            {
                case UnitArchetype.Archer:
                case UnitArchetype.Slinger:
                    return Archetype;
                default:
                    return UnitArchetype.Archer;
            }
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
                case MapSupplyKind.Horse: return HorseSupply;
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
                case MapSupplyKind.Horse:
                    return HorseSupplyCapacity;
                default: return 0m;
            }
        }

        internal decimal StoreSupply(MapSupplyKind kind, decimal amount)
        {
            if (kind != MapSupplyKind.Horse)
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
                case MapSupplyKind.Horse:
                    HorseSupply += stored;
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
            decimal horseNeed = RequiredHorseCount * 0.01m;
            decimal foodConsumed = TakeSupply(
                MapSupplyKind.Food,
                foodNeed);
            TakeSupply(MapSupplyKind.Equipment, equipmentNeed);
            TakeSupply(MapSupplyKind.Medicine, medicineNeed);
            TakeSupply(MapSupplyKind.Horse, horseNeed);
            decimal foodFulfillment = foodNeed <= 0m
                ? 1m
                : Math.Clamp(foodConsumed / foodNeed, 0m, 1m);
            if (foodFulfillment < 1m)
            {
                AdjustMorale(-8m * (1m - foodFulfillment));
                AdjustFatigue(4m * (1m - foodFulfillment));
            }
            return foodNeed > 0m || equipmentNeed > 0m ||
                medicineNeed > 0m || horseNeed > 0m;
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
                case MapSupplyKind.Horse:
                    HorseSupply -= taken;
                    break;
            }
            return taken;
        }

        internal decimal LoseHorses(decimal lossRate)
        {
            decimal lost = Math.Min(
                HorseSupply,
                HorseSupply * Math.Clamp(lossRate, 0m, 1m));
            HorseSupply -= lost;
            return lost;
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
            ArmorClass armorClass,
            EquipmentQuality weaponQuality = EquipmentQuality.Standard,
            EquipmentQuality armorQuality = EquipmentQuality.Standard)
        {
            WeaponType = weaponType;
            ArmorClass = armorClass;
            WeaponQuality = weaponQuality;
            ArmorQuality = armorQuality;
            WeaponDurability = MaximumWeaponDurability;
            ArmorDurability = MaximumArmorDurability;
        }

        internal bool RepairEquipment()
        {
            bool changed = WeaponDurability < MaximumWeaponDurability ||
                (ArmorClass != ArmorClass.Unarmored &&
                 ArmorDurability < MaximumArmorDurability);
            WeaponDurability = MaximumWeaponDurability;
            ArmorDurability = MaximumArmorDurability;
            return changed;
        }

        internal void DamageEquipment(decimal amount)
        {
            decimal damage = Math.Max(0m, amount);
            WeaponDurability = Math.Max(0m, WeaponDurability - damage);
            if (ArmorClass != ArmorClass.Unarmored)
                ArmorDurability = Math.Max(0m, ArmorDurability - damage);
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

        public static decimal GetArchetypeAttackModifier(
            UnitArchetype archetype) =>
            CombatBalance.Get(archetype).BaseAttack;

        public static decimal GetArchetypeDefenseModifier(
            UnitArchetype archetype) =>
            CombatBalance.Get(archetype).BaseDefense;

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

        internal bool TryAdvanceOneTile()
        {
            if (_path.Count == 0)
                return false;

            Coordinate = _path.Dequeue();
            AdjustFatigue(MovementFatiguePerTile);
            TakeSupply(
                MapSupplyKind.Horse,
                RequiredHorseCount * 0.001m);
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
            bool isDynamic = false,
            decimal initialYieldMultiplier = 1m)
        {
            Coordinate = placement.Coordinate;
            Kind = placement.Kind;
            OwnerFactionId = string.Empty;
            CapturingFactionId = string.Empty;
            SpawnedEconomicDay = Math.Max(0, spawnedEconomicDay);
            IsDynamic = isDynamic;
            YieldMultiplier = Math.Clamp(initialYieldMultiplier, 0.50m, 1.50m);
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
        public bool WasConstructed { get; }
        public string OwnerFactionId { get; }

        public MapMineSpawnRecord(
            GridCoordinate coordinate,
            MineKind kind,
            int economicDay,
            bool wasConstructed = false,
            string ownerFactionId = "")
        {
            Coordinate = coordinate;
            Kind = kind;
            EconomicDay = Math.Max(1, economicDay);
            WasConstructed = wasConstructed;
            OwnerFactionId = ownerFactionId ?? string.Empty;
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
        public const string ProtagonistCommanderId =
            "commander_protagonist";

        private static readonly string[] GeneratedCommanderFamilyNames =
        {
            "김", "이", "박", "최", "정", "조", "임", "백"
        };
        private static readonly string[] GeneratedCommanderGivenNames =
        {
            "서준", "하늘", "지안", "도현", "수아", "현우", "은채", "태윤"
        };
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
            MapSupplyKind.Medicine,
            MapSupplyKind.Horse
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
        private readonly Dictionary<string, FactionStrategyKind>
            _aiStrategies =
                new Dictionary<string, FactionStrategyKind>(
                    StringComparer.Ordinal);
        private readonly Dictionary<string, GridCoordinate>
            _aiLongTermTargets =
                new Dictionary<string, GridCoordinate>(StringComparer.Ordinal);
        private readonly List<MapUnitState> _units = new List<MapUnitState>();
        private readonly List<MapCommanderState> _commanders =
            new List<MapCommanderState>();
        private readonly List<MapMineControlState> _mines =
            new List<MapMineControlState>();
        private readonly Dictionary<GridCoordinate, MapEconomicSurveyState>
            _economicSurveys =
                new Dictionary<GridCoordinate, MapEconomicSurveyState>();
        private readonly List<MapMineConstructionState> _mineConstructions =
            new List<MapMineConstructionState>();
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
        private readonly Dictionary<string, MapWorldMissionState>
            _worldMissionsByUnitId =
                new Dictionary<string, MapWorldMissionState>(
                    StringComparer.Ordinal);
        private readonly List<MapSupplyInterdictionResult>
            _lastSupplyInterdictionResults =
                new List<MapSupplyInterdictionResult>();
        private int _unitSequence;
        private int _generatedCommanderSequence;
        private int _fixedStepSequence;
        private int _economicDaySequence;
        private readonly bool _enableAi;

        public string PlayerFactionId { get; }
        public IReadOnlyList<MapUnitState> Units => _units;
        public IReadOnlyList<MapCommanderState> Commanders => _commanders;
        public IReadOnlyList<MapMineControlState> Mines => _mines;
        public IReadOnlyCollection<MapEconomicSurveyState> EconomicSurveys =>
            _economicSurveys.Values;
        public IReadOnlyList<MapMineConstructionState> MineConstructions =>
            _mineConstructions;
        public IReadOnlyList<MapCastleControlState> Castles => _castles;
        public int FixedStepsToCapture => _tuning.FixedStepsToCapture;
        public int FixedStepsPerMove => _tuning.FixedStepsPerMove;
        public int UnitScoutingRange => _tuning.UnitScoutingRange;
        public int InitialSoldiersPerUnit =>
            _tuning.InitialSoldiersPerUnit;
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
        public IReadOnlyCollection<MapWorldMissionState> WorldMissions =>
            _worldMissionsByUnitId.Values;

        public event Action StateChanged;
        public event Action<MapMineCaptureRecord> MineCaptured;
        public event Action<MapMineSpawnRecord> MineSpawned;
        public event Action<MapEconomicSurveyState> EconomicSurveyCompleted;
        public event Action<MapMineConstructionState> MineConstructionStarted;
        public event Action<MapMineConstructionCompletedRecord>
            MineConstructionCompleted;
        public event Action<MapCastleCaptureRecord> CastleCaptured;
        public event Action<MapCapitalDestroyedRecord> CapitalDestroyed;
        public event Action<MapCastleRoleChangedRecord> CastleRoleChanged;
        public event Action<MapSiegeDayResult> SiegeDayResolved;
        public event Action<MapCommanderGeneratedRecord> CommanderGenerated;
        public event Action<MapCommanderDeathRecord> CommanderDied;
        public event Action<MapSupplyInterdictionResult>
            SupplyInterdictionResolved;
        public event Action<MapWorldMissionState> WorldMissionReady;

        public RealtimeMapGameplayService(
            GridMapLayout layout,
            string playerFactionId,
            IReadOnlyList<string> aiFactionIds = null,
            MapGameplayTuning tuning = null,
            bool enableAi = true)
        {
            _layout = layout ?? throw new ArgumentNullException(nameof(layout));
            PlayerFactionId = RequireFactionId(playerFactionId);
            _tuning = tuning ?? new MapGameplayTuning();
            _enableAi = enableAi;
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
                _aiStrategies[factionId] = FactionStrategicAi.GetStrategy(i);
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
            SeedInitialCommander();
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

        public MapCommanderState FindCommander(string commanderId)
        {
            if (string.IsNullOrWhiteSpace(commanderId))
                return null;

            for (int i = 0; i < _commanders.Count; i++)
            {
                if (string.Equals(
                        _commanders[i].Id,
                        commanderId,
                        StringComparison.Ordinal))
                {
                    return _commanders[i];
                }
            }

            return null;
        }

        public bool CanHireCommander(
            string ownerFactionId,
            string commanderId,
            string unitId,
            out string reason)
        {
            if (string.IsNullOrWhiteSpace(ownerFactionId))
            {
                reason = "장수를 소환할 세력이 필요합니다.";
                return false;
            }

            MapCommanderState commander = FindCommander(commanderId);
            if (commander == null)
            {
                reason = "소환할 장수를 찾을 수 없습니다.";
                return false;
            }
            if (!commander.IsAlive)
            {
                reason = "전사한 장수는 소환할 수 없습니다.";
                return false;
            }
            if (commander.IsProtagonist && !string.Equals(
                    ownerFactionId,
                    PlayerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "주인공 장수는 플레이어 세력만 지휘할 수 있습니다.";
                return false;
            }
            if (!commander.IsAvailable)
            {
                reason = "이미 다른 부대에 배속된 장수입니다.";
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "장수를 배속할 아군 부대를 찾을 수 없습니다.";
                return false;
            }
            if (unit.Commander != null)
            {
                reason = $"{unit.Id}에는 이미 {unit.Commander.DisplayName} 장수가 있습니다.";
                return false;
            }
            MapCastleControlState castle = FindCastle(unit.Coordinate);
            if (castle == null || !string.Equals(
                    castle.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "장수 소환과 배속은 부대가 주둔한 아군 성에서만 가능합니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryHireCommander(
            string ownerFactionId,
            string commanderId,
            string unitId,
            out string reason)
        {
            if (!CanHireCommander(
                    ownerFactionId,
                    commanderId,
                    unitId,
                    out reason))
            {
                return false;
            }

            MapCommanderState commander = FindCommander(commanderId);
            MapUnitState unit = FindUnit(unitId);
            commander.Hire(ownerFactionId, unit.Id);
            unit.AssignCommander(commander);
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        private void SeedInitialCommander()
        {
            _commanders.Add(new MapCommanderState(
                ProtagonistCommanderId,
                "주인공",
                82,
                78,
                74,
                MapCommanderPersonality.Cautious,
                100,
                0m,
                isProtagonist: true));
        }

        private bool AssignAvailableCommandersToAiFaction(string factionId)
        {
            bool changed = false;
            for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
            {
                MapUnitState unit = _units[unitIndex];
                if (unit.Commander != null || !string.Equals(
                        unit.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MapCastleControlState castle = FindCastle(unit.Coordinate);
                if (castle == null || !string.Equals(
                        castle.OwnerFactionId,
                        factionId,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                MapCommanderState best = null;
                int bestScore = int.MinValue;
                for (int commanderIndex = 0;
                     commanderIndex < _commanders.Count;
                     commanderIndex++)
                {
                    MapCommanderState candidate = _commanders[commanderIndex];
                    if (!candidate.IsAvailable || candidate.IsProtagonist)
                        continue;

                    int score = candidate.Command + candidate.Tactics +
                        candidate.Logistics + candidate.Loyalty / 2;
                    if (score > bestScore)
                    {
                        best = candidate;
                        bestScore = score;
                    }
                }

                if (best == null)
                    break;

                best.Hire(factionId, unit.Id);
                unit.AssignCommander(best);
                changed = true;
            }

            return changed;
        }

        private MapCommanderState GenerateVictoryCommander(
            string winningFactionId,
            GridCoordinate coordinate)
        {
            _generatedCommanderSequence++;
            int profileSeed = CreateBattleOutcomeRoll(
                "profile_" + _generatedCommanderSequence,
                winningFactionId,
                coordinate,
                string.Empty);
            string displayName =
                GeneratedCommanderFamilyNames[
                    profileSeed % GeneratedCommanderFamilyNames.Length] +
                GeneratedCommanderGivenNames[
                    (profileSeed / GeneratedCommanderFamilyNames.Length) %
                    GeneratedCommanderGivenNames.Length];
            int command = 55 + profileSeed % 36;
            int tactics = 55 + (profileSeed * 7 + 11) % 36;
            int logistics = 55 + (profileSeed * 13 + 19) % 36;
            int loyalty = 55 + (profileSeed * 17 + 23) % 41;
            decimal hiringCost = 18000m +
                (command + tactics + logistics) * 100m;

            return new MapCommanderState(
                $"commander_generated_{_economicDaySequence}_" +
                $"{coordinate.X}_{coordinate.Y}_" +
                _generatedCommanderSequence,
                displayName,
                command,
                tactics,
                logistics,
                (MapCommanderPersonality)(profileSeed % 4),
                loyalty,
                hiringCost);
        }

        private int CreateBattleOutcomeRoll(
            string category,
            string factionId,
            GridCoordinate coordinate,
            string commanderId)
        {
            uint hash = 2166136261u;
            hash = MixStableHash(hash, _layout.Seed);
            hash = MixStableHash(hash, _economicDaySequence);
            hash = MixStableHash(hash, coordinate.X);
            hash = MixStableHash(hash, coordinate.Y);
            hash = MixStableHash(hash, category);
            hash = MixStableHash(hash, factionId);
            hash = MixStableHash(hash, commanderId);
            return (int)(hash % MapCommanderBattleRules.RollScale);
        }

        private static uint MixStableHash(uint hash, int value)
        {
            unchecked
            {
                for (int shift = 0; shift < 32; shift += 8)
                {
                    hash ^= (byte)(value >> shift);
                    hash *= 16777619u;
                }
                return hash;
            }
        }

        private static uint MixStableHash(uint hash, string value)
        {
            unchecked
            {
                string safeValue = value ?? string.Empty;
                for (int i = 0; i < safeValue.Length; i++)
                {
                    hash ^= safeValue[i];
                    hash *= 16777619u;
                }
                return hash;
            }
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

        public bool IsCoastalPort(GridCoordinate coordinate)
        {
            MapCastleControlState castle = FindCastle(coordinate);
            if (castle == null || castle.IsDestroyed ||
                !_layout.IsLand(coordinate))
            {
                return false;
            }

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
                }
            }

            return false;
        }

        public bool IsUsingSeaTransport(MapUnitState unit)
        {
            if (unit == null)
                return false;
            if (_layout.Contains(unit.Coordinate) &&
                !_layout.IsLand(unit.Coordinate))
            {
                return true;
            }

            for (int i = 0; i < unit.PlannedPath.Count; i++)
            {
                GridCoordinate coordinate = unit.PlannedPath[i];
                if (_layout.Contains(coordinate) &&
                    !_layout.IsLand(coordinate))
                {
                    return true;
                }
            }

            return false;
        }

        public bool WillUseSeaTransport(
            string ownerFactionId,
            string unitId,
            GridCoordinate destination)
        {
            return CanIssueMove(
                    ownerFactionId,
                    unitId,
                    destination,
                    out IReadOnlyList<GridCoordinate> path,
                    out _) &&
                ContainsOceanTile(path);
        }

        public MapEconomicSurveyState FindEconomicSurvey(
            GridCoordinate coordinate)
        {
            _economicSurveys.TryGetValue(coordinate, out MapEconomicSurveyState survey);
            return survey;
        }

        public MapMineConstructionState FindMineConstruction(
            GridCoordinate coordinate)
        {
            for (int i = 0; i < _mineConstructions.Count; i++)
            {
                if (_mineConstructions[i].Coordinate.Equals(coordinate))
                    return _mineConstructions[i];
            }

            return null;
        }

        public bool CanSurveyEconomicSite(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            out string reason)
        {
            if (!_layout.TryNormalize(coordinate, out GridCoordinate normalized) ||
                !_layout.IsLand(normalized))
            {
                reason = "경제 탐사는 육지 칸에서만 수행할 수 있습니다.";
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "경제 탐사를 수행할 아군 부대를 찾을 수 없습니다.";
                return false;
            }
            if (unit.IsMoving || !unit.Coordinate.Equals(normalized))
            {
                reason = "선택한 부대가 해당 칸에 정지한 뒤 경제 탐사를 수행할 수 있습니다.";
                return false;
            }
            if (unit.Stamina < MapEconomicDevelopmentRules.SurveyStaminaCost)
            {
                reason = $"경제 탐사에 필요한 체력이 부족합니다. 필요 " +
                    $"{MapEconomicDevelopmentRules.SurveyStaminaCost}, " +
                    $"현재 {unit.Stamina}/{unit.MaxStamina}";
                return false;
            }
            if (FindMine(normalized) != null ||
                FindCastle(normalized) != null ||
                IsFactionBase(normalized))
            {
                reason = "기존 거점이나 광산이 있는 칸은 경제 탐사 대상이 아닙니다.";
                return false;
            }
            if (FindMineConstruction(normalized) != null)
            {
                reason = "이미 채굴소를 건설 중인 칸입니다.";
                return false;
            }
            if (FindEconomicSurvey(normalized) != null)
            {
                reason = "이미 경제 탐사를 마친 칸입니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TrySurveyEconomicSite(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            out MapEconomicSurveyState survey,
            out string reason)
        {
            survey = null;
            if (!CanSurveyEconomicSite(
                    ownerFactionId,
                    unitId,
                    coordinate,
                    out reason))
            {
                return false;
            }

            _layout.TryNormalize(coordinate, out GridCoordinate normalized);
            MapUnitState unit = FindUnit(unitId);
            if (!unit.TrySpendStamina(
                    MapEconomicDevelopmentRules.SurveyStaminaCost,
                    out reason))
            {
                return false;
            }

            survey = MapEconomicDevelopmentRules.Evaluate(
                _layout,
                normalized,
                _economicDaySequence);
            _economicSurveys.Add(normalized, survey);
            EconomicSurveyCompleted?.Invoke(survey);
            StateChanged?.Invoke();
            reason = string.Empty;
            return true;
        }

        public bool CanStartMineConstruction(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            out MapEconomicSurveyState survey,
            out string reason)
        {
            survey = FindEconomicSurvey(coordinate);
            if (survey == null || !survey.HasViableDeposit)
            {
                reason = survey == null
                    ? "먼저 이 칸의 경제 탐사를 완료해야 합니다."
                    : "채굴 가치가 있는 매장지를 찾지 못한 칸입니다.";
                return false;
            }
            if (FindMine(coordinate) != null)
            {
                reason = "이미 광산이 있는 칸입니다.";
                return false;
            }
            if (FindMineConstruction(coordinate) != null)
            {
                reason = "이미 채굴소를 건설 중인 칸입니다.";
                return false;
            }

            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "건설을 지시할 아군 부대를 찾을 수 없습니다.";
                return false;
            }
            if (unit.IsMoving || !unit.Coordinate.Equals(coordinate))
            {
                reason = "선택한 부대가 탐사한 칸에 정지한 뒤 건설을 시작할 수 있습니다.";
                return false;
            }

            reason = string.Empty;
            return true;
        }

        public bool TryStartMineConstruction(
            string ownerFactionId,
            string unitId,
            GridCoordinate coordinate,
            out MapMineConstructionState construction,
            out string reason)
        {
            construction = null;
            if (!CanStartMineConstruction(
                    ownerFactionId,
                    unitId,
                    coordinate,
                    out MapEconomicSurveyState survey,
                    out reason))
            {
                return false;
            }

            MineKind kind = survey.DepositKind.Value;
            construction = new MapMineConstructionState(
                coordinate,
                ownerFactionId,
                kind,
                MapEconomicDevelopmentRules.GetConstructionCost(kind),
                MapEconomicDevelopmentRules.GetConstructionDays(kind),
                survey.YieldMultiplier);
            _mineConstructions.Add(construction);
            MineConstructionStarted?.Invoke(construction);
            StateChanged?.Invoke();
            reason = string.Empty;
            return true;
        }

        public bool TryRestoreAuthoritativeUnitState(
            string unitId,
            GridCoordinate coordinate,
            IReadOnlyList<GridCoordinate> remainingPath,
            int movementProgress,
            int soldiers,
            int stamina,
            decimal morale,
            decimal fatigue,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (unit == null)
            {
                reason = "복원할 지도 부대를 찾을 수 없습니다.";
                return false;
            }
            if (!_layout.Contains(coordinate) ||
                !_layout.IsLand(coordinate))
            {
                reason = "서버 부대 좌표가 이동 가능한 지도 칸이 아닙니다.";
                return false;
            }

            var path = new List<GridCoordinate>();
            if (remainingPath != null)
            {
                for (int i = 0; i < remainingPath.Count; i++)
                {
                    if (!_layout.Contains(remainingPath[i]) ||
                        !_layout.IsLand(remainingPath[i]))
                    {
                        reason = "서버 부대 이동 경로가 올바르지 않습니다.";
                        return false;
                    }
                    path.Add(remainingPath[i]);
                }
            }

            unit.Coordinate = coordinate;
            unit.RestoreAuthoritativeDisplayState(
                soldiers,
                stamina,
                morale,
                fatigue);
            unit.SetPath(path);
            unit.MovementProgress = path.Count == 0
                ? 0
                : Math.Clamp(
                    movementProgress,
                    0,
                    Math.Max(0, GetRequiredMovementStepsPerTile(unit) - 1));
            reason = string.Empty;
            return true;
        }

        public bool TryRestoreAuthoritativeMineState(
            GridCoordinate coordinate,
            string ownerFactionId,
            string capturingFactionId,
            int captureProgress,
            out string reason)
        {
            MapMineControlState mine = FindMine(coordinate);
            if (mine == null)
            {
                reason = "복원할 광산을 찾을 수 없습니다.";
                return false;
            }

            mine.OwnerFactionId = ownerFactionId ?? string.Empty;
            mine.CapturingFactionId = capturingFactionId ?? string.Empty;
            mine.CaptureProgress = Math.Clamp(
                captureProgress,
                0,
                FixedStepsToCapture);
            reason = string.Empty;
            return true;
        }

        public bool TryRestoreAuthoritativeCastleState(
            GridCoordinate coordinate,
            string ownerFactionId,
            string capturingFactionId,
            int captureProgress,
            MapCastleRole role,
            MapCastleConflictKind conflictKind,
            MapSiegeAction siegeAction,
            MapOccupationPolicy occupationPolicy,
            bool isDestroyed,
            int wallDurability,
            int foodSupply,
            out string reason)
        {
            MapCastleControlState castle = FindCastle(coordinate);
            if (castle == null)
            {
                reason = "복원할 성을 찾을 수 없습니다.";
                return false;
            }

            castle.RestoreAuthoritativeState(
                ownerFactionId,
                capturingFactionId,
                captureProgress,
                role,
                conflictKind,
                siegeAction,
                occupationPolicy,
                isDestroyed,
                wallDurability,
                foodSupply);
            RefreshCastleGarrison(castle);
            reason = string.Empty;
            return true;
        }

        public void RestoreAuthoritativeEconomicDay(int economicDay)
        {
            _economicDaySequence = Math.Max(0, economicDay);
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
            return TryCreateUnitAt(
                ownerFactionId,
                origin,
                archetype,
                weaponType,
                armorClass,
                EquipmentQuality.Standard,
                EquipmentQuality.Standard,
                out unit,
                out reason);
        }

        public bool TryCreateUnitAt(
            string ownerFactionId,
            GridCoordinate origin,
            UnitArchetype archetype,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            EquipmentQuality weaponQuality,
            EquipmentQuality armorQuality,
            out MapUnitState unit,
            out string reason)
        {
            unit = null;
            if (!CanCreateUnitAt(ownerFactionId, origin, out reason))
                return false;

            MapCastleControlState castle = FindCastle(origin);
            if (castle == null || !string.Equals(
                    castle.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "아군 성의 무기고에서만 부대를 편성할 수 있습니다.";
                return false;
            }
            int requiredHorses = archetype == UnitArchetype.Cavalry
                ? _tuning.InitialSoldiersPerUnit
                : 0;
            if (requiredHorses > 0 &&
                castle.GetWarehouseSupply(MapSupplyKind.Horse) <
                    requiredHorses)
            {
                reason = $"기병 모집에 필요한 말이 부족합니다. 필요 " +
                    $"{requiredHorses:N0}, 보유 " +
                    $"{castle.WarehouseHorseAmount:N0}";
                return false;
            }
            if (!castle.TryConsumeLoadout(
                    weaponType,
                    weaponQuality,
                    armorClass,
                    armorQuality,
                    _tuning.InitialSoldiersPerUnit,
                    out reason))
            {
                return false;
            }
            decimal recruitedHorses = requiredHorses > 0
                ? castle.TakeWarehouseSupply(
                    MapSupplyKind.Horse,
                    requiredHorses)
                : 0m;

            MapRecruitmentSiteState recruitmentSite =
                _recruitmentSites[origin];
            if (!recruitmentSite.TryConsumeRecruit())
            {
                castle.ReturnLoadout(
                    weaponType,
                    weaponQuality,
                    armorClass,
                    armorQuality,
                    _tuning.InitialSoldiersPerUnit,
                    1m,
                    1m);
                castle.StoreWarehouseSupply(
                    MapSupplyKind.Horse,
                    recruitedHorses);
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
            unit.ChangeEquipment(
                weaponType,
                armorClass,
                weaponQuality,
                armorQuality);
            if (recruitedHorses > 0m)
                unit.StoreSupply(MapSupplyKind.Horse, recruitedHorses);
            if (_supplyEnabledFactionIds.Contains(ownerFactionId))
                unit.EnableSupplySystem();
            _units.Add(unit);
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
            return TryChangeEquipment(
                ownerFactionId,
                unitId,
                weaponType,
                armorClass,
                EquipmentQuality.Standard,
                EquipmentQuality.Standard,
                out reason);
        }

        public bool TryChangeEquipment(
            string ownerFactionId,
            string unitId,
            UnitWeaponType weaponType,
            ArmorClass armorClass,
            EquipmentQuality weaponQuality,
            EquipmentQuality armorQuality,
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

            if (unit.WeaponType == weaponType &&
                unit.ArmorClass == armorClass &&
                unit.WeaponQuality == weaponQuality &&
                unit.ArmorQuality == armorQuality)
            {
                reason = "현재 사용 중인 장비와 같습니다.";
                return false;
            }

            MapCastleControlState castle = FindCastle(unit.Coordinate);
            if (castle == null || !string.Equals(
                    castle.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "장비 변경은 부대가 주둔한 아군 성에서만 가능합니다.";
                return false;
            }
            if (!castle.TryConsumeLoadout(
                    weaponType,
                    weaponQuality,
                    armorClass,
                    armorQuality,
                    unit.Soldiers,
                    out reason))
            {
                return false;
            }

            castle.ReturnLoadout(
                unit.WeaponType,
                unit.WeaponQuality,
                unit.ArmorClass,
                unit.ArmorQuality,
                unit.Soldiers,
                unit.WeaponDurabilityRatio,
                unit.ArmorDurabilityRatio);
            unit.ChangeEquipment(
                weaponType,
                armorClass,
                weaponQuality,
                armorQuality);
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool TryRepairEquipment(
            string ownerFactionId,
            string unitId,
            out decimal consumedEquipment,
            out string reason)
        {
            consumedEquipment = 0m;
            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "수리할 아군 부대를 찾을 수 없습니다.";
                return false;
            }

            MapCastleControlState castle = FindCastle(unit.Coordinate);
            if (castle == null || !string.Equals(
                    castle.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "장비 수리는 부대가 주둔한 아군 성에서만 가능합니다.";
                return false;
            }

            decimal missingRatio = 1m - unit.WeaponDurabilityRatio;
            if (unit.ArmorClass != ArmorClass.Unarmored)
                missingRatio += 1m - unit.ArmorDurabilityRatio;
            decimal required = Math.Ceiling(
                Math.Max(0m, missingRatio) * unit.Soldiers * 0.02m);
            if (required <= 0m)
            {
                reason = "장비 내구도가 이미 최대입니다.";
                return false;
            }
            if (castle.GetWarehouseSupply(MapSupplyKind.Equipment) < required)
            {
                reason = $"수리용 장비 보급이 부족합니다. 필요 {required:N1}, " +
                    $"보유 {castle.WarehouseEquipmentAmount:N1}";
                return false;
            }

            consumedEquipment = castle.TakeWarehouseSupply(
                MapSupplyKind.Equipment,
                required);
            unit.RepairEquipment();
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySetUnitFormationPreset(
            string ownerFactionId,
            string unitId,
            MapUnitFormationPreset preset,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (!CanChangeUnitFormation(
                    ownerFactionId,
                    unit,
                    preset != MapUnitFormationPreset.Custom,
                    out reason))
            {
                return false;
            }

            unit.SetFormation(MapUnitFormation.CreatePreset(
                preset,
                unit.Soldiers));
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        public bool TrySetUnitFormation(
            string ownerFactionId,
            string unitId,
            int frontlineSoldiers,
            int rangedSoldiers,
            int cavalrySoldiers,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (!CanChangeUnitFormation(
                    ownerFactionId,
                    unit,
                    true,
                    out reason))
            {
                return false;
            }

            if (frontlineSoldiers < 0 || rangedSoldiers < 0 ||
                cavalrySoldiers < 0)
            {
                reason = "병과별 인원은 0명 이상이어야 합니다.";
                return false;
            }

            int requestedTotal = frontlineSoldiers + rangedSoldiers +
                cavalrySoldiers;
            if (requestedTotal != unit.Soldiers)
            {
                reason = $"편성 인원 합계가 총병력 {unit.Soldiers:N0}명과 " +
                    $"같아야 합니다. 현재 합계 {requestedTotal:N0}명";
                return false;
            }

            unit.SetFormation(MapUnitFormation.CreateCustom(
                frontlineSoldiers,
                rangedSoldiers,
                cavalrySoldiers));
            reason = string.Empty;
            StateChanged?.Invoke();
            return true;
        }

        private static bool CanChangeUnitFormation(
            string ownerFactionId,
            MapUnitState unit,
            bool validPreset,
            out string reason)
        {
            if (unit == null)
            {
                reason = "편성을 변경할 부대를 찾을 수 없습니다.";
                return false;
            }

            if (!string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "다른 세력의 부대 편성은 변경할 수 없습니다.";
                return false;
            }

            if (!validPreset)
            {
                reason = "사용자 편성은 병과별 인원을 직접 지정해야 합니다.";
                return false;
            }

            if (unit.Soldiers <= 0)
            {
                reason = "병력이 없는 부대는 편성을 변경할 수 없습니다.";
                return false;
            }

            reason = string.Empty;
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

            List<GridCoordinate> landRoute = FindShortestLandPath(
                unit.Coordinate,
                normalized);
            List<GridCoordinate> seaRoute =
                FindShortestFriendlyPortSeaPath(
                    ownerFactionId,
                    unit.Coordinate,
                    normalized);
            List<GridCoordinate> route = seaRoute.Count > 0 &&
                (landRoute.Count == 0 || seaRoute.Count < landRoute.Count)
                    ? seaRoute
                    : landRoute;
            if (route.Count == 0)
            {
                reason = "이동 가능한 육지 경로나 아군 항구 간 해상 경로가 없습니다.";
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

        public bool TryAssignWorldMission(
            string ownerFactionId,
            string unitId,
            string opportunityId,
            GridCoordinate target,
            MapWorldMissionAction action,
            out string reason)
        {
            return TryAssignWorldMission(
                ownerFactionId,
                unitId,
                opportunityId,
                new WorldMissionMapTarget(target, action),
                out reason);
        }

        public bool TryAssignWorldMission(
            string ownerFactionId,
            string unitId,
            string opportunityId,
            WorldMissionMapTarget mapTarget,
            out string reason)
        {
            MapUnitState unit = FindUnit(unitId);
            if (unit == null || !string.Equals(
                    unit.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal))
            {
                reason = "미션을 수행할 아군 부대를 찾을 수 없습니다.";
                return false;
            }
            if (string.IsNullOrWhiteSpace(opportunityId))
            {
                reason = "경제 미션 ID가 비어 있습니다.";
                return false;
            }
            if (!_layout.TryNormalize(
                    mapTarget.Coordinate,
                    out GridCoordinate normalized) ||
                !_layout.IsLand(normalized))
            {
                reason = "미션 목표가 이동 가능한 지도 좌표가 아닙니다.";
                return false;
            }

            var mission = new MapWorldMissionState(
                opportunityId,
                unitId,
                normalized,
                mapTarget.Action);
            if (mapTarget.CargoKind.HasValue && mapTarget.RequiredCargo > 0m)
            {
                MapCastleControlState source = FindCastle(unit.Coordinate);
                if (source == null || source.IsDestroyed || !string.Equals(
                        source.OwnerFactionId,
                        ownerFactionId,
                        StringComparison.Ordinal))
                {
                    reason = "납품·밀수 부대는 먼저 아군 성에서 화물을 적재해야 합니다.";
                    return false;
                }
                decimal available = source.GetWarehouseSupply(
                    mapTarget.CargoKind.Value);
                if (available < mapTarget.RequiredCargo)
                {
                    reason = $"성 창고 화물이 부족합니다. 필요 " +
                        $"{mapTarget.RequiredCargo:N1}, 보유 {available:N1}";
                    return false;
                }
                mission.CargoSource = source.Coordinate;
                mission.CargoKind = mapTarget.CargoKind;
                mission.RequiredCargo = mapTarget.RequiredCargo;
                mission.LoadedCargo = source.TakeWarehouseSupply(
                    mapTarget.CargoKind.Value,
                    mapTarget.RequiredCargo);
            }
            _worldMissionsByUnitId[unitId] = mission;
            if (!unit.Coordinate.Equals(normalized) &&
                !TryIssueMove(
                    ownerFactionId,
                    unitId,
                    normalized,
                    out reason))
            {
                _worldMissionsByUnitId.Remove(unitId);
                ReturnWorldMissionCargo(mission);
                return false;
            }

            reason = string.Empty;
            AdvanceWorldMissions();
            StateChanged?.Invoke();
            return true;
        }

        public bool CompleteWorldMission(
            string unitId,
            string opportunityId,
            bool cancelled = false)
        {
            if (!_worldMissionsByUnitId.TryGetValue(
                    unitId,
                    out MapWorldMissionState mission) ||
                !string.Equals(
                    mission.OpportunityId,
                    opportunityId,
                    StringComparison.Ordinal))
                return false;

            mission.Status = cancelled
                ? MapWorldMissionStatus.Cancelled
                : MapWorldMissionStatus.Completed;
            if (cancelled)
                ReturnWorldMissionCargo(mission);
            _worldMissionsByUnitId.Remove(unitId);
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
            if (IsUsingSeaTransport(unit))
            {
                reason = "간단 해상 수송은 자동 하선 전까지 취소할 수 없습니다.";
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
                if (_enableAi &&
                    _fixedStepSequence % _tuning.AiDecisionIntervalSteps == 0)
                    changed |= RunAiDecisions();
                changed |= MoveUnitsOneFixedStep();
                changed |= AdvanceCastleCaptures();
                changed |= AdvanceMineCaptures();
                changed |= AdvanceWorldMissions();
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
            bool constructionProgressed = _mineConstructions.Count > 0;
            bool constructionCompleted = AdvanceMineConstructions(
                out MapMineSpawnRecord constructedMine);
            spawnedMine = constructedMine;
            bool mineSpawned = constructionCompleted;
            if (_economicDaySequence % _tuning.MineSpawnIntervalDays == 0 &&
                TryFindDynamicMineCoordinate(
                    _economicDaySequence,
                    out GridCoordinate coordinate))
            {
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
                var dynamicMine = new MapMineSpawnRecord(
                    coordinate,
                    kind,
                    _economicDaySequence);
                if (!mineSpawned)
                    spawnedMine = dynamicMine;
                MineSpawned?.Invoke(dynamicMine);
                mineSpawned = true;
            }

            if (supplyChanged || recruitmentChanged || fatigueChanged ||
                siegeChanged || constructionProgressed || mineSpawned)
            {
                StateChanged?.Invoke();
            }
            return mineSpawned;
        }

        private bool AdvanceMineConstructions(
            out MapMineSpawnRecord firstCompletedMine)
        {
            firstCompletedMine = default;
            bool completedAny = false;
            bool recordedFirst = false;
            for (int i = _mineConstructions.Count - 1; i >= 0; i--)
            {
                MapMineConstructionState construction = _mineConstructions[i];
                if (!construction.AdvanceDay())
                    continue;

                completedAny = true;

                var mine = new MapMineControlState(
                    new MinePlacement(construction.Coordinate, construction.Kind),
                    _economicDaySequence,
                    true,
                    construction.YieldMultiplier)
                {
                    OwnerFactionId = construction.OwnerFactionId
                };
                _mines.Add(mine);
                _mineConstructions.RemoveAt(i);
                var spawnRecord = new MapMineSpawnRecord(
                    construction.Coordinate,
                    construction.Kind,
                    _economicDaySequence,
                    true,
                    construction.OwnerFactionId);
                if (!recordedFirst)
                {
                    firstCompletedMine = spawnRecord;
                    recordedFirst = true;
                }
                MineConstructionCompleted?.Invoke(
                    new MapMineConstructionCompletedRecord(
                        construction.Coordinate,
                        construction.OwnerFactionId,
                        construction.Kind,
                        _economicDaySequence,
                        construction.YieldMultiplier));
                MineSpawned?.Invoke(spawnRecord);
            }

            return completedAny;
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
                string defenderFaction = castle.OwnerFactionId;
                MapSiegeAction resolvedAction = castle.SiegeAction;
                decimal attackerPower = SumCombatPowerAt(
                    castle.Coordinate,
                    attackerFaction,
                    true);
                decimal defenderPower = SumCombatPowerAt(
                    castle.Coordinate,
                    defenderFaction,
                    false);
                int defenderSoldiers = SumSoldiersAt(
                    castle.Coordinate,
                    defenderFaction);
                var defeatedCommanderUnits =
                    new List<MapUnitState>();
                for (int unitIndex = 0;
                     unitIndex < _units.Count;
                     unitIndex++)
                {
                    MapUnitState unit = _units[unitIndex];
                    if (unit.Commander != null &&
                        unit.Soldiers > 0 &&
                        unit.Coordinate.Equals(castle.Coordinate) &&
                        string.Equals(
                            unit.OwnerFactionId,
                            defenderFaction,
                            StringComparison.Ordinal))
                    {
                        defeatedCommanderUnits.Add(unit);
                    }
                }
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
                    defenderFaction,
                    defenderCasualties);
                int appliedAttackerCasualties = ApplyCasualtiesAt(
                    castle.Coordinate,
                    attackerFaction,
                    attackerCasualties);
                AdjustFactionMoraleAt(
                    castle.Coordinate,
                    defenderFaction,
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
                    defenderFaction);
                decimal defenderMorale = AverageMoraleAt(
                    castle.Coordinate,
                    defenderFaction);
                bool sufferedDecisiveLoss = remainingDefenders > 0 &&
                    (appliedDefenderCasualties * 4 >=
                         Math.Max(1, defenderSoldiers) ||
                     defenderMorale <= 20m);
                if (sufferedDecisiveLoss)
                {
                    if (TryFindRetreatDestination(
                            castle.Coordinate,
                            defenderFaction,
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
                            defenderFaction,
                            RoundToInt(remainingDefenders * pursuitRate));
                        defenderRetreated = RetreatFactionUnitsAt(
                            castle.Coordinate,
                            defenderFaction,
                            retreatDestination);
                        RefreshCastleGarrison(castle);
                        remainingDefenders = SumSoldiersAt(
                            castle.Coordinate,
                            defenderFaction);
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
                bool battleDecided = defenderSoldiers > 0 &&
                    (defenderRetreated || remainingDefenders == 0);
                if (battleDecided)
                {
                    ResolveDecisiveBattleCommanderOutcome(
                        attackerFaction,
                        defenderFaction,
                        castle.Coordinate,
                        defeatedCommanderUnits);
                }
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

        public IReadOnlyList<MapMilitaryUpkeepRecord>
            CreateDailyMilitaryUpkeep()
        {
            var records = new List<MapMilitaryUpkeepRecord>(_units.Count);
            for (int i = 0; i < _units.Count; i++)
            {
                MapUnitState unit = _units[i];
                if (unit.Soldiers > 0)
                    records.Add(MapCommanderUpkeepRules.Calculate(unit));
            }
            return records;
        }

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

                decimal roleOutputMultiplier =
                    MapCastleRules.GetMineOutputMultiplier(
                        warehouseCastle.Role);

                if (mine.Kind == MineKind.Gold)
                {
                    decimal cashAmount =
                        _tuning.GoldMineCashPerDay * mine.YieldMultiplier *
                        roleOutputMultiplier;
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
                        _tuning.NormalMineIronPerDay * mine.YieldMultiplier *
                        roleOutputMultiplier;
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
            decimal roleSpeedMultiplier =
                MapCastleRules.GetTransportSpeedMultiplier(source.Role);
            decimal dailyRange = vehicleCount <= 0
                ? 0.5m
                : Math.Min(4m, 1m + (decimal)Math.Sqrt(vehicleCount));
            dailyRange *= roleSpeedMultiplier;
            int travelDays = route.Count == 0
                ? 0
                : Math.Max(1, (int)Math.Ceiling(terrainWeight / dailyRange));
            decimal roleCostMultiplier =
                MapCastleRules.GetTransportCostMultiplier(source.Role);
            decimal cost = Math.Round(
                amount * 0.20m + terrainWeight * 5m /
                Math.Max(1m, 1m + vehicleCount * 0.15m),
                2,
                MidpointRounding.AwayFromZero) * roleCostMultiplier;
            cost = Math.Round(
                cost,
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
                case MapSupplyKind.Horse: return 100m;
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
                    FindEconomicSurvey(candidate) != null ||
                    FindMineConstruction(candidate) != null ||
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
                if (FindFirstOwnedUnit(factionId) == null)
                {
                    if (!TryCreateUnit(factionId, out _, out _))
                        continue;
                    changed = true;
                }
                changed |= AssignAvailableCommandersToAiFaction(factionId);
                for (int unitIndex = 0; unitIndex < _units.Count; unitIndex++)
                {
                    MapUnitState unit = _units[unitIndex];
                    if (!string.Equals(
                            unit.OwnerFactionId,
                            factionId,
                            StringComparison.Ordinal) ||
                        unit.IsMoving ||
                        IsStandingOnCapturableObjective(unit))
                        continue;

                    GridCoordinate? previousTarget =
                        _aiLongTermTargets.TryGetValue(
                            factionId,
                            out GridCoordinate savedTarget)
                            ? savedTarget
                            : (GridCoordinate?)null;
                    if (!FactionStrategicAi.TryChooseObjective(
                            _layout,
                            _mines,
                            _castles,
                            _units,
                            factionId,
                            unit,
                            _aiStrategies[factionId],
                            previousTarget,
                            out StrategicObjective objective))
                        continue;

                    _aiLongTermTargets[factionId] = objective.Coordinate;
                    bool issued = objective.Kind ==
                            StrategicObjectiveKind.Castle
                        ? TryIssueCastleOccupation(
                            factionId,
                            unit.Id,
                            objective.Coordinate,
                            out _)
                        : TryIssueMove(
                            factionId,
                            unit.Id,
                            objective.Coordinate,
                            out _);
                    changed |= issued;
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

        private void ResolveDecisiveBattleCommanderOutcome(
            string winningFactionId,
            string defeatedFactionId,
            GridCoordinate coordinate,
            IReadOnlyList<MapUnitState> defeatedCommanderUnits)
        {
            int generationRoll = CreateBattleOutcomeRoll(
                "victory_generation",
                winningFactionId,
                coordinate,
                string.Empty);
            if (MapCommanderBattleRules.ShouldGenerateCommanderAfterVictory(
                    generationRoll))
            {
                MapCommanderState generated = GenerateVictoryCommander(
                    winningFactionId,
                    coordinate);
                _commanders.Add(generated);
                CommanderGenerated?.Invoke(new MapCommanderGeneratedRecord(
                    generated,
                    winningFactionId,
                    coordinate,
                    _economicDaySequence));
            }

            if (defeatedCommanderUnits == null)
                return;

            for (int i = 0; i < defeatedCommanderUnits.Count; i++)
            {
                MapUnitState defeated = defeatedCommanderUnits[i];
                if (defeated == null || defeated.RequiredHorseCount <= 0)
                    continue;

                int lossRoll = CreateBattleOutcomeRoll(
                    "defeat_horse_loss",
                    defeatedFactionId,
                    coordinate,
                    defeated.Id);
                decimal lossRate = 0.05m +
                    (lossRoll % 1001) / 10000m;
                defeated.LoseHorses(lossRate);
            }

            for (int i = 0; i < defeatedCommanderUnits.Count; i++)
            {
                MapUnitState unit = defeatedCommanderUnits[i];
                MapCommanderState commander = unit?.Commander;
                if (commander == null)
                    continue;

                int deathRoll = CreateBattleOutcomeRoll(
                    "defeat_death",
                    defeatedFactionId,
                    coordinate,
                    commander.Id);
                if (!MapCommanderBattleRules.ShouldCommanderDieAfterDefeat(
                        commander,
                        deathRoll))
                {
                    continue;
                }

                string commanderId = commander.Id;
                string commanderName = commander.DisplayName;
                string unitId = unit.Id;
                if (!commander.MarkKilled())
                    continue;

                unit.AssignCommander(null);
                CommanderDied?.Invoke(new MapCommanderDeathRecord(
                    commanderId,
                    commanderName,
                    defeatedFactionId,
                    unitId,
                    coordinate,
                    _economicDaySequence));
            }
        }

        private bool AdvanceWorldMissions()
        {
            if (_worldMissionsByUnitId.Count == 0)
                return false;

            bool changed = false;
            var missions = new List<MapWorldMissionState>(
                _worldMissionsByUnitId.Values);
            for (int i = 0; i < missions.Count; i++)
            {
                MapWorldMissionState mission = missions[i];
                if (mission.Status != MapWorldMissionStatus.EnRoute)
                    continue;
                MapUnitState unit = FindUnit(mission.UnitId);
                if (unit == null || unit.IsMoving ||
                    !unit.Coordinate.Equals(mission.Target))
                    continue;

                if (mission.Action == MapWorldMissionAction.Occupy)
                {
                    MapCastleControlState castle = FindCastle(mission.Target);
                    MapMineControlState mine = FindMine(mission.Target);
                    bool controlled = castle != null
                        ? string.Equals(
                            castle.OwnerFactionId,
                            unit.OwnerFactionId,
                            StringComparison.Ordinal)
                        : mine == null || string.Equals(
                            mine.OwnerFactionId,
                            unit.OwnerFactionId,
                            StringComparison.Ordinal);
                    if (!controlled)
                        continue;
                }

                if (mission.Action == MapWorldMissionAction.Escort)
                    unit.AssignSupplyMission(
                        MapSupplyMissionKind.Escort,
                        mission.Target);
                else if (mission.Action == MapWorldMissionAction.Raid)
                    unit.AssignSupplyMission(
                        MapSupplyMissionKind.Raid,
                        mission.Target);
                else if (mission.Action == MapWorldMissionAction.Deliver ||
                         mission.Action == MapWorldMissionAction.Smuggle)
                {
                    if (mission.LoadedCargo < mission.RequiredCargo)
                        continue;
                    mission.DeliveredCargo = mission.LoadedCargo;
                    mission.LoadedCargo = 0m;
                }
                else if (mission.Action == MapWorldMissionAction.Sabotage)
                {
                    MapCastleControlState targetCastle =
                        FindCastle(mission.Target);
                    if (targetCastle == null || targetCastle.IsDestroyed ||
                        string.Equals(
                            targetCastle.OwnerFactionId,
                            unit.OwnerFactionId,
                            StringComparison.Ordinal))
                        continue;
                    mission.SabotageDamage = targetCastle.ApplyWallDamage(
                        Math.Max(25, unit.Soldiers / 2));
                    unit.AdjustFatigue(8m);
                }

                mission.Status = MapWorldMissionStatus.Performing;
                WorldMissionReady?.Invoke(mission);
                changed = true;
            }
            return changed;
        }

        private void ReturnWorldMissionCargo(MapWorldMissionState mission)
        {
            if (mission == null || !mission.CargoKind.HasValue ||
                mission.LoadedCargo <= 0m || !mission.CargoSource.HasValue)
                return;
            MapCastleControlState source = FindCastle(
                mission.CargoSource.Value);
            if (source != null && !source.IsDestroyed)
                source.StoreWarehouseSupply(
                    mission.CargoKind.Value,
                    mission.LoadedCargo);
            mission.LoadedCargo = 0m;
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

        private List<GridCoordinate> FindShortestFriendlyPortSeaPath(
            string ownerFactionId,
            GridCoordinate origin,
            GridCoordinate destination)
        {
            if (!IsFriendlyPort(ownerFactionId, origin) ||
                !IsFriendlyPort(ownerFactionId, destination))
            {
                return new List<GridCoordinate>();
            }

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

                    bool canEnter = !_layout.IsLand(neighbor) ||
                        neighbor.Equals(destination);
                    if (!canEnter || !visited.Add(neighbor))
                        continue;

                    previous.Add(neighbor, current);
                    if (neighbor.Equals(destination))
                        return ReconstructPath(origin, destination, previous);
                    frontier.Enqueue(neighbor);
                }
            }

            return new List<GridCoordinate>();
        }

        private bool IsFriendlyPort(
            string ownerFactionId,
            GridCoordinate coordinate)
        {
            MapCastleControlState castle = FindCastle(coordinate);
            return castle != null &&
                string.Equals(
                    castle.OwnerFactionId,
                    ownerFactionId,
                    StringComparison.Ordinal) &&
                IsCoastalPort(coordinate);
        }

        private bool ContainsOceanTile(IReadOnlyList<GridCoordinate> path)
        {
            if (path == null)
                return false;
            for (int i = 0; i < path.Count; i++)
            {
                if (!_layout.IsLand(path[i]))
                    return true;
            }

            return false;
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
