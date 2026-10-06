using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingsRescue.InputControl
{
    /// <summary>
    /// One-thumb analogue stick. The touch zone covers the lower part of the portrait
    /// screen and, in floating mode, the stick materialises wherever the thumb lands
    /// so the player never has to look for it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Hierarchy")]
        [Tooltip("Ring sprite. In floating mode this is repositioned to the thumb.")]
        [SerializeField] private RectTransform background;

        [Tooltip("Knob sprite, a child of the background.")]
        [SerializeField] private RectTransform handle;

        [Header("Feel")]
        [Tooltip("Maximum knob travel in canvas units. Full deflection is reached here.")]
        [SerializeField] private float handleRange = 70f;

        [SerializeField, Range(0f, 0.5f)] private float deadZone = 0.15f;

        [Tooltip("Spawn the stick under the thumb instead of keeping it in a fixed spot.")]
        [SerializeField] private bool floatingOrigin = true;

        [Tooltip("Hide the stick until a finger is down. The touch zone still receives input.")]
        [SerializeField] private bool hideWhenIdle = true;

        private RectTransform touchZone;
        private Canvas canvas;
        private Camera uiCamera;
        private CanvasGroup canvasGroup;
        private Vector2 restPosition;
        private int activePointerId = int.MinValue;

        /// <summary>Normalised stick deflection, magnitude clamped to 1.</summary>
        public Vector2 Value { get; private set; }

        /// <summary>True while a finger or the mouse is holding the stick.</summary>
        public bool IsHeld { get; private set; }

        private void Awake()
        {
            touchZone = (RectTransform)transform;
            canvas = GetComponentInParent<Canvas>();

            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            }

            if (background != null)
            {
                restPosition = background.anchoredPosition;

                canvasGroup = background.GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = background.gameObject.AddComponent<CanvasGroup>();
                }

                canvasGroup.blocksRaycasts = false;
            }

            // The zone itself must be raycastable or pointer events never arrive.
            var graphic = GetComponent<Graphic>();
            if (graphic != null)
            {
                graphic.raycastTarget = true;
            }

            ShowStick(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld)
            {
                return;
            }

            activePointerId = eventData.pointerId;
            IsHeld = true;

            ShowStick(true);

            if (floatingOrigin && background != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(touchZone, eventData.position, uiCamera, out Vector2 local))
            {
                background.anchoredPosition = local;
            }

            UpdateFromPointer(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!IsHeld || eventData.pointerId != activePointerId)
            {
                return;
            }

            UpdateFromPointer(eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != activePointerId)
            {
                return;
            }

            Release();
        }

        private void OnDisable()
        {
            Release();
        }

        private void UpdateFromPointer(PointerEventData eventData)
        {
            if (background == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, uiCamera, out Vector2 local))
            {
                return;
            }

            Vector2 clamped = Vector2.ClampMagnitude(local, handleRange);

            if (handle != null)
            {
                handle.anchoredPosition = clamped;
            }

            Vector2 raw = clamped / handleRange;
            float magnitude = raw.magnitude;

            if (magnitude <= deadZone)
            {
                Value = Vector2.zero;
                return;
            }

            // Re-map so the stick ramps from 0 at the dead zone edge rather than snapping.
            float scaled = Mathf.InverseLerp(deadZone, 1f, magnitude);
            Value = raw.normalized * scaled;
        }

        private void Release()
        {
            IsHeld = false;
            activePointerId = int.MinValue;
            Value = Vector2.zero;

            if (handle != null)
            {
                handle.anchoredPosition = Vector2.zero;
            }

            if (floatingOrigin && background != null)
            {
                background.anchoredPosition = restPosition;
            }

            ShowStick(false);
        }

        private void ShowStick(bool visible)
        {
            if (background == null)
            {
                return;
            }

            bool show = visible || !hideWhenIdle;
            background.gameObject.SetActive(show);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1f : 0f;
            }
        }
    }
}
