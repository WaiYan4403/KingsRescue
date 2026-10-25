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
        private PlayerHealth health;
        private float activatedAt;
        private float expireAt;
        private float hiddenUntil;
        private SpriteRenderer[] bodySprites;
        private Color[] restColors;
        private bool restColorsCached;
        private bool stealthVisual;
        private Stage stage;

        /// <summary>True while the max power-up's stealth is up. Enemies lose their aim.</summary>
        public static bool IsHeroHidden { get; private set; }

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

        /// <summary>Fixed extra shots for the active stage. The projectile-count passive does not add to this.</summary>
        public int ExtraProjectiles
        {
            get
            {
                if (!IsActive || loadout == null || loadout.Character == null)
                {
                    return 0;
                }

                CharacterDefinition character = loadout.Character;
                return stage == Stage.Final
                    ? Mathf.Max(0, character.powerUpMaxExtraProjectiles)
                    : Mathf.Max(0, character.powerUpExtraProjectiles);
            }
        }

        private void Awake()
        {
            loadout = GetComponent<WeaponLoadout>();
            stats = GetComponent<PlayerStats>();
            health = GetComponent<PlayerHealth>();
            CacheBodySprites();
        }

        private void OnDisable()
        {
            EndPowerUp();
            IsHeroHidden = false;
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
            HealFor(character, next);
            if (next == Stage.Final && character.powerUpMaxStealthDuration > 0f)
            {
                hiddenUntil = Time.time + character.powerUpMaxStealthDuration;
                IsHeroHidden = true;
            }
            else
            {
                hiddenUntil = 0f;
                IsHeroHidden = false;
            }
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
            IsHeroHidden = Time.time < hiddenUntil;
            if (Time.time >= expireAt)
            {
                EndPowerUp();
            }
        }

        private void LateUpdate()
        {
            if (!IsHeroHidden)
            {
                if (stealthVisual)
                {
                    stealthVisual = false;
                    RestoreSpriteColors();
                    ApplyPoweredTint(stage != Stage.None);
                }

                return;
            }

            stealthVisual = true;
            float alpha = 0.18f + 0.42f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 22f));
            ApplyStealthAlpha(alpha);
        }

        private float ResolveDuration(CharacterDefinition character)
        {
            float baseline = character != null ? Mathf.Max(1f, character.powerUpDuration) : 8f;
            return baseline * Mathf.Max(0.1f, stats.DurationMultiplier);
        }

        private void HealFor(CharacterDefinition character, Stage next)
        {
            if (health == null || character == null || !health.IsAlive)
            {
                return;
            }

            float fraction = next == Stage.Final ? character.powerUpMaxHealFraction : character.powerUpHealFraction;
            if (fraction <= 0f)
            {
                return;
            }

            health.Heal(health.Max * fraction);
        }

        private void EndPowerUp()
        {
            stage = Stage.None;
            hiddenUntil = 0f;
            IsHeroHidden = false;
            if (stealthVisual)
            {
                stealthVisual = false;
                RestoreSpriteColors();
            }

            ApplyPoweredTint(false);
        }

        private void CacheBodySprites()
        {
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.name != "Effect")
                {
                    count++;
                }
            }

            bodySprites = new SpriteRenderer[count];
            restColors = new Color[count];
            int write = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].gameObject.name != "Effect")
                {
                    bodySprites[write++] = all[i];
                }
            }
        }

        private void RememberRestColors()
        {
            if (restColorsCached || bodySprites == null)
            {
                return;
            }

            for (int i = 0; i < bodySprites.Length; i++)
            {
                restColors[i] = bodySprites[i] != null ? bodySprites[i].color : Color.white;
            }

            restColorsCached = true;
        }

        private void ApplyPoweredTint(bool powered)
        {
            if (bodySprites == null || IsHeroHidden)
            {
                return;
            }

            RememberRestColors();
            for (int i = 0; i < bodySprites.Length; i++)
            {
                SpriteRenderer renderer = bodySprites[i];
                if (renderer == null)
                {
                    continue;
                }

                Color rest = restColors[i];
                renderer.color = powered ? new Color(1f, 0.82f, 0.35f, rest.a) : rest;
            }
        }

        private void ApplyStealthAlpha(float alpha)
        {
            if (bodySprites == null)
            {
                return;
            }

            RememberRestColors();
            for (int i = 0; i < bodySprites.Length; i++)
            {
                SpriteRenderer renderer = bodySprites[i];
                if (renderer == null)
                {
                    continue;
                }

                Color color = restColors[i];
                color.a = alpha;
                renderer.color = color;
            }
        }

        private void RestoreSpriteColors()
        {
            if (!restColorsCached || bodySprites == null)
            {
                return;
            }

            for (int i = 0; i < bodySprites.Length; i++)
            {
                if (bodySprites[i] != null)
                {
                    bodySprites[i].color = restColors[i];
                }
            }
        }
    }
}
