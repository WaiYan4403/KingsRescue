using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Single source of truth for how fast the world slides downward.
    /// The camera never moves; everything spawned reads this speed instead.
    /// </summary>
    public class ScrollDirector : SingletonBehaviour<ScrollDirector>
    {
        [Header("Speed Ramp")]
        [SerializeField] private float baseSpeed = 2.2f;
        [SerializeField] private float speedGainPerMinute = 0.9f;
        [SerializeField] private float maxSpeed = 6f;

        [Header("Runtime")]
        [SerializeField] private bool scrolling = true;

        private float elapsed;

        public float CurrentSpeed { get; private set; }

        /// <summary>Downward world velocity, ready to multiply by deltaTime.</summary>
        public Vector2 ScrollVelocity => new Vector2(0f, -CurrentSpeed);

        /// <summary>Safe accessor for objects that may outlive the director during teardown.</summary>
        public static float Speed => Instance != null ? Instance.CurrentSpeed : 0f;

        public static Vector2 Velocity => Instance != null ? Instance.ScrollVelocity : Vector2.zero;

        protected override void Awake()
        {
            base.Awake();
            CurrentSpeed = baseSpeed;
        }

        private void Update()
        {
            if (!scrolling)
            {
                CurrentSpeed = 0f;
                return;
            }

            elapsed += Time.deltaTime;
            CurrentSpeed = Mathf.Min(maxSpeed, baseSpeed + speedGainPerMinute * (elapsed / 60f));
        }

        public void SetScrolling(bool value)
        {
            scrolling = value;
        }

        public void ResetRun()
        {
            elapsed = 0f;
            CurrentSpeed = baseSpeed;
        }
    }
}
