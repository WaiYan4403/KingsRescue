using System;
using UnityEngine;
using KingsRescue.Data;

namespace KingsRescue.Meta
{
    /// <summary>
    /// Persistent achievement counters, character unlocks, and gated weapons.
    /// </summary>
    public static class AchievementProgress
    {
        private const string SaveKey = "KingsRescue.Achievements";

        [Serializable]
        private class EnemyKill
        {
            public string id;
            public int count;
        }

        [Serializable]
        private class SaveData
        {
            public string[] unlockedCharacters;
            public string[] unlockedWeapons;
            public string[] completedIds;
            public int totalKills;
            public int chestsSmashed;
            public int powerUpsCollected;
            public int lifetimeCoins;
            public float bestSurviveSeconds;
            public EnemyKill[] enemyKills;
        }

        private static SaveData data;

        public static event Action Changed;

        public static void EnsureLoaded()
        {
            if (data != null)
            {
                return;
            }

            if (PlayerPrefs.HasKey(SaveKey))
            {
                data = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey));
            }

            if (data == null)
            {
                data = NewSave();
            }

            Normalize(data);
            SeedDefaultUnlocks();
            SweepCompletions();
            Persist();
        }

        public static bool IsCharacterUnlocked(CharacterDefinition character)
        {
            if (character == null)
            {
                return false;
            }

            EnsureLoaded();
            if (character.unlockedByDefault)
            {
                return true;
            }

            return Contains(data.unlockedCharacters, character.Id);
        }

        public static bool IsWeaponUnlocked(WeaponDefinition weapon)
        {
            if (weapon == null)
            {
                return true;
            }

            if (!weapon.requiresUnlock)
            {
                return true;
            }

            EnsureLoaded();
            return Contains(data.unlockedWeapons, weapon.name);
        }

        public static bool IsCompleted(AchievementDefinition achievement)
        {
            if (achievement == null)
            {
                return false;
            }

            EnsureLoaded();
            return Contains(data.completedIds, achievement.Id);
        }

        public static int GetProgress(AchievementDefinition achievement)
        {
            if (achievement == null)
            {
                return 0;
            }

            EnsureLoaded();
            switch (achievement.kind)
            {
                case AchievementKind.KillAny:
                    return data.totalKills;
                case AchievementKind.KillEnemy:
                    return CountEnemy(achievement.enemy);
                case AchievementKind.PayCoins:
                    return IsCompleted(achievement) ? achievement.target : 0;
                case AchievementKind.SurviveSeconds:
                    return Mathf.FloorToInt(data.bestSurviveSeconds);
                case AchievementKind.SmashChests:
                    return data.chestsSmashed;
                case AchievementKind.CollectPowerUps:
                    return data.powerUpsCollected;
                case AchievementKind.EarnCoins:
                    return data.lifetimeCoins;
                default:
                    return 0;
            }
        }

        public static bool TryPurchase(AchievementDefinition achievement)
        {
            if (achievement == null || achievement.kind != AchievementKind.PayCoins)
            {
                return false;
            }

            EnsureLoaded();
            if (IsCompleted(achievement))
            {
                return false;
            }

            int cost = Mathf.Max(1, achievement.target);
            if (MetaProgress.Coins < cost)
            {
                return false;
            }

            MetaProgress.AddCoins(-cost);
            Complete(achievement);
            return true;
        }

        public static void RecordKill(EnemyDefinition enemy)
        {
            EnsureLoaded();
            data.totalKills++;
            if (enemy != null)
            {
                AddEnemyKill(enemy.name, 1);
            }

            SweepCompletions();
            Persist();
        }

        public static void RecordChest()
        {
            EnsureLoaded();
            data.chestsSmashed++;
            SweepCompletions();
            Persist();
        }

        public static void RecordPowerUp()
        {
            EnsureLoaded();
            data.powerUpsCollected++;
            SweepCompletions();
            Persist();
        }

        public static void RecordCoinsEarned(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            EnsureLoaded();
            data.lifetimeCoins += amount;
            SweepCompletions();
            Persist();
        }

        public static void RecordSurvive(float seconds)
        {
            EnsureLoaded();
            if (seconds > data.bestSurviveSeconds)
            {
                data.bestSurviveSeconds = seconds;
            }

            SweepCompletions();
            Persist();
        }

        private static void SweepCompletions()
        {
            AchievementDefinition[] list = AchievementCatalog.All;
            for (int i = 0; i < list.Length; i++)
            {
                AchievementDefinition entry = list[i];
                if (entry == null || entry.kind == AchievementKind.PayCoins || IsCompleted(entry))
                {
                    continue;
                }

                if (GetProgress(entry) >= Mathf.Max(1, entry.target))
                {
                    Complete(entry);
                }
            }
        }

        private static void Complete(AchievementDefinition achievement)
        {
            if (achievement == null || IsCompleted(achievement))
            {
                return;
            }

            data.completedIds = Append(data.completedIds, achievement.Id);

            if (achievement.rewardCoins > 0)
            {
                MetaProgress.AddCoins(achievement.rewardCoins);
            }

            if (achievement.rewardCharacter != null)
            {
                data.unlockedCharacters = Append(data.unlockedCharacters, achievement.rewardCharacter.Id);
            }

            if (achievement.rewardWeapon != null)
            {
                data.unlockedWeapons = Append(data.unlockedWeapons, achievement.rewardWeapon.name);
            }

            Persist();
        }

        private static void SeedDefaultUnlocks()
        {
            CharacterRoster roster = Resources.Load<CharacterRoster>("CharacterRoster");
            if (roster == null || roster.characters == null)
            {
                return;
            }

            for (int i = 0; i < roster.characters.Length; i++)
            {
                CharacterDefinition character = roster.characters[i];
                if (character != null && character.unlockedByDefault)
                {
                    data.unlockedCharacters = Append(data.unlockedCharacters, character.Id);
                }
            }
        }

        private static int CountEnemy(EnemyDefinition enemy)
        {
            if (enemy == null || data.enemyKills == null)
            {
                return 0;
            }

            string id = enemy.name;
            for (int i = 0; i < data.enemyKills.Length; i++)
            {
                if (data.enemyKills[i] != null && data.enemyKills[i].id == id)
                {
                    return data.enemyKills[i].count;
                }
            }

            return 0;
        }

        private static void AddEnemyKill(string id, int amount)
        {
            if (string.IsNullOrEmpty(id) || data.enemyKills == null)
            {
                return;
            }

            for (int i = 0; i < data.enemyKills.Length; i++)
            {
                if (data.enemyKills[i] != null && data.enemyKills[i].id == id)
                {
                    data.enemyKills[i].count += amount;
                    return;
                }
            }

            var expanded = new EnemyKill[data.enemyKills.Length + 1];
            Array.Copy(data.enemyKills, expanded, data.enemyKills.Length);
            expanded[expanded.Length - 1] = new EnemyKill { id = id, count = amount };
            data.enemyKills = expanded;
        }

        private static SaveData NewSave()
        {
            return new SaveData
            {
                unlockedCharacters = Array.Empty<string>(),
                unlockedWeapons = Array.Empty<string>(),
                completedIds = Array.Empty<string>(),
                enemyKills = Array.Empty<EnemyKill>()
            };
        }

        private static void Normalize(SaveData save)
        {
            save.unlockedCharacters ??= Array.Empty<string>();
            save.unlockedWeapons ??= Array.Empty<string>();
            save.completedIds ??= Array.Empty<string>();
            save.enemyKills ??= Array.Empty<EnemyKill>();
        }

        private static bool Contains(string[] list, string id)
        {
            if (list == null || string.IsNullOrEmpty(id))
            {
                return false;
            }

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == id)
                {
                    return true;
                }
            }

            return false;
        }

        private static string[] Append(string[] list, string id)
        {
            if (string.IsNullOrEmpty(id) || Contains(list, id))
            {
                return list ?? Array.Empty<string>();
            }

            var expanded = new string[list.Length + 1];
            Array.Copy(list, expanded, list.Length);
            expanded[expanded.Length - 1] = id;
            return expanded;
        }

        private static void Persist()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
