using System;
using System.Collections.Generic;
using UnityEngine;

namespace KingsRescue.Core
{
    /// <summary>
    /// Prefab-keyed object pool. Survivors-style spawn counts on WebGL will GC-thrash
    /// without this, so every enemy, projectile, pickup and prop goes through it.
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

        private readonly Dictionary<GameObject, Stack<GameObject>> pools = new Dictionary<GameObject, Stack<GameObject>>();
        private readonly Dictionary<GameObject, Transform> roots = new Dictionary<GameObject, Transform>();

        protected override void Awake()
        {
            base.Awake();

            for (int i = 0; i < prewarm.Length; i++)
            {
                PrewarmEntry entry = prewarm[i];
                if (entry.prefab == null || entry.count <= 0)
                {
                    continue;
                }

                Prewarm(entry.prefab, entry.count);
            }
        }

        public void Prewarm(GameObject prefab, int count)
        {
            Stack<GameObject> pool = GetPool(prefab);
            for (int i = 0; i < count; i++)
            {
                GameObject instance = CreateInstance(prefab);
                instance.SetActive(false);
                pool.Push(instance);
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            if (prefab == null)
            {
                Debug.LogError("PoolManager.Spawn called with a null prefab.");
                return null;
            }

            Stack<GameObject> pool = GetPool(prefab);
            GameObject instance = pool.Count > 0 ? pool.Pop() : CreateInstance(prefab);

            Transform t = instance.transform;
            t.SetParent(parent != null ? parent : roots[prefab], false);
            t.SetPositionAndRotation(position, rotation);
            t.localScale = prefab.transform.localScale;

            instance.SetActive(true);
            instance.GetComponent<PooledObject>().NotifySpawned();
            return instance;
        }

        public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null) where T : Component
        {
            if (prefab == null)
            {
                Debug.LogError($"PoolManager.Spawn<{typeof(T).Name}> called with a null prefab.");
                return null;
            }

            GameObject instance = Spawn(prefab.gameObject, position, rotation, parent);
            return instance != null ? instance.GetComponent<T>() : null;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            PooledObject pooled = instance.GetComponent<PooledObject>();
            if (pooled == null || pooled.SourcePrefab == null)
            {
                Destroy(instance);
                return;
            }

            if (!pooled.IsSpawned)
            {
                return;
            }

            pooled.NotifyDespawned();
            instance.SetActive(false);
            instance.transform.SetParent(roots[pooled.SourcePrefab], false);
            GetPool(pooled.SourcePrefab).Push(instance);
        }

        public void Despawn(Component component)
        {
            if (component != null)
            {
                Despawn(component.gameObject);
            }
        }

        /// <summary>Despawns after a delay, used for one-shot VFX and projectile lifetimes.</summary>
        public void DespawnDelayed(GameObject instance, float delay)
        {
            if (instance == null)
            {
                return;
            }

            StartCoroutine(DespawnRoutine(instance, delay));
        }

        private System.Collections.IEnumerator DespawnRoutine(GameObject instance, float delay)
        {
            yield return new WaitForSeconds(delay);
            Despawn(instance);
        }

        private Stack<GameObject> GetPool(GameObject prefab)
        {
            if (pools.TryGetValue(prefab, out Stack<GameObject> pool))
            {
                return pool;
            }

            pool = new Stack<GameObject>();
            pools.Add(prefab, pool);

            var root = new GameObject($"Pool [{prefab.name}]").transform;
            root.SetParent(transform, false);
            roots.Add(prefab, root);

            return pool;
        }

        private GameObject CreateInstance(GameObject prefab)
        {
            GetPool(prefab);

            GameObject instance = Instantiate(prefab, roots[prefab]);
            instance.name = prefab.name;

            PooledObject pooled = instance.GetComponent<PooledObject>();
            if (pooled == null)
            {
                pooled = instance.AddComponent<PooledObject>();
            }

            pooled.Bind(prefab);
            return instance;
        }
    }
}
