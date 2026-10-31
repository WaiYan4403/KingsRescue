using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KingsRescue.Audio;
using KingsRescue.Data;

namespace KingsRescue.UI
{
    /// <summary>
    /// One row of the achievements list (the "Row Template" prefab): an unlocked checkbox, the
    /// achievement name and info, and the reward icon. Clicking anywhere on the row selects it.
    /// </summary>
    public class AchievementRowView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private Toggle unlockedToggle;
        [SerializeField] private TMP_Text conditionText;
        [SerializeField] private Image rewardIcon;
        [Tooltip("Frame shown while this row is the selected one.")]
        [SerializeField] private GameObject selectedMark;

        private Action<AchievementRowView> clicked;
        private bool unlocked;

        public AchievementDefinition Achievement { get; private set; }

        /// <summary>The reward sprite currently drawn on this row. The detail panel copies this.</summary>
        public Sprite ShownIcon => rewardIcon != null && rewardIcon.enabled ? rewardIcon.sprite : null;

        private void Awake()
        {
            // The checkbox only displays state. Keep it, and the selection frame, from stealing clicks.
            if (unlockedToggle != null)
            {
                Navigation navigation = unlockedToggle.navigation;
                navigation.mode = Navigation.Mode.None;
                unlockedToggle.navigation = navigation;
                unlockedToggle.onValueChanged.AddListener(OnToggleTouched);

                foreach (Graphic graphic in unlockedToggle.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }

            if (selectedMark != null)
            {
                foreach (Graphic graphic in selectedMark.GetComponentsInChildren<Graphic>(true))
                {
                    graphic.raycastTarget = false;
                }
            }

            // The row root needs a raycast target of its own, or clicks between the children miss it.
            var catcher = GetComponent<Image>();
            if (catcher == null)
            {
                catcher = gameObject.AddComponent<Image>();
                catcher.color = Color.clear;
            }

            catcher.raycastTarget = true;
        }

        private void OnDestroy()
        {
            if (unlockedToggle != null)
            {
                unlockedToggle.onValueChanged.RemoveListener(OnToggleTouched);
            }
        }

        public void Bind(AchievementDefinition achievement, Action<AchievementRowView> onClicked)
        {
            Achievement = achievement;
            clicked = onClicked;
            gameObject.name = achievement != null ? achievement.Id : "Achievement";
        }

        public void Show(string title, string info, bool isUnlocked, Sprite icon)
        {
            unlocked = isUnlocked;

            if (conditionText != null)
            {
                conditionText.text = string.IsNullOrEmpty(info)
                    ? $"<b>{title}</b>"
                    : $"<b>{title}</b>\n<size=75%>{info}</size>";
            }

            if (unlockedToggle != null)
            {
                unlockedToggle.SetIsOnWithoutNotify(isUnlocked);
            }

            if (rewardIcon != null)
            {
                rewardIcon.sprite = icon;
                rewardIcon.enabled = icon != null;
                rewardIcon.preserveAspect = true;
            }
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
            SoundEffects.Play(SoundId.UiClick);
            Choose();
        }

        /// <summary>Selects the row without the click sound. Used while moving the menu cursor.</summary>
        public void Choose()
        {
            clicked?.Invoke(this);
        }

        private void OnToggleTouched(bool value)
        {
            if (value != unlocked && unlockedToggle != null)
            {
                unlockedToggle.SetIsOnWithoutNotify(unlocked);
            }
        }
    }
}
