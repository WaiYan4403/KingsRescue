using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;

namespace KingsRescue.Player
{
    /// <summary>
    /// Retro arcade power-up: for a few seconds the unique weapon is replaced by
    /// a powered form. Damage, area, cooldown and remaining time all read live
    /// stats, so a mid-buff level-up immediately changes the next throw.
    /// </summary>
    [RequireComponent(typeof(WeaponLoadout))]
    [RequireComponent(typeof(PlayerStats))]
    public class PowerUpController : MonoBehaviour
    {
        private WeaponLoadout loadout;
        private PlayerStats stats;
        private PlayerController controller;
        private WeaponRuntime uniqueRuntime;
        private WeaponRuntime poweredRuntime;
        private float activatedAt;
        private float expireAt;
        private SpriteRenderer flashTarget;
        private Color restColor = Color.white;
        private bool restColorCached;

        public bool IsActive => poweredRuntime != null && Time.time < expireAt;

        private void Awake()
        {
            loadout = GetComponent<WeaponLoadout>();
            stats = GetComponent<PlayerStats>();
            controller = GetComponent<PlayerController>();
            flashTarget = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnDisable()
        {
            EndPowerUp();
        }

        public void Activate()
        {
            CharacterDefinition character = loadout != null ? loadout.Character : null;
            WeaponDefinition powered = character != null ? character.powerUpWeapon : null;
            if (powered == null || stats == null)
            {
                return;
            }

            activatedAt = Time.time;
            expireAt = activatedAt + ResolveDuration(character);
            EnsurePoweredRuntime(powered);
            SuppressUnique(true);
            ApplyPoweredTint(true);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (poweredRuntime == null)
            {
                return;
            }

            CharacterDefinition character = loadout != null ? loadout.Character : null;
            expireAt = activatedAt + ResolveDuration(character);
            if (Time.time >= expireAt)
            {
                EndPowerUp();
                return;
            }

            SyncPoweredLevel();
        }

        private void EnsurePoweredRuntime(WeaponDefinition powered)
        {
            if (poweredRuntime != null)
            {
                poweredRuntime.enabled = true;
                SyncPoweredLevel();
                return;
            }

            var host = new GameObject($"PowerUp [{powered.displayName}]");
            host.transform.SetParent(transform, false);
            host.transform.localPosition = Vector3.zero;
            poweredRuntime = host.AddComponent<WeaponRuntime>();
            poweredRuntime.Initialize(powered, stats, controller, ResolveUniqueLevel());
        }

        private void SyncPoweredLevel()
        {
            if (poweredRuntime != null)
            {
                poweredRuntime.SetLevel(ResolveUniqueLevel());
            }
        }

        private int ResolveUniqueLevel()
        {
            WeaponDefinition unique = loadout != null ? loadout.UniqueWeapon : null;
            int level = unique != null ? loadout.GetWeaponLevel(unique) : 1;
            return Mathf.Max(1, level);
        }

        private float ResolveDuration(CharacterDefinition character)
        {
            float baseline = character != null ? Mathf.Max(1f, character.powerUpDuration) : 8f;
            return baseline * Mathf.Max(0.1f, stats.DurationMultiplier);
        }

        private void SuppressUnique(bool suppress)
        {
            WeaponDefinition unique = loadout != null ? loadout.UniqueWeapon : null;
            if (unique == null || loadout == null || !loadout.Weapons.TryGetValue(unique, out uniqueRuntime))
            {
                uniqueRuntime = null;
                return;
            }

            uniqueRuntime.enabled = !suppress;
        }

        private void EndPowerUp()
        {
            SuppressUnique(false);
            ApplyPoweredTint(false);

            if (poweredRuntime != null)
            {
                Destroy(poweredRuntime.gameObject);
                poweredRuntime = null;
            }

            uniqueRuntime = null;
        }

        private void ApplyPoweredTint(bool powered)
        {
            if (flashTarget == null)
            {
                return;
            }

            if (!restColorCached)
            {
                restColor = flashTarget.color;
                restColorCached = true;
            }

            flashTarget.color = powered ? new Color(1f, 0.82f, 0.35f, restColor.a) : restColor;
        }
    }
}
