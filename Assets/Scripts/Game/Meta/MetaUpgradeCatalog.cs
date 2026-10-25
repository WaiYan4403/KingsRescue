using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Data;

namespace KingsRescue.Meta
{
    public readonly struct MetaUpgradeOffer
    {
        public readonly StatType stat;
        public readonly ModifierMode mode;
        public readonly float perLevel;
        public readonly int maxLevel;
        public readonly int baseCost;

        public MetaUpgradeOffer(StatType stat, ModifierMode mode, float perLevel, int maxLevel, int baseCost)
        {
            this.stat = stat;
            this.mode = mode;
            this.perLevel = perLevel;
            this.maxLevel = maxLevel;
            this.baseCost = baseCost;
        }

        public int CostForNext(int currentLevel)
        {
            return baseCost * (currentLevel + 1);
        }

        /// <summary>Coins paid to reach this level from zero.</summary>
        public int SpentForLevel(int level)
        {
            int n = Mathf.Max(0, level);
            return baseCost * n * (n + 1) / 2;
        }

        public StatModifier ModifierForLevels(int levels)
        {
            int clamped = Mathf.Clamp(levels, 0, maxLevel);
            float amount = perLevel * clamped;

            // Amount and pierce are whole projectiles. One point as soon as the
            // upgrade is bought, a second only when the track is finished.
            if (stat == StatType.ProjectileCount || stat == StatType.Pierce)
            {
                amount = clamped <= 0 ? 0f : clamped >= maxLevel ? 2f : 1f;
            }

            return new StatModifier(stat, mode, amount);
        }
    }

    public static class MetaUpgradeCatalog
    {
        public static readonly MetaUpgradeOffer[] Offers =
        {
            new MetaUpgradeOffer(StatType.MaxHp, ModifierMode.Flat, 6f, 6, 30),
            new MetaUpgradeOffer(StatType.Damage, ModifierMode.Percent, 0.03f, 6, 35),
            new MetaUpgradeOffer(StatType.AttackSpeed, ModifierMode.Percent, 0.03f, 6, 35),
            new MetaUpgradeOffer(StatType.MoveSpeed, ModifierMode.Flat, 0.1f, 6, 30),
            new MetaUpgradeOffer(StatType.Area, ModifierMode.Percent, 0.03f, 6, 30),
            new MetaUpgradeOffer(StatType.Armor, ModifierMode.Flat, 0.5f, 6, 40),
            new MetaUpgradeOffer(StatType.HpRegen, ModifierMode.Flat, 0.12f, 6, 30),
            new MetaUpgradeOffer(StatType.Luck, ModifierMode.Percent, 0.04f, 6, 40),
            new MetaUpgradeOffer(StatType.Duration, ModifierMode.Percent, 0.04f, 6, 35),
            new MetaUpgradeOffer(StatType.ProjectileCount, ModifierMode.Flat, 1f, 6, 50),
            new MetaUpgradeOffer(StatType.ProjectileSpeed, ModifierMode.Percent, 0.04f, 6, 30),
            new MetaUpgradeOffer(StatType.Pierce, ModifierMode.Flat, 1f, 6, 45),
            new MetaUpgradeOffer(StatType.MagnetRadius, ModifierMode.Percent, 0.06f, 6, 30),
            new MetaUpgradeOffer(StatType.XpGain, ModifierMode.Percent, 0.04f, 6, 35),
            new MetaUpgradeOffer(StatType.CoinGain, ModifierMode.Percent, 0.05f, 6, 35)
        };

        public static bool TryGet(StatType stat, out MetaUpgradeOffer offer)
        {
            for (int i = 0; i < Offers.Length; i++)
            {
                if (Offers[i].stat == stat)
                {
                    offer = Offers[i];
                    return true;
                }
            }

            offer = default;
            return false;
        }

        public static List<StatModifier> BuildModifiers(CharacterDefinition character)
        {
            var modifiers = new List<StatModifier>(Offers.Length);
            if (character == null)
            {
                return modifiers;
            }

            for (int i = 0; i < Offers.Length; i++)
            {
                MetaUpgradeOffer offer = Offers[i];
                int level = Mathf.Min(offer.maxLevel, MetaProgress.GetLevel(character, offer.stat));
                if (level > 0)
                {
                    modifiers.Add(offer.ModifierForLevels(level));
                }
            }

            return modifiers;
        }
    }
}
