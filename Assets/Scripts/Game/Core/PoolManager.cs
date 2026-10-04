using System;
using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Gameplay-facing pool API. All spawn/despawn goes through <see cref="ObjectPoolManager"/>.
    /// </summary>
    public class PoolManager : SingletonBehaviour<PoolManager>
    {
        [Serializable]
        private struct PrewarmEntry
        {
            public GameObject prefab;
            public int count;
        }

        [SerializeField] private PrewarmEntry[] prewarm = Array.Empty<PrewarmEntry>();

        protected override void Awake()
        {
            base.Awake();

            ObjectPoolManager backend = ObjectPoolManager.Instance;
            if (backend == null)
            {
                backend = gameObject.GetComponent<ObjectPoolManager>();
                if (backend == null)
                {
                    backend = gameObject.AddComponent<ObjectPoolManager>();
                }
            }

            for (int i = 0; i < prewarm.Length; i++)
            {
                PrewarmEntry entry = prewarm[i];
                if (entry.prefab == null || entry.count <= 0)
                {
                    continue;
                }

                backend.Prewarm(entry.prefab, entry.count);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            ObjectPoolManager.Instance?.Prewarm(prefab, count);
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            ObjectPoolManager backend = ObjectPoolManager.Instance;
            if (backend == null)
            {
                Debug.LogError("ObjectPoolManager is missing. Cannot spawn.");
                return null;
            }

            return backend.Spawn(prefab, position, rotation, parent);
        }

        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            ObjectPoolManager backend = ObjectPoolManager.Instance;
            if (backend == null || prefab == null)
            {
                return null;
            }

            return backend.Spawn(prefab, position, rotation, parent);
        }

        public void Despawn(GameObject instance)
        {
            ObjectPoolManager.Instance?.Return(instance);
        }

        public void Despawn(Component component)
        {
            if (component != null)
            {
                Despawn(component.gameObject);
            }
        }

        public void DespawnDelayed(GameObject instance, float delay)
        {
            ObjectPoolManager.Instance?.ReturnDelayed(instance, delay);
        }
    }
}
