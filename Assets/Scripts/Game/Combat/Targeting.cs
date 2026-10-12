using System;
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

        private const float Cell = 3f;
        private const float InvCell = 1f / Cell;

        private static readonly Dictionary<int, int> cellHead = new Dictionary<int, int>(256);
        private static int[] cellNext = new int[1024];
        private static Health[] cellBody = new Health[1024];
        private static int cellUsed;
        private static int gridFrame = -1;
        private static int queryStamp = 1;

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
            EnsureGrid();
            int token = NextStamp();
            int x0 = Mathf.FloorToInt((center.x - radius) * InvCell);
            int x1 = Mathf.FloorToInt((center.x + radius) * InvCell);
            int y0 = Mathf.FloorToInt((center.y - radius) * InvCell);
            int y1 = Mathf.FloorToInt((center.y + radius) * InvCell);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    CollectCell(CellKey(x, y), token, center, radius, results);
                }
            }
        }

        /// <summary>Living targets touched by a circle swept from <paramref name="from"/> to <paramref name="to"/>.</summary>
        public static void OverlapSegment(Vector2 from, Vector2 to, float radius, List<Health> results)
        {
            results.Clear();
            EnsureGrid();
            int token = NextStamp();
            Vector2 delta = to - from;
            float distSq = delta.sqrMagnitude;
            float minX = Mathf.Min(from.x, to.x) - radius;
            float maxX = Mathf.Max(from.x, to.x) + radius;
            float minY = Mathf.Min(from.y, to.y) - radius;
            float maxY = Mathf.Max(from.y, to.y) + radius;
            int x0 = Mathf.FloorToInt(minX * InvCell);
            int x1 = Mathf.FloorToInt(maxX * InvCell);
            int y0 = Mathf.FloorToInt(minY * InvCell);
            int y1 = Mathf.FloorToInt(maxY * InvCell);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    if (!cellHead.TryGetValue(CellKey(x, y), out int slot))
                    {
                        continue;
                    }

                    while (slot >= 0)
                    {
                        Health target = cellBody[slot];
                        slot = cellNext[slot];
                        if (target.BroadphaseStamp == token)
                        {
                            continue;
                        }

                        target.BroadphaseStamp = token;
                        if (!target.IsAlive || target.Untargetable)
                        {
                            continue;
                        }

                        Vector2 closest = from;
                        if (distSq > 0.0001f)
                        {
                            float t = Mathf.Clamp01(Vector2.Dot(target.Position - from, delta) / distSq);
                            closest = from + delta * t;
                        }

                        if (target.Overlaps(closest, radius))
                        {
                            results.Add(target);
                        }
                    }
                }
            }
        }

        private static void EnsureGrid()
        {
            // Once per frame. Rebuilding after every kill made a crowded clear stall,
            // and a body that dies mid-frame is skipped by the alive check.
            if (gridFrame == Time.frameCount)
            {
                return;
            }

            gridFrame = Time.frameCount;
            cellHead.Clear();
            cellUsed = 0;

            List<Health> targets = HitRegistry.Targets;
            for (int i = 0; i < targets.Count; i++)
            {
                Health target = targets[i];
                if (target == null || !target.IsAlive || target.Untargetable)
                {
                    continue;
                }

                Vector2 pos = target.Position;
                float body = target.HitRadius;
                int x0 = Mathf.FloorToInt((pos.x - body) * InvCell);
                int x1 = Mathf.FloorToInt((pos.x + body) * InvCell);
                int y0 = Mathf.FloorToInt((pos.y - body) * InvCell);
                int y1 = Mathf.FloorToInt((pos.y + body) * InvCell);
                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        AddToCell(CellKey(x, y), target);
                    }
                }
            }
        }

        private static void AddToCell(int key, Health target)
        {
            if (cellUsed >= cellBody.Length)
            {
                int size = cellBody.Length * 2;
                Array.Resize(ref cellNext, size);
                Array.Resize(ref cellBody, size);
            }

            int slot = cellUsed++;
            cellBody[slot] = target;
            cellNext[slot] = cellHead.TryGetValue(key, out int head) ? head : -1;
            cellHead[key] = slot;
        }

        private static void CollectCell(int key, int token, Vector2 center, float radius, List<Health> results)
        {
            if (!cellHead.TryGetValue(key, out int slot))
            {
                return;
            }

            while (slot >= 0)
            {
                Health target = cellBody[slot];
                slot = cellNext[slot];
                if (target.BroadphaseStamp == token)
                {
                    continue;
                }

                target.BroadphaseStamp = token;
                if (target.IsAlive && !target.Untargetable && target.Overlaps(center, radius))
                {
                    results.Add(target);
                }
            }
        }

        private static int CellKey(int x, int y)
        {
            return ((x + 1024) << 16) | ((y + 1024) & 0xFFFF);
        }

        private static int NextStamp()
        {
            queryStamp++;
            if (queryStamp == int.MaxValue)
            {
                queryStamp = 1;
                List<Health> targets = HitRegistry.Targets;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (targets[i] != null)
                    {
                        targets[i].BroadphaseStamp = 0;
                    }
                }
            }

            return queryStamp;
        }

        /// <summary>Damages everything a forward lance crosses. Returns how many targets were hit.</summary>
        public static int DamageSegment(Vector2 from, Vector2 to, float radius, float damage, float knockback, GameObject source)
        {
            OverlapSegment(from, to, radius, Scratch);

            Vector2 direction = to - from;
            direction = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;

            for (int i = 0; i < Scratch.Count; i++)
            {
                Health target = Scratch[i];
                target.TakeDamage(new DamageInfo(damage, target.Position, direction, knockback, source));
            }

            int count = Scratch.Count;
            Scratch.Clear();
            return count;
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
