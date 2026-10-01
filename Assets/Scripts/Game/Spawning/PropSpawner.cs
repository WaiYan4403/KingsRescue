using System;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Spawning
{
    /// <summary>
    /// Scatters scenery into the scrolling lane: breakable loot props and purely
    /// decorative dressing so the ground does not read as an empty treadmill.
    /// </summary>
    public class PropSpawner : MonoBehaviour
    {
        [Serializable]
        private struct PropEntry
        {
            public GameObject prefab;

            [Tooltip("Relative chance of being chosen. 0 disables it.")]
            public float weight;
        }

        [Header("Breakables (drop coins)")]
        [SerializeField] private PropEntry[] breakables = Array.Empty<PropEntry>();
        [SerializeField] private Vector2 breakableInterval = new Vector2(2.5f, 5f);

        [Header("Decoration (no loot)")]
        [SerializeField] private PropEntry[] decorations = Array.Empty<PropEntry>();
        [SerializeField] private Vector2 decorationInterval = new Vector2(0.6f, 1.6f);

        [Header("Placement")]
        [SerializeField] private float horizontalInset = 0.8f;

        [Tooltip("Keeps props from spawning right on top of each other horizontally.")]
        [SerializeField] private int placementAttempts = 4;

        [SerializeField] private float minHorizontalSeparation = 1.2f;

        private float nextBreakableTime;
        private float nextDecorationTime;
        private float lastBreakableX = float.NegativeInfinity;

        private void Start()
        {
            nextBreakableTime = Time.time + UnityEngine.Random.Range(breakableInterval.x, breakableInterval.y);
            nextDecorationTime = Time.time + UnityEngine.Random.Range(decorationInterval.x, decorationInterval.y);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying)
            {
                return;
            }

            if (Time.time >= nextBreakableTime)
            {
                nextBreakableTime = Time.time + UnityEngine.Random.Range(breakableInterval.x, breakableInterval.y);
                SpawnFrom(breakables, true);
            }

            if (Time.time >= nextDecorationTime)
            {
                nextDecorationTime = Time.time + UnityEngine.Random.Range(decorationInterval.x, decorationInterval.y);
                SpawnFrom(decorations, false);
            }
        }

        private void SpawnFrom(PropEntry[] entries, bool trackSeparation)
        {
            GameObject prefab = PickWeighted(entries);
            if (prefab == null)
            {
                return;
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            PoolManager pool = PoolManager.Instance;
            if (bounds == null || pool == null)
            {
                return;
            }

            float x = bounds.RandomSpawnX(horizontalInset);

            if (trackSeparation)
            {
                for (int i = 0; i < placementAttempts && Mathf.Abs(x - lastBreakableX) < minHorizontalSeparation; i++)
                {
                    x = bounds.RandomSpawnX(horizontalInset);
                }

                lastBreakableX = x;
            }

            pool.Spawn(prefab, new Vector3(x, bounds.TopSpawnY, 0f), Quaternion.identity);
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
    }
}
