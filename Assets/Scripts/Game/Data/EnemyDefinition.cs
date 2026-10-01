using UnityEngine;

namespace KingsRescue.Data
{
    /// <summary>
    /// Authored stats for one enemy type. The prefab stays dumb; this asset decides
    /// how hard it hits and how fast it chases.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Enemy Definition", fileName = "Enemy_")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Enemy";
        public GameObject prefab;

        [Header("Combat")]
        public float maxHealth = 10f;
        public float contactDamage = 8f;

        [Tooltip("Minimum seconds between this enemy's contact hits, on top of the player's i-frames.")]
        public float contactInterval = 0.6f;

        [Tooltip("World radius at which contact damage lands.")]
        public float contactRadius = 0.5f;

        [Header("Movement")]
        [Tooltip("Follow / cruise speed in world units per second, once the enemy is on screen.")]
        public float moveSpeed = 2.4f;

        [Tooltip("How much of the world scroll drags this enemy down. Lower values let it fight the current and stay on the player.")]
        [Range(0f, 1f)] public float scrollInfluence = 1f;

        [Tooltip("Seconds to reach full cruise / follow speed. Gives heavier enemies weight.")]
        public float turnResponsiveness = 6f;

        [Tooltip("Chance this spawn follows the player while at or above the player's Y.")]
        [Range(0f, 1f)] public float followChance = 1f;

        [Tooltip("Chance a follower gives up and just flies down once its Y is below the player's.")]
        [Range(0f, 1f)] public float giveUpWhenPassedChance = 0.45f;

        [Tooltip("Personal space. Other enemies inside this radius get a soft push away.")]
        public float separationRadius = 0.8f;

        [Tooltip("Push speed in world units per second when two enemies occupy the same point.")]
        public float separationStrength = 3.25f;

        [Header("Rewards")]
        public int xpValue = 1;
        [Range(0f, 1f)] public float coinDropChance = 0.06f;
        [Range(0f, 1f)] public float healthDropChance = 0.015f;

        [Header("Scaling")]
        [Tooltip("Fraction of base health added per minute of run time.")]
        public float healthGrowthPerMinute = 0.35f;

        [Tooltip("Fraction of base contact damage added per minute of run time.")]
        public float damageGrowthPerMinute = 0.12f;

        public float ScaledHealth(float runMinutes)
        {
            return maxHealth * (1f + healthGrowthPerMinute * runMinutes);
        }

        public float ScaledContactDamage(float runMinutes)
        {
            return contactDamage * (1f + damageGrowthPerMinute * runMinutes);
        }
    }
}
