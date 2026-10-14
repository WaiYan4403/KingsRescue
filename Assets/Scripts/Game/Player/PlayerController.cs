using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.InputControl;
using KingsRescue.World;

namespace KingsRescue.Player
{
    /// <summary>
    /// Repositions the hero inside the authored world boundary. The world is what
    /// moves, so holding "up" means running ahead of the scroll rather than travelling anywhere.
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

        [Header("Flight")]
        [SerializeField] private float flyBobAmplitude = 0.14f;
        [SerializeField] private float flyBobHz = 1.6f;

        [Header("Visuals")]
        [SerializeField] private Animator bodyAnimator;
        [SerializeField] private Animator legAnimator;
        [SerializeField] private Animator effectAnimator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private string speedParameter = "MoveSpeed";

        private Rigidbody2D body;
        private PlayerStats stats;
        private PlayerStatus status;
        private WeaponLoadout loadout;
        private Vector2 velocity;
        private Vector2 inputAxis;
        private int speedParameterHash;
        private bool hasSpeedParameter;
        private Vector3 spriteRestLocal;
        private bool spriteRestCached;
        private readonly List<Collider2D> overlappingObstacles = new List<Collider2D>(8);
        private SpriteRenderer slashRenderer;

        public Vector2 Velocity => velocity;

        /// <summary>Cached so hostile shots never call GetComponent on a hit.</summary>
        public PlayerStatus Status => status;

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
            status = GetComponent<PlayerStatus>();
            loadout = GetComponent<WeaponLoadout>();
            Health = GetComponent<PlayerHealth>();

            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0f;
            body.freezeRotation = true;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            if (effectAnimator != null)
            {
                slashRenderer = effectAnimator.GetComponent<SpriteRenderer>();
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
                UpdateFlightBob(false);
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

            float maxSpeed = stats.MoveSpeed * (status != null ? status.MoveScale : 1f);
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

            if (CanFlipSprite() && spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
            {
                spriteRenderer.flipX = velocity.x < 0f;
            }

            UpdateFlightBob(IgnoresObstacles());
        }

        private bool CanFlipSprite()
        {
            return loadout != null && loadout.Character != null && loadout.Character.canFlipSprite;
        }

        private bool IgnoresObstacles()
        {
            return loadout != null && loadout.Character != null && loadout.Character.ignoresObstacles;
        }

        private void UpdateFlightBob(bool flying)
        {
            if (spriteRenderer == null)
            {
                return;
            }

            Transform visual = spriteRenderer.transform;
            if (!spriteRestCached)
            {
                spriteRestLocal = visual.localPosition;
                spriteRestCached = true;
            }

            if (!flying)
            {
                visual.localPosition = spriteRestLocal;
                return;
            }

            float bob = Mathf.Sin(Time.time * flyBobHz * Mathf.PI * 2f) * flyBobAmplitude;
            visual.localPosition = spriteRestLocal + new Vector3(0f, bob, 0f);
        }

        private Vector2 ResolveObstacles(Vector2 position)
        {
            if (IgnoresObstacles())
            {
                return position;
            }

            List<BlockingObstacle> blockers = BlockingObstacle.Active;
            if (blockers.Count == 0)
            {
                return position;
            }

            float minSep = obstacleRadius + obstacleSkin;
            Vector2 pos = position;

            for (int iter = 0; iter < resolveIterations; iter++)
            {
                CollectOverlaps(pos, blockers, minSep);
                if (overlappingObstacles.Count == 0)
                {
                    break;
                }

                float escapeX = SharedEscapeX(pos, minSep);
                bool cluster = overlappingObstacles.Count > 1;

                for (int i = 0; i < overlappingObstacles.Count; i++)
                {
                    Collider2D col = overlappingObstacles[i];
                    Vector2 closest = col.ClosestPoint(pos);
                    Vector2 delta = pos - closest;
                    float dist = delta.sqrMagnitude;
                    bool inside = col.OverlapPoint(pos);
                    Vector2 fromCenter = pos - (Vector2)col.bounds.center;

                    Vector2 geometric;
                    if (inside || dist < 0.0001f)
                    {
                        geometric = fromCenter.sqrMagnitude > 0.0001f ? fromCenter.normalized : new Vector2(escapeX, 0f);
                    }
                    else
                    {
                        geometric = delta / Mathf.Sqrt(dist);
                    }

                    Vector2 normal;
                    if (cluster)
                    {
                        // Every overlapping rock shoves the same way so a sandwich cannot pin the hero.
                        float pushY = Mathf.Min(0f, geometric.y);
                        normal = new Vector2(escapeX, pushY);
                        if (normal.sqrMagnitude < 0.0001f)
                        {
                            normal = new Vector2(escapeX, 0f);
                        }

                        normal = (normal.normalized + new Vector2(escapeX, 0f) * asideBias).normalized;
                    }
                    else
                    {
                        float side = fromCenter.x;
                        if (Mathf.Abs(side) < 0.08f)
                        {
                            side = inputAxis.x;
                        }

                        if (Mathf.Abs(side) < 0.01f)
                        {
                            side = escapeX;
                        }

                        normal = (geometric + new Vector2(Mathf.Sign(side), 0f) * asideBias).normalized;
                    }

                    float currentSep = inside ? 0f : Mathf.Sqrt(dist);
                    pos += normal * (minSep - currentSep);

                    // Keep sliding along the surface; only kill speed aimed into the rock.
                    float into = Vector2.Dot(velocity, -normal);
                    if (into > 0f)
                    {
                        velocity += normal * into;
                    }
                }
            }

            return pos;
        }

        private void CollectOverlaps(Vector2 pos, List<BlockingObstacle> blockers, float minSep)
        {
            overlappingObstacles.Clear();
            float minSepSq = minSep * minSep;

            for (int i = 0; i < blockers.Count; i++)
            {
                BlockingObstacle blocker = blockers[i];
                Collider2D col = blocker != null ? blocker.Collider : null;
                if (col == null || !col.enabled)
                {
                    continue;
                }

                Vector2 closest = col.ClosestPoint(pos);
                float dist = (pos - closest).sqrMagnitude;
                if (col.OverlapPoint(pos) || dist < minSepSq)
                {
                    overlappingObstacles.Add(col);
                }
            }
        }

        /// <summary>
        /// One shared left/right for the whole overlapping pile: already-on-this-side,
        /// then stick input, then whichever side of the road still has room.
        /// </summary>
        private float SharedEscapeX(Vector2 pos, float minSep)
        {
            float centroidX = 0f;
            for (int i = 0; i < overlappingObstacles.Count; i++)
            {
                centroidX += overlappingObstacles[i].bounds.center.x;
            }

            centroidX /= overlappingObstacles.Count;
            float offset = pos.x - centroidX;
            float escape;
            if (Mathf.Abs(offset) > 0.12f)
            {
                escape = Mathf.Sign(offset);
            }
            else if (Mathf.Abs(inputAxis.x) > 0.2f)
            {
                escape = Mathf.Sign(inputAxis.x);
            }
            else
            {
                escape = 1f;
                ArenaBounds room = ArenaBounds.Instance;
                if (room != null)
                {
                    Rect play = room.PlayRect;
                    escape = (play.xMax - pos.x) >= (pos.x - play.xMin) ? 1f : -1f;
                }
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null)
            {
                Rect play = bounds.PlayRect;
                if (escape < 0f && pos.x - minSep <= play.xMin + 0.05f)
                {
                    escape = 1f;
                }
                else if (escape > 0f && pos.x + minSep >= play.xMax - 0.05f)
                {
                    escape = -1f;
                }
            }

            return escape;
        }

        /// <summary>
        /// World bounds of the slash frame currently on the effect animator.
        /// The lancer art plays upward and does not turn toward a target.
        /// </summary>
        public bool TryGetSlashBounds(out Bounds bounds)
        {
            bounds = default;
            if (slashRenderer == null || !slashRenderer.enabled || slashRenderer.sprite == null)
            {
                return false;
            }

            bounds = slashRenderer.bounds;
            return bounds.size.sqrMagnitude > 0.04f;
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
