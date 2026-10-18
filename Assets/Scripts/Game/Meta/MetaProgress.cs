using System;
using UnityEngine;
using KingsRescue.Data;

namespace KingsRescue.Meta
{
    /// <summary>
    /// Persistent coins and per-character attribute levels. Stored in PlayerPrefs.
    /// </summary>
    public static class MetaProgress
    {
        private const string SaveKey = "KingsRescue.Meta";

        [Serializable]
        private class CharacterSave
        {
            public string id;
            public int[] levels;
        }

        [Serializable]
        private class SaveData
        {
            public int coins;
            public CharacterSave[] characters;
        }

        private static SaveData data;

        public static int Coins
        {
            get
            {
                EnsureLoaded();
                return data.coins;
            }
        }

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
                data = new SaveData { coins = 100, characters = Array.Empty<CharacterSave>() };
                Persist();
            }

            if (data.characters == null)
            {
                data.characters = Array.Empty<CharacterSave>();
            }
        }

        /// <summary>Snapshot used by the cloud save.</summary>
        public static string ExportJson()
        {
            EnsureLoaded();
            return JsonUtility.ToJson(data);
        }

        /// <summary>Replaces the local save with a cloud snapshot. Ignores empty or unreadable input.</summary>
        public static void ImportJson(string json)
        {
            SaveData parsed = string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveData>(json);
            if (parsed == null)
            {
                return;
            }

            parsed.coins = Mathf.Max(0, parsed.coins);
            parsed.characters ??= Array.Empty<CharacterSave>();
            data = parsed;
            Persist();
        }

        /// <summary>Forgets the local save (account sign-out). The next read starts from defaults.</summary>
        public static void ClearLocal()
        {
            PlayerPrefs.DeleteKey(SaveKey);
            data = null;
            Changed?.Invoke();
        }

        public static void AddCoins(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            EnsureLoaded();
            data.coins = Mathf.Max(0, data.coins + amount);
            Persist();
            if (amount > 0)
            {
                AchievementProgress.RecordCoinsEarned(amount);
            }
        }

        public static int GetLevel(CharacterDefinition character, StatType stat)
        {
            CharacterSave save = Find(character, false);
            if (save == null || save.levels == null)
            {
                return 0;
            }

            int index = (int)stat;
            return index >= 0 && index < save.levels.Length ? Mathf.Max(0, save.levels[index]) : 0;
        }

        public static bool TryPurchase(CharacterDefinition character, StatType stat)
        {
            if (character == null || !MetaUpgradeCatalog.TryGet(stat, out MetaUpgradeOffer offer))
            {
                return false;
            }

            EnsureLoaded();
            int current = GetLevel(character, stat);
            if (current >= offer.maxLevel)
            {
                return false;
            }

            int cost = offer.CostForNext(current);
            if (data.coins < cost)
            {
                return false;
            }

            data.coins -= cost;
            CharacterSave save = Find(character, true);
            save.levels[(int)stat] = current + 1;
            Persist();
            return true;
        }

        /// <summary>Coins this hero has spent on meta upgrades.</summary>
        public static int SpentOnCharacter(CharacterDefinition character)
        {
            if (character == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < MetaUpgradeCatalog.Offers.Length; i++)
            {
                MetaUpgradeOffer offer = MetaUpgradeCatalog.Offers[i];
                total += offer.SpentForLevel(GetLevel(character, offer.stat));
            }

            return total;
        }

        /// <summary>
        /// Clears this hero's upgrade levels and refunds every coin spent on them.
        /// Other characters are left alone.
        /// </summary>
        public static int ResetCharacter(CharacterDefinition character)
        {
            if (character == null)
            {
                return 0;
            }

            EnsureLoaded();
            int refund = SpentOnCharacter(character);
            CharacterSave save = Find(character, false);
            if (save != null && save.levels != null)
            {
                for (int i = 0; i < save.levels.Length; i++)
                {
                    save.levels[i] = 0;
                }
            }

            if (refund <= 0)
            {
                Persist();
                return 0;
            }

            data.coins = Mathf.Max(0, data.coins + refund);
            Persist();
            return refund;
        }

        private static CharacterSave Find(CharacterDefinition character, bool create)
        {
            EnsureLoaded();
            if (character == null)
            {
                return null;
            }

            string id = character.Id;
            for (int i = 0; i < data.characters.Length; i++)
            {
                if (data.characters[i] != null && data.characters[i].id == id)
                {
                    EnsureLevels(data.characters[i]);
                    return data.characters[i];
                }
            }

            if (!create)
            {
                return null;
            }

            var next = new CharacterSave { id = id, levels = new int[StatTypes.Count] };
            var expanded = new CharacterSave[data.characters.Length + 1];
            Array.Copy(data.characters, expanded, data.characters.Length);
            expanded[expanded.Length - 1] = next;
            data.characters = expanded;
            return next;
        }

        private static void EnsureLevels(CharacterSave save)
        {
            if (save.levels != null && save.levels.Length >= StatTypes.Count)
            {
                return;
            }

            var levels = new int[StatTypes.Count];
            if (save.levels != null)
            {
                Array.Copy(save.levels, levels, Mathf.Min(save.levels.Length, levels.Length));
            }

            save.levels = levels;
        }

        private static void Persist()
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
