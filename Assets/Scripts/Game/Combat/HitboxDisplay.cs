using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Draws a circle sprite over melee swings and projectiles so the authored
    /// hit radius is visible in play.
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

            HitboxMarker marker = SpawnWorld(display, center);
            if (marker == null)
            {
                return;
            }

            marker.SetPersistent(false);
            marker.SetRadius(radius);

            AutoDespawn despawn = marker.GetComponent<AutoDespawn>();
            if (despawn != null)
            {
                despawn.SetLifetime(lifetime > 0f ? lifetime : display.flashLifetime);
            }
        }

        /// <summary>Circle that rides a projectile for as long as it lives.</summary>
        public static void Follow(Transform parent, float radius)
        {
            if (parent == null)
            {
                return;
            }

            HitboxDisplay display = Instance;
            HitboxMarker marker = parent.GetComponentInChildren<HitboxMarker>(true);
            if (!CanShow(display))
            {
                if (marker != null)
                {
                    marker.gameObject.SetActive(false);
                }

                return;
            }

            if (marker == null)
            {
                marker = Instantiate(display.markerPrefab, parent);
                marker.name = display.markerPrefab.name;
                marker.transform.localPosition = Vector3.zero;
                marker.transform.localRotation = Quaternion.identity;
            }

            marker.gameObject.SetActive(true);
            marker.SetPersistent(true);
            marker.SetRadius(radius);
        }

        private static bool CanShow(HitboxDisplay display)
        {
            return display != null && display.showHitboxes && display.markerPrefab != null;
        }

        private static HitboxMarker SpawnWorld(HitboxDisplay display, Vector2 center)
        {
            PoolManager pool = PoolManager.Instance;
            if (pool != null)
            {
                return pool.Spawn(display.markerPrefab, center, Quaternion.identity);
            }

            return Instantiate(display.markerPrefab, center, Quaternion.identity);
        }
    }
}
