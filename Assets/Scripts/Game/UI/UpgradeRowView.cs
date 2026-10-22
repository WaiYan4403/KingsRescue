using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.UI
{
    public class UpgradeRowView : MonoBehaviour
    {
        [SerializeField] private StatType stat;
        [SerializeField] private MainMenuController menu;
        [SerializeField] private Button button;
        [SerializeField] private TMP_Text label;

        public StatType Stat => stat;

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
            CharacterDefinition character = menu != null ? menu.Selected : null;
            if (character != null && MetaProgress.TryPurchase(character, stat) && menu != null)
            {
                menu.RefreshUpgrades();
            }
        }

        public void Refresh(CharacterDefinition character)
        {
            if (!MetaUpgradeCatalog.TryGet(stat, out MetaUpgradeOffer offer))
            {
                return;
            }

            int level = MetaProgress.GetLevel(character, stat);
            bool maxed = level >= offer.maxLevel;
            int cost = offer.CostForNext(level);
            string bonus;
            if (offer.stat == StatType.AttackSpeed)
            {
                bonus = $"-{offer.perLevel * 100f:0}% / lv";
            }
            else if (offer.mode == ModifierMode.Percent)
            {
                bonus = $"+{offer.perLevel * 100f:0}% / lv";
            }
            else
            {
                bonus = $"+{offer.perLevel:0.##} / lv";
            }

            if (label != null)
            {
                label.text = maxed
                    ? $"{StatTypes.DisplayName(stat)}  Lv {level}/{offer.maxLevel}  MAX"
                    : $"{StatTypes.DisplayName(stat)}  Lv {level}/{offer.maxLevel}  {bonus}  ({cost} coins)";
            }

            if (button != null)
            {
                bool canPay = MetaProgress.DevMode || MetaProgress.Coins >= cost;
                button.interactable = character != null && !maxed && canPay;
            }
        }
    }
}
