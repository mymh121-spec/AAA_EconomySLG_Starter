namespace Game.Domain.Military
{
    public enum UnitWeaponType
    {
        Sword,
        Spear,
        Mace,
        Bow,
        Sling,
        Lance
    }

    public enum EquipmentQuality
    {
        Worn,
        Standard,
        Fine,
        Masterwork
    }

    /// <summary>
    /// Prototype recruitment and equipment rules shared by the map UI and
    /// the authoritative gameplay service. Values can later move to assets.
    /// </summary>
    public static class UnitEquipmentCatalog
    {
        public static string GetQualityDisplayName(EquipmentQuality quality)
        {
            switch (quality)
            {
                case EquipmentQuality.Worn: return "낡음";
                case EquipmentQuality.Fine: return "고급";
                case EquipmentQuality.Masterwork: return "명품";
                default: return "표준";
            }
        }

        public static decimal GetQualityCombatModifier(
            EquipmentQuality quality)
        {
            switch (quality)
            {
                case EquipmentQuality.Worn: return 0.85m;
                case EquipmentQuality.Fine: return 1.10m;
                case EquipmentQuality.Masterwork: return 1.20m;
                default: return 1m;
            }
        }

        public static decimal GetMaximumDurability(
            EquipmentQuality quality)
        {
            switch (quality)
            {
                case EquipmentQuality.Worn: return 70m;
                case EquipmentQuality.Fine: return 120m;
                case EquipmentQuality.Masterwork: return 150m;
                default: return 100m;
            }
        }

        public static decimal GetDurabilityCombatModifier(
            decimal durability,
            EquipmentQuality quality)
        {
            decimal maximum = GetMaximumDurability(quality);
            decimal ratio = maximum <= 0m
                ? 0m
                : System.Math.Clamp(durability / maximum, 0m, 1m);
            return 0.40m + ratio * 0.60m;
        }

        public static int GetWeaponRequirement(int soldiers) =>
            System.Math.Max(0, soldiers);

        public static int GetArmorRequirement(
            ArmorClass armor,
            int soldiers) =>
            armor == ArmorClass.Unarmored
                ? 0
                : System.Math.Max(0, soldiers);

        public static UnitWeaponType GetDefaultWeapon(UnitArchetype archetype)
        {
            switch (archetype)
            {
                case UnitArchetype.Spearman: return UnitWeaponType.Spear;
                case UnitArchetype.Maceman: return UnitWeaponType.Mace;
                case UnitArchetype.Archer: return UnitWeaponType.Bow;
                case UnitArchetype.Slinger: return UnitWeaponType.Sling;
                case UnitArchetype.Cavalry: return UnitWeaponType.Lance;
                default: return UnitWeaponType.Sword;
            }
        }

        public static string GetWeaponDisplayName(UnitWeaponType weapon)
        {
            switch (weapon)
            {
                case UnitWeaponType.Sword: return "장검";
                case UnitWeaponType.Spear: return "장창";
                case UnitWeaponType.Mace: return "철퇴";
                case UnitWeaponType.Bow: return "장궁";
                case UnitWeaponType.Sling: return "투석구";
                case UnitWeaponType.Lance: return "기병창";
                default: return weapon.ToString();
            }
        }

        public static string GetArmorDisplayName(ArmorClass armor)
        {
            switch (armor)
            {
                case ArmorClass.Unarmored: return "평상복";
                case ArmorClass.Light: return "경갑";
                case ArmorClass.Heavy: return "중갑";
                default: return armor.ToString();
            }
        }

        public static decimal GetRecruitBaseCost(UnitArchetype archetype)
        {
            switch (archetype)
            {
                case UnitArchetype.Spearman: return 7500m;
                case UnitArchetype.Maceman: return 9000m;
                case UnitArchetype.Archer: return 9500m;
                case UnitArchetype.Slinger: return 6000m;
                case UnitArchetype.Cavalry: return 18000m;
                default: return 8000m;
            }
        }

        public static decimal GetWeaponCost(UnitWeaponType weapon)
        {
            switch (weapon)
            {
                case UnitWeaponType.Sword: return 2800m;
                case UnitWeaponType.Spear: return 2200m;
                case UnitWeaponType.Mace: return 3600m;
                case UnitWeaponType.Bow: return 4500m;
                case UnitWeaponType.Sling: return 1200m;
                case UnitWeaponType.Lance: return 6000m;
                default: return 0m;
            }
        }

        public static decimal GetArmorCost(ArmorClass armor)
        {
            switch (armor)
            {
                case ArmorClass.Light: return 4000m;
                case ArmorClass.Heavy: return 12000m;
                default: return 0m;
            }
        }

        public static decimal GetRecruitmentCost(
            UnitArchetype archetype,
            UnitWeaponType weapon,
            ArmorClass armor) =>
            GetRecruitBaseCost(archetype) +
            GetWeaponCost(weapon) +
            GetArmorCost(armor);

        public static decimal GetEquipmentCost(
            UnitWeaponType weapon,
            ArmorClass armor) =>
            GetWeaponCost(weapon) + GetArmorCost(armor);

        public static decimal GetAttackModifier(UnitWeaponType weapon)
        {
            switch (weapon)
            {
                case UnitWeaponType.Spear: return 1.06m;
                case UnitWeaponType.Mace: return 1.14m;
                case UnitWeaponType.Bow: return 1.08m;
                case UnitWeaponType.Sling: return 0.88m;
                case UnitWeaponType.Lance: return 1.24m;
                default: return 1.00m;
            }
        }

        public static decimal GetDefenseModifier(ArmorClass armor)
        {
            switch (armor)
            {
                case ArmorClass.Light: return 1.22m;
                case ArmorClass.Heavy: return 1.58m;
                default: return 0.92m;
            }
        }

        public static decimal GetMobilityModifier(ArmorClass armor)
        {
            switch (armor)
            {
                case ArmorClass.Light: return 0.92m;
                case ArmorClass.Heavy: return 0.72m;
                default: return 1.08m;
            }
        }

        public static decimal GetArchetypeMobilityModifier(
            UnitArchetype archetype)
        {
            switch (archetype)
            {
                case UnitArchetype.Spearman: return 0.90m;
                case UnitArchetype.Maceman: return 0.82m;
                case UnitArchetype.Archer: return 0.92m;
                case UnitArchetype.Slinger: return 1.00m;
                case UnitArchetype.Cavalry: return 1.65m;
                default: return 1.00m;
            }
        }

        public static decimal GetMobilityModifier(
            UnitArchetype archetype,
            ArmorClass armor) =>
            GetArchetypeMobilityModifier(archetype) *
            GetMobilityModifier(armor);
    }
}
