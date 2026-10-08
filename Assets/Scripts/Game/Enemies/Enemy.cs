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
        private OffscreenDespawner despawner;
        private bool dying;

        public EnemyDefinition Definition => definition;

        public Health Health => health;

        public Vector2 Position => transform.position;

        public bool IsAlive => !dying && health != null && health.IsAlive;

        public bool BelongsToBossFight =>
            definition != null && (definition.isBoss || definition.bossMinion);

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
            despawner = GetComponent<OffscreenDespawner>();
        }

        private void OnEnable()
        {
            Active.Add(this);
            health.Died += OnDied;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            health.Died -= OnDied;
        }

        public void OnSpawned()
        {
            dying = false;
        }

        public void OnDespawned()
        {
            dying = true;
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

            slimeKing?.Configure(def, runMinutes);
        }

        public void SetSpawnHeading(Vector2 spawnPosition, SpawnPattern pattern)
        {
            movement?.SetSpawnHeading(spawnPosition, pattern);
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
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
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

            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
