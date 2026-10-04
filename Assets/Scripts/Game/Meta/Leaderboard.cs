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

            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(new SaveData { entries = cache.ToArray() }));
            PlayerPrefs.Save();
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
