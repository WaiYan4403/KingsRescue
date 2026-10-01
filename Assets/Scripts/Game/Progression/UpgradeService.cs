using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Progression
{
    /// <summary>
    /// Picks the four cards shown on a level-up. Draws without replacement from the
    /// weighted pool, filtered by what the hero can still take.
    /// </summary>
    public class UpgradeService : SingletonBehaviour<UpgradeService>
    {
        [SerializeField] private List<UpgradeDefinition> pool = new List<UpgradeDefinition>();

        [Tooltip("Shown when fewer than the required number of real upgrades qualify. Should be a CoinBonus.")]
        [SerializeField] private UpgradeDefinition fallback;

        [SerializeField] private int choiceCount = 4;

        [Header("Scene References")]
        [SerializeField] private WeaponLoadout loadout;
        [SerializeField] private PlayerStats stats;

        private readonly List<UpgradeDefinition> candidates = new List<UpgradeDefinition>(32);

        public int ChoiceCount => choiceCount;

        public WeaponLoadout Loadout => loadout;

        protected override void Awake()
        {
            base.Awake();

            if (loadout == null)
            {
                loadout = FindAnyObjectByType<WeaponLoadout>();
            }

            if (stats == null)
            {
                stats = FindAnyObjectByType<PlayerStats>();
            }
        }

        /// <summary>Fills <paramref name="results"/> with up to <see cref="ChoiceCount"/> distinct cards.</summary>
        public void BuildChoices(List<UpgradeDefinition> results)
        {
            results.Clear();

            if (loadout == null || stats == null)
            {
                Debug.LogError("UpgradeService is missing the player's WeaponLoadout or PlayerStats.", this);
                return;
            }

            candidates.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                UpgradeDefinition entry = pool[i];
                if (entry != null && entry != fallback && entry.IsAvailable(loadout, stats))
                {
                    candidates.Add(entry);
                }
            }

            while (results.Count < choiceCount && candidates.Count > 0)
            {
                UpgradeDefinition picked = DrawWeighted();
                if (picked == null)
                {
                    break;
                }

                candidates.Remove(picked);
                results.Add(picked);
            }

            // The hero can run out of legal upgrades late in a run; never show an empty screen.
            while (results.Count < choiceCount && fallback != null)
            {
                results.Add(fallback);
            }
        }

        public void Apply(UpgradeDefinition upgrade)
        {
            if (upgrade == null || loadout == null || stats == null)
            {
                return;
            }

            upgrade.Apply(loadout, stats);
        }

        private UpgradeDefinition DrawWeighted()
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                total += Mathf.Max(0f, candidates[i].weight);
            }

            if (total <= 0f)
            {
                return null;
            }

            float roll = Random.value * total;
            for (int i = 0; i < candidates.Count; i++)
            {
                roll -= Mathf.Max(0f, candidates[i].weight);
                if (roll <= 0f)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }
    }
}
