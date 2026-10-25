using System;
using UnityEngine;

namespace KingsRescue.Audio
{
    /// <summary>
    /// Unity audio clips for gameplay and UI. Music stays on FMOD.
    /// Leave a clip empty to keep that moment silent.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Sound Bank", fileName = "SoundBank")]
    public class SoundBank : ScriptableObject
    {
        public SoundSlot[] slots = Array.Empty<SoundSlot>();

        public bool TryGet(SoundId id, out SoundSlot slot)
        {
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    if (slots[i] != null && slots[i].id == id)
                    {
                        slot = slots[i];
                        return true;
                    }
                }
            }

            slot = null;
            return false;
        }
    }

    [Serializable]
    public class SoundSlot
    {
        public SoundId id;
        public AudioClip clip;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Random pitch range. Both at 1 plays the clip as recorded.")]
        public float pitchMin = 1f;
        public float pitchMax = 1f;

        [Tooltip("Seconds before this slot can play again. Stops hit and footstep clips from stacking.")]
        public float minInterval;
    }
}
