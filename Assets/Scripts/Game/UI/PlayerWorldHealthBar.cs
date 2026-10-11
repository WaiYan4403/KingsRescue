using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Player;

namespace KingsRescue.UI
{
    /// <summary>
    /// Overlay HP bar locked under the hero's feet. Lives on the screen-space canvas
    /// so it draws above every world sorting layer, including enemies and props.
    /// </summary>
    public class PlayerWorldHealthBar : MonoBehaviour
    {
        [SerializeField] private PlayerHealth health;
        [SerializeField] private SpriteRenderer followRenderer;
        [SerializeField] private Image fill;
        [SerializeField] private Vector2 worldOffset = new Vector2(0f, -0.18f);
        [SerializeField] private Vector2 screenPadding = new Vector2(8f, 8f);

        private RectTransform rect;
        private RectTransform canvasRect;
        private Canvas canvas;
        private Camera uiCamera;
        private Camera worldCamera;
        private Transform follow;
        private GameObject overlayRoot;
        private CanvasGroup visibility;
        private Vector2 lastLocal;

        private void Awake()
        {
            rect = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                canvasRect = canvas.transform as RectTransform;
                uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }

            ResolveFollow();
            DetachFromHudCanvas();
        }

        private void Start()
        {
            ResolveFollow();

            if (health != null)
            {
                health.HealthChanged += OnHealthChanged;
                OnHealthChanged(health.Current, health.Max);
            }
        }

        private void OnDestroy()
        {
            if (health != null)
            {
                health.HealthChanged -= OnHealthChanged;
            }

            if (overlayRoot != null)
            {
                GameObject root = overlayRoot;
                overlayRoot = null;
                Destroy(root);
            }
        }

        private void LateUpdate()
        {
            bool covered = CoveredByMenu();
            if (visibility != null && visibility.alpha != (covered ? 0f : 1f))
            {
                visibility.alpha = covered ? 0f : 1f;
            }

            if (covered || follow == null || rect == null || canvasRect == null)
            {
                return;
            }

            Vector3 world = FollowPoint();
            if (worldCamera == null)
            {
                worldCamera = uiCamera != null ? uiCamera : Camera.main;
            }

            Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCamera, world);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local))
            {
                return;
            }

            Vector2 half = rect.sizeDelta * 0.5f;
            Vector2 canvasHalf = canvasRect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -canvasHalf.x + half.x + screenPadding.x, canvasHalf.x - half.x - screenPadding.x);
            local.y = Mathf.Clamp(local.y, -canvasHalf.y + half.y + screenPadding.y, canvasHalf.y - half.y - screenPadding.y);
            if ((local - lastLocal).sqrMagnitude < 0.25f)
            {
                return;
            }

            lastLocal = local;
            rect.anchoredPosition = local;
        }

        /// <summary>
        /// The bar follows the hero every frame. On the HUD canvas that rebuilds every label too.
        /// </summary>
        private void DetachFromHudCanvas()
        {
            visibility = GetComponent<CanvasGroup>();
            if (visibility == null)
            {
                visibility = gameObject.AddComponent<CanvasGroup>();
            }

            visibility.interactable = false;
            visibility.blocksRaycasts = false;

            if (canvas == null)
            {
                return;
            }

            int shared = canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length;
            int own = GetComponentsInChildren<UnityEngine.UI.Graphic>(true).Length;
            if (shared <= own)
            {
                return;
            }

            GameObject host = new GameObject("WorldHealthCanvas");
            RectTransform hostRect = host.AddComponent<RectTransform>();
            hostRect.anchorMin = Vector2.zero;
            hostRect.anchorMax = Vector2.one;
            hostRect.offsetMin = Vector2.zero;
            hostRect.offsetMax = Vector2.zero;

            Canvas hud = canvas;
            Canvas overlay = host.AddComponent<Canvas>();
            overlay.renderMode = hud.renderMode;
            overlay.worldCamera = hud.worldCamera;
            overlay.planeDistance = hud.planeDistance;
            overlay.sortingLayerID = hud.sortingLayerID;
            overlay.sortingOrder = hud.sortingOrder - 1;

            CanvasScaler source = canvas.GetComponent<CanvasScaler>();
            CanvasScaler scaler = host.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            if (source != null)
            {
                scaler.uiScaleMode = source.uiScaleMode;
                scaler.referenceResolution = source.referenceResolution;
                scaler.screenMatchMode = source.screenMatchMode;
                scaler.matchWidthOrHeight = source.matchWidthOrHeight;
            }

            transform.SetParent(host.transform, false);
            overlayRoot = host;
            canvas = overlay;
            canvasRect = hostRect;
            uiCamera = overlay.renderMode == RenderMode.ScreenSpaceOverlay ? null : overlay.worldCamera;
            worldCamera = uiCamera != null ? uiCamera : Camera.main;
        }

        private static bool CoveredByMenu()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return false;
            }

            GameState state = game.State;
            return state == GameState.LevelUp || state == GameState.Chest || state == GameState.GameOver;
        }

        private Vector3 FollowPoint()
        {
            if (followRenderer != null)
            {
                Bounds bounds = followRenderer.bounds;
                return new Vector3(bounds.center.x + worldOffset.x, bounds.min.y + worldOffset.y, follow.position.z);
            }

            return follow.position + (Vector3)worldOffset;
        }

        private void ResolveFollow()
        {
            if (health == null)
            {
                health = PlayerController.Instance != null
                    ? PlayerController.Instance.Health
                    : FindAnyObjectByType<PlayerHealth>();
            }

            if (health != null)
            {
                follow = health.transform;
                if (followRenderer == null)
                {
                    followRenderer = health.GetComponentInChildren<SpriteRenderer>();
                }
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (fill != null)
            {
                fill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            }
        }
    }
}
