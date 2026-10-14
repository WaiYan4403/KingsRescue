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

        /// <summary>Pots, boxes and chests. Their picture is the hit target, and they do not spend projectile pierce.</summary>
        public bool Scenery { get; private set; }

        private SpriteRenderer visibleBody;

        public float Current { get; private set; }

        public float Normalised => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        public bool IsAlive => Current > 0f;

        /// <summary>When true, weapons skip this body (jump / vanish windows).</summary>
        public bool Untargetable { get; set; }

        /// <summary>Shield goblins: most of a hit from the facing side is ignored.</summary>
        public bool BlocksFrontal { get; set; }

        public bool FacingLeft { get; set; }

        /// <summary>Stamps a broadphase query so a body that covers several cells is tested once.</summary>
        internal int BroadphaseStamp;

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

        /// <summary>
        /// Pots and boxes never exceed 2 HP. Each landed hit knocks off 1 so a
        /// strong weapon cannot skip the crack sprite, and a weak tick cannot
        /// stall the smash for a dozen hits.
        /// </summary>
        public void ConfigureSmashProp(float newMax)
        {
            maxHealth = Mathf.Clamp(newMax, 1f, 2f);
            stepwiseHits = true;
            Current = maxHealth;
            Untargetable = false;
            BlocksFrontal = false;
        }

        /// <summary>
        /// Breakables are drawn above their pivot. Weapons connect when they touch the sprite,
        /// and the round hitbox grows to cover that sprite.
        /// </summary>
        public void UseVisibleBody(SpriteRenderer renderer)
        {
            Scenery = true;
            visibleBody = renderer;
            if (renderer == null || renderer.sprite == null)
            {
                return;
            }

            Bounds local = renderer.sprite.bounds;
            Vector3 scale = renderer.transform.lossyScale;
            float sx = Mathf.Abs(scale.x);
            float sy = Mathf.Abs(scale.y);
            Vector2 center = new Vector2(local.center.x * sx, local.center.y * sy);
            Vector2 ext = new Vector2(local.extents.x * sx, local.extents.y * sy);
            float farthest = 0f;
            farthest = Mathf.Max(farthest, (center + new Vector2(ext.x, ext.y)).magnitude);
            farthest = Mathf.Max(farthest, (center + new Vector2(-ext.x, ext.y)).magnitude);
            farthest = Mathf.Max(farthest, (center + new Vector2(ext.x, -ext.y)).magnitude);
            farthest = Mathf.Max(farthest, (center - new Vector2(ext.x, ext.y)).magnitude);
            hitRadius = Mathf.Max(hitRadius, farthest);
        }

        public Vector2 BodyCenter
        {
            get
            {
                if (visibleBody != null && visibleBody.sprite != null)
                {
                    return visibleBody.bounds.center;
                }

                return transform.position;
            }
        }

        public Vector2 ClosestPoint(Vector2 center)
        {
            if (visibleBody != null && visibleBody.sprite != null)
            {
                Bounds bounds = visibleBody.bounds;
                return new Vector2(
                    Mathf.Clamp(center.x, bounds.min.x, bounds.max.x),
                    Mathf.Clamp(center.y, bounds.min.y, bounds.max.y));
            }

            return transform.position;
        }

        public bool Overlaps(Vector2 center, float radius)
        {
            float reach = radius + hitRadius;
            if (((Vector2)transform.position - center).sqrMagnitude <= reach * reach)
            {
                return true;
            }

            if (visibleBody == null || visibleBody.sprite == null || !visibleBody.enabled)
            {
                return false;
            }

            Vector2 nearest = ClosestPoint(center);
            return (nearest - center).sqrMagnitude <= radius * radius;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || Untargetable || info.Amount <= 0f)
            {
                return;
            }

            float amount = stepwiseHits ? 1f : info.Amount;
            if (!stepwiseHits && BlocksFrontal && IsFrontal(info.Direction))
            {
                amount *= 0.22f;
            }

            Current = Mathf.Max(0f, Current - amount);
            DamageInfo applied = new DamageInfo(amount, info.HitPoint, info.Direction, info.Knockback, info.Source, info.Critical);
            Damaged?.Invoke(this, applied);

            if (Current <= 0f)
            {
                Died?.Invoke(this, applied);
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
