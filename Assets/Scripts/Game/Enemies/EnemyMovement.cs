using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Enemies steer at the player, then a rolled subset gives up once they
    /// slip below the player's Y and just keep flying down.
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
        private float jitterFactor = 1f;
        private bool tracksPlayer;
        private bool givesUpWhenPassed;
        private Vector2 cruiseVelocity;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public void OnSpawned()
        {
            cruiseVelocity = Vector2.zero;
            jitterFactor = 1f + Random.Range(-speedJitter, speedJitter);
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
            jitterFactor = 1f + Random.Range(-speedJitter, speedJitter);
            cruiseVelocity = Vector2.zero;
            RollFollowRole();
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

            Vector2 desired = Vector2.zero;
            if (onScreen)
            {
                desired = BuildDesiredCruise(position);
            }

            cruiseVelocity = Vector2.Lerp(cruiseVelocity, desired, 1f - Mathf.Exp(-responsiveness * dt));

            Vector2 scroll = Vector2.down * (ScrollDirector.Speed * scrollInfluence);
            Vector2 total = cruiseVelocity + scroll + ComputeSeparation(position);

            transform.position = position + total * dt;

            if (spriteRenderer != null && Mathf.Abs(cruiseVelocity.x) > 0.05f)
            {
                spriteRenderer.flipX = cruiseVelocity.x < 0f;
            }
        }

        private Vector2 BuildDesiredCruise(Vector2 position)
        {
            float speed = moveSpeed * jitterFactor;
            Vector2 cruise = Vector2.down * speed;

            Transform player = PlayerController.PlayerTransform;
            if (player == null || !tracksPlayer)
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

            if (toPlayer.y > 0f)
            {
                speed *= chaseUpSpeedFactor;
            }

            return toPlayer.normalized * speed;
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
                    // Same tile: pick a stable side so the pair does not sit on one pixel.
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
