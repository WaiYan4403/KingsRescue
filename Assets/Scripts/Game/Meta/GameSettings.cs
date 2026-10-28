using UnityEngine;

namespace KingsRescue.Meta
{
    /// <summary>
    /// Device settings. These stay in PlayerPrefs and are not part of the cloud save.
    /// </summary>
    public static class GameSettings
    {
        private const string LegacyVolumeKey = "KingsRescue.Volume";
        private const string MusicKey = "KingsRescue.Music";
        private const string SoundKey = "KingsRescue.Sound";
        private const string ShakeKey = "KingsRescue.ScreenShake";
        private const string DamageTextKey = "KingsRescue.DamageText";
        private const string SensitivityKey = "KingsRescue.JoystickSensitivity";
        private const string SensitivityVersionKey = "KingsRescue.JoystickSensitivityVersion";
        private const string DefaultsVersionKey = "KingsRescue.SettingsDefaultsVersion";

        public const float MinJoystickSensitivity = 0.25f;
        public const float MaxJoystickSensitivity = 0.75f;
        public const float DefaultJoystickSensitivity = 0.5f;
        public const float DefaultMusicVolume = 1f;
        public const float DefaultSoundVolume = 0.6f;

        // The FMOD project routes the soundtrack to the master bus. There is no separate music bus.
        private const string MusicBusPath = "bus:/";

        public static float MusicVolume
        {
            get
            {
                if (PlayerPrefs.HasKey(MusicKey))
                {
                    return PlayerPrefs.GetFloat(MusicKey, 1f);
                }

                return PlayerPrefs.GetFloat(LegacyVolumeKey, DefaultMusicVolume);
            }
            set
            {
                PlayerPrefs.SetFloat(MusicKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static float SoundVolume
        {
            get => PlayerPrefs.GetFloat(SoundKey, DefaultSoundVolume);
            set
            {
                PlayerPrefs.SetFloat(SoundKey, Mathf.Clamp01(value));
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ShakeKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(ShakeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool ShowDamageText
        {
            get => PlayerPrefs.GetInt(DamageTextKey, 1) == 1;
            set
            {
                PlayerPrefs.SetInt(DamageTextKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Joystick responsiveness. 0.5 is the middle and matches the old lowest setting.
        /// Higher values reach full speed with a shorter thumb drag, lower values need a longer drag.
        /// </summary>
        public static float JoystickSensitivity
        {
            get => Mathf.Clamp(ReadJoystickSensitivity(), MinJoystickSensitivity, MaxJoystickSensitivity);
            set
            {
                PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinJoystickSensitivity, MaxJoystickSensitivity));
                PlayerPrefs.SetInt(SensitivityVersionKey, 2);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// The first saved range was 0.5 to 2. That old floor is now the middle, and anything above it
        /// is folded into the upper half so a saved slider does not jump to the new ceiling.
        /// </summary>
        private static float ReadJoystickSensitivity()
        {
            const float legacyMin = 0.5f;
            const float legacyMax = 2f;

            float stored = PlayerPrefs.GetFloat(SensitivityKey, DefaultJoystickSensitivity);
            if (PlayerPrefs.GetInt(SensitivityVersionKey, 0) >= 2)
            {
                return stored;
            }

            if (PlayerPrefs.HasKey(SensitivityKey))
            {
                float t = Mathf.InverseLerp(legacyMin, legacyMax, stored);
                stored = Mathf.Lerp(DefaultJoystickSensitivity, MaxJoystickSensitivity, t);
            }
            else
            {
                stored = DefaultJoystickSensitivity;
            }

            PlayerPrefs.SetFloat(SensitivityKey, stored);
            PlayerPrefs.SetInt(SensitivityVersionKey, 2);
            PlayerPrefs.Save();
            return stored;
        }

        /// <summary>
        /// One-time starting point: music full, sound at 0.6, screen shake and damage text on.
        /// Later slider changes stay saved.
        /// </summary>
        public static void EnsureDefaults()
        {
            if (PlayerPrefs.GetInt(DefaultsVersionKey, 0) >= 1)
            {
                return;
            }

            PlayerPrefs.SetFloat(MusicKey, DefaultMusicVolume);
            PlayerPrefs.SetFloat(SoundKey, DefaultSoundVolume);
            PlayerPrefs.SetInt(ShakeKey, 1);
            PlayerPrefs.SetInt(DamageTextKey, 1);
            PlayerPrefs.SetInt(DefaultsVersionKey, 1);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            EnsureDefaults();
            AudioListener.volume = SoundVolume;
            SetBusVolume(MusicBusPath, MusicVolume);
        }

        private static void SetBusVolume(string path, float volume)
        {
            try
            {
                FMOD.Studio.Bus bus = FMODUnity.RuntimeManager.GetBus(path);
                if (bus.isValid())
                {
                    bus.setVolume(Mathf.Clamp01(volume));
                }
            }
            catch (System.Exception)
            {
                // Banks are not loaded yet. The next Apply, from the menu or the run, sets it.
            }
        }
    }
}
