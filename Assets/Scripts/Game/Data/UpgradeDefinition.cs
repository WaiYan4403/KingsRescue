using UnityEngine;
using KingsRescue.Player;

namespace KingsRescue.Data
{
    public enum UpgradeKind
    {
        /// <summary>Grants a weapon the hero does not own yet.</summary>
        NewWeapon = 0,

        /// <summary>Raises the level of a weapon the hero already owns.</summary>
        WeaponLevel = 1,

        /// <summary>Grants or raises a passive item.</summary>
        Passive = 2,

        /// <summary>Consolation coins. Used to pad the choice screen when nothing else qualifies.</summary>
        CoinBonus = 3
    }

    /// <summary>
    /// One card on the level-up screen. Knows whether it is legal to offer right now
    /// so the pool never shows a maxed or duplicate pick.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Upgrade Definition", fileName = "Upgrade_")]
    public class UpgradeDefinition : ScriptableObject
    {
        [Header("Type")]
        public UpgradeKind kind = UpgradeKind.Passive;

        [Tooltip("Relative chance of being offered. 0 removes it from the pool.")]
        public float weight = 100f;

        [Header("Payload")]
        [Tooltip("Used by NewWeapon and WeaponLevel.")]
        public WeaponDefinition weapon;

        [Tooltip("Used by Passive.")]
        public PassiveDefinition passive;

        [Tooltip("Used by CoinBonus.")]
        public int coinAmount = 25;

        [Header("Presentation Overrides (optional)")]
        public string titleOverride = "";
        [TextArea(2, 4)] public string descriptionOverride = "";
        public Sprite iconOverride;

        /// <summary>Can this card legally be shown given what the hero already has?</summary>
        public bool IsAvailable(WeaponLoadout loadout, PlayerStats stats)
        {
            if (weight <= 0f)
            {
                return false;
            }

            switch (kind)
            {
                case UpgradeKind.NewWeapon:
                    return weapon != null
                        && !weapon.isUniqueWeapon
                        && loadout.CanReceiveWeapon(weapon)
                        && !loadout.HasWeapon(weapon)
                        && loadout.HasRoomForWeapon;

                case UpgradeKind.WeaponLevel:
                    return weapon != null
                        && loadout.CanReceiveWeapon(weapon)
                        && loadout.HasWeapon(weapon)
                        && !loadout.IsWeaponMaxed(weapon);

                case UpgradeKind.Passive:
                    if (passive == null || loadout.IsPassiveMaxed(passive))
                    {
                        return false;
                    }

                    return loadout.HasPassive(passive) || loadout.HasRoomForPassive;

                case UpgradeKind.CoinBonus:
                    return true;

                default:
                    return false;
            }
        }

        public void Apply(WeaponLoadout loadout, PlayerStats stats)
        {
            switch (kind)
            {
                case UpgradeKind.NewWeapon:
                case UpgradeKind.WeaponLevel:
                    if (loadout.CanReceiveWeapon(weapon))
                    {
                        loadout.AddOrLevelWeapon(weapon);
                    }
                    break;

                case UpgradeKind.Passive:
                    loadout.AddOrLevelPassive(passive);
                    break;

                case UpgradeKind.CoinBonus:
                    Core.GameManager.Instance?.AddCoins(coinAmount);
                    break;
            }
        }

        /// <summary>The level the hero will be at after taking this card. 0 when not applicable.</summary>
        public int ResultingLevel(WeaponLoadout loadout)
        {
            switch (kind)
            {
                case UpgradeKind.NewWeapon:
                    return 1;
                case UpgradeKind.WeaponLevel:
                    return loadout.GetWeaponLevel(weapon) + 1;
                case UpgradeKind.Passive:
                    return loadout.GetPassiveLevel(passive) + 1;
                default:
                    return 0;
            }
        }

        public string Title(WeaponLoadout loadout)
        {
            if (!string.IsNullOrWhiteSpace(titleOverride))
            {
                return titleOverride;
            }

            switch (kind)
            {
                case UpgradeKind.NewWeapon:
                case UpgradeKind.WeaponLevel:
                    return weapon != null ? weapon.displayName : name;
                case UpgradeKind.Passive:
                    return passive != null ? passive.displayName : name;
                case UpgradeKind.CoinBonus:
                    return $"{coinAmount} Coins";
                default:
                    return name;
            }
        }

        public string Description(WeaponLoadout loadout)
        {
            if (!string.IsNullOrWhiteSpace(descriptionOverride))
            {
                return descriptionOverride;
            }

            switch (kind)
            {
                case UpgradeKind.NewWeapon:
                    return weapon != null ? weapon.description : "";
                case UpgradeKind.WeaponLevel:
                    return weapon != null ? weapon.DescribeLevel(ResultingLevel(loadout)) : "";
                case UpgradeKind.Passive:
                    return passive != null ? passive.DescribeLevel(ResultingLevel(loadout)) : "";
                case UpgradeKind.CoinBonus:
                    return "Nothing left to learn. Take the purse instead.";
                default:
                    return "";
            }
        }

        /// <summary>Short banner shown in the card corner, e.g. "NEW" or "Lv 3".</summary>
        public string Banner(WeaponLoadout loadout)
        {
            switch (kind)
            {
                case UpgradeKind.NewWeapon:
                    return "NEW";
                case UpgradeKind.WeaponLevel:
                case UpgradeKind.Passive:
                    int level = ResultingLevel(loadout);
                    return level <= 1 ? "NEW" : $"Lv {level}";
                default:
                    return "";
            }
        }

        public Sprite Icon()
        {
            if (iconOverride != null)
            {
                return iconOverride;
            }

            if (weapon != null && weapon.icon != null)
            {
                return weapon.icon;
            }

            return passive != null ? passive.icon : null;
        }
    }
}
