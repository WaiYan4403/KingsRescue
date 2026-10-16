using UnityEngine;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// Rare heal drop. Percentage-based so it stays relevant after Max HP upgrades.
    /// </summary>
    public class HealthPickup : Pickup
    {
        [SerializeField] private float flatHeal = 15f;

        [SerializeField, Range(0f, 1f)] private float percentOfMaxHeal = 0.1f;

        protected override void OnCollected(PlayerController hero)
        {
            PlayerHealth health = hero.Health;
            if (health == null)
            {
                return;
            }

            health.Heal(flatHeal + health.Max * percentOfMaxHeal);
            PlayerHealFeedback feedback = hero.GetComponent<PlayerHealFeedback>();
            if (feedback != null)
            {
                feedback.Play();
            }
        }
    }
}
