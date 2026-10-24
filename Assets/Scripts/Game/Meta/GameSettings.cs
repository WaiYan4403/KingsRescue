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

        public const float MinJoystickSensitivity = 0.5f;
        public const float MaxJoystickSensitivity = 2f;

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

                return PlayerPrefs.GetFloat(LegacyVolumeKey, 1f);
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
            get => PlayerPrefs.GetFloat(SoundKey, 1f);
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
        /// Joystick responsiveness. 1 is the default feel. Higher values reach full speed with a shorter
        /// thumb drag, lower values need a longer drag.
        /// </summary>
        public static float JoystickSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SensitivityKey, 1f), MinJoystickSensitivity, MaxJoystickSensitivity);
            set
            {
                PlayerPrefs.SetFloat(SensitivityKey, Mathf.Clamp(value, MinJoystickSensitivity, MaxJoystickSensitivity));
                PlayerPrefs.Save();
            }
        }

        public static void Apply()
        {
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
