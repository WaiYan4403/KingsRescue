using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KingsRescue.Audio;
using KingsRescue.Data;
using KingsRescue.Meta;
using ToggleSwitch = Poker.UI.ToggleSwitch.ToggleSwitch;

namespace KingsRescue.UI
{
    public class MainMenuController : MonoBehaviour
    {
        [Header("Data")]
        [SerializeField] private CharacterRoster roster;
        [SerializeField] private string gameplayScene = RunSession.GameplayScene;

        [Header("Panels")]
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject characterPanel;
        [SerializeField] private GameObject upgradePanel;
        [SerializeField] private GameObject settingsPanel;
        [SerializeField] private GameObject leaderboardPanel;
        [SerializeField] private GameObject achievementPanel;
        [SerializeField] private GameObject accountPanel;
        [SerializeField] private GameObject usernamePanel;
        [SerializeField] private GameObject creditsPanel;
        [SerializeField] private GameObject quitDialog;

        [Header("Account")]
        [SerializeField] private AccountMenuView accountView;

        [Header("Character")]
        [SerializeField] private CharacterSelectButton[] characterButtons;
        [Tooltip("Hero name shown between the previous / next arrows. A locked hero also shows its unlock hint.")]
        [SerializeField] private TMP_Text characterName;
        [Tooltip("Optional long description (name, story, unique weapon).")]
        [SerializeField] private TMP_Text characterDetail;
        [SerializeField] private Button startButton;
        [SerializeField] private Button upgradesButton;
        [Tooltip("Shown for a hero who is sold for coins. The label is the price plus the coin sprite.")]
        [SerializeField] private Button characterBuyButton;
        [SerializeField] private TMP_Text characterBuyLabel;

        [Header("Upgrades")]
        [SerializeField] private TMP_Text upgradeTitle;
        [Tooltip("Coin balance under the UPGRADES title.")]
        [SerializeField] private TMP_Text bankLabel;
        [SerializeField] private UpgradeRowView[] upgradeRows;
        [SerializeField] private Button resetUpgradesButton;
        [SerializeField] private TMP_Text resetUpgradesLabel;

        [Header("Selected upgrade")]
        [SerializeField] private Image upgradeIcon;
        [SerializeField] private TMP_Text upgradeNameLabel;
        [SerializeField] private TMP_Text upgradeDescriptionLabel;
        [Tooltip("Holds the coin icon and the price. Hidden once the upgrade is maxed.")]
        [SerializeField] private GameObject upgradePriceRoot;
        [SerializeField] private TMP_Text upgradePriceLabel;
        [SerializeField] private Button upgradeBuyButton;
        [SerializeField] private TMP_Text upgradeBuyLabel;

        [Tooltip("Upgrade purchases do not spend coins. Turn this on to test the upgrade cards.")]
        [SerializeField] private bool devMode;

        [Header("Settings")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider soundSlider;
        [SerializeField] private Slider sensitivitySlider;
        [SerializeField] private ToggleSwitch screenShakeToggle;
        [SerializeField] private ToggleSwitch damageTextToggle;

        [Header("Leaderboards")]
        [SerializeField] private LeaderboardMenuView leaderboardView;

        [Header("Achievements")]
        [SerializeField] private AchievementListView achievementList;

        private CharacterDefinition selected;
        private StatType selectedStat;
        private bool hasSelectedStat;
        private GameObject currentPanel;
        private GameObject requestedPanel;
        private SoundId requestedOpenSound;
        private Coroutine panelTransition;
        private bool panelShown;

        public CharacterDefinition Selected => selected;
        public StatType SelectedUpgrade => selectedStat;

        private void OnValidate()
        {
            MetaProgress.DevMode = devMode;
            if (Application.isPlaying)
            {
                RefreshUpgrades();
            }
        }

        private void Awake()
        {
            Time.timeScale = 1f;
            MetaProgress.DevMode = devMode;
            GameSettings.Apply();

            if (roster == null)
            {
                roster = Resources.Load<CharacterRoster>("CharacterRoster");
            }

            SyncSettingsControls();

            if (resetUpgradesButton != null)
            {
                resetUpgradesButton.onClick.AddListener(ResetSelectedUpgrades);
                SoundEffects.UseButtonSound(resetUpgradesButton, SoundId.UiReset);
            }

            if (upgradeBuyButton != null)
            {
                upgradeBuyButton.onClick.AddListener(BuySelectedUpgrade);
                SoundEffects.UseButtonSound(upgradeBuyButton, SoundId.UiBuy);
            }

            AchievementProgress.EnsureLoaded();

            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] != null)
                    {
                        characterButtons[i].ApplyLabel();
                    }
                }
            }

            if (selected == null || !CanPlay(selected))
            {
                SelectDefault();
            }

            RefreshCharacter();
        }

        void Start()
        {
            // First launch (or an unfinished sign-up) lands on the account screens instead of home.
            if (accountView != null && accountView.TryBeginSetup())
            {
                return;
            }

            ShowHome();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame)
            {
                return;
            }

            // Escape is also the Android back key.
            if (quitDialog != null && quitDialog.activeSelf)
            {
                CloseQuitConfirm();
                return;
            }

            if (currentPanel == homePanel && currentPanel != null)
            {
                if (Application.platform != RuntimePlatform.WebGLPlayer)
                {
                    OpenQuitConfirm();
                }

                return;
            }

            GoBack();
        }

        private void OnDestroy()
        {
            if (resetUpgradesButton != null)
            {
                resetUpgradesButton.onClick.RemoveListener(ResetSelectedUpgrades);
            }

            if (upgradeBuyButton != null)
            {
                upgradeBuyButton.onClick.RemoveListener(BuySelectedUpgrade);
            }
        }

        // ---------------------------------------------------------------- navigation

        public void ShowHome()
        {
            Show(homePanel);
            MenuMusicController.Instance.SetMainMusic();
        }

        public void ShowAccount()
        {
            if (accountView != null)
            {
                accountView.RefreshAccount();
            }

            Show(accountPanel, SoundId.MenuOpen);
            //MenuMusicController.Instance.SetMainMusic();
        }

        public void ShowUsername()
        {
            if (accountView != null)
            {
                accountView.RefreshUsername();
            }

            Show(usernamePanel, SoundId.MenuOpen);
            //MenuMusicController.Instance.SetMainMusic();
        }

        public void ShowCharacters()
        {
            OpenCharacters(SoundId.MenuOpen);
        }

        public void ShowUpgrades()
        {
            RefreshUpgrades();
            Show(upgradePanel, SoundId.MenuOpen);
            //MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowSettings()
        {
            SyncSettingsControls();
            Show(settingsPanel, SoundId.MenuOpenGold);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowLeaderboards()
        {
            Show(leaderboardPanel, SoundId.MenuOpenGold);
            if (leaderboardView != null)
            {
                leaderboardView.Refresh();
            }

            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowCredits()
        {
            Show(creditsPanel, SoundId.MenuOpenGold);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowAchievements()
        {
            if (achievementList != null)
            {
                achievementList.Refresh();
            }

            Show(achievementPanel, SoundId.MenuOpenGold);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        private void OpenCharacters(SoundId openSound)
        {
            RefreshCharacter();
            Show(characterPanel, openSound);
            MenuMusicController.Instance.SetCharacterMusic();
        }

        /// <summary>
        /// Back button: the upgrades page returns to hero selection, every other page returns to the
        /// main menu. The account screens have their own back handling.
        /// </summary>
        public void GoBack()
        {
            if (currentPanel == null || currentPanel == homePanel
                || currentPanel == accountPanel || currentPanel == usernamePanel)
            {
                return;
            }

            SoundEffects.Play(SoundId.UiBack);
            if (currentPanel == upgradePanel)
            {
                OpenCharacters(SoundId.None);
                return;
            }

            ShowHome();
        }

        public void OpenQuitConfirm()
        {
            if (quitDialog == null || quitDialog.activeSelf)
            {
                return;
            }

            quitDialog.SetActive(true);
            quitDialog.transform.SetAsLastSibling();
            SoundEffects.Play(SoundId.MenuOpen);
        }

        public void CloseQuitConfirm()
        {
            if (quitDialog == null || !quitDialog.activeSelf)
            {
                return;
            }

            SoundEffects.Play(SoundId.UiBack);
            SoundEffects.Play(SoundId.MenuOpen);
            quitDialog.SetActive(false);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void PlaySelected()
        {
            if (!CanPlay(selected))
            {
                ShowCharacters();
                return;
            }

            RunSession.SelectedCharacter = selected;
            Time.timeScale = 1f;
            MenuMusicController.Instance.StopMusic();
            SceneManager.LoadScene(gameplayScene);
        }

        // ---------------------------------------------------------------- characters

        public void SelectCharacter(CharacterDefinition character)
        {
            if (character == null)
            {
                return;
            }

            selected = character;
            RefreshCharacter();

            if (currentPanel == upgradePanel)
            {
                RefreshUpgrades();
            }
        }

        /// <summary>Right arrow on the hero selection page.</summary>
        public void SelectNextCharacter()
        {
            StepCharacter(1);
        }

        /// <summary>Left arrow on the hero selection page.</summary>
        public void SelectPreviousCharacter()
        {
            StepCharacter(-1);
        }

        private void StepCharacter(int direction)
        {
            List<CharacterDefinition> heroes = CollectHeroes();
            if (heroes.Count == 0)
            {
                return;
            }

            int index = selected != null ? heroes.IndexOf(selected) : -1;
            index = index < 0
                ? (direction > 0 ? 0 : heroes.Count - 1)
                : (index + direction + heroes.Count) % heroes.Count;

            SelectCharacter(heroes[index]);
        }

        /// <summary>Heroes in the order the portraits appear on screen, locked ones included.</summary>
        private List<CharacterDefinition> CollectHeroes()
        {
            var heroes = new List<CharacterDefinition>();

            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    CharacterDefinition hero = characterButtons[i] != null ? characterButtons[i].Character : null;
                    if (hero != null && !heroes.Contains(hero))
                    {
                        heroes.Add(hero);
                    }
                }
            }

            if (heroes.Count == 0 && roster != null && roster.characters != null)
            {
                for (int i = 0; i < roster.characters.Length; i++)
                {
                    if (roster.characters[i] != null && !heroes.Contains(roster.characters[i]))
                    {
                        heroes.Add(roster.characters[i]);
                    }
                }
            }

            return heroes;
        }

        private static bool CanPlay(CharacterDefinition character)
        {
            return character != null
                && !character.comingSoon
                && AchievementProgress.IsCharacterUnlocked(character);
        }

        private void SelectDefault()
        {
            List<CharacterDefinition> heroes = CollectHeroes();
            for (int i = 0; i < heroes.Count; i++)
            {
                if (CanPlay(heroes[i]))
                {
                    selected = heroes[i];
                    return;
                }
            }
        }

        private void RefreshCharacter()
        {
            bool comingSoon = selected != null && selected.comingSoon;
            bool unlocked = CanPlay(selected);

            if (characterName != null)
            {
                if (selected == null)
                {
                    characterName.text = "No heroes";
                }
                else if (comingSoon)
                {
                    characterName.text = $"{selected.displayName}\n<size=60%>Coming Soon</size>";
                }
                else if (unlocked)
                {
                    characterName.text = selected.displayName;
                }
                else if (selected.unlockCost > 0)
                {
                    characterName.text = selected.displayName;
                }
                else
                {
                    characterName.text = $"{selected.displayName}\n<size=60%>{AchievementCatalog.UnlockHint(selected)}</size>";
                }
            }

            if (characterDetail != null)
            {
                if (selected == null)
                {
                    characterDetail.text = "No heroes in the roster.";
                }
                else
                {
                    string weapon = selected.uniqueWeapon != null ? selected.uniqueWeapon.displayName : "None";
                    string body = $"{selected.displayName}\n{selected.description}\n\nUnique weapon: {weapon}";
                    if (comingSoon)
                    {
                        body = $"{selected.displayName}\n\nComing Soon";
                    }
                    else if (!unlocked && selected.unlockCost <= 0)
                    {
                        AchievementDefinition unlock = AchievementCatalog.FindUnlockForCharacter(selected);
                        string hint = unlock != null ? unlock.description : "Complete an achievement to unlock this hero.";
                        body += $"\n\nLOCKED — {hint}";
                    }

                    characterDetail.text = body;
                }
            }

            if (startButton != null)
            {
                startButton.interactable = unlocked;
            }

            if (upgradesButton != null)
            {
                upgradesButton.interactable = unlocked;
            }

            bool forSale = selected != null && !comingSoon && !unlocked && selected.unlockCost > 0;
            if (characterBuyButton != null)
            {
                characterBuyButton.gameObject.SetActive(forSale);
                characterBuyButton.interactable = forSale;
            }

            if (characterBuyLabel != null && forSale)
            {
                characterBuyLabel.text = $"Buy {{{selected.unlockCost}<sprite=0>}}";
            }

            if (characterButtons == null)
            {
                return;
            }

            for (int i = 0; i < characterButtons.Length; i++)
            {
                if (characterButtons[i] != null)
                {
                    characterButtons[i].Refresh(characterButtons[i].Character == selected);
                }
            }
        }

        // ---------------------------------------------------------------- upgrades

        /// <summary>A card was clicked. Shows its details, price and Buy button.</summary>
        public void SelectUpgrade(StatType stat)
        {
            selectedStat = stat;
            hasSelectedStat = true;
            RefreshUpgrades();
        }

        /// <summary>Spends the selected hero's coin price and unlocks them.</summary>
        public void BuySelectedCharacter()
        {
            if (selected == null || !AchievementProgress.TryPurchaseCharacter(selected))
            {
                RefreshCharacter();
                return;
            }

            RefreshCharacter();
        }

        public void BuySelectedUpgrade()
        {
            if (selected == null || !hasSelectedStat)
            {
                return;
            }

            if (MetaProgress.TryPurchase(selected, selectedStat))
            {
                RefreshUpgrades();
            }
        }

        public void RefreshUpgrades()
        {
            EnsureSelectedUpgrade();

            if (upgradeTitle != null)
            {
                upgradeTitle.text = selected != null
                    ? $"{selected.displayName.ToUpper()} UPGRADES"
                    : "UPGRADES";
            }

            if (bankLabel != null)
            {
                bankLabel.text = devMode
                    ? $"{MetaProgress.Coins}  ·  DEV"
                    : MetaProgress.Coins.ToString();
            }

            if (upgradeRows != null)
            {
                for (int i = 0; i < upgradeRows.Length; i++)
                {
                    if (upgradeRows[i] != null)
                    {
                        upgradeRows[i].Refresh(selected, hasSelectedStat && upgradeRows[i].Stat == selectedStat);
                    }
                }
            }

            RefreshUpgradeDetail();

            int refund = MetaProgress.SpentOnCharacter(selected);
            if (resetUpgradesButton != null)
            {
                resetUpgradesButton.interactable = selected != null && refund > 0;
            }

            if (resetUpgradesLabel != null)
            {
                resetUpgradesLabel.text = refund > 0
                    ? $"RESET  (refund {refund} coins)"
                    : "RESET UPGRADES";
            }
        }

        private void EnsureSelectedUpgrade()
        {
            if (hasSelectedStat)
            {
                return;
            }

            if (upgradeRows != null)
            {
                for (int i = 0; i < upgradeRows.Length; i++)
                {
                    if (upgradeRows[i] != null)
                    {
                        selectedStat = upgradeRows[i].Stat;
                        hasSelectedStat = true;
                        return;
                    }
                }
            }
        }

        private void RefreshUpgradeDetail()
        {
            if (!hasSelectedStat || !MetaUpgradeCatalog.TryGet(selectedStat, out MetaUpgradeOffer offer))
            {
                return;
            }

            int level = selected != null ? MetaProgress.GetLevel(selected, selectedStat) : 0;
            level = Mathf.Min(level, offer.maxLevel);
            bool maxed = level >= offer.maxLevel;
            int cost = offer.CostForNext(level);
            bool canPay = MetaProgress.DevMode || MetaProgress.Coins >= cost;

            if (upgradeNameLabel != null)
            {
                upgradeNameLabel.text = StatTypes.DisplayName(selectedStat);
            }

            if (upgradeDescriptionLabel != null)
            {
                float owned = offer.ModifierForLevels(level).value;
                float shown = owned;
                if (!maxed)
                {
                    float next = offer.ModifierForLevels(level + 1).value;
                    shown = next - owned;
                    if (shown <= 0f)
                    {
                        shown = next;
                    }
                }

                string bonus = BonusText(offer, shown);
                upgradeDescriptionLabel.text = level > 0 ? $"{bonus}\nLevel {level}" : bonus;
            }

            if (upgradeIcon != null)
            {
                Sprite icon = IconFor(selectedStat);
                if (icon != null)
                {
                    upgradeIcon.sprite = icon;
                }
            }

            if (upgradePriceRoot != null)
            {
                upgradePriceRoot.SetActive(!maxed);
            }

            if (upgradePriceLabel != null)
            {
                upgradePriceLabel.text = maxed ? "MAX" : cost.ToString();
            }

            if (upgradeBuyLabel != null)
            {
                upgradeBuyLabel.text = maxed ? "MAXED" : "BUY";
            }

            if (upgradeBuyButton != null)
            {
                upgradeBuyButton.interactable = selected != null && !maxed && canPay;
            }
        }

        private Sprite IconFor(StatType stat)
        {
            if (upgradeRows == null)
            {
                return null;
            }

            for (int i = 0; i < upgradeRows.Length; i++)
            {
                if (upgradeRows[i] != null && upgradeRows[i].Stat == stat)
                {
                    return upgradeRows[i].Icon;
                }
            }

            return null;
        }

        private static string BonusText(MetaUpgradeOffer offer, float total)
        {
            string name = ShortStatName(offer.stat);
            if (offer.stat == StatType.AttackSpeed)
            {
                return $"-{total * 100f:0}% {name}";
            }

            if (offer.mode == ModifierMode.Percent)
            {
                return $"+{total * 100f:0}% {name}";
            }

            return $"+{total:0.##} {name}";
        }

        private static string ShortStatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.MaxHp: return "HP";
                case StatType.MoveSpeed: return "speed";
                case StatType.Damage: return "damage";
                case StatType.AttackSpeed: return "cooldown";
                case StatType.Area: return "area";
                case StatType.ProjectileSpeed: return "shot speed";
                case StatType.ProjectileCount: return "amount";
                case StatType.Pierce: return "pierce";
                case StatType.MagnetRadius: return "magnet";
                case StatType.XpGain: return "growth";
                case StatType.CoinGain: return "greed";
                case StatType.Armor: return "armor";
                case StatType.HpRegen: return "recovery";
                case StatType.Luck: return "luck";
                case StatType.Duration: return "duration";
                default: return StatTypes.DisplayName(stat);
            }
        }

        public void ResetSelectedUpgrades()
        {
            if (selected == null)
            {
                return;
            }

            MetaProgress.ResetCharacter(selected);
            RefreshUpgrades();
        }

        // ---------------------------------------------------------------- settings

        public void OnMusicChanged(float value)
        {
            GameSettings.MusicVolume = value;
        }

        public void OnSoundChanged(float value)
        {
            GameSettings.SoundVolume = value;
        }

        public void OnSensitivityChanged(float value)
        {
            GameSettings.JoystickSensitivity = value;
        }

        public void OnScreenShakeChanged(bool value)
        {
            GameSettings.ScreenShake = value;
        }

        public void OnDamageTextChanged(bool value)
        {
            GameSettings.ShowDamageText = value;
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

        // ---------------------------------------------------------------- panels

        private void Show(GameObject panel, SoundId openSound = SoundId.None)
        {
            requestedPanel = panel;
            requestedOpenSound = openSound;
            if (panelShown && currentPanel == panel && panelTransition == null)
            {
                requestedOpenSound = SoundId.None;
                return;
            }

            if (panelTransition == null)
            {
                panelTransition = StartCoroutine(RunPanelTransition());
            }
        }

        /// <summary>
        /// Settings, leaderboards, achievements and credits play a close clip. The panel stays
        /// active until that clip finishes, then the next screen is shown.
        /// </summary>
        private IEnumerator RunPanelTransition()
        {
            while (!panelShown || currentPanel != requestedPanel)
            {
                GameObject target = requestedPanel;
                SoundId closingSound = SoundId.None;
                bool animatedClose = false;

                if (panelShown && currentPanel != null && currentPanel != target && currentPanel.activeSelf)
                {
                    closingSound = SoundFor(currentPanel);
                    animatedClose = PanelClose.TryPlay(currentPanel, out float duration);
                    if (closingSound != SoundId.None)
                    {
                        SoundEffects.Play(closingSound);
                    }

                    if (animatedClose)
                    {
                        float waited = 0f;
                        while (waited < duration)
                        {
                            waited += Time.unscaledDeltaTime;
                            yield return null;
                        }

                        if (currentPanel != null)
                        {
                            currentPanel.SetActive(false);
                        }
                    }
                }

                if (requestedPanel != target)
                {
                    continue;
                }

                SoundId openSound = requestedOpenSound;
                ApplyPanels(target);
                currentPanel = target;
                panelShown = true;
                requestedOpenSound = SoundId.None;
                // A plain panel swaps in one moment, so the same clip is not played twice.
                if (openSound != SoundId.None && (animatedClose || openSound != closingSound))
                {
                    SoundEffects.Play(openSound);
                }
            }

            panelTransition = null;
        }

        private SoundId SoundFor(GameObject panel)
        {
            if (panel == settingsPanel || panel == leaderboardPanel || panel == achievementPanel || panel == creditsPanel)
            {
                return SoundId.MenuOpenGold;
            }

            if (panel == characterPanel || panel == upgradePanel || panel == accountPanel || panel == usernamePanel)
            {
                return SoundId.MenuOpen;
            }

            return SoundId.None;
        }

        private void ApplyPanels(GameObject panel)
        {
            SetActive(homePanel, panel);
            SetActive(characterPanel, panel);
            SetActive(upgradePanel, panel);
            SetActive(settingsPanel, panel);
            SetActive(leaderboardPanel, panel);
            SetActive(achievementPanel, panel);
            SetActive(accountPanel, panel);
            SetActive(usernamePanel, panel);
            SetActive(creditsPanel, panel);
            SetCharacterStage(panel == characterPanel || panel == upgradePanel);
            CloseQuitConfirm();
        }

        private void SetCharacterStage(bool visible)
        {
            if (characterPanel == null)
            {
                return;
            }

            Transform stage = characterPanel.transform.parent;
            if (stage == null || stage.GetComponent<Canvas>() != null)
            {
                return;
            }

            stage.gameObject.SetActive(visible);
        }

        private static void SetActive(GameObject target, GameObject visible)
        {
            if (target != null)
            {
                target.SetActive(target == visible);
            }
        }
    }
}
