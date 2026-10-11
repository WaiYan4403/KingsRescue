using System;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.World
{
    /// <summary>
    /// One ground/road tile that scrolls with the world. Trees and breakables live
    /// on the segment so they stay glued to the art instead of spawning on a timer.
    /// </summary>
    public class BackgroundSegment : MonoBehaviour, IPoolable
    {
        [Serializable]
        public struct PropEntry
        {
            public GameObject prefab;

            [Tooltip("Relative chance of being chosen. 0 disables it.")]
            public float weight;
        }

        [Header("Layout")]
        [Tooltip("Leave 0 to use the ground sprite height.")]
        [SerializeField] private float heightOverride;

        [SerializeField] private SpriteRenderer groundRenderer;
        [SerializeField] private Transform obstaclesRoot;
        [SerializeField] private TreesGenerator trees;

        [Header("Breakables (drop coins)")]
        [SerializeField] private PropEntry[] breakables = Array.Empty<PropEntry>();
        [SerializeField] private Vector2Int breakableCount = new Vector2Int(1, 2);

        [Header("Decoration (no loot)")]
        [SerializeField] private PropEntry[] decorations = Array.Empty<PropEntry>();
        [SerializeField] private Vector2Int decorationCount = new Vector2Int(2, 4);

        [Header("Blockers (stop the player)")]
        [SerializeField] private PropEntry[] blockers = Array.Empty<PropEntry>();
        [SerializeField] private Vector2Int blockerCount = new Vector2Int(1, 2);

        [Header("Placement")]
        [SerializeField] private float horizontalRange = 2.8f;
        [SerializeField] private float verticalPadding = 1.2f;
        [SerializeField] private int placementAttempts = 4;
        [SerializeField] private float minHorizontalSeparation = 1.2f;

        private readonly List<GameObject> spawned = new List<GameObject>();
        private readonly List<IPoolable[]> propPoolables = new List<IPoolable[]>();
        private float cachedHeight;

        public float Height
        {
            get
            {
                if (cachedHeight > 0f)
                {
                    return cachedHeight;
                }

                if (heightOverride > 0f)
                {
                    cachedHeight = heightOverride;
                    return cachedHeight;
                }

                SpriteRenderer renderer = groundRenderer != null
                    ? groundRenderer
                    : GetComponent<SpriteRenderer>();
                if (renderer != null && renderer.sprite != null)
                {
                    float scaleY = Mathf.Abs(renderer.transform.localScale.y);
                    cachedHeight = renderer.sprite.rect.height / renderer.sprite.pixelsPerUnit * scaleY;
                    return cachedHeight;
                }

                cachedHeight = 20f;
                return cachedHeight;
            }
        }

        public float TopY => transform.position.y + Height * 0.5f;

        public float BottomY => transform.position.y - Height * 0.5f;

        private void Awake()
        {
            if (groundRenderer == null)
            {
                groundRenderer = GetComponent<SpriteRenderer>();
            }

            if (obstaclesRoot == null)
            {
                obstaclesRoot = transform;
            }

            if (trees == null)
            {
                trees = GetComponentInChildren<TreesGenerator>(true);
            }
        }

        public void OnSpawned()
        {
            Prepare();
        }

        public void OnDespawned()
        {
            ClearSpawned();
        }

        public void Scroll(float deltaY)
        {
            transform.position += Vector3.down * deltaY;
        }

        public void Prepare()
        {
            trees?.Generate(Height);
            RefreshProps();
        }

        public void ClearSpawned()
        {
            trees?.ClearSpawned();

            for (int i = 0; i < spawned.Count; i++)
            {
                if (IsOwned(spawned[i]))
                {
                    Release(spawned[i]);
                }
            }

            spawned.Clear();
            propPoolables.Clear();
        }

        private void RefreshProps()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (IsOwned(spawned[i]))
                {
                    continue;
                }

                spawned.RemoveAt(i);
                propPoolables.RemoveAt(i);
            }

            if (spawned.Count == 0)
            {
                SpawnProps(breakables, RandomCount(breakableCount), true);
                SpawnProps(blockers, RandomCount(blockerCount), true);
                SpawnProps(decorations, RandomCount(decorationCount), false);
                return;
            }

            float lastX = float.NegativeInfinity;
            for (int i = 0; i < spawned.Count; i++)
            {
                Relocate(spawned[i], true, ref lastX);
                IPoolable[] poolables = propPoolables[i];
                for (int p = 0; p < poolables.Length; p++)
                {
                    poolables[p].OnSpawned();
                }
            }

            int missing = RandomCount(breakableCount) + RandomCount(blockerCount) + RandomCount(decorationCount) - spawned.Count;
            if (missing > 0)
            {
                SpawnProps(blockers, Mathf.Min(missing, 1), true);
                missing = RandomCount(breakableCount) + RandomCount(blockerCount) + RandomCount(decorationCount) - spawned.Count;
                if (missing > 0)
                {
                    SpawnProps(decorations, missing, false);
                }
            }
        }

        private void SpawnProps(PropEntry[] entries, int count, bool trackSeparation)
        {
            float lastX = float.NegativeInfinity;
            for (int n = 0; n < count; n++)
            {
                GameObject prefab = PickWeighted(entries);
                if (prefab == null)
                {
                    return;
                }

                Vector3 local = PlaceLocal(trackSeparation, ref lastX);
                Vector3 world = obstaclesRoot.TransformPoint(local);
                GameObject instance = SpawnChild(prefab, world, obstaclesRoot);
                if (instance == null || instance.GetComponent<KingsRescue.Pickups.Pickup>() != null)
                {
                    continue;
                }

                spawned.Add(instance);
                propPoolables.Add(instance.GetComponentsInChildren<IPoolable>(true));
            }
        }

        private void Relocate(GameObject instance, bool trackSeparation, ref float lastX)
        {
            instance.transform.localPosition = PlaceLocal(trackSeparation, ref lastX);
        }

        private Vector3 PlaceLocal(bool trackSeparation, ref float lastX)
        {
            float half = Height * 0.5f;
            float yMin = -half + verticalPadding;
            float yMax = half - verticalPadding;
            if (yMax < yMin)
            {
                yMin = -half;
                yMax = half;
            }

            float x = RandomX();
            if (trackSeparation)
            {
                for (int i = 0; i < placementAttempts && Mathf.Abs(x - lastX) < minHorizontalSeparation; i++)
                {
                    x = RandomX();
                }

                lastX = x;
            }

            return new Vector3(x, UnityEngine.Random.Range(yMin, yMax), 0f);
        }

        private bool IsOwned(GameObject instance)
        {
            return instance != null && instance.activeInHierarchy && instance.transform.parent == obstaclesRoot;
        }

        private float RandomX()
        {
            return UnityEngine.Random.Range(-horizontalRange, horizontalRange);
        }

        private static int RandomCount(Vector2Int range)
        {
            int min = Mathf.Max(0, Mathf.Min(range.x, range.y));
            int max = Mathf.Max(0, Mathf.Max(range.x, range.y));
            return UnityEngine.Random.Range(min, max + 1);
        }

        private static GameObject PickWeighted(PropEntry[] entries)
        {
            float total = 0f;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].prefab != null)
                {
                    total += Mathf.Max(0f, entries[i].weight);
                }
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].prefab == null)
                {
                    continue;
                }

                roll -= Mathf.Max(0f, entries[i].weight);
                if (roll <= 0f)
                {
                    return entries[i].prefab;
                }
            }

            return null;
        }

        internal static GameObject SpawnChild(GameObject prefab, Vector3 worldPosition, Transform parent)
        {
            bool loosePickup = prefab.GetComponent<KingsRescue.Pickups.Pickup>() != null;
            PoolManager pool = PoolManager.Instance;
            GameObject instance = pool != null
                ? pool.Spawn(prefab, worldPosition, Quaternion.identity, loosePickup ? null : parent)
                : Instantiate(prefab, worldPosition, Quaternion.identity, loosePickup ? null : parent);

            if (instance == null)
            {
                return null;
            }

            if (!loosePickup)
            {
                SetIndependentMotion(instance, false);
            }

            return instance;
        }

        internal static void Release(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            SetIndependentMotion(instance, true);

            PoolManager pool = PoolManager.Instance;
            if (pool != null)
            {
                pool.Despawn(instance);
            }
            else
            {
                Destroy(instance);
            }
        }

        private static void SetIndependentMotion(GameObject instance, bool enabled)
        {
            Scrollable scrollable = instance.GetComponent<Scrollable>();
            if (scrollable != null)
            {
                scrollable.enabled = enabled;
            }

            OffscreenDespawner despawner = instance.GetComponent<OffscreenDespawner>();
            if (despawner != null)
            {
                despawner.enabled = enabled;
            }
        }

        private void OnDestroy()
        {
            ClearSpawned();
        }
    }
}
