using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;

namespace KingsRescue.UI
{
    public class CharacterSelectButton : MonoBehaviour
    {
        [SerializeField] private CharacterDefinition character;
        [SerializeField] private MainMenuController menu;
        [SerializeField] private Button button;
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text label;

        public CharacterDefinition Character => character;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            if (background == null)
            {
                background = GetComponent<Image>();
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
            if (menu != null)
            {
                menu.SelectCharacter(character);
            }
        }

        public void SetSelected(bool selected)
        {
            if (background != null)
            {
                background.color = selected ? MenuUi.ButtonSelected : MenuUi.Button;
            }
        }

        public void ApplyLabel()
        {
            if (label != null && character != null)
            {
                label.text = character.displayName;
            }
        }
    }
}
