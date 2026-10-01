using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// World-space play field derived from the orthographic camera. The player is hard
    /// clamped to it, spawners use its top edge, and despawners use its bottom edge.
    /// </summary>
    public class ArenaBounds : SingletonBehaviour<ArenaBounds>
    {
        [SerializeField] private Camera targetCamera;

        [Header("Player Padding (world units inset from the screen edge)")]
        [SerializeField] private float paddingLeft = 0.5f;
        [SerializeField] private float paddingRight = 0.5f;
        [SerializeField] private float paddingTop = 1.2f;
        [SerializeField] private float paddingBottom = 0.8f;

        [Header("Spawn / Despawn Margins (world units outside the screen)")]
        [SerializeField] private float spawnMargin = 2f;
        [SerializeField] private float despawnMargin = 3f;

        private Rect screenRect;
        private int cachedWidth;
        private int cachedHeight;

        /// <summary>Full visible rect in world space.</summary>
        public Rect ScreenRect => screenRect;

        /// <summary>Rect the player is allowed to occupy.</summary>
        public Rect PlayRect => Rect.MinMaxRect(
            screenRect.xMin + paddingLeft,
            screenRect.yMin + paddingBottom,
            screenRect.xMax - paddingRight,
            screenRect.yMax - paddingTop);

        /// <summary>Y coordinate just above the screen where new content appears.</summary>
        public float TopSpawnY => screenRect.yMax + spawnMargin;

        /// <summary>Y coordinate below which anything is recycled.</summary>
        public float BottomDespawnY => screenRect.yMin - despawnMargin;

        public float LeftDespawnX => screenRect.xMin - despawnMargin;

        public float RightDespawnX => screenRect.xMax + despawnMargin;

        protected override void Awake()
        {
            base.Awake();

            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            Recalculate();
        }

        private void Update()
        {
            if (cachedWidth != Screen.width || cachedHeight != Screen.height)
            {
                Recalculate();
            }
        }

        public void Recalculate()
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    return;
                }
            }

            cachedWidth = Screen.width;
            cachedHeight = Screen.height;

            float halfHeight = targetCamera.orthographic
                ? targetCamera.orthographicSize
                : Mathf.Abs(targetCamera.transform.position.z) * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * targetCamera.aspect;

            Vector3 center = targetCamera.transform.position;
            screenRect = new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
        }

        /// <summary>Clamps a position into the player-legal rect.</summary>
        public Vector2 ClampToPlayArea(Vector2 position)
        {
            Rect r = PlayRect;
            return new Vector2(
                Mathf.Clamp(position.x, r.xMin, r.xMax),
                Mathf.Clamp(position.y, r.yMin, r.yMax));
        }

        /// <summary>True when the position is on screen (optionally with slack outside it).</summary>
        public bool IsOnScreen(Vector2 position, float slack = 0f)
        {
            return position.x >= screenRect.xMin - slack
                && position.x <= screenRect.xMax + slack
                && position.y >= screenRect.yMin - slack
                && position.y <= screenRect.yMax + slack;
        }

        /// <summary>True once an object has drifted far enough out to be recycled.</summary>
        public bool IsPastDespawn(Vector2 position)
        {
            return position.y < BottomDespawnY
                || position.x < LeftDespawnX
                || position.x > RightDespawnX
                || position.y > TopSpawnY + despawnMargin;
        }

        /// <summary>Random x inside the visible width, inset by the given margin.</summary>
        public float RandomSpawnX(float inset = 0.75f)
        {
            return Random.Range(screenRect.xMin + inset, screenRect.xMax - inset);
        }

        public Vector2 RandomTopSpawnPoint(float inset = 0.75f)
        {
            return new Vector2(RandomSpawnX(inset), TopSpawnY);
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                Recalculate();
            }

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(screenRect.center, screenRect.size);

            Rect play = PlayRect;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(play.center, play.size);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(screenRect.xMin, BottomDespawnY), new Vector3(screenRect.xMax, BottomDespawnY));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(screenRect.xMin, TopSpawnY), new Vector3(screenRect.xMax, TopSpawnY));
        }
    }
}
