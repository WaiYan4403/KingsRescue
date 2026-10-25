using UnityEngine;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// The two arcade power-ups. Each has its own prefab and drop chance.
    /// Luck raises both chances. A normal drop never becomes the max drop.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Power-Up Catalog", fileName = "PowerUpCatalog")]
    public class PowerUpCatalog : ScriptableObject
    {
        public GameObject pickupPrefab;

        public GameObject maxPickupPrefab;

        [Range(0f, 1f)] public float enemyDropChance = 0.012f;

        [Range(0f, 1f)] public float enemyMaxDropChance = 0.004f;

        [Range(0f, 1f)] public float chestDropChance = 0.28f;

        [Range(0f, 1f)] public float chestMaxDropChance = 0.07f;

        [Tooltip("Used when a smashed prop has already decided to drop a power-up, to choose the max one.")]
        [Range(0f, 1f)] public float propDropChance = 0.03f;

        [Range(0f, 1f)] public float propMaxDropChance = 0.008f;

        [SerializeField] private float popSpeed = 2.6f;

        private static PowerUpCatalog cached;

        public static PowerUpCatalog Load()
        {
            if (cached == null)
            {
                cached = Resources.Load<PowerUpCatalog>("PowerUpCatalog");
            }

            return cached;
        }

        public static void TryDrop(Vector2 origin, bool fromChest)
        {
            PowerUpCatalog catalog = Load();
            if (catalog == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            float luck = Luck();
            float normalChance = fromChest ? catalog.chestDropChance : catalog.enemyDropChance;
            float maxChance = fromChest ? catalog.chestMaxDropChance : catalog.enemyMaxDropChance;

            if (Roll(normalChance, luck))
            {
                catalog.Spawn(catalog.pickupPrefab, origin);
            }

            if (Roll(maxChance, luck))
            {
                catalog.Spawn(catalog.maxPickupPrefab, origin + (Vector2)Random.insideUnitCircle * 0.45f);
            }
        }

        public static void Spawn(Vector2 origin)
        {
            PowerUpCatalog catalog = Load();
            catalog?.Spawn(catalog.pickupPrefab, origin);
        }

        /// <summary>
        /// A prop already passed its own drop roll. That drop is usually the normal
        /// power-up. The max prefab takes the share of the two prop chances.
        /// </summary>
        public static void SpawnFromProp(Vector2 origin)
        {
            PowerUpCatalog catalog = Load();
            if (catalog == null)
            {
                return;
            }

            float normal = Mathf.Max(0f, catalog.propDropChance);
            float max = Mathf.Max(0f, catalog.propMaxDropChance);
            bool useMax = catalog.maxPickupPrefab != null
                && max > 0f
                && Random.value < max / Mathf.Max(0.0001f, normal + max);
            catalog.Spawn(useMax ? catalog.maxPickupPrefab : catalog.pickupPrefab, origin);
        }

        private void Spawn(GameObject prefab, Vector2 origin)
        {
            if (prefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            GameObject instance = ObjectPoolManager.Instance.Spawn(prefab, origin, Quaternion.identity);
            if (instance == null)
            {
                return;
            }

            Pickup pickup = instance.GetComponent<Pickup>();
            pickup?.Pop(Random.insideUnitCircle.normalized * popSpeed);
        }

        private static bool Roll(float chance, float luck)
        {
            return chance > 0f && Random.value < Mathf.Clamp01(chance * luck);
        }

        private static float Luck()
        {
            PlayerController hero = PlayerController.Instance;
            if (hero == null)
            {
                return 1f;
            }

            PlayerStats stats = hero.GetComponent<PlayerStats>();
            return stats != null ? Mathf.Max(0.1f, stats.Luck) : 1f;
        }
    }
}
