using TMPro;
using UnityEngine;

namespace KingsRescue.UI
{
    /// <summary>
    /// One designed leaderboard card: rank, name, and score, found by the child names in the menu.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text scoreLabel;

        public void Show(int rank, string username, int scoreSeconds)
        {
            gameObject.SetActive(true);
            if (rankLabel != null)
            {
                rankLabel.text = rank.ToString();
            }

            if (nameLabel != null)
            {
                nameLabel.text = string.IsNullOrEmpty(username) ? "Player" : username;
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = HudView.FormatClock(scoreSeconds);
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Keeps the card visible and clears the rank line, for the guest or offline message.</summary>
        public void ShowBlank()
        {
            gameObject.SetActive(true);
            if (rankLabel != null)
            {
                rankLabel.text = string.Empty;
            }

            if (nameLabel != null)
            {
                nameLabel.text = string.Empty;
            }

            if (scoreLabel != null)
            {
                scoreLabel.text = string.Empty;
            }
        }

        public void RefreshMesh()
        {
            rankLabel?.ForceMeshUpdate(true, true);
            nameLabel?.ForceMeshUpdate(true, true);
            scoreLabel?.ForceMeshUpdate(true, true);
        }
    }
}
