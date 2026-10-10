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

        [Tooltip("Each hit removes 1 HP regardless of weapon damage. Used by pots and chests so they crack before they open.")]
        [SerializeField] private bool stepwiseHits;

        public float Max => maxHealth;

        public float HitRadius => hitRadius;

        public Vector2 Position => transform.position;

        public float Current { get; private set; }

        public float Normalised => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        public bool IsAlive => Current > 0f;

        /// <summary>When true, weapons skip this body (jump / vanish windows).</summary>
        public bool Untargetable { get; set; }

        /// <summary>Shield goblins: most of a hit from the facing side is ignored.</summary>
        public bool BlocksFrontal { get; set; }

        public bool FacingLeft { get; set; }

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
            Untargetable = false;
            BlocksFrontal = false;
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
            if (!IsAlive || Untargetable || info.Amount <= 0f)
            {
                return;
            }

            float amount = stepwiseHits ? 1f : info.Amount;
            if (BlocksFrontal && IsFrontal(info.Direction))
            {
                amount *= 0.22f;
            }

            Current = Mathf.Max(0f, Current - amount);
            Damaged?.Invoke(this, info);

            if (Current <= 0f)
            {
                Died?.Invoke(this, info);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f)
            {
                return;
            }

            Current = Mathf.Min(maxHealth, Current + amount);
        }

        private bool IsFrontal(Vector2 incoming)
        {
            if (Mathf.Abs(incoming.x) < 0.12f)
            {
                return false;
            }

            bool fromLeft = incoming.x > 0f;
            return FacingLeft ? fromLeft : !fromLeft;
        }
    }
}
