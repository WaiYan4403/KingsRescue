using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.UI
{
    /// <summary>
    /// One upgrade card on the upgrades page: name, icon and a row of slots that fill as the
    /// selected hero buys levels. Clicking the card selects it; the Buy button of the detail
    /// panel (see <see cref="MainMenuController"/>) does the purchase.
    /// </summary>
    public class UpgradeRowView : MonoBehaviour
    {
        private static readonly Color SelectedTint = Color.white;
        private static readonly Color IdleTint = new Color(0.78f, 0.78f, 0.78f, 1f);

        [SerializeField] private StatType stat;
        [SerializeField] private MainMenuController menu;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        [Header("Card art (found by name when left empty)")]
        [Tooltip("Child 'Icon/Item Icon'.")]
        [SerializeField] private Image itemIcon;
        [Tooltip("The container named 'Upgrades Slots'. Its children are the slots.")]
        [SerializeField] private Transform slotsRoot;
        [Tooltip("Optional object shown while this card is the selected one.")]
        [SerializeField] private GameObject selectedFrame;

        private GameObject[] slotMarks;
        private Image cardImage;

        public StatType Stat => stat;
        public Sprite Icon => itemIcon != null ? itemIcon.sprite : null;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (label == null)
            {
                label = GetComponentInChildren<TMP_Text>();
            }

            cardImage = GetComponent<Image>();
            ResolveArt();

            if (button != null)
            {
                button.onClick.AddListener(OnClicked);
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClicked);
            }
        }

        public void OnClicked()
        {
            if (menu != null)
            {
                menu.SelectUpgrade(stat);
            }
        }

        public void Refresh(CharacterDefinition character, bool isSelected)
        {
            if (!MetaUpgradeCatalog.TryGet(stat, out MetaUpgradeOffer offer))
            {
                return;
            }

            ResolveArt();

            int level = character != null ? MetaProgress.GetLevel(character, stat) : 0;

            if (label != null)
            {
                label.text = StatTypes.DisplayName(stat);
            }

            ApplySlots(level, offer.maxLevel);

            if (selectedFrame != null)
            {
                selectedFrame.SetActive(isSelected);
            }

            if (cardImage != null)
            {
                cardImage.color = isSelected ? SelectedTint : IdleTint;
            }

            if (button != null)
            {
                button.interactable = character != null;
            }
        }

        /// <summary>
        /// One slot per level when the card has enough slots. When the upgrade has more levels than
        /// the card has slots, each slot stands for a share of the levels and fills once that share is reached.
        /// </summary>
        private void ApplySlots(int level, int maxLevel)
        {
            if (slotMarks == null)
            {
                return;
            }

            int slotCount = slotMarks.Length;
            int visibleSlots = Mathf.Min(slotCount, Mathf.Max(1, maxLevel));
            Transform root = slotsRoot;

            for (int i = 0; i < slotCount; i++)
            {
                Transform slot = root != null && i < root.childCount ? root.GetChild(i) : null;
                if (slot != null)
                {
                    slot.gameObject.SetActive(i < visibleSlots);
                }

                if (slotMarks[i] == null)
                {
                    continue;
                }

                // Level needed to light slot i. Equals i + 1 when slots >= max levels.
                int needed = Mathf.CeilToInt((i + 1) * maxLevel / (float)visibleSlots);
                slotMarks[i].SetActive(i < visibleSlots && level >= needed);
            }
        }

        private void ResolveArt()
        {
            if (itemIcon == null)
            {
                Transform icon = transform.Find("Icon/Item Icon");
                if (icon != null)
                {
                    itemIcon = icon.GetComponent<Image>();
                }
            }

            if (slotsRoot == null)
            {
                slotsRoot = transform.Find("Upgrades Slots");
            }

            if (selectedFrame == null)
            {
                Transform frame = transform.Find("Selected");
                if (frame != null)
                {
                    selectedFrame = frame.gameObject;
                }
            }

            if (slotMarks != null || slotsRoot == null)
            {
                return;
            }

            slotMarks = new GameObject[slotsRoot.childCount];
            for (int i = 0; i < slotMarks.Length; i++)
            {
                Transform slot = slotsRoot.GetChild(i);
                Transform mark = slot.Find("Unlocked Mark");
                if (mark == null && slot.childCount > 0)
                {
                    mark = slot.GetChild(0);
                }

                slotMarks[i] = mark != null ? mark.gameObject : null;
            }
        }
    }
}
