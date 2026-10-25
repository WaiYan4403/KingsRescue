using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;

namespace KingsRescue.Player
{
    /// <summary>
    /// Arcade power-up in two steps. Both keep the normal combo and fire the
    /// strike's slash straight up the road. The second step uses the same
    /// slashes with a longer reach, drawn larger and hitting harder.
    /// </summary>
    [RequireComponent(typeof(WeaponLoadout))]
    [RequireComponent(typeof(PlayerStats))]
    public class PowerUpController : MonoBehaviour
    {
        private WeaponLoadout loadout;
        private PlayerStats stats;
        private float activatedAt;
        private float expireAt;
        private SpriteRenderer flashTarget;
        private Color restColor = Color.white;
        private bool restColorCached;
        private Stage stage;

        private const float ShortSlashRange = 6f;
        private const float FinalSlashRange = 11f;
        private const float FinalSlashScale = 1.35f;
        private const float FinalSlashDamage = 1.6f;

        private enum Stage
        {
            None,
            Slash,
            Final
        }

        public bool IsActive => stage != Stage.None && Time.time < expireAt;

        /// <summary>A power-up is up, so unlocked strikes also fire a slash straight up.</summary>
        public bool AddsSlashProjectiles => stage != Stage.None;

        public float SlashScale => stage == Stage.Final ? FinalSlashScale : 1f;

        public float SlashDamageMultiplier => stage == Stage.Final ? FinalSlashDamage : 1f;

        public float SlashRange => stage == Stage.Final ? FinalSlashRange : ShortSlashRange;

        private void Awake()
        {
            loadout = GetComponent<WeaponLoadout>();
            stats = GetComponent<PlayerStats>();
            flashTarget = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnDisable()
        {
            EndPowerUp();
        }

        /// <summary>The normal power-up. Refreshes its timer and does not become the max form.</summary>
        public void Activate()
        {
            Begin(Stage.Slash);
        }

        /// <summary>The max power-up. Starts at full strength on its own.</summary>
        public void ActivateMax()
        {
            Begin(Stage.Final);
        }

        private void Begin(Stage next)
        {
            CharacterDefinition character = loadout != null ? loadout.Character : null;
            WeaponDefinition powered = character != null ? character.powerUpWeapon : null;
            if (powered == null || stats == null)
            {
                return;
            }

            activatedAt = Time.time;
            expireAt = activatedAt + ResolveDuration(character);

            // A normal pickup must not downgrade a max power-up that is already running.
            if (next == Stage.Slash && stage == Stage.Final)
            {
                ApplyPoweredTint(true);
                return;
            }

            stage = next;
            ApplyPoweredTint(true);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (stage == Stage.None)
            {
                return;
            }

            CharacterDefinition character = loadout != null ? loadout.Character : null;
            expireAt = activatedAt + ResolveDuration(character);
            if (Time.time >= expireAt)
            {
                EndPowerUp();
            }
        }

        private float ResolveDuration(CharacterDefinition character)
        {
            float baseline = character != null ? Mathf.Max(1f, character.powerUpDuration) : 8f;
            return baseline * Mathf.Max(0.1f, stats.DurationMultiplier);
        }

        private void EndPowerUp()
        {
            stage = Stage.None;
            ApplyPoweredTint(false);
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
