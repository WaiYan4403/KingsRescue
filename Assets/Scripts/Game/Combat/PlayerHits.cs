using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Builds the hit the hero's weapons apply. A crit doubles the hit and marks it
    /// so the damage number can grow.
    /// </summary>
    public static class PlayerHits
    {
        public const float CritDamageMultiplier = 2f;

        public static DamageInfo Strike(float amount, Vector2 hitPoint, Vector2 direction, float knockback, GameObject source)
        {
            bool critical = false;
            PlayerStats stats = GameManager.Instance != null ? GameManager.Instance.PlayerStats : null;
            float chance = stats != null ? stats.CritChance : 0f;
            if (chance > 0f && Random.value < chance)
            {
                critical = true;
                amount *= CritDamageMultiplier;
            }

            return new DamageInfo(amount, hitPoint, direction, knockback, source, critical);
        }
    }
}
