using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Progression;
using System.Collections;

namespace KingsRescue.UI
{
    /// <summary>
    /// Run summary shown when the hero's HP hits zero.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [Header("Summary")]
        [SerializeField] private TMP_Text timeLabel;
        [SerializeField] private TMP_Text levelLabel;
        [SerializeField] private TMP_Text killsLabel;
        [SerializeField] private TMP_Text coinsLabel;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;

        [SerializeField] private Animator animator;

        private void Awake()
        {
            if (root == null)
            {
                root = gameObject;
            }

            if (restartButton != null)
            {
                restartButton.onClick.AddListener(OnRestart);
            }

            if (menuButton != null)
            {
                menuButton.onClick.AddListener(OnMenu);
            }
        }

        private void Start()
        {
            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged += OnStateChanged;
            }

            // Hidden only after subscribing, so this script may sit on the panel root itself.
            root.SetActive(false);
        }

        private void OnDestroy()
        {
            GameManager game = GameManager.Instance;
            if (game != null)
            {
                game.StateChanged -= OnStateChanged;
            }

            if (restartButton != null)
            {
                restartButton.onClick.RemoveListener(OnRestart);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(OnMenu);
            }
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.GameOver)
            {
                return;
            }

            Populate();
            root.SetActive(true);
            animator.SetTrigger("Open");
        }

        private void Populate()
        {
            GameManager game = GameManager.Instance;

            if (timeLabel != null)
            {
                timeLabel.text = HudView.FormatClock(game != null ? game.RunTime : 0f);
            }

            if (killsLabel != null)
            {
                killsLabel.text = (game != null ? game.Kills : 0).ToString();
            }

            if (coinsLabel != null)
            {
                coinsLabel.text = (game != null ? game.Coins : 0).ToString();
            }

            if (levelLabel != null)
            {
                int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;
                levelLabel.text = level.ToString();
            }
        }

        private void OnRestart()
        {
            GameManager.Instance?.RestartRun();
            GameMusicController.Instance.StopMusic();
            StartCoroutine(DelayClose());
        }

        private void OnMenu()
        {
            GameManager.Instance?.ReturnToMenu();
            GameMusicController.Instance.StopMusic();
            StartCoroutine(DelayClose());
        }

        IEnumerator DelayClose()
        {
            animator.SetTrigger("Close");
            yield return new WaitForSeconds(0.45f);
            root.SetActive(false);
        }
    }
}
