using System.Collections;
using DG.Tweening;
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
        [SerializeField] private RectTransform card;
        [SerializeField] private GameObject title;

        private bool open;
        private bool closing;
        private Coroutine closeRoutine;

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
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || closing)
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
            if (animator != null)
            {
                animator.enabled = true;
                animator.speed = 1f;
                animator.Play("PauseUI_Open", 0, 0f);
                animator.Update(0f);
            }

            game.PauseRun();
        }

        private void Resume()
        {
            if (!open || closing)
            {
                return;
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
