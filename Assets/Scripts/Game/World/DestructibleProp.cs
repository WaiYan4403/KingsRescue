using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Meta;
using KingsRescue.Pickups;
using KingsRescue.Player;
using KingsRescue.UI;

namespace KingsRescue.World
{
    /// <summary>
    /// Pots crack then dump loot. Chests crack then open the ChestPanel overlay.
    /// Gold pockets are collected whole. Opened scenery stays until it scrolls off.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DestructibleProp : MonoBehaviour, IPoolable
    {
        public enum UseMode
        {
            Smash = 0,
            Collect = 1
        }

        [Header("Use")]
        [SerializeField] private UseMode use = UseMode.Smash;

        [Header("Sprites")]
        [SerializeField] private SpriteRenderer body;
        [SerializeField] private Sprite intactSprite;
        [SerializeField] private Sprite crackedSprite;
        [SerializeField] private Sprite openedSprite;

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

        [Header("Gold pocket")]
        [SerializeField] private int minPocketCoins = 10;
        [SerializeField] private int maxPocketCoins = 100;
        [SerializeField] private float collectRadius = 0.7f;

        [Header("Feedback")]
        [SerializeField] private GameObject breakEffectPrefab;
        [SerializeField] private float popSpeed = 3.2f;

        private Health health;
        private HitFlash flash;
        private bool opened;
        private bool collected;

        private void Awake()
        {
            health = GetComponent<Health>();
            flash = GetComponent<HitFlash>();
            if (body == null)
            {
                body = GetComponent<SpriteRenderer>();
            }
        }

        private void OnEnable()
        {
            health.Damaged += OnDamaged;
            health.Died += OnBroken;
            if (use == UseMode.Collect)
            {
                health.Untargetable = true;
            }
        }

        private void OnDisable()
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnBroken;
        }

        public void OnSpawned()
        {
            opened = false;
            collected = false;
            ApplySprite(intactSprite != null ? intactSprite : body != null ? body.sprite : null);

            if (use == UseMode.Collect)
            {
                health.Untargetable = true;
            }
        }

        public void OnDespawned()
        {
            opened = true;
            collected = true;
        }

        private void Update()
        {
            if (use != UseMode.Collect || collected)
            {
                return;
            }

            health.Untargetable = true;

            PlayerController hero = PlayerController.Instance;
            if (hero == null || hero.Health == null || !hero.Health.IsAlive)
            {
                return;
            }

            float reach = collectRadius;
            PlayerStats stats = hero.GetComponent<PlayerStats>();
            if (stats != null)
            {
                reach = Mathf.Max(reach, stats.MagnetRadius);
            }

            if (((Vector2)transform.position - (Vector2)hero.transform.position).sqrMagnitude <= reach * reach)
            {
                CollectPocket(hero);
            }
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (use != UseMode.Smash || opened)
            {
                return;
            }

            flash?.Flash();
            if (health.Current > 0f)
            {
                ApplySprite(crackedSprite != null ? crackedSprite : intactSprite);
            }
        }

        private void OnBroken(Health source, DamageInfo info)
        {
            if (use != UseMode.Smash || opened)
            {
                return;
            }

            opened = true;
            health.Untargetable = true;
            flash?.Flash();
            ApplySprite(openedSprite != null ? openedSprite : crackedSprite);

            if (isChest)
            {
                AchievementProgress.RecordChest();
                if (ChestPanel.Instance != null)
                {
                    ChestPanel.Instance.Open(minCoins, maxCoins, coinValue);
                    return;
                }
            }

            DropLoot();
        }

        private void CollectPocket(PlayerController hero)
        {
            if (collected)
            {
                return;
            }

            collected = true;
            int amount = Random.Range(Mathf.Max(1, minPocketCoins), Mathf.Max(minPocketCoins, maxPocketCoins) + 1);
            PlayerStats stats = hero.GetComponent<PlayerStats>();
            float multiplier = stats != null ? stats.CoinGain : 1f;
            GameManager.Instance?.AddCoins(Mathf.Max(1, Mathf.RoundToInt(amount * multiplier)));

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void ApplySprite(Sprite sprite)
        {
            if (body != null && sprite != null)
            {
                body.sprite = sprite;
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

            if (breakEffectPrefab != null)
            {
                pool.Spawn(breakEffectPrefab, origin, Quaternion.identity);
            }

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
