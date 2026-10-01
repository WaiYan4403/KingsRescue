using UnityEngine;
using KingsRescue.Core;
using KingsRescue.InputControl;

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

        [Header("Visuals")]
        [SerializeField] private Animator animator;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private string speedParameter = "Speed";

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

            if (animator == null)
            {
                animator = GetComponentInChildren<Animator>();
            }

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }

            speedParameterHash = Animator.StringToHash(speedParameter);
            hasSpeedParameter = HasAnimatorParameter(speedParameterHash);
        }

        private void Update()
        {
            inputAxis = PlayerInputReader.Axis;
            UpdateVisuals();
        }

        private void FixedUpdate()
        {
            float maxSpeed = stats.MoveSpeed;
            Vector2 desired = inputAxis * maxSpeed;

            if (desired.y > 0f)
            {
                desired.y *= forwardSpeedFactor;
            }

            velocity = Vector2.MoveTowards(velocity, desired, acceleration * Time.fixedDeltaTime);

            Vector2 next = body.position + velocity * Time.fixedDeltaTime;

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
            if (animator != null && hasSpeedParameter)
            {
                animator.SetFloat(speedParameterHash, velocity.magnitude);
            }

            if (spriteRenderer != null && Mathf.Abs(velocity.x) > 0.05f)
            {
                spriteRenderer.flipX = velocity.x < 0f;
            }
        }

        private bool HasAnimatorParameter(int hash)
        {
            if (animator == null || animator.runtimeAnimatorController == null)
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
    }
}
