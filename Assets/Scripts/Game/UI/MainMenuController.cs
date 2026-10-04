using System.Collections.Generic;
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

        [Header("Character")]
        [SerializeField] private CharacterSelectButton[] characterButtons;
        [SerializeField] private TMP_Text characterDetail;

        [Header("Upgrades")]
        [SerializeField] private TMP_Text upgradeTitle;
        [SerializeField] private TMP_Text bankLabel;
        [SerializeField] private UpgradeRowView[] upgradeRows;

        [Header("Settings")]
        [SerializeField] private Slider volumeSlider;

        [Header("Leaderboards")]
        [SerializeField] private TMP_Text[] leaderboardRows;
        [SerializeField] private TMP_Text leaderboardEmpty;

        private CharacterDefinition selected;

        public CharacterDefinition Selected => selected;

        private void Awake()
        {
            Time.timeScale = 1f;
            GameSettings.Apply();

            if (roster == null)
            {
                roster = Resources.Load<CharacterRoster>("CharacterRoster");
            }

            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            }

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

            if (selected == null)
            {
                SelectDefault();
            }

            RefreshCharacter();
        }

        void Start()
        {
            ShowHome();
        }

        public void ShowHome()
        {
            Show(homePanel);
            MenuMusicController.Instance.SetMainMusic();
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
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowSettings()
        {
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(GameSettings.Volume);
            }

            Show(settingsPanel);
            MenuMusicController.Instance.SetOptionsMusic();
        }

        public void ShowLeaderboards()
        {
            RefreshLeaderboard();
            Show(leaderboardPanel);
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
            if (selected == null)
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

        public void OnVolumeChanged(float value)
        {
            GameSettings.Volume = value;
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
                bankLabel.text = $"Bank: {MetaProgress.Coins}";
            }

            if (upgradeRows == null)
            {
                return;
            }

            for (int i = 0; i < upgradeRows.Length; i++)
            {
                if (upgradeRows[i] != null)
                {
                    upgradeRows[i].Refresh(selected);
                }
            }
        }

        private void SelectDefault()
        {
            if (characterButtons != null)
            {
                for (int i = 0; i < characterButtons.Length; i++)
                {
                    if (characterButtons[i] != null && characterButtons[i].Character != null)
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
                    if (roster.characters[i] != null)
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
                    characterDetail.text = $"{selected.displayName}\n{selected.description}\n\nUnique weapon: {weapon}";
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
                    characterButtons[i].SetSelected(characterButtons[i].Character == selected);
                }
            }
        }

        private void RefreshLeaderboard()
        {
            IReadOnlyList<LeaderboardEntry> entries = Leaderboard.Entries;
            bool empty = entries == null || entries.Count == 0;

            if (leaderboardEmpty != null)
            {
                leaderboardEmpty.gameObject.SetActive(empty);
                if (empty)
                {
                    leaderboardEmpty.text = "No runs recorded yet.";
                }
            }

            if (leaderboardRows == null)
            {
                return;
            }

            for (int i = 0; i < leaderboardRows.Length; i++)
            {
                TMP_Text row = leaderboardRows[i];
                if (row == null)
                {
                    continue;
                }

                if (empty || i >= entries.Count)
                {
                    row.gameObject.SetActive(false);
                    continue;
                }

                LeaderboardEntry entry = entries[i];
                row.gameObject.SetActive(true);
                row.text = $"{i + 1}. {entry.character}   {HudView.FormatClock(entry.time)}   {entry.kills} kills   Lv {entry.level}";
            }
        }

        private void Show(GameObject panel)
        {
            SetActive(homePanel, panel);
            SetActive(characterPanel, panel);
            SetActive(upgradePanel, panel);
            SetActive(settingsPanel, panel);
            SetActive(leaderboardPanel, panel);
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
