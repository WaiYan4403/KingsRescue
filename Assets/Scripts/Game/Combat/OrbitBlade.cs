using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// A blade circling the hero. Keeps its own per-target re-hit timer so a single
    /// pass through a crowd does not delete the whole crowd at once.
    /// </summary>
    public class OrbitBlade : MonoBehaviour, IPoolable
    {
        [SerializeField] private float hitRadius = 0.45f;
        [SerializeField] private bool spinSprite = true;
        [SerializeField] private float spinDegreesPerSecond = 220f;

        private readonly Dictionary<Health, float> nextHitTime = new Dictionary<Health, float>(16);
        private readonly List<Health> expired = new List<Health>(8);
        private readonly List<Health> overlapped = new List<Health>(16);

        private float damage;
        private float knockback;
        private float rehitInterval = 0.45f;
        private float radiusScale = 1f;
        private GameObject source;
        private float nextPrune;

        public void OnSpawned()
        {
            nextHitTime.Clear();
        }

        public void OnDespawned()
        {
            nextHitTime.Clear();
        }

        public void Configure(float damageAmount, float areaScale, float knockbackForce, float rehit, GameObject owner)
        {
            damage = damageAmount;
            radiusScale = Mathf.Max(0.1f, areaScale);
            knockback = knockbackForce;
            rehitInterval = Mathf.Max(0.05f, rehit);
            source = owner;
        }

        private void Update()
        {
            if (spinSprite)
            {
                transform.Rotate(0f, 0f, spinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            Vector2 position = transform.position;
            float now = Time.time;

            // Snapshot first: a lethal hit unregisters the target mid-sweep.
            Targeting.Overlap(position, hitRadius * radiusScale, overlapped);

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
                target.TakeDamage(new DamageInfo(damage, targetPosition, direction, knockback, source));
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
