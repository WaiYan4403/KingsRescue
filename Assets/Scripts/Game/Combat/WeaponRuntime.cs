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
        private const float DefaultShotRange = 6.5f;
        private const float UnlimitedShotLifetime = 12f;

        // Lancer effect clips are 0.35s. The slash sprite is empty at frame 0,
        // the strike frame is 0.167s, and the sprite clears at 0.333s.
        private const float CharacterSlashHitTime = 0.167f;
        private const float CharacterSlashClearTime = 0.333f;

        private readonly List<OrbitBlade> blades = new List<OrbitBlade>(8);
        private readonly List<Enemy> strikeTargets = new List<Enemy>(16);

        private WeaponDefinition definition;
        private PlayerStats stats;
        private PlayerController owner;
        private PowerUpController powerUp;
        private int level = 1;
        private float nextFireTime;
        private float orbitAngle;
        private float orbitUntil;
        private int activeOrbitCount = -1;
        private Coroutine comboRoutine;
        private Vector2 lastFacing = Vector2.up;
        private Vector2 lastMoveFacing = Vector2.up;

        public WeaponDefinition Definition => definition;

        public int Level => level;

        public bool IsMaxLevel => definition != null && level >= definition.MaxLevel;

        public void Initialize(WeaponDefinition weapon, PlayerStats playerStats, PlayerController playerController, int startingLevel = 1)
        {
            definition = weapon;
            stats = playerStats;
            owner = playerController;
            powerUp = playerController != null ? playerController.GetComponent<PowerUpController>() : null;
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

        public void SetLevel(int weaponLevel)
        {
            if (definition == null)
            {
                return;
            }

            int next = Mathf.Clamp(weaponLevel, 1, definition.MaxLevel);
            if (next == level)
            {
                return;
            }

            level = next;
            activeOrbitCount = -1;
        }

        private void OnDisable()
        {
            ClearBlades();
            StopMusicField();

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

            if (definition.fireMode == WeaponFireMode.Aura)
            {
                UpdateMusicField();
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

        /// <summary>0 means unlimited travel; otherwise a hard world-unit cap.</summary>
        private float ResolveShotRange()
        {
            float authored = Data.maxDistance;
            if (authored < 0f)
            {
                return 0f;
            }

            return authored > 0.01f ? authored : DefaultShotRange;
        }

        private float ResolveShotLifetime(float range)
        {
            if (range <= 0f)
            {
                return UnlimitedShotLifetime;
            }

            return ResolveDuration();
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
                case WeaponFireMode.FreezeField:
                    FireFreezeFields();
                    break;
                case WeaponFireMode.BoltStrike:
                    FireBoltStrikes();
                    break;
                case WeaponFireMode.Boomerang:
                    FireBoomerangs();
                    break;
            }
        }

        private void FireMelee()
        {
            int swings = ResolveMeleeSwings();
            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }

            if (definition.usesCharacterAnimators)
            {
                comboRoutine = StartCoroutine(SyncedCombo(swings));
                return;
            }

            if (swings <= 1)
            {
                DoSwing(0, swings);
                return;
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

        private IEnumerator SyncedCombo(int swings)
        {
            float gap = Mathf.Max(CharacterSlashClearTime, definition.comboDelay);
            for (int i = 0; i < swings; i++)
            {
                PlayCharacterAttack(i, swings);
                yield return new WaitForSeconds(CharacterSlashHitTime);
                if (!isActiveAndEnabled || definition == null)
                {
                    comboRoutine = null;
                    yield break;
                }

                ApplySwingHit(i, swings);
                float rest = gap - CharacterSlashHitTime;
                if (i < swings - 1 && rest > 0.01f)
                {
                    yield return new WaitForSeconds(rest);
                }
            }

            comboRoutine = null;
        }

        private void DoSwing(int swingIndex, int swingCount)
        {
            PlayCharacterAttack(swingIndex, swingCount);
            ApplySwingHit(swingIndex, swingCount);
        }

        private void ApplySwingHit(int swingIndex, int swingCount)
        {
            WeaponLevel data = Data;
            Vector2 center = transform.position;
            int unlocked = definition.GetUnlockedAttackStages(level);
            int stage = unlocked > 0 ? Mathf.Clamp(swingIndex + 1, 1, unlocked) : 0;
            float radius = ResolveArea();
            float duration = definition.usesCharacterAnimators
                ? CharacterSlashClearTime - CharacterSlashHitTime
                : ResolveDuration();

            if (definition.usesCharacterAnimators && TrySlashHit(center, radius, out Vector2 slashCenter, out float slashRadius))
            {
                Targeting.DamageCircle(slashCenter, slashRadius, ResolveDamage(), data.knockback, gameObject);
                HitboxDisplay.Flash(slashCenter, slashRadius, duration);
                TrySpawnPowerSlash(stage);
                return;
            }

            if (IsFinishingStrike(swingIndex))
            {
                Vector2 forward = Vector2.up;
                float length = radius;
                float thickness = Mathf.Max(0.85f, radius * 0.4f);
                Targeting.DamageSegment(center, center + forward * length, thickness * 0.5f, ResolveDamage(), data.knockback, gameObject);
                HitboxDisplay.FlashCapsule(center, forward, length, thickness, duration);
                TrySpawnPowerSlash(stage);
                return;
            }

            Vector2 facing = ResolveFacing(center);

            if (swingIndex > 0)
            {
                float tilt = (swingIndex % 2 == 0 ? 1f : -1f) * data.arcDegrees * 0.25f;
                facing = Rotate(facing, tilt);
            }

            Targeting.DamageArc(center, facing, radius, data.arcDegrees, ResolveDamage(), data.knockback, gameObject);
            HitboxDisplay.Flash(center, radius, duration);
            TrySpawnPowerSlash(stage);

            if (!definition.usesCharacterAnimators)
            {
                SpawnEffect(center, facing, radius, duration);
            }
        }

        /// <summary>
        /// Character swings are centered on the slash sprite. Area 2 matches that sprite.
        /// Every other Area value scales the circle, including values below 2.
        /// </summary>
        private bool TrySlashHit(Vector2 origin, float area, out Vector2 center, out float radius)
        {
            float scale = Mathf.Max(0.05f, area) / 2f;
            center = origin + Vector2.up * 1.2f;
            radius = 2.5f * scale;
            if (owner == null || !owner.TryGetSlashBounds(out Bounds bounds))
            {
                return true;
            }

            center = bounds.center;
            float spriteReach = Mathf.Max(bounds.extents.x, bounds.extents.y);
            radius = spriteReach * scale;
            return radius > 0.05f;
        }

        private bool IsFinishingStrike(int swingIndex)
        {
            if (definition == null || !definition.usesCharacterAnimators || definition.attackTriggers == null)
            {
                return false;
            }

            int stages = definition.attackTriggers.Length;
            int unlocked = definition.GetUnlockedAttackStages(level);
            if (stages < 2 || unlocked < stages)
            {
                return false;
            }

            int stage = Mathf.Clamp(swingIndex + 1, 1, unlocked);
            return stage >= unlocked;
        }

        private int ResolveMeleeSwings()
        {
            if (definition.usesCharacterAnimators)
            {
                int unlocked = definition.GetUnlockedAttackStages(level);
                if (unlocked > 0)
                {
                    return unlocked;
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

            int stage = Mathf.Clamp(swingIndex + 1, 1, unlocked);
            string bodyTrigger = definition.GetAttackTrigger(stage);
            string effectTrigger = definition.GetEffectAttackTrigger(level, stage);
            owner.PlayAttackStage(bodyTrigger, definition.attackTriggers, effectTrigger, definition.advancedAttackTriggers);
        }

        private void TrySpawnPowerSlash(int stage)
        {
            if (stage < 1 || powerUp == null || !powerUp.AddsSlashProjectiles)
            {
                return;
            }

            int unlocked = definition.GetUnlockedAttackStages(level);
            GameObject[] prefabs = definition.powerSlashPrefabs;
            if (stage > unlocked || prefabs == null || stage > prefabs.Length)
            {
                return;
            }

            GameObject prefab = prefabs[stage - 1];
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (prefab == null || pool == null)
            {
                return;
            }

            int extra = definition.usesGlobalProjectileCount ? Mathf.Max(0, stats.BonusProjectiles) : 0;
            int total = 1 + extra;
            float scale = Mathf.Max(1f, powerUp.SlashScale);
            WeaponLevel data = Data;
            float damage = ResolveDamage() * Mathf.Max(1f, powerUp.SlashDamageMultiplier);
            float range = Mathf.Max(0.5f, powerUp.SlashRange);
            const float speed = 9f;
            float lifetime = range / speed + 0.05f;
            const float spread = 14f;
            Vector2 origin = (Vector2)transform.position + Vector2.up * 0.35f;

            for (int i = 0; i < total; i++)
            {
                float angle = total <= 1
                    ? 0f
                    : Mathf.Lerp(-spread * 0.5f, spread * 0.5f, i / (float)(total - 1));
                GameObject instance = pool.Spawn(prefab, origin, Quaternion.identity);
                if (instance == null)
                {
                    continue;
                }

                instance.transform.localScale = prefab.transform.localScale * scale;
                Projectile projectile = instance.GetComponent<Projectile>();
                if (projectile == null)
                {
                    continue;
                }

                projectile.Launch(Rotate(Vector2.up, angle), speed, damage, 2, lifetime, scale, data.knockback, gameObject, range);
            }
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

            ObjectPoolManager pool = ObjectPoolManager.Instance;
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
            PlayCharacterAttack(shotIndex, shotCount);
            Projectile projectile = pool.Spawn(definition.projectilePrefab, origin, Quaternion.identity)
                .GetComponent<Projectile>();

            if (projectile == null)
            {
                Debug.LogError($"Projectile prefab '{definition.projectilePrefab.name}' has no Projectile component.", definition);
                return;
            }

            float range = ResolveShotRange();
            projectile.Launch(direction, speed, damage, pierce, ResolveShotLifetime(range), area, data.knockback, gameObject, range);
        }

        private void FireDirectedShot(bool randomTargets)
        {
            int count = ResolveCount();
            Vector2 origin = transform.position;

            if (randomTargets)
            {
                FireAtCollectedTargets(origin, count, true);
                return;
            }

            if (definition.aimStyle == WeaponAimStyle.DistinctNearest)
            {
                FireAtCollectedTargets(origin, count, false);
                return;
            }

            Vector2 aim = definition.aimStyle == WeaponAimStyle.Movement
                ? ResolveMoveFacing()
                : ResolveFacing(origin);

            if (count <= 1 || definition.fanProjectiles)
            {
                for (int i = 0; i < count; i++)
                {
                    SpawnDirectedShot(origin, aim, i, count);
                }

                return;
            }

            if (comboRoutine != null)
            {
                StopCoroutine(comboRoutine);
            }

            comboRoutine = StartCoroutine(DirectedVolley(count));
        }

        private void FireAtCollectedTargets(Vector2 origin, int count, bool randomTargets)
        {
            Targeting.CollectLiving(origin, TargetSearchRange, strikeTargets);
            for (int i = 0; i < count; i++)
            {
                Vector2 direction;
                if (strikeTargets.Count > 0)
                {
                    int pick = randomTargets ? Random.Range(0, strikeTargets.Count) : IndexOfNearest(origin);
                    Enemy locked = strikeTargets[pick];
                    Vector2 toTarget = (Vector2)locked.transform.position - origin;
                    direction = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : ResolveMoveFacing();
                    lastFacing = direction;
                    if (strikeTargets.Count > 1)
                    {
                        strikeTargets.RemoveAt(pick);
                    }

                    SpawnDirectedShot(origin, direction, i, count, locked);
                    continue;
                }
                else
                {
                    direction = definition.aimStyle == WeaponAimStyle.Movement
                        ? ResolveMoveFacing()
                        : ResolveFacing(origin);
                }

                SpawnDirectedShot(origin, direction, i, count);
            }
        }

        private int IndexOfNearest(Vector2 origin)
        {
            int best = 0;
            float bestSqr = float.PositiveInfinity;
            for (int i = 0; i < strikeTargets.Count; i++)
            {
                float sqr = ((Vector2)strikeTargets[i].transform.position - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }

            return best;
        }

        private IEnumerator DirectedVolley(int count)
        {
            var wait = new WaitForSeconds(Mathf.Max(0.02f, definition.comboDelay));
            bool moveAim = definition.aimStyle == WeaponAimStyle.Movement;
            Vector2 direction = moveAim ? ResolveMoveFacing() : ResolveFacing(transform.position);

            for (int i = 0; i < count; i++)
            {
                Vector2 origin = transform.position;
                if (moveAim)
                {
                    direction = ResolveMoveFacing();
                }

                SpawnDirectedShot(origin, direction, i, count);
                if (i < count - 1)
                {
                    yield return wait;
                }
            }

            comboRoutine = null;
        }

        private void SpawnDirectedShot(Vector2 origin, Vector2 baseDirection, int shotIndex, int shotCount, Enemy lockedTarget = null)
        {
            if (definition.projectilePrefab == null)
            {
                Debug.LogError($"Weapon '{definition.name}' needs a projectile prefab.", definition);
                enabled = false;
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
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

            float range = ResolveShotRange();
            projectile.Launch(direction, speed, damage, pierce, ResolveShotLifetime(range), area, data.knockback, gameObject, range, definition.projectileHoming, lockedTarget);
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
                SpawnEffect(point, Vector2.up, radius, duration, true);
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
                SpawnEffect(point, Vector2.up, puddle, duration, true);
            }
        }

        private void FireFreezeFields()
        {
            if (definition.effectPrefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            int count = ResolveCount();
            float radius = Mathf.Max(0.7f, ResolveArea());
            float duration = Mathf.Max(0.4f, ResolveDuration());
            float damage = ResolveDamage();
            float reach = 4.5f + radius;
            Targeting.CollectLiving(transform.position, reach, strikeTargets);

            for (int i = strikeTargets.Count - 1; i >= 0; i--)
            {
                Enemy enemy = strikeTargets[i];
                if (enemy != null && enemy.GetComponentInChildren<FreezeZone>() != null)
                {
                    strikeTargets.RemoveAt(i);
                }
            }

            int casts = Mathf.Min(count, strikeTargets.Count);
            for (int i = 0; i < casts; i++)
            {
                int pick = Random.Range(0, strikeTargets.Count);
                SpawnFreeze(strikeTargets[pick], radius, damage, duration);
                strikeTargets.RemoveAt(pick);
            }
        }

        private void SpawnFreeze(Enemy enemy, float radius, float damage, float duration)
        {
            if (enemy == null)
            {
                return;
            }

            GameObject effect = ObjectPoolManager.Instance.Spawn(
                definition.effectPrefab,
                enemy.transform.position,
                Quaternion.identity,
                enemy.transform);
            if (effect == null)
            {
                return;
            }

            effect.transform.localPosition = Vector3.zero;
            effect.transform.localRotation = Quaternion.identity;
            FreezeZone zone = effect.GetComponent<FreezeZone>();
            if (zone != null)
            {
                zone.Attach(enemy, radius, damage, duration, gameObject);
            }
        }

        private void FireBoltStrikes()
        {
            int count = ResolveCount();
            float radius = Mathf.Max(0.4f, ResolveArea());
            float damage = ResolveDamage();
            float duration = Mathf.Max(0.2f, ResolveDuration());
            float knockback = Data.knockback;
            PlaceOnTargets(count, out int placed);

            for (int i = 0; i < placed; i++)
            {
                Vector2 point = strikeTargets[i].transform.position;
                Targeting.DamageCircle(point, radius, damage, knockback, gameObject);
                HitboxDisplay.Flash(point, radius, duration);
                SpawnBolt(point, duration);
            }
        }

        private void FireBoomerangs()
        {
            if (definition.projectilePrefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            int count = ResolveCount();
            Vector2 origin = transform.position;
            Vector2 aim = ResolveFacing(origin);
            WeaponLevel data = Data;
            float speed = data.projectileSpeed * stats.ProjectileSpeedMultiplier;
            float damage = ResolveDamage();
            int pierce = data.pierce + stats.BonusPierce;
            float area = ResolveArea();
            float range = ResolveShotRange();

            float step = count > 1 ? 360f / count : 0f;
            for (int i = 0; i < count; i++)
            {
                float offset = step * i;

                GameObject instance = ObjectPoolManager.Instance.Spawn(definition.projectilePrefab, origin, Quaternion.identity);
                if (instance == null)
                {
                    continue;
                }

                Boomerang boomerang = instance.GetComponent<Boomerang>();
                if (boomerang == null)
                {
                    ObjectPoolManager.Instance.Return(instance);
                    continue;
                }

                boomerang.Launch(Rotate(aim, offset), speed, damage, pierce, ResolveDuration(), area, data.knockback, gameObject, range);
            }
        }

        private void PlaceOnTargets(int count, out int placed)
        {
            Vector2 origin = transform.position;
            Targeting.CollectLiving(origin, TargetSearchRange, strikeTargets);
            int limit = Mathf.Min(count, strikeTargets.Count);
            for (int i = 0; i < limit; i++)
            {
                int best = i;
                float bestSqr = float.PositiveInfinity;
                for (int j = i; j < strikeTargets.Count; j++)
                {
                    float sqr = ((Vector2)strikeTargets[j].transform.position - origin).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        best = j;
                    }
                }

                Enemy swap = strikeTargets[i];
                strikeTargets[i] = strikeTargets[best];
                strikeTargets[best] = swap;
            }

            placed = limit;
        }

        private void SpawnBolt(Vector2 point, float duration)
        {
            if (definition.effectPrefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            GameObject effect = ObjectPoolManager.Instance.Spawn(definition.effectPrefab, point, Quaternion.identity);
            if (effect == null)
            {
                return;
            }

            Animator animator = effect.GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.Play(0, 0, 0f);
                animator.Update(0f);
            }

            ObjectPoolManager.Instance.ReturnDelayed(effect, duration);
        }

        private MusicField musicField;
        private GameObject activeFieldPrefab;
        private GameObject musicNotes;
        private HitboxMarker musicHitbox;
        private float musicUntil;
        private float nextMusicHit;

        private void UpdateMusicField()
        {
            if (musicField != null && definition.GetAreaEffectPrefab(level) != activeFieldPrefab)
            {
                StopMusicField();
                if (!TryStartMusicField())
                {
                    nextFireTime = Time.time + 0.5f;
                    return;
                }
            }

            if (musicField != null)
            {
                float radius = ResolveArea();
                musicField.SetRadius(radius);
                HitboxDisplay.SyncCircle(ref musicHitbox, transform.position, radius);
                if (Time.time >= nextMusicHit)
                {
                    WeaponLevel data = Data;
                    nextMusicHit = Time.time + Mathf.Max(0.05f, data.rehitInterval);
                    Targeting.DamageCircle(transform.position, radius, ResolveDamage(), data.knockback, gameObject);
                }

                if (Time.time >= musicUntil)
                {
                    StopMusicField();
                    nextFireTime = Time.time + ResolveCooldown();
                }

                return;
            }

            if (Time.time < nextFireTime)
            {
                return;
            }

            if (!TryStartMusicField())
            {
                nextFireTime = Time.time + 0.5f;
                return;
            }

            musicUntil = Time.time + Mathf.Max(0.2f, ResolveDuration());
            nextMusicHit = Time.time;
        }

        private bool TryStartMusicField()
        {
            GameObject prefab = definition.GetAreaEffectPrefab(level);
            if (prefab == null)
            {
                Debug.LogError($"Weapon '{definition.name}' is a music field but has no field prefab.", definition);
                return false;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return false;
            }

            Transform parent = owner != null ? owner.transform : transform;
            GameObject fieldObject = pool.Spawn(prefab, parent.position, Quaternion.identity, parent);
            if (fieldObject == null)
            {
                return false;
            }

            fieldObject.transform.localPosition = Vector3.zero;
            fieldObject.transform.localRotation = Quaternion.identity;
            musicField = fieldObject.GetComponent<MusicField>();
            if (musicField == null)
            {
                Debug.LogError($"Music field prefab '{prefab.name}' has no MusicField component.", definition);
                pool.Return(fieldObject);
                return false;
            }

            activeFieldPrefab = prefab;
            musicField.SetRadius(ResolveArea());
            if (definition.musicNotesPrefab != null)
            {
                musicNotes = pool.Spawn(definition.musicNotesPrefab, parent.position, Quaternion.identity, parent);
                if (musicNotes != null)
                {
                    musicNotes.transform.localPosition = Vector3.zero;
                    musicNotes.transform.localRotation = Quaternion.identity;
                    MusicNotes notes = musicNotes.GetComponent<MusicNotes>();
                    if (notes != null)
                    {
                        notes.ApplyFieldRadius(musicField.Radius, MusicField.BaselineRadius);
                    }
                }
            }

            return true;
        }

        private void StopMusicField()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (musicField != null)
            {
                if (pool != null)
                {
                    pool.Return(musicField.gameObject);
                }
                else
                {
                    Destroy(musicField.gameObject);
                }

                musicField = null;
                activeFieldPrefab = null;
            }

            if (musicNotes != null)
            {
                if (pool != null)
                {
                    pool.Return(musicNotes);
                }
                else
                {
                    Destroy(musicNotes);
                }

                musicNotes = null;
            }

            HitboxDisplay.Release(musicHitbox);
            musicHitbox = null;
        }

        // --- Orbit -----------------------------------------------------------

        private void UpdateOrbit()
        {
            if (blades.Count > 0)
            {
                int count = ResolveCount();
                if (count != activeOrbitCount)
                {
                    RebuildBlades(count);
                }

                TickOrbitMotion();
                if (Time.time >= orbitUntil)
                {
                    ClearBlades();
                    nextFireTime = Time.time + ResolveCooldown();
                }

                return;
            }

            if (Time.time < nextFireTime)
            {
                return;
            }

            RebuildBlades(ResolveCount());
            orbitUntil = Time.time + Mathf.Max(0.2f, ResolveDuration());
            TickOrbitMotion();
        }

        private void TickOrbitMotion()
        {
            WeaponLevel data = Data;
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

                float angle = orbitAngle + i * (360f / blades.Count);
                blade.SetOrbit(center, angle, radius);
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

            ObjectPoolManager pool = ObjectPoolManager.Instance;
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
            ObjectPoolManager pool = ObjectPoolManager.Instance;

            for (int i = 0; i < blades.Count; i++)
            {
                if (blades[i] == null)
                {
                    continue;
                }

                if (pool != null)
                {
                    pool.Return(blades[i].gameObject);
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

            return ResolveMoveFacing();
        }

        private Vector2 ResolveMoveFacing()
        {
            if (owner != null && owner.Velocity.sqrMagnitude > 0.04f)
            {
                lastMoveFacing = owner.Velocity.normalized;
            }

            return lastMoveFacing;
        }

        private void SpawnEffect(Vector2 center, Vector2 facing, float radius, float lifetime, bool areaEffect = false)
        {
            GameObject prefab = areaEffect ? definition.GetAreaEffectPrefab(level) : definition.effectPrefab;
            if (prefab == null || ObjectPoolManager.Instance == null)
            {
                return;
            }

            float angle = Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg - 90f;
            GameObject effect = ObjectPoolManager.Instance.Spawn(prefab, center, Quaternion.Euler(0f, 0f, angle));

            if (effect == null)
            {
                return;
            }

            if (!areaEffect || !definition.UsesSizedAreaEffects)
            {
                effect.transform.localScale = prefab.transform.localScale * radius;
            }

            AutoDespawn despawn = effect.GetComponent<AutoDespawn>();
            if (despawn != null)
            {
                despawn.SetLifetime(Mathf.Max(0.05f, lifetime));
            }
            else
            {
                ObjectPoolManager.Instance.ReturnDelayed(effect, Mathf.Max(0.05f, lifetime));
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
