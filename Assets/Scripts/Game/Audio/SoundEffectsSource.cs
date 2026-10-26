using UnityEngine;

namespace KingsRescue.Audio
{
    /// <summary>
    /// Scene hook for <see cref="SoundBank"/>. Select this object to assign clips.
    /// The same bank is shared with gameplay.
    /// </summary>
    [AddComponentMenu("King's Rescue/Sound Effects")]
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class SoundEffectsSource : MonoBehaviour
    {
        [SerializeField] private SoundBank bank;

        public SoundBank Bank => bank;

        private void Awake()
        {
            if (bank != null)
            {
                SoundEffects.Use(bank);
            }
        }
    }
}
