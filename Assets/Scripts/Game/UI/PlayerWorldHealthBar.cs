using UnityEngine;
using UnityEngine.UI;
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
        private Transform follow;

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
        }

        private void LateUpdate()
        {
            if (follow == null || rect == null || canvasRect == null)
            {
                return;
            }

            Vector3 world = FollowPoint();
            Camera worldCamera = uiCamera != null ? uiCamera : Camera.main;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(worldCamera, world);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, uiCamera, out Vector2 local))
            {
                return;
            }

            Vector2 half = rect.sizeDelta * 0.5f;
            Vector2 canvasHalf = canvasRect.rect.size * 0.5f;
            local.x = Mathf.Clamp(local.x, -canvasHalf.x + half.x + screenPadding.x, canvasHalf.x - half.x - screenPadding.x);
            local.y = Mathf.Clamp(local.y, -canvasHalf.y + half.y + screenPadding.y, canvasHalf.y - half.y - screenPadding.y);
            rect.anchoredPosition = local;
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
