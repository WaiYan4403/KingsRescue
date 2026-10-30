using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Combat;
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
        private GameObject normalEffect;
        private GameObject maxEffect;
        private SpriteRenderer bodyRenderer;
        private SpriteRenderer legRenderer;
        private Material bodyOutline;
        private Material legOutline;
        private Material bodyShared;
        private Material legShared;
        private bool legBorrowedOutline;

        /// <summary>True while the max power-up's stealth is up. Enemies lose their aim.</summary>
        public static bool IsHeroHidden { get; private set; }

        private static readonly int EnableOutlineId = Shader.PropertyToID("_EnableOutline");
        private static readonly int OutlineColorId = Shader.PropertyToID("_Color");
        private static readonly int OutlineColorExtraId = Shader.PropertyToID("_Outline_Color");
        private static readonly Color DefaultOutline = Color.black;
        private static readonly Color NormalOutline = new Color(0.2f, 0.92f, 1f, 1f);
        private static readonly Color MaxOutline = new Color(0.7f, 0.25f, 1f, 1f);

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
            CachePowerVisuals();
            CacheBodySprites();
            PrepareOutlines();
            ShowEffect(normalEffect, false);
            ShowEffect(maxEffect, false);
        }

        private void OnDestroy()
        {
            if (bodyRenderer != null && bodyShared != null)
            {
                bodyRenderer.sharedMaterial = bodyShared;
            }

            if (legRenderer != null && legShared != null && !legBorrowedOutline)
            {
                legRenderer.sharedMaterial = legShared;
            }

            if (bodyOutline != null)
            {
                Destroy(bodyOutline);
            }

            if (legOutline != null)
            {
                Destroy(legOutline);
            }
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
            bool max = next == Stage.Final || stage == Stage.Final;
            SoundEffects.Play(max ? SoundId.PowerUpMax : SoundId.PowerUp);

            // A normal pickup must not downgrade a max power-up that is already running.
            if (next == Stage.Slash && stage == Stage.Final)
            {
                ApplyPoweredTint(true);
                ShowPowerVisual(true);
                return;
            }

            stage = next;
            ApplyPoweredTint(true);
            ShowPowerVisual(next == Stage.Final);
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
            HidePowerVisual();
        }

        private void CacheBodySprites()
        {
            SpriteRenderer[] all = GetComponentsInChildren<SpriteRenderer>(true);
            int count = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (IsBodySprite(all[i]))
                {
                    count++;
                }
            }

            bodySprites = new SpriteRenderer[count];
            restColors = new Color[count];
            int write = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (IsBodySprite(all[i]))
                {
                    bodySprites[write++] = all[i];
                }
            }
        }

        private static bool IsBodySprite(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            string name = renderer.gameObject.name;
            return name != "Effect" && name != "PowerUp Normal" && name != "PowerUp Max";
        }

        private void CachePowerVisuals()
        {
            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                switch (renderer.gameObject.name)
                {
                    case "PowerUp Normal":
                        normalEffect = renderer.gameObject;
                        break;
                    case "PowerUp Max":
                        maxEffect = renderer.gameObject;
                        break;
                    case "Body":
                        bodyRenderer = renderer;
                        break;
                    case "Leg":
                        legRenderer = renderer;
                        break;
                }
            }
        }

        private void PrepareOutlines()
        {
            bodyShared = bodyRenderer != null ? bodyRenderer.sharedMaterial : null;
            bodyOutline = CreateOutline(bodyRenderer);
            if (bodyRenderer != null && bodyOutline != null)
            {
                PaintOutline(bodyOutline, DefaultOutline, false);
                bodyRenderer.sharedMaterial = bodyOutline;
            }

            if (legRenderer != null && legRenderer.sharedMaterial != null && legRenderer.sharedMaterial.HasProperty(EnableOutlineId))
            {
                legShared = legRenderer.sharedMaterial;
                legOutline = CreateOutline(legRenderer);
                PaintOutline(legOutline, DefaultOutline, false);
                legRenderer.sharedMaterial = legOutline;
            }
            else if (legRenderer != null && bodyOutline != null)
            {
                legOutline = new Material(bodyOutline);
                legBorrowedOutline = true;
                PaintOutline(legOutline, DefaultOutline, false);
                legRenderer.sharedMaterial = legOutline;
            }
        }

        private static Material CreateOutline(SpriteRenderer renderer)
        {
            Material source = renderer != null ? renderer.sharedMaterial : null;
            if (source == null || !source.HasProperty(EnableOutlineId))
            {
                return null;
            }

            return new Material(source);
        }

        private void ShowPowerVisual(bool max)
        {
            ShowEffect(normalEffect, !max);
            ShowEffect(maxEffect, max);
            Color color = max ? MaxOutline : NormalOutline;
            ApplyOutline(bodyRenderer, bodyOutline, color, true);
            ApplyOutline(legRenderer, LegOutlineMaterial(), color, true);
        }

        private void HidePowerVisual()
        {
            ShowEffect(normalEffect, false);
            ShowEffect(maxEffect, false);
            ApplyOutline(bodyRenderer, bodyOutline, DefaultOutline, false);
            ApplyOutline(legRenderer, legOutline, DefaultOutline, false);
        }

        private Material LegOutlineMaterial()
        {
            if (legOutline != null)
            {
                return legOutline;
            }

            if (legRenderer == null || bodyOutline == null)
            {
                return null;
            }

            legOutline = new Material(bodyOutline);
            legBorrowedOutline = true;
            return legOutline;
        }

        private static void ApplyOutline(SpriteRenderer renderer, Material outline, Color color, bool enabled)
        {
            if (renderer == null || outline == null)
            {
                return;
            }

            PaintOutline(outline, color, enabled);
            SpriteFlash.ReplaceParked(renderer, outline);
        }

        private static void PaintOutline(Material outline, Color color, bool enabled)
        {
            if (outline == null)
            {
                return;
            }

            outline.SetFloat(EnableOutlineId, enabled ? 1f : 0f);
            if (outline.HasProperty(OutlineColorId))
            {
                outline.SetColor(OutlineColorId, color);
            }

            if (outline.HasProperty(OutlineColorExtraId))
            {
                outline.SetColor(OutlineColorExtraId, color);
            }
        }

        private static void ShowEffect(GameObject effect, bool show)
        {
            if (effect == null)
            {
                return;
            }

            effect.SetActive(show);
            if (!show)
            {
                return;
            }

            Animator animator = effect.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.Play(0, 0, 0f);
            animator.Update(0f);
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
