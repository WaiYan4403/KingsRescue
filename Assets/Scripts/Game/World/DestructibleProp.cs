using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Meta;
using KingsRescue.Pickups;
using KingsRescue.Player;
using KingsRescue.UI;

namespace KingsRescue.World
{
    /// <summary>
    /// Pots and boxes crack, then dump loot. Chests open when the player walks up to them.
    /// Gold pockets are collected whole. Opened scenery stays until it scrolls off.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class DestructibleProp : MonoBehaviour, IPoolable
    {
        public enum UseMode
        {
            Smash = 0,
            Collect = 1,
            Interact = 2
        }

        public enum BreakSound
        {
            Pot = 0,
            Box = 1
        }

        [Header("Use")]
        [SerializeField] private UseMode use = UseMode.Smash;

        [Tooltip("Which break clip plays when this prop is smashed.")]
        [SerializeField] private BreakSound breakSound = BreakSound.Pot;

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
        [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.18f;

        [Tooltip("Rolled only when a health pickup does not drop. Keep this lower than health.")]
        [SerializeField, Range(0f, 1f)] private float powerUpDropChance = 0.03f;

        [Header("Chest")]
        [Tooltip("Chests open when the player walks up to them. Weapons do not damage them.")]
        [SerializeField] private bool isChest;

        public bool IsChest => isChest;
        [SerializeField] private float interactRadius = 1.15f;
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
            if (IgnoresWeapons)
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

            if (IgnoresWeapons)
            {
                health.Untargetable = true;
            }
            else
            {
                health.ConfigureSmashProp(2f);
                health.UseVisibleBody(body);
            }
        }

        public void OnDespawned()
        {
            opened = true;
            collected = true;
        }

        private bool IgnoresWeapons => isChest || use == UseMode.Interact || use == UseMode.Collect;

        private void Update()
        {
            if (isChest || use == UseMode.Interact)
            {
                TryOpenChest();
                return;
            }

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

        private void TryOpenChest()
        {
            if (opened)
            {
                return;
            }

            health.Untargetable = true;

            PlayerController hero = PlayerController.Instance;
            if (hero == null || hero.Health == null || !hero.Health.IsAlive)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game != null && (game.State == GameState.GameOver || game.State == GameState.Boot))
            {
                return;
            }

            Vector2 heroPosition = hero.transform.position;
            Vector2 nearest = body != null && body.sprite != null
                ? (Vector2)body.bounds.ClosestPoint(heroPosition)
                : (Vector2)transform.position;
            float reach = Mathf.Max(0.45f, interactRadius);
            if ((heroPosition - nearest).sqrMagnitude > reach * reach)
            {
                return;
            }

            opened = true;
            health.Untargetable = true;
            ApplySprite(openedSprite != null ? openedSprite : intactSprite);

            if (!isChest)
            {
                DropLoot();
                return;
            }

            AchievementProgress.RecordChest();
            if (ChestPanel.Instance != null)
            {
                ChestPanel.Instance.Open(minCoins, maxCoins, coinValue);
                return;
            }

            DropLoot();
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            if (use != UseMode.Smash || isChest || opened)
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
            if (use != UseMode.Smash || isChest || opened)
            {
                return;
            }

            opened = true;
            SoundEffects.Play(breakSound == BreakSound.Box ? SoundId.BoxBreak : SoundId.PotBreak);
            health.Untargetable = true;
            flash?.Flash();
            ApplySprite(openedSprite != null ? openedSprite : crackedSprite);
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
            GameManager.Instance?.RegisterPocket();
            GameManager.Instance?.AddCoins(Mathf.Max(1, Mathf.RoundToInt(amount * multiplier)));

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
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
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            Vector2 origin = transform.position;
            float luck = ResolveLuck();

            if (breakEffectPrefab != null)
            {
                GameObject effect = pool.Spawn(breakEffectPrefab, origin, Quaternion.identity);
                if (effect != null && effect.GetComponent<AutoDespawn>() == null)
                {
                    pool.ReturnDelayed(effect, 0.6f);
                }
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
                return;
            }

            if (Random.value < Mathf.Clamp01(powerUpDropChance * luck))
            {
                PowerUpCatalog.SpawnFromProp(origin);
            }
        }

        private static PlayerStats cachedLuck;

        private static float ResolveLuck()
        {
            if (cachedLuck == null)
            {
                PlayerController hero = PlayerController.Instance;
                if (hero != null)
                {
                    cachedLuck = hero.GetComponent<PlayerStats>();
                }
            }

            return cachedLuck != null ? Mathf.Max(0.1f, cachedLuck.Luck) : 1f;
        }

        private Pickup SpawnPickup(ObjectPoolManager pool, GameObject prefab, Vector2 origin)
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
