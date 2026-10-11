using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Enemies;

namespace KingsRescue.Combat
{
    /// <summary>
    /// All weapon queries run over <see cref="HitRegistry"/> with squared-distance tests
    /// rather than physics. No colliders, no layer matrix, no Rigidbody2D on 140 enemies.
    /// Aiming deliberately looks at enemies only, so the hero never turns to face a pot.
    /// </summary>
    public static class Targeting
    {
        private static readonly List<Health> Scratch = new List<Health>(64);

        /// <summary>Closest living enemy within range, or null. Ignores props.</summary>
        public static Enemy FindNearest(Vector2 from, float maxRange = float.PositiveInfinity)
        {
            Enemy best = null;
            float bestSqr = maxRange * maxRange;

            List<Enemy> active = Enemy.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy enemy = active[i];
                if (enemy == null || !enemy.IsAlive || (enemy.Health != null && enemy.Health.Untargetable))
                {
                    continue;
                }

                float sqr = ((Vector2)enemy.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = enemy;
                }
            }

            return best;
        }

        /// <summary>Living, targetable enemies within range. Order is undefined.</summary>
        public static void CollectLiving(Vector2 from, float maxRange, List<Enemy> results)
        {
            results.Clear();
            float maxSqr = maxRange * maxRange;
            List<Enemy> active = Enemy.Active;

            for (int i = 0; i < active.Count; i++)
            {
                Enemy enemy = active[i];
                if (enemy == null || !enemy.IsAlive || (enemy.Health != null && enemy.Health.Untargetable))
                {
                    continue;
                }

                if (((Vector2)enemy.transform.position - from).sqrMagnitude <= maxSqr)
                {
                    results.Add(enemy);
                }
            }
        }

        /// <summary>Fills <paramref name="results"/> with living targets whose bodies touch the circle.</summary>
        public static void Overlap(Vector2 center, float radius, List<Health> results)
        {
            results.Clear();

            List<Health> targets = HitRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                Health target = targets[i];
                if (target == null || !target.IsAlive || target.Untargetable)
                {
                    continue;
                }

                if (target.Overlaps(center, radius))
                {
                    results.Add(target);
                }
            }
        }

        /// <summary>Damages everything in a circle. Returns how many targets were hit.</summary>
        public static int DamageCircle(Vector2 center, float radius, float damage, float knockback, GameObject source)
        {
            Overlap(center, radius, Scratch);

            for (int i = 0; i < Scratch.Count; i++)
            {
                Health target = Scratch[i];
                Vector2 position = target.Position;
                Vector2 direction = position - center;
                direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;

                target.TakeDamage(new DamageInfo(damage, position, direction, knockback, source));
            }

            int count = Scratch.Count;
            Scratch.Clear();
            return count;
        }

        /// <summary>
        /// Damages everything in a wedge. Used by the hero's sword swing so hits read as
        /// a slash in front of him rather than a silent ring.
        /// </summary>
        public static int DamageArc(Vector2 center, Vector2 facing, float radius, float arcDegrees, float damage, float knockback, GameObject source)
        {
            Overlap(center, radius, Scratch);

            float halfArcCos = Mathf.Cos(Mathf.Clamp(arcDegrees, 1f, 360f) * 0.5f * Mathf.Deg2Rad);
            bool fullCircle = arcDegrees >= 359f;
            Vector2 forward = facing.sqrMagnitude > 0.0001f ? facing.normalized : Vector2.up;

            int hits = 0;
            for (int i = 0; i < Scratch.Count; i++)
            {
                Health target = Scratch[i];
                Vector2 position = target.Position;
                Vector2 toTarget = position - center;
                Vector2 direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : forward;

                if (!fullCircle && !InSwing(center, forward, halfArcCos, target, direction))
                {
                    continue;
                }

                target.TakeDamage(new DamageInfo(damage, position, direction, knockback, source));
                hits++;
            }

            Scratch.Clear();
            return hits;
        }

        /// <summary>
        /// Enemies use the facing wedge. A prop also counts when the swing covers its sprite,
        /// or when the hero is already standing on that sprite.
        /// </summary>
        private static bool InSwing(Vector2 center, Vector2 forward, float halfArcCos, Health target, Vector2 pivotDirection)
        {
            if (Vector2.Dot(forward, pivotDirection) >= halfArcCos)
            {
                return true;
            }

            if (!target.Scenery)
            {
                return false;
            }

            Vector2 closest = target.ClosestPoint(center);
            if ((closest - center).sqrMagnitude <= 0.0001f)
            {
                return true;
            }

            Vector2 toBody = target.BodyCenter - center;
            if (toBody.sqrMagnitude > 0.0001f && Vector2.Dot(forward, toBody.normalized) >= halfArcCos)
            {
                return true;
            }

            Vector2 toClosest = closest - center;
            return toClosest.sqrMagnitude > 0.0001f && Vector2.Dot(forward, toClosest.normalized) >= halfArcCos;
        }
    }
}
