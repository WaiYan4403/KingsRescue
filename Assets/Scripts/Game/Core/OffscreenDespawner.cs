using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Enforces the "nothing lives outside the boundary" rule: anything that scrolls
    /// past the despawn margin goes straight back to its pool.
    /// </summary>
    public class OffscreenDespawner : MonoBehaviour
    {
        [Tooltip("Seconds between bounds checks. Staggered so hundreds of objects do not all test on the same frame.")]
        [SerializeField] private float checkInterval = 0.2f;

        [Tooltip("Grace period after spawning, so objects placed above the screen are not culled instantly.")]
        [SerializeField] private float spawnGrace = 0.25f;

        private float nextCheck;
        private float armedAt;

        private void OnEnable()
        {
            armedAt = Time.time + spawnGrace;
            nextCheck = armedAt + Random.value * checkInterval;
        }

        private void Update()
        {
            if (Time.time < nextCheck)
            {
                return;
            }

            nextCheck = Time.time + checkInterval;

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return;
            }

            if (bounds.IsPastDespawn(transform.position))
            {
                Recycle();
            }
        }

        private void Recycle()
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
