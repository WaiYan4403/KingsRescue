using UnityEngine;

namespace KingsRescue.Player
{
    /// <summary>
    /// Temporary combat statuses on the hero (slow from webs).
    /// </summary>
    public class PlayerStatus : MonoBehaviour
    {
        private float slowUntil;
        private float slowFactor = 1f;

        /// <summary>Multiply move speed by this. 1 when nothing is applied.</summary>
        public float MoveScale => Time.time < slowUntil ? slowFactor : 1f;

        public bool IsSlowed => Time.time < slowUntil;

        public void ApplySlow(float factor, float duration)
        {
            slowFactor = Mathf.Clamp(factor, 0.1f, 1f);
            slowUntil = Mathf.Max(slowUntil, Time.time + Mathf.Max(0.05f, duration));
        }
    }
}
