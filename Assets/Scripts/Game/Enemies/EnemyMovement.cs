using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

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
        private float dashUntil;
        private Vector2 dashVelocity;
        private bool halted;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public bool FacingLeft => spriteRenderer != null && spriteRenderer.flipX;

        public void OnSpawned()
        {
            cruiseVelocity = Vector2.zero;
            lockedHeading = Vector2.down;
            jitterFactor = 1f + Random.Range(-speedJitter, speedJitter);
            hasteUntil = 0f;
            hasteMult = 1f;
            dashUntil = 0f;
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
            dashVelocity = velocity;
            dashUntil = Time.time + Mathf.Max(0.05f, duration);
            halted = false;
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

            if (halted && Time.time >= dashUntil)
            {
                Vector2 holdScroll = Vector2.down * (ScrollDirector.Speed * scrollInfluence);
                transform.position = position + holdScroll * dt;
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

            Vector2 scroll = Vector2.down * (ScrollDirector.Speed * scrollInfluence);
            Vector2 total = cruiseVelocity + scroll + ComputeSeparation(position);

            transform.position = position + total * dt;

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

            float radiusSq = separationRadius * separationRadius;
            Vector2 steer = Vector2.zero;
            List<Enemy> active = Enemy.Active;

            for (int i = 0; i < active.Count; i++)
            {
                Enemy other = active[i];
                if (other == null || !other.IsAlive || other.transform == transform)
                {
                    continue;
                }

                Vector2 delta = position - other.Position;
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

            if (steer.sqrMagnitude < 0.0001f)
            {
                return Vector2.zero;
            }

            return Vector2.ClampMagnitude(steer * separationStrength, separationStrength * 1.75f);
        }
    }
}
