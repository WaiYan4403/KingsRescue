using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.UI
{
    /// <summary>
    /// The achievements page. Spawns one row per achievement from the row template, and shows the
    /// selected row's objective and reward in the detail strip at the bottom.
    /// </summary>
    public class AchievementListView : MonoBehaviour
    {
        [Header("List")]
        [Tooltip("Parent of the spawned rows (the scroll view's Content).")]
        [SerializeField] private Transform content;
        [Tooltip("The Row Template prefab.")]
        [SerializeField] private AchievementRowView rowPrefab;
        [Tooltip("Optional text shown when the catalog is empty.")]
        [SerializeField] private TMP_Text statusLabel;
        [SerializeField] private TMP_Text bankLabel;
        [Tooltip("Reward icon for achievements that pay coins and name no hero or weapon.")]
        [SerializeField] private Sprite coinIcon;

        [Header("Selected achievement")]
        [SerializeField] private Image detailIcon;
        [SerializeField] private TMP_Text detailObjective;
        [SerializeField] private TMP_Text detailReward;
        [Tooltip("Dimmed while the selected achievement is still locked.")]
        [SerializeField] private Graphic detailUnlockedMark;
        [Tooltip("Shown for 'spend coins' achievements that are not bought yet.")]
        [SerializeField] private Button buyButton;
        [SerializeField] private TMP_Text buyLabel;

        private static readonly Color LockedTint = new Color(1f, 1f, 1f, 0.3f);

        private readonly List<AchievementRowView> rows = new List<AchievementRowView>();
        private AchievementDefinition selected;

        private void Awake()
        {
            if (buyButton != null)
            {
                buyButton.onClick.AddListener(OnBuyClicked);
            }
        }

        private void OnDestroy()
        {
            if (buyButton != null)
            {
                buyButton.onClick.RemoveListener(OnBuyClicked);
            }
        }

        private void OnEnable()
        {
            AchievementProgress.Changed += Refresh;
            MetaProgress.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            AchievementProgress.Changed -= Refresh;
            MetaProgress.Changed -= Refresh;
        }

        public void Refresh()
        {
            AchievementProgress.EnsureLoaded();

            if (bankLabel != null)
            {
                bankLabel.text = MetaProgress.Coins.ToString();
            }

            AchievementDefinition[] list = AchievementCatalog.All ?? new AchievementDefinition[0];
            EnsureRows(list);
            HideDesignTimeRows();

            if (statusLabel != null)
            {
                statusLabel.gameObject.SetActive(rows.Count == 0);
                if (rows.Count == 0)
                {
                    statusLabel.text = "No achievements yet.";
                }
            }

            if (selected == null || !Contains(list, selected))
            {
                selected = rows.Count > 0 ? rows[0].Achievement : null;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                AchievementRowView row = rows[i];
                AchievementDefinition achievement = row.Achievement;
                row.Show(achievement.title, achievement.description, AchievementProgress.IsCompleted(achievement), RewardIcon(achievement));
                row.SetSelected(achievement == selected);
            }

            RefreshDetail();
        }

        /// <summary>
        /// A row template placed in the scene to preview the layout has no achievement bound.
        /// It stays in the scene for editing and is hidden while playing.
        /// </summary>
        private void HideDesignTimeRows()
        {
            if (content == null)
            {
                return;
            }

            AchievementRowView[] found = content.GetComponentsInChildren<AchievementRowView>(true);
            for (int i = 0; i < found.Length; i++)
            {
                if (found[i].Achievement == null)
                {
                    found[i].gameObject.SetActive(false);
                }
            }
        }

        private void EnsureRows(AchievementDefinition[] list)
        {
            int wanted = 0;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    wanted++;
                }
            }

            bool upToDate = rows.Count == wanted;
            if (upToDate)
            {
                int index = 0;
                for (int i = 0; i < list.Length && upToDate; i++)
                {
                    if (list[i] == null)
                    {
                        continue;
                    }

                    upToDate = rows[index] != null && rows[index].Achievement == list[i];
                    index++;
                }
            }

            if (upToDate || content == null || rowPrefab == null)
            {
                return;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                if (rows[i] != null)
                {
                    Destroy(rows[i].gameObject);
                }
            }

            rows.Clear();

            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == null)
                {
                    continue;
                }

                AchievementRowView row = Instantiate(rowPrefab, content);
                row.gameObject.SetActive(true);
                row.Bind(list[i], OnRowClicked);
                rows.Add(row);
            }
        }

        private void OnRowClicked(AchievementRowView row)
        {
            if (row == null || row.Achievement == null)
            {
                return;
            }

            selected = row.Achievement;
            for (int i = 0; i < rows.Count; i++)
            {
                rows[i].SetSelected(rows[i].Achievement == selected);
            }

            RefreshDetail();
        }

        private void RefreshDetail()
        {
            bool has = selected != null;
            bool done = has && AchievementProgress.IsCompleted(selected);

            if (detailObjective != null)
            {
                detailObjective.text = has ? ObjectiveText(selected, done) : string.Empty;
            }

            if (detailReward != null)
            {
                detailReward.text = has ? $"Reward: {selected.RewardSummary()}" : string.Empty;
            }

            if (detailIcon != null)
            {
                Sprite icon = ShownIcon();
                detailIcon.sprite = icon;
                detailIcon.enabled = icon != null;
                detailIcon.preserveAspect = true;
            }

            if (detailUnlockedMark != null)
            {
                detailUnlockedMark.color = done ? Color.white : LockedTint;
            }

            if (buyButton != null)
            {
                bool buyable = has && selected.kind == AchievementKind.PayCoins && !done;
                buyButton.gameObject.SetActive(buyable);
                if (buyable)
                {
                    buyButton.interactable = MetaProgress.Coins >= Mathf.Max(1, selected.target);
                    if (buyLabel != null)
                    {
                        buyLabel.text = $"UNLOCK ({Mathf.Max(1, selected.target)})";
                    }
                }
            }
        }

        private void OnBuyClicked()
        {
            if (selected != null)
            {
                AchievementProgress.TryPurchase(selected);
            }
        }

        private static string ObjectiveText(AchievementDefinition achievement, bool done)
        {
            int target = Mathf.Max(1, achievement.target);
            if (achievement.kind == AchievementKind.PayCoins)
            {
                string status = done ? "Purchased" : $"Requires {target} coins";
                return $"{achievement.description}\n{status}";
            }

            int progress = Mathf.Min(AchievementProgress.GetProgress(achievement), target);
            return $"{achievement.description}\n{progress} / {target}";
        }

        private Sprite ShownIcon()
        {
            if (selected == null)
            {
                return null;
            }

            for (int i = 0; i < rows.Count; i++)
            {
                AchievementRowView row = rows[i];
                if (row != null && row.Achievement == selected)
                {
                    return row.ShownIcon;
                }
            }

            return RewardIcon(selected);
        }

        private Sprite RewardIcon(AchievementDefinition achievement)
        {
            return achievement != null ? achievement.RewardIcon(coinIcon) : null;
        }

        private static bool Contains(AchievementDefinition[] list, AchievementDefinition value)
        {
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
