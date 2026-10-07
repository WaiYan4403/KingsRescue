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
        EarnCoins = 6
    }

    [CreateAssetMenu(menuName = "King's Rescue/Achievement", fileName = "Achievement_")]
    public class AchievementDefinition : ScriptableObject
    {
        public string id = "";
        public string title = "Achievement";
        [TextArea(2, 4)] public string description = "";
        public AchievementKind kind = AchievementKind.KillAny;
        public int target = 1;

        [Tooltip("KillEnemy only.")]
        public EnemyDefinition enemy;

        [Header("Rewards")]
        public int rewardCoins;
        public CharacterDefinition rewardCharacter;
        public WeaponDefinition rewardWeapon;

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

            return parts.Count == 0 ? "Glory" : string.Join(" · ", parts);
        }
    }
}
