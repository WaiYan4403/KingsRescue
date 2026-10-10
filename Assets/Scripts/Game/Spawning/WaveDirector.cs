using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Enemies;
using KingsRescue.Player;

namespace KingsRescue.Spawning
{
    /// <summary>
    /// Feeds enemies in from off screen. Prefers a SpawnTimeline (new type every
    /// minute, previous types stay in the mix). Falls back to authored wave windows.
    /// Boss appointments still overlay the trickle.
    /// </summary>
    public class WaveDirector : MonoBehaviour
    {
        private class WaveState
        {
            public WaveDefinition definition;
            public bool started;
            public bool finished;
            public float nextSpawnTime;
            public int nextBurstIndex;
        }

        [SerializeField] private List<WaveDefinition> waves = new List<WaveDefinition>();

        [Header("Minute Cadence")]
        [Tooltip("Vampire Survivors-style roster: a new type every minute. When set, authored waves are ignored.")]
        [SerializeField] private SpawnTimeline timeline;

        [Header("Budget")]
        [Tooltip("Hard cap on simultaneous enemies. Protects WebGL frame time.")]
        [SerializeField] private int maxAliveEnemies = 140;

        [Tooltip("Caps how many enemies enter the field in one frame so bursts do not hitch.")]
        [SerializeField] private int maxSpawnsPerFrame = 4;

        [Tooltip("World-unit inset from the world edge when picking a spawn x.")]
        [SerializeField] private float horizontalInset = 0.6f;

        [Header("Fallback")]
        [Tooltip("Used once every authored wave has expired so the run never goes quiet.")]
        [SerializeField] private WaveDefinition endlessWave;

        [Header("Boss")]
        [SerializeField] private EnemyDefinition firstBoss;

        [Tooltip("Seconds into the run when Slime King walks in.")]
        [SerializeField] private float firstBossTime = 120f;

        [Tooltip("Stop regular spawns this many seconds before a boss appointment.")]
        [SerializeField] private float bossHushLead = 5f;

        [SerializeField] private BossAppointment[] bosses = System.Array.Empty<BossAppointment>();

        private readonly List<WaveState> states = new List<WaveState>();
        private readonly List<PendingSpawn> pending = new List<PendingSpawn>(32);
        private int spawnedThisFrame;
        private bool firstBossSpawned;
        private int nextBossIndex;
        private int unlockedCount;
        private float[] nextTypeSpawnTime;
        private float[] flockSessionEndTime;
        private float[] flockResumeTime;

        public static string LastUnlockAnnouncement { get; private set; }

        public static float LastUnlockAnnounceUntil { get; private set; }

        [System.Serializable]
        private struct BossAppointment
        {
            public EnemyDefinition boss;
            public float atTime;
        }

        public int AliveCount => Enemy.Active.Count;

        private struct PendingSpawn
        {
            public EnemyDefinition definition;
            public Vector2 position;
            public float runMinutes;
            public SpawnPattern pattern;
            public Vector2 heading;
            public bool hasHeading;
        }

        private void Awake()
        {
            LastUnlockAnnouncement = null;
            LastUnlockAnnounceUntil = 0f;

            if (UsingTimeline)
            {
                EnsureTimelineArrays();
                return;
            }

            for (int i = 0; i < waves.Count; i++)
            {
                if (waves[i] != null)
                {
                    states.Add(new WaveState { definition = waves[i] });
                }
            }

            if (endlessWave != null)
            {
                states.Add(new WaveState { definition = endlessWave });
            }
        }

        private bool UsingTimeline => timeline != null && timeline.HasRoster;

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying)
            {
                return;
            }

            spawnedThisFrame = 0;

            float runTime = game.RunTime;
            float runMinutes = runTime / 60f;

            TrySpawnScheduledBoss(runTime, runMinutes);

            if (game.BossFightActive || RegularSpawnsHushed(runTime))
            {
                pending.Clear();
                return;
            }

            FlushPending();

            if (UsingTimeline)
            {
                TickTimeline(runTime, runMinutes);
                return;
            }

            for (int i = 0; i < states.Count; i++)
            {
                TickWave(states[i], runTime, runMinutes);
            }
        }

        private void EnsureTimelineArrays()
        {
            int count = timeline.roster.Length;
            if (nextTypeSpawnTime == null || nextTypeSpawnTime.Length != count)
            {
                nextTypeSpawnTime = new float[count];
            }

            if (flockSessionEndTime == null || flockSessionEndTime.Length != count)
            {
                flockSessionEndTime = new float[count];
            }

            if (flockResumeTime == null || flockResumeTime.Length != count)
            {
                flockResumeTime = new float[count];
            }
        }

        private void TickTimeline(float runTime, float runMinutes)
        {
            EnsureTimelineArrays();
            UnlockDueTypes(runTime, runMinutes);

            for (int i = 0; i < unlockedCount; i++)
            {
                SpawnTimeline.Entry entry = timeline.roster[i];
                EnemyDefinition enemy = entry.enemy;
                if (enemy == null || enemy.isBoss || enemy.bossMinion)
                {
                    continue;
                }

                if (enemy.spawnAsFlock)
                {
                    TickFlockSession(i, runTime, runMinutes, entry);
                    continue;
                }

                if (runTime < nextTypeSpawnTime[i])
                {
                    continue;
                }

                nextTypeSpawnTime[i] = runTime + timeline.IntervalFor(entry);
                SpawnTimelineEnemy(enemy, runMinutes);
            }
        }

        private void TickFlockSession(int index, float runTime, float runMinutes, SpawnTimeline.Entry entry)
        {
            if (runTime < flockSessionEndTime[index])
            {
                if (runTime < nextTypeSpawnTime[index])
                {
                    return;
                }

                nextTypeSpawnTime[index] = runTime + timeline.FlockInSessionInterval;
                SpawnTimelineEnemy(entry.enemy, runMinutes);
                return;
            }

            if (flockSessionEndTime[index] > 0f && runTime < flockResumeTime[index])
            {
                return;
            }

            BeginFlockSession(index, runTime, runMinutes, entry.enemy);
        }

        private void BeginFlockSession(int index, float runTime, float runMinutes, EnemyDefinition enemy)
        {
            float duration = timeline.FlockSessionDuration * UnityEngine.Random.Range(0.85f, 1.15f);
            float cooldown = timeline.FlockSessionCooldown * UnityEngine.Random.Range(0.9f, 1.2f);
            flockSessionEndTime[index] = runTime + duration;
            flockResumeTime[index] = flockSessionEndTime[index] + cooldown;
            nextTypeSpawnTime[index] = runTime + timeline.FlockInSessionInterval;
            string name = string.IsNullOrEmpty(enemy.displayName) ? enemy.name : enemy.displayName;
            Debug.Log($"[Spawn] {name} flyover for {duration:0.0}s, then {cooldown:0.0}s quiet.", enemy);
            SpawnTimelineEnemy(enemy, runMinutes);
        }

        private void SpawnTimelineEnemy(EnemyDefinition enemy, float runMinutes)
        {
            if (enemy.spawnAsFlock)
            {
                SpawnPattern flockPattern = UnityEngine.Random.value < 0.5f
                    ? SpawnPattern.FlockDown
                    : SpawnPattern.FlockDiagonal;
                SpawnGroup(enemy, RollFlockSize(enemy), flockPattern, runMinutes);
                return;
            }

            SpawnGroup(enemy, 1, timeline.pattern, runMinutes);
        }

        private void UnlockDueTypes(float runTime, float runMinutes)
        {
            while (unlockedCount < timeline.roster.Length
                   && runTime >= timeline.UnlockTime(unlockedCount))
            {
                SpawnTimeline.Entry entry = timeline.roster[unlockedCount];
                unlockedCount++;

                EnemyDefinition enemy = entry.enemy;
                if (enemy == null || enemy.isBoss || enemy.bossMinion)
                {
                    continue;
                }

                string name = string.IsNullOrEmpty(enemy.displayName) ? enemy.name : enemy.displayName;
                LastUnlockAnnouncement = name;
                LastUnlockAnnounceUntil = Time.unscaledTime + 6f;
                Debug.Log($"[Spawn] New enemy type: {name} (minute {Mathf.FloorToInt(runMinutes)})", enemy);

                int unlockedIndex = unlockedCount - 1;
                if (enemy.spawnAsFlock)
                {
                    BeginFlockSession(unlockedIndex, runTime, runMinutes, enemy);
                    continue;
                }

                if (nextTypeSpawnTime != null && unlockedIndex < nextTypeSpawnTime.Length)
                {
                    nextTypeSpawnTime[unlockedIndex] = runTime + timeline.IntervalFor(entry);
                }

                int intro = Mathf.Max(0, entry.introCount);
                if (intro > 0)
                {
                    SpawnGroup(enemy, intro, entry.introPattern, runMinutes);
                }
            }
        }

        private void TickWave(WaveState state, float runTime, float runMinutes)
        {
            WaveDefinition def = state.definition;

            bool isEndless = def == endlessWave;
            bool active = isEndless ? AllAuthoredWavesFinished(runTime) : def.IsActiveAt(runTime);

            if (!active)
            {
                if (state.started && !isEndless && runTime >= def.EndTime)
                {
                    state.finished = true;
                }

                return;
            }

            if (!state.started)
            {
                state.started = true;
                state.nextSpawnTime = runTime;
                state.nextBurstIndex = 0;
            }

            float elapsedInWave = runTime - def.startTime;

            while (state.nextBurstIndex < def.bursts.Length
                   && elapsedInWave >= def.bursts[state.nextBurstIndex].atTime)
            {
                WaveDefinition.Burst burst = def.bursts[state.nextBurstIndex];
                state.nextBurstIndex++;

                if (burst.enemy != null)
                {
                    SpawnGroup(burst.enemy, Mathf.Max(1, burst.count), burst.pattern, runMinutes);
                }
            }

            if (def.spawnInterval <= 0f || runTime < state.nextSpawnTime)
            {
                return;
            }

            state.nextSpawnTime = runTime + def.spawnInterval;

            int count = Mathf.Max(1, def.spawnsPerTick);
            for (int i = 0; i < count; i++)
            {
                EnemyDefinition enemy = def.PickEnemy();
                if (enemy == null)
                {
                    break;
                }

                if (enemy.spawnAsFlock)
                {
                    int flock = RollFlockSize(enemy);
                    SpawnPattern flockPattern = Random.value < 0.5f
                        ? SpawnPattern.FlockDown
                        : SpawnPattern.FlockDiagonal;
                    SpawnGroup(enemy, flock, flockPattern, runMinutes);
                    break;
                }

                SpawnGroup(enemy, 1, def.pattern, runMinutes, i, count);
            }
        }

        private bool RegularSpawnsHushed(float runTime)
        {
            float nextBoss = PeekNextBossTime();
            if (float.IsPositiveInfinity(nextBoss))
            {
                return false;
            }

            float lead = bossHushLead > 0.01f ? bossHushLead : 5f;
            return runTime >= nextBoss - lead;
        }

        private float PeekNextBossTime()
        {
            if (bosses != null && bosses.Length > 0)
            {
                if (nextBossIndex >= bosses.Length)
                {
                    return float.PositiveInfinity;
                }

                return bosses[nextBossIndex].atTime;
            }

            if (firstBossSpawned || firstBoss == null)
            {
                return float.PositiveInfinity;
            }

            return firstBossTime;
        }

        private void TrySpawnScheduledBoss(float runTime, float runMinutes)
        {
            GameManager game = GameManager.Instance;
            if (game != null && game.BossFightActive)
            {
                return;
            }

            EnemyDefinition boss;
            if (bosses != null && bosses.Length > 0)
            {
                if (nextBossIndex >= bosses.Length)
                {
                    return;
                }

                BossAppointment next = bosses[nextBossIndex];
                if (next.boss == null || next.boss.prefab == null || runTime < next.atTime)
                {
                    return;
                }

                boss = next.boss;
                nextBossIndex++;
            }
            else
            {
                if (firstBossSpawned || firstBoss == null || firstBoss.prefab == null || runTime < firstBossTime)
                {
                    return;
                }

                boss = firstBoss;
                firstBossSpawned = true;
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return;
            }

            Vector2 position = new Vector2(bounds.WorldRect.center.x, bounds.TopSpawnY);
            SpawnImmediate(boss, position, runMinutes, SpawnPattern.TopRandom);
            game?.BeginBossFight();
        }

        private bool AllAuthoredWavesFinished(float runTime)
        {
            for (int i = 0; i < states.Count; i++)
            {
                WaveState s = states[i];
                if (s.definition == endlessWave)
                {
                    continue;
                }

                if (runTime < s.definition.EndTime)
                {
                    return false;
                }
            }

            return true;
        }

        private static int RollFlockSize(EnemyDefinition enemy)
        {
            int min = enemy.flockMin > 1 ? enemy.flockMin : 8;
            int max = enemy.flockMax >= min ? enemy.flockMax : 12;
            return Random.Range(min, max + 1);
        }

        private static bool IsCohesiveFlock(SpawnPattern pattern, int count)
        {
            if (count <= 1)
            {
                return false;
            }

            return pattern == SpawnPattern.CrossScreen
                || pattern == SpawnPattern.DiagonalDive
                || pattern == SpawnPattern.FlockDown
                || pattern == SpawnPattern.FlockDiagonal;
        }

        private void SpawnGroup(EnemyDefinition enemy, int count, SpawnPattern pattern, float runMinutes, int indexOffset = 0, int groupSize = 1)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return;
            }

            if (IsCohesiveFlock(pattern, count))
            {
                SpawnFlock(enemy, count, pattern, runMinutes);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                if (Enemy.Active.Count >= maxAliveEnemies)
                {
                    return;
                }

                int slot = count > 1 ? i : indexOffset;
                int slotTotal = count > 1 ? count : groupSize;

                Spawn(enemy, ResolveSpawnPoint(bounds, pattern, slot, slotTotal), runMinutes, pattern);
            }
        }

        private void SpawnFlock(EnemyDefinition enemy, int count, SpawnPattern pattern, float runMinutes)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
                return;
            }

            Rect world = bounds.WorldRect;
            Rect screen = bounds.ScreenRect;
            bool diveDown = pattern == SpawnPattern.FlockDown;
            bool fromLeft = Random.value < 0.5f;
            Vector2 origin;
            Vector2 heading;

            if (diveDown)
            {
                float x = Random.Range(world.xMin + horizontalInset + 0.6f, world.xMax - horizontalInset - 0.6f);
                float y = Mathf.Min(bounds.TopSpawnY, screen.yMax + 0.7f);
                origin = new Vector2(x, y);
                heading = Vector2.down;
            }
            else
            {
                float y = Random.Range(
                    Mathf.Lerp(screen.yMin, screen.yMax, 0.42f),
                    screen.yMax - 0.85f);
                float x = fromLeft ? screen.xMin - 0.55f : screen.xMax + 0.55f;
                origin = new Vector2(x, y);
                heading = new Vector2(fromLeft ? 0.85f : -0.85f, -1f).normalized;
            }

            // Tight overlapping disk. Golden-angle fill, then a little jitter so
            // it reads as a swarm instead of a formation.
            const float goldenAngle = 2.3999632f;
            float packRadius = 0.55f + count * 0.04f;
            float spin = Random.Range(0f, Mathf.PI * 2f);

            for (int i = 0; i < count; i++)
            {
                if (Enemy.Active.Count >= maxAliveEnemies)
                {
                    return;
                }

                float radius = packRadius * Mathf.Sqrt((i + 0.35f) / count) * Random.Range(0.55f, 1.05f);
                float angle = spin + i * goldenAngle + Random.Range(-0.28f, 0.28f);
                Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                SpawnImmediate(enemy, origin + offset, runMinutes, pattern, heading, true);
            }
        }

        private Vector2 ResolveSpawnPoint(ArenaBounds bounds, SpawnPattern pattern, int index, int total)
        {
            Rect world = bounds.WorldRect;

            switch (pattern)
            {
                case SpawnPattern.TopLine:
                {
                    float t = total <= 1 ? 0.5f : index / (float)(total - 1);
                    float x = Mathf.Lerp(world.xMin + horizontalInset, world.xMax - horizontalInset, t);
                    return new Vector2(x, bounds.TopSpawnY);
                }

                case SpawnPattern.Sides:
                {
                    bool left = total <= 1 ? Random.value < 0.5f : index % 2 == 0;
                    return EdgeSpawnPoint(bounds, left);
                }

                case SpawnPattern.SurroundPlayer:
                {
                    Vector2 center = PlayerController.Instance != null
                        ? (Vector2)PlayerController.Instance.transform.position
                        : world.center;

                    float radius = Mathf.Max(world.width, world.height) * 0.6f;
                    float angle = total <= 1
                        ? Random.value * Mathf.PI * 2f
                        : index / (float)total * Mathf.PI * 2f;

                    return center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
                }

                case SpawnPattern.FlockDown:
                    return new Vector2(bounds.RandomSpawnX(horizontalInset + 0.6f), bounds.TopSpawnY);

                case SpawnPattern.CrossScreen:
                case SpawnPattern.FlockDiagonal:
                {
                    bool left = Random.value < 0.5f;
                    Rect screen = bounds.ScreenRect;
                    float x = left ? screen.xMin - 0.55f : screen.xMax + 0.55f;
                    float y = Random.Range(Mathf.Lerp(screen.yMin, screen.yMax, 0.42f), screen.yMax - 0.85f);
                    return new Vector2(x, y);
                }

                case SpawnPattern.DiagonalDive:
                {
                    bool fromLeft = Random.value < 0.5f;
                    float x = fromLeft
                        ? world.xMin + horizontalInset
                        : world.xMax - horizontalInset;
                    return new Vector2(x, bounds.TopSpawnY);
                }

                default:
                {
                    // Mix top with left/right so packs are not deleted in the
                    // projectile gauntlet above the screen before they appear.
                    float roll = Random.value;
                    if (roll < 0.32f)
                    {
                        return bounds.RandomTopSpawnPoint(horizontalInset);
                    }

                    return EdgeSpawnPoint(bounds, roll < 0.66f);
                }
            }
        }

        private Vector2 EdgeSpawnPoint(ArenaBounds bounds, bool left)
        {
            Rect world = bounds.WorldRect;
            Rect screen = bounds.ScreenRect;
            float x = left
                ? Mathf.Max(world.xMin, screen.xMin) + 0.45f
                : Mathf.Min(world.xMax, screen.xMax) - 0.45f;
            float yMin = Mathf.Max(world.yMin + 1.6f, screen.yMin + 1.6f);
            float yMax = Mathf.Min(world.yMax - 1.2f, screen.yMax - 1.2f);
            if (yMax <= yMin)
            {
                yMax = yMin + 0.5f;
            }

            return new Vector2(x, Random.Range(yMin, yMax));
        }

        private void Spawn(EnemyDefinition definition, Vector2 position, float runMinutes, SpawnPattern pattern, Vector2 heading = default, bool hasHeading = false)
        {
            if (definition == null)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game != null && game.BossFightActive && !definition.isBoss && !definition.bossMinion)
            {
                return;
            }

            if (definition.prefab == null)
            {
                Debug.LogError($"EnemyDefinition '{definition.name}' has no prefab assigned.", definition);
                return;
            }

            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            if (Enemy.Active.Count >= maxAliveEnemies)
            {
                return;
            }

            if (spawnedThisFrame >= Mathf.Max(1, maxSpawnsPerFrame))
            {
                pending.Add(new PendingSpawn
                {
                    definition = definition,
                    position = position,
                    runMinutes = runMinutes,
                    pattern = pattern,
                    heading = heading,
                    hasHeading = hasHeading || heading.sqrMagnitude > 0.0001f
                });
                return;
            }

            SpawnImmediate(definition, position, runMinutes, pattern, heading, hasHeading || heading.sqrMagnitude > 0.0001f);
        }

        private void FlushPending()
        {
            int written = 0;
            for (int i = 0; i < pending.Count; i++)
            {
                if (spawnedThisFrame >= Mathf.Max(1, maxSpawnsPerFrame) || Enemy.Active.Count >= maxAliveEnemies)
                {
                    pending[written++] = pending[i];
                    continue;
                }

                PendingSpawn spawn = pending[i];
                SpawnImmediate(spawn.definition, spawn.position, spawn.runMinutes, spawn.pattern, spawn.heading, spawn.hasHeading);
            }

            if (written < pending.Count)
            {
                pending.RemoveRange(written, pending.Count - written);
            }
        }

        private void SpawnImmediate(EnemyDefinition definition, Vector2 position, float runMinutes, SpawnPattern pattern, Vector2 heading = default, bool hasHeading = false)
        {
            PoolManager pool = PoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            GameObject instance = pool.Spawn(definition.prefab, position, Quaternion.identity);
            if (instance == null)
            {
                return;
            }

            spawnedThisFrame++;

            Enemy enemy = instance.GetComponent<Enemy>();
            if (enemy == null)
            {
                Debug.LogError($"Enemy prefab '{definition.prefab.name}' is missing an Enemy component.", definition.prefab);
                return;
            }

            enemy.Configure(definition, runMinutes);
            if (hasHeading)
            {
                enemy.SetLockedHeading(heading);
            }
            else
            {
                enemy.SetSpawnHeading(position, pattern);
            }
        }
    }
}
