using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

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
        [SerializeField] private TMP_Text characterDetail;

        [Header("Upgrades")]
        [SerializeField] private TMP_Text upgradeTitle;
        [SerializeField] private TMP_Text bankLabel;
        [SerializeField] private UpgradeRowView[] upgradeRows;
        [SerializeField] private Button resetUpgradesButton;
        [SerializeField] private TMP_Text resetUpgradesLabel;

        [Tooltip("Upgrade purchases do not spend coins. Turn this on to test the upgrade rows.")]
        [SerializeField] private bool devMode;

        [Header("Settings")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider soundSlider;
        [SerializeField] private Toggle screenShakeToggle;
        [SerializeField] private Toggle damageTextToggle;

        [Header("Leaderboards")]
        [SerializeField] private LeaderboardMenuView leaderboardView;

        [Header("Achievements")]
        [SerializeField] private AchievementListView achievementList;

        private CharacterDefinition selected;

        public CharacterDefinition Selected => selected;

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

            if (selected == null || !AchievementProgress.IsCharacterUnlocked(selected))
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

        private void OnDestroy()
        {
            if (resetUpgradesButton != null)
            {
                resetUpgradesButton.onClick.RemoveListener(ResetSelectedUpgrades);
            }
        }

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

            Show(accountPanel);
            //MenuMusicController.Instance.SetMainMusic();
        }

        public void ShowUsername()
        {
            if (accountView != null)
            {
                accountView.RefreshUsername();
            }

            Show(usernamePanel);
            //MenuMusicController.Instance.SetMainMusic();
        }

        public void ShowCharacters()
        {
            RefreshCharacter();
            Show(characterPanel);
            MenuMusicController.Instance.SetCharacterMusic();
        }

        public void ShowUpgrades()
        {
            RefreshUpgrades();
            Show(upgradePanel);
            //MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowSettings()
        {
            SyncSettingsControls();
            Show(settingsPanel);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowLeaderboards()
        {
            Show(leaderboardPanel);
            if (leaderboardView != null)
            {
                leaderboardView.Refresh();
            }

            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowCredits()
        {
            Show(creditsPanel);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void OpenQuitConfirm()
        {
            if (quitDialog != null)
            {
                quitDialog.SetActive(true);
                quitDialog.transform.SetAsLastSibling();
            }
        }

        public void CloseQuitConfirm()
        {
            if (quitDialog != null)
            {
                quitDialog.SetActive(false);
            }
        }

        public void ShowAchievements()
        {
            if (achievementList != null)
            {
                achievementList.Refresh();
            }

            Show(achievementPanel);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void SelectCharacter(CharacterDefinition character)
        {
            if (character == null)
            {
                return;
            }

            selected = character;
            RefreshCharacter();
        }

        public void PlaySelected()
        {
            if (selected == null || !AchievementProgress.IsCharacterUnlocked(selected))
            {
                ShowCharacters();
                return;
            }

            RunSession.SelectedCharacter = selected;
            Time.timeScale = 1f;
            MenuMusicController.Instance.StopMusic();
            SceneManager.LoadScene(gameplayScene);
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnMusicChanged(float value)
        {
            GameSettings.MusicVolume = value;
        }

        public void OnSoundChanged(float value)
        {
            GameSettings.SoundVolume = value;
        }

        public void OnScreenShakeChanged(bool value)
        {
            GameSettings.ScreenShake = value;
        }

        public void OnDamageTextChanged(bool value)
        {
            GameSettings.ShowDamageText = value;
        }

        public void RefreshUpgrades()
        {
            if (upgradeTitle != null)
            {
                upgradeTitle.text = selected != null
                    ? $"{selected.displayName.ToUpper()} UPGRADES"
                    : "UPGRADES";
            }

            if (bankLabel != null)
            {
                bankLabel.text = devMode
                    ? $"Bank: {MetaProgress.Coins}  ·  DEV"
                    : $"Bank: {MetaProgress.Coins}";
            }

            if (upgradeRows != null)
            {
                for (int i = 0; i < upgradeRows.Length; i++)
                {
                    if (upgradeRows[i] != null)
                    {
                        upgradeRows[i].Refresh(selected);
                    }
                }
            }

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

        public void ResetSelectedUpgrades()
        {
            if (selected == null)
            {
                return;
            }

            MetaProgress.ResetCharacter(selected);
            RefreshUpgrades();
        }

        private void SelectDefault()
        {
            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] != null
                        && characterButtons[i].Character != null
                        && AchievementProgress.IsCharacterUnlocked(characterButtons[i].Character))
                    {
                        selected = characterButtons[i].Character;
                        return;
                    }
                }
            }

            if (roster != null && roster.characters != null)
            {
                for (int i = 0; i < roster.characters.Length; i++)
                {
                    if (roster.characters[i] != null
                        && AchievementProgress.IsCharacterUnlocked(roster.characters[i]))
                    {
                        selected = roster.characters[i];
                        return;
                    }
                }
            }
        }

        private void RefreshCharacter()
        {
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
                    if (!AchievementProgress.IsCharacterUnlocked(selected))
                    {
                        AchievementDefinition unlock = AchievementCatalog.FindUnlockForCharacter(selected);
                        string hint = unlock != null ? unlock.description : "Complete an achievement to unlock this hero.";
                        body += $"\n\nLOCKED — {hint}";
                    }

                    characterDetail.text = body;
                }
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

            if (screenShakeToggle != null)
            {
                screenShakeToggle.SetIsOnWithoutNotify(GameSettings.ScreenShake);
            }

            if (damageTextToggle != null)
            {
                damageTextToggle.SetIsOnWithoutNotify(GameSettings.ShowDamageText);
            }
        }

        private void Show(GameObject panel)
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
