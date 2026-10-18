using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Online;

namespace KingsRescue.UI
{
    /// <summary>
    /// Online top 100. The signed-in player is repeated under the list only when they rank below 100.
    /// </summary>
    public class LeaderboardMenuView : MonoBehaviour
    {
        public const int TopCount = 100;

        [SerializeField] private TMP_Text status;
        [SerializeField] private Transform content;
        [SerializeField] private TMP_Text rowTemplate;
        [SerializeField] private TMP_Text youRow;

        private int requestId;

        public void Refresh()
        {
            _ = Load();
        }

        private async Task Load()
        {
            int id = ++requestId;
            SetStatus("Loading...");
            SetYou(null);
            ClearRows();

            OnlineServices services = OnlineServices.Instance;
            if (services == null)
            {
                SetStatus("Online scores are turned off.");
                return;
            }

            ApiResult<LeaderboardResponse> result = await services.GetLeaderboard(TopCount);
            if (this == null || id != requestId)
            {
                return;
            }

            if (!result.Ok || result.Data == null)
            {
                SetStatus(string.IsNullOrEmpty(result.Error) ? "Could not load the board." : result.Error);
                return;
            }

            LeaderboardEntryDto[] entries = result.Data.entries ?? System.Array.Empty<LeaderboardEntryDto>();
            SetStatus(entries.Length == 0 ? "No scores yet." : string.Empty);

            for (int i = 0; i < entries.Length && i < TopCount; i++)
            {
                CreateRow(entries[i], false);
            }

            PlayerPayload me = result.Data.currentPlayer;
            bool showYou = me != null && me.IsValid && me.rank > TopCount;
            SetYou(showYou ? me : null);
            RebuildLayout();
            RefreshRowMeshes();
        }

        private void CreateRow(LeaderboardEntryDto entry, bool highlight)
        {
            if (rowTemplate == null || content == null || entry == null)
            {
                return;
            }

            TMP_Text row = Instantiate(rowTemplate, content);
            row.gameObject.SetActive(true);
            row.text = Format(entry.rank, entry.username, entry.score);
            if (highlight)
            {
                row.color = MenuUi.Gold;
            }
        }

        private void SetYou(PlayerPayload player)
        {
            if (youRow == null)
            {
                return;
            }

            bool show = player != null && player.IsValid;
            youRow.gameObject.SetActive(show);
            if (show)
            {
                youRow.text = "You   " + Format(player.rank, player.username, player.score);
            }
        }

        private void ClearRows()
        {
            if (content == null)
            {
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Transform child = content.GetChild(i);
                if (rowTemplate != null && child == rowTemplate.transform)
                {
                    continue;
                }

                Destroy(child.gameObject);
            }
        }

        private void SetStatus(string message)
        {
            if (status != null)
            {
                status.text = message ?? string.Empty;
            }
        }

        private void RebuildLayout()
        {
            if (transform is RectTransform board)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(board);
            }
        }

        private void RefreshRowMeshes()
        {
            if (content == null)
            {
                return;
            }

            for (int i = 0; i < content.childCount; i++)
            {
                Transform child = content.GetChild(i);
                if (!child.gameObject.activeSelf)
                {
                    continue;
                }

                if (child.TryGetComponent(out TMP_Text label))
                {
                    label.ForceMeshUpdate(true, true);
                }
            }
        }

        private static string Format(int rank, string username, int scoreSeconds)
        {
            string name = string.IsNullOrEmpty(username) ? "Player" : username;
            return $"#{rank}   {name}   {HudView.FormatClock(scoreSeconds)}";
        }
    }
}
