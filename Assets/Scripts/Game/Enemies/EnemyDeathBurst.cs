using UnityEngine;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Optional pop when the enemy dies. Used by exploding slimes.
    /// </summary>
    public class EnemyDeathBurst : MonoBehaviour
    {
        [SerializeField] private float playerRadius = 0.35f;

        private float damage;
        private float radius;

        public void Configure(EnemyDefinition def, float runMinutes)
        {
            if (def == null || def.deathBurstDamage <= 0.01f)
            {
                damage = 0f;
                radius = 0f;
                return;
            }

            damage = def.ScaledDeathBurstDamage(runMinutes);
            radius = Mathf.Max(0.4f, def.deathBurstRadius);
        }

        public void Trigger()
        {
            if (damage <= 0.01f)
            {
                return;
            }

            PlayerController player = PlayerController.Instance;
            if (player == null || player.Health == null || !player.Health.IsAlive)
            {
                return;
            }

            float reach = radius + playerRadius;
            Vector2 delta = (Vector2)player.transform.position - (Vector2)transform.position;
            if (delta.sqrMagnitude > reach * reach)
            {
                return;
            }

            Vector2 direction = delta.sqrMagnitude > 0.0001f ? delta.normalized : Vector2.up;
            player.Health.TakeDamage(new DamageInfo(damage, player.transform.position, direction, 0f, gameObject));
        }
    }
}
