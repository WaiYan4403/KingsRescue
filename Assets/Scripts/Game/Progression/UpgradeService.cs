using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.Progression
{
    /// <summary>
    /// Picks the cards shown on a level-up. Three cards by default; a fourth when
    /// Luck is high enough. Draws without replacement from the weighted pool,
    /// filtered by what the hero can still take.
    /// </summary>
    public class UpgradeService : SingletonBehaviour<UpgradeService>
    {
        [SerializeField] private List<UpgradeDefinition> pool = new List<UpgradeDefinition>();

        [Tooltip("Shown when fewer than the required number of real upgrades qualify. Should be a CoinBonus.")]
        [SerializeField] private UpgradeDefinition fallback;

        [SerializeField] private int choiceCount = 4;

        [Tooltip("Luck at or above this reveals a fourth level-up card.")]
        [SerializeField] private float fourthCardLuck = 1.2f;

        [Header("Scene References")]
        [SerializeField] private WeaponLoadout loadout;
        [SerializeField] private PlayerStats stats;

        private readonly List<UpgradeDefinition> candidates = new List<UpgradeDefinition>(32);

        public int ChoiceCount => choiceCount;

        public bool FourthCardUnlocked
        {
            get
            {
                float need = fourthCardLuck > 0.01f ? fourthCardLuck : 1.2f;
                return stats != null && stats.Luck >= need;
            }
        }

        public int ActiveChoiceCount => FourthCardUnlocked
            ? Mathf.Max(1, choiceCount)
            : Mathf.Min(3, Mathf.Max(1, choiceCount));

        public WeaponLoadout Loadout => loadout;

        protected override void Awake()
        {
            base.Awake();

            if (loadout == null)
            {
                loadout = GameManager.Instance != null ? GameManager.Instance.Loadout : null;
            }

            if (stats == null)
            {
                stats = GameManager.Instance != null ? GameManager.Instance.PlayerStats : null;
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

            int want = ActiveChoiceCount;
            while (results.Count < want && candidates.Count > 0)
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
            while (results.Count < want && fallback != null)
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

        /// <summary>
        /// Picks one random legal weapon or passive without applying it.
        /// Returns false when the loadout has nothing left to unlock.
        /// </summary>
        public bool TryPickRandomItem(out UpgradeDefinition picked)
        {
            picked = null;
            if (loadout == null || stats == null)
            {
                return false;
            }

            candidates.Clear();
            for (int i = 0; i < pool.Count; i++)
            {
                UpgradeDefinition entry = pool[i];
                if (entry == null || entry == fallback || entry.kind == UpgradeKind.CoinBonus)
                {
                    continue;
                }

                if (entry.IsAvailable(loadout, stats))
                {
                    candidates.Add(entry);
                }
            }

            picked = DrawWeighted();
            return picked != null;
        }

        /// <summary>
        /// Grants one random legal weapon or passive. Used by lucky chest drops.
        /// Returns false when the loadout has nothing left to unlock.
        /// </summary>
        public bool TryGrantRandomItem(out UpgradeDefinition granted)
        {
            if (!TryPickRandomItem(out granted))
            {
                return false;
            }

            granted.Apply(loadout, stats);
            return true;
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
