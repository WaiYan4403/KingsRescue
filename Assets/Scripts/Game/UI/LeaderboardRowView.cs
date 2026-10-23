using TMPro;
using UnityEngine;

namespace KingsRescue.UI
{
    /// <summary>
    /// One designed leaderboard card: rank, name, and score, found by the child names in the menu.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        private TMP_Text rankLabel;
        private TMP_Text nameLabel;
        private TMP_Text scoreLabel;
        private bool cached;

        public void Show(int rank, string username, int scoreSeconds)
        {
            Cache();
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

        public void RefreshMesh()
        {
            Cache();
            rankLabel?.ForceMeshUpdate(true, true);
            nameLabel?.ForceMeshUpdate(true, true);
            scoreLabel?.ForceMeshUpdate(true, true);
        }

        private void Cache()
        {
            if (cached)
            {
                return;
            }

            cached = true;
            rankLabel = FindText("Rank");
            nameLabel = FindText("NameBG");
            scoreLabel = FindText("ScoreBG");
        }

        private TMP_Text FindText(string parentName)
        {
            Transform parent = FindNamed(transform, parentName);
            return parent != null ? parent.GetComponentInChildren<TMP_Text>(true) : null;
        }

        private static Transform FindNamed(Transform root, string name)
        {
            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindNamed(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
