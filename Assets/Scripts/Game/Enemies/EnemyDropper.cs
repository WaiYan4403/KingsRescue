using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Pickups;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Turns a corpse into loot. EXP always, coins and heals on a roll.
    /// </summary>
    public class EnemyDropper : MonoBehaviour
    {
        [Header("Prefabs")]
        [SerializeField] private GameObject xpGemPrefab;
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private GameObject healthPrefab;

        [Header("Scatter")]
        [SerializeField] private float popSpeed = 2.2f;

        private readonly List<int> gemValues = new List<int>(XpGem.MaxGemsPerDrop);

        private EnemyDefinition definition;

        private struct PendingDrop
        {
            public GameObject prefab;
            public Vector2 position;
            public Vector2 pop;
            public int gemValue;
        }

        private static readonly List<PendingDrop> pendingDrops = new List<PendingDrop>(64);
        private static int pendingHead;

        /// <summary>Spawns a few queued gems per frame so a crowded kill does not activate them all at once.</summary>
        public static void TickPending()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                pendingDrops.Clear();
                pendingHead = 0;
                return;
            }

            int budget = 3;
            while (budget > 0 && pendingHead < pendingDrops.Count)
            {
                SpawnNow(pool, pendingDrops[pendingHead]);
                pendingHead++;
                budget--;
            }

            if (pendingHead > 24)
            {
                pendingDrops.RemoveRange(0, pendingHead);
                pendingHead = 0;
            }
        }

        public void Configure(EnemyDefinition def)
        {
            definition = def;
        }

        public void DropRewards()
        {
            if (definition == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            Vector2 origin = transform.position;

            if (xpGemPrefab != null && definition.xpValue > 0)
            {
                XpGem.Split(definition.xpValue, gemValues);
                for (int i = 0; i < gemValues.Count; i++)
                {
                    Enqueue(xpGemPrefab, origin, gemValues[i]);
                }
            }

            if (coinPrefab != null && Random.value < definition.coinDropChance)
            {
                Enqueue(coinPrefab, origin, 0);
            }

            if (healthPrefab != null && Random.value < definition.healthDropChance)
            {
                Enqueue(healthPrefab, origin, 0);
            }

            PowerUpCatalog.TryDrop(origin, false);
        }

        private void Enqueue(GameObject prefab, Vector2 origin, int gemValue)
        {
            if (prefab == null)
            {
                return;
            }

            pendingDrops.Add(new PendingDrop
            {
                prefab = prefab,
                position = origin,
                pop = Random.insideUnitCircle.normalized * popSpeed,
                gemValue = gemValue
            });
        }

        private static void SpawnNow(ObjectPoolManager pool, PendingDrop drop)
        {
            GameObject instance = pool.Spawn(drop.prefab, drop.position, Quaternion.identity);
            if (instance == null)
            {
                return;
            }

            Pickup pickup = instance.GetComponent<Pickup>();
            if (pickup == null)
            {
                Debug.LogError($"Pickup prefab '{drop.prefab.name}' has no Pickup component.", drop.prefab);
                return;
            }

            if (drop.gemValue > 0)
            {
                (pickup as XpGem)?.SetValue(drop.gemValue);
            }

            pickup.Pop(drop.pop);
        }
    }
}
