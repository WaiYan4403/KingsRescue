using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// EXP gem. Three denominations exist: small, medium and large.
    /// </summary>
    public class XpGem : Pickup
    {
        public const int SmallValue = 1;
        public const int MediumValue = 5;
        public const int LargeValue = 10;
        public const int MaxGemsPerDrop = 8;

        [SerializeField] private int value = SmallValue;

        [Tooltip("Brightest last. The gem uses the highest tier its value reaches.")]
        [SerializeField] private Tier[] tiers;

        [SerializeField] private SpriteRenderer spriteRenderer;

        [System.Serializable]
        private struct Tier
        {
            public int minValue;
            public Sprite sprite;
            public Color color;
            public float scale;
        }

        private static readonly int[] Denominations = { LargeValue, MediumValue, SmallValue };

        private static readonly Tier[] DefaultTiers =
        {
            new Tier { minValue = SmallValue, color = Color.white, scale = 1f },
            new Tier { minValue = MediumValue, color = Color.white, scale = 1f },
            new Tier { minValue = LargeValue, color = Color.white, scale = 1f }
        };

        private Vector3 baseScale = Vector3.one;

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

        /// <summary>
        /// Breaks a kill's EXP into the three gem sizes, largest first.
        /// </summary>
        public static void Split(int totalXp, List<int> dest)
        {
            dest.Clear();
            int remaining = Mathf.Max(0, totalXp);

            for (int i = 0; i < Denominations.Length; i++)
            {
                int amount = Denominations[i];
                while (remaining >= amount && dest.Count < MaxGemsPerDrop)
                {
                    dest.Add(amount);
                    remaining -= amount;
                }
            }
        }

        protected override SoundId CollectSound => SoundId.PickupGem;

        protected override void OnCollected(PlayerController hero)
        {
            ExperienceSystem.Instance?.AddXp(value);
        }

        private void ApplyTier()
        {
            Tier[] source = tiers != null && tiers.Length > 0 ? tiers : DefaultTiers;
            Tier chosen = source[0];
            int best = int.MinValue;

            for (int i = 0; i < source.Length; i++)
            {
                int min = source[i].minValue;
                if (value >= min && min >= best)
                {
                    best = min;
                    chosen = source[i];
                }
            }

            if (spriteRenderer != null)
            {
                if (chosen.sprite != null)
                {
                    spriteRenderer.sprite = chosen.sprite;
                }

                spriteRenderer.color = chosen.color.a > 0.01f ? chosen.color : Color.white;
            }

            float scale = chosen.scale <= 0f ? 1f : chosen.scale;
            transform.localScale = baseScale * scale;
        }
    }
}
