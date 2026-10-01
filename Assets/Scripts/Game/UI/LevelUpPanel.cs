using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.InputControl;
using KingsRescue.Progression;

namespace KingsRescue.UI
{
    /// <summary>
    /// The choose-your-upgrade screen. Freezes the run through
    /// <see cref="GameManager"/> and walks through queued level-ups one at a time.
    /// </summary>
    public class LevelUpPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private UpgradeCardView[] cards;
        [SerializeField] private TMP_Text levelLabel;

        private readonly List<UpgradeDefinition> choices = new List<UpgradeDefinition>(4);
        private VirtualJoystick joystick;
        private Graphic joystickZone;
        private Image overlay;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            overlay = root.GetComponent<Image>();
            if (overlay != null)
            {
                overlay.raycastTarget = true;
            }

            joystick = FindAnyObjectByType<VirtualJoystick>(FindObjectsInactive.Include);
            if (joystick != null)
            {
                joystickZone = joystick.GetComponent<Graphic>();
            }
        }

        private void Start()
        {
            ExperienceSystem xp = ExperienceSystem.Instance;
            if (xp != null)
            {
                xp.LeveledUp += OnLeveledUp;
            }
            else
            {
                Debug.LogError("LevelUpPanel found no ExperienceSystem in the scene.", this);
            }

            // Hidden only after subscribing, so this script may sit on the panel root itself.
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            ExperienceSystem xp = ExperienceSystem.Instance;
            if (xp != null)
            {
                xp.LeveledUp -= OnLeveledUp;
            }
        }

        private void OnLeveledUp(int level)
        {
            GameManager.Instance?.PushLevelUpPause();
            Present();
        }

        private void Present()
        {
            UpgradeService service = UpgradeService.Instance;
            if (service == null)
            {
                Debug.LogError("LevelUpPanel found no UpgradeService in the scene.", this);
                return;
            }

            service.BuildChoices(choices);

            if (levelLabel != null)
            {
                int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;
                levelLabel.text = $"Level {level}";
            }

            for (int i = 0; i < cards.Length; i++)
            {
                UpgradeDefinition choice = i < choices.Count ? choices[i] : null;
                cards[i].EnsureClickable();
                cards[i].Bind(choice, service.Loadout, OnCardChosen);
            }

            SetJoystickRaycasts(false);
            if (overlay != null)
            {
                overlay.raycastTarget = true;
            }

            // Sit above HUD / joystick so GraphicRaycaster hits the cards first.
            root.transform.SetAsLastSibling();
            root.SetActive(true);
        }

        private void OnCardChosen(UpgradeDefinition upgrade)
        {
            // Lock the row so a double tap cannot spend two level-ups on one card.
            for (int i = 0; i < cards.Length; i++)
            {
                cards[i].SetInteractable(false);
            }

            UpgradeService.Instance?.Apply(upgrade);

            GameManager game = GameManager.Instance;
            bool moreQueued = game != null && game.PopLevelUpPause();

            if (moreQueued)
            {
                Present();
                return;
            }

            root.SetActive(false);
            SetJoystickRaycasts(true);
        }

        private void SetJoystickRaycasts(bool enabled)
        {
            if (joystickZone != null)
            {
                joystickZone.raycastTarget = enabled;
            }
        }
    }
}
