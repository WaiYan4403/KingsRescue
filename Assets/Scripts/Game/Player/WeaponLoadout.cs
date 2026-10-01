using System;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Data;

namespace KingsRescue.Player
{
    /// <summary>
    /// What the hero is carrying. Weapons become child objects with a
    /// <see cref="WeaponRuntime"/>; passives fold straight into <see cref="PlayerStats"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class WeaponLoadout : MonoBehaviour
    {
        [SerializeField] private WeaponDefinition startingWeapon;

        [Tooltip("Parent for the weapon child objects. Defaults to this transform.")]
        [SerializeField] private Transform weaponRoot;

        [SerializeField] private int maxWeapons = 6;
        [SerializeField] private int maxPassives = 6;

        private readonly Dictionary<WeaponDefinition, WeaponRuntime> weapons = new Dictionary<WeaponDefinition, WeaponRuntime>();
        private readonly Dictionary<PassiveDefinition, int> passiveLevels = new Dictionary<PassiveDefinition, int>();

        private PlayerStats stats;
        private PlayerController controller;

        public event Action Changed;

        public int WeaponCount => weapons.Count;

        public int PassiveCount => passiveLevels.Count;

        public int MaxWeapons => maxWeapons;

        public int MaxPassives => maxPassives;

        public IReadOnlyDictionary<WeaponDefinition, WeaponRuntime> Weapons => weapons;

        public IReadOnlyDictionary<PassiveDefinition, int> Passives => passiveLevels;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();

            if (weaponRoot == null)
            {
                weaponRoot = transform;
            }
        }

        private void Start()
        {
            if (startingWeapon != null)
            {
                AddOrLevelWeapon(startingWeapon);
            }
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
            if (weapon == null)
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
            Changed?.Invoke();
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
            passiveLevels[passive] = next;

            // Only the new level's modifiers are applied; earlier levels are already in.
            stats.AddModifiers(passive.GetLevel(next).modifiers);
            Changed?.Invoke();
        }
    }
}
