using System;
using UnityEngine;

namespace KingsRescue.Data
{
    public enum WeaponFireMode
    {
        /// <summary>The hero's sword: a wedge swing toward the nearest threat. Starting weapon.</summary>
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

        [Tooltip("Seconds between the individual swings of a multi-hit melee combo.")]
        public float comboDelay = 0.12f;

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
    }
}
