using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingsRescue.UI
{
    /// <summary>
    /// One designed leaderboard card: rank, name, score, and the hero portrait for that score.
    /// </summary>
    public class LeaderboardRowView : MonoBehaviour
    {
        [SerializeField] private TMP_Text rankLabel;
        [SerializeField] private TMP_Text nameLabel;
        [SerializeField] private TMP_Text scoreLabel;
        [SerializeField] private Image portraitImage;
        [SerializeField] private Image rankImage;
        [SerializeField] private Image borderImage;

        private Sprite defaultRank;
        private Sprite defaultBorder;
        private bool capturedDefaults;

        private void Awake()
        {
            CacheImages();
        }

        public void Show(int rank, string username, int distanceMeters, Sprite portrait)
        {
            CacheImages();
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
                scoreLabel.text = HudView.FormatDistance(distanceMeters);
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = portrait;
                portraitImage.enabled = portrait != null;
                portraitImage.preserveAspect = true;
            }
        }

        /// <summary>
        /// Your Place uses the 1st, 2nd, or 3rd badge and avatar border. Any other rank keeps the card's own art.
        /// </summary>
        public void ApplyRankStyle(int rank, Sprite firstBadge, Sprite secondBadge, Sprite thirdBadge, Sprite firstBorder, Sprite secondBorder, Sprite thirdBorder)
        {
            CacheImages();
            Sprite badge = defaultRank;
            Sprite border = defaultBorder;
            if (rank == 1)
            {
                badge = firstBadge != null ? firstBadge : defaultRank;
                border = firstBorder != null ? firstBorder : defaultBorder;
            }
            else if (rank == 2)
            {
                badge = secondBadge != null ? secondBadge : defaultRank;
                border = secondBorder != null ? secondBorder : defaultBorder;
            }
            else if (rank == 3)
            {
                badge = thirdBadge != null ? thirdBadge : defaultRank;
                border = thirdBorder != null ? thirdBorder : defaultBorder;
            }

            if (rankImage != null && badge != null)
            {
                rankImage.sprite = badge;
            }

            if (borderImage != null && border != null)
            {
                borderImage.sprite = border;
            }
        }

        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>Keeps the card visible and clears the rank line, for the guest or offline message.</summary>
        public void ShowBlank()
        {
            CacheImages();
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

            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }

            ApplyRankStyle(0, null, null, null, null, null, null);
        }

        public void RefreshMesh()
        {
            rankLabel?.ForceMeshUpdate(true, true);
            nameLabel?.ForceMeshUpdate(true, true);
            scoreLabel?.ForceMeshUpdate(true, true);
        }

        private void CacheImages()
        {
            if (portraitImage == null)
            {
                portraitImage = FindNamedImage("Character Profile");
            }

            if (borderImage == null)
            {
                borderImage = FindNamedImage("Border");
            }

            if (rankImage == null)
            {
                rankImage = FindRankBadge();
            }

            if (capturedDefaults)
            {
                return;
            }

            capturedDefaults = true;
            if (rankImage != null)
            {
                defaultRank = rankImage.sprite;
            }

            if (borderImage != null)
            {
                defaultBorder = borderImage.sprite;
            }
        }

        private Image FindRankBadge()
        {
            Image fallback = null;
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform child = all[i];
                if (child.name != "Rank")
                {
                    continue;
                }

                Image image = child.GetComponent<Image>();
                if (image == null)
                {
                    continue;
                }

                if (child.parent != null && child.parent.name == "Rank")
                {
                    return image;
                }

                fallback = image;
            }

            return fallback;
        }

        private Image FindNamedImage(string childName)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name != childName)
                {
                    continue;
                }

                Image image = all[i].GetComponent<Image>();
                if (image != null)
                {
                    return image;
                }
            }

            return null;
        }
    }
}
