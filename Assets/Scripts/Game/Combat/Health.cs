using System;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Hit points for everything that is not the player: enemies and destructible props.
    /// Resets itself on pool reuse.
    /// </summary>
    public class Health : MonoBehaviour, IDamageable, IPoolable
    {
        [SerializeField] private float maxHealth = 10f;

        [Tooltip("Body radius used by weapon sweeps. Bigger targets should be easier to clip.")]
        [SerializeField] private float hitRadius = 0.35f;

        public float Max => maxHealth;

        public float HitRadius => hitRadius;

        public Vector2 Position => transform.position;

        public float Current { get; private set; }

        public float Normalised => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        public bool IsAlive => Current > 0f;

        /// <summary>Raised whenever damage lands and the target survives it.</summary>
        public event Action<Health, DamageInfo> Damaged;

        /// <summary>Raised once, on the hit that drops the target to zero.</summary>
        public event Action<Health, DamageInfo> Died;

        private void Awake()
        {
            Current = maxHealth;
        }

        private void OnEnable()
        {
            HitRegistry.Register(this);
        }

        private void OnDisable()
        {
            HitRegistry.Unregister(this);
        }

        public void OnSpawned()
        {
            Current = maxHealth;
        }

        public void OnDespawned()
        {
        }

        /// <summary>Applies an authored stat block, typically from an EnemyDefinition.</summary>
        public void Configure(float newMax)
        {
            maxHealth = Mathf.Max(1f, newMax);
            Current = maxHealth;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || info.Amount <= 0f)
            {
                return;
            }

            Current = Mathf.Max(0f, Current - info.Amount);

            if (Current > 0f)
            {
                Damaged?.Invoke(this, info);
                return;
            }

            Died?.Invoke(this, info);
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Min(maxHealth, Current + amount);
        }
    }
}
