using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using KingsRescue.Combat;
using KingsRescue.Data;
using KingsRescue.Enemies;
using KingsRescue.Meta;
using KingsRescue.Online;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.Core
{
    public enum GameState
    {
        Boot,
        Intro,
        Playing,
        LevelUp,
        Chest,
        Paused,
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
        private GameState resumeState = GameState.Playing;

        public GameState State { get; private set; } = GameState.Boot;

        public float RunTime { get; private set; }

        public int Kills { get; private set; }

        public int BossKills { get; private set; }

        public int Coins { get; private set; }

        public int HealthPickups { get; private set; }

        public int Pockets { get; private set; }

        public int PowerUps { get; private set; }

        public int PowerUpsMax { get; private set; }

        public bool IsPlaying => State == GameState.Playing && (playerHealth == null || playerHealth.IsAlive);

        /// <summary>
        /// True while a boss encounter owns the field. The road stops, regular waves
        /// pause, and leftover trash is cleared until the boss dies.
        /// </summary>
        public bool BossFightActive { get; private set; }

        /// <summary>
        /// Level-up cards are only legal while the hero is alive. Death and game-over
        /// both suppress them so the overlay cannot cover the run summary.
        /// </summary>
        public bool CanOfferLevelUp =>
            State != GameState.GameOver
            && State != GameState.Boot
            && State != GameState.Intro
            && State != GameState.Paused
            && (playerHealth == null || playerHealth.IsAlive);

        public CharacterDefinition SelectedCharacter => selectedCharacter;

        /// <summary>The hero for this run. Resolved in Awake, before any other system runs.</summary>
        public PlayerHealth PlayerHealth => playerHealth;

        public PlayerStats PlayerStats { get; private set; }

        public WeaponLoadout Loadout { get; private set; }

        public event Action<GameState> StateChanged;

        public event Action<int> KillsChanged;

        public event Action<int> CoinsChanged;

        /// <summary>Fired as soon as HP hits zero, before the delayed game-over screen.</summary>
        public event Action HeroDied;

        protected override void Awake()
        {
            base.Awake();

            GameSettings.Apply();
            ResolveSelectedPlayer();
            Time.timeScale = 0f;
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
            StartCoroutine(StartWhenPoolsReady());
        }

        private IEnumerator StartWhenPoolsReady()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            while (pool != null && !pool.IsReady)
            {
                yield return null;
            }

            // The road tiles spawn on this frame, still under the loading screen.
            yield return null;
            pool?.DismissLoading();
            StartRun();
        }

        private void Update()
        {
            HitFlash.Tick();
            EnemyDropper.TickPending();

            if (State == GameState.Playing && !BossFightActive)
            {
                RunTime += Time.deltaTime;
            }
        }

        public void StartRun()
        {
            RunTime = 0f;
            Kills = 0;
            BossKills = 0;
            Coins = 0;
            HealthPickups = 0;
            Pockets = 0;
            PowerUps = 0;
            PowerUpsMax = 0;
            pendingLevelUps = 0;
            BossFightActive = false;
            AchievementProgress.BeginRun();

            ScrollDirector.Instance?.ResetRun();
            ScrollDirector.Instance?.SetScrolling(false);
            SetState(GameState.Intro);

            KillsChanged?.Invoke(Kills);
            CoinsChanged?.Invoke(Coins);

            if (PlayerController.Instance != null)
            {
                PlayerController.Instance.BeginRunEntrance();
            }
            else
            {
                BeginField();
            }
        }

        /// <summary>
        /// The hero has reached the stand mark. The road, the clock, and spawns start together.
        /// </summary>
        public void BeginField()
        {
            if (State == GameState.Playing || State == GameState.GameOver)
            {
                return;
            }

            ScrollDirector.Instance?.SetScrolling(true);
            SetState(GameState.Playing);
        }

        /// <summary>Freezes the run and remembers whether it was still the walk-in.</summary>
        public void PauseRun()
        {
            if (State != GameState.Playing && State != GameState.Intro)
            {
                return;
            }

            resumeState = State;
            SetState(GameState.Paused);
        }

        /// <summary>Continues the run from the state it had when pause opened.</summary>
        public void ResumeRun()
        {
            if (State != GameState.Paused)
            {
                return;
            }

            SetState(resumeState == GameState.Intro ? GameState.Intro : GameState.Playing);
        }

        /// <summary>Stops the road. Regulars already on the field stay; new trash is hushed by WaveDirector.</summary>
        public void BeginBossFight()
        {
            if (BossFightActive || State == GameState.GameOver)
            {
                return;
            }

            BossFightActive = true;
            ScrollDirector.Instance?.SetScrolling(false);
        }

        /// <summary>Hands the road back to the run after the boss is gone.</summary>
        public void EndBossFight()
        {
            if (!BossFightActive)
            {
                return;
            }

            BossFightActive = false;

            bool heroAlive = playerHealth == null || playerHealth.IsAlive;
            if (State != GameState.GameOver && heroAlive)
            {
                ScrollDirector.Instance?.SetScrolling(true);
            }
        }

        public void RegisterKill(int amount = 1, bool boss = false)
        {
            Kills += amount;
            if (boss)
            {
                BossKills += amount;
            }

            KillsChanged?.Invoke(Kills);
        }

        public void RegisterHealthPickup()
        {
            HealthPickups++;
        }

        public void RegisterPocket()
        {
            Pockets++;
        }

        public void RegisterPowerUp(bool maxForm)
        {
            if (maxForm)
            {
                PowerUpsMax++;
            }
            else
            {
                PowerUps++;
            }
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
            if (!CanOfferLevelUp)
            {
                return;
            }

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

        /// <summary>Freezes the run for the chest reveal overlay.</summary>
        public void PushChestPause()
        {
            if (State == GameState.GameOver || State == GameState.Boot)
            {
                return;
            }

            if (playerHealth != null && !playerHealth.IsAlive)
            {
                return;
            }

            if (State == GameState.Playing)
            {
                SetState(GameState.Chest);
            }
        }

        public void PopChestPause()
        {
            if (State != GameState.Chest)
            {
                return;
            }

            SetState(GameState.Playing);
        }

        public void TriggerGameOver()
        {
            if (State == GameState.GameOver)
            {
                return;
            }

            pendingLevelUps = 0;
            RecordRun();
            SetState(GameState.GameOver);
        }

        private void ResolveSelectedPlayer()
        {
            if (RunSession.SelectedCharacter != null)
            {
                selectedCharacter = RunSession.SelectedCharacter;
            }

            if (playerHealth == null && PlayerController.Instance != null)
            {
                playerHealth = PlayerController.Instance.Health;
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

                    ObjectPoolManager pool = ObjectPoolManager.Instance;
                    GameObject spawned = pool != null
                        ? pool.SpawnBeforeRun(selectedCharacter.playerPrefab, position, rotation)
                        : null;
                    if (spawned != null)
                    {
                        playerHealth = spawned.GetComponent<PlayerHealth>();
                    }
                    else
                    {
                        Debug.LogError("GameManager could not create the selected hero through ObjectPoolManager.", this);
                    }
                }
            }

            PlayerStats = playerHealth != null ? playerHealth.GetComponent<PlayerStats>() : null;
            Loadout = playerHealth != null ? playerHealth.GetComponent<WeaponLoadout>() : null;
            if (Loadout != null && selectedCharacter != null)
            {
                Loadout.BindCharacter(selectedCharacter);
            }

            if (playerHealth != null)
            {
                playerHealth.ApplyCharacter(selectedCharacter);
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
            CharacterDefinition character = Loadout != null ? Loadout.Character : selectedCharacter;
            int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;

            MetaProgress.AddCoins(Coins);
            AchievementProgress.RecordSurvive(RunTime);
            Leaderboard.Submit(character, RunTime, Kills, Coins, level);
            OnlineServices.NotifyRunFinished(RunTime);
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
            pendingLevelUps = 0;
            HeroDied?.Invoke();
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
            Time.timeScale = next == GameState.Playing || next == GameState.Intro ? 1f : 0f;

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
                case GameState.Intro:
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
            if (hasFocus && (State == GameState.Playing || State == GameState.Intro))
            {
                Time.timeScale = 1f;
            }
        }
    }
}
