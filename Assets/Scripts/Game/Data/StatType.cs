using System;

namespace KingsRescue.Data
{
    /// <summary>
    /// Every number an upgrade is allowed to touch.
    /// </summary>
    public enum StatType
    {
        MaxHp = 0,
        MoveSpeed = 1,
        Damage = 2,
        AttackSpeed = 3,
        Area = 4,
        ProjectileSpeed = 5,
        ProjectileCount = 6,
        Pierce = 7,
        MagnetRadius = 8,
        XpGain = 9,
        CoinGain = 10,
        Armor = 11,
        HpRegen = 12,
        Luck = 13,
        Duration = 14,
        CritChance = 15
    }

    public enum ModifierMode
    {
        /// <summary>Added to the base value before multipliers.</summary>
        Flat = 0,

        /// <summary>Added to a multiplier that starts at 1. A value of 0.2 means +20%.</summary>
        Percent = 1
    }

    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public ModifierMode mode;
        public float value;

        public StatModifier(StatType stat, ModifierMode mode, float value)
        {
            this.stat = stat;
            this.mode = mode;
            this.value = value;
        }

        public string Describe()
        {
            string label = StatTypes.DisplayName(stat);
            return mode == ModifierMode.Percent
                ? $"{label} {(value >= 0f ? "+" : "")}{value * 100f:0}%"
                : $"{label} {(value >= 0f ? "+" : "")}{value:0.##}";
        }
    }

    public static class StatTypes
    {
        public const int Count = 16;

        /// <summary>
        /// Base values used when a <see cref="KingsRescue.Player.PlayerStats"/> field is left at its default.
        /// Multiplier-style stats default to 1, additive bonuses to 0.
        /// </summary>
        public static float DefaultValue(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHp: return 100f;
                case StatType.MoveSpeed: return 5.5f;
                case StatType.Damage: return 1f;
                case StatType.AttackSpeed: return 1f;
                case StatType.Area: return 1f;
                case StatType.ProjectileSpeed: return 1f;
                case StatType.ProjectileCount: return 0f;
                case StatType.Pierce: return 0f;
                case StatType.MagnetRadius: return 1.6f;
                case StatType.XpGain: return 1f;
                case StatType.CoinGain: return 1f;
                case StatType.Armor: return 0f;
                case StatType.HpRegen: return 0f;
                case StatType.Luck: return 1f;
                case StatType.Duration: return 1f;
                case StatType.CritChance: return 0.05f;
                default: return 0f;
            }
        }

        public static string DisplayName(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHp: return "Max HP";
                case StatType.MoveSpeed: return "Move Speed";
                case StatType.Damage: return "Damage";
                case StatType.AttackSpeed: return "Cooldown";
                case StatType.Area: return "Area";
                case StatType.ProjectileSpeed: return "Projectile Speed";
                case StatType.ProjectileCount: return "Amount";
                case StatType.Pierce: return "Pierce";
                case StatType.MagnetRadius: return "Magnet";
                case StatType.XpGain: return "Growth";
                case StatType.CoinGain: return "Greed";
                case StatType.Armor: return "Armor";
                case StatType.HpRegen: return "Recovery";
                case StatType.Luck: return "Luck";
                case StatType.Duration: return "Duration";
                case StatType.CritChance: return "Crit Chance";
                default: return stat.ToString();
            }
        }
    }
}
