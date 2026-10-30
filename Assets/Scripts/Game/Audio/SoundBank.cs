using System;
using System.Collections.Generic;
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

        /// <summary>Adds a slot for every sound id that is not already in the bank. Existing clips stay.</summary>
        public bool EnsureSlots()
        {
            var present = new HashSet<SoundId>();
            var list = new List<SoundSlot>();
            if (slots != null)
            {
                for (int i = 0; i < slots.Length; i++)
                {
                    SoundSlot slot = slots[i];
                    if (slot == null || !Enum.IsDefined(typeof(SoundId), slot.id) || slot.id == SoundId.None || !present.Add(slot.id))
                    {
                        continue;
                    }

                    list.Add(slot);
                }
            }

            bool changed = slots == null || list.Count != slots.Length;
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                if (id == SoundId.None || present.Contains(id))
                {
                    continue;
                }

                list.Add(CreateSlot(id));
                changed = true;
            }

            if (changed)
            {
                slots = list.ToArray();
            }

            return changed;
        }

        private void OnValidate()
        {
            EnsureSlots();
        }

        static SoundSlot CreateSlot(SoundId id)
        {
            string name = id.ToString();
            if (id == SoundId.Typewriter)
            {
                return new SoundSlot
                {
                    id = id,
                    volume = 0.45f,
                    pitchMin = 0.96f,
                    pitchMax = 1.05f,
                    minInterval = 0f
                };
            }

            bool hit = name.EndsWith("Hit");
            bool death = name.EndsWith("Death");
            return new SoundSlot
            {
                id = id,
                volume = hit ? 0.75f : 1f,
                pitchMin = hit || name.EndsWith("Attack") ? 0.94f : 1f,
                pitchMax = hit ? 1.08f : death || name.EndsWith("Attack") ? 1.05f : 1f,
                minInterval = hit ? 0.05f : 0.08f
            };
        }
    }

    [Serializable]
    public class SoundSlot
    {
        public SoundId id;
        public AudioClip clip;

        [Tooltip("Extra takes played after the main clip, in order, then the list repeats.")]
        public AudioClip[] variants;

        [Range(0f, 1f)] public float volume = 1f;

        [Tooltip("Random pitch range. Both at 1 plays the clip as recorded.")]
        public float pitchMin = 1f;
        public float pitchMax = 1f;

        [Tooltip("Seconds before this slot can play again. Stops hit and footstep clips from stacking.")]
        public float minInterval;
    }
}
