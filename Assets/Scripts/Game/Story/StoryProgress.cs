using UnityEngine;

namespace KingsRescue.Story
{
    /// <summary>
    /// Remembers that the opening story has been finished or skipped on this device.
    /// </summary>
    public static class StoryProgress
    {
        const string SeenKey = "KingsRescue.StorySeen";

        public static bool HasSeen => PlayerPrefs.GetInt(SeenKey, 0) == 1;

        public static void MarkSeen()
        {
            PlayerPrefs.SetInt(SeenKey, 1);
            PlayerPrefs.Save();
        }
    }
}
