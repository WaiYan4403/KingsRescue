using System;
using UnityEngine;

namespace KingsRescue.Data
{
    public enum SpawnPattern
    {
        /// <summary>Trickle spawn: mix of the top edge and the left/right sides.</summary>
        TopRandom = 0,

        /// <summary>Evenly spaced line across the top edge.</summary>
        TopLine = 1,

        /// <summary>Alternating left and right edges, walking inwards.</summary>
        Sides = 2,

        /// <summary>Ring centred on the player, just outside the visible area.</summary>
        SurroundPlayer = 3,

        /// <summary>Left or right edge, flying across the road. Used by bat packs.</summary>
        CrossScreen = 4,

        /// <summary>Top corners diving diagonally through the field.</summary>
        DiagonalDive = 5
    }

    /// <summary>
    /// One slice of the run's difficulty curve: which enemies appear, how often,
    /// and any scripted bursts inside the window.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Wave Definition", fileName = "Wave_")]
    public class WaveDefinition : ScriptableObject
    {
        [Serializable]
        public struct EnemyEntry
        {
            public EnemyDefinition enemy;

            [Tooltip("Relative chance of picking this enemy. 0 disables it.")]
            public float weight;
        }

        [Serializable]
        public struct Burst
        {
            [Tooltip("Seconds after the wave starts.")]
            public float atTime;

            public EnemyDefinition enemy;
            public int count;
            public SpawnPattern pattern;
        }

        [Header("Window")]
        public string label = "Wave";

        [Tooltip("Seconds into the run when this wave activates.")]
        public float startTime;

        [Tooltip("How long the wave stays active. Waves may overlap.")]
        public float duration = 60f;

        [Header("Trickle")]
        [Tooltip("Seconds between spawn ticks.")]
        public float spawnInterval = 1.1f;

        [Tooltip("Enemies spawned per tick.")]
        public int spawnsPerTick = 1;

        public SpawnPattern pattern = SpawnPattern.TopRandom;

        public EnemyEntry[] enemies = Array.Empty<EnemyEntry>();

        [Header("Scripted Bursts")]
        public Burst[] bursts = Array.Empty<Burst>();

        public float EndTime => startTime + duration;

        public bool IsActiveAt(float runTime)
        {
            return runTime >= startTime && runTime < EndTime;
        }

        /// <summary>Weighted pick from the enemy table. Null when the table is empty.</summary>
        public EnemyDefinition PickEnemy()
        {
            float total = 0f;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i].enemy != null)
                {
                    total += Mathf.Max(0f, enemies[i].weight);
                }
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = UnityEngine.Random.value * total;
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i].enemy == null)
                {
                    continue;
                }

                roll -= Mathf.Max(0f, enemies[i].weight);
                if (roll <= 0f)
                {
                    return enemies[i].enemy;
                }
            }

            return enemies[enemies.Length - 1].enemy;
        }
    }
}
