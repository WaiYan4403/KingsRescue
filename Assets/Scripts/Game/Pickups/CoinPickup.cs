using UnityEngine;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// Coins come from smashing scenery rather than from kills, which is what makes
    /// detouring into a pot or a chest worth the risk.
    /// </summary>
    public class CoinPickup : Pickup
    {
        [SerializeField] private int value = 1;

        public void SetValue(int amount)
        {
            value = Mathf.Max(1, amount);
        }

        protected override void OnCollected(PlayerController hero)
        {
            var stats = hero.GetComponent<PlayerStats>();
            float multiplier = stats != null ? stats.CoinGain : 1f;

            GameManager.Instance?.AddCoins(Mathf.Max(1, Mathf.RoundToInt(value * multiplier)));
        }
    }
}
