using System.Collections.Generic;
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

        public StatModifier ModifierForLevels(int levels)
        {
            return new StatModifier(stat, mode, perLevel * levels);
        }
    }

    public static class MetaUpgradeCatalog
    {
        public static readonly MetaUpgradeOffer[] Offers =
        {
            new MetaUpgradeOffer(StatType.MaxHp, ModifierMode.Flat, 12f, 10, 30),
            new MetaUpgradeOffer(StatType.Damage, ModifierMode.Percent, 0.06f, 10, 35),
            new MetaUpgradeOffer(StatType.AttackSpeed, ModifierMode.Percent, 0.05f, 10, 35),
            new MetaUpgradeOffer(StatType.MoveSpeed, ModifierMode.Flat, 0.25f, 8, 30),
            new MetaUpgradeOffer(StatType.Area, ModifierMode.Percent, 0.05f, 8, 30),
            new MetaUpgradeOffer(StatType.Armor, ModifierMode.Flat, 1f, 8, 40)
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
                int level = MetaProgress.GetLevel(character, offer.stat);
                if (level > 0)
                {
                    modifiers.Add(offer.ModifierForLevels(level));
                }
            }

            return modifiers;
        }
    }
}
