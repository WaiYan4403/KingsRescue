using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Enemies;
using KingsRescue.Player;

namespace KingsRescue.Combat
{
    /// <summary>
    /// One owned weapon, living on a child of the hero. Ticks its own cooldown,
    /// resolves authored level data against the player's stats, and fires itself.
    /// The player never presses an attack button.
    /// </summary>
    public class WeaponRuntime : MonoBehaviour
    {
        private const float TargetSearchRange = 14f;

        private readonly List<OrbitBlade> blades = new List<OrbitBlade>(8);
        private readonly List<Enemy> strikeTargets = new List<Enemy>(16);

        private WeaponDefinition definition;
        private PlayerStats stats;
        private PlayerController owner;
        private int level = 1;
        private float nextFireTime;
        private float orbitAngle;
        private int activeOrbitCount = -1;
        private Coroutine comboRoutine;
        private Vector2 lastFacing = Vector2.up;

        public WeaponDefinition Definition => definition;

        public int Level => level;

        public bool IsMaxLevel => definition != null && level >= definition.MaxLevel;

        public void Initialize(WeaponDefinition weapon, PlayerStats playerStats, PlayerController playerController, int startingLevel = 1)
        {
            definition = weapon;
            stats = playerStats;
            owner = playerController;
            level = Mathf.Clamp(startingLevel, 1, weapon != null ? weapon.MaxLevel : 1);
            nextFireTime = Time.time + 0.25f;
            activeOrbitCount = -1;
        }

        public void LevelUp()
        {
            if (definition == null || IsMaxLevel)
            {
                return;
            }

            level++;
            activeOrbitCount = -1;
        }

        private void OnDisable()
        {
            ClearBlades();

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
                comboRoutine = null;
            }
        }

        private void Update()
        {
            if (definition == null || stats == null)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (definition.fireMode == WeaponFireMode.Orbit)
            {
                UpdateOrbit();
                return;
            }

            if (Time.time < nextFireTime || comboRoutine != null)
            {
                return;
            }

            nextFireTime = Time.time + ResolveCooldown();
            Fire();
        }

        // --- Stat resolution -------------------------------------------------

        private WeaponLevel Data => definition.GetLevel(level);

        private float ResolveCooldown()
        {
            return Mathf.Max(0.05f, Data.cooldown / Mathf.Max(0.1f, stats.AttackSpeed));
        }

        private float ResolveDamage()
        {
            return Data.damage * stats.DamageMultiplier;
        }

        private float ResolveArea()
        {
            return Data.area * stats.AreaMultiplier;
        }

        private float ResolveDuration()
        {
            return Data.lifetime * Mathf.Max(0.1f, stats.DurationMultiplier);
        }

        private int ResolveCount()
        {
            int bonus = definition.usesGlobalProjectileCount ? stats.BonusProjectiles : 0;
            return Mathf.Max(1, Data.count + bonus);
        }

        // --- Firing ----------------------------------------------------------

        private void Fire()
        {
            switch (definition.fireMode)
            {
                case WeaponFireMode.MeleeSwing:
                    FireMelee();
                    break;
                case WeaponFireMode.ForwardShot:
                    FireForwardShot();
                    break;
                case WeaponFireMode.AimedShot:
                    FireDirectedShot(false);
                    break;
                case WeaponFireMode.RandomShot:
                    FireDirectedShot(true);
                    break;
                case WeaponFireMode.RandomStrike:
                    FireRandomStrikes();
                    break;
                case WeaponFireMode.GroundZone:
                    FireGroundZones();
                    break;
                case WeaponFireMode.Aura:
                    FireAura();
                    break;
            }
        }

        private void FireMelee()
        {
            int swings = ResolveMeleeSwings();

            if (swings <= 1)
            {
                DoSwing(0, swings);
                return;
            }

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }

            comboRoutine = StartCoroutine(ComboRoutine(swings));
        }

        private IEnumerator ComboRoutine(int swings)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.02f, definition.comboDelay));

            for (int i = 0; i < swings; i++)
            {
                DoSwing(i, swings);
                yield return wait;
            }

            comboRoutine = null;
        }

        private void DoSwing(int swingIndex, int swingCount)
        {
            WeaponLevel data = Data;
            Vector2 center = transform.position;
            Vector2 facing = ResolveFacing(center);

            // Alternate the wedge slightly left and right so a combo reads as a flurry.
            if (swingIndex > 0)
            {
                float tilt = (swingIndex % 2 == 0 ? 1f : -1f) * data.arcDegrees * 0.25f;
                facing = Rotate(facing, tilt);
            }

            PlayCharacterAttack(swingIndex, swingCount);

            float radius = ResolveArea();
            Targeting.DamageArc(center, facing, radius, data.arcDegrees, ResolveDamage(), data.knockback, gameObject);
            float duration = ResolveDuration();
            HitboxDisplay.Flash(center, radius, duration);

            if (!definition.usesCharacterAnimators)
            {
                SpawnEffect(center, facing, radius, duration);
            }
        }

        private int ResolveMeleeSwings()
        {
            if (definition.usesCharacterAnimators)
            {
                int unlocked = definition.GetUnlockedAttackStages(level);
                if (unlocked > 0)
                {
                    int bonus = definition.usesGlobalProjectileCount ? stats.BonusProjectiles : 0;
                    return Mathf.Max(1, unlocked + bonus);
                }
            }

            return ResolveCount();
        }

        private void PlayCharacterAttack(int swingIndex, int swingCount)
        {
            if (!definition.usesCharacterAnimators || owner == null)
            {
                return;
            }

            int unlocked = definition.GetUnlockedAttackStages(level);
            if (unlocked <= 0)
            {
                return;
            }

            int stage = swingCount <= 1
                ? unlocked
                : Mathf.Clamp(swingIndex + 1, 1, unlocked);
            owner.PlayAttackStage(definition.GetAttackTrigger(stage), definition.attackTriggers);
        }

        private void FireForwardShot()
        {
            int count = ResolveCount();
            if (count <= 1 || definition.fanProjectiles)
            {
                for (int i = 0; i < count; i++)
                {
                    SpawnForwardShot(i, count);
                }

                return;
            }

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }

            comboRoutine = StartCoroutine(StraightVolley(count));
        }

        private IEnumerator StraightVolley(int count)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.02f, definition.comboDelay));

            for (int i = 0; i < count; i++)
            {
                SpawnForwardShot(i, count);
                if (i < count - 1)
                {
                    yield return wait;
                }
            }

            comboRoutine = null;
        }

        private void SpawnForwardShot(int shotIndex, int shotCount)
        {
            if (definition.projectilePrefab == null)
            {
                Debug.LogError($"Weapon '{definition.name}' is ForwardShot but has no projectile prefab.", definition);
                enabled = false;
                return;
            }

            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            WeaponLevel data = Data;
            Vector2 origin = transform.position;
            Vector2 baseDirection = Vector2.up;
            float speed = data.projectileSpeed * stats.ProjectileSpeedMultiplier;
            float damage = ResolveDamage();
            int pierce = data.pierce + stats.BonusPierce;
            float area = ResolveArea();

            float offset = 0f;
            if (definition.fanProjectiles && shotCount > 1)
            {
                offset = Mathf.Lerp(-data.spreadDegrees * 0.5f, data.spreadDegrees * 0.5f, shotIndex / (float)(shotCount - 1));
            }

            Vector2 direction = Rotate(baseDirection, offset);
            Projectile projectile = pool.Spawn(definition.projectilePrefab, origin, Quaternion.identity)
                .GetComponent<Projectile>();

            if (projectile == null)
            {
                Debug.LogError($"Projectile prefab '{definition.projectilePrefab.name}' has no Projectile component.", definition);
                return;
            }

            float range = data.maxDistance > 0.01f ? data.maxDistance : 6.5f;
            projectile.Launch(direction, speed, damage, pierce, ResolveDuration(), area, data.knockback, gameObject, range);
        }

        private void FireDirectedShot(bool randomTargets)
        {
            int count = ResolveCount();
            Vector2 origin = transform.position;

            if (count <= 1 || definition.fanProjectiles || randomTargets)
            {
                for (int i = 0; i < count; i++)
                {
                    Vector2 direction = randomTargets
                        ? ResolveRandomDirection(origin)
                        : ResolveFacing(origin);
                    SpawnDirectedShot(origin, direction, i, count);
                }

                return;
            }

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }

            comboRoutine = StartCoroutine(DirectedVolley(count));
        }

        private IEnumerator DirectedVolley(int count)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.02f, definition.comboDelay));
            Vector2 origin = transform.position;
            Vector2 direction = ResolveFacing(origin);

            for (int i = 0; i < count; i++)
            {
                origin = transform.position;
                SpawnDirectedShot(origin, direction, i, count);
                if (i < count - 1)
                {
                    yield return wait;
                }
            }

            comboRoutine = null;
        }

        private void SpawnDirectedShot(Vector2 origin, Vector2 baseDirection, int shotIndex, int shotCount)
        {
            if (definition.projectilePrefab == null)
            {
                Debug.LogError($"Weapon '{definition.name}' needs a projectile prefab.", definition);
                enabled = false;
                return;
            }

            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            WeaponLevel data = Data;
            Vector2 aim = baseDirection.sqrMagnitude > 0.0001f ? baseDirection.normalized : Vector2.up;
            float speed = data.projectileSpeed * stats.ProjectileSpeedMultiplier;
            float damage = ResolveDamage();
            int pierce = data.pierce + stats.BonusPierce;
            float area = ResolveArea();

            float offset = 0f;
            if (definition.fanProjectiles && shotCount > 1)
            {
                offset = Mathf.Lerp(-data.spreadDegrees * 0.5f, data.spreadDegrees * 0.5f, shotIndex / (float)(shotCount - 1));
            }

            Vector2 direction = Rotate(aim, offset);
            Projectile projectile = pool.Spawn(definition.projectilePrefab, origin, Quaternion.identity)
                .GetComponent<Projectile>();

            if (projectile == null)
            {
                Debug.LogError($"Projectile prefab '{definition.projectilePrefab.name}' has no Projectile component.", definition);
                return;
            }

            float range = data.maxDistance > 0.01f ? data.maxDistance : 6.5f;
            projectile.Launch(direction, speed, damage, pierce, ResolveDuration(), area, data.knockback, gameObject, range);
        }

        private Vector2 ResolveRandomDirection(Vector2 origin)
        {
            Targeting.CollectLiving(origin, TargetSearchRange, strikeTargets);
            if (strikeTargets.Count == 0)
            {
                return ResolveFacing(origin);
            }

            Enemy enemy = strikeTargets[Random.Range(0, strikeTargets.Count)];
            Vector2 toTarget = (Vector2)enemy.transform.position - origin;
            if (toTarget.sqrMagnitude > 0.0001f)
            {
                lastFacing = toTarget.normalized;
                return lastFacing;
            }

            return ResolveFacing(origin);
        }

        private void FireRandomStrikes()
        {
            Vector2 origin = transform.position;
            int count = ResolveCount();
            float radius = ResolveArea();
            float damage = ResolveDamage();
            float duration = ResolveDuration();
            float knockback = Data.knockback;

            Targeting.CollectLiving(origin, TargetSearchRange, strikeTargets);

            for (int i = 0; i < count; i++)
            {
                Vector2 point;
                if (strikeTargets.Count > 0)
                {
                    int pick = Random.Range(0, strikeTargets.Count);
                    point = strikeTargets[pick].transform.position;
                    if (strikeTargets.Count > 1)
                    {
                        strikeTargets.RemoveAt(pick);
                    }
                }
                else
                {
                    point = origin + ResolveFacing(origin) * Mathf.Max(1.2f, radius);
                }

                Targeting.DamageCircle(point, radius, damage, knockback, gameObject);
                HitboxDisplay.Flash(point, radius, duration);
                SpawnEffect(point, Vector2.up, radius, duration);
            }
        }

        private void FireGroundZones()
        {
            Vector2 origin = transform.position;
            int count = ResolveCount();
            float spread = ResolveArea() * 1.7f;
            float puddle = Mathf.Max(0.7f, ResolveArea() * 0.55f);
            float damage = ResolveDamage();
            float duration = ResolveDuration();
            float knockback = Data.knockback;

            for (int i = 0; i < count; i++)
            {
                Vector2 point = origin + Random.insideUnitCircle * spread;
                Targeting.DamageCircle(point, puddle, damage, knockback, gameObject);
                HitboxDisplay.Flash(point, puddle, duration);
                SpawnEffect(point, Vector2.up, puddle, duration);
            }
        }

        private void FireAura()
        {
            WeaponLevel data = Data;
            Vector2 center = transform.position;
            float radius = ResolveArea();

            Targeting.DamageCircle(center, radius, ResolveDamage(), data.knockback, gameObject);
            SpawnEffect(center, Vector2.up, radius, ResolveDuration());
        }

        // --- Orbit -----------------------------------------------------------

        private void UpdateOrbit()
        {
            WeaponLevel data = Data;
            int count = ResolveCount();

            if (count != activeOrbitCount)
            {
                RebuildBlades(count);
            }

            if (blades.Count == 0)
            {
                return;
            }

            float radius = ResolveArea();
            float damage = ResolveDamage();
            orbitAngle += data.orbitSpeed * 360f * Time.deltaTime;

            Vector2 center = transform.position;

            for (int i = 0; i < blades.Count; i++)
            {
                OrbitBlade blade = blades[i];
                if (blade == null)
                {
                    continue;
                }

                float angle = (orbitAngle + i * (360f / blades.Count)) * Mathf.Deg2Rad;
                blade.transform.position = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                blade.Configure(damage, 1f, data.knockback, data.rehitInterval, gameObject);
            }
        }

        private void RebuildBlades(int count)
        {
            ClearBlades();

            if (definition.orbitPrefab == null)
            {
                Debug.LogError($"Weapon '{definition.name}' is Orbit but has no orbit prefab.", definition);
                enabled = false;
                return;
            }

            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            for (int i = 0; i < count; i++)
            {
                OrbitBlade blade = pool.Spawn(definition.orbitPrefab, transform.position, Quaternion.identity, transform)
                    .GetComponent<OrbitBlade>();

                if (blade == null)
                {
                    Debug.LogError($"Orbit prefab '{definition.orbitPrefab.name}' has no OrbitBlade component.", definition);
                    return;
                }

                blades.Add(blade);
            }

            activeOrbitCount = count;
        }

        private void ClearBlades()
        {
            PoolManager pool = PoolManager.Instance;

            for (int i = 0; i < blades.Count; i++)
            {
                if (blades[i] == null)
                {
                    continue;
                }

                if (pool != null)
                {
                    pool.Despawn(blades[i].gameObject);
                }
                else
                {
                    Destroy(blades[i].gameObject);
                }
            }

            blades.Clear();
            activeOrbitCount = -1;
        }

        // --- Helpers ---------------------------------------------------------

        private Vector2 ResolveFacing(Vector2 center)
        {
            Enemy target = Targeting.FindNearest(center, TargetSearchRange);
            if (target != null)
            {
                Vector2 toTarget = (Vector2)target.transform.position - center;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    lastFacing = toTarget.normalized;
                    return lastFacing;
                }
            }

            if (owner != null && owner.Velocity.sqrMagnitude > 0.04f)
            {
                lastFacing = owner.Velocity.normalized;
            }

            return lastFacing;
        }

        private void SpawnEffect(Vector2 center, Vector2 facing, float radius, float lifetime)
        {
            if (definition.effectPrefab == null || PoolManager.Instance == null)
            {
                return;
            }

            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
            GameObject effect = PoolManager.Instance.Spawn(definition.effectPrefab, center, Quaternion.Euler(0f, 0f, angle));

            if (effect == null)
            {
                return;
            }

            effect.transform.localScale = definition.effectPrefab.transform.localScale * radius;

            AutoDespawn despawn = effect.GetComponent<AutoDespawn>();
            if (despawn != null)
            {
                despawn.SetLifetime(Mathf.Max(0.05f, lifetime));
            }
        }

        private static Vector2 Rotate(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float cos = Mathf.Cos(rad);
            float sin = Mathf.Sin(rad);
            return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
        }
    }
}
