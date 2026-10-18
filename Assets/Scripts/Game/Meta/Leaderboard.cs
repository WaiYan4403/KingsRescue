using System;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Data;

namespace KingsRescue.Meta
{
    [Serializable]
    public class LeaderboardEntry
    {
        public string character;
        public float time;
        public int kills;
        public int coins;
        public int level;
    }

    public static class Leaderboard
    {
        private const string SaveKey = "KingsRescue.Leaderboard";
        private const int MaxEntries = 10;

        [Serializable]
        private class SaveData
        {
            public LeaderboardEntry[] entries;
        }

        public static IReadOnlyList<LeaderboardEntry> Entries
        {
            get
            {
                EnsureLoaded();
                return cache;
            }
        }

        private static List<LeaderboardEntry> cache;

        public static event System.Action Changed;

        /// <summary>Snapshot used by the cloud save.</summary>
        public static string ExportJson()
        {
            EnsureLoaded();
            return JsonUtility.ToJson(new SaveData { entries = cache.ToArray() });
        }

        /// <summary>Replaces the local run history with a cloud snapshot.</summary>
        public static void ImportJson(string json)
        {
            SaveData data = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);
            if (data == null)
            {
                return;
            }

            cache = new List<LeaderboardEntry>(MaxEntries);
            if (data.entries != null)
            {
                cache.AddRange(data.entries);
            }

            cache.Sort(Compare);
            if (cache.Count > MaxEntries)
            {
                cache.RemoveRange(MaxEntries, cache.Count - MaxEntries);
            }

            Save();
        }

        /// <summary>Forgets the local run history (account sign-out).</summary>
        public static void ClearLocal()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            cache = null;
            Changed?.Invoke();
        }

        private static void Save()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData { entries = cache.ToArray() }));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        public static void Submit(CharacterDefinition character, float time, int kills, int coins, int level)
        {
            EnsureLoaded();

            cache.Add(new LeaderboardEntry
            {
                character = character != null ? character.displayName : "Hero",
                time = time,
                kills = kills,
                coins = coins,
                level = level
            });

            cache.Sort(Compare);
            if (cache.Count > MaxEntries)
            {
                cache.RemoveRange(MaxEntries, cache.Count - MaxEntries);
            }

            Save();
        }

        private static int Compare(LeaderboardEntry a, LeaderboardEntry b)
        {
            int time = b.time.CompareTo(a.time);
            if (time != 0)
            {
                return time;
            }

            return b.kills.CompareTo(a.kills);
        }

        private static void EnsureLoaded()
        {
            if (cache != null)
            {
                return;
            }

            cache = new List<LeaderboardEntry>(MaxEntries);
            if (!PlayerPrefs.HasKey(SaveKey))
            {
                return;
            }

            SaveData data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            if (data?.entries == null)
            {
                return;
            }

            cache.AddRange(data.entries);
            cache.Sort(Compare);
        }
    }
}
