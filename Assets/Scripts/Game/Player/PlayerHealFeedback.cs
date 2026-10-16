using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;

namespace KingsRescue.Player
{
    /// <summary>
    /// Plays the heal burst on the hero and holds the green flash until those particles finish.
    /// </summary>
    public class PlayerHealFeedback : MonoBehaviour
    {
        [SerializeField] private GameObject healingParticles;

        private PlayerHealth health;
        private GameObject active;
        private ParticleSystem system;
        private float emitUntil;
        private bool emitting;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        private void OnDisable()
        {
            Finish();
        }

        public void Play()
        {
            if (healingParticles == null || !isActiveAndEnabled)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                return;
            }

            if (active == null)
            {
                active = pool.Spawn(healingParticles, transform.position, Quaternion.identity, transform);
                if (active == null)
                {
                    return;
                }

                active.transform.localPosition = Vector3.zero;
                active.transform.localRotation = Quaternion.identity;
                system = active.GetComponentInChildren<ParticleSystem>();
            }

            if (system != null)
            {
                ParticleSystem.MainModule main = system.main;
                main.loop = false;
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                system.Play(true);
                emitUntil = Time.time + Mathf.Max(0.2f, main.duration);
                emitting = true;
            }
            else
            {
                emitUntil = Time.time + 0.6f;
                emitting = false;
            }

            if (health != null)
            {
                health.SetHealFlash(true);
            }
        }

        private void LateUpdate()
        {
            if (active == null)
            {
                return;
            }

            if (emitting && Time.time >= emitUntil && system != null)
            {
                system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                emitting = false;
            }

            bool alive = system != null && system.IsAlive(true);
            if (emitting || alive || Time.time < emitUntil)
            {
                return;
            }

            Finish();
        }

        private void Finish()
        {
            if (health != null)
            {
                health.SetHealFlash(false);
            }

            if (active == null)
            {
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                pool.Return(active);
            }
            else
            {
                Destroy(active);
            }

            active = null;
            system = null;
            emitting = false;
        }
    }
}
