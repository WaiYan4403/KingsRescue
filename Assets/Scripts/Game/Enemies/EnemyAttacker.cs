using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Aimed shots for archers and spiders, and meteor casts for mages.
    /// The falling fireball itself is a pooled <see cref="MeteorStrike"/>.
    /// </summary>
    public class EnemyAttacker : MonoBehaviour, IPoolable
    {
        private EnemyDefinition definition;
        private float damage;
        private float runMinutes;
        private float nextShot;

        public void OnSpawned()
        {
            nextShot = Time.time + 0.4f;
        }

        public void OnDespawned()
        {
            definition = null;
        }

        public void Configure(EnemyDefinition def, float minutes)
        {
            definition = def;
            runMinutes = minutes;
            damage = def != null ? def.ScaledAttackDamage(minutes) : 0f;
            nextShot = Time.time + (def != null ? Mathf.Max(0.2f, def.attackInterval * 0.4f) : 0.5f);
            enabled = def != null && IsCaster(def);
        }

        private static bool IsCaster(EnemyDefinition def)
        {
            if (def.attackStyle == EnemyAttackStyle.Ranged || def.attackStyle == EnemyAttackStyle.Web)
            {
                return true;
            }

            return def.attackStyle == EnemyAttackStyle.Meteor
                && (def.meteorDefinition != null || def.projectilePrefab != null);
        }

        private void Update()
        {
            if (definition == null)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (Time.time < nextShot)
            {
                return;
            }

            Transform player = PlayerController.PlayerTransform;
            if (player == null)
            {
                return;
            }

            Vector2 origin = transform.position;
            Vector2 toPlayer = (Vector2)player.position - origin;
            float range = definition.attackRange;
            if (toPlayer.sqrMagnitude > range * range || toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null && !bounds.IsOnScreen(origin, 0.5f))
            {
                return;
            }

            bool cast = definition.attackStyle == EnemyAttackStyle.Meteor
                ? CastMeteor(player.position)
                : FireBolt(origin, toPlayer.normalized);

            if (cast)
            {
                nextShot = Time.time + Mathf.Max(0.25f, definition.attackInterval);
            }
        }

        private bool FireBolt(Vector2 origin, Vector2 direction)
        {
            if (definition.projectilePrefab == null)
            {
                return false;
            }

            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return false;
            }

            HostileProjectile shot = pool.Spawn(definition.projectilePrefab, origin, Quaternion.identity)
                ?.GetComponent<HostileProjectile>();
            if (shot == null)
            {
                Debug.LogError($"Enemy '{definition.displayName}' projectile is missing HostileProjectile.", definition);
                return false;
            }

            bool web = definition.attackStyle == EnemyAttackStyle.Web;
            float range = definition.projectileMaxDistance > 0.01f ? definition.projectileMaxDistance : 6.5f;
            shot.Launch(
                direction,
                definition.projectileSpeed,
                damage,
                definition.projectileLifetime,
                definition.projectileRadius,
                web ? definition.slowFactor : 1f,
                web ? definition.slowDuration : 0f,
                definition.projectileHoming,
                gameObject,
                range);
            return true;
        }

        private bool CastMeteor(Vector2 playerPosition)
        {
            EnemyDefinition meteorDef = definition.meteorDefinition;
            GameObject prefab = meteorDef != null ? meteorDef.prefab : definition.projectilePrefab;
            if (prefab == null)
            {
                Debug.LogError($"Enemy '{definition.displayName}' has no meteor prefab to cast.", definition);
                return false;
            }

            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return false;
            }

            Vector2 impact = PickImpactNearPlayer(playerPosition);
            GameObject instance = pool.Spawn(prefab, impact, Quaternion.identity);
            if (instance == null)
            {
                return false;
            }

            Enemy summoned = instance.GetComponent<Enemy>();
            if (summoned != null)
            {
                summoned.Configure(meteorDef != null ? meteorDef : definition, runMinutes);
            }

            MeteorStrike strike = instance.GetComponent<MeteorStrike>();
            if (strike == null)
            {
                Debug.LogError($"Enemy '{definition.displayName}' meteor prefab is missing MeteorStrike.", prefab);
                return true;
            }

            strike.Retarget(impact);
            return true;
        }

        private static Vector2 PickImpactNearPlayer(Vector2 playerPosition)
        {
            Vector2 scatter = Random.insideUnitCircle * 1.35f;
            Vector2 impact = playerPosition + scatter;
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null)
            {
                impact = bounds.ClampToPlayArea(impact);
            }

            return impact;
        }
    }
}
