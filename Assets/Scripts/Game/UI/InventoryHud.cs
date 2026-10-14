using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.UI
{
    /// <summary>
    /// Vampire Survivors-style HUD tray: four weapon slots on the first row,
    /// four passive slots on the second. Empty frames stay visible.
    /// </summary>
    public class InventoryHud : MonoBehaviour
    {
        [System.Serializable]
        public class Slot
        {
            public Image icon;
            public TMP_Text level;
            public TMP_Text initial;
        }

        [SerializeField] private Slot[] weaponSlots = new Slot[4];
        [SerializeField] private Slot[] passiveSlots = new Slot[4];
        [SerializeField] private WeaponLoadout loadout;

        private readonly List<WeaponRuntime> weapons = new List<WeaponRuntime>(4);
        private readonly List<PassiveDefinition> passives = new List<PassiveDefinition>(4);
        private readonly List<int> passiveLevels = new List<int>(4);
        private readonly Dictionary<string, Sprite> swatches = new Dictionary<string, Sprite>(24);

        private void OnEnable()
        {
            ResolveLoadout();
            Bind(true);
            Refresh();
        }

        private void Start()
        {
            ResolveLoadout();
            Bind(true);
            Refresh();
        }

        private void OnDisable()
        {
            Bind(false);
        }

        private void OnDestroy()
        {
            Bind(false);
            foreach (KeyValuePair<string, Sprite> pair in swatches)
            {
                if (pair.Value != null)
                {
                    Destroy(pair.Value);
                }
            }

            swatches.Clear();
        }

        private void ResolveLoadout()
        {
            if (loadout == null)
            {
                loadout = GameManager.Instance != null ? GameManager.Instance.Loadout : null;
            }
        }

        private void Bind(bool subscribe)
        {
            if (loadout == null)
            {
                return;
            }

            loadout.Changed -= Refresh;
            if (subscribe)
            {
                loadout.Changed += Refresh;
            }
        }

        private void Refresh()
        {
            if (loadout == null)
            {
                ResolveLoadout();
                Bind(true);
            }

            if (loadout != null)
            {
                loadout.CollectWeapons(weapons);
                loadout.CollectPassives(passives, passiveLevels);
            }
            else
            {
                weapons.Clear();
                passives.Clear();
                passiveLevels.Clear();
            }

            PaintWeapons();
            PaintPassives();
        }

        private void PaintWeapons()
        {
            for (int i = 0; i < weaponSlots.Length; i++)
            {
                if (i < weapons.Count && weapons[i] != null && weapons[i].Definition != null)
                {
                    WeaponDefinition weapon = weapons[i].Definition;
                    BindSlot(weaponSlots[i], weapon.icon, weapon.displayName, weapons[i].Level);
                }
                else
                {
                    ClearSlot(weaponSlots[i]);
                }
            }
        }

        private void PaintPassives()
        {
            for (int i = 0; i < passiveSlots.Length; i++)
            {
                if (i < passives.Count && passives[i] != null)
                {
                    PassiveDefinition passive = passives[i];
                    BindSlot(passiveSlots[i], passive.icon, passive.displayName, passiveLevels[i]);
                }
                else
                {
                    ClearSlot(passiveSlots[i]);
                }
            }
        }

        private void BindSlot(Slot slot, Sprite icon, string title, int level)
        {
            if (slot == null)
            {
                return;
            }

            bool hasArt = icon != null;
            if (slot.icon != null)
            {
                slot.icon.sprite = hasArt ? icon : SwatchFor(title);
                slot.icon.enabled = true;
                slot.icon.preserveAspect = true;
            }

            if (slot.initial != null)
            {
                slot.initial.text = hasArt ? "" : Initial(title);
                slot.initial.enabled = !hasArt;
            }

            if (slot.level != null)
            {
                slot.level.text = level > 0 ? level.ToString() : "";
                slot.level.enabled = level > 0;
            }
        }

        private static void ClearSlot(Slot slot)
        {
            if (slot == null)
            {
                return;
            }

            if (slot.icon != null)
            {
                slot.icon.enabled = false;
            }

            if (slot.initial != null)
            {
                slot.initial.text = "";
                slot.initial.enabled = false;
            }

            if (slot.level != null)
            {
                slot.level.text = "";
                slot.level.enabled = false;
            }
        }

        private static string Initial(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                return "?";
            }

            return title.Trim()[0].ToString().ToUpperInvariant();
        }

        private Sprite SwatchFor(string title)
        {
            string key = string.IsNullOrWhiteSpace(title) ? "?" : title;
            if (swatches.TryGetValue(key, out Sprite existing) && existing != null)
            {
                return existing;
            }

            float hue = Mathf.Abs(key.GetHashCode() % 360) / 360f;
            Color color = Color.HSVToRGB(hue, 0.42f, 0.92f);
            Texture2D texture = new Texture2D(8, 8, TextureFormat.RGBA32, false)
            {
                name = "InvSwatch_" + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };

            Color[] pixels = new Color[64];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }

            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 8f, 8f), new Vector2(0.5f, 0.5f), 8f);
            sprite.name = texture.name;
            swatches[key] = sprite;
            return sprite;
        }
    }
}
