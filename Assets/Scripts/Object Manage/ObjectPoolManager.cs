using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

/// <summary>
/// Prefab-keyed pool used by environments, props, enemies, pickups and VFX.
/// Every entry in <see cref="pools"/> is created in Awake, before gameplay starts.
/// Spawn only reactivates those copies. It never instantiates during play.
/// Tag-based SpawnFromPool remains for older callers.
/// </summary>
[DefaultExecutionOrder(-200)]
public class ObjectPoolManager : Singleton<ObjectPoolManager>
{
    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    public List<Pool> pools;

    private readonly Dictionary<string, HashSet<GameObject>> taggedActive = new Dictionary<string, HashSet<GameObject>>();
    private readonly Dictionary<string, Pool> taggedConfigs = new Dictionary<string, Pool>();

    private readonly Dictionary<GameObject, Stack<GameObject>> prefabPools = new Dictionary<GameObject, Stack<GameObject>>();
    private readonly Dictionary<GameObject, Transform> prefabRoots = new Dictionary<GameObject, Transform>();
    private readonly HashSet<GameObject> exhausted = new HashSet<GameObject>();
    private bool building;

    protected override void Awake()
    {
        base.Awake();
        InitializePools();
    }

    private void InitializePools()
    {
        if (pools == null)
        {
            return;
        }

        building = true;
        for (int i = 0; i < pools.Count; i++)
        {
            Pool pool = pools[i];
            if (pool.prefab == null || string.IsNullOrEmpty(pool.tag))
            {
                continue;
            }

            taggedConfigs[pool.tag] = pool;
            taggedActive[pool.tag] = new HashSet<GameObject>();
            Prewarm(pool.prefab, pool.size);
        }

        building = false;
    }

    public void Prewarm(GameObject prefab, int count)
    {
        if (!building || prefab == null || count <= 0)
        {
            return;
        }

        Stack<GameObject> pool = GetPrefabPool(prefab);
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
            Debug.LogError("[ObjectPoolManager] Spawn called with a null prefab.");
            return null;
        }

        Stack<GameObject> pool = GetPrefabPool(prefab);
        if (pool.Count == 0)
        {
            if (exhausted.Add(prefab))
            {
                Debug.LogWarning($"[ObjectPoolManager] Pool for '{prefab.name}' is empty. Increase its size. Nothing was created during play.");
            }

            return null;
        }

        GameObject instance = pool.Pop();

        Transform t = instance.transform;
        t.SetParent(parent != null ? parent : prefabRoots[prefab], false);
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
            return null;
        }

        GameObject instance = Spawn(prefab.gameObject, position, rotation, parent);
        return instance != null ? instance.GetComponent<T>() : null;
    }

    public void Return(GameObject instance)
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
        instance.transform.SetParent(prefabRoots[pooled.SourcePrefab], false);
        GetPrefabPool(pooled.SourcePrefab).Push(instance);
    }

    public void ReturnDelayed(GameObject instance, float delay)
    {
        if (instance != null)
        {
            StartCoroutine(ReturnRoutine(instance, delay));
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!taggedConfigs.TryGetValue(tag, out Pool config) || config.prefab == null)
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool with tag '{tag}' doesn't exist.");
            return null;
        }

        GameObject instance = Spawn(config.prefab, position, rotation);
        if (instance != null)
        {
            if (!taggedActive.TryGetValue(tag, out HashSet<GameObject> activeSet))
            {
                activeSet = new HashSet<GameObject>();
                taggedActive[tag] = activeSet;
            }

            activeSet.Add(instance);
        }

        return instance;
    }

    public void ReturnToPool(string tag, GameObject objectToReturn)
    {
        if (objectToReturn == null)
        {
            return;
        }

        if (taggedActive.TryGetValue(tag, out HashSet<GameObject> activeSet) && !activeSet.Remove(objectToReturn))
        {
            return;
        }

        Return(objectToReturn);
    }

    public void ExpandPool(string tag, int additionalSize)
    {
        if (!taggedConfigs.TryGetValue(tag, out Pool pool) || pool.prefab == null)
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool with tag {tag} doesn't exist.");
            return;
        }

        Debug.LogWarning($"[ObjectPoolManager] Pool '{tag}' is fixed at startup. Raise its size on ObjectPoolManager instead of expanding during play.");
    }

    public int GetActiveObjectCount(string tag)
    {
        return taggedActive.TryGetValue(tag, out HashSet<GameObject> set) ? set.Count : 0;
    }

    public bool IsObjectActive(string tag, GameObject obj)
    {
        return taggedActive.TryGetValue(tag, out HashSet<GameObject> set) && set.Contains(obj);
    }

    private Stack<GameObject> GetPrefabPool(GameObject prefab)
    {
        if (prefabPools.TryGetValue(prefab, out Stack<GameObject> pool))
        {
            return pool;
        }

        pool = new Stack<GameObject>();
        prefabPools.Add(prefab, pool);

        var root = new GameObject($"Pool [{prefab.name}]").transform;
        root.SetParent(transform, false);
        prefabRoots.Add(prefab, root);
        return pool;
    }

    private GameObject CreateInstance(GameObject prefab)
    {
        GetPrefabPool(prefab);

        GameObject instance = Instantiate(prefab, prefabRoots[prefab]);
        instance.name = prefab.name;

        PooledObject pooled = instance.GetComponent<PooledObject>();
        if (pooled == null)
        {
            pooled = instance.AddComponent<PooledObject>();
        }

        pooled.Bind(prefab);
        instance.SetActive(false);
        return instance;
    }

    private System.Collections.IEnumerator ReturnRoutine(GameObject instance, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        Return(instance);
    }
}
