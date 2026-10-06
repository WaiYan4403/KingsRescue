using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Pickups;
using KingsRescue.Player;
using KingsRescue.Progression;

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

        [Header("Chest")]
        [Tooltip("Chests can unlock a random weapon or passive. Luck raises that chance.")]
        [SerializeField] private bool isChest;
        [SerializeField, Range(0f, 1f)] private float rareDropChance = 0.2f;

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
            float luck = ResolveLuck();

            if (coinPrefab != null)
            {
                int count = Random.Range(Mathf.Max(0, minCoins), Mathf.Max(minCoins, maxCoins) + 1);
                for (int i = 0; i < count; i++)
                {
                    CoinPickup coin = SpawnPickup(pool, coinPrefab, origin) as CoinPickup;
                    coin?.SetValue(coinValue);
                }
            }

            if (healthPrefab != null && Random.value < Mathf.Clamp01(healthDropChance * luck))
            {
                SpawnPickup(pool, healthPrefab, origin);
            }

            if (isChest)
            {
                TryDropRareItem(pool, origin, luck);
            }
        }

        private void TryDropRareItem(PoolManager pool, Vector2 origin, float luck)
        {
            float chance = Mathf.Clamp01(rareDropChance * luck);
            if (Random.value >= chance)
            {
                return;
            }

            UpgradeService upgrades = UpgradeService.Instance;
            if (upgrades != null && upgrades.TryGrantRandomItem(out UpgradeDefinition granted))
            {
                Debug.Log($"Lucky chest unlocked {granted.Title(upgrades.Loadout)}.", this);
                return;
            }

            if (coinPrefab == null)
            {
                return;
            }

            int bonus = Mathf.Max(3, coinValue * 5);
            CoinPickup coin = SpawnPickup(pool, coinPrefab, origin) as CoinPickup;
            coin?.SetValue(bonus);
        }

        private static float ResolveLuck()
        {
            PlayerStats stats = Object.FindAnyObjectByType<PlayerStats>();
            return stats != null ? Mathf.Max(0.1f, stats.Luck) : 1f;
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
