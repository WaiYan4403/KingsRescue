using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KingsRescue.Audio;
using KingsRescue.Core;
using KingsRescue.InputControl;
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
        [SerializeField] private ToggleSwitch fixedJoystickToggle;
        [Tooltip("Left and right controls. Hidden while fixed joystick is off.")]
        [SerializeField] private GameObject joystickSideRow;
        [Tooltip("Shown while the fixed stick sits in the bottom-left corner.")]
        [SerializeField] private GameObject joystickLeftSelected;
        [Tooltip("Shown while the fixed stick sits in the bottom-right corner.")]
        [SerializeField] private GameObject joystickRightSelected;
        [Tooltip("On parks the stick in the bottom-left corner. Off parks it in the bottom-right.")]
        [SerializeField] private ToggleSwitch joystickLeftToggle;

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

            if (animator == null && panel != null)
            {
                animator = panel.GetComponent<Animator>();
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
            ButtonKeyCursor.Poll();
            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool pause = ButtonKeyCursor.PausePressed;
            bool cancel = ButtonKeyCursor.CancelPressed;
            if (!closing && (escape || pause || (cancel && open)))
            {
                if (settingsOpen)
                {
                    HideSettings();
                }
                else if (open)
                {
                    Resume();
                }
                else if (escape || pause)
                {
                    Open();
                }
            }

            if (open && !closing)
            {
                Transform scope = settingsOpen && settingsPanel != null
                    ? settingsPanel.transform
                    : panel != null ? panel.transform : null;
                ButtonKeyCursor.Tick(scope);
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
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.speed = 1f;
                animator.Play("ScrollCover_Open", 0, 0f);
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

        public void OnFixedJoystickChanged(bool value)
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.FixedJoystick = value;
            ApplyJoystickPlacement();
            RefreshJoystickSide();
        }

        public void OnJoystickLeft()
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.JoystickOnRight = false;
            ApplyJoystickPlacement();
            RefreshJoystickSide();
        }

        public void OnJoystickRight()
        {
            if (holdSettings)
            {
                return;
            }

            GameSettings.JoystickOnRight = true;
            ApplyJoystickPlacement();
            RefreshJoystickSide();
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

            if (fixedJoystickToggle != null)
            {
                fixedJoystickToggle.SetValueWithoutNotify(GameSettings.FixedJoystick);
            }

            RefreshJoystickSide();
        }

        private void ApplyJoystickPlacement()
        {
            if (VirtualJoystick.Instance != null)
            {
                VirtualJoystick.Instance.ApplyPlacement();
            }
        }

        private void RefreshJoystickSide()
        {
            if (joystickSideRow != null)
            {
                joystickSideRow.SetActive(GameSettings.FixedJoystick);
            }

            if (joystickLeftSelected != null)
            {
                joystickLeftSelected.SetActive(!GameSettings.JoystickOnRight);
            }

            if (joystickRightSelected != null)
            {
                joystickRightSelected.SetActive(GameSettings.JoystickOnRight);
            }

            if (joystickLeftToggle != null)
            {
                joystickLeftToggle.SetValueWithoutNotify(!GameSettings.JoystickOnRight);
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
        /// Plays ScrollCover_Close on the pause animator, then hides the panel.
        /// </summary>
        private IEnumerator CloseThenResume()
        {
            closing = true;
            ButtonKeyCursor.Release();
            SoundEffects.Play(SoundId.MenuOpenGold);

            if (animator == null && panel != null)
            {
                animator = panel.GetComponent<Animator>();
            }

            if (animator != null)
            {
                animator.enabled = true;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.speed = 1f;
            }

            float wait = 0f;
            if (PanelClose.TryPlay(panel, out float duration))
            {
                wait = duration;
            }

            if (wait > 0f)
            {
                yield return new WaitForSecondsRealtime(wait);
            }

            open = false;
            closing = false;
            closeRoutine = null;
            if (panel != null)
            {
                panel.SetActive(false);
            }

            GameManager.Instance?.ResumeRun();
        }

        private void Restart()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.RestartRun();
        }

        private void ReturnToMenu()
        {
            Time.timeScale = 1f;
            GameManager.Instance?.ReturnToMenu();
        }
    }
}
