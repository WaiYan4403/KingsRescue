using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Meta;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// Rare arcade 'P'. Collecting it asks the hero to enter their powered form.
    /// </summary>
    public class PowerUpPickup : Pickup
    {
        [Tooltip("The stronger pickup. Counts separately and starts the powered form at full strength.")]
        [SerializeField] private bool maxForm;

        [SerializeField] private SpriteRenderer flashTarget;

        [Tooltip("Pulse the pickup between two colors. Leave off to keep the sprite's own color.")]
        [SerializeField] private bool pulseColor;

        [SerializeField] private Color dim = new Color(1f, 0.72f, 0.12f, 1f);
        [SerializeField] private Color bright = new Color(1f, 0.95f, 0.45f, 1f);
        [SerializeField] private float pulseHz = 4f;

        private Color restColor = Color.white;
        private bool restColorCached;

        public override void OnSpawned()
        {
            base.OnSpawned();
            if (flashTarget == null)
            {
                flashTarget = GetComponent<SpriteRenderer>();
            }

            if (flashTarget != null && !restColorCached)
            {
                restColor = flashTarget.color;
                restColorCached = true;
            }
        }

        private void LateUpdate()
        {
            if (flashTarget == null)
            {
                return;
            }

            if (!pulseColor)
            {
                if (restColorCached)
                {
                    flashTarget.color = restColor;
                }

                return;
            }

            float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseHz * Mathf.PI * 2f);
            flashTarget.color = Color.Lerp(dim, bright, wave);
        }

        protected override void OnCollected(PlayerController hero)
        {
            AchievementProgress.RecordPowerUp();
            GameManager.Instance?.RegisterPowerUp(maxForm);
            PowerUpController power = hero.GetComponent<PowerUpController>();
            if (maxForm)
            {
                power?.ActivateMax();
            }
            else
            {
                power?.Activate();
            }
        }
    }
}
