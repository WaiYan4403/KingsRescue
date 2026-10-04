using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.InputControl;
using KingsRescue.World;

namespace KingsRescue.Player
{
    /// <summary>
    /// Repositions the hero inside the screen. The world is what moves, so holding "up"
    /// means running ahead of the scroll rather than travelling anywhere.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PlayerStats))]
    public class PlayerController : SingletonBehaviour<PlayerController>
    {
        [Header("Feel")]
        [Tooltip("How quickly the hero reaches full speed, in units per second squared. Higher is snappier.")]
        [SerializeField] private float acceleration = 90f;

        [Tooltip("Extra speed multiplier while running against the scroll, sold to the player as sprinting.")]
        [SerializeField, Range(0.5f, 1.5f)] private float forwardSpeedFactor = 1f;

        [Tooltip("Speed multiplier while moving down the screen. Higher offsets the world scroll so down feels as quick as up.")]
        [SerializeField, Range(1f, 3f)] private float backwardSpeedFactor = 1.5f;

        [Header("Obstacles")]
        [Tooltip("Body radius used to keep the hero out of rocks and trees. Larger than the foot collider so they cannot squeeze through.")]
        [SerializeField] private float obstacleRadius = 0.45f;
        [SerializeField] private float obstacleSkin = 0.04f;
        [SerializeField] private int resolveIterations = 3;
        [Tooltip("How hard contact shoves the hero left or right around a prop.")]
        [SerializeField, Range(0f, 2f)] private float asideBias = 1.1f;

        [Header("Visuals")]
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private Animator legAnimator;
        [SerializeField] private Animator effectAnimator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private string speedParameter = "MoveSpeed";

        private Rigidbody2D body;
        private PlayerStats stats;
        private Vector2 velocity;
        private Vector2 inputAxis;
        private int speedParameterHash;
        private bool hasSpeedParameter;

        public Vector2 Velocity => velocity;

        /// <summary>Cached so enemies do not call GetComponent every contact check.</summary>
        public PlayerHealth Health { get; private set; }

        /// <summary>Convenience accessor for enemies, pickups and weapons.</summary>
        public static Transform PlayerTransform => Instance != null ? Instance.transform : null;

        public static Vector2 Position => Instance != null ? (Vector2)Instance.transform.position : Vector2.zero;

        protected override void Awake()
        {
            base.Awake();

            body = GetComponent<Rigidbody2D>();
            stats = GetComponent<PlayerStats>();
            Health = GetComponent<PlayerHealth>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            speedParameterHash = Animator.StringToHash(speedParameter);
            hasSpeedParameter = HasAnimatorParameter(speedParameterHash);
        }

        private void Update()
        {
            if (Health != null && !Health.IsAlive)
            {
                inputAxis = Vector2.zero;
                velocity = Vector2.zero;
                return;
            }

            inputAxis = PlayerInputReader.Axis;
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            if (Health != null && !Health.IsAlive)
            {
                velocity = Vector2.zero;
                return;
            }

            float maxSpeed = stats.MoveSpeed;
            Vector2 desired = inputAxis * maxSpeed;

            if (desired.y > 0f)
            {
                desired.y *= forwardSpeedFactor;
            }
            else if (desired.y < 0f)
            {
                desired.y *= backwardSpeedFactor;
            }

            velocity = Vector2.MoveTowards(velocity, desired, acceleration * Time.fixedDeltaTime);

            Vector2 next = ResolveObstacles(body.position + velocity * Time.fixedDeltaTime);

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null)
            {
                Vector2 clamped = bounds.ClampToPlayArea(next);

                // Kill the velocity component that ran into the wall so it does not build up.
                if (!Mathf.Approximately(clamped.x, next.x))
                {
                    velocity.x = 0f;
                }

                if (!Mathf.Approximately(clamped.y, next.y))
                {
                    velocity.y = 0f;
                }

                next = clamped;
            }

            body.MovePosition(next);
        }

        private void UpdateVisuals()
        {
            if (legAnimator != null && bodyAnimator != null && hasSpeedParameter)
            {
                bodyAnimator.SetFloat(speedParameterHash, velocity.magnitude);
                legAnimator.SetFloat(speedParameterHash, velocity.magnitude);
            }

            /*if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
            {
                spriteRenderer.flipX = velocity.x < 0f;
            }*/
        }

        private Vector2 ResolveObstacles(Vector2 position)
        {
            List<BlockingObstacle> blockers = BlockingObstacle.Active;
            if (blockers.Count == 0)
            {
                return position;
            }

            float minSep = obstacleRadius + obstacleSkin;
            Vector2 pos = position;

            for (int iter = 0; iter < resolveIterations; iter++)
            {
                bool pushed = false;

                for (int i = 0; i < blockers.Count; i++)
                {
                    BlockingObstacle blocker = blockers[i];
                    Collider2D col = blocker != null ? blocker.Collider : null;
                    if (col == null || !col.enabled)
                    {
                        continue;
                    }

                    Vector2 closest = col.ClosestPoint(pos);
                    Vector2 delta = pos - closest;
                    float dist = delta.sqrMagnitude;
                    bool inside = col.OverlapPoint(pos);

                    if (!inside && dist >= minSep * minSep)
                    {
                        continue;
                    }

                    Vector2 fromCenter = pos - (Vector2)col.bounds.center;
                    Vector2 normal;
                    if (inside || dist < 0.0001f)
                    {
                        normal = fromCenter.sqrMagnitude > 0.0001f ? fromCenter.normalized : Vector2.right;
                    }
                    else
                    {
                        normal = delta / Mathf.Sqrt(dist);
                    }

                    float side = fromCenter.x;
                    if (Mathf.Abs(side) < 0.08f)
                    {
                        side = inputAxis.x;
                    }

                    if (Mathf.Abs(side) < 0.01f)
                    {
                        side = fromCenter.x >= 0f ? 1f : -1f;
                    }

                    normal = (normal + new Vector2(Mathf.Sign(side), 0f) * asideBias).normalized;

                    float currentSep = inside ? 0f : Mathf.Sqrt(dist);
                    pos += normal * (minSep - currentSep);
                    pushed = true;

                    velocity -= normal * Mathf.Max(0f, Vector2.Dot(velocity, normal));
                }

                if (!pushed)
                {
                    break;
                }
            }

            return pos;
        }

        /// <summary>
        /// Plays one unlockable attack stage on the body and effect animators.
        /// Each character's unique weapon supplies the trigger names (Attack1, Attack2, ...).
        /// </summary>
        public void PlayAttackStage(string triggerName, string[] allStageTriggers = null)
        {
            if (string.IsNullOrEmpty(triggerName))
            {
                return;
            }

            SetAttackTrigger(bodyAnimator, triggerName, allStageTriggers);
            SetAttackTrigger(effectAnimator, triggerName, allStageTriggers);
        }

        private static void SetAttackTrigger(Animator animator, string triggerName, string[] allStageTriggers)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            if (allStageTriggers != null)
            {
                for (int i = 0; i < allStageTriggers.Length; i++)
                {
                    string sibling = allStageTriggers[i];
                    if (!string.IsNullOrEmpty(sibling) && HasAnimatorParameter(animator, Animator.StringToHash(sibling)))
                    {
                        animator.ResetTrigger(sibling);
                    }
                }
            }

            if (!HasAnimatorParameter(animator, Animator.StringToHash(triggerName)))
            {
                return;
            }

            animator.SetTrigger(triggerName);
        }

        private static bool HasAnimatorParameter(Animator animator, int hash)
        {
            if (animator == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = animator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }

        private bool HasAnimatorParameter(int hash)
        {
            if (bodyAnimator == null || bodyAnimator.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = bodyAnimator.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].nameHash == hash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
