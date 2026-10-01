using System;
using UnityEngine;

namespace KingsRescue.Data
{
    /// <summary>
    /// A passive item. Each level hands the player a bundle of stat modifiers.
    /// </summary>
    [CreateAssetMenu(menuName = "King's Rescue/Passive Definition", fileName = "Passive_")]
    public class PassiveDefinition : ScriptableObject
    {
        [Serializable]
        public class PassiveLevel
        {
            [Tooltip("Optional override for the card text. Left empty, the modifiers describe themselves.")]
            [TextArea(1, 3)] public string upgradeText = "";

            public StatModifier[] modifiers = Array.Empty<StatModifier>();
        }

        [Header("Presentation")]
        public string displayName = "Passive";
        [TextArea(2, 4)] public string description = "";
        public Sprite icon;

        [Header("Levels (index 0 is level 1)")]
        public PassiveLevel[] levels = { new PassiveLevel() };

        public int MaxLevel => levels != null && levels.Length > 0 ? levels.Length : 1;

        public PassiveLevel GetLevel(int level)
        {
            if (levels == null || levels.Length == 0)
            {
                return new PassiveLevel();
            }

            int index = Mathf.Clamp(level - 1, 0, levels.Length - 1);
            return levels[index];
        }

        /// <summary>Card text for taking this passive to the given level.</summary>
        public string DescribeLevel(int level)
        {
            PassiveLevel data = GetLevel(level);

            if (!string.IsNullOrWhiteSpace(data.upgradeText))
            {
                return data.upgradeText;
            }

            if (data.modifiers == null || data.modifiers.Length == 0)
            {
                return description;
            }

            var builder = new System.Text.StringBuilder();
            for (int i = 0; i < data.modifiers.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append('\n');
                }

                builder.Append(data.modifiers[i].Describe());
            }

            return builder.ToString();
        }
    }
}
