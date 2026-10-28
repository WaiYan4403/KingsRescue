using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KingsRescue.Audio;
using KingsRescue.Core;
using KingsRescue.Meta;
using ToggleSwitch = Poker.UI.ToggleSwitch.ToggleSwitch;

namespace KingsRescue.UI
{
    /// <summary>
    /// Opens the authored pause panel from the HUD button or Escape, and freezes the run.
    /// </summary>
    public class PausePanel : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resumeButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private Animator animator;
        [SerializeField] private RectTransform card;
        [SerializeField] private GameObject title;

        [Header("Settings")]
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider soundSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private ToggleSwitch screenShakeToggle;
        [SerializeField] private ToggleSwitch damageTextToggle;

        private bool open;
        private bool closing;
        private bool settingsOpen;
        private bool holdSettings;
        private Coroutine closeRoutine;
        private Coroutine settingsRoutine;
        private Coroutine settingsSettle;

        private void Awake()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.AddListener(Toggle);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.AddListener(Resume);
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(Restart);
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(ReturnToMenu);
            }

            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (pauseButton != null)
            {
                pauseButton.onClick.RemoveListener(Toggle);
            }

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(Resume);
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(Restart);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(ReturnToMenu);
            }
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || closing)
            {
                return;
            }

            if (settingsOpen)
            {
                HideSettings();
                return;
            }

            if (open)
            {
                Resume();
            }
            else
            {
                Open();
            }
        }

        public void Toggle()
        {
            if (closing)
            {
                return;
            }

            if (open)
            {
                Resume();
            }
            else
            {
                Open();
            }
        }

        private void Open()
        {
            GameManager game = GameManager.Instance;
            if (panel == null || game == null)
            {
                return;
            }

            if (game.State != GameState.Playing && game.State != GameState.Intro)
            {
                return;
            }

            open = true;
            panel.SetActive(true);
            panel.transform.SetAsLastSibling();
            SoundEffects.Play(SoundId.MenuOpenGold);
            if (animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.Play("PauseUI_Open", 0, 0f);
                animator.Update(0f);
            }

            game.PauseRun();
        }

        /// <summary>Opens the settings overlay without leaving the pause or the run.</summary>
        public void ShowSettings()
        {
            if (!open || closing || settingsPanel == null)
            {
                return;
            }

            if (settingsRoutine != null)
            {
                StopCoroutine(settingsRoutine);
                settingsRoutine = null;
            }

            Animator settingsAnimator = settingsPanel.GetComponent<Animator>();
            if (settingsAnimator != null)
            {
                settingsAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;
            }

            if (settingsSettle != null)
            {
                StopCoroutine(settingsSettle);
                settingsSettle = null;
            }

            holdSettings = true;
            settingsOpen = true;
            settingsPanel.SetActive(true);
            settingsPanel.transform.SetAsLastSibling();
            PanelClose.PlayOpen(settingsPanel);
            SoundEffects.Play(SoundId.MenuOpenGold);
            SyncSettingsControls();
            settingsSettle = StartCoroutine(SettleSettings());
        }

        /// <summary>Closes settings and returns to the pause card. The run stays paused.</summary>
        public void HideSettings()
        {
            if (!settingsOpen || settingsPanel == null)
            {
                return;
            }

            SoundEffects.Play(SoundId.UiBack);
            SoundEffects.Play(SoundId.MenuOpenGold);
            if (settingsRoutine != null)
            {
                StopCoroutine(settingsRoutine);
            }

            if (settingsSettle != null)
            {
                StopCoroutine(settingsSettle);
                settingsSettle = null;
            }

            holdSettings = true;
            settingsRoutine = StartCoroutine(CloseSettings());
        }

        public void OnMusicChanged(float value)
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.MusicVolume = value;
        }

        public void OnSoundChanged(float value)
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.SoundVolume = value;
        }

        public void OnSensitivityChanged(float value)
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.JoystickSensitivity = value;
        }

        public void OnScreenShakeChanged(bool value)
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.ScreenShake = value;
        }

        public void OnDamageTextChanged(bool value)
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.ShowDamageText = value;
        }

        /// <summary>
        /// The scroll animator reports a collapsed slider as 0. Reapply the saved values after that pass.
        /// </summary>
        private IEnumerator SettleSettings()
        {
            yield return null;
            if (settingsOpen)
            {
                SyncSettingsControls();
            }

            yield return null;
            if (settingsOpen)
            {
                SyncSettingsControls();
            }

            holdSettings = false;
            settingsSettle = null;
        }

        private void SyncSettingsControls()
        {
            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(GameSettings.MusicVolume);
            }

            if (soundSlider != null)
            {
                soundSlider.SetValueWithoutNotify(GameSettings.SoundVolume);
            }

            if (sensitivitySlider != null)
            {
                sensitivitySlider.minValue = GameSettings.MinJoystickSensitivity;
                sensitivitySlider.maxValue = GameSettings.MaxJoystickSensitivity;
                sensitivitySlider.SetValueWithoutNotify(GameSettings.JoystickSensitivity);
            }

            if (screenShakeToggle != null)
            {
                screenShakeToggle.SetValueWithoutNotify(GameSettings.ScreenShake);
            }

            if (damageTextToggle != null)
            {
                damageTextToggle.SetValueWithoutNotify(GameSettings.ShowDamageText);
            }
        }

        private IEnumerator CloseSettings()
        {
            float wait = 0f;
            if (PanelClose.TryPlay(settingsPanel, out float duration))
            {
                wait = duration;
            }

            if (wait > 0f)
            {
                yield return new WaitForSecondsRealtime(wait);
            }

            if (settingsPanel != null)
            {
                settingsPanel.SetActive(false);
            }

            settingsOpen = false;
            holdSettings = false;
            settingsRoutine = null;
        }

        private void Resume()
        {
            if (!open || closing)
            {
                return;
            }

            if (settingsOpen)
            {
                settingsOpen = false;
                holdSettings = true;
                if (settingsRoutine != null)
                {
                    StopCoroutine(settingsRoutine);
                    settingsRoutine = null;
                }

                if (settingsSettle != null)
                {
                    StopCoroutine(settingsSettle);
                    settingsSettle = null;
                }

                if (settingsPanel != null)
                {
                    settingsPanel.SetActive(false);
                }

                holdSettings = false;
            }

            if (closeRoutine != null)
            {
                StopCoroutine(closeRoutine);
            }

            closeRoutine = StartCoroutine(CloseThenResume());
        }

        /// <summary>
        /// Rolls the pause card back up, then hides the panel. The open clip keeps writing that
        /// card, so the animator is switched off until the roll-up finishes.
        /// </summary>
        private IEnumerator CloseThenResume()
        {
            closing = true;
            SoundEffects.Play(SoundId.MenuOpenGold);
            float wait = 0.2f;

            if (animator != null)
            {
                AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
                float played = Mathf.Clamp(info.normalizedTime, 0f, 1f);
                wait = Mathf.Max(0.12f, played * info.length);
                animator.enabled = false;
            }

            if (title != null)
            {
                title.SetActive(false);
            }

            if (card != null)
            {
                card.DOKill();
                card.DOScaleY(0f, wait).SetEase(Ease.InBack).SetUpdate(true);
                card.DOAnchorPosY(630f, wait).SetEase(Ease.InCubic).SetUpdate(true);
            }

            yield return new WaitForSecondsRealtime(wait);

            if (card != null)
            {
                card.DOKill();
            }

            open = false;
            closing = false;
            closeRoutine = null;
            if (panel != null)
            {
                panel.SetActive(false);
            }

            if (animator != null)
            {
                animator.speed = 1f;
                animator.enabled = true;
            }

            GameManager.Instance?.ResumeRun();
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            GameMusicController.Instance?.StopMusic();
            GameManager.Instance?.RestartRun();
        }

        private void ReturnToMenu()
        {
            Time.timeScale = 1f;
            GameMusicController.Instance?.StopMusic();
            GameManager.Instance?.ReturnToMenu();
        }
    }
}
