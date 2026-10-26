using System;
using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Body damage. Uses a plain distance test rather than trigger callbacks so it
    /// works without a Rigidbody2D on every enemy, which matters at high enemy counts.
    /// </summary>
    public class ContactDamage : MonoBehaviour, IPoolable
    {
        [SerializeField] private float radius = 0.5f;
        [SerializeField] private float damage = 8f;
        [SerializeField] private float interval = 0.6f;

        [Tooltip("Half-size of the player's hurt box, added to the radius.")]
        [SerializeField] private float playerRadius = 0.35f;

        private float nextHitTime;
        private HitboxMarker hitbox;
        private EnemyDefinition source;

        public event Action HitPlayer;

        public void OnSpawned()
        {
            nextHitTime = 0f;
        }

        public void OnDespawned()
        {
            HitboxDisplay.Release(hitbox);
            hitbox = null;
        }

        public void Configure(EnemyDefinition definition, float runMinutes)
        {
            source = definition;
            radius = definition.contactRadius;
            damage = definition.ScaledContactDamage(runMinutes);
            interval = definition.contactInterval;
            nextHitTime = 0f;
        }

        private void Update()
        {
            HitboxDisplay.SyncCircle(ref hitbox, transform.position, radius);

            if (Time.time < nextHitTime)
            {
                return;
            }

            // Time.time is frozen during the level-up pause, so without this an enemy
            // parked on the player would still land one free hit.
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            PlayerController player = PlayerController.Instance;
            if (player == null)
            {
                return;
            }

            float reach = radius + playerRadius;
            Vector2 delta = (Vector2)player.transform.position - (Vector2)transform.position;

            if (delta.sqrMagnitude > reach * reach)
            {
                return;
            }

            PlayerHealth health = player.Health;
            if (health == null || !health.IsAlive || health.IsInvulnerable)
            {
                return;
            }

            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
            health.TakeDamage(new DamageInfo(damage, player.transform.position, direction, 0f, gameObject));
            SoundEffects.PlayEnemy(source, EnemyCue.Attack);
            nextHitTime = Time.time + interval;
            HitPlayer?.Invoke();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
    }
}
