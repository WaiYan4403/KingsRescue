using TMPro;
using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Data;
using KingsRescue.Meta;

namespace KingsRescue.UI
{
    public class AchievementListView : MonoBehaviour
    {
        [SerializeField] private Transform content;
        [SerializeField] private TMP_Text bankLabel;

        private static Sprite fillSprite;

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
                bankLabel.text = $"Bank: {MetaProgress.Coins}";
            }

            if (content == null)
            {
                return;
            }

            for (int i = content.childCount - 1; i >= 0; i--)
            {
                Destroy(content.GetChild(i).gameObject);
            }

            AchievementDefinition[] list = AchievementCatalog.All;
            for (int i = 0; i < list.Length; i++)
            {
                if (list[i] != null)
                {
                    BuildRow(list[i]);
                }
            }
        }

        private void BuildRow(AchievementDefinition achievement)
        {
            bool done = AchievementProgress.IsCompleted(achievement);
            int progress = AchievementProgress.GetProgress(achievement);
            int target = Mathf.Max(1, achievement.target);
            float fill = Mathf.Clamp01(progress / (float)target);

            bool canBuy = achievement.kind == AchievementKind.PayCoins && !done;
            float rowHeight = canBuy ? 220f : 176f;
            GameObject row = MenuUi.Image(content, achievement.Id, new Color(0.08f, 0.12f, 0.08f, 1f), new Vector2(0f, rowHeight));
            var rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = rowHeight;
            rowLayout.preferredHeight = rowHeight;
            MenuUi.Column(row, 4f, new RectOffset(14, 14, 10, 10));

            TMP_Text title = MenuUi.Label(row.transform, "Title", achievement.title, 24, done ? MenuUi.Gold : MenuUi.Ink, TextAlignmentOptions.MidlineLeft);
            Size(title.rectTransform, 28f);

            TMP_Text body = MenuUi.Label(row.transform, "Body", achievement.description, 16, MenuUi.Dim, TextAlignmentOptions.TopLeft);
            Size(body.rectTransform, 36f);
            body.enableWordWrapping = true;

            GameObject barBack = MenuUi.Image(row.transform, "Bar", new Color(0.05f, 0.07f, 0.05f, 1f), new Vector2(0f, 14f));
            Size(barBack.GetComponent<RectTransform>(), 14f);
            GameObject barFill = MenuUi.Image(barBack.transform, "Fill", done ? MenuUi.Gold : MenuUi.ButtonSelected, Vector2.zero);
            MenuUi.Stretch(barFill.GetComponent<RectTransform>());
            Image fillImage = barFill.GetComponent<Image>();
            fillImage.sprite = WhiteSprite();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillAmount = fill;

            string progressText = achievement.kind == AchievementKind.PayCoins
                ? (done ? "Purchased" : $"Requires {target} coins")
                : $"{Mathf.Min(progress, target)} / {target}";
            TMP_Text progressLabel = MenuUi.Label(row.transform, "Progress", progressText, 16, MenuUi.Ink, TextAlignmentOptions.MidlineLeft);
            Size(progressLabel.rectTransform, 20f);

            string status = done ? $"Reward: {achievement.RewardSummary()}  —  COMPLETED" : $"Reward: {achievement.RewardSummary()}";
            TMP_Text reward = MenuUi.Label(row.transform, "Reward", status, 16, MenuUi.Gold, TextAlignmentOptions.MidlineLeft);
            Size(reward.rectTransform, 22f);

            if (canBuy)
            {
                Button buy = MenuUi.MakeButton(row.transform, "Buy", $"Unlock ({target})", new Vector2(0f, 36f), () =>
                {
                    AchievementProgress.TryPurchase(achievement);
                });
                Size(buy.GetComponent<RectTransform>(), 36f);
                buy.interactable = MetaProgress.Coins >= target;
                TMP_Text buyLabel = buy.GetComponentInChildren<TMP_Text>();
                if (buyLabel != null)
                {
                    buyLabel.fontSize = 18;
                }
            }
        }

        private static void Size(RectTransform rect, float height)
        {
            var element = rect.gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = rect.gameObject.AddComponent<LayoutElement>();
            }

            element.minHeight = height;
            element.preferredHeight = height;
        }

        private static Sprite WhiteSprite()
        {
            if (fillSprite == null)
            {
                fillSprite = Sprite.Create(
                    Texture2D.whiteTexture,
                    new Rect(0f, 0f, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height),
                    new Vector2(0.5f, 0.5f),
                    4f);
            }

            return fillSprite;
        }
    }
}
