using System;
using UnityEngine;
using KingsRescue.Combat;

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
        [SerializeField, Range(0f, 1f)] private float invulnerableAlpha = 0.45f;

        private PlayerStats stats;
        private float invulnerableUntil;
        private float regenCarry;

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

            if (flashTarget == null)
            {
                flashTarget = GetComponentInChildren<SpriteRenderer>();
            }
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
            HealthChanged?.Invoke(Current, Max);
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
            Current = Mathf.Max(0f, Current - mitigated);
            invulnerableUntil = Time.time + invulnerabilityDuration;

            HealthChanged?.Invoke(Current, Max);
            Damaged?.Invoke();
            SyncAnimatorHealth();

            if (Current <= 0f)
            {
                // Switch Music
                GameMusicController.Instance.SetGameOverMusic();
                RestoreVisual();
                PlayDeathAnimation();
                Died?.Invoke();
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Min(Max, Current + amount);
            HealthChanged?.Invoke(Current, Max);
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
            if (flashTarget == null)
            {
                return;
            }

            if (IsInvulnerable)
            {
                // 12 Hz blink reads clearly at 360x640 without being seizure-inducing.
                bool visible = Mathf.Repeat(Time.time * 12f, 2f) < 1f;
                SetAlpha(visible ? 1f : invulnerableAlpha);
            }
            else
            {
                RestoreVisual();
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

        private void RestoreVisual()
        {
            SetAlpha(1f);
        }

        private void SetAlpha(float alpha)
        {
            if (flashTarget == null)
            {
                return;
            }

            Color c = flashTarget.color;
            if (!Mathf.Approximately(c.a, alpha))
            {
                c.a = alpha;
                flashTarget.color = c;
            }
        }
    }
}
