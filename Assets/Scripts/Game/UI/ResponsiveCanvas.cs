using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KingsRescue.UI
{
    /// <summary>
    /// Keeps a screen-space canvas usable on phones, tablets, and desktop browsers.
    /// The authored phone layout still fits. Wider screens park the stat cluster on the right,
    /// and anything taller than the visible area scales down instead of being clipped.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasScaler))]
    public class ResponsiveCanvas : MonoBehaviour
    {
        private const float WideWidth = 1200f;
        private const float DesignWidth = 1080f;
        private const float FitPadding = 48f;

        private struct Box
        {
            public RectTransform Rect;
            public Vector2 AnchorMin;
            public Vector2 AnchorMax;
            public Vector2 Pivot;
            public Vector2 AnchoredPosition;
            public Vector2 SizeDelta;
            public Vector2 OffsetMin;
            public Vector2 OffsetMax;
            public Vector3 LocalScale;
        }

        [SerializeField] private RectTransform hud;
        [SerializeField] private RectTransform info;
        [SerializeField] private RectTransform joystick;

        private CanvasScaler scaler;
        private Box hudBox;
        private Box infoBox;
        private Box joystickBox;
        private bool hasHud;
        private bool hasInfo;
        private bool hasJoystick;
        private float lastWidth = -1f;
        private float lastHeight = -1f;
        private Rect lastSafe;
        private readonly Dictionary<int, Vector3> authoredScale = new Dictionary<int, Vector3>(8);

        private void Awake()
        {
            scaler = GetComponent<CanvasScaler>();
            ConfigureScaler();
            Cache();
        }

        private void OnEnable()
        {
            ConfigureScaler();
            lastWidth = -1f;
        }

        private void LateUpdate()
        {
            var root = (RectTransform)transform;
            Rect safe = Screen.safeArea;
            float width = root.rect.width;
            float height = root.rect.height;
            if (Mathf.Approximately(width, lastWidth) && Mathf.Approximately(height, lastHeight) && safe == lastSafe)
            {
                return;
            }

            lastWidth = width;
            lastHeight = height;
            lastSafe = safe;
            Apply(width, height, safe);
        }

        private void ConfigureScaler()
        {
            if (scaler == null)
            {
                scaler = GetComponent<CanvasScaler>();
            }

            if (scaler == null || scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize)
            {
                return;
            }

            // Expand keeps the whole reference layout on screen. Extra space is added on the longer side.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        }

        private void Cache()
        {
            hasHud = hud != null;
            if (hasHud)
            {
                hudBox = Capture(hud);
            }

            hasInfo = info != null;
            if (hasInfo)
            {
                infoBox = Capture(info);
            }

            hasJoystick = joystick != null;
            if (hasJoystick)
            {
                joystickBox = Capture(joystick);
            }
        }

        private void Apply(float width, float height, Rect safe)
        {
            if (width < 2f || height < 2f || Screen.width < 2 || Screen.height < 2)
            {
                return;
            }

            float left = safe.xMin / Screen.width * width;
            float right = (Screen.width - safe.xMax) / Screen.width * width;
            float bottom = safe.yMin / Screen.height * height;
            float top = (Screen.height - safe.yMax) / Screen.height * height;

            if (hasHud)
            {
                Restore(hudBox);
                hud.offsetMin = new Vector2(hudBox.OffsetMin.x + left, hudBox.OffsetMin.y - top);
                hud.offsetMax = new Vector2(hudBox.OffsetMax.x - right, hudBox.OffsetMax.y - top);
            }

            if (hasInfo)
            {
                Restore(infoBox);
                float hudWidth = hasHud ? hud.rect.width : width;
                if (hudWidth >= WideWidth)
                {
                    float rightEdge = infoBox.AnchoredPosition.x + infoBox.SizeDelta.x * (1f - infoBox.Pivot.x);
                    float margin = Mathf.Max(12f, DesignWidth - rightEdge);
                    info.anchorMin = info.anchorMax = new Vector2(1f, 1f);
                    info.anchoredPosition = new Vector2(-(infoBox.SizeDelta.x * infoBox.Pivot.x + margin), infoBox.AnchoredPosition.y);
                }
            }

            if (hasJoystick)
            {
                Restore(joystickBox);
                joystick.offsetMin = new Vector2(joystickBox.OffsetMin.x + left, joystickBox.OffsetMin.y + bottom);
                joystick.offsetMax = new Vector2(joystickBox.OffsetMax.x - right, joystickBox.OffsetMax.y - top);
            }

            float maxWidth = Mathf.Max(100f, width - left - right - FitPadding);
            float maxHeight = Mathf.Max(100f, height - top - bottom - FitPadding);
            FitCentered(transform, maxWidth, maxHeight, 0);
        }

        private void FitCentered(Transform parent, float maxWidth, float maxHeight, int depth)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                var rect = parent.GetChild(i) as RectTransform;
                if (rect == null)
                {
                    continue;
                }

                bool centered = rect.anchorMin.x > 0.4f && rect.anchorMax.x < 0.6f
                    && rect.anchorMin.y > 0.4f && rect.anchorMax.y < 0.6f
                    && rect.sizeDelta.y > 200f;
                if (centered)
                {
                    int id = rect.GetInstanceID();
                    if (!authoredScale.TryGetValue(id, out Vector3 authored))
                    {
                        authored = rect.localScale;
                        authoredScale[id] = authored;
                    }

                    float scale = Mathf.Min(1f, maxWidth / Mathf.Max(1f, rect.sizeDelta.x), maxHeight / Mathf.Max(1f, rect.sizeDelta.y));
                    rect.localScale = scale < 0.995f
                        ? new Vector3(authored.x * scale, authored.y * scale, authored.z)
                        : authored;
                    continue;
                }

                if (depth < 1)
                {
                    FitCentered(rect, maxWidth, maxHeight, depth + 1);
                }
            }
        }

        private static Box Capture(RectTransform rect)
        {
            return new Box
            {
                Rect = rect,
                AnchorMin = rect.anchorMin,
                AnchorMax = rect.anchorMax,
                Pivot = rect.pivot,
                AnchoredPosition = rect.anchoredPosition,
                SizeDelta = rect.sizeDelta,
                OffsetMin = rect.offsetMin,
                OffsetMax = rect.offsetMax,
                LocalScale = rect.localScale
            };
        }

        private static void Restore(Box box)
        {
            box.Rect.anchorMin = box.AnchorMin;
            box.Rect.anchorMax = box.AnchorMax;
            box.Rect.pivot = box.Pivot;
            box.Rect.anchoredPosition = box.AnchoredPosition;
            box.Rect.sizeDelta = box.SizeDelta;
            box.Rect.offsetMin = box.OffsetMin;
            box.Rect.offsetMax = box.OffsetMax;
        }
    }
}
