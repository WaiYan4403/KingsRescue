using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Authored world-space play field. Player clamp and spawners use this box so
    /// aspect ratio no longer changes the gameplay area. The camera frustum is
    /// kept only for visibility tests and background recycle.
    /// </summary>
    public class ArenaBounds : SingletonBehaviour<ArenaBounds>
    {
        [SerializeField] private Camera targetCamera;

        [Header("World Boundary")]
        [Tooltip("Optional collider that defines the playable world. When set, its bounds become the world rect.")]
        [SerializeField] private Collider2D worldBoundary;

        [Tooltip("Playable world size in world units, centered on the camera. An axis set to 0 uses the camera frustum on that axis.")]
        [SerializeField] private Vector2 worldSize = new Vector2(10f, 0f);

        [Header("Player Padding (world units inset from the world edge)")]
        [SerializeField] private float paddingLeft = 0.5f;
        [SerializeField] private float paddingRight = 0.5f;
        [SerializeField] private float paddingTop = 1.2f;
        [SerializeField] private float paddingBottom = 0.8f;

        [Header("Spawn / Despawn Margins (world units outside the world)")]
        [SerializeField] private float spawnMargin = 2f;
        [SerializeField] private float despawnMargin = 3f;

        private Rect screenRect;
        private Rect worldRect;
        private int cachedWidth;
        private int cachedHeight;

        /// <summary>Visible camera rect in world space. Use for on-screen tests only.</summary>
        public Rect ScreenRect => screenRect;

        /// <summary>Authored playable world. Spawners and player clamp use this.</summary>
        public Rect WorldRect => worldRect;

        /// <summary>Rect the player is allowed to occupy.</summary>
        public Rect PlayRect => Rect.MinMaxRect(
            worldRect.xMin + paddingLeft,
            worldRect.yMin + paddingBottom,
            worldRect.xMax - paddingRight,
            worldRect.yMax - paddingTop);

        /// <summary>Y coordinate just above the world where new content appears.</summary>
        public float TopSpawnY => worldRect.yMax + spawnMargin;

        /// <summary>Y coordinate below which gameplay objects are recycled.</summary>
        public float BottomDespawnY => worldRect.yMin - despawnMargin;

        /// <summary>Y below the visible camera, used to recycle background tiles.</summary>
        public float ScreenDespawnY => screenRect.yMin - despawnMargin;

        public float LeftDespawnX => worldRect.xMin - despawnMargin;

        public float RightDespawnX => worldRect.xMax + despawnMargin;

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
                if (targetCamera == null && worldBoundary == null)
                {
                    return;
                }
            }

            cachedWidth = Screen.width;
            cachedHeight = Screen.height;

            if (targetCamera != null)
            {
                float halfHeight = targetCamera.orthographic
                    ? targetCamera.orthographicSize
                    : Mathf.Abs(targetCamera.transform.position.z) * Mathf.Tan(targetCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
                float halfWidth = halfHeight * targetCamera.aspect;

                Vector3 center = targetCamera.transform.position;
                screenRect = new Rect(center.x - halfWidth, center.y - halfHeight, halfWidth * 2f, halfHeight * 2f);
            }
            else
            {
                screenRect = worldRect;
            }

            worldRect = BuildWorldRect();
        }

        private Rect BuildWorldRect()
        {
            if (worldBoundary != null)
            {
                Bounds bounds = worldBoundary.bounds;
                return new Rect(bounds.min.x, bounds.min.y, bounds.size.x, bounds.size.y);
            }

            Vector2 center = screenRect.center;
            float width = worldSize.x > 0.01f ? worldSize.x : screenRect.width;
            float height = worldSize.y > 0.01f ? worldSize.y : screenRect.height;
            return new Rect(center.x - width * 0.5f, center.y - height * 0.5f, width, height);
        }

        /// <summary>Clamps a position into the player-legal rect.</summary>
        public Vector2 ClampToPlayArea(Vector2 position)
        {
            Rect r = PlayRect;
            return new Vector2(
                Mathf.Clamp(position.x, r.xMin, r.xMax),
                Mathf.Clamp(position.y, r.yMin, r.yMax));
        }

        /// <summary>True when the position is on the visible camera (optionally with slack).</summary>
        public bool IsOnScreen(Vector2 position, float slack = 0f)
        {
            return position.x >= screenRect.xMin - slack
                && position.x <= screenRect.xMax + slack
                && position.y >= screenRect.yMin - slack
                && position.y <= screenRect.yMax + slack;
        }

        /// <summary>True once an object has drifted far enough out of the world to be recycled.</summary>
        public bool IsPastDespawn(Vector2 position)
        {
            return position.y < BottomDespawnY
                || position.x < LeftDespawnX
                || position.x > RightDespawnX
                || position.y > TopSpawnY + despawnMargin;
        }

        /// <summary>Random x inside the world width, inset by the given margin.</summary>
        public float RandomSpawnX(float inset = 0.75f)
        {
            return Random.Range(worldRect.xMin + inset, worldRect.xMax - inset);
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

            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.9f);
            Gizmos.DrawWireCube(screenRect.center, screenRect.size);

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireCube(worldRect.center, worldRect.size);

            Rect play = PlayRect;
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(play.center, play.size);

            Gizmos.color = Color.red;
            Gizmos.DrawLine(new Vector3(worldRect.xMin, BottomDespawnY), new Vector3(worldRect.xMax, BottomDespawnY));
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(new Vector3(worldRect.xMin, TopSpawnY), new Vector3(worldRect.xMax, TopSpawnY));
        }
    }
}
