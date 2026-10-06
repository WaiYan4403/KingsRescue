using UnityEngine;

namespace KingsRescue.Data
{
    public enum EnemyMoveStyle
    {
        /// <summary>Steer toward the player, then maybe give up once passed.</summary>
        Chase = 0,

        /// <summary>Fly in a locked heading. Used by bat lines crossing the field.</summary>
        Straight = 1,

        /// <summary>Hold a ring around the player so archers and mages can shoot.</summary>
        KeepDistance = 2,

        /// <summary>Always chase, including up-screen. Used by wolves.</summary>
        Charge = 3
    }

    public enum EnemyAttackStyle
    {
        Contact = 0,
        Ranged = 1,
        Web = 2,
        Meteor = 3
    }

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

        [Tooltip("When false, walking into the body does nothing. Meteors wait for impact instead.")]
        public bool dealsContactDamage = true;

        [Header("Movement")]
        public EnemyMoveStyle moveStyle = EnemyMoveStyle.Chase;

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

        [Tooltip("KeepDistance only. Preferred gap from the player.")]
        public float preferredDistance = 4.5f;

        [Header("Ranged / Special")]
        public EnemyAttackStyle attackStyle = EnemyAttackStyle.Contact;

        public GameObject projectilePrefab;

        [Tooltip("Meteor cast. The falling fireball summoned onto the player. Archers leave this empty.")]
        public EnemyDefinition meteorDefinition;

        [Tooltip("Seconds between shots, webs, or meteor casts.")]
        public float attackInterval = 1.8f;

        [Tooltip("Must be this close to the player before shooting.")]
        public float attackRange = 8f;

        public float attackDamage = 8f;
        public float projectileSpeed = 7f;
        public float projectileLifetime = 3.5f;
        public float projectileRadius = 0.28f;
        public bool projectileHoming;

        [Tooltip("Web only. 0.5 means half speed.")]
        [Range(0.1f, 1f)] public float slowFactor = 0.45f;

        [Tooltip("Web only. How long the slow lasts on the player.")]
        public float slowDuration = 2.2f;

        [Header("Meteor")]
        [Tooltip("Seconds the ground marker stays up before the fireball lands.")]
        public float meteorWarning = 1.1f;

        [Tooltip("Impact blast radius.")]
        public float meteorRadius = 1.15f;

        [Header("Death Burst")]
        [Tooltip("0 disables. Exploders pop for this much damage in a small radius.")]
        public float deathBurstDamage = 0f;

        public float deathBurstRadius = 1.4f;

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

        public float ScaledAttackDamage(float runMinutes)
        {
            return attackDamage * (1f + damageGrowthPerMinute * runMinutes);
        }

        public float ScaledDeathBurstDamage(float runMinutes)
        {
            return deathBurstDamage * (1f + damageGrowthPerMinute * runMinutes);
        }
    }
}
