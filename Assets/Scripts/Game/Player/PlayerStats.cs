using System;
using System.Collections.Generic;
using UnityEngine;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.Player
{
    /// <summary>
    /// Aggregates base values with every modifier granted by passives and weapons.
    /// Final value = (base + flat) * (1 + percent).
    /// </summary>
    public class PlayerStats : MonoBehaviour
    {
        [Serializable]
        private struct BaseOverride
        {
            public StatType stat;
            public float value;
        }

        [Tooltip("Optional overrides. Any stat not listed uses StatTypes.DefaultValue.")]
        [SerializeField] private List<BaseOverride> baseOverrides = new List<BaseOverride>();

        private readonly float[] baseValues = new float[StatTypes.Count];
        private readonly float[] flatBonuses = new float[StatTypes.Count];
        private readonly float[] percentBonuses = new float[StatTypes.Count];
        private readonly float[] cached = new float[StatTypes.Count];
        private bool persistentApplied;

        public event Action StatsChanged;

        public float MaxHp => cached[(int)StatType.MaxHp];
        public float MoveSpeed => cached[(int)StatType.MoveSpeed];
        public float DamageMultiplier => cached[(int)StatType.Damage];
        public float AttackSpeed => cached[(int)StatType.AttackSpeed];
        public float AreaMultiplier => cached[(int)StatType.Area];
        public float ProjectileSpeedMultiplier => cached[(int)StatType.ProjectileSpeed];
        public int BonusProjectiles => Mathf.RoundToInt(cached[(int)StatType.ProjectileCount]);
        public int BonusPierce => Mathf.RoundToInt(cached[(int)StatType.Pierce]);
        public float MagnetRadius => cached[(int)StatType.MagnetRadius];
        public float XpGain => cached[(int)StatType.XpGain];
        public float CoinGain => cached[(int)StatType.CoinGain];
        public float Armor => cached[(int)StatType.Armor];
        public float HpRegen => cached[(int)StatType.HpRegen];

        private void Awake()
        {
            for (int i = 0; i < StatTypes.Count; i++)
            {
                baseValues[i] = StatTypes.DefaultValue((StatType)i);
            }

            for (int i = 0; i < baseOverrides.Count; i++)
            {
                baseValues[(int)baseOverrides[i].stat] = baseOverrides[i].value;
            }

            Recalculate(false);
            ApplyPersistent(RunSession.SelectedCharacter);
        }

        public void ApplyPersistent(CharacterDefinition character)
        {
            if (persistentApplied || character == null)
            {
                return;
            }

            persistentApplied = true;
            AddModifiers(MetaUpgradeCatalog.BuildModifiers(character));
        }

        public float Get(StatType stat)
        {
            return cached[(int)stat];
        }

        public float GetBase(StatType stat)
        {
            return baseValues[(int)stat];
        }

        public void AddModifier(StatModifier modifier)
        {
            int index = (int)modifier.stat;
            if (modifier.mode == ModifierMode.Flat)
            {
                flatBonuses[index] += modifier.value;
            }
            else
            {
                percentBonuses[index] += modifier.value;
            }

            Recalculate(true);
        }

        public void AddModifiers(IReadOnlyList<StatModifier> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier m = modifiers[i];
                int index = (int)m.stat;
                if (m.mode == ModifierMode.Flat)
                {
                    flatBonuses[index] += m.value;
                }
                else
                {
                    percentBonuses[index] += m.value;
                }
            }

            Recalculate(true);
        }

        private void Recalculate(bool notify)
        {
            for (int i = 0; i < StatTypes.Count; i++)
            {
                cached[i] = (baseValues[i] + flatBonuses[i]) * (1f + percentBonuses[i]);
            }

            // Armor is a flat reduction, so a percent bonus on a zero base would stay zero.
            cached[(int)StatType.Armor] = baseValues[(int)StatType.Armor] + flatBonuses[(int)StatType.Armor];

            if (notify)
            {
                StatsChanged?.Invoke();
            }
        }
    }
}
