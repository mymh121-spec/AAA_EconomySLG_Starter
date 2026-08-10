using System;
using System.Collections.Generic;
using Game.Domain.Common;

namespace Game.Domain.Military
{
    public enum UnitArchetype
    {
        Swordsman,
        Spearman,
        Maceman,
        Archer,
        Slinger,
        Cavalry
    }

    public enum ArmorClass
    {
        Unarmored,
        Light,
        Heavy
    }

    public enum ExperienceTier
    {
        Recruit,
        Trained,
        Veteran,
        Elite
    }

    public enum BattlePhase
    {
        RangedApproach,
        Charge,
        Melee,
        Finished
    }

    public readonly struct DamageProfile
    {
        public decimal Slash { get; }
        public decimal Pierce { get; }
        public decimal Blunt { get; }

        public DamageProfile(
            decimal slash,
            decimal pierce,
            decimal blunt)
        {
            Slash = Math.Max(0m, slash);
            Pierce = Math.Max(0m, pierce);
            Blunt = Math.Max(0m, blunt);
        }

        public decimal Total => Slash + Pierce + Blunt;

        public decimal ResolveAgainst(ArmorProfile armor)
        {
            return
                Slash * (1m - armor.SlashReduction) +
                Pierce * (1m - armor.PierceReduction) +
                Blunt * (1m - armor.BluntReduction);
        }
    }

    public readonly struct ArmorProfile
    {
        public ArmorClass Class { get; }
        public decimal SlashReduction { get; }
        public decimal PierceReduction { get; }
        public decimal BluntReduction { get; }
        public decimal Weight { get; }

        public ArmorProfile(
            ArmorClass armorClass,
            decimal slashReduction,
            decimal pierceReduction,
            decimal bluntReduction,
            decimal weight)
        {
            Class = armorClass;
            SlashReduction = Math.Clamp(slashReduction, 0m, 0.90m);
            PierceReduction = Math.Clamp(pierceReduction, 0m, 0.90m);
            BluntReduction = Math.Clamp(bluntReduction, 0m, 0.90m);
            Weight = Math.Max(0m, weight);
        }

        public static ArmorProfile Unarmored => new ArmorProfile(
            ArmorClass.Unarmored, 0.03m, 0.02m, 0.01m, 0m);

        public static ArmorProfile Light => new ArmorProfile(
            ArmorClass.Light, 0.25m, 0.18m, 0.08m, 0.18m);

        public static ArmorProfile Heavy => new ArmorProfile(
            ArmorClass.Heavy, 0.58m, 0.43m, 0.16m, 0.38m);
    }

    public sealed class EquipmentLoadout
    {
        public string Id { get; }
        public string DisplayName { get; }
        public ArmorProfile Armor { get; }
        public decimal AttackModifier { get; }
        public decimal MobilityModifier { get; }
        public decimal ChargeModifier { get; }
        public decimal UpkeepModifier { get; }

        public EquipmentLoadout(
            string id,
            string displayName,
            ArmorProfile armor,
            decimal attackModifier = 1m,
            decimal mobilityModifier = 1m,
            decimal chargeModifier = 1m,
            decimal upkeepModifier = 1m)
        {
            Id = string.IsNullOrWhiteSpace(id) ? "basic" : id.Trim();
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? Id
                : displayName.Trim();
            Armor = armor;
            AttackModifier = Math.Clamp(attackModifier, 0.25m, 3m);
            MobilityModifier = Math.Clamp(mobilityModifier, 0.25m, 2m);
            ChargeModifier = Math.Clamp(chargeModifier, 0.25m, 3m);
            UpkeepModifier = Math.Clamp(upkeepModifier, 0.25m, 5m);
        }
    }

    public sealed class UnitArchetypeDefinition
    {
        public UnitArchetype Archetype { get; }
        public string DisplayName { get; }
        public decimal BaseAttack { get; }
        public decimal BaseDefense { get; }
        public decimal Mobility { get; }
        public decimal Morale { get; }
        public int RangedApproachAttacks { get; }
        public decimal RangedAccuracy { get; }
        public decimal MeleePenalty { get; }
        public decimal ChargePower { get; }
        public decimal AntiCavalry { get; }
        public decimal FormationPressure { get; }
        public decimal FormationReliance { get; }
        public decimal BaseDailyUpkeep { get; }
        public DamageProfile RangedDamage { get; }
        public DamageProfile MeleeDamage { get; }
        public bool IsRanged => RangedApproachAttacks > 0;

        public UnitArchetypeDefinition(
            UnitArchetype archetype,
            string displayName,
            decimal baseAttack,
            decimal baseDefense,
            decimal mobility,
            decimal morale,
            int rangedApproachAttacks,
            decimal rangedAccuracy,
            decimal meleePenalty,
            decimal chargePower,
            decimal antiCavalry,
            decimal formationPressure,
            decimal formationReliance,
            decimal baseDailyUpkeep,
            DamageProfile rangedDamage,
            DamageProfile meleeDamage)
        {
            Archetype = archetype;
            DisplayName = string.IsNullOrWhiteSpace(displayName)
                ? archetype.ToString()
                : displayName.Trim();
            BaseAttack = Math.Max(0.01m, baseAttack);
            BaseDefense = Math.Max(0.01m, baseDefense);
            Mobility = Math.Max(0.01m, mobility);
            Morale = Math.Max(0.01m, morale);
            RangedApproachAttacks = Math.Max(0, rangedApproachAttacks);
            RangedAccuracy = Math.Clamp(rangedAccuracy, 0m, 1m);
            MeleePenalty = Math.Clamp(meleePenalty, 0.05m, 1m);
            ChargePower = Math.Max(0m, chargePower);
            AntiCavalry = Math.Max(0m, antiCavalry);
            FormationPressure = Math.Max(0m, formationPressure);
            FormationReliance = Math.Max(0m, formationReliance);
            BaseDailyUpkeep = Math.Max(0m, baseDailyUpkeep);
            RangedDamage = rangedDamage;
            MeleeDamage = meleeDamage;
        }
    }

    public sealed class MilitaryBalanceCatalog
    {
        private readonly Dictionary<UnitArchetype, UnitArchetypeDefinition>
            _definitions =
                new Dictionary<UnitArchetype, UnitArchetypeDefinition>();

        public void Register(UnitArchetypeDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException(nameof(definition));

            _definitions[definition.Archetype] = definition;
        }

        public UnitArchetypeDefinition Get(UnitArchetype archetype)
        {
            if (!_definitions.TryGetValue(archetype, out var definition))
            {
                throw new KeyNotFoundException(
                    $"병종 데이터가 없습니다: {archetype}");
            }

            return definition;
        }

        public static MilitaryBalanceCatalog CreatePrototypeDefaults()
        {
            var catalog = new MilitaryBalanceCatalog();
            catalog.Register(new UnitArchetypeDefinition(
                UnitArchetype.Swordsman, "검병",
                1.00m, 1.00m, 1.00m, 1.00m,
                0, 0m, 1m, 0.05m, 0m, 0.22m, 0.10m, 1.0m,
                new DamageProfile(0m, 0m, 0m),
                new DamageProfile(0.72m, 0.22m, 0.06m)));
            catalog.Register(new UnitArchetypeDefinition(
                UnitArchetype.Spearman, "창병",
                0.90m, 1.08m, 0.88m, 1.05m,
                0, 0m, 1m, 0.02m, 0.75m, 0.02m, 0.48m, 0.95m,
                new DamageProfile(0m, 0m, 0m),
                new DamageProfile(0.12m, 0.84m, 0.04m)));
            catalog.Register(new UnitArchetypeDefinition(
                UnitArchetype.Maceman, "둔기병",
                0.92m, 0.98m, 0.82m, 0.96m,
                0, 0m, 1m, 0.04m, 0m, 0.08m, 0.08m, 1.08m,
                new DamageProfile(0m, 0m, 0m),
                new DamageProfile(0.08m, 0.10m, 0.82m)));
            catalog.Register(new UnitArchetypeDefinition(
                UnitArchetype.Archer, "궁병",
                0.90m, 0.58m, 0.92m, 0.82m,
                2, 0.72m, 0.32m, 0m, 0m, 0m, 0m, 1.10m,
                new DamageProfile(0.08m, 0.88m, 0.04m),
                new DamageProfile(0.48m, 0.42m, 0.10m)));
            catalog.Register(new UnitArchetypeDefinition(
                UnitArchetype.Slinger, "돌팔매병",
                0.82m, 0.55m, 0.98m, 0.80m,
                1, 0.62m, 0.38m, 0m, 0m, 0m, 0m, 0.82m,
                new DamageProfile(0.04m, 0.08m, 0.88m),
                new DamageProfile(0.12m, 0.08m, 0.80m)));
            catalog.Register(new UnitArchetypeDefinition(
                UnitArchetype.Cavalry, "기마병",
                1.04m, 0.90m, 1.75m, 1.05m,
                0, 0m, 1m, 0.90m, 0m, 0.12m, 0.04m, 2.10m,
                new DamageProfile(0m, 0m, 0m),
                new DamageProfile(0.48m, 0.38m, 0.14m)));
            return catalog;
        }
    }

    public static class UnitExperienceModel
    {
        public static ExperienceTier GetTier(decimal averageExperience)
        {
            if (averageExperience >= 120m) return ExperienceTier.Elite;
            if (averageExperience >= 60m) return ExperienceTier.Veteran;
            if (averageExperience >= 20m) return ExperienceTier.Trained;
            return ExperienceTier.Recruit;
        }

        public static decimal AccuracyFactor(ExperienceTier tier) =>
            1m + (int)tier * 0.08m;

        public static decimal MoraleResistance(ExperienceTier tier) =>
            1m + (int)tier * 0.10m;

        public static decimal FormationFactor(ExperienceTier tier) =>
            1m + (int)tier * 0.07m;

        public static decimal CommandResponse(ExperienceTier tier) =>
            1m + (int)tier * 0.05m;
    }

    public sealed class MilitaryUnit
    {
        public string Id { get; }
        public string FactionId { get; }
        public UnitArchetypeDefinition Definition { get; }
        public EquipmentLoadout Equipment { get; private set; }
        public int Soldiers { get; private set; }
        public decimal TotalExperience { get; private set; }
        public decimal Morale { get; private set; }
        public decimal SupplyRatio { get; private set; }
        public decimal AverageExperience => Soldiers <= 0
            ? 0m
            : TotalExperience / Soldiers;
        public ExperienceTier ExperienceTier =>
            UnitExperienceModel.GetTier(AverageExperience);
        public bool IsDestroyed => Soldiers <= 0;

        public MilitaryUnit(
            string id,
            string factionId,
            UnitArchetypeDefinition definition,
            EquipmentLoadout equipment,
            int soldiers,
            decimal averageExperience = 0m,
            decimal morale = 1m,
            decimal supplyRatio = 1m)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("부대 ID가 필요합니다.")
                : id.Trim();
            FactionId = factionId ?? string.Empty;
            Definition = definition ??
                throw new ArgumentNullException(nameof(definition));
            Equipment = equipment ??
                throw new ArgumentNullException(nameof(equipment));
            Soldiers = Math.Max(0, soldiers);
            TotalExperience = Math.Max(0m, averageExperience) * Soldiers;
            Morale = Math.Clamp(morale, 0m, 1.5m);
            SupplyRatio = Math.Clamp(supplyRatio, 0m, 1m);
        }

        public void Recruit(int recruits)
        {
            Soldiers += Math.Max(0, recruits);
        }

        public void AddExperience(decimal experiencePerSurvivor)
        {
            TotalExperience += Math.Max(0m, experiencePerSurvivor) *
                Soldiers;
        }

        public int ApplyCasualties(int casualties)
        {
            int applied = Math.Min(Soldiers, Math.Max(0, casualties));
            if (applied == 0)
                return 0;

            decimal average = AverageExperience;
            Soldiers -= applied;
            TotalExperience = average * Soldiers;
            Morale = Math.Max(0m, Morale - applied * 0.0025m);
            return applied;
        }

        public void SetSupplyRatio(decimal ratio)
        {
            SupplyRatio = Math.Clamp(ratio, 0m, 1m);
        }

        public void SetEquipment(EquipmentLoadout equipment)
        {
            Equipment = equipment ??
                throw new ArgumentNullException(nameof(equipment));
        }
    }

    public sealed class ArmyState
    {
        private readonly List<MilitaryUnit> _units =
            new List<MilitaryUnit>(8);

        public string Id { get; }
        public string FactionId { get; }
        public RegionId RegionId { get; private set; }
        public int DistanceFromHome { get; private set; }
        public bool IsMobilized { get; private set; }
        public IReadOnlyList<MilitaryUnit> Units => _units;
        public int TotalSoldiers
        {
            get
            {
                int total = 0;
                for (int i = 0; i < _units.Count; i++)
                    total += _units[i].Soldiers;
                return total;
            }
        }

        public ArmyState(
            string id,
            string factionId,
            RegionId regionId)
        {
            Id = id ?? string.Empty;
            FactionId = factionId ?? string.Empty;
            RegionId = regionId;
        }

        public void AddUnit(MilitaryUnit unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));
            if (!string.Equals(
                unit.FactionId,
                FactionId,
                StringComparison.Ordinal))
            {
                throw new ArgumentException("군대와 부대의 세력이 다릅니다.");
            }

            _units.Add(unit);
        }

        public void Deploy(RegionId regionId, int distanceFromHome)
        {
            RegionId = regionId;
            DistanceFromHome = Math.Max(0, distanceFromHome);
            IsMobilized = true;
        }

        public void StandDown()
        {
            DistanceFromHome = 0;
            IsMobilized = false;
        }
    }

    public sealed class MilitaryLogisticsTuning
    {
        public decimal SoldierDailyWage { get; }
        public decimal MobilizationCostPerSoldier { get; }
        public decimal DistanceSupplyCostPerSoldier { get; }
        public decimal FoodDemandPerSoldier { get; }
        public decimal EquipmentDemandPerSoldier { get; }
        public decimal MedicineDemandPerSoldier { get; }

        public MilitaryLogisticsTuning(
            decimal soldierDailyWage = 0.8m,
            decimal mobilizationCostPerSoldier = 0.5m,
            decimal distanceSupplyCostPerSoldier = 0.02m,
            decimal foodDemandPerSoldier = 0.03m,
            decimal equipmentDemandPerSoldier = 0.004m,
            decimal medicineDemandPerSoldier = 0.001m)
        {
            SoldierDailyWage = Math.Max(0m, soldierDailyWage);
            MobilizationCostPerSoldier = Math.Max(
                0m,
                mobilizationCostPerSoldier);
            DistanceSupplyCostPerSoldier = Math.Max(
                0m,
                distanceSupplyCostPerSoldier);
            FoodDemandPerSoldier = Math.Max(0m, foodDemandPerSoldier);
            EquipmentDemandPerSoldier = Math.Max(
                0m,
                equipmentDemandPerSoldier);
            MedicineDemandPerSoldier = Math.Max(
                0m,
                medicineDemandPerSoldier);
        }

        public decimal GetReadiness(decimal supplyRatio)
        {
            if (supplyRatio >= 1m) return 1m;
            if (supplyRatio >= 0.75m) return 0.90m;
            if (supplyRatio >= 0.50m) return 0.75m;
            if (supplyRatio >= 0.25m) return 0.55m;
            return 0.35m;
        }

        public decimal CalculateDailyUpkeep(ArmyState army)
        {
            if (army == null)
                throw new ArgumentNullException(nameof(army));

            decimal total = 0m;
            for (int i = 0; i < army.Units.Count; i++)
            {
                MilitaryUnit unit = army.Units[i];
                decimal perSoldier = SoldierDailyWage +
                    unit.Definition.BaseDailyUpkeep *
                    unit.Equipment.UpkeepModifier;
                if (army.IsMobilized)
                {
                    perSoldier += MobilizationCostPerSoldier +
                        army.DistanceFromHome *
                        DistanceSupplyCostPerSoldier;
                }

                total += unit.Soldiers * perSoldier;
            }

            return total;
        }

        public decimal GetRecruitmentSpeed(decimal supplyRatio) =>
            0.45m + 0.55m * GetReadiness(supplyRatio);

        public decimal GetReplacementSpeed(decimal supplyRatio) =>
            0.30m + 0.70m * GetReadiness(supplyRatio);
    }

    public readonly struct BattlePhaseRecord
    {
        public BattlePhase Phase { get; }
        public int Round { get; }
        public int AttackerCasualties { get; }
        public int DefenderCasualties { get; }

        public BattlePhaseRecord(
            BattlePhase phase,
            int round,
            int attackerCasualties,
            int defenderCasualties)
        {
            Phase = phase;
            Round = round;
            AttackerCasualties = Math.Max(0, attackerCasualties);
            DefenderCasualties = Math.Max(0, defenderCasualties);
        }
    }

    public sealed class BattleReport
    {
        public string WinnerFactionId { get; }
        public IReadOnlyList<BattlePhaseRecord> Phases { get; }
        public int AttackerRemaining { get; }
        public int DefenderRemaining { get; }

        public BattleReport(
            string winnerFactionId,
            IReadOnlyList<BattlePhaseRecord> phases,
            int attackerRemaining,
            int defenderRemaining)
        {
            WinnerFactionId = winnerFactionId ?? string.Empty;
            Phases = phases ?? Array.Empty<BattlePhaseRecord>();
            AttackerRemaining = Math.Max(0, attackerRemaining);
            DefenderRemaining = Math.Max(0, defenderRemaining);
        }
    }

    public sealed class BattleResolver
    {
        private readonly MilitaryLogisticsTuning _logistics;
        private readonly decimal _damagePerCasualty;
        private readonly int _maxMeleeRounds;

        public BattleResolver(
            MilitaryLogisticsTuning logistics,
            decimal damagePerCasualty = 18m,
            int maxMeleeRounds = 8)
        {
            _logistics = logistics ??
                throw new ArgumentNullException(nameof(logistics));
            _damagePerCasualty = Math.Max(1m, damagePerCasualty);
            _maxMeleeRounds = Math.Max(1, maxMeleeRounds);
        }

        public BattleReport Resolve(
            ArmyState attacker,
            ArmyState defender,
            int seed)
        {
            if (attacker == null)
                throw new ArgumentNullException(nameof(attacker));
            if (defender == null)
                throw new ArgumentNullException(nameof(defender));

            var random = new Random(seed);
            var phases = new List<BattlePhaseRecord>(12);
            int approachRounds = Math.Max(
                GetRangedApproachRounds(attacker),
                GetRangedApproachRounds(defender));
            if (HasLivingCavalry(attacker) || HasLivingCavalry(defender))
                approachRounds = Math.Max(1, approachRounds - 1);

            for (int round = 1; round <= approachRounds; round++)
            {
                int attackerLosses = CalculateArmyAttack(
                    defender,
                    attacker,
                    true,
                    false,
                    random);
                int defenderLosses = CalculateArmyAttack(
                    attacker,
                    defender,
                    true,
                    false,
                    random);
                ApplyArmyCasualties(attacker, attackerLosses);
                ApplyArmyCasualties(defender, defenderLosses);
                phases.Add(new BattlePhaseRecord(
                    BattlePhase.RangedApproach,
                    round,
                    attackerLosses,
                    defenderLosses));
                if (attacker.TotalSoldiers == 0 || defender.TotalSoldiers == 0)
                    break;
            }

            if (attacker.TotalSoldiers > 0 && defender.TotalSoldiers > 0 &&
                (HasLivingCavalry(attacker) || HasLivingCavalry(defender)))
            {
                int attackerLosses = CalculateArmyAttack(
                    defender,
                    attacker,
                    false,
                    true,
                    random);
                int defenderLosses = CalculateArmyAttack(
                    attacker,
                    defender,
                    false,
                    true,
                    random);
                ApplyArmyCasualties(attacker, attackerLosses);
                ApplyArmyCasualties(defender, defenderLosses);
                phases.Add(new BattlePhaseRecord(
                    BattlePhase.Charge,
                    1,
                    attackerLosses,
                    defenderLosses));
            }

            for (int round = 1;
                 round <= _maxMeleeRounds &&
                 attacker.TotalSoldiers > 0 &&
                 defender.TotalSoldiers > 0;
                 round++)
            {
                int attackerLosses = CalculateArmyAttack(
                    defender,
                    attacker,
                    false,
                    false,
                    random);
                int defenderLosses = CalculateArmyAttack(
                    attacker,
                    defender,
                    false,
                    false,
                    random);
                ApplyArmyCasualties(attacker, attackerLosses);
                ApplyArmyCasualties(defender, defenderLosses);
                phases.Add(new BattlePhaseRecord(
                    BattlePhase.Melee,
                    round,
                    attackerLosses,
                    defenderLosses));
            }

            string winner = attacker.TotalSoldiers == defender.TotalSoldiers
                ? string.Empty
                : attacker.TotalSoldiers > defender.TotalSoldiers
                    ? attacker.FactionId
                    : defender.FactionId;

            for (int i = 0; i < attacker.Units.Count; i++)
                attacker.Units[i].AddExperience(6m);
            for (int i = 0; i < defender.Units.Count; i++)
                defender.Units[i].AddExperience(6m);

            phases.Add(new BattlePhaseRecord(
                BattlePhase.Finished,
                phases.Count + 1,
                0,
                0));
            return new BattleReport(
                winner,
                phases,
                attacker.TotalSoldiers,
                defender.TotalSoldiers);
        }

        private int CalculateArmyAttack(
            ArmyState source,
            ArmyState target,
            bool rangedOnly,
            bool charge,
            Random random)
        {
            decimal damage = 0m;
            for (int i = 0; i < source.Units.Count; i++)
            {
                MilitaryUnit unit = source.Units[i];
                if (unit.IsDestroyed)
                    continue;
                if (rangedOnly && !unit.Definition.IsRanged)
                    continue;

                MilitaryUnit targetUnit = FindTarget(target);
                if (targetUnit == null)
                    break;

                DamageProfile profile = rangedOnly
                    ? unit.Definition.RangedDamage
                    : unit.Definition.MeleeDamage;
                decimal roleFactor = 1m;
                if (!rangedOnly && unit.Definition.IsRanged)
                    roleFactor *= unit.Definition.MeleePenalty;
                if (charge && unit.Definition.Archetype == UnitArchetype.Cavalry)
                {
                    roleFactor *= 1m +
                        unit.Definition.ChargePower *
                        unit.Equipment.ChargeModifier;
                }
                if (!rangedOnly &&
                    targetUnit.Definition.Archetype == UnitArchetype.Cavalry)
                {
                    roleFactor *= 1m + unit.Definition.AntiCavalry;
                }

                roleFactor *= 1m +
                    unit.Definition.FormationPressure *
                    targetUnit.Definition.FormationReliance;
                decimal experience = rangedOnly
                    ? UnitExperienceModel.AccuracyFactor(unit.ExperienceTier)
                    : UnitExperienceModel.FormationFactor(unit.ExperienceTier);
                decimal supply = _logistics.GetReadiness(unit.SupplyRatio);
                decimal accuracy = rangedOnly
                    ? unit.Definition.RangedAccuracy
                    : 0.78m;
                decimal variation = 0.90m +
                    (decimal)random.NextDouble() * 0.20m;
                decimal channelDamage = profile.ResolveAgainst(
                    targetUnit.Equipment.Armor);
                decimal defense = Math.Max(
                    0.35m,
                    targetUnit.Definition.BaseDefense *
                    UnitExperienceModel.FormationFactor(
                        targetUnit.ExperienceTier));

                damage += unit.Soldiers *
                    unit.Definition.BaseAttack *
                    unit.Equipment.AttackModifier *
                    experience *
                    supply *
                    Math.Max(0.1m, unit.Morale) *
                    accuracy *
                    roleFactor *
                    variation *
                    channelDamage /
                    defense;
            }

            return Math.Max(
                0,
                (int)decimal.Floor(damage / _damagePerCasualty));
        }

        private static int GetRangedApproachRounds(ArmyState army)
        {
            int rounds = 0;
            for (int i = 0; i < army.Units.Count; i++)
            {
                if (!army.Units[i].IsDestroyed)
                {
                    rounds = Math.Max(
                        rounds,
                        army.Units[i].Definition.RangedApproachAttacks);
                }
            }

            return rounds;
        }

        private static bool HasLivingCavalry(ArmyState army)
        {
            for (int i = 0; i < army.Units.Count; i++)
            {
                if (!army.Units[i].IsDestroyed &&
                    army.Units[i].Definition.Archetype ==
                    UnitArchetype.Cavalry)
                {
                    return true;
                }
            }

            return false;
        }

        private static MilitaryUnit FindTarget(ArmyState army)
        {
            MilitaryUnit target = null;
            for (int i = 0; i < army.Units.Count; i++)
            {
                MilitaryUnit candidate = army.Units[i];
                if (candidate.IsDestroyed)
                    continue;
                if (target == null || candidate.Soldiers > target.Soldiers)
                    target = candidate;
            }

            return target;
        }

        private static void ApplyArmyCasualties(
            ArmyState army,
            int casualties)
        {
            int remaining = Math.Max(0, casualties);
            while (remaining > 0)
            {
                MilitaryUnit target = FindTarget(army);
                if (target == null)
                    return;
                remaining -= target.ApplyCasualties(remaining);
            }
        }
    }
}
