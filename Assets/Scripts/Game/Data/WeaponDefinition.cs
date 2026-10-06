using System;
using UnityEngine;

namespace KingsRescue.Data
{
    public enum WeaponFireMode
    {
        /// <summary>A wedge swing toward the nearest threat. Character signature weapons use this with body/effect attack stages.</summary>
        MeleeSwing = 0,

        /// <summary>Retro shmup volley fired up the screen.</summary>
        ForwardShot = 1,

        /// <summary>Blades circling the hero, damaging on contact.</summary>
        Orbit = 2,

        /// <summary>Pulsing damage field centred on the hero.</summary>
        Aura = 3
    }

    /// <summary>
    /// Per-level numbers for a weapon. Every level-up is authored data, not code.
    /// </summary>
    [Serializable]
    public class WeaponLevel
    {
        [Tooltip("Shown on the level-up card, e.g. \"+1 blade, +15% area\".")]
        [TextArea(1, 3)] public string upgradeText = "";

        public float damage = 10f;

        [Tooltip("Seconds between activations, before Attack Speed is applied.")]
        public float cooldown = 1.1f;

        [Tooltip("Projectiles per volley, blades in the ring, or swings in the combo.")]
        public int count = 1;

        public int pierce = 0;

        [Tooltip("Swing/aura radius, orbit radius, or projectile hit radius, before the Area stat.")]
        public float area = 1.6f;

        public float projectileSpeed = 12f;

        [Tooltip("Projectile lifetime, or the on-screen life of a swing effect.")]
        public float lifetime = 1.6f;

        [Tooltip("ForwardShot only. World units a projectile may travel. 0 uses a default cap.")]
        public float maxDistance = 6.5f;

        public float knockback = 0f;

        [Tooltip("MeleeSwing only. Width of the wedge in degrees. 360 hits all around.")]
        [Range(10f, 360f)] public float arcDegrees = 150f;

        [Tooltip("ForwardShot only. Total fan width in degrees when firing more than one projectile.")]
        [Range(0f, 180f)] public float spreadDegrees = 18f;

        [Tooltip("Orbit only. Seconds before the same enemy can be hit again by one blade.")]
        public float rehitInterval = 0.45f;

        [Tooltip("Orbit only. Revolutions per second.")]
        public float orbitSpeed = 0.7f;
    }

    /// <summary>
    /// A weapon the hero can own. Weapons fire themselves; the player only steers.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Weapon Definition", fileName = "Weapon_")]
    public class WeaponDefinition : ScriptableObject
    {
        [Header("Presentation")]
        public string displayName = "Weapon";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;

        [Header("Behaviour")]
        public WeaponFireMode fireMode = WeaponFireMode.MeleeSwing;

        [Tooltip("ForwardShot: the projectile prefab. Needs a Projectile component.")]
        public GameObject projectilePrefab;

        [Tooltip("Orbit: the blade prefab. Needs an OrbitBlade component.")]
        public GameObject orbitPrefab;

        [Tooltip("MeleeSwing and Aura: optional cosmetic prefab spawned at the hit. Needs an AutoDespawn component.")]
        public GameObject effectPrefab;

        [Tooltip("Whether the global Extra Projectiles stat adds to this weapon's count.")]
        public bool usesGlobalProjectileCount = true;

        [Tooltip("Seconds between the individual swings of a multi-hit melee combo, or bolts of a straight volley.")]
        public float comboDelay = 0.12f;

        [Tooltip("ForwardShot: when true, extra projectiles fan in an arc. When false, every shot travels straight forward.")]
        public bool fanProjectiles = true;

        [Header("Ownership")]
        [Tooltip("Signature weapon for one playable character. Other heroes cannot pick it up or be offered it.")]
        public bool isUniqueWeapon;

        [Header("Character Animators")]
        [Tooltip("Signature weapon: swings fire Attack-stage triggers on the player's body and effect animators.")]
        public bool usesCharacterAnimators;

        [Tooltip("Animator trigger names, one per unlockable attack stage (Attack1, Attack2, ...).")]
        public string[] attackTriggers = { "Attack1", "Attack2", "Attack3" };

        [Tooltip("Weapon level required to unlock each trigger. Missing entries default to stage index + 1.")]
        public int[] attackStageUnlockLevels = { 1, 2, 3 };

        [Header("Levels (index 0 is level 1)")]
        public WeaponLevel[] levels = { new WeaponLevel() };

        public int MaxLevel => levels != null && levels.Length > 0 ? levels.Length : 1;

        /// <summary>Level is 1-based and clamped to the authored range.</summary>
        public WeaponLevel GetLevel(int level)
        {
            if (levels == null || levels.Length == 0)
            {
                return new WeaponLevel();
            }

            int index = Mathf.Clamp(level - 1, 0, levels.Length - 1);
            return levels[index];
        }

        /// <summary>Card text for taking this weapon to the given level.</summary>
        public string DescribeLevel(int level)
        {
            if (level <= 1)
            {
                return description;
            }

            WeaponLevel data = GetLevel(level);
            return string.IsNullOrWhiteSpace(data.upgradeText) ? description : data.upgradeText;
        }

        public int GetUnlockedAttackStages(int weaponLevel)
        {
            if (attackTriggers == null || attackTriggers.Length == 0)
            {
                return 0;
            }

            int unlocked = 0;
            for (int i = 0; i < attackTriggers.Length; i++)
            {
                if (string.IsNullOrEmpty(attackTriggers[i]))
                {
                    continue;
                }

                if (weaponLevel >= GetAttackStageUnlockLevel(i))
                {
                    unlocked++;
                }
            }

            return unlocked;
        }

        public string GetAttackTrigger(int stage)
        {
            if (attackTriggers == null || attackTriggers.Length == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(stage - 1, 0, attackTriggers.Length - 1);
            return attackTriggers[index];
        }

        public int GetAttackStageUnlockLevel(int stageIndex)
        {
            if (attackStageUnlockLevels != null && stageIndex >= 0 && stageIndex < attackStageUnlockLevels.Length)
            {
                return Mathf.Max(1, attackStageUnlockLevels[stageIndex]);
            }

            return stageIndex + 1;
        }
    }
}
