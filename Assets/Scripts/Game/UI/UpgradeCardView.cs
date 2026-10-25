using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Audio;
using KingsRescue.Data;
using KingsRescue.Player;

namespace KingsRescue.UI
{
    /// <summary>
    /// One tappable upgrade card. Sized for a thumb on a 360x640 screen.
    /// </summary>
    public class UpgradeCardView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text descriptionLabel;
        [SerializeField] private TMP_Text bannerLabel;

        private UpgradeDefinition bound;
        private Action<UpgradeDefinition> onChosen;

        private void Awake()
        {
            if (button == null)
            {
                button = GetComponent<Button>();
            }

            EnsureClickable();

            if (button != null)
            {
                button.onClick.AddListener(HandleClick);
            }
        }

        /// <summary>
        /// Prototype cards were authored with Image.raycastTarget off so they would
        /// not steal joystick drags. A Button with no raycast graphic never receives
        /// clicks — the full-screen dimmer eats them instead.
        /// </summary>
        public void EnsureClickable()
        {
            var background = GetComponent<Image>();
            if (background != null)
            {
                background.raycastTarget = true;
                if (button != null)
                {
                    button.targetGraphic = background;
                }
            }

            if (iconImage != null)
            {
                iconImage.raycastTarget = false;
            }

            if (titleLabel != null)
            {
                titleLabel.raycastTarget = false;
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.raycastTarget = false;
            }

            if (bannerLabel != null)
            {
                bannerLabel.raycastTarget = false;
            }
        }

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(HandleClick);
            }
        }

        public void Bind(UpgradeDefinition upgrade, WeaponLoadout loadout, Action<UpgradeDefinition> callback)
        {
            bound = upgrade;
            onChosen = callback;

            gameObject.SetActive(upgrade != null);

            if (upgrade == null)
            {
                return;
            }

            if (titleLabel != null)
            {
                titleLabel.text = upgrade.Title(loadout);
            }

            if (descriptionLabel != null)
            {
                descriptionLabel.text = upgrade.Description(loadout);
            }

            if (bannerLabel != null)
            {
                string banner = upgrade.Banner(loadout);
                bannerLabel.text = banner;
                bannerLabel.gameObject.SetActive(!string.IsNullOrEmpty(banner));
            }

            if (iconImage != null)
            {
                Sprite icon = upgrade.Icon();
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (button != null)
            {
                button.interactable = true;
            }
        }

        public void SetInteractable(bool value)
        {
            if (button != null)
            {
                button.interactable = value;
            }
        }

        private void HandleClick()
        {
            if (bound == null)
            {
                return;
            }

            onChosen?.Invoke(bound);
            SoundEffects.Play(SoundId.UiConfirm);
        }
    }
}
