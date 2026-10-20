using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;
using KingsRescue.World;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Enemies steer at the player, fly a locked heading, or hold a shooting ring.
    /// Nearby enemies also get a short-range push so crowds keep personal space.
    /// </summary>
    public class EnemyMovement : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        [Tooltip("Slack in world units around the screen where steering is still allowed.")]
        [SerializeField] private float chaseSlack = 1f;

        [Tooltip("Random per-enemy speed variation so crowds do not move as one block.")]
        [SerializeField, Range(0f, 0.5f)] private float speedJitter = 0.12f;

        [Tooltip("How far below the player counts as 'passed behind'.")]
        [SerializeField] private float passedMargin = 0.35f;

        [Tooltip("Follow speed multiplier while chasing up the screen, against the scroll.")]
        [SerializeField, Range(0.2f, 1f)] private float chaseUpSpeedFactor = 0.5f;

        private float moveSpeed = 2.4f;
        private float scrollInfluence = 1f;
        private float responsiveness = 6f;
        private float followChance = 1f;
        private float giveUpWhenPassedChance = 0.45f;
        private float separationRadius = 0.8f;
        private float separationStrength = 3.25f;
        private float preferredDistance = 4.5f;
        private float jitterFactor = 1f;
        private bool tracksPlayer;
        private bool givesUpWhenPassed;
        private Vector2 cruiseVelocity;
        private Vector2 lockedHeading = Vector2.down;
        private EnemyMoveStyle moveStyle = EnemyMoveStyle.Chase;
        private bool canFlipSprite;
        private float hasteUntil;
        private float hasteMult = 1f;
        private float freezeUntil;

        public bool IsFrozen => Time.time < freezeUntil;
        private float dashUntil;
        private Vector2 dashVelocity;
        private Vector2 knockbackVelocity;
        private float knockbackUntil;
        private bool halted;
        private Enemy self;

        private const float KnockbackDuration = 0.16f;
        private const float KnockbackPop = 0.28f;
        private const float SeparationCell = 2.5f;
        private const float InvSeparationCell = 1f / SeparationCell;

        private static int separationFrame = -1;
        private static readonly Dictionary<int, int> separationHead = new Dictionary<int, int>(128);
        private static int[] separationNext = new int[320];
        private static Enemy[] separationEnemy = new Enemy[320];
        private static Vector2[] separationPos = new Vector2[320];
        private static int separationCount;

        private const float ObstacleRadius = 0.4f;
        private const float ObstacleCell = 3f;
        private const float InvObstacleCell = 1f / ObstacleCell;

        private static int obstacleFrame = -1;
        private static int obstacleQuery;
        private static readonly Dictionary<int, int> obstacleHead = new Dictionary<int, int>(64);
        private static int[] obstacleNext = new int[128];
        private static int[] obstacleIndex = new int[128];
        private static Collider2D[] obstacleCol = new Collider2D[48];
        private static int[] obstacleStamp = new int[48];
        private static int obstacleCount;
        private static int obstacleLinkCount;

        private void Awake()
        {
            self = GetComponent<Enemy>();
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public bool FacingLeft => spriteRenderer != null && spriteRenderer.flipX;

        public string StatusLine
        {
            get
            {
            if (IsFrozen)
            {
                return "frozen";
            }

            if (halted)
            {
                return "waiting";
            }

                if (Time.time < dashUntil)
                {
                    return moveStyle == EnemyMoveStyle.Straight ? "diving" : "lunging";
                }

                if (Time.time < hasteUntil)
                {
                    return "buffed chase";
                }

                switch (moveStyle)
                {
                    case EnemyMoveStyle.Straight:
                        return "flying through";
                    case EnemyMoveStyle.KeepDistance:
                        return "holding range";
                    case EnemyMoveStyle.Orbit:
                        return "circling";
                    case EnemyMoveStyle.Charge:
                        return "hunting";
                    default:
                        return tracksPlayer ? "chasing" : "drifting down";
                }
            }
        }

        public void OnSpawned()
        {
            cruiseVelocity = Vector2.zero;
            lockedHeading = Vector2.down;
            jitterFactor = 1f + Random.Range(-speedJitter, speedJitter);
            hasteUntil = 0f;
            hasteMult = 1f;
            freezeUntil = 0f;
            dashUntil = 0f;
            knockbackVelocity = Vector2.zero;
            knockbackUntil = 0f;
            halted = false;
            RollFollowRole();
        }

        public void OnDespawned()
        {
        }

        public void Configure(EnemyDefinition definition)
        {
            moveSpeed = definition.moveSpeed;
            scrollInfluence = definition.scrollInfluence;
            responsiveness = Mathf.Max(0.1f, definition.turnResponsiveness);
            followChance = Mathf.Clamp01(definition.followChance);
            giveUpWhenPassedChance = Mathf.Clamp01(definition.giveUpWhenPassedChance);
            separationRadius = Mathf.Max(0f, definition.separationRadius);
            separationStrength = Mathf.Max(0f, definition.separationStrength);
            preferredDistance = Mathf.Max(1f, definition.preferredDistance);
            moveStyle = definition.moveStyle;
            canFlipSprite = definition.canFlipSprite;
            lockedHeading = Vector2.down;
            jitterFactor = 1f + Random.Range(-speedJitter, speedJitter);
            cruiseVelocity = Vector2.zero;
            RollFollowRole();

            if (spriteRenderer != null && !canFlipSprite)
            {
                spriteRenderer.flipX = false;
                spriteRenderer.flipY = false;
            }

            if (moveStyle == EnemyMoveStyle.Charge)
            {
                tracksPlayer = true;
                givesUpWhenPassed = false;
            }
            else if (moveStyle == EnemyMoveStyle.Straight)
            {
                tracksPlayer = false;
                jitterFactor = 1f;
            }
            else if (moveStyle == EnemyMoveStyle.KeepDistance || moveStyle == EnemyMoveStyle.Orbit)
            {
                tracksPlayer = true;
                givesUpWhenPassed = false;
            }
        }

        public void ApplyHaste(float multiplier, float duration)
        {
            hasteMult = Mathf.Max(1f, multiplier);
            hasteUntil = Time.time + Mathf.Max(0.1f, duration);
        }

        public void ApplyFreeze(float duration)
        {
            freezeUntil = Mathf.Max(freezeUntil, Time.time + Mathf.Max(0.05f, duration));
        }

        public void SetHalted(bool value)
        {
            halted = value;
        }

        public void SetMimicSleeping(bool sleeping)
        {
            if (sleeping)
            {
                tracksPlayer = false;
                halted = true;
            }
        }

        public void WakeMimic()
        {
            halted = false;
            tracksPlayer = true;
            givesUpWhenPassed = false;
        }

        public void Dash(Vector2 velocity, float duration)
        {
            if (IsFrozen)
            {
                return;
            }

            dashVelocity = velocity;
            dashUntil = Time.time + Mathf.Max(0.05f, duration);
            halted = false;
        }

        /// <summary>
        /// Shoves this enemy along the hit for a short beat. <paramref name="force"/>
        /// is authored world units (WeaponLevel.knockback).
        /// </summary>
        public void ApplyKnockback(Vector2 direction, float force)
        {
            if (IsFrozen || force <= 0.01f)
            {
                return;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            float duration = KnockbackDuration;
            knockbackVelocity = dir * (force / duration);
            knockbackUntil = Time.time + duration;
            halted = false;
            transform.position += (Vector3)(dir * force * KnockbackPop);
        }

        public void SetLockedHeading(Vector2 heading)
        {
            lockedHeading = heading.sqrMagnitude > 0.0001f ? heading.normalized : Vector2.down;
            cruiseVelocity = lockedHeading * (moveSpeed * jitterFactor);
        }

        public void SetSpawnHeading(Vector2 spawnPosition, SpawnPattern pattern)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            Vector2 center = bounds != null ? bounds.WorldRect.center : Vector2.zero;
            bool fromLeft = spawnPosition.x < center.x;
            bool fromSide = pattern == SpawnPattern.Sides
                || pattern == SpawnPattern.CrossScreen
                || pattern == SpawnPattern.FlockDiagonal;
            if (bounds != null)
            {
                Rect world = bounds.WorldRect;
                fromSide |= spawnPosition.x <= world.xMin + 0.85f || spawnPosition.x >= world.xMax - 0.85f;
                fromLeft = spawnPosition.x < world.center.x;
            }

            if (moveStyle != EnemyMoveStyle.Straight)
            {
                if (fromSide)
                {
                    cruiseVelocity = new Vector2(fromLeft ? 1f : -1f, 0f) * (moveSpeed * jitterFactor);
                }

                return;
            }

            switch (pattern)
            {
                case SpawnPattern.CrossScreen:
                case SpawnPattern.FlockDiagonal:
                    lockedHeading = new Vector2(fromLeft ? 0.85f : -0.85f, -1f).normalized;
                    break;
                case SpawnPattern.DiagonalDive:
                    lockedHeading = new Vector2(fromLeft ? 0.7f : -0.7f, -1f).normalized;
                    break;
                case SpawnPattern.FlockDown:
                    lockedHeading = Vector2.down;
                    break;
                case SpawnPattern.Sides:
                    lockedHeading = new Vector2(fromLeft ? 1f : -1f, -0.15f).normalized;
                    break;
                default:
                    lockedHeading = fromSide
                        ? new Vector2(fromLeft ? 1f : -1f, -0.2f).normalized
                        : Vector2.down;
                    break;
            }

            cruiseVelocity = lockedHeading * (moveSpeed * jitterFactor);
        }

        private void RollFollowRole()
        {
            tracksPlayer = Random.value < followChance;
            givesUpWhenPassed = Random.value < giveUpWhenPassedChance;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector2 position = transform.position;

            ArenaBounds bounds = ArenaBounds.Instance;
            bool onScreen = bounds == null || bounds.IsOnScreen(position, chaseSlack);

            if (IsFrozen)
            {
                cruiseVelocity = Vector2.zero;
                knockbackVelocity = Vector2.zero;
                knockbackUntil = 0f;
                dashUntil = 0f;
                // Chase uses a reduced scroll so enemies can fight the road. Frozen bodies
                // have to match the ground exactly or they slide across it.
                transform.position = position + ScrollDirector.Velocity * dt;
                return;
            }

            if (halted && Time.time >= dashUntil)
            {
                Vector2 holdScroll = Vector2.down * (ScrollDirector.Speed * scrollInfluence);
                transform.position = ResolveObstacles(position + holdScroll * dt, Vector2.down);
                return;
            }

            float speedScale = Time.time < hasteUntil ? hasteMult : 1f;
            Vector2 desired = Vector2.zero;
            if (Time.time < dashUntil)
            {
                desired = dashVelocity;
                cruiseVelocity = dashVelocity;
            }
            else if (moveStyle == EnemyMoveStyle.Straight)
            {
                desired = lockedHeading * (moveSpeed * jitterFactor * speedScale);
            }
            else if (onScreen)
            {
                desired = BuildDesiredCruise(position) * speedScale;
            }
            else
            {
                desired = EnterFromOffscreen(position) * speedScale;
            }

            cruiseVelocity = Vector2.Lerp(cruiseVelocity, desired, 1f - Mathf.Exp(-responsiveness * dt));

            Vector2 knock = Vector2.zero;
            if (Time.time < knockbackUntil)
            {
                float remain = Mathf.Clamp01((knockbackUntil - Time.time) / KnockbackDuration);
                knock = knockbackVelocity * remain;
                cruiseVelocity = Vector2.Lerp(cruiseVelocity, knock, 0.65f);
            }

            Vector2 scroll = Vector2.down * (ScrollDirector.Speed * scrollInfluence);
            Vector2 total = cruiseVelocity + scroll + ComputeSeparation(position) + knock;

            transform.position = ResolveObstacles(position + total * dt, cruiseVelocity);

            if (canFlipSprite && spriteRenderer != null && Mathf.Abs(cruiseVelocity.x) > 0.05f)
            {
                spriteRenderer.flipX = cruiseVelocity.x < 0f;
            }
        }

        private Vector2 EnterFromOffscreen(Vector2 position)
        {
            float speed = moveSpeed * jitterFactor;
            Vector2 target = PlayerController.Position;
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null)
            {
                Rect play = bounds.PlayRect;
                if (position.x < play.xMin + 0.15f)
                {
                    return new Vector2(speed, Mathf.Clamp(target.y - position.y, -1f, 1f) * speed * 0.25f);
                }

                if (position.x > play.xMax - 0.15f)
                {
                    return new Vector2(-speed, Mathf.Clamp(target.y - position.y, -1f, 1f) * speed * 0.25f);
                }
            }

            Vector2 toTarget = target - position;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return Vector2.down * speed;
            }

            return toTarget.normalized * speed;
        }

        private Vector2 BuildDesiredCruise(Vector2 position)
        {
            float speed = moveSpeed * jitterFactor;
            Vector2 cruise = Vector2.down * speed;

            Transform player = PlayerController.PlayerTransform;
            if (player == null)
            {
                return cruise;
            }

            if (moveStyle == EnemyMoveStyle.KeepDistance)
            {
                return KeepDistanceCruise(position, player.position, speed);
            }

            if (moveStyle == EnemyMoveStyle.Orbit)
            {
                return OrbitCruise(position, player.position, speed);
            }

            if (!tracksPlayer)
            {
                return cruise;
            }

            bool passedBelow = position.y < player.position.y - passedMargin;
            if (passedBelow && givesUpWhenPassed)
            {
                return cruise;
            }

            Vector2 toPlayer = (Vector2)player.position - position;
            if (toPlayer.sqrMagnitude <= 0.0001f)
            {
                return cruise;
            }

            if (moveStyle != EnemyMoveStyle.Charge && toPlayer.y > 0f)
            {
                speed *= chaseUpSpeedFactor;
            }

            return toPlayer.normalized * speed;
        }

        private Vector2 KeepDistanceCruise(Vector2 position, Vector2 player, float speed)
        {
            Vector2 toPlayer = player - position;
            float distance = toPlayer.magnitude;
            if (distance < 0.001f)
            {
                return Vector2.down * speed;
            }

            Vector2 radial = toPlayer / distance;
            float error = distance - preferredDistance;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            Vector2 steer = radial * Mathf.Clamp(error, -1.5f, 1.5f) + tangent * 0.45f;
            if (steer.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            return steer.normalized * speed;
        }

        private Vector2 OrbitCruise(Vector2 position, Vector2 player, float speed)
        {
            Vector2 toPlayer = player - position;
            float distance = toPlayer.magnitude;
            if (distance < 0.001f)
            {
                return Vector2.down * speed;
            }

            Vector2 radial = toPlayer / distance;
            Vector2 tangent = new Vector2(-radial.y, radial.x);
            float error = distance - preferredDistance;
            Vector2 steer = tangent * 1.1f - radial * Mathf.Clamp(error, -1.2f, 1.2f);
            if (steer.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            return steer.normalized * speed;
        }

        private Vector2 ComputeSeparation(Vector2 position)
        {
            if (separationRadius <= 0.01f || separationStrength <= 0f)
            {
                return Vector2.zero;
            }

            RebuildSeparation();

            float radiusSq = separationRadius * separationRadius;
            Vector2 steer = Vector2.zero;
            int span = Mathf.Max(1, Mathf.CeilToInt(separationRadius * InvSeparationCell));
            int cx = Mathf.FloorToInt(position.x * InvSeparationCell);
            int cy = Mathf.FloorToInt(position.y * InvSeparationCell);

            for (int oy = -span; oy <= span; oy++)
            {
                for (int ox = -span; ox <= span; ox++)
                {
                    if (!separationHead.TryGetValue(SeparationKey(cx + ox, cy + oy), out int slot))
                    {
                        continue;
                    }

                    while (slot >= 0)
                    {
                        Enemy other = separationEnemy[slot];
                        Vector2 otherPos = separationPos[slot];
                        slot = separationNext[slot];
                        if (other == null || other == self || !other.IsAlive)
                        {
                            continue;
                        }

                        Vector2 delta = position - otherPos;
                        float distSq = delta.sqrMagnitude;
                        if (distSq > radiusSq)
                        {
                            continue;
                        }

                        if (distSq < 0.000001f)
                        {
                            float sign = other.GetInstanceID() > GetInstanceID() ? 1f : -1f;
                            steer += new Vector2(sign, sign * 0.35f);
                            continue;
                        }

                        float dist = Mathf.Sqrt(distSq);
                        float weight = 1f - (dist / separationRadius);
                        steer += (delta / dist) * weight;
                    }
                }
            }

            if (steer.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(steer * separationStrength, separationStrength * 1.75f);
        }

        private static void RebuildSeparation()
        {
            if (separationFrame == Time.frameCount)
            {
                return;
            }

            separationFrame = Time.frameCount;
            separationHead.Clear();
            separationCount = 0;

            List<Enemy> active = Enemy.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Enemy enemy = active[i];
                if (enemy == null || !enemy.IsAlive)
                {
                    continue;
                }

                if (separationCount >= separationEnemy.Length)
                {
                    int size = separationEnemy.Length * 2;
                    System.Array.Resize(ref separationNext, size);
                    System.Array.Resize(ref separationEnemy, size);
                    System.Array.Resize(ref separationPos, size);
                }

                Vector2 pos = enemy.Position;
                int slot = separationCount++;
                separationEnemy[slot] = enemy;
                separationPos[slot] = pos;
                int key = SeparationKey(
                    Mathf.FloorToInt(pos.x * InvSeparationCell),
                    Mathf.FloorToInt(pos.y * InvSeparationCell));
                separationNext[slot] = separationHead.TryGetValue(key, out int head) ? head : -1;
                separationHead[key] = slot;
            }
        }

        private Vector2 ResolveObstacles(Vector2 position, Vector2 heading)
        {
            if (moveStyle == EnemyMoveStyle.Straight)
            {
                return position;
            }

            RebuildObstacles();
            if (obstacleCount == 0)
            {
                return position;
            }

            Vector2 pos = position;
            float minSepSq = ObstacleRadius * ObstacleRadius;
            float escapeX = Mathf.Abs(heading.x) > 0.08f ? Mathf.Sign(heading.x) : 0f;
            for (int iter = 0; iter < 2; iter++)
            {
                if (!PushOutOfObstacles(ref pos, ref escapeX, minSepSq))
                {
                    break;
                }
            }

            return pos;
        }

        private bool PushOutOfObstacles(ref Vector2 pos, ref float escapeX, float minSepSq)
        {
            int cx = Mathf.FloorToInt(pos.x * InvObstacleCell);
            int cy = Mathf.FloorToInt(pos.y * InvObstacleCell);
            NextObstacleQuery();

            bool hit = false;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!obstacleHead.TryGetValue(SeparationKey(cx + ox, cy + oy), out int slot))
                    {
                        continue;
                    }

                    while (slot >= 0)
                    {
                        int index = obstacleIndex[slot];
                        slot = obstacleNext[slot];
                        if (index < 0 || obstacleStamp[index] == obstacleQuery)
                        {
                            continue;
                        }

                        obstacleStamp[index] = obstacleQuery;
                        Collider2D col = obstacleCol[index];
                        if (col == null || !col.enabled)
                        {
                            continue;
                        }

                        Bounds bounds = col.bounds;
                        Vector2 center = bounds.center;
                        float reach = ObstacleRadius + Mathf.Max(bounds.extents.x, bounds.extents.y);
                        if ((pos - center).sqrMagnitude > reach * reach)
                        {
                            continue;
                        }

                        bool inside = col.OverlapPoint(pos);
                        Vector2 closest = col.ClosestPoint(pos);
                        Vector2 delta = pos - closest;
                        float distSq = delta.sqrMagnitude;
                        if (!inside && distSq >= minSepSq)
                        {
                            continue;
                        }

                        Vector2 fromCenter = pos - center;
                        if (Mathf.Abs(escapeX) < 0.5f)
                        {
                            float raw = Mathf.Abs(fromCenter.x) > 0.01f ? fromCenter.x : 1f;
                            escapeX = Mathf.Sign(raw);
                        }

                        Vector2 geometric;
                        if (inside || distSq < 0.0001f)
                        {
                            geometric = fromCenter.sqrMagnitude > 0.0001f ? fromCenter.normalized : new Vector2(escapeX, 0f);
                        }
                        else
                        {
                            geometric = delta / Mathf.Sqrt(distSq);
                        }

                        Vector2 normal = geometric + new Vector2(escapeX, 0f);
                        if (normal.sqrMagnitude < 0.0001f)
                        {
                            normal = new Vector2(escapeX, 0f);
                        }

                        normal.Normalize();
                        float currentSep = inside ? 0f : Mathf.Sqrt(distSq);
                        pos += normal * (ObstacleRadius - currentSep);

                        float into = Vector2.Dot(cruiseVelocity, -normal);
                        if (into > 0f)
                        {
                            cruiseVelocity += normal * into;
                        }

                        hit = true;
                    }
                }
            }

            return hit;
        }

        private static void RebuildObstacles()
        {
            if (obstacleFrame == Time.frameCount)
            {
                return;
            }

            obstacleFrame = Time.frameCount;
            obstacleHead.Clear();
            obstacleCount = 0;
            obstacleLinkCount = 0;
            System.Array.Clear(obstacleStamp, 0, obstacleStamp.Length);

            List<BlockingObstacle> blockers = BlockingObstacle.Active;
            for (int i = 0; i < blockers.Count; i++)
            {
                BlockingObstacle blocker = blockers[i];
                Collider2D col = blocker != null ? blocker.Collider : null;
                if (col == null || !col.enabled)
                {
                    continue;
                }

                if (obstacleCount >= obstacleCol.Length)
                {
                    int size = obstacleCol.Length * 2;
                    System.Array.Resize(ref obstacleCol, size);
                    System.Array.Resize(ref obstacleStamp, size);
                }

                int index = obstacleCount++;
                obstacleCol[index] = col;

                Bounds bounds = col.bounds;
                Vector2 min = (Vector2)bounds.min - new Vector2(ObstacleRadius, ObstacleRadius);
                Vector2 max = (Vector2)bounds.max + new Vector2(ObstacleRadius, ObstacleRadius);
                int x0 = Mathf.FloorToInt(min.x * InvObstacleCell);
                int x1 = Mathf.FloorToInt(max.x * InvObstacleCell);
                int y0 = Mathf.FloorToInt(min.y * InvObstacleCell);
                int y1 = Mathf.FloorToInt(max.y * InvObstacleCell);

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        LinkObstacle(SeparationKey(x, y), index);
                    }
                }
            }
        }

        private static void LinkObstacle(int key, int index)
        {
            if (obstacleLinkCount >= obstacleNext.Length)
            {
                int size = obstacleNext.Length * 2;
                System.Array.Resize(ref obstacleNext, size);
                System.Array.Resize(ref obstacleIndex, size);
            }

            int slot = obstacleLinkCount++;
            obstacleIndex[slot] = index;
            obstacleNext[slot] = obstacleHead.TryGetValue(key, out int head) ? head : -1;
            obstacleHead[key] = slot;
        }

        private static void NextObstacleQuery()
        {
            obstacleQuery++;
            if (obstacleQuery == int.MaxValue)
            {
                System.Array.Clear(obstacleStamp, 0, obstacleStamp.Length);
                obstacleQuery = 1;
            }
        }

        private static int SeparationKey(int x, int y)
        {
            return ((x + 1024) << 16) | ((y + 1024) & 0xFFFF);
        }
    }
}
