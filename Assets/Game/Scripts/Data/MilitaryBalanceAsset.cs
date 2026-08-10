using System;
using UnityEngine;
using Game.Domain.Military;

namespace Game.Data
{
    [CreateAssetMenu(
        fileName = "MilitaryBalance",
        menuName = "게임/설정/병종 밸런스")]
    public sealed class MilitaryBalanceAsset : ScriptableObject
    {
        [Serializable]
        private sealed class UnitRecord
        {
            public UnitArchetype archetype;
            public string displayName;
            [Min(0.01f)] public float attack = 1f;
            [Min(0.01f)] public float defense = 1f;
            [Min(0.01f)] public float mobility = 1f;
            [Min(0.01f)] public float morale = 1f;
            [Min(0)] public int rangedApproachAttacks;
            [Range(0f, 1f)] public float rangedAccuracy;
            [Range(0.05f, 1f)] public float meleePenalty = 1f;
            [Min(0f)] public float chargePower;
            [Min(0f)] public float antiCavalry;
            [Min(0f)] public float formationPressure;
            [Min(0f)] public float formationReliance;
            [Min(0f)] public float dailyUpkeep = 1f;
            [Header("원거리 피해")]
            [Min(0f)] public float rangedSlash;
            [Min(0f)] public float rangedPierce;
            [Min(0f)] public float rangedBlunt;
            [Header("근접 피해")]
            [Min(0f)] public float meleeSlash;
            [Min(0f)] public float meleePierce;
            [Min(0f)] public float meleeBlunt;

            public UnitArchetypeDefinition ToDomain()
            {
                return new UnitArchetypeDefinition(
                    archetype,
                    displayName,
                    (decimal)attack,
                    (decimal)defense,
                    (decimal)mobility,
                    (decimal)morale,
                    rangedApproachAttacks,
                    (decimal)rangedAccuracy,
                    (decimal)meleePenalty,
                    (decimal)chargePower,
                    (decimal)antiCavalry,
                    (decimal)formationPressure,
                    (decimal)formationReliance,
                    (decimal)dailyUpkeep,
                    new DamageProfile(
                        (decimal)rangedSlash,
                        (decimal)rangedPierce,
                        (decimal)rangedBlunt),
                    new DamageProfile(
                        (decimal)meleeSlash,
                        (decimal)meleePierce,
                        (decimal)meleeBlunt));
            }
        }

        [SerializeField] private UnitRecord[] units = CreateDefaults();

        [ContextMenu("프로토타입 기본값으로 초기화")]
        private void ResetToPrototypeDefaults()
        {
            units = CreateDefaults();
        }

        public MilitaryBalanceCatalog ToDomain()
        {
            // 누락된 병종은 프로토타입 기본값을 유지하고,
            // 에셋에 입력된 병종만 덮어쓴다.
            MilitaryBalanceCatalog catalog =
                MilitaryBalanceCatalog.CreatePrototypeDefaults();
            if (units == null)
                return catalog;

            for (int i = 0; i < units.Length; i++)
            {
                if (units[i] != null)
                    catalog.Register(units[i].ToDomain());
            }

            return catalog;
        }

        private static UnitRecord[] CreateDefaults()
        {
            MilitaryBalanceCatalog defaults =
                MilitaryBalanceCatalog.CreatePrototypeDefaults();
            UnitArchetype[] archetypes =
            {
                UnitArchetype.Swordsman,
                UnitArchetype.Spearman,
                UnitArchetype.Maceman,
                UnitArchetype.Archer,
                UnitArchetype.Slinger,
                UnitArchetype.Cavalry
            };
            var records = new UnitRecord[archetypes.Length];
            for (int i = 0; i < archetypes.Length; i++)
            {
                UnitArchetypeDefinition value = defaults.Get(archetypes[i]);
                records[i] = new UnitRecord
                {
                    archetype = value.Archetype,
                    displayName = value.DisplayName,
                    attack = (float)value.BaseAttack,
                    defense = (float)value.BaseDefense,
                    mobility = (float)value.Mobility,
                    morale = (float)value.Morale,
                    rangedApproachAttacks = value.RangedApproachAttacks,
                    rangedAccuracy = (float)value.RangedAccuracy,
                    meleePenalty = (float)value.MeleePenalty,
                    chargePower = (float)value.ChargePower,
                    antiCavalry = (float)value.AntiCavalry,
                    formationPressure = (float)value.FormationPressure,
                    formationReliance = (float)value.FormationReliance,
                    dailyUpkeep = (float)value.BaseDailyUpkeep,
                    rangedSlash = (float)value.RangedDamage.Slash,
                    rangedPierce = (float)value.RangedDamage.Pierce,
                    rangedBlunt = (float)value.RangedDamage.Blunt,
                    meleeSlash = (float)value.MeleeDamage.Slash,
                    meleePierce = (float)value.MeleeDamage.Pierce,
                    meleeBlunt = (float)value.MeleeDamage.Blunt
                };
            }

            return records;
        }
    }
}
