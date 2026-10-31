using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KingsRescue.Audio;

namespace KingsRescue.UI
{
    /// <summary>
    /// One cell in the collections grid. The border stays on the prefab.
    /// A locked item's icon is fully black and cannot be selected.
    /// </summary>
    public class CollectionSlotView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Image icon;
        [SerializeField] private GameObject locked;
        [SerializeField] private GameObject selectedMark;

        private Action<CollectionSlotView> clicked;

        public string Title { get; private set; }
        public string Description { get; private set; }
        public bool Collected { get; private set; }
        public bool IsWeapon { get; private set; }
        public Sprite IconSprite => icon != null ? icon.sprite : null;

        public void Bind(Action<CollectionSlotView> onClicked)
        {
            clicked = onClicked;
        }

        public void Show(string title, string description, Sprite sprite, bool collected, bool weapon)
        {
            Title = string.IsNullOrWhiteSpace(title) ? "Unknown" : title;
            Description = description ?? string.Empty;
            Collected = collected;
            IsWeapon = weapon;

            if (icon != null)
            {
                icon.sprite = sprite;
                icon.enabled = sprite != null;
                icon.preserveAspect = true;
                icon.color = collected ? Color.white : Color.black;
            }

            if (locked != null)
            {
                Image veil = locked.GetComponent<Image>();
                if (veil != null)
                {
                    veil.color = Color.black;
                    veil.raycastTarget = false;
                }

                locked.SetActive(!collected);
            }

            SetSelected(false);
        }

        public void SetSelected(bool value)
        {
            if (selectedMark != null)
            {
                selectedMark.SetActive(value);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!Collected)
            {
                return;
            }

            SoundEffects.Play(SoundId.UiClick);
            Choose();
        }

        /// <summary>Selects the slot without the click sound. Used while moving the menu cursor.</summary>
        public void Choose()
        {
            if (!Collected)
            {
                return;
            }

            clicked?.Invoke(this);
        }
    }
}
