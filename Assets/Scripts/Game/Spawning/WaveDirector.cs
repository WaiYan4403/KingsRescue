using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Enemies;
using KingsRescue.Player;

namespace KingsRescue.Spawning
{
    /// <summary>
    /// Walks the authored wave list against the run clock and feeds enemies in from
    /// off screen. Every spawn goes through the pool and carries the current
    /// difficulty ramp.
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

        private readonly List<WaveState> states = new List<WaveState>();
        private readonly List<PendingSpawn> pending = new List<PendingSpawn>(32);
        private int spawnedThisFrame;

        public int AliveCount => Enemy.Active.Count;

        private struct PendingSpawn
        {
            public EnemyDefinition definition;
            public Vector2 position;
            public float runMinutes;
            public SpawnPattern pattern;
        }

        private void Awake()
        {
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

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.IsPlaying)
            {
                return;
            }

            spawnedThisFrame = 0;
            FlushPending();

            float runTime = game.RunTime;
            float runMinutes = runTime / 60f;

            for (int i = 0; i < states.Count; i++)
            {
                TickWave(states[i], runTime, runMinutes);
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

                SpawnGroup(enemy, 1, def.pattern, runMinutes, i, count);
            }
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

        private void SpawnGroup(EnemyDefinition enemy, int count, SpawnPattern pattern, float runMinutes, int indexOffset = 0, int groupSize = 1)
        {
            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds == null)
            {
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
                    bool left = index % 2 == 0;
                    float x = left ? bounds.LeftDespawnX + 0.5f : bounds.RightDespawnX - 0.5f;
                    float y = Random.Range(world.center.y, world.yMax);
                    return new Vector2(x, y);
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

                case SpawnPattern.CrossScreen:
                {
                    bool left = index % 2 == 0;
                    float x = left ? bounds.LeftDespawnX + 0.4f : bounds.RightDespawnX - 0.4f;
                    float y = Mathf.Lerp(world.yMin + 1.5f, world.yMax - 0.5f, total <= 1 ? Random.value : index / (float)Mathf.Max(1, total - 1));
                    return new Vector2(x, y);
                }

                case SpawnPattern.DiagonalDive:
                {
                    float t = total <= 1 ? Random.value : index / (float)(total - 1);
                    bool fromLeft = index % 2 == 0;
                    float x = fromLeft
                        ? Mathf.Lerp(world.xMin + horizontalInset, world.center.x, t)
                        : Mathf.Lerp(world.center.x, world.xMax - horizontalInset, t);
                    return new Vector2(x, bounds.TopSpawnY);
                }

                default:
                    return bounds.RandomTopSpawnPoint(horizontalInset);
            }
        }

        private void Spawn(EnemyDefinition definition, Vector2 position, float runMinutes, SpawnPattern pattern)
        {
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
                    pattern = pattern
                });
                return;
            }

            SpawnImmediate(definition, position, runMinutes, pattern);
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
                SpawnImmediate(spawn.definition, spawn.position, spawn.runMinutes, spawn.pattern);
            }

            if (written < pending.Count)
            {
                pending.RemoveRange(written, pending.Count - written);
            }
        }

        private void SpawnImmediate(EnemyDefinition definition, Vector2 position, float runMinutes, SpawnPattern pattern)
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
            enemy.SetSpawnHeading(position, pattern);
        }
    }
}
