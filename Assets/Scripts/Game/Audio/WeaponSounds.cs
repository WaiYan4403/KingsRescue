using KingsRescue.Data;

namespace KingsRescue.Audio
{
    /// <summary>
    /// Maps a <see cref="WeaponSound"/> onto the weapon slots that start at id 146.
    /// </summary>
    public static class WeaponSounds
    {
        public const int FirstId = 146;

        public static SoundId Resolve(WeaponSound sound)
        {
            if (sound == WeaponSound.None || sound == WeaponSound.Lance)
            {
                return SoundId.None;
            }

            return (SoundId)(FirstId + (int)sound - 1);
        }
    }
}
