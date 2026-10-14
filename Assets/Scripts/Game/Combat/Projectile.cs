using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Enemies;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Pooled straight-line shot. Hit detection is a squared-distance sweep over the
    /// active enemy list, matching the rest of the combat code.
    /// </summary>
    public class Projectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private float hitRadius = 0.3f;
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private GameObject impactEffectPrefab;

        private readonly List<Health> alreadyHit = new List<Health>(8);
        private readonly List<Health> overlapped = new List<Health>(16);

        private Vector2 velocity;
        private float damage;
        private float knockback;
        private int pierceRemaining;
        private float expireAt;
        private float maxTravel;
        private float traveled;
        private GameObject source;
        private float radiusScale = 1f;
        private bool homing;
        private float cruiseSpeed;

        /// <summary>Seconds until this shot returns to the pool, by time or by travel distance.</summary>
        public float TimeLeft
        {
            get
            {
                float byTime = expireAt - Time.time;
                float byTravel = maxTravel > 0f && cruiseSpeed > 0.01f
                    ? (maxTravel - traveled) / cruiseSpeed
                    : byTime;
                return Mathf.Max(0f, Mathf.Min(byTime, byTravel));
            }
        }
        private Enemy homeTarget;
        private HitboxMarker hitbox;
        private const float HomingTurn = 7f;

        public void OnSpawned()
        {
            alreadyHit.Clear();
            homing = false;
            homeTarget = null;
        }

        public void OnDespawned()
        {
            alreadyHit.Clear();
            ReleaseHitbox();
        }

        private void ReleaseHitbox()
        {
            if (hitbox != null)
            {
                HitboxDisplay.Release(hitbox);
                hitbox = null;
            }
        }

        public void Launch(Vector2 direction, float speed, float damageAmount, int pierce, float lifetime, float areaScale, float knockbackForce, GameObject owner, float maxDistance = 0f, bool home = false, Enemy lockedTarget = null)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;

            cruiseSpeed = Mathf.Max(0.1f, speed);
            velocity = dir * cruiseSpeed;
            damage = damageAmount;
            pierceRemaining = pierce;
            knockback = knockbackForce;
            radiusScale = Mathf.Max(0.1f, areaScale);
            expireAt = Time.time + Mathf.Max(0.05f, lifetime);
            maxTravel = maxDistance;
            traveled = 0f;
            source = owner;
            homing = home;
            homeTarget = lockedTarget;
            alreadyHit.Clear();

            FaceVelocity(dir);
            ReleaseHitbox();
            hitbox = HitboxDisplay.Follow(transform, hitRadius * radiusScale);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (homing)
            {
                SteerTowardNearest(dt);
            }

            Vector2 previous = transform.position;
            float step = velocity.magnitude * dt;
            Vector2 position = previous + velocity * dt;
            transform.position = position;
            traveled += step;

            if ((maxTravel > 0f && traveled >= maxTravel) || Time.time >= expireAt)
            {
                Recycle(false);
                return;
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null && bounds.IsPastDespawn(position))
            {
                Recycle(false);
                return;
            }

            SweepHits(previous, position);
        }

        private void SteerTowardNearest(float dt)
        {
            Enemy target = homeTarget != null && homeTarget.IsAlive
                ? homeTarget
                : null;
            if (target == null)
            {
                homing = false;
                return;
            }

            Vector2 toTarget = (Vector2)target.transform.position - (Vector2)transform.position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 desired = toTarget.normalized * cruiseSpeed;
            velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-HomingTurn * dt));
            FaceVelocity(velocity);
        }

        private void FaceVelocity(Vector2 dir)
        {
            if (!rotateToDirection || dir.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void SweepHits(Vector2 from, Vector2 to)
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            Targeting.OverlapSegment(from, to, hitRadius * radiusScale, overlapped);
            SortAlong(from, to);

            bool blocked = false;
            for (int i = 0; i < overlapped.Count; i++)
            {
                Health target = overlapped[i];
                if (target == null || !target.IsAlive || alreadyHit.Contains(target))
                {
                    continue;
                }

                // Props never spend pierce. A shot that touches a pot still cracks it
                // when a nearer enemy is what stops the projectile.
                bool scenery = target.Scenery;
                if (blocked && !scenery)
                {
                    continue;
                }

                alreadyHit.Add(target);
                Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.up;
                target.TakeDamage(PlayerHits.Strike(damage, target.Position, direction, knockback, source));

                if (scenery)
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

            if (blocked)
            {
                Recycle(true);
            }
        }

        private void SortAlong(Vector2 from, Vector2 to)
        {
            Vector2 delta = to - from;
            float distSq = delta.sqrMagnitude;
            for (int i = 1; i < overlapped.Count; i++)
            {
                Health key = overlapped[i];
                float keyT = Along(key, from, delta, distSq);
                int j = i - 1;
                while (j >= 0)
                {
                    if (Along(overlapped[j], from, delta, distSq) <= keyT)
                    {
                        break;
                    }

                    overlapped[j + 1] = overlapped[j];
                    j--;
                }

                overlapped[j + 1] = key;
            }
        }

        private static float Along(Health target, Vector2 from, Vector2 delta, float distSq)
        {
            if (target == null)
            {
                return float.MaxValue;
            }

            if (distSq < 0.0001f)
            {
                return (target.Position - from).sqrMagnitude;
            }

            return Vector2.Dot(target.Position - from, delta) / distSq;
        }

        private void Recycle(bool spawnImpact)
        {
            if (spawnImpact && impactEffectPrefab != null && ObjectPoolManager.Instance != null)
            {
                GameObject impact = ObjectPoolManager.Instance.Spawn(impactEffectPrefab, transform.position, Quaternion.identity);
                if (impact != null && impact.GetComponent<AutoDespawn>() == null)
                {
                    ObjectPoolManager.Instance.ReturnDelayed(impact, 0.6f);
                }
            }

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
