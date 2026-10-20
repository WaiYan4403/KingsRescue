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

        /// <summary>Music field centred on the hero. It stays up for its duration, then waits out the cooldown.</summary>
        Aura = 3,

        /// <summary>Projectiles aimed at the nearest enemy, like Magic Wand or Knife.</summary>
        AimedShot = 4,

        /// <summary>Each projectile picks a random living enemy.</summary>
        RandomShot = 5,

        /// <summary>Damage bursts on random enemies, like Lightning Ring.</summary>
        RandomStrike = 6,

        /// <summary>Damaging zones dropped around the hero, like Santa Water.</summary>
        GroundZone = 7,

        /// <summary>A crystal planted on enemies. It damages and freezes anyone standing in it.</summary>
        FreezeField = 8,

        /// <summary>A bolt dropped on the nearest enemies, like the Lightning Wand.</summary>
        BoltStrike = 9,

        /// <summary>A shot that flies out and comes back, hitting on both passes.</summary>
        Boomerang = 10
    }

    public enum WeaponAimStyle
    {
        /// <summary>Throw toward the closest living enemy.</summary>
        Nearest = 0,

        /// <summary>Throw along the hero's walk direction. Standing still aims up the road.</summary>
        Movement = 1,

        /// <summary>Each projectile in the volley locks onto a different nearby enemy.</summary>
        DistinctNearest = 2
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

        [Tooltip("Seconds between activations, before Attack Speed. When Cooldown Follows Duration is on, this is the pause after the effect ends.")]
        public float cooldown = 1.1f;

        [Tooltip("Projectiles per volley, blades in the ring, or swings in the combo.")]
        public int count = 1;

        public int pierce = 0;

        [Tooltip("Swing/aura radius, orbit radius, or projectile hit radius, before the Area stat.")]
        public float area = 1.6f;

        public float projectileSpeed = 12f;

        [Tooltip("How long a projectile, swing effect, or orbit cycle stays on screen. Duration upgrades stretch this.")]
        public float lifetime = 1.6f;

        [Tooltip("World units a projectile may travel. 0 uses the default cap. Negative means unlimited (flies until off-screen).")]
        public float maxDistance = 6.5f;

        [Tooltip("World units to shove a living enemy along the hit. 0 does nothing.")]
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

        [Tooltip("Projectile weapons: the prefab. Needs a Projectile component.")]
        public GameObject projectilePrefab;

        [Tooltip("Orbit: the blade prefab. Needs an OrbitBlade component.")]
        public GameObject orbitPrefab;

        [Tooltip("MeleeSwing: optional cosmetic prefab spawned at the hit. Aura and other area effects use this only when Area Effect Prefabs is empty.")]
        public GameObject effectPrefab;

        [Tooltip("Area-effect visuals by size. Each prefab covers two weapon levels: 1–2, 3–4, 5–6, and so on. The last prefab also covers every higher level until a larger one is added. Used by Aura, ground zones, and random strikes.")]
        public GameObject[] areaEffectPrefabs = System.Array.Empty<GameObject>();

        [Tooltip("Aura: notes played with the music field and returned with it.")]
        public GameObject musicNotesPrefab;

        [Tooltip("Whether the global Extra Projectiles stat adds to this weapon's count.")]
        public bool usesGlobalProjectileCount = true;

        [Tooltip("Seconds between the individual swings of a multi-hit melee combo, or bolts of a straight volley.")]
        public float comboDelay = 0.12f;

        [Tooltip("The next shot waits until this one has finished, then counts down Cooldown. Used by throws and casts that should not overlap.")]
        public bool cooldownFollowsDuration;

        [Tooltip("ForwardShot: when true, extra projectiles fan in an arc. When false, every shot travels straight forward.")]
        public bool fanProjectiles = true;

        [Tooltip("AimedShot: who each projectile is thrown at.")]
        public WeaponAimStyle aimStyle = WeaponAimStyle.Nearest;

        [Tooltip("Projectile curves toward a living enemy after it leaves the hand.")]
        public bool projectileHoming;

        [Header("Ownership")]
        [Tooltip("Signature weapon for one playable character. Other heroes cannot pick it up or be offered it.")]
        public bool isUniqueWeapon;

        [Tooltip("Stays out of the level-up pool until an achievement unlocks it.")]
        public bool requiresUnlock;

        [Header("Character Animators")]
        [Tooltip("Signature weapon: swings fire Attack-stage triggers on the player's body and effect animators.")]
        public bool usesCharacterAnimators;

        [Tooltip("Animator trigger names, one per unlockable attack stage (Attack1, Attack2, ...).")]
        public string[] attackTriggers = { "Attack1", "Attack2", "Attack3" };

        [Tooltip("Weapon level that plays the longer effect slashes. 0 keeps the normal clips.")]
        public int advancedReachLevel;

        [Tooltip("Effect animator triggers used once advanced reach is unlocked. Same order as Attack Triggers.")]
        public string[] advancedAttackTriggers = System.Array.Empty<string>();

        [Tooltip("Weapon level required to unlock each trigger. Missing entries default to stage index + 1.")]
        public int[] attackStageUnlockLevels = { 1, 2, 3 };

        [Tooltip("First power-up. One short slash prefab per attack stage. Strikes 1 and 2 can share a prefab. An empty slot, or a stage the weapon has not unlocked, does not fire.")]
        public GameObject[] powerSlashPrefabs = System.Array.Empty<GameObject>();

        [Header("Levels (index 0 is level 1)")]
        public WeaponLevel[] levels = { new WeaponLevel() };

        public int MaxLevel => levels != null && levels.Length > 0 ? levels.Length : 1;

        public bool UsesSizedAreaEffects => areaEffectPrefabs != null && areaEffectPrefabs.Length > 0;

        /// <summary>
        /// Visual for an area effect at this weapon level. Each authored prefab covers two levels.
        /// The last prefab also covers every level above it.
        /// </summary>
        public GameObject GetAreaEffectPrefab(int weaponLevel)
        {
            if (areaEffectPrefabs != null && areaEffectPrefabs.Length > 0)
            {
                int band = Mathf.Max(0, weaponLevel - 1) / 2;
                int index = Mathf.Min(band, areaEffectPrefabs.Length - 1);
                for (int i = index; i >= 0; i--)
                {
                    if (areaEffectPrefabs[i] != null)
                    {
                        return areaEffectPrefabs[i];
                    }
                }
            }

            return effectPrefab;
        }

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
                if (string.IsNullOrEmpty(attackTriggers[i]) || weaponLevel < GetAttackStageUnlockLevel(i))
                {
                    break;
                }

                unlocked++;
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

        public bool UsesAdvancedReach(int weaponLevel)
        {
            return advancedReachLevel > 0
                && weaponLevel >= advancedReachLevel
                && advancedAttackTriggers != null
                && advancedAttackTriggers.Length > 0;
        }

        /// <summary>
        /// Effect-animator trigger for this strike. The body keeps the normal attack trigger.
        /// </summary>
        public string GetEffectAttackTrigger(int weaponLevel, int stage)
        {
            if (!UsesAdvancedReach(weaponLevel))
            {
                return GetAttackTrigger(stage);
            }

            int index = Mathf.Clamp(stage - 1, 0, advancedAttackTriggers.Length - 1);
            string advanced = advancedAttackTriggers[index];
            return string.IsNullOrEmpty(advanced) ? GetAttackTrigger(stage) : advanced;
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
