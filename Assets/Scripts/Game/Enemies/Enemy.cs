using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Ties an <see cref="EnemyDefinition"/> to a pooled prefab and keeps the global
    /// active list that weapon targeting reads instead of scanning the scene.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Enemy : MonoBehaviour, IPoolable
    {
        /// <summary>Every enemy currently alive and on the field.</summary>
        public static readonly List<Enemy> Active = new List<Enemy>(256);

        [SerializeField] private EnemyDefinition definition;

        private Health health;
        private EnemyMovement movement;
        private ContactDamage contact;
        private EnemyDropper dropper;
        private EnemyAttacker attacker;
        private MeteorStrike meteor;
        private EnemyDeathBurst deathBurst;
        private SlimeKingBoss slimeKing;
        private EncounterBoss encounterBoss;
        private EnemySpecials specials;
        private HitFlash hitFlash;
        private SpriteRenderer body;
        private Animator animator;
        private OffscreenDespawner despawner;
        private Vector3 baseScale = Vector3.one;
        private bool dying;

        public EnemyDefinition Definition => definition;

        public Health Health => health;

        public Vector2 Position => transform.position;

        public bool IsAlive => !dying && health != null && health.IsAlive;

        public bool BelongsToBossFight =>
            definition != null && (definition.isBoss || definition.bossMinion);

        public string DisplayName =>
            definition != null && !string.IsNullOrEmpty(definition.displayName)
                ? definition.displayName
                : "Enemy";

        public bool IsBoss => definition != null && definition.isBoss;

        /// <summary>Short live description for the field log: chasing, shooting, slamming, etc.</summary>
        public string DescribeActivity()
        {
            if (slimeKing != null && slimeKing.enabled)
            {
                return slimeKing.StatusLine;
            }

            if (encounterBoss != null && encounterBoss.enabled)
            {
                return encounterBoss.StatusLine;
            }

            if (meteor != null && meteor.enabled)
            {
                return "meteor strike";
            }

            string special = specials != null && specials.enabled ? specials.StatusLine : null;
            if (!string.IsNullOrEmpty(special))
            {
                return special;
            }

            string shot = attacker != null && attacker.enabled ? attacker.StatusLine : null;
            if (!string.IsNullOrEmpty(shot))
            {
                return shot;
            }

            return movement != null ? movement.StatusLine : "idle";
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            movement = GetComponent<EnemyMovement>();
            contact = GetComponent<ContactDamage>();
            dropper = GetComponent<EnemyDropper>();
            attacker = GetComponent<EnemyAttacker>();
            meteor = GetComponent<MeteorStrike>();
            deathBurst = GetComponent<EnemyDeathBurst>();
            slimeKing = GetComponent<SlimeKingBoss>();
            encounterBoss = GetComponent<EncounterBoss>();
            specials = GetComponent<EnemySpecials>();
            hitFlash = GetComponent<HitFlash>();
            body = GetComponent<SpriteRenderer>();
            animator = GetComponent<Animator>();
            despawner = GetComponent<OffscreenDespawner>();
            baseScale = transform.localScale;

            if (animator != null)
            {
                // Pooling disables the body. Without this, every reuse rebuilds the animator.
                animator.keepAnimatorStateOnDisable = true;
                animator.cullingMode = AnimatorCullingMode.CullCompletely;
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
            health.Died += OnDied;
            health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            health.Died -= OnDied;
            health.Damaged -= OnDamaged;
        }

        public void OnSpawned()
        {
            dying = false;
            OffsetWalkCycle();
        }

        public void OnDespawned()
        {
            dying = true;
            for (int i = transform.childCount - 1; i >= 0; i--)
            {
                FreezeZone crystal = transform.GetChild(i).GetComponent<FreezeZone>();
                if (crystal != null && ObjectPoolManager.Instance != null)
                {
                    ObjectPoolManager.Instance.Return(crystal.gameObject);
                }
            }
        }

        /// <summary>
        /// Applies the definition and the current difficulty ramp. Called by the spawner
        /// immediately after the instance comes out of the pool.
        /// </summary>
        public void Configure(EnemyDefinition def, float runMinutes)
        {
            definition = def;
            dying = false;

            if (def == null)
            {
                return;
            }

            health.Configure(def.ScaledHealth(runMinutes));
            movement?.Configure(def);
            if (contact != null)
            {
                contact.enabled = def.dealsContactDamage;
                if (def.dealsContactDamage)
                {
                    contact.Configure(def, runMinutes);
                }
            }

            dropper?.Configure(def);
            attacker?.Configure(def, runMinutes);
            meteor?.Configure(def, runMinutes);
            deathBurst?.Configure(def, runMinutes);
            if (despawner != null)
            {
                despawner.enabled = !def.isBoss;
            }

            if (animator != null)
            {
                animator.cullingMode = def.isBoss
                    ? AnimatorCullingMode.AlwaysAnimate
                    : AnimatorCullingMode.CullCompletely;
            }

            slimeKing?.Configure(def, runMinutes);
            encounterBoss?.Configure(def, runMinutes);
            ApplyVisual(def);
            specials?.Configure(def, runMinutes);
        }

        public static Enemy SpawnConfigured(EnemyDefinition def, Vector2 position, float runMinutes)
        {
            if (def == null || def.prefab == null)
            {
                return null;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return null;
            }

            GameObject instance = pool.Spawn(def.prefab, position, Quaternion.identity);
            if (instance == null)
            {
                return null;
            }

            Enemy enemy = instance.GetComponent<Enemy>();
            if (enemy == null)
            {
                return null;
            }

            enemy.Configure(def, runMinutes);
            return enemy;
        }

        /// <summary>
        /// Walk cycles that start together swap sprites on the same frames and hitch.
        /// A random phase spreads those swaps out.
        /// </summary>
        private void OffsetWalkCycle()
        {
            if (animator == null || animator.runtimeAnimatorController == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            animator.Play(state.fullPathHash, 0, Random.value);
        }

        private void ApplyVisual(EnemyDefinition def)
        {
            float scale = def.bodyScale > 0.05f ? def.bodyScale : 1f;
            transform.localScale = new Vector3(baseScale.x * scale, baseScale.y * scale, baseScale.z);

            Color tint = def.bodyTint.a > 0.05f ? def.bodyTint : Color.white;
            if (body != null)
            {
                body.color = tint;
            }

            hitFlash?.SetBaseColor(tint);
            specials?.ApplyVisual(tint);
        }

        public void SetSpawnHeading(Vector2 spawnPosition, SpawnPattern pattern)
        {
            movement?.SetSpawnHeading(spawnPosition, pattern);
        }

        public void SetLockedHeading(Vector2 heading)
        {
            movement?.SetLockedHeading(heading);
        }

        /// <summary>
        /// Recycles every enemy that is not the boss (or a tagged boss add) without
        /// granting kills, XP, or coins. Used when the arena takes over the road.
        /// </summary>
        public static void DespawnNonEncounter()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                Enemy enemy = Active[i];
                if (enemy == null || enemy.BelongsToBossFight)
                {
                    continue;
                }

                enemy.RecycleQuiet();
            }
        }

        private void RecycleQuiet()
        {
            dying = true;
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            Vector2 at = source != null ? source.BodyCenter : (Vector2)transform.position;
            DamageNumberDisplay.Popup(at, info.Amount, info.Critical);

            if (movement == null || info.Knockback <= 0.01f)
            {
                return;
            }

            float taken = definition != null ? definition.KnockbackTaken : 1f;
            float force = info.Knockback * taken;
            if (force <= 0.01f)
            {
                return;
            }

            movement.ApplyKnockback(info.Direction, force);
        }

        private void OnDied(Health source, DamageInfo info)
        {
            if (dying)
            {
                return;
            }

            dying = true;

            deathBurst?.Trigger();
            GameManager.Instance?.RegisterKill();
            AchievementProgress.RecordKill(definition);
            dropper?.DropRewards();
            slimeKing?.SpawnDeathSplit();

            if (definition != null && definition.isBoss)
            {
                GameManager.Instance?.EndBossFight();
            }

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
