using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// Base for everything the hero hoovers up. Like the combat code these use a
    /// registry and distance checks instead of colliders, so the prefabs stay trivial.
    /// </summary>
    public abstract class Pickup : MonoBehaviour, IPoolable
    {
        /// <summary>Every pickup currently on the field, scanned by <see cref="PickupMagnet"/>.</summary>
        public static readonly List<Pickup> Active = new List<Pickup>(128);

        [Header("Drift")]
        [Tooltip("How much of the world scroll carries this pickup away. 1 matches the ground.")]
        [SerializeField, Range(0f, 1f)] private float scrollInfluence = 1f;

        [Tooltip("Damping applied to the little burst it gets when dropped.")]
        [SerializeField] private float popDamping = 6f;

        [Header("Magnet")]
        [SerializeField] private float attractAcceleration = 40f;
        [SerializeField] private float maxAttractSpeed = 20f;
        [SerializeField] private float collectRadius = 0.35f;

        private Vector2 popVelocity;
        private Vector2 attractVelocity;
        private Transform target;

        public bool IsAttracted => target != null;

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public virtual void OnSpawned()
        {
            target = null;
            popVelocity = Vector2.zero;
            attractVelocity = Vector2.zero;
        }

        public virtual void OnDespawned()
        {
            target = null;
        }

        /// <summary>Gives the pickup a small outward burst when it drops.</summary>
        public void Pop(Vector2 velocity)
        {
            popVelocity = velocity;
        }

        /// <summary>Called by the magnet once the hero is close enough.</summary>
        public void Attract(Transform hero)
        {
            target = hero;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            Vector2 position = transform.position;

            if (target != null)
            {
                Vector2 toHero = (Vector2)target.position - position;
                float distance = toHero.magnitude;

                if (distance <= collectRadius)
                {
                    Collect();
                    return;
                }

                Vector2 desired = toHero / Mathf.Max(0.0001f, distance) * maxAttractSpeed;
                attractVelocity = Vector2.MoveTowards(attractVelocity, desired, attractAcceleration * dt);
                transform.position = position + attractVelocity * dt;
                return;
            }

            popVelocity = Vector2.MoveTowards(popVelocity, Vector2.zero, popDamping * dt);
            Vector2 scroll = Vector2.down * (ScrollDirector.Speed * scrollInfluence);
            transform.position = position + (popVelocity + scroll) * dt;
        }

        private void Collect()
        {
            PlayerController hero = PlayerController.Instance;
            if (hero != null)
            {
                OnCollected(hero);
                SoundEffects.Play(CollectSound);
            }

            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        protected virtual SoundId CollectSound => SoundId.None;

        protected abstract void OnCollected(PlayerController hero);
    }
}
