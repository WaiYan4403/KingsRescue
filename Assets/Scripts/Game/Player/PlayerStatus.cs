using UnityEngine;
using KingsRescue.Combat;

namespace KingsRescue.Player
{
    /// <summary>
    /// Temporary combat statuses on the hero (slow from webs, poison from beasts).
    /// </summary>
    public class PlayerStatus : MonoBehaviour
    {
        private float slowUntil;
        private float slowFactor = 1f;
        private float poisonUntil;
        private float poisonDps;
        private float poisonTick;

        private PlayerHealth health;

        /// <summary>Multiply move speed by this. 1 when nothing is applied.</summary>
        public float MoveScale => Time.time < slowUntil ? slowFactor : 1f;

        public bool IsSlowed => Time.time < slowUntil;

        private void Awake()
        {
            health = GetComponent<PlayerHealth>();
        }

        private void Update()
        {
            if (Time.time >= poisonUntil || health == null || !health.IsAlive)
            {
                return;
            }

            if (Time.time < poisonTick)
            {
                return;
            }

            poisonTick = Time.time + 0.45f;
            health.ApplyDot(Mathf.Max(0.5f, poisonDps * 0.45f));
        }

        public void ApplySlow(float factor, float duration)
        {
            slowFactor = Mathf.Clamp(factor, 0.1f, 1f);
            slowUntil = Mathf.Max(slowUntil, Time.time + Mathf.Max(0.05f, duration));
        }

        public void ApplyPoison(float dps, float duration)
        {
            poisonDps = Mathf.Max(poisonDps, dps);
            poisonUntil = Mathf.Max(poisonUntil, Time.time + Mathf.Max(0.2f, duration));
            if (Time.time >= poisonTick)
            {
                poisonTick = Time.time + 0.2f;
            }
        }
    }
}
