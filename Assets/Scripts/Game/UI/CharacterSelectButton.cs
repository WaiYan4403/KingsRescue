using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

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

        private Material portraitMaterial;
        private bool portraitMode;
        private static readonly int EnableOutlineId = Shader.PropertyToID("_EnableOutline");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private bool UsesPortraitOutline
        {
            get
            {
                if (portraitMode)
                {
                    return true;
                }

                if (background != null && background.material != null && background.material.HasProperty(EnableOutlineId))
                {
                    portraitMode = true;
                    return true;
                }

                return false;
            }
        }

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

            if (portraitMaterial != null)
            {
                Destroy(portraitMaterial);
                portraitMaterial = null;
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
            Refresh(selected);
        }

        public void Refresh(bool selected)
        {
            bool unlocked = character != null && AchievementProgress.IsCharacterUnlocked(character);
            ApplyLabel(unlocked);

            if (UsesPortraitOutline)
            {
                ApplyPortrait(selected);
                return;
            }

            if (background != null)
            {
                if (!unlocked)
                {
                    background.color = selected ? new Color(0.28f, 0.24f, 0.16f, 1f) : new Color(0.14f, 0.14f, 0.12f, 1f);
                }
                else
                {
                    background.color = selected ? MenuUi.ButtonSelected : MenuUi.Button;
                }
            }
        }

        private void ApplyPortrait(bool selected)
        {
            EnsurePortraitMaterial();
            if (button != null)
            {
                button.transition = Selectable.Transition.None;
            }

            if (selected && portraitMaterial != null)
            {
                background.material = portraitMaterial;
                background.color = Color.white;
                portraitMaterial.SetFloat(EnableOutlineId, 1f);
                if (portraitMaterial.HasProperty(ColorId))
                {
                    portraitMaterial.SetColor(ColorId, Color.white);
                }

                return;
            }

            // The outline shader draws the sprite art on its own, so a black
            // vertex color never reaches it. The default UI material does.
            background.material = null;
            background.color = Color.black;
        }

        private void EnsurePortraitMaterial()
        {
            if (portraitMaterial != null || background == null)
            {
                return;
            }

            Material source = background.material;
            if (source == null || !source.HasProperty(EnableOutlineId))
            {
                return;
            }

            portraitMaterial = new Material(source);
            background.material = portraitMaterial;
        }

        public void ApplyLabel()
        {
            ApplyLabel(character != null && AchievementProgress.IsCharacterUnlocked(character));
        }

        private void ApplyLabel(bool unlocked)
        {
            if (label == null || character == null)
            {
                return;
            }

            label.enableWordWrapping = true;
            label.fontSize = unlocked ? 28 : 20;
            label.text = unlocked
                ? character.displayName
                : $"{character.displayName}\n{AchievementCatalog.UnlockHint(character)}";
        }
    }
}
