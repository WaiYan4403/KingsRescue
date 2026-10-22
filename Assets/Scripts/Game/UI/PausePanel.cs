using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KingsRescue.Core;

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

        private bool open;

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
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
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

        public void Toggle()
        {
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
            if (animator != null)
            {
                animator.Play("PauseUI_Open", 0, 0f);
                animator.Update(0f);
            }

            game.PauseRun();
        }

        private void Resume()
        {
            if (!open)
            {
                return;
            }

            open = false;
            if (panel != null)
            {
                panel.SetActive(false);
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
