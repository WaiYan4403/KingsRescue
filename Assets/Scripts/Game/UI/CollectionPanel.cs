using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.UI
{
    /// <summary>
    /// Fills the collections grid. Starting weapons, level-up picks, and achievement
    /// rewards share one grid. Uncollected cells keep the slot's locked veil on.
    /// </summary>
    public class CollectionPanel : MonoBehaviour
    {
        [SerializeField] private CharacterRoster roster;
        [SerializeField] private Transform grid;
        [SerializeField] private CollectionSlotView weaponSlot;
        [SerializeField] private CollectionSlotView passiveSlot;

        [Header("Your Place")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailName;
        [SerializeField] private TMP_Text detailDescription;
        [SerializeField] private GameObject detailUnlocked;

        private readonly List<WeaponDefinition> weapons = new List<WeaponDefinition>(24);
        private readonly List<PassiveDefinition> passives = new List<PassiveDefinition>(24);
        private readonly List<CollectionSlotView> spawned = new List<CollectionSlotView>(32);
        private string selectedTitle;
        private bool selectedIsWeapon;

        private void OnEnable()
        {
            AchievementProgress.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            AchievementProgress.Changed -= Refresh;
        }

        public void Refresh()
        {
            AchievementProgress.EnsureLoaded();
            ClearSpawned();
            HideSamples();
            GatherWeapons();
            GatherPassives();

            for (int i = 0; i < weapons.Count; i++)
            {
                WeaponDefinition weapon = weapons[i];
                Spawn(weaponSlot, weapon.displayName, weapon.description, weapon.icon, IsWeaponCollected(weapon), true);
            }

            for (int i = 0; i < passives.Count; i++)
            {
                PassiveDefinition passive = passives[i];
                Spawn(passiveSlot, passive.displayName, passive.description, passive.icon, IsPassiveCollected(passive), false);
            }

            SelectInitial();
        }

        private void Spawn(CollectionSlotView prefab, string title, string description, Sprite icon, bool collected, bool weapon)
        {
            if (prefab == null || grid == null)
            {
                return;
            }

            CollectionSlotView slot = Instantiate(prefab, grid);
            slot.gameObject.name = string.IsNullOrWhiteSpace(title) ? prefab.name : title;
            slot.Bind(OnSlotClicked);
            slot.Show(title, description, icon, collected, weapon);
            spawned.Add(slot);
        }

        private void SelectInitial()
        {
            CollectionSlotView match = null;
            for (int i = 0; i < spawned.Count; i++)
            {
                CollectionSlotView slot = spawned[i];
                if (slot != null && slot.Collected && slot.Title == selectedTitle && slot.IsWeapon == selectedIsWeapon)
                {
                    match = slot;
                    break;
                }
            }

            if (match == null)
            {
                for (int i = 0; i < spawned.Count; i++)
                {
                    if (spawned[i] != null && spawned[i].Collected)
                    {
                        match = spawned[i];
                        break;
                    }
                }
            }

            ApplySelection(match);
        }

        private void OnSlotClicked(CollectionSlotView slot)
        {
            if (slot == null || !slot.Collected)
            {
                return;
            }

            ApplySelection(slot);
        }

        private void ApplySelection(CollectionSlotView slot)
        {
            selectedTitle = slot != null ? slot.Title : null;
            selectedIsWeapon = slot != null && slot.IsWeapon;

            for (int i = 0; i < spawned.Count; i++)
            {
                if (spawned[i] != null)
                {
                    spawned[i].SetSelected(spawned[i] == slot);
                }
            }

            if (detailIcon != null)
            {
                Sprite sprite = slot != null ? slot.IconSprite : null;
                detailIcon.sprite = sprite;
                detailIcon.enabled = sprite != null;
                detailIcon.preserveAspect = true;
            }

            if (detailName != null)
            {
                detailName.text = slot != null ? slot.Title : string.Empty;
            }

            if (detailDescription != null)
            {
                detailDescription.text = slot != null ? slot.Description : string.Empty;
            }

            if (detailUnlocked != null)
            {
                detailUnlocked.SetActive(slot != null && slot.Collected);
            }
        }

        private void ClearSpawned()
        {
            for (int i = spawned.Count - 1; i >= 0; i--)
            {
                if (spawned[i] != null)
                {
                    Destroy(spawned[i].gameObject);
                }
            }

            spawned.Clear();
        }

        /// <summary>A slot left in the grid for layout stays in the scene and is hidden while playing.</summary>
        private void HideSamples()
        {
            if (grid == null)
            {
                return;
            }

            for (int i = 0; i < grid.childCount; i++)
            {
                Transform child = grid.GetChild(i);
                if (child.GetComponent<CollectionSlotView>() == null)
                {
                    child.gameObject.SetActive(false);
                }
            }
        }

        private void GatherWeapons()
        {
            weapons.Clear();
            if (roster != null && roster.characters != null)
            {
                for (int i = 0; i < roster.characters.Length; i++)
                {
                    CharacterDefinition hero = roster.characters[i];
                    if (hero == null || hero.comingSoon || hero.uniqueWeapon == null)
                    {
                        continue;
                    }

                    AddWeapon(hero.uniqueWeapon);
                }
            }

            UpgradePool pool = UpgradePool.Load();
            if (pool != null && pool.entries != null)
            {
                for (int i = 0; i < pool.entries.Length; i++)
                {
                    UpgradeDefinition entry = pool.entries[i];
                    if (entry != null && entry.kind == UpgradeKind.NewWeapon)
                    {
                        AddWeapon(entry.weapon);
                    }
                }
            }

            AchievementDefinition[] achievements = AchievementCatalog.All;
            for (int i = 0; i < achievements.Length; i++)
            {
                if (achievements[i] != null)
                {
                    AddWeapon(achievements[i].rewardWeapon);
                }
            }
        }

        private void GatherPassives()
        {
            passives.Clear();
            UpgradePool pool = UpgradePool.Load();
            if (pool != null && pool.entries != null)
            {
                for (int i = 0; i < pool.entries.Length; i++)
                {
                    UpgradeDefinition entry = pool.entries[i];
                    if (entry != null && entry.kind == UpgradeKind.Passive)
                    {
                        AddPassive(entry.passive);
                    }
                }
            }

            AchievementDefinition[] achievements = AchievementCatalog.All;
            for (int i = 0; i < achievements.Length; i++)
            {
                if (achievements[i] != null)
                {
                    AddPassive(achievements[i].rewardPassive);
                }
            }
        }

        private void AddWeapon(WeaponDefinition weapon)
        {
            if (weapon != null && !weapons.Contains(weapon))
            {
                weapons.Add(weapon);
            }
        }

        private void AddPassive(PassiveDefinition passive)
        {
            if (passive != null && !passives.Contains(passive))
            {
                passives.Add(passive);
            }
        }

        private bool IsWeaponCollected(WeaponDefinition weapon)
        {
            if (AchievementProgress.HasCollectedWeapon(weapon))
            {
                return true;
            }

            if (weapon.requiresUnlock && AchievementProgress.IsWeaponUnlocked(weapon))
            {
                return true;
            }

            if (roster == null || roster.characters == null)
            {
                return false;
            }

            for (int i = 0; i < roster.characters.Length; i++)
            {
                CharacterDefinition hero = roster.characters[i];
                if (hero != null && !hero.comingSoon && hero.uniqueWeapon == weapon
                    && AchievementProgress.IsCharacterUnlocked(hero))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPassiveCollected(PassiveDefinition passive)
        {
            if (AchievementProgress.HasCollectedPassive(passive))
            {
                return true;
            }

            return passive.requiresUnlock && AchievementProgress.IsPassiveUnlocked(passive);
        }
    }
}
