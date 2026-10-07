using UnityEngine;

namespace KingsRescue.Data
{
    [CreateAssetMenu(menuName = "King's Rescue/Achievement Catalog", fileName = "AchievementCatalog")]
    public class AchievementCatalog : ScriptableObject
    {
        public AchievementDefinition[] achievements = System.Array.Empty<AchievementDefinition>();

        private static AchievementCatalog cached;

        public static AchievementCatalog Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<AchievementCatalog>("AchievementCatalog");
            }

            return cached;
        }

        public static AchievementDefinition[] All
        {
            get
            {
                AchievementCatalog catalog = Load();
                return catalog != null && catalog.achievements != null
                    ? catalog.achievements
                    : System.Array.Empty<AchievementDefinition>();
            }
        }

        public static bool IsWeaponGated(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            AchievementDefinition[] list = All;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null && list[i].rewardWeapon == weapon)
                {
                    return true;
                }
            }

            return false;
        }

        public static AchievementDefinition FindUnlockForCharacter(CharacterDefinition character)
        {
            if (character == null)
            {
                return null;
            }

            AchievementDefinition[] list = All;
            for (int i = 0; i < list.Length; i++)
            {
                AchievementDefinition entry = list[i];
                if (entry != null && entry.rewardCharacter == character)
                {
                    return entry;
                }
            }

            return null;
        }

        public static string UnlockHint(CharacterDefinition character)
        {
            AchievementDefinition entry = FindUnlockForCharacter(character);
            if (entry == null)
            {
                return "Locked";
            }

            if (entry.kind == AchievementKind.PayCoins)
            {
                return $"{entry.target} coins";
            }

            return entry.title;
        }
    }
}
