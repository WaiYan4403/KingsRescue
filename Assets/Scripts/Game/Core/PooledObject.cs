using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Added automatically to pooled instances so they can find their way home.
    /// </summary>
    [DisallowMultipleComponent]
    public class PooledObject : MonoBehaviour
    {
        private IPoolable[] poolables;

        public GameObject SourcePrefab { get; private set; }

        public bool IsSpawned { get; private set; }

        /// <summary>Cached at bind time so callers never need GetComponentsInChildren during play.</summary>
        public IPoolable[] Poolables => poolables ?? System.Array.Empty<IPoolable>();

        internal void Bind(GameObject prefab)
        {
            SourcePrefab = prefab;
            poolables = GetComponentsInChildren<IPoolable>(true);
        }

        internal void NotifySpawned()
        {
            IsSpawned = true;
            if (poolables == null)
            {
                return;
            }

            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnSpawned();
            }
        }

        internal void NotifyDespawned()
        {
            IsSpawned = false;
            if (poolables == null)
            {
                return;
            }

            for (int i = 0; i < poolables.Length; i++)
            {
                poolables[i].OnDespawned();
            }
        }
    }
}
