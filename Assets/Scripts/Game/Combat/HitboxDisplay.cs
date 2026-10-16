using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Draws a circle sprite over melee swings and projectiles so the authored
    /// hit radius is visible in play. Every marker comes from <see cref="ObjectPoolManager"/>;
    /// the marker prefab needs a pool entry sized for the most shots alive at once.
    /// </summary>
    public class HitboxDisplay : SingletonBehaviour<HitboxDisplay>
    {
        [SerializeField] private HitboxMarker markerPrefab;

        [Tooltip("Spawns hitbox markers for player weapons and enemy attacks. Off spawns nothing and clears any that are already showing.")]
        [SerializeField] private bool showHitboxes = true;

        [SerializeField] private float flashLifetime = 0.2f;

        private readonly System.Collections.Generic.List<HitboxMarker> live = new System.Collections.Generic.List<HitboxMarker>(64);

        private void LateUpdate()
        {
            if (!showHitboxes)
            {
                ReleaseAll();
                return;
            }

            for (int i = live.Count - 1; i >= 0; i--)
            {
                HitboxMarker marker = live[i];
                if (marker == null || !marker.gameObject.activeInHierarchy)
                {
                    live.RemoveAt(i);
                }
            }
        }

        /// <summary>One-shot circle at a world point, used by melee swings.</summary>
        public static void Flash(Vector2 center, float radius, float lifetime = 0f)
        {
            HitboxDisplay display = Instance;
            if (!CanShow(display))
            {
                return;
            }

            HitboxMarker marker = SpawnMarker(display, center, null);
            if (marker == null)
            {
                return;
            }

            marker.SetPersistent(false);
            marker.SetRadius(radius);
            marker.SetLifetime(lifetime > 0f ? lifetime : display.flashLifetime);
        }

        /// <summary>Forward lance hit, drawn from <paramref name="origin"/> along <paramref name="direction"/>.</summary>
        public static void FlashCapsule(Vector2 origin, Vector2 direction, float length, float thickness, float lifetime = 0f)
        {
            HitboxDisplay display = Instance;
            if (!CanShow(display))
            {
                return;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            float reach = Mathf.Max(0.2f, length);
            HitboxMarker marker = SpawnMarker(display, origin + dir * (reach * 0.5f), null);
            if (marker == null)
            {
                return;
            }

            marker.SetPersistent(false);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            marker.SetCapsule(reach, thickness, angle);
            marker.SetLifetime(lifetime > 0f ? lifetime : display.flashLifetime);
        }

        /// <summary>
        /// Circle that rides a projectile for as long as it lives. Hand the result back
        /// through <see cref="Release"/> when the projectile returns to its pool.
        /// </summary>
        public static HitboxMarker Follow(Transform parent, float radius)
        {
            HitboxDisplay display = Instance;
            if (parent == null || !CanShow(display))
            {
                return null;
            }

            HitboxMarker marker = SpawnMarker(display, parent.position, parent);
            if (marker == null)
            {
                return null;
            }

            marker.transform.localPosition = Vector3.zero;
            marker.SetPersistent(true);
            marker.SetRadius(radius);
            return marker;
        }

        /// <summary>
        /// Keeps one circle on a moving attack. Pass the same marker back each frame.
        /// A hidden display releases it and clears the reference.
        /// </summary>
        public static void SyncCircle(ref HitboxMarker marker, Vector2 center, float radius)
        {
            HitboxDisplay display = Instance;
            if (!CanShow(display))
            {
                Release(marker);
                marker = null;
                return;
            }

            if (!IsShowing(marker))
            {
                marker = Pin(display, center, radius);
                return;
            }

            marker.transform.SetPositionAndRotation(center, Quaternion.identity);
            marker.SetRadius(radius);
        }

        /// <summary>Keeps one capsule on a beam or thrust. Length runs along the direction.</summary>
        public static void SyncCapsule(ref HitboxMarker marker, Vector2 origin, Vector2 direction, float length, float thickness)
        {
            HitboxDisplay display = Instance;
            if (!CanShow(display))
            {
                Release(marker);
                marker = null;
                return;
            }

            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.up;
            float reach = Mathf.Max(0.2f, length);
            float width = Mathf.Max(0.05f, thickness);
            Vector2 center = origin + dir * (reach * 0.5f);
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
            if (!IsShowing(marker))
            {
                marker = Pin(display, center, width * 0.5f);
            }

            if (marker == null)
            {
                return;
            }

            marker.transform.position = center;
            marker.SetCapsule(reach, width, angle);
        }

        /// <summary>Returns a marker handed out by <see cref="Follow"/> or a sync call to the pool.</summary>
        public static void Release(HitboxMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            HitboxDisplay display = Instance;
            if (display != null)
            {
                display.live.Remove(marker);
            }

            if (!marker.gameObject.activeInHierarchy)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                pool.Return(marker.gameObject);
            }
        }

        private void ReleaseAll()
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                Release(live[i]);
            }

            live.Clear();
        }

        private static HitboxMarker Pin(HitboxDisplay display, Vector2 center, float radius)
        {
            HitboxMarker marker = SpawnMarker(display, center, null);
            if (marker == null)
            {
                return null;
            }

            marker.SetPersistent(true);
            marker.SetRadius(radius);
            return marker;
        }

        private static bool IsShowing(HitboxMarker marker)
        {
            return marker != null && marker.gameObject.activeInHierarchy;
        }

        private static bool CanShow(HitboxDisplay display)
        {
            return display != null && display.showHitboxes && display.markerPrefab != null;
        }

        private static HitboxMarker SpawnMarker(HitboxDisplay display, Vector2 center, Transform parent)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            HitboxMarker marker = pool != null
                ? pool.Spawn(display.markerPrefab.gameObject, center, Quaternion.identity, parent)?.GetComponent<HitboxMarker>()
                : null;
            if (marker != null)
            {
                display.live.Add(marker);
            }

            return marker;
        }
    }
}
