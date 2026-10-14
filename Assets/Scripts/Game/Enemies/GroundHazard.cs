using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Fire patch or poison pool left on the road. Ticks the player while they stand in it.
    /// </summary>
    public class GroundHazard : MonoBehaviour, IPoolable
    {
        [SerializeField] private float radius = 0.85f;
        [SerializeField] private float playerRadius = 0.35f;

        private float damage;
        private float slowFactor = 1f;
        private float slowDuration;
        private float poisonDps;
        private float expireAt;
        private float nextTick;

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
        }

        public void Arm(float radiusValue, float damagePerTick, float lifetime, float slow, float slowTime, float poison)
        {
            radius = Mathf.Max(0.2f, radiusValue);
            damage = damagePerTick;
            expireAt = Time.time + Mathf.Max(0.2f, lifetime);
            slowFactor = slow;
            slowDuration = slowTime;
            poisonDps = poison;
            nextTick = Time.time + 0.12f;
            transform.localScale = Vector3.one * (radius * 2f);
        }

        private void Update()
        {
            GameManager game = GameManager.Instance;
            if (game != null && !game.IsPlaying)
            {
                return;
            }

            if (Time.time >= expireAt)
            {
                Recycle();
                return;
            }

            if (Time.time < nextTick)
            {
                return;
            }

            nextTick = Time.time + 0.35f;
            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive)
            {
                return;
            }

            float reach = radius + playerRadius;
            if (((Vector2)player.transform.position - (Vector2)transform.position).sqrMagnitude > reach * reach)
            {
                return;
            }

            if (damage > 0.01f)
            {
                player.Health.ApplyDot(damage);
            }

            PlayerStatus status = player.Status;
            if (status == null)
            {
                return;
            }

            if (slowDuration > 0.01f && slowFactor < 0.99f)
            {
                status.ApplySlow(slowFactor, slowDuration);
            }

            if (poisonDps > 0.01f)
            {
                status.ApplyPoison(poisonDps, 1.6f);
            }
        }

        private void Recycle()
        {
            if (ObjectPoolManager.Instance != null)
            {
                ObjectPoolManager.Instance.Return(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
