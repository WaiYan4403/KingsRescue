using UnityEngine;
using KingsRescue.Meta;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// Rare arcade 'P'. Collecting it asks the hero to enter their powered form.
    /// </summary>
    public class PowerUpPickup : Pickup
    {
        [SerializeField] private SpriteRenderer flashTarget;
        [SerializeField] private Color dim = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private Color bright = new Color(1f, 0.95f, 0.45f, 1f);
        [SerializeField] private float pulseHz = 4f;

        public override void OnSpawned()
        {
            base.OnSpawned();
            if (flashTarget == null)
            {
                flashTarget = GetComponent<SpriteRenderer>();
            }
        }

        private void LateUpdate()
        {
            if (flashTarget == null)
            {
                return;
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);
            flashTarget.color = Color.Lerp(dim, bright, wave);
        }

        protected override void OnCollected(PlayerController hero)
        {
            AchievementProgress.RecordPowerUp();
            PowerUpController power = hero.GetComponent<PowerUpController>();
            power?.Activate();
        }
    }
}
