using System;
using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.Player
{
    /// <summary>
    /// Player hit points with invulnerability frames, flat armour and passive regen.
    /// Separate from <see cref="Health"/> because only the player needs i-frames and
    /// only the player's maximum is driven by upgrades.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Tooltip("Seconds of immunity after taking a hit. Without this, standing in a crowd is instant death.")]
        [SerializeField] private float invulnerabilityDuration = 0.5f;

        [SerializeField] private SpriteRenderer flashTarget;
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private Animator legAnimator;
        [SerializeField] private string healthParameter = "Health";
        [SerializeField] private string dieStateName = "Lancer_Die";
        [SerializeField] private Color hurtFlashColor = new Color(1f, 0.15f, 0.12f, 1f);
        [SerializeField] private bool useHealFlash = false;
        [SerializeField] private Color healFlashColor = new Color(0.45f, 1f, 0.55f, 1f);
        [SerializeField, Range(0.1f, 1f)] private float reviveHealthFraction = 0.5f;
        [SerializeField] private float reviveInvulnerability = 2.5f;

        private PlayerStats stats;
        private WeaponLoadout loadout;
        private SpriteRenderer[] hurtRenderers;
        private bool hurtFlashOn;
        private bool healFlashOn;
        private float invulnerableUntil;
        private float regenCarry;
        private int extraLives;
        private bool extraLivesBound;
        private bool reviveUsed;

        public float Current { get; private set; }

        public float Max { get; private set; }

        public float Normalised => Max > 0f ? Mathf.Clamp01(Current / Max) : 0f;

        public bool IsAlive => Current > 0f;

        public bool IsInvulnerable => Time.time < invulnerableUntil;

        /// <summary>Current, max.</summary>
        public event Action<float, float> HealthChanged;

        public event Action Damaged;

        public event Action Died;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
            loadout = GetComponent<WeaponLoadout>();

            CacheHurtRenderers();
        }

        private void OnEnable()
        {
            stats.StatsChanged += OnStatsChanged;
        }

        private void OnDisable()
        {
            stats.StatsChanged -= OnStatsChanged;
        }

        private void Start()
        {
            Max = stats.MaxHp;
            Current = Max;
            ApplyCharacter(ResolveCharacter());
            HealthChanged?.Invoke(Current, Max);
            SyncAnimatorHealth();
        }

        public void ApplyCharacter(CharacterDefinition character)
        {
            if (character == null || reviveUsed)
            {
                return;
            }

            extraLives = Mathf.Max(0, character.extraLives);
            extraLivesBound = true;
        }

        private void Update()
        {
            if (!IsAlive)
            {
                return;
            }

            ApplyRegen();
            UpdateInvulnerabilityVisual();
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || IsInvulnerable || info.Amount <= 0f)
            {
                return;
            }

            float mitigated = Mathf.Max(1f, info.Amount - stats.Armor);
            if (mitigated >= Current && TryRevive())
            {
                return;
            }

            Current = Mathf.Max(0f, Current - mitigated);
            invulnerableUntil = Time.time + invulnerabilityDuration;

            HealthChanged?.Invoke(Current, Max);
            Damaged?.Invoke();
            ScreenShake.Add(0.7f);
            SyncAnimatorHealth();

            if (Current <= 0f)
            {
                DieFromDamage();
            }
            else
            {
                SoundEffects.Play(SoundId.PlayerHurt);
            }
        }

        /// <summary>Poison / standing in a zone. Does not grant i-frames.</summary>
        public void ApplyDot(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - amount);
            HealthChanged?.Invoke(Current, Max);
            SyncAnimatorHealth();
            if (Current <= 0f)
            {
                DieFromDamage();
            }
        }

        private void DieFromDamage()
        {
            if (GameMusicController.Instance != null)
            {
                GameMusicController.Instance.SetGameOverMusic();
            }

            healFlashOn = false;
            hurtFlashOn = false;
            PaintBody(null);
            PlayDeathAnimation();
            SoundEffects.Play(SoundId.PlayerDeath);
            Died?.Invoke();
        }

        private bool TryRevive()
        {
            if (!extraLivesBound)
            {
                ApplyCharacter(ResolveCharacter());
            }

            if (extraLives <= 0)
            {
                return false;
            }

            extraLives--;
            reviveUsed = true;
            Current = Mathf.Max(1f, Max * Mathf.Clamp01(reviveHealthFraction));
            invulnerableUntil = Time.time + Mathf.Max(invulnerabilityDuration, reviveInvulnerability);
            healFlashOn = false;
            hurtFlashOn = false;
            PaintBody(null);
            HealthChanged?.Invoke(Current, Max);
            Damaged?.Invoke();
            SyncAnimatorHealth();
            SoundEffects.Play(SoundId.PlayerHurt);
            return true;
        }

        private CharacterDefinition ResolveCharacter()
        {
            if (loadout != null && loadout.Character != null)
            {
                return loadout.Character;
            }

            if (RunSession.SelectedCharacter != null)
            {
                return RunSession.SelectedCharacter;
            }

            GameManager game = GameManager.Instance;
            return game != null ? game.SelectedCharacter : null;
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            float before = Current;
            Current = Mathf.Min(Max, Current + amount);
            if (Current <= before)
            {
                return;
            }

            HealthChanged?.Invoke(Current, Max);
            SoundEffects.Play(SoundId.PlayerHeal);
        }

        /// <summary>Green overlay held by the heal burst until its particles finish.</summary>
        public void SetHealFlash(bool on)
        {
            healFlashOn = on;
            if (!IsInvulnerable && useHealFlash)
            {
                PaintBody(on ? healFlashColor : (Color?)null);
            }
        }

        private void OnStatsChanged()
        {
            float newMax = stats.MaxHp;
            float delta = newMax - Max;
            Max = newMax;

            // Max HP upgrades should also hand over the health they promise.
            Current = Mathf.Clamp(Current + Mathf.Max(0f, delta), 0f, Max);
            HealthChanged?.Invoke(Current, Max);
        }

        private void ApplyRegen()
        {
            float regen = stats.HpRegen;
            if (regen <= 0f || Current >= Max)
            {
                regenCarry = 0f;
                return;
            }

            regenCarry += regen * Time.deltaTime;
            if (regenCarry < 0.5f)
            {
                return;
            }

            Current = Mathf.Min(Max, Current + regenCarry);
            regenCarry = 0f;
            HealthChanged?.Invoke(Current, Max);
        }

        private void UpdateInvulnerabilityVisual()
        {
            if (IsInvulnerable)
            {
                // Same cadence as the old alpha blink: red on the "dim" half of each cycle.
                bool hurt = Mathf.Repeat(Time.time * 12f, 2f) >= 1f;
                hurtFlashOn = hurt;
                PaintBody(hurt ? hurtFlashColor : (Color?)null);
            }
            else if (hurtFlashOn || (useHealFlash && healFlashOn))
            {
                hurtFlashOn = false;
                PaintBody(useHealFlash && healFlashOn ? healFlashColor : (Color?)null);
            }

            SyncAnimatorHealth();
        }

        private void SyncAnimatorHealth()
        {
            if (legAnimator != null && bodyAnimator != null)
            {
                legAnimator.SetInteger(healthParameter, (int)Current);
                bodyAnimator.SetInteger(healthParameter, (int)Current);
            }
        }

        private void PlayDeathAnimation()
        {
            if (legAnimator == null && bodyAnimator == null)
            {
                return;
            }

            // Game over freezes scaled time; keep the death clip running anyway.
            legAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            bodyAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            legAnimator.SetInteger(healthParameter, 0);
            bodyAnimator.SetInteger(healthParameter, 0);
            if (!string.IsNullOrEmpty(dieStateName))
            {
                legAnimator.Play(dieStateName, 0, 0f);
                bodyAnimator.Play(dieStateName, 0, 0f);
            }
        }

        private void CacheHurtRenderers()
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

            if (count == 0)
            {
                hurtRenderers = flashTarget != null ? new[] { flashTarget } : all;
            }
            else
            {
                hurtRenderers = new SpriteRenderer[count];
                int write = 0;
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] != null && all[i].gameObject.name != "Effect")
                    {
                        hurtRenderers[write++] = all[i];
                    }
                }
            }

            for (int i = 0; i < hurtRenderers.Length; i++)
            {
                SpriteFlash.Prepare(hurtRenderers[i]);
            }
        }

        private void PaintBody(Color? color)
        {
            if (hurtRenderers == null)
            {
                return;
            }

            for (int i = 0; i < hurtRenderers.Length; i++)
            {
                SpriteRenderer renderer = hurtRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (color.HasValue)
                {
                    SpriteFlash.Apply(renderer, color.Value, 1f);
                }
                else
                {
                    SpriteFlash.Clear(renderer);
                }
            }
        }
    }
}
