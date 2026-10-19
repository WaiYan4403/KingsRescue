using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Flies out along its throw, then comes back to the hero. Each pass can cut again.
    /// </summary>
    public class Boomerang : MonoBehaviour, IPoolable
    {
        [SerializeField] private float hitRadius = 0.4f;
        [SerializeField] private float spinDegreesPerSecond = 720f;

        private readonly List<Health> alreadyHit = new List<Health>(8);
        private readonly List<Health> overlapped = new List<Health>(16);

        private Vector2 velocity;
        private float damage;
        private float knockback;
        private int piercePerPass;
        private int pierceRemaining;
        private float expireAt;
        private float outboundRange;
        private float traveled;
        private float cruiseSpeed;
        private bool returning;
        private GameObject source;
        private float radiusScale = 1f;
        private HitboxMarker hitbox;

        public void OnSpawned()
        {
            alreadyHit.Clear();
            returning = false;
        }

        public void OnDespawned()
        {
            alreadyHit.Clear();
            ReleaseHitbox();
        }

        public void Launch(Vector2 direction, float speed, float damageAmount, int pierce, float lifetime, float areaScale, float knockbackForce, GameObject owner, float maxDistance)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            cruiseSpeed = Mathf.Max(0.1f, speed);
            velocity = dir * cruiseSpeed;
            damage = damageAmount;
            piercePerPass = Mathf.Max(0, pierce);
            pierceRemaining = piercePerPass;
            knockback = knockbackForce;
            radiusScale = Mathf.Max(0.1f, areaScale);
            expireAt = Time.time + Mathf.Max(0.4f, lifetime + 1.5f);
            outboundRange = Mathf.Max(1.5f, maxDistance);
            traveled = 0f;
            returning = false;
            source = owner;
            alreadyHit.Clear();
            transform.rotation = Quaternion.identity;
            ReleaseHitbox();
            hitbox = HitboxDisplay.Follow(transform, hitRadius * radiusScale);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            transform.Rotate(0f, 0f, spinDegreesPerSecond * dt);

            Vector2 previous = transform.position;
            if (returning)
            {
                Vector2 home = source != null ? (Vector2)source.transform.position : previous;
                Vector2 toHome = home - previous;
                if (toHome.sqrMagnitude <= 0.2f * 0.2f || Time.time >= expireAt)
                {
                    Recycle();
                    return;
                }

                velocity = toHome.normalized * cruiseSpeed;
            }

            Vector2 position = previous + velocity * dt;
            transform.position = position;
            if (!returning)
            {
                traveled += velocity.magnitude * dt;
                if (traveled >= outboundRange)
                {
                    BeginReturn();
                }
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null && bounds.IsPastDespawn(position))
            {
                Recycle();
                return;
            }

            SweepHits(previous, position);
        }

        private void BeginReturn()
        {
            returning = true;
            alreadyHit.Clear();
            pierceRemaining = piercePerPass;
        }

        private void SweepHits(Vector2 from, Vector2 to)
        {
            Targeting.OverlapSegment(from, to, hitRadius * radiusScale, overlapped);
            bool blocked = false;
            for (int i = 0; i < overlapped.Count; i++)
            {
                Health target = overlapped[i];
                if (target == null || !target.IsAlive || alreadyHit.Contains(target))
                {
                    continue;
                }

                if (blocked && !target.Scenery)
                {
                    continue;
                }

                alreadyHit.Add(target);
                Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.up;
                target.TakeDamage(PlayerHits.Strike(damage, target.Position, direction, knockback, source));
                if (target.Scenery)
                {
                    continue;
                }

                if (pierceRemaining <= 0)
                {
                    blocked = true;
                }
                else
                {
                    pierceRemaining--;
                }
            }
        }

        private void Recycle()
        {
            ReleaseHitbox();
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ReleaseHitbox()
        {
            if (hitbox == null)
            {
                return;
            }

            HitboxDisplay.Release(hitbox);
            hitbox = null;
        }
    }
}
