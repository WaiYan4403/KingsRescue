using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KingsRescue.Data;
using KingsRescue.Meta;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.Core
{
    public enum GameState
    {
        Boot,
        Playing,
        LevelUp,
        GameOver
    }

    /// <summary>
    /// Owns the run: state machine, timer, score counters and the pause that the
    /// level-up screen rides on.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class GameManager : SingletonBehaviour<GameManager>
    {
        [Header("Scene References")]
        [SerializeField] private PlayerHealth playerHealth;

        [Tooltip("Playable hero for this run. Their unique weapon is granted at start.")]
        [SerializeField] private CharacterDefinition selectedCharacter;

        [Tooltip("Existing FMOD music controller. Optional; state changes are simply not scored without it.")]
        [SerializeField] private MusicController musicController;

        [Header("Flow")]
        [Tooltip("Scene to load when the player asks to leave the run. Leave empty to disable the button.")]
        [SerializeField] private string menuSceneName = "MainMenu";

        [Tooltip("How long the death animation has to play before time freezes and the game-over panel appears.")]
        [SerializeField] private float gameOverDelay = 1.15f;

        private int pendingLevelUps;
        private bool gameplayMusicStarted;

        public GameState State { get; private set; } = GameState.Boot;

        public float RunTime { get; private set; }

        public int Kills { get; private set; }

        public int Coins { get; private set; }

        public bool IsPlaying => State == GameState.Playing && (playerHealth == null || playerHealth.IsAlive);

        public CharacterDefinition SelectedCharacter => selectedCharacter;

        public event Action<GameState> StateChanged;

        public event Action<int> KillsChanged;

        public event Action<int> CoinsChanged;

        protected override void Awake()
        {
            base.Awake();

            GameSettings.Apply();
            ResolveSelectedPlayer();
        }

        private void OnEnable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died += OnPlayerDied;
            }
        }

        private void OnDisable()
        {
            if (playerHealth != null)
            {
                playerHealth.Died -= OnPlayerDied;
            }
        }

        private void Start()
        {
            StartRun();
        }

        private void Update()
        {
            if (State == GameState.Playing)
            {
                RunTime += Time.deltaTime;
            }
        }

        public void StartRun()
        {
            RunTime = 0f;
            Kills = 0;
            Coins = 0;
            pendingLevelUps = 0;

            ScrollDirector.Instance?.ResetRun();
            SetState(GameState.Playing);

            KillsChanged?.Invoke(Kills);
            CoinsChanged?.Invoke(Coins);
        }

        public void RegisterKill(int amount = 1)
        {
            Kills += amount;
            KillsChanged?.Invoke(Kills);
        }

        public void AddCoins(int amount)
        {
            if (amount == 0)
            {
                return;
            }

            Coins += amount;
            CoinsChanged?.Invoke(Coins);
        }

        /// <summary>
        /// Freezes the run so an upgrade can be chosen. Multiple level-ups in one frame
        /// are queued and presented one after another.
        /// </summary>
        public void PushLevelUpPause()
        {
            pendingLevelUps++;

            if (State == GameState.Playing)
            {
                SetState(GameState.LevelUp);
            }
        }

        /// <summary>Called once the player has picked a card. Returns true if another choice is queued.</summary>
        public bool PopLevelUpPause()
        {
            pendingLevelUps = Mathf.Max(0, pendingLevelUps - 1);

            if (pendingLevelUps > 0)
            {
                return true;
            }

            if (State == GameState.LevelUp)
            {
                SetState(GameState.Playing);
            }

            return false;
        }

        public void TriggerGameOver()
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            RecordRun();
            SetState(GameState.GameOver);
        }

        private void ResolveSelectedPlayer()
        {
            if (RunSession.SelectedCharacter != null)
            {
                selectedCharacter = RunSession.SelectedCharacter;
            }

            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>();
            }

            if (selectedCharacter != null && selectedCharacter.playerPrefab != null)
            {
                PlayerHealth existing = playerHealth;
                if (existing == null || !IsInstanceOf(existing.gameObject, selectedCharacter.playerPrefab))
                {
                    Vector3 position = existing != null ? existing.transform.position : new Vector3(0f, -5f, 0f);
                    Quaternion rotation = existing != null ? existing.transform.rotation : Quaternion.identity;
                    if (existing != null)
                    {
                        DestroyImmediate(existing.gameObject);
                    }

                    GameObject spawned = Instantiate(selectedCharacter.playerPrefab, position, rotation);
                    spawned.name = selectedCharacter.playerPrefab.name;
                    playerHealth = spawned.GetComponent<PlayerHealth>();
                }
            }

            WeaponLoadout loadout = playerHealth != null
                ? playerHealth.GetComponent<WeaponLoadout>()
                : FindAnyObjectByType<WeaponLoadout>();
            if (loadout != null && selectedCharacter != null)
            {
                loadout.BindCharacter(selectedCharacter);
            }
        }

        private static bool IsInstanceOf(GameObject instance, GameObject prefab)
        {
            if (instance == null || prefab == null)
            {
                return false;
            }

            string instanceName = instance.name.Replace("(Clone)", string.Empty).Trim();
            return instanceName == prefab.name;
        }

        private void RecordRun()
        {
            WeaponLoadout loadout = playerHealth != null
                ? playerHealth.GetComponent<WeaponLoadout>()
                : FindAnyObjectByType<WeaponLoadout>();
            CharacterDefinition character = loadout != null ? loadout.Character : selectedCharacter;
            int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;

            MetaProgress.AddCoins(Coins);
            Leaderboard.Submit(character, RunTime, Kills, Coins, level);
        }

        public void RestartRun()
        {
            Time.timeScale = 1f;
            Scene active = SceneManager.GetActiveScene();
            SceneManager.LoadScene(active.buildIndex);
        }

        public void ReturnToMenu()
        {
            if (string.IsNullOrEmpty(menuSceneName))
            {
                Debug.LogWarning("GameManager has no menu scene configured.", this);
                return;
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }

        private void OnPlayerDied()
        {
            ScrollDirector.Instance?.SetScrolling(false);
            StartCoroutine(GameOverAfterDeath());
        }

        private IEnumerator GameOverAfterDeath()
        {
            // Keep timeScale at 1 so the death clip can evaluate and play.
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, gameOverDelay));
            TriggerGameOver();
        }

        private void SetState(GameState next)
        {
            if (State == next)
            {
                return;
            }

            State = next;
            Time.timeScale = next == GameState.Playing ? 1f : 0f;

            UpdateMusic(next);
            StateChanged?.Invoke(next);
        }

        private void UpdateMusic(GameState state)
        {
            if (musicController == null)
            {
                return;
            }

            switch (state)
            {
                case GameState.Playing:
                    // Resuming from a level-up must not restart the track.
                    if (!gameplayMusicStarted)
                    {
                        gameplayMusicStarted = true;
                        musicController.SetGameplayMusic();
                    }
                    break;
                case GameState.GameOver:
                    musicController.SetGameOverMusic();
                    break;
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            // WebGL tabs lose focus constantly; make sure a paused run stays paused correctly.
            if (hasFocus && State == GameState.Playing)
            {
                Time.timeScale = 1f;
            }
        }
    }
}
