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

        public static void AddCoins(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            EnsureLoaded();
            data.coins = Mathf.Max(0, data.coins + amount);
            Persist();
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
