using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// One rare arcade pickup. Enemies and chests roll against these chances;
    /// Luck raises them.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Power-Up Catalog", fileName = "PowerUpCatalog")]
    public class PowerUpCatalog : ScriptableObject
    {
        public GameObject pickupPrefab;

        [Range(0f, 1f)] public float enemyDropChance = 0.012f;

        [Range(0f, 1f)] public float chestDropChance = 0.28f;

        [Tooltip("Smashable pots and boxes. Keep this below their health drop chance.")]
        [Range(0f, 1f)] public float propDropChance = 0.03f;

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
            if (catalog == null || catalog.pickupPrefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            float luck = 1f;
            PlayerController hero = PlayerController.Instance;
            if (hero != null)
            {
                PlayerStats stats = hero.GetComponent<PlayerStats>();
                if (stats != null)
                {
                    luck = Mathf.Max(0.1f, stats.Luck);
                }
            }

            float chance = fromChest ? catalog.chestDropChance : catalog.enemyDropChance;
            if (Random.value >= Mathf.Clamp01(chance * luck))
            {
                return;
            }

            Spawn(origin);
        }

        public static void Spawn(Vector2 origin)
        {
            PowerUpCatalog catalog = Load();
            if (catalog == null || catalog.pickupPrefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            GameObject instance = ObjectPoolManager.Instance.Spawn(catalog.pickupPrefab, origin, Quaternion.identity);
            if (instance == null)
            {
                return;
            }

            Pickup pickup = instance.GetComponent<Pickup>();
            pickup?.Pop(Random.insideUnitCircle.normalized * catalog.popSpeed);
        }
    }
}
