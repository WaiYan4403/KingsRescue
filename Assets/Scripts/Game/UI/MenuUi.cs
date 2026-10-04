using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingsRescue.UI
{
    public static class MenuUi
    {
        public static readonly Color Background = new Color(0.07f, 0.1f, 0.07f, 1f);
        public static readonly Color Panel = new Color(0.12f, 0.18f, 0.12f, 0.96f);
        public static readonly Color Button = new Color(0.22f, 0.34f, 0.18f, 1f);
        public static readonly Color ButtonSelected = new Color(0.4f, 0.55f, 0.22f, 1f);
        public static readonly Color Gold = new Color(0.95f, 0.84f, 0.42f, 1f);
        public static readonly Color Ink = new Color(0.95f, 0.93f, 0.86f, 1f);
        public static readonly Color Dim = new Color(0.75f, 0.78f, 0.7f, 1f);

        public static GameObject PanelRoot(Transform parent, string name, Vector2 size)
        {
            GameObject go = Image(parent, name, Panel, size);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            return go;
        }

        public static GameObject Image(Transform parent, string name, Color color, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;
            return go;
        }

        public static TMP_Text Label(Transform parent, string name, string value, int size, Color color, TextAlignmentOptions align = TextAlignmentOptions.Center)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var text = go.GetComponent<TextMeshProUGUI>();
            if (TMP_Settings.defaultFontAsset != null)
            {
                text.font = TMP_Settings.defaultFontAsset;
            }

            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = align;
            text.raycastTarget = false;
            return text;
        }

        public static Button MakeButton(Transform parent, string name, string caption, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            GameObject go = Image(parent, name, Button, size);
            Button button = go.AddComponent<Button>();
            ColorBlock colors = button.colors;
            colors.highlightedColor = ButtonSelected;
            colors.pressedColor = new Color(0.18f, 0.26f, 0.14f, 1f);
            colors.selectedColor = ButtonSelected;
            button.colors = colors;
            button.onClick.AddListener(onClick);

            TMP_Text label = Label(go.transform, "Label", caption, 28, Gold);
            Stretch(label.rectTransform);
            return button;
        }

        public static Slider MakeSlider(Transform parent, string name, Vector2 size, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            GameObject root = Image(parent, name, new Color(0.08f, 0.1f, 0.08f, 1f), size);
            Slider slider = root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = value;

            GameObject fillArea = Image(root.transform, "Fill Area", Color.clear, size);
            Stretch(fillArea.GetComponent<RectTransform>(), 8f, 6f);
            GameObject fill = Image(fillArea.transform, "Fill", ButtonSelected, Vector2.zero);
            Stretch(fill.GetComponent<RectTransform>());

            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(root.transform, false);
            Stretch(handleArea.GetComponent<RectTransform>(), 8f, 0f);
            GameObject handle = Image(handleArea.transform, "Handle", Gold, new Vector2(22f, size.y));
            handle.GetComponent<RectTransform>().sizeDelta = new Vector2(22f, 0f);

            slider.fillRect = fill.GetComponent<RectTransform>();
            slider.handleRect = handle.GetComponent<RectTransform>();
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.onValueChanged.AddListener(onChanged);
            return slider;
        }

        public static void Stretch(RectTransform rect, float x = 0f, float y = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(x, y);
            rect.offsetMax = new Vector2(-x, -y);
        }

        public static VerticalLayoutGroup Column(GameObject go, float spacing, RectOffset padding)
        {
            var layout = go.GetComponent<VerticalLayoutGroup>();
            if (layout == null)
            {
                layout = go.AddComponent<VerticalLayoutGroup>();
            }
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }
    }
}
