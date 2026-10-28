using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Audio;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.InputControl;
using KingsRescue.Player;
using KingsRescue.Progression;
using System.Collections;

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
        [SerializeField] private GameObject notEnoughLuck;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private Animator animator;

        private const float EffectLead = 1f;
        private const string EffectObjectName = "Level Up Effect";

        private readonly List<UpgradeDefinition> choices = new List<UpgradeDefinition>(4);
        private Graphic joystickZone;
        private Image overlay;
        private GameObject levelUpEffect;
        private Coroutine leadRoutine;
        private bool presenting;

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

            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged += OnStateChanged;
                game.HeroDied += DismissImmediate;
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

            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged -= OnStateChanged;
                game.HeroDied -= DismissImmediate;
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (state == GameState.GameOver)
            {
                DismissImmediate();
            }
        }

        private void OnLeveledUp(int level)
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.CanOfferLevelUp)
            {
                return;
            }

            game.PushLevelUpPause();
            if (presenting)
            {
                return;
            }

            BeginLead();
        }

        /// <summary>
        /// The panel object is inactive until the cards open, so the wait runs on the game manager.
        /// </summary>
        private void BeginLead()
        {
            presenting = true;
            SetJoystickRaycasts(false);
            GameManager host = GameManager.Instance;
            if (host == null)
            {
                presenting = false;
                Present();
                return;
            }

            if (leadRoutine != null)
            {
                host.StopCoroutine(leadRoutine);
            }

            leadRoutine = host.StartCoroutine(PlayEffectThenPresent());
        }

        /// <summary>
        /// The run is already paused. The hero effect and level-up sound play, then the cards open.
        /// </summary>
        private IEnumerator PlayEffectThenPresent()
        {
            SoundEffects.Play(SoundId.PlayerLevelUp);
            ShowLevelUpEffect();
            yield return new WaitForSecondsRealtime(EffectLead);
            leadRoutine = null;
            if (!presenting)
            {
                yield break;
            }

            HideLevelUpEffect();
            presenting = false;
            Present();
        }

        private void Present()
        {
            GameManager game = GameManager.Instance;
            if (game == null || !game.CanOfferLevelUp)
            {
                DismissImmediate();
                return;
            }

            UpgradeService service = UpgradeService.Instance;
            if (service == null)
            {
                Debug.LogError("LevelUpPanel found no UpgradeService in the scene.", this);
                return;
            }

            service.BuildChoices(choices);
            SoundEffects.Play(SoundId.MenuOpenPaper);

            if (levelLabel != null)
            {
                int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;
                levelLabel.text = $"Level {level}";
            }

            bool fourthUnlocked = service.FourthCardUnlocked;
            if (notEnoughLuck != null)
            {
                notEnoughLuck.SetActive(!fourthUnlocked);
            }

            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] == null)
                {
                    continue;
                }

                bool show = i < choices.Count && (i < 3 || fourthUnlocked);
                if (!show)
                {
                    cards[i].gameObject.SetActive(false);
                    continue;
                }

                cards[i].EnsureClickable();
                cards[i].Bind(choices[i], service.Loadout, OnCardChosen);
            }

            SetJoystickRaycasts(false);
            if (overlay != null)
            {
                overlay.raycastTarget = true;
            }

            // Sit above HUD / joystick so GraphicRaycaster hits the cards first.
            root.transform.SetAsLastSibling();
            root.SetActive(true);
            animator.SetTrigger("Open");
        }

        private void OnCardChosen(UpgradeDefinition upgrade)
        {
            // Lock the row so a double tap cannot spend two level-ups on one card.
            for (int i = 0; i < cards.Length; i++)
            {
                if (cards[i] != null)
                {
                    cards[i].SetInteractable(false);
                }
            }

            UpgradeService.Instance?.Apply(upgrade);

            GameManager game = GameManager.Instance;
            if (game == null || !game.CanOfferLevelUp)
            {
                DismissImmediate();
                return;
            }

            bool moreQueued = game.PopLevelUpPause();

            if (moreQueued)
            {
                if (root != null)
                {
                    root.SetActive(false);
                }

                BeginLead();
                return;
            }

            StartCoroutine(DelayClose());
        }

        private void DismissImmediate()
        {
            if (leadRoutine != null && GameManager.Instance != null)
            {
                GameManager.Instance.StopCoroutine(leadRoutine);
                leadRoutine = null;
            }

            StopAllCoroutines();
            presenting = false;
            HideLevelUpEffect();
            if (root != null)
            {
                root.SetActive(false);
            }

            SetJoystickRaycasts(true);
        }

        private void ShowLevelUpEffect()
        {
            GameObject effect = FindLevelUpEffect();
            if (effect == null)
            {
                return;
            }

            effect.SetActive(true);
            Animator effectAnimator = effect.GetComponent<Animator>();
            if (effectAnimator == null)
            {
                return;
            }

            effectAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            effectAnimator.Play(0, 0, 0f);
            effectAnimator.Update(0f);
        }

        private void HideLevelUpEffect()
        {
            if (levelUpEffect != null)
            {
                levelUpEffect.SetActive(false);
            }
        }

        private GameObject FindLevelUpEffect()
        {
            if (levelUpEffect != null)
            {
                return levelUpEffect;
            }

            PlayerController player = PlayerController.Instance;
            if (player == null)
            {
                return null;
            }

            Transform[] children = player.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i].name == EffectObjectName)
                {
                    levelUpEffect = children[i].gameObject;
                    return levelUpEffect;
                }
            }

            return null;
        }

        private void SetJoystickRaycasts(bool enabled)
        {
            if (joystickZone == null && VirtualJoystick.Instance != null)
            {
                joystickZone = VirtualJoystick.Instance.GetComponent<Graphic>();
            }

            if (joystickZone != null)
            {
                joystickZone.raycastTarget = enabled;
            }
        }

        IEnumerator DelayClose()
        {
            SoundEffects.Play(SoundId.MenuOpenPaper);
            animator.SetTrigger("Close");
            yield return new WaitForSeconds(0.45f);
            root.SetActive(false);
            SetJoystickRaycasts(true);
        }
    }
}
