using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Flames circling the hero. The pivot turns around the player; the flame sprite stays upright.
    /// Each flame keeps its own per-target re-hit timer so one pass does not clear a whole crowd.
    /// </summary>
    public class OrbitBlade : MonoBehaviour, IPoolable
    {
        [SerializeField] private float hitRadius = 0.45f;
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform flame;

        private readonly Dictionary<Health, float> nextHitTime = new Dictionary<Health, float>(16);
        private readonly List<Health> expired = new List<Health>(8);
        private readonly List<Health> overlapped = new List<Health>(16);

        private SpriteRenderer flameRenderer;
        private Animator flameAnimator;
        private HitboxMarker hitbox;
        private float damage;
        private float knockback;
        private float rehitInterval = 0.45f;
        private float radiusScale = 1f;
        private GameObject source;
        private float nextPrune;

        private void Awake()
        {
            if (flame == null)
            {
                return;
            }

            flameRenderer = flame.GetComponent<SpriteRenderer>();
            flameAnimator = flame.GetComponent<Animator>();
        }

        public void OnSpawned()
        {
            nextHitTime.Clear();
            if (flameAnimator != null && flameAnimator.runtimeAnimatorController != null)
            {
                flameAnimator.Rebind();
                flameAnimator.Update(0f);
            }
        }

        public void OnDespawned()
        {
            nextHitTime.Clear();
            HitboxDisplay.Release(hitbox);
            hitbox = null;
        }

        public void Configure(float damageAmount, float areaScale, float knockbackForce, float rehit, GameObject owner)
        {
            damage = damageAmount;
            radiusScale = Mathf.Max(0.1f, areaScale);
            knockback = knockbackForce;
            rehitInterval = Mathf.Max(0.05f, rehit);
            source = owner;
        }

        /// <summary>
        /// The pivot spins around the hero. The flame sits on that arm and stays upright.
        /// </summary>
        public void SetOrbit(Vector2 center, float angleDegrees, float radius)
        {
            transform.SetPositionAndRotation(center, Quaternion.identity);

            if (pivot != null)
            {
                pivot.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
            }

            if (flame == null)
            {
                return;
            }

            flame.localPosition = new Vector3(Mathf.Max(0.1f, radius), 0f, 0f);
            flame.localRotation = Quaternion.Euler(0f, 0f, -angleDegrees);
        }

        private void Update()
        {
            Vector2 position = transform.position;
            if (flameRenderer != null && flameRenderer.sprite != null)
            {
                position = flameRenderer.bounds.center;
            }
            else if (flame != null)
            {
                position = flame.position;
            }

            float now = Time.time;

            // Snapshot first: a lethal hit unregisters the target mid-sweep.
            Targeting.Overlap(position, hitRadius * radiusScale, overlapped);
            HitboxDisplay.SyncCircle(ref hitbox, position, hitRadius * radiusScale);

            for (int i = 0; i < overlapped.Count; i++)
            {
                Health target = overlapped[i];
                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                if (nextHitTime.TryGetValue(target, out float readyAt) && now < readyAt)
                {
                    continue;
                }

                nextHitTime[target] = now + rehitInterval;

                Vector2 targetPosition = target.Position;
                Vector2 direction = targetPosition - position;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
                target.TakeDamage(PlayerHits.Strike(damage, targetPosition, direction, knockback, source));
            }

            PruneTimers(now);
        }

        private void PruneTimers(float now)
        {
            if (now < nextPrune || nextHitTime.Count == 0)
            {
                return;
            }

            nextPrune = now + 2f;

            foreach (KeyValuePair<Health, float> pair in nextHitTime)
            {
                if (pair.Key == null || !pair.Key.IsAlive || now >= pair.Value)
                {
                    expired.Add(pair.Key);
                }
            }

            for (int i = 0; i < expired.Count; i++)
            {
                nextHitTime.Remove(expired[i]);
            }

            expired.Clear();
        }
    }
}
