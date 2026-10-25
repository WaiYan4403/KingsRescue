using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Combat;
using KingsRescue.Core;
using KingsRescue.Data;
using KingsRescue.Meta;
using KingsRescue.Player;
using KingsRescue.Progression;

namespace KingsRescue.UI
{
    /// <summary>
    /// Run summary shown when the hero's HP hits zero.
    /// </summary>
    public class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        [Header("Hero")]
        [SerializeField] private Image portrait;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text levelLabel;

        [Header("Run")]
        [SerializeField] private TMP_Text distanceLabel;
        [SerializeField] private TMP_Text coinsLabel;
        [SerializeField] private TMP_Text enemiesLabel;
        [SerializeField] private TMP_Text bossesLabel;
        [SerializeField] private TMP_Text healthLabel;
        [SerializeField] private TMP_Text pocketsLabel;
        [SerializeField] private TMP_Text powerUpsLabel;
        [SerializeField] private TMP_Text powerUpsMaxLabel;

        [Header("Loadout")]
        [SerializeField] private ResultItemSlot[] weaponSlots;
        [SerializeField] private ResultItemSlot[] passiveSlots;

        [Header("Achievements earned this run")]
        [SerializeField] private GameObject achievementRoot;
        [SerializeField] private Image achievementIcon;
        [SerializeField] private TMP_Text achievementObjective;
        [SerializeField] private TMP_Text achievementReward;
        [SerializeField] private Toggle achievementUnlocked;
        [SerializeField] private Sprite coinIcon;
        [Tooltip("How long each new achievement stays up before the next one, when the run earned more than one.")]
        [SerializeField] private float achievementDuration = 2.5f;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;

        [SerializeField] private Animator animator;

        [SerializeField] private float showDelay = 1f;

        private readonly List<AchievementDefinition> earned = new List<AchievementDefinition>(4);
        private readonly List<WeaponRuntime> weapons = new List<WeaponRuntime>(4);
        private readonly List<PassiveDefinition> passives = new List<PassiveDefinition>(4);
        private readonly List<int> passiveLevels = new List<int>(4);
        private Coroutine openRoutine;
        private Coroutine achievementRoutine;

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

            if (openRoutine != null && GameManager.Instance != null)
            {
                GameManager.Instance.StopCoroutine(openRoutine);
                openRoutine = null;
            }
        }

        private void OnDisable()
        {
            StopAchievementCycle();
        }

        private void OnStateChanged(GameState state)
        {
            if (state != GameState.GameOver)
            {
                return;
            }

            GameManager game = GameManager.Instance;
            if (game == null || showDelay <= 0f)
            {
                Show();
                return;
            }

            // The panel object is inactive, and timeScale is 0, so the wait has to
            // run on the manager in real time.
            if (openRoutine != null)
            {
                game.StopCoroutine(openRoutine);
            }

            openRoutine = game.StartCoroutine(ShowAfterDelay());
        }

        private IEnumerator ShowAfterDelay()
        {
            yield return new WaitForSecondsRealtime(showDelay);
            openRoutine = null;
            Show();
        }

        private void Show()
        {
            Populate();
            if (achievementRoot != null)
            {
                achievementRoot.SetActive(earned.Count > 0);
            }

            root.transform.SetAsLastSibling();
            root.SetActive(true);
            if (animator != null)
            {
                animator.SetTrigger("Open");
            }

            BeginAchievementCycle();
        }

        private void Populate()
        {
            GameManager game = GameManager.Instance;
            CharacterDefinition character = game != null && game.Loadout != null
                ? game.Loadout.Character
                : null;
            int level = ExperienceSystem.Instance != null ? ExperienceSystem.Instance.Level : 1;

            if (portrait != null)
            {
                Sprite face = HeroPortrait(character);
                portrait.sprite = face;
                portrait.enabled = face != null;
                portrait.preserveAspect = true;
            }

            if (nameLabel != null)
            {
                nameLabel.text = character != null ? character.displayName : "Hero";
            }

            if (levelLabel != null)
            {
                levelLabel.text = $"Lvl {level}";
            }

            SetAmount(distanceLabel, HudView.FormatDistance(ScrollDirector.DistanceMeters));
            SetAmount(coinsLabel, (game != null ? game.Coins : 0).ToString());
            int bosses = game != null ? game.BossKills : 0;
            int kills = game != null ? game.Kills : 0;
            SetAmount(enemiesLabel, Mathf.Max(0, kills - bosses).ToString());
            SetAmount(bossesLabel, bosses.ToString());
            SetAmount(healthLabel, (game != null ? game.HealthPickups : 0).ToString());
            SetAmount(pocketsLabel, (game != null ? game.Pockets : 0).ToString());
            SetAmount(powerUpsLabel, (game != null ? game.PowerUps : 0).ToString());
            SetAmount(powerUpsMaxLabel, (game != null ? game.PowerUpsMax : 0).ToString());

            FillWeapons(game != null ? game.Loadout : null);
            FillPassives(game != null ? game.Loadout : null);
            CollectEarned();
        }

        private static void SetAmount(TMP_Text label, string value)
        {
            if (label != null)
            {
                label.text = value;
            }
        }

        private static Sprite HeroPortrait(CharacterDefinition character)
        {
            if (character != null && character.portrait != null)
            {
                Texture texture = character.portrait.texture;
                if (texture == null || texture.name.IndexOf("Slime") < 0)
                {
                    return character.portrait;
                }
            }

            PlayerController hero = PlayerController.Instance;
            if (hero == null)
            {
                return character != null ? character.portrait : null;
            }

            Sprite best = null;
            float bestSize = 0f;
            SpriteRenderer[] renderers = hero.GetComponentsInChildren<SpriteRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Sprite sprite = renderers[i] != null ? renderers[i].sprite : null;
                if (sprite == null)
                {
                    continue;
                }

                float size = sprite.rect.width * sprite.rect.height;
                if (size > bestSize)
                {
                    best = sprite;
                    bestSize = size;
                }
            }

            return best != null ? best : (character != null ? character.portrait : null);
        }

        private void FillWeapons(WeaponLoadout loadout)
        {
            weapons.Clear();
            loadout?.CollectWeapons(weapons);
            FillSlots(weaponSlots, weapons.Count, i => weapons[i].Definition != null ? weapons[i].Definition.icon : null, i => weapons[i].Level);
        }

        private void FillPassives(WeaponLoadout loadout)
        {
            passives.Clear();
            passiveLevels.Clear();
            loadout?.CollectPassives(passives, passiveLevels);
            FillSlots(passiveSlots, passives.Count, i => passives[i] != null ? passives[i].icon : null, i => passiveLevels[i]);
        }

        private static void FillSlots(ResultItemSlot[] slots, int count, System.Func<int, Sprite> icon, System.Func<int, int> level)
        {
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    continue;
                }

                if (i < count)
                {
                    slots[i].Show(icon(i), level(i));
                }
                else
                {
                    slots[i].Clear();
                }
            }
        }

        private void CollectEarned()
        {
            earned.Clear();
            IReadOnlyList<AchievementDefinition> run = AchievementProgress.EarnedThisRun;
            for (int i = 0; i < run.Count; i++)
            {
                if (run[i] != null)
                {
                    earned.Add(run[i]);
                }
            }
        }

        private void BeginAchievementCycle()
        {
            StopAchievementCycle();
            if (achievementRoot != null)
            {
                achievementRoot.SetActive(earned.Count > 0);
            }

            if (earned.Count == 0)
            {
                return;
            }

            achievementRoutine = StartCoroutine(CycleAchievements());
        }

        private void StopAchievementCycle()
        {
            if (achievementRoutine != null)
            {
                StopCoroutine(achievementRoutine);
                achievementRoutine = null;
            }
        }

        private IEnumerator CycleAchievements()
        {
            int index = 0;
            while (true)
            {
                ShowAchievement(earned[index]);
                if (earned.Count < 2)
                {
                    yield break;
                }

                yield return new WaitForSecondsRealtime(Mathf.Max(0.5f, achievementDuration));
                index = (index + 1) % earned.Count;
            }
        }

        private void ShowAchievement(AchievementDefinition achievement)
        {
            if (achievementObjective != null)
            {
                achievementObjective.text = achievement.description;
            }

            if (achievementReward != null)
            {
                string reward = achievement.RewardSummary();
                achievementReward.text = reward == "Glory" ? reward : $"Unlock: {reward}";
            }

            if (achievementIcon != null)
            {
                Sprite icon = achievement.RewardIcon(coinIcon);
                achievementIcon.sprite = icon;
                achievementIcon.enabled = icon != null;
                achievementIcon.preserveAspect = true;
            }

            if (achievementUnlocked != null)
            {
                achievementUnlocked.SetIsOnWithoutNotify(true);
            }
        }

        private void OnRestart()
        {
            StopAchievementCycle();
            GameManager.Instance?.RestartRun();
            GameMusicController.Instance.StopMusic();
            StartCoroutine(DelayClose());
        }

        private void OnMenu()
        {
            StopAchievementCycle();
            GameManager.Instance?.ReturnToMenu();
            GameMusicController.Instance.StopMusic();
            StartCoroutine(DelayClose());
        }

        private IEnumerator DelayClose()
        {
            if (animator != null)
            {
                animator.SetTrigger("Close");
            }

            yield return new WaitForSecondsRealtime(0.45f);
            root.SetActive(false);
        }
    }
}
