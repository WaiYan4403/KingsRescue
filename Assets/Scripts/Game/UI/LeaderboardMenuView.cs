using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Online;

namespace KingsRescue.UI
{
    /// <summary>
    /// Online top 100. The first three scores use the podium cards. Everyone after that
    /// copies the other-place card. The signed-in player also gets the your-place card.
    /// </summary>
    public class LeaderboardMenuView : MonoBehaviour
    {
        public const int TopCount = 100;

        [SerializeField] private TMP_Text status;
        [SerializeField] private GameObject guestStatus;
        [SerializeField] private Transform content;
        [SerializeField] private LeaderboardRowView firstPlace;
        [SerializeField] private LeaderboardRowView secondPlace;
        [SerializeField] private LeaderboardRowView thirdPlace;
        [SerializeField] private LeaderboardRowView otherTemplate;
        [SerializeField] private GameObject yourPlaceSlot;
        [SerializeField] private LeaderboardRowView yourPlace;

        private readonly List<LeaderboardRowView> spawnedOthers = new List<LeaderboardRowView>();
        private int requestId;

        public void Refresh()
        {
            _ = Load();
        }

        private async Task Load()
        {
            int id = ++requestId;
            if (otherTemplate != null)
            {
                otherTemplate.Hide();
            }

            SetStatus("Loading...");
            HidePodium();
            ClearSpawned();
            ShowYou(null);

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

            ShowPodium(firstPlace, entries, 0);
            ShowPodium(secondPlace, entries, 1);
            ShowPodium(thirdPlace, entries, 2);

            for (int i = 3; i < entries.Length && i < TopCount; i++)
            {
                SpawnOther(entries[i]);
            }

            ShowYou(result.Data.currentPlayer);
            RebuildLayout();
        }

        private static void ShowPodium(LeaderboardRowView row, LeaderboardEntryDto[] entries, int index)
        {
            if (row == null)
            {
                return;
            }

            if (entries == null || index < 0 || index >= entries.Length || entries[index] == null)
            {
                row.Hide();
                return;
            }

            LeaderboardEntryDto entry = entries[index];
            row.Show(entry.rank > 0 ? entry.rank : index + 1, entry.username, entry.score);
        }

        private void SpawnOther(LeaderboardEntryDto entry)
        {
            if (otherTemplate == null || content == null || entry == null)
            {
                return;
            }

            LeaderboardRowView row = Instantiate(otherTemplate, content);
            row.gameObject.name = "Other Place";
            row.Show(entry.rank, entry.username, entry.score);
            spawnedOthers.Add(row);
        }

        private void ShowYou(PlayerPayload player)
        {
            bool show = player != null && player.IsValid && player.rank > 0;
            if (yourPlaceSlot != null)
            {
                yourPlaceSlot.SetActive(show);
            }

            if (!show || yourPlace == null)
            {
                if (guestStatus != null)
                {
                    guestStatus.SetActive(true);
                }

                return;
            }

            yourPlace.Show(player.rank, player.username, player.score);
            if (guestStatus != null)
            {
                guestStatus.SetActive(false);
            }
        }

        private void HidePodium()
        {
            firstPlace?.Hide();
            secondPlace?.Hide();
            thirdPlace?.Hide();
        }

        private void ClearSpawned()
        {
            for (int i = spawnedOthers.Count - 1; i >= 0; i--)
            {
                if (spawnedOthers[i] != null)
                {
                    Destroy(spawnedOthers[i].gameObject);
                }
            }

            spawnedOthers.Clear();
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
            firstPlace?.RefreshMesh();
            secondPlace?.RefreshMesh();
            thirdPlace?.RefreshMesh();
            yourPlace?.RefreshMesh();
            for (int i = 0; i < spawnedOthers.Count; i++)
            {
                spawnedOthers[i]?.RefreshMesh();
            }

            if (content is RectTransform contentRect)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
            }

            if (transform is RectTransform board)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(board);
            }
        }
    }
}
