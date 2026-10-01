using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Returns a pooled one-shot (swing arcs, impact bursts, aura pulses) after a fixed life.
    /// </summary>
    public class AutoDespawn : MonoBehaviour, IPoolable
    {
        [SerializeField] private float lifetime = 0.35f;

        private float expireAt;

        public void SetLifetime(float value)
        {
            lifetime = value;
            expireAt = Time.time + lifetime;
        }

        public void OnSpawned()
        {
            expireAt = Time.time + lifetime;
        }

        public void OnDespawned()
        {
        }

        private void OnEnable()
        {
            expireAt = Time.time + lifetime;
        }

        private void Update()
        {
            if (Time.time < expireAt)
            {
                return;
            }

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
