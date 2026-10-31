using UnityEngine;

namespace KingsRescue.Data
{
    public enum AchievementKind
    {
        KillAny = 0,
        KillEnemy = 1,
        PayCoins = 2,
        SurviveSeconds = 3,
        SmashChests = 4,
        CollectPowerUps = 5,
        EarnCoins = 6,
        KillWithWeapon = 7
    }

    [CreateAssetMenu(menuName = "King's Rescue/Achievement", fileName = "Achievement_")]
    public class AchievementDefinition : ScriptableObject
    {
        public string id = "";
        public string title = "Achievement";
        [TextArea(2, 4)] public string description = "";
        public AchievementKind kind = AchievementKind.KillAny;
        public int target = 1;

        [Tooltip("KillEnemy: every listed enemy counts. KillWithWeapon: only these enemies, or any enemy when the list is empty.")]
        public EnemyDefinition[] enemies;

        [Tooltip("KillWithWeapon only. Kills landed by this weapon.")]
        public WeaponDefinition weapon;

        [Header("Rewards")]
        public int rewardCoins;
        public CharacterDefinition rewardCharacter;
        public WeaponDefinition rewardWeapon;
        public PassiveDefinition rewardPassive;

        public string Id => string.IsNullOrWhiteSpace(id) ? name : id;

        public string RewardSummary()
        {
            var parts = new System.Collections.Generic.List<string>(3);
            if (rewardCoins > 0)
            {
                parts.Add($"{rewardCoins} coins");
            }

            if (rewardCharacter != null)
            {
                parts.Add(rewardCharacter.displayName);
            }

            if (rewardWeapon != null)
            {
                parts.Add(rewardWeapon.displayName);
            }

            if (rewardPassive != null)
            {
                parts.Add(rewardPassive.displayName);
            }

            return parts.Count == 0 ? "Glory" : string.Join(" · ", parts);
        }

        /// <summary>Item art for this reward. Coin payouts use the supplied coin sprite.</summary>
        public Sprite RewardIcon(Sprite coinIcon)
        {
            if (rewardCharacter != null)
            {
                WeaponDefinition unique = rewardCharacter.uniqueWeapon;
                if (unique != null && unique.icon != null)
                {
                    return unique.icon;
                }

                Sprite portrait = rewardCharacter.portrait;
                if (portrait != null && portrait.texture != null && portrait.texture.name == "Items")
                {
                    return portrait;
                }
            }

            if (rewardWeapon != null && rewardWeapon.icon != null)
            {
                return rewardWeapon.icon;
            }

            if (rewardPassive != null && rewardPassive.icon != null)
            {
                return rewardPassive.icon;
            }

            return rewardCoins > 0 ? coinIcon : null;
        }
    }
}
