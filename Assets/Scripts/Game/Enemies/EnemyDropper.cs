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

        private EnemyDefinition definition;

        public void Configure(EnemyDefinition def)
        {
            definition = def;
        }

        public void DropRewards()
        {
            if (definition == null || PoolManager.Instance == null)
            {
                return;
            }

            Vector2 origin = transform.position;

            if (xpGemPrefab != null && definition.xpValue > 0)
            {
                XpGem gem = SpawnPickup(xpGemPrefab, origin) as XpGem;
                gem?.SetValue(definition.xpValue);
            }

            if (coinPrefab != null && Random.value < definition.coinDropChance)
            {
                SpawnPickup(coinPrefab, origin);
            }

            if (healthPrefab != null && Random.value < definition.healthDropChance)
            {
                SpawnPickup(healthPrefab, origin);
            }
        }

        private Pickup SpawnPickup(GameObject prefab, Vector2 origin)
        {
            GameObject instance = PoolManager.Instance.Spawn(prefab, origin, Quaternion.identity);
            if (instance == null)
            {
                return null;
            }

            Pickup pickup = instance.GetComponent<Pickup>();
            if (pickup == null)
            {
                Debug.LogError($"Pickup prefab '{prefab.name}' has no Pickup component.", prefab);
                return null;
            }

            pickup.Pop(Random.insideUnitCircle.normalized * popSpeed);
            return pickup;
        }
    }
}
