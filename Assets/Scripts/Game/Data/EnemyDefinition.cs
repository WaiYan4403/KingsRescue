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
        Charge = 3,

        /// <summary>Circle the player, then periodically dive in.</summary>
        Orbit = 4
    }

    [System.Flags]
    public enum EnemyTrait
    {
        None = 0,
        Lunge = 1 << 0,
        LineCharge = 1 << 1,
        Poison = 1 << 2,
        FireTrail = 1 << 3,
        IceSlow = 1 << 4,
        Stealth = 1 << 5,
        Summon = 1 << 6,
        HealAllies = 1 << 7,
        BuffAllies = 1 << 8,
        Teleport = 1 << 9,
        Lifesteal = 1 << 10,
        FrontBlock = 1 << 11,
        DropHazard = 1 << 12,
        Mimic = 1 << 13
    }

    [System.Flags]
    public enum BossKit
    {
        None = 0,
        Slam = 1 << 0,
        Volley = 1 << 1,
        Summon = 1 << 2,
        Charge = 1 << 3,
        Teleport = 1 << 4,
        Howl = 1 << 5,
        Breath = 1 << 6,
        Vanish = 1 << 7,
        Zones = 1 << 8,
        Enrage = 1 << 9
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

        [Tooltip("Timed encounter. Does not despawn off-screen and is spawned by WaveDirector, not the trickle table.")]
        public bool isBoss;

        [Tooltip("World units above this boss's position for the health bar. Lower it when the bar sits off the top of the screen.")]
        public float healthBarHeight = 1.15f;

        [Tooltip("Allowed to stay on the field during a boss fight (adds, meteors the boss summons). Regular waves will not spawn these.")]
        public bool bossMinion;

        [Tooltip("Denser, named elite. Used by waves and the HUD kill feed.")]
        public bool isElite;

        [Header("Visuals")]
        [Tooltip("Mirror the sprite when moving left. Turn off for art that is drawn facing a fixed direction.")]
        public bool canFlipSprite = true;

        [Tooltip("Tint applied on spawn so variants can share a prefab.")]
        public Color bodyTint = Color.white;

        [Tooltip("Multiplier on the prefab's authored scale. 0 uses 1.")]
        public float bodyScale = 1f;

        [Header("Combat")]
        public float maxHealth = 10f;
        public float contactDamage = 8f;

        [Tooltip("Minimum seconds between this enemy's contact hits, on top of the player's i-frames.")]
        public float contactInterval = 0.6f;

        [Tooltip("World radius at which contact damage lands.")]
        public float contactRadius = 0.5f;

        [Tooltip("When false, walking into the body does nothing. Meteors wait for impact instead.")]
        public bool dealsContactDamage = true;

        [Tooltip("0 = full shove. 1 = immovable. Bosses default to immune if this is left at 0.")]
        [Range(0f, 1f)] public float knockbackResistance;

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

        [Header("Flock")]
        [Tooltip("Trickle spawns drop a tight group that shares one heading instead of a single enemy.")]
        public bool spawnAsFlock;

        [Tooltip("Smallest flock size when spawnAsFlock is on.")]
        public int flockMin = 8;

        [Tooltip("Largest flock size when spawnAsFlock is on.")]
        public int flockMax = 12;

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

        [Tooltip("World units a hostile shot may travel. 0 uses a default cap.")]
        public float projectileMaxDistance = 6.5f;

        [Tooltip("Web only. 0.5 means half speed.")]
        [Range(0.1f, 1f)] public float slowFactor = 0.45f;

        [Tooltip("Web only. How long the slow lasts on the player.")]
        public float slowDuration = 2.2f;

        [Header("Meteor")]
        [Tooltip("Seconds the ground marker stays up before the fireball lands.")]
        public float meteorWarning = 1.1f;

        [Tooltip("Impact blast radius.")]
        public float meteorRadius = 1.15f;

        [Header("Traits / Specials")]
        public EnemyTrait traits;

        [Tooltip("Ally to spawn for summoners and bosses.")]
        public EnemyDefinition summonAlly;

        [Tooltip("Extra summon table. Empty uses summonAlly only.")]
        public EnemyDefinition[] summonAllies;

        public int summonCount = 2;

        [Tooltip("Seconds between lunges, heals, teleports, hazards, and other specials.")]
        public float specialInterval = 3.6f;

        [Tooltip("Reach for slams, heals, howls, and dropped zones.")]
        public float specialRadius = 1.3f;

        [Tooltip("Boss attack set. Ignored on regular enemies.")]
        public BossKit bossKit;

        public GameObject hazardPrefab;

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
        public float healthGrowthPerMinute = 0.28f;

        [Tooltip("Fraction of base contact damage added per minute of run time.")]
        public float damageGrowthPerMinute = 0.1f;

        private const float ExtraHealthPerMinute = 0.12f;
        private const float ExtraDamagePerMinute = 0.06f;
        private const float HealthAccelPerMinuteSquared = 0.015f;
        private const float DamageAccelPerMinuteSquared = 0.008f;

        public float ScaledHealth(float runMinutes)
        {
            return maxHealth * DifficultyMultiplier(runMinutes, healthGrowthPerMinute, ExtraHealthPerMinute, HealthAccelPerMinuteSquared);
        }

        public float ScaledContactDamage(float runMinutes)
        {
            return contactDamage * DifficultyMultiplier(runMinutes, damageGrowthPerMinute, ExtraDamagePerMinute, DamageAccelPerMinuteSquared);
        }

        public float ScaledAttackDamage(float runMinutes)
        {
            return attackDamage * DifficultyMultiplier(runMinutes, damageGrowthPerMinute, ExtraDamagePerMinute, DamageAccelPerMinuteSquared);
        }

        public float ScaledDeathBurstDamage(float runMinutes)
        {
            return deathBurstDamage * DifficultyMultiplier(runMinutes, damageGrowthPerMinute, ExtraDamagePerMinute, DamageAccelPerMinuteSquared);
        }

        /// <summary>Multiplier on incoming knockback. 0 means this body does not move.</summary>
        public float KnockbackTaken
        {
            get
            {
                float resist = knockbackResistance;
                if (isBoss && resist <= 0f)
                {
                    resist = 1f;
                }

                return 1f - Mathf.Clamp01(resist);
            }
        }

        private static float DifficultyMultiplier(float runMinutes, float linear, float extraLinear, float accel)
        {
            float t = Mathf.Max(0f, runMinutes);
            float growth = Mathf.Max(0f, linear) + Mathf.Max(0f, extraLinear);
            return 1f + growth * t + Mathf.Max(0f, accel) * t * t;
        }
    }
}
