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

        [SerializeField] private bool showHitboxes = true;

        [SerializeField] private float flashLifetime = 0.2f;

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

        /// <summary>Returns a marker handed out by <see cref="Follow"/> to the pool.</summary>
        public static void Release(HitboxMarker marker)
        {
            if (marker == null)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                pool.Return(marker.gameObject);
            }
        }

        private static bool CanShow(HitboxDisplay display)
        {
            return display != null && display.showHitboxes && display.markerPrefab != null;
        }

        private static HitboxMarker SpawnMarker(HitboxDisplay display, Vector2 center, Transform parent)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            return pool != null
                ? pool.Spawn(display.markerPrefab, center, Quaternion.identity, parent)
                : null;
        }
    }
}
