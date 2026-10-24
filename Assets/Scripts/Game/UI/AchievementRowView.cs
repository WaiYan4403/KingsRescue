using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
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

        private void Awake()
        {
            ResolveParts();

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
            ResolveParts();
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

            if (rewardIcon != null && icon != null)
            {
                rewardIcon.sprite = icon;
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
            clicked?.Invoke(this);
        }

        private void OnToggleTouched(bool value)
        {
            if (value != unlocked && unlockedToggle != null)
            {
                unlockedToggle.SetIsOnWithoutNotify(unlocked);
            }
        }

        private void ResolveParts()
        {
            if (unlockedToggle == null)
            {
                unlockedToggle = GetComponentInChildren<Toggle>(true);
            }

            if (conditionText == null)
            {
                Transform text = transform.Find("Condition Text");
                conditionText = text != null ? text.GetComponent<TMP_Text>() : GetComponentInChildren<TMP_Text>(true);
            }

            if (rewardIcon == null)
            {
                Transform icon = transform.Find("Icon Slot/Icon");
                if (icon != null)
                {
                    rewardIcon = icon.GetComponent<Image>();
                }
            }

            if (selectedMark == null)
            {
                Transform mark = transform.Find("Icon Slot/Selected");
                if (mark != null)
                {
                    selectedMark = mark.gameObject;
                }
            }
        }
    }
}
