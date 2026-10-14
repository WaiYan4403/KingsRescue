using System;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Progression
{
    /// <summary>
    /// EXP and levels. Multiple level-ups from one big gem are queued so the player
    /// gets a card for each.
    /// </summary>
    public class ExperienceSystem : SingletonBehaviour<ExperienceSystem>
    {
        [SerializeField] private PlayerStats stats;

        [Header("Curve: xpToNext = base + growth * level^exponent")]
        [SerializeField] private float baseXp = 5f;
        [SerializeField] private float growth = 2.4f;
        [SerializeField] private float exponent = 1.35f;

        public int Level { get; private set; } = 1;

        public float CurrentXp { get; private set; }

        public float XpToNext { get; private set; }

        public float Normalised => XpToNext > 0f ? Mathf.Clamp01(CurrentXp / XpToNext) : 0f;

        /// <summary>Current xp, xp needed for the next level.</summary>
        public event Action<float, float> XpChanged;

        /// <summary>Fired once per level gained, in order.</summary>
        public event Action<int> LeveledUp;

        protected override void Awake()
        {
            base.Awake();

            if (stats == null)
            {
                stats = GameManager.Instance != null ? GameManager.Instance.PlayerStats : null;
            }

            XpToNext = ComputeRequirement(Level);
        }

        private void Start()
        {
            XpChanged?.Invoke(CurrentXp, XpToNext);
        }

        public void AddXp(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float gain = amount * (stats != null ? stats.XpGain : 1f);
            CurrentXp += gain;

            GameManager game = GameManager.Instance;
            bool offerCards = game == null || game.CanOfferLevelUp;

            // A single fat gem can carry the player through several levels at once.
            while (CurrentXp >= XpToNext)
            {
                CurrentXp -= XpToNext;
                Level++;
                XpToNext = ComputeRequirement(Level);
                if (offerCards)
                {
                    LeveledUp?.Invoke(Level);
                }
            }

            XpChanged?.Invoke(CurrentXp, XpToNext);
        }

        private float ComputeRequirement(int level)
        {
            return Mathf.Round(baseXp + growth * Mathf.Pow(level, exponent));
        }
    }
}
