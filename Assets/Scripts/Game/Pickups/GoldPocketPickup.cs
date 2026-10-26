using UnityEngine;
using KingsRescue.Audio;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.Pickups
{
    /// <summary>
    /// A purse lying on the road. It drifts, pops, and flies to the hero the same way coins and hearts do.
    /// </summary>
    public class GoldPocketPickup : Pickup
    {
        [SerializeField] private int minCoins = 10;
        [SerializeField] private int maxCoins = 100;
        [SerializeField] private float popBurst = 1.6f;

        private int payout;

        public override void OnSpawned()
        {
            base.OnSpawned();
            int min = Mathf.Max(1, minCoins);
            int max = Mathf.Max(min, maxCoins);
            payout = Random.Range(min, max + 1);
            Pop(Random.insideUnitCircle * popBurst);
        }

        protected override SoundId CollectSound => SoundId.PickupPurse;

        protected override void OnCollected(PlayerController hero)
        {
            PlayerStats stats = hero.GetComponent<PlayerStats>();
            float multiplier = stats != null ? stats.CoinGain : 1f;
            GameManager.Instance?.RegisterPocket();
            GameManager.Instance?.AddCoins(Mathf.Max(1, Mathf.RoundToInt(payout * multiplier)));
        }
    }
}
