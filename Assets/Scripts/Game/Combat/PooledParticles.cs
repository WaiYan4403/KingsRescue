using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Restarts pooled particle systems from the first particle each time they leave the pool.
    /// </summary>
    public class PooledParticles : MonoBehaviour, IPoolable
    {
        private ParticleSystem[] systems;

        private void Awake()
        {
            systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        public void OnSpawned()
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                if (system == null)
                {
                    continue;
                }

                system.Clear(true);
                system.Play(true);
            }
        }

        public void OnDespawned()
        {
            if (systems == null)
            {
                return;
            }

            for (int i = 0; i < systems.Length; i++)
            {
                if (systems[i] != null)
                {
                    systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }
    }
}
