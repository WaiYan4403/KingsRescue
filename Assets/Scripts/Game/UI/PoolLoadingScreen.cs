using UnityEngine;
using UnityEngine.UI;

namespace KingsRescue.UI
{
    /// <summary>
    /// Full-screen overlay shown while <see cref="ObjectPoolManager"/> builds its pools.
    /// Created at runtime so the first painted frame is the loading screen, not a hitch.
    /// </summary>
    public sealed class PoolLoadingScreen : MonoBehaviour
    {
        private Image fill;
        private Text percent;

        public static PoolLoadingScreen Show()
        {
            var root = new GameObject("Pool Loading");
            var screen = root.AddComponent<PoolLoadingScreen>();
            screen.Build();
            return screen;
        }

        public void SetProgress(float progress)
        {
            float amount = Mathf.Clamp01(progress);
            if (fill != null)
            {
                fill.fillAmount = amount;
            }

            if (percent != null)
            {
                percent.text = Mathf.RoundToInt(amount * 100f) + "%";
            }
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            gameObject.AddComponent<GraphicRaycaster>();

            var backdrop = CreateImage("Backdrop", transform, new Color(0.04f, 0.03f, 0.02f, 1f));
            Stretch(backdrop.rectTransform);

            var title = CreateText("Title", transform, 54, FontStyle.Bold);
            title.text = "Loading";
            var titleRect = title.rectTransform;
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.sizeDelta = new Vector2(800f, 80f);
            titleRect.anchoredPosition = new Vector2(0f, 70f);

            var bar = CreateImage("Bar", transform, new Color(1f, 1f, 1f, 0.16f));
            var barRect = bar.rectTransform;
            barRect.anchorMin = barRect.anchorMax = new Vector2(0.5f, 0.5f);
            barRect.sizeDelta = new Vector2(640f, 28f);
            barRect.anchoredPosition = new Vector2(0f, -20f);

            fill = CreateImage("Fill", bar.transform, new Color(0.93f, 0.78f, 0.35f, 1f));
            Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 0f;

            percent = CreateText("Percent", transform, 32, FontStyle.Normal);
            percent.text = "0%";
            var percentRect = percent.rectTransform;
            percentRect.anchorMin = percentRect.anchorMax = new Vector2(0.5f, 0.5f);
            percentRect.sizeDelta = new Vector2(400f, 50f);
            percentRect.anchoredPosition = new Vector2(0f, -70f);
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = WhiteSprite();
            image.color = color;
            image.raycastTarget = true;
            return image;
        }

        private static Text CreateText(string name, Transform parent, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.96f, 0.93f, 0.86f, 1f);
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static Sprite whiteSprite;

        private static Sprite WhiteSprite()
        {
            if (whiteSprite != null)
            {
                return whiteSprite;
            }

            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
            return whiteSprite;
        }
    }
}
