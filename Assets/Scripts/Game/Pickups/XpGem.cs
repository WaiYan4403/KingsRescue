using UnityEngine;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// The EXP gem every kill leaves behind.
    /// </summary>
    public class XpGem : Pickup
    {
        [SerializeField] private int value = 1;

        [Tooltip("Optional tiers, brightest last. The gem picks the highest tier its value reaches.")]
        [SerializeField] private Tier[] tiers;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [System.Serializable]
        private struct Tier
        {
            public int minValue;
            public Color color;
            public float scale;
        }

        private Vector3 baseScale;

        private void Awake()
        {
            baseScale = transform.localScale;

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
        }

        public override void OnSpawned()
        {
            base.OnSpawned();
            ApplyTier();
        }

        public void SetValue(int xp)
        {
            value = Mathf.Max(1, xp);
            ApplyTier();
        }

        protected override void OnCollected(PlayerController hero)
        {
            ExperienceSystem.Instance?.AddXp(value);
        }

        private void ApplyTier()
        {
            if (tiers == null || tiers.Length == 0)
            {
                return;
            }

            Tier chosen = tiers[0];
            for (int i = 0; i < tiers.Length; i++)
            {
                if (value >= tiers[i].minValue)
                {
                    chosen = tiers[i];
                }
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = chosen.color;
            }

            float scale = chosen.scale <= 0f ? 1f : chosen.scale;
            transform.localScale = baseScale * scale;
        }
    }
}
