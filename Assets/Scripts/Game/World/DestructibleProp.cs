using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Pickups;

namespace KingsRescue.World
{
    /// <summary>
    /// Pots, barrels and chests. Coins only come from scenery, which is what makes
    /// detouring toward a chest worth the risk of walking into a mob.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DestructibleProp : MonoBehaviour, IPoolable
    {
        [Header("Loot")]
        [SerializeField] private GameObject coinPrefab;
        [SerializeField] private int minCoins = 1;
        [SerializeField] private int maxCoins = 3;
        [SerializeField] private int coinValue = 1;

        [SerializeField] private GameObject healthPrefab;
        [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.08f;

        [Header("Feedback")]
        [SerializeField] private GameObject breakEffectPrefab;
        [SerializeField] private float popSpeed = 3.2f;

        private Health health;
        private bool broken;

        private void Awake()
        {
            health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            health.Died -= OnDied;
        }

        public void OnSpawned()
        {
            broken = false;
        }

        public void OnDespawned()
        {
            broken = true;
        }

        private void OnDied(Health source, DamageInfo info)
        {
            if (broken)
            {
                return;
            }

            broken = true;
            DropLoot();

            PoolManager pool = PoolManager.Instance;

            if (breakEffectPrefab != null && pool != null)
            {
                pool.Spawn(breakEffectPrefab, transform.position, Quaternion.identity);
            }

            if (pool != null)
            {
                pool.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void DropLoot()
        {
            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            Vector2 origin = transform.position;

            if (coinPrefab != null)
            {
                int count = Random.Range(Mathf.Max(0, minCoins), Mathf.Max(minCoins, maxCoins) + 1);
                for (int i = 0; i < count; i++)
                {
                    CoinPickup coin = SpawnPickup(pool, coinPrefab, origin) as CoinPickup;
                    coin?.SetValue(coinValue);
                }
            }

            if (healthPrefab != null && Random.value < healthDropChance)
            {
                SpawnPickup(pool, healthPrefab, origin);
            }
        }

        private Pickup SpawnPickup(PoolManager pool, GameObject prefab, Vector2 origin)
        {
            GameObject instance = pool.Spawn(prefab, origin, Quaternion.identity);
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
