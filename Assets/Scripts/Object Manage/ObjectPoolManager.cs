using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class ObjectPoolManager : PersistentSingleton<ObjectPoolManager>
{
    [System.Serializable]
    public class Pool
    {
        public string tag;
        public GameObject prefab;
        public int size;
    }

    public List<Pool> pools;
    private Dictionary<string, Queue<GameObject>> poolDictionary;
    private Dictionary<string, HashSet<GameObject>> activeObjects;
    private Dictionary<string, Pool> poolConfigs;

    [SerializeField] int expandBatchSize = 8;

    private void Start()
    {
        InitializePools();
    }

    private void InitializePools()
    {
        poolDictionary = new Dictionary<string, Queue<GameObject>>();
        activeObjects = new Dictionary<string, HashSet<GameObject>>();
        poolConfigs = new Dictionary<string, Pool>();

        if (pools == null) return;

        foreach (var pool in pools)
        {
            if (pool.prefab == null)
            {
                Debug.LogError($"[ObjectPoolManager] Prefab is null for pool with tag: {pool.tag}. Skipping this pool.");
                continue;
            }

            Queue<GameObject> objectPool = new Queue<GameObject>();
            activeObjects[pool.tag] = new HashSet<GameObject>();
            poolConfigs[pool.tag] = pool;

            for (int i = 0; i < pool.size; i++)
            {
                GameObject obj = Instantiate(pool.prefab, transform.position, Quaternion.identity);
                if (obj == null) continue;

                obj.transform.SetParent(transform);
                obj.transform.localScale = Vector3.one;
                obj.SetActive(false);
                objectPool.Enqueue(obj);
            }

            poolDictionary[pool.tag] = objectPool;
        }
    }

    public void ExpandPool(string tag, int additionalSize)
    {
        if (!poolConfigs.TryGetValue(tag, out Pool pool))
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool with tag {tag} doesn't exist.");
            return;
        }

        Queue<GameObject> objectPool = poolDictionary[tag];
        for (int i = 0; i < additionalSize; i++)
        {
            GameObject obj = Instantiate(pool.prefab, transform.position, Quaternion.identity);
            obj.transform.SetParent(transform);
            obj.transform.localScale = Vector3.one;
            obj.SetActive(false);
            objectPool.Enqueue(obj);
        }
    }

    public GameObject SpawnFromPool(string tag, Vector3 position, Quaternion rotation)
    {
        if (!poolDictionary.TryGetValue(tag, out Queue<GameObject> queue))
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool with tag '{tag}' doesn't exist.");
            return null;
        }

        // Expand in small batches to avoid hitching dozens of Instantiates at once.
        if (queue.Count == 0)
            ExpandPool(tag, Mathf.Max(1, expandBatchSize));

        GameObject objectToSpawn = queue.Dequeue();

        if (objectToSpawn == null)
        {
            if (!poolConfigs.TryGetValue(tag, out Pool config) || config.prefab == null)
                return null;

            objectToSpawn = Instantiate(config.prefab, transform.position, Quaternion.identity);
            objectToSpawn.transform.SetParent(transform);
            objectToSpawn.transform.localScale = Vector3.one;
        }

        if (objectToSpawn.activeInHierarchy)
        {
            queue.Enqueue(objectToSpawn);
            return null;
        }

        objectToSpawn.SetActive(true);
        objectToSpawn.transform.position = position;
        objectToSpawn.transform.rotation = rotation;
        objectToSpawn.transform.localScale = Vector3.one;

        if (!activeObjects.TryGetValue(tag, out HashSet<GameObject> activeSet))
        {
            activeSet = new HashSet<GameObject>();
            activeObjects[tag] = activeSet;
        }
        activeSet.Add(objectToSpawn);

        return objectToSpawn;
    }

    public void ReturnToPool(string tag, GameObject objectToReturn)
    {
        if (!poolDictionary.TryGetValue(tag, out Queue<GameObject> queue))
        {
            Debug.LogWarning($"[ObjectPoolManager] Pool with tag {tag} doesn't exist.");
            return;
        }

        if (objectToReturn == null)
            return;

        // Guard against double-returns corrupting the queue with duplicate refs.
        if (activeObjects.TryGetValue(tag, out HashSet<GameObject> activeSet))
        {
            if (!activeSet.Remove(objectToReturn))
                return;
        }

        DOTween.Kill(objectToReturn.transform);
        objectToReturn.SetActive(false);
        objectToReturn.transform.localScale = Vector3.one;
        objectToReturn.transform.SetParent(transform);
        queue.Enqueue(objectToReturn);
    }

    public int GetActiveObjectCount(string tag)
    {
        return activeObjects.TryGetValue(tag, out HashSet<GameObject> set) ? set.Count : 0;
    }

    public bool IsObjectActive(string tag, GameObject obj)
    {
        return activeObjects.TryGetValue(tag, out HashSet<GameObject> set) && set.Contains(obj);
    }
}
