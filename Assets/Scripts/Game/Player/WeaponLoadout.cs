using System;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.Player
{
    /// <summary>
    /// What the hero is carrying. Weapons become child objects with a
    /// <see cref="WeaponRuntime"/>; passives fold straight into <see cref="PlayerStats"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class WeaponLoadout : MonoBehaviour
    {
        [Tooltip("Playable hero. Their unique weapon is granted at run start.")]
        [SerializeField] private CharacterDefinition character;

        [Tooltip("Fallback unique weapon when no character is assigned.")]
        [SerializeField] private WeaponDefinition startingWeapon;

        [Tooltip("Parent for the weapon child objects. Defaults to this transform.")]
        [SerializeField] private Transform weaponRoot;

        [SerializeField] private int maxWeapons = 4;
        [SerializeField] private int maxPassives = 4;

        private readonly Dictionary<WeaponDefinition, WeaponRuntime> weapons = new Dictionary<WeaponDefinition, WeaponRuntime>();
        private readonly Dictionary<PassiveDefinition, int> passiveLevels = new Dictionary<PassiveDefinition, int>();
        private readonly List<WeaponDefinition> weaponOrder = new List<WeaponDefinition>(4);
        private readonly List<PassiveDefinition> passiveOrder = new List<PassiveDefinition>(4);

        private PlayerStats stats;
        private PlayerController controller;

        public event Action Changed;

        public int WeaponCount => weapons.Count;

        public int PassiveCount => passiveLevels.Count;

        public int MaxWeapons => maxWeapons;

        public int MaxPassives => maxPassives;

        public IReadOnlyDictionary<WeaponDefinition, WeaponRuntime> Weapons => weapons;

        public IReadOnlyDictionary<PassiveDefinition, int> Passives => passiveLevels;

        public CharacterDefinition Character => character;

        public WeaponDefinition UniqueWeapon => character != null && character.uniqueWeapon != null
            ? character.uniqueWeapon
            : startingWeapon;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();

            if (weaponRoot == null)
            {
                weaponRoot = transform;
            }
        }

        public void BindCharacter(CharacterDefinition next)
        {
            if (next != null)
            {
                character = next;
            }

            PlayerHealth health = GetComponent<PlayerHealth>();
            if (health != null)
            {
                health.ApplyCharacter(character);
            }
        }

        private void Start()
        {
            stats.ApplyPersistent(character);

            WeaponDefinition unique = UniqueWeapon;
            if (unique != null)
            {
                AddOrLevelWeapon(unique);
            }
        }

        public bool CanReceiveWeapon(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                return false;
            }

            if (!weapon.isUniqueWeapon)
            {
                return true;
            }

            return weapon == UniqueWeapon || HasWeapon(weapon);
        }

        // --- Weapons ---------------------------------------------------------

        public bool HasWeapon(WeaponDefinition weapon)
        {
            return weapon != null && weapons.ContainsKey(weapon);
        }

        public int GetWeaponLevel(WeaponDefinition weapon)
        {
            return weapon != null && weapons.TryGetValue(weapon, out WeaponRuntime runtime) ? runtime.Level : 0;
        }

        public bool IsWeaponMaxed(WeaponDefinition weapon)
        {
            return weapon != null && weapons.TryGetValue(weapon, out WeaponRuntime runtime) && runtime.IsMaxLevel;
        }

        public bool HasRoomForWeapon => weapons.Count < maxWeapons;

        public void AddOrLevelWeapon(WeaponDefinition weapon)
        {
            if (weapon == null || !CanReceiveWeapon(weapon))
            {
                return;
            }

            if (weapons.TryGetValue(weapon, out WeaponRuntime existing))
            {
                existing.LevelUp();
                Changed?.Invoke();
                return;
            }

            if (!HasRoomForWeapon)
            {
                Debug.LogWarning($"Loadout is full, cannot add '{weapon.displayName}'.", this);
                return;
            }

            var host = new GameObject($"Weapon [{weapon.displayName}]");
            host.transform.SetParent(weaponRoot, false);
            host.transform.localPosition = Vector3.zero;

            WeaponRuntime runtime = host.AddComponent<WeaponRuntime>();
            runtime.Initialize(weapon, stats, controller);

            weapons.Add(weapon, runtime);
            weaponOrder.Add(weapon);
            AchievementProgress.MarkCollectedWeapon(weapon);
            Changed?.Invoke();
        }

        public void CollectWeapons(List<WeaponRuntime> results)
        {
            results.Clear();
            for (int i = 0; i < weaponOrder.Count; i++)
            {
                if (weapons.TryGetValue(weaponOrder[i], out WeaponRuntime runtime) && runtime != null)
                {
                    results.Add(runtime);
                }
            }
        }

        // --- Passives --------------------------------------------------------

        public bool HasPassive(PassiveDefinition passive)
        {
            return passive != null && passiveLevels.ContainsKey(passive);
        }

        public int GetPassiveLevel(PassiveDefinition passive)
        {
            return passive != null && passiveLevels.TryGetValue(passive, out int level) ? level : 0;
        }

        public bool IsPassiveMaxed(PassiveDefinition passive)
        {
            return passive != null && GetPassiveLevel(passive) >= passive.MaxLevel;
        }

        public bool HasRoomForPassive => passiveLevels.Count < maxPassives;

        public void AddOrLevelPassive(PassiveDefinition passive)
        {
            if (passive == null)
            {
                return;
            }

            int current = GetPassiveLevel(passive);

            if (current == 0 && !HasRoomForPassive)
            {
                Debug.LogWarning($"Passive slots are full, cannot add '{passive.displayName}'.", this);
                return;
            }

            if (current >= passive.MaxLevel)
            {
                return;
            }

            int next = current + 1;
            if (current == 0)
            {
                passiveOrder.Add(passive);
                AchievementProgress.MarkCollectedPassive(passive);
            }

            passiveLevels[passive] = next;

            // Only the new level's modifiers are applied; earlier levels are already in.
            stats.AddModifiers(passive.GetLevel(next).modifiers);
            Changed?.Invoke();
        }

        public void CollectPassives(List<PassiveDefinition> results, List<int> levels)
        {
            results.Clear();
            levels.Clear();
            for (int i = 0; i < passiveOrder.Count; i++)
            {
                PassiveDefinition passive = passiveOrder[i];
                if (passive != null && passiveLevels.TryGetValue(passive, out int level))
                {
                    results.Add(passive);
                    levels.Add(level);
                }
            }
        }
    }
}
