using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Pickups;

namespace KingsRescue.Player
{
    /// <summary>
    /// Pulls nearby pickups toward the hero. The radius is a stat, so "Pickup Radius"
    /// upgrades widen it without touching any colliders.
    /// </summary>
    [RequireComponent(typeof(PlayerStats))]
    public class PickupMagnet : MonoBehaviour
    {
        [Tooltip("Seconds between scans. Pickups keep flying once attracted, so this can be coarse.")]
        [SerializeField] private float scanInterval = 0.08f;

        private PlayerStats stats;
        private float nextScan;

        private void Awake()
        {
            stats = GetComponent<PlayerStats>();
        }

        private void Update()
        {
            if (Time.time < nextScan)
            {
                return;
            }

            nextScan = Time.time + scanInterval;

            float radius = stats.MagnetRadius;
            float sqrRadius = radius * radius;
            Vector2 position = transform.position;

            List<Pickup> active = Pickup.Active;
            for (int i = 0; i < active.Count; i++)
            {
                Pickup pickup = active[i];
                if (pickup == null || pickup.IsAttracted)
                {
                    continue;
                }

                if (((Vector2)pickup.transform.position - position).sqrMagnitude <= sqrRadius)
                {
                    pickup.Attract(transform);
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            Gizmos.color = new Color(0.4f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, stats != null ? stats.MagnetRadius : 0f);
        }
    }
}
