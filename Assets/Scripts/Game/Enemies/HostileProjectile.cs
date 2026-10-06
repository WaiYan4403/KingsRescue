using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Hostile shot aimed at the hero. Optional homing and a slow on hit (spider webs).
    /// </summary>
    public class HostileProjectile : MonoBehaviour, IPoolable
    {
        [SerializeField] private float hitRadius = 0.28f;
        [SerializeField] private bool rotateToDirection = true;
        [SerializeField] private float playerRadius = 0.35f;

        private Vector2 velocity;
        private float damage;
        private float expireAt;
        private float slowFactor;
        private float slowDuration;
        private bool homing;
        private float homingTurn = 4.5f;
        private GameObject source;

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
        }

        public void Launch(Vector2 direction, float speed, float damageAmount, float lifetime, float radius, float slow, float slowTime, bool homes, GameObject owner)
        {
            Vector2 dir = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.down;
            velocity = dir * Mathf.Max(0.5f, speed);
            damage = damageAmount;
            hitRadius = Mathf.Max(0.08f, radius);
            expireAt = Time.time + Mathf.Max(0.05f, lifetime);
            slowFactor = slow;
            slowDuration = slowTime;
            homing = homes;
            source = owner;
            FaceVelocity();
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (homing)
            {
                SteerTowardPlayer(dt);
            }

            Vector2 position = (Vector2)transform.position + velocity * dt;
            transform.position = position;
            FaceVelocity();

            if (Time.time >= expireAt)
            {
                Recycle();
                return;
            }

            ArenaBounds bounds = ArenaBounds.Instance;
            if (bounds != null && bounds.IsPastDespawn(position))
            {
                Recycle();
                return;
            }

            TryHitPlayer(position);
        }

        private void SteerTowardPlayer(float dt)
        {
            Transform player = PlayerController.PlayerTransform;
            if (player == null)
            {
                return;
            }

            Vector2 toPlayer = (Vector2)player.position - (Vector2)transform.position;
            if (toPlayer.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float speed = velocity.magnitude;
            Vector2 desired = toPlayer.normalized * speed;
            velocity = Vector2.Lerp(velocity, desired, 1f - Mathf.Exp(-homingTurn * dt));
        }

        private void TryHitPlayer(Vector2 position)
        {
            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive)
            {
                return;
            }

            float reach = hitRadius + playerRadius;
            if (((Vector2)player.transform.position - position).sqrMagnitude > reach * reach)
            {
                return;
            }

            if (!player.Health.IsInvulnerable)
            {
                Vector2 direction = velocity.sqrMagnitude > 0.0001f ? velocity.normalized : Vector2.down;
                player.Health.TakeDamage(new DamageInfo(damage, player.transform.position, direction, 0f, source));
            }

            PlayerStatus status = player.GetComponent<PlayerStatus>();
            if (status != null && slowDuration > 0.01f && slowFactor < 0.99f)
            {
                status.ApplySlow(slowFactor, slowDuration);
            }

            Recycle();
        }

        private void FaceVelocity()
        {
            if (!rotateToDirection || velocity.sqrMagnitude < 0.0001f)
            {
                return;
            }

            float angle = Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        private void Recycle()
        {
            if (PoolManager.Instance != null)
            {
                PoolManager.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
