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

        [Tooltip("World units that count as one meter on the HUD.")]
        [SerializeField] private float unitsToMeters = 1f;

        private float elapsed;

        public float CurrentSpeed { get; private set; }

        /// <summary>Meters of road the run has covered. Grows with scroll speed and time.</summary>
        public float Distance { get; private set; }

        /// <summary>Downward world velocity, ready to multiply by deltaTime.</summary>
        public Vector2 ScrollVelocity => new Vector2(0f, -CurrentSpeed);

        /// <summary>Safe accessor for objects that may outlive the director during teardown.</summary>
        public static float Speed => Instance != null ? Instance.CurrentSpeed : 0f;

        public static Vector2 Velocity => Instance != null ? Instance.ScrollVelocity : Vector2.zero;

        public static float DistanceMeters => Instance != null ? Instance.Distance : 0f;

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
            Distance += CurrentSpeed * Mathf.Max(0.0001f, unitsToMeters) * Time.deltaTime;
        }

        public void SetScrolling(bool value)
        {
            scrolling = value;
        }

        public void ResetRun()
        {
            elapsed = 0f;
            Distance = 0f;
            CurrentSpeed = baseSpeed;
        }
    }
}
