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
        private Enemy homeTarget;
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
            HitboxDisplay.Follow(transform, hitRadius * radiusScale);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (homing)
            {
                SteerTowardNearest(dt);
            }

            float step = velocity.magnitude * dt;
            Vector2 position = (Vector2)transform.position + velocity * dt;
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

            CheckHits(position);
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

        private void CheckHits(Vector2 position)
        {
            // Snapshot first: a lethal hit unregisters the target and would otherwise
            // mutate the registry while we walk it.
            Targeting.Overlap(position, hitRadius * radiusScale, overlapped);

            for (int i = 0; i < overlapped.Count; i++)
            {
                Health target = overlapped[i];
                if (target == null || !target.IsAlive || alreadyHit.Contains(target))
                {
                    continue;
                }

                alreadyHit.Add(target);
                Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.up;
                target.TakeDamage(new DamageInfo(damage, target.Position, direction, knockback, source));

                if (pierceRemaining <= 0)
                {
                    Recycle(true);
                    return;
                }

                pierceRemaining--;
            }
        }

        private void Recycle(bool spawnImpact)
        {
            if (spawnImpact && impactEffectPrefab != null && PoolManager.Instance != null)
            {
                PoolManager.Instance.Spawn(impactEffectPrefab, transform.position, Quaternion.identity);
            }

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
