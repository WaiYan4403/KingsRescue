using System;
using UnityEngine;

namespace KingsRescue.Data
{
    /// <summary>
    /// Vampire Survivors-style cadence: one new enemy type unlocks every minute.
    /// Each unlocked type keeps its own spawn stream, so older types do not get
    /// diluted when a new one arrives.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Spawn Timeline", fileName = "SpawnTimeline")]
    public class SpawnTimeline : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public EnemyDefinition enemy;

            [Tooltip("Seconds into the run when this type joins. 0 uses index * unlock interval.")]
            public float unlockAt;

            [Tooltip("Relative pick chance once unlocked. 0 is treated as 1.")]
            public float weight;

            [Tooltip("How many to dump the moment this type arrives. Flock enemies ignore this and spawn one swarm.")]
            public int introCount;

            public SpawnPattern introPattern;
        }

        [Header("Cadence")]
        [Tooltip("Seconds between each new enemy type.")]
        public float unlockInterval = 60f;

        [Header("Trickle")]
        [Tooltip("Seconds between spawn ticks at the start of the run.")]
        public float baseSpawnInterval = 1.2f;

        [Tooltip("Floor for the spawn interval after the density ramp.")]
        public float minSpawnInterval = 0.38f;

        [Tooltip("How much the interval shrinks each minute.")]
        public float intervalDecayPerMinute = 0.055f;

        public int baseSpawnsPerTick = 1;

        [Tooltip("Add one extra enemy per tick every this many minutes.")]
        public int extraSpawnEveryMinutes = 5;

        public int maxSpawnsPerTick = 4;

        public SpawnPattern pattern = SpawnPattern.TopRandom;

        [Header("Per-type cadence")]
        [Tooltip("Elites wait this many times longer than fodder.")]
        public float eliteIntervalScale = 1.75f;

        [Header("Flock sessions")]
        [Tooltip("How long a bat/rat/crow flyover keeps dumping groups.")]
        public float flockSessionDuration = 11f;

        [Tooltip("Quiet time after a flyover before the next session.")]
        public float flockSessionCooldown = 24f;

        [Tooltip("Seconds between groups while a flyover session is active.")]
        public float flockSessionSpawnInterval = 2.5f;

        public Entry[] roster = Array.Empty<Entry>();

        public bool HasRoster => roster != null && roster.Length > 0;

        public float UnlockTime(int index)
        {
            if (roster == null || index < 0 || index >= roster.Length)
            {
                return float.PositiveInfinity;
            }

            float authored = roster[index].unlockAt;
            if (authored > 0f)
            {
                return authored;
            }

            return index * Mathf.Max(1f, unlockInterval);
        }

        public float SpawnIntervalAt(float runMinutes)
        {
            float interval = baseSpawnInterval - intervalDecayPerMinute * Mathf.Max(0f, runMinutes);
            return Mathf.Max(minSpawnInterval, interval);
        }

        public int SpawnsPerTickAt(float runMinutes)
        {
            int extraEvery = Mathf.Max(1, extraSpawnEveryMinutes);
            int extra = Mathf.FloorToInt(Mathf.Max(0f, runMinutes) / extraEvery);
            return Mathf.Clamp(baseSpawnsPerTick + extra, 1, Mathf.Max(1, maxSpawnsPerTick));
        }

        /// <summary>
        /// Seconds between spawns for this type. Old types keep this cadence after
        /// newer ones unlock; density rises because more streams are running.
        /// </summary>
        public float IntervalFor(Entry entry)
        {
            EnemyDefinition enemy = entry.enemy;
            float interval = Mathf.Max(0.2f, baseSpawnInterval);
            if (enemy == null)
            {
                return interval;
            }

            if (enemy.spawnAsFlock)
            {
                return FlockInSessionInterval;
            }

            if (enemy.isElite)
            {
                return Mathf.Max(minSpawnInterval, interval * Mathf.Max(1f, eliteIntervalScale));
            }

            return Mathf.Max(minSpawnInterval, interval);
        }

        public float FlockSessionDuration => Mathf.Max(4f, flockSessionDuration);

        public float FlockSessionCooldown => Mathf.Max(8f, flockSessionCooldown);

        public float FlockInSessionInterval => Mathf.Max(1.2f, flockSessionSpawnInterval);
    }
}
