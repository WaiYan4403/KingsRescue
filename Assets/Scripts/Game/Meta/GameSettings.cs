using UnityEngine;

namespace KingsRescue.Meta
{
    public static class GameSettings
    {
        private const string VolumeKey = "KingsRescue.Volume";

        public static float Volume
        {
            get => PlayerPrefs.GetFloat(VolumeKey, 1f);
            set
            {
                float clamped = Mathf.Clamp01(value);
                PlayerPrefs.SetFloat(VolumeKey, clamped);
                PlayerPrefs.Save();
                Apply();
            }
        }

        public static void Apply()
        {
            AudioListener.volume = Volume;
        }
    }
}
