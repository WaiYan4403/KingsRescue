using KingsRescue.Data;

namespace KingsRescue.Audio
{
    public enum EnemyCue
    {
        Hit = 0,
        Death = 1,
        Attack = 2
    }

    /// <summary>
    /// Maps an <see cref="EnemyVoice"/> onto the three slots that follow <see cref="SoundId.UiToggle"/>.
    /// Voice 1 starts at id 30, then hit, death, and attack for each voice in enum order.
    /// </summary>
    public static class EnemySounds
    {
        public const int FirstTypedId = 30;

        public static SoundId Resolve(EnemyVoice voice, EnemyCue cue)
        {
            if (voice == EnemyVoice.Generic)
            {
                return SoundId.None;
            }

            int voiceIndex = (int)voice - 1;
            return (SoundId)(FirstTypedId + voiceIndex * 3 + (int)cue);
        }
    }
}
