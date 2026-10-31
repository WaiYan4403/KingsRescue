using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KingsRescue.Core;
using KingsRescue.Meta;

namespace KingsRescue.InputControl
{
    /// <summary>
    /// One-thumb analogue stick. The touch zone covers the lower part of the portrait
    /// screen and, in floating mode, the stick materialises wherever the thumb lands
    /// so the player never has to look for it.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class VirtualJoystick : SingletonBehaviour<VirtualJoystick>, IPointerDownHandler, IDragHandler, IPointerUpHandler
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

        [Tooltip("Spawn the stick under the thumb instead of keeping it in a fixed spot. Ignored while the fixed-joystick setting is on.")]
        [SerializeField] private bool floatingOrigin = true;

        [Tooltip("Hide the stick until a finger is down. The touch zone still receives input. A fixed stick stays visible.")]
        [SerializeField] private bool hideWhenIdle = true;

        [Tooltip("Gap between the screen edge and the outside of a fixed stick.")]
        [SerializeField] private float edgePadding = 64f;

        private RectTransform touchZone;
        private Canvas canvas;
        private Camera uiCamera;
        private CanvasGroup canvasGroup;
        private Vector2 restPosition;
        private int activePointerId = int.MinValue;
        private bool ready;
        private bool placing;

        /// <summary>Normalised stick deflection, magnitude clamped to 1.</summary>
        public Vector2 Value { get; private set; }

        /// <summary>True while a finger or the mouse is holding the stick.</summary>
        public bool IsHeld { get; private set; }

        protected override void Awake()
        {
            base.Awake();
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
            ready = true;
            ApplyPlacement();
            JoystickHandGuide.Ensure(this);
        }

        private void Start()
        {
            ApplyPlacement();
        }

        private void OnRectTransformDimensionsChange()
        {
            if (!ready || IsHeld || !GameSettings.FixedJoystick)
            {
                return;
            }

            ApplyPlacement();
        }

        /// <summary>Moves the stick to the saved corner, or back to its floating rest spot.</summary>
        public void ApplyPlacement()
        {
            if (placing || background == null)
            {
                return;
            }

            placing = true;

            if (GameSettings.FixedJoystick)
            {
                background.anchoredPosition = FixedCorner();
            }
            else if (!IsHeld)
            {
                background.anchoredPosition = restPosition;
            }

            ShowStick(IsHeld);
            placing = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsHeld)
            {
                return;
            }

            if (GameSettings.FixedJoystick)
            {
                ApplyPlacement();
                if (!PointerOnStick(eventData))
                {
                    return;
                }
            }

            JoystickHandGuide.NotifyUsed();

            activePointerId = eventData.pointerId;
            IsHeld = true;

            ShowStick(true);

            if (!GameSettings.FixedJoystick && floatingOrigin && background != null &&
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

            // The knob always travels the full handleRange. Sensitivity shortens or lengthens the
            // drag that counts as full deflection (0.75 is a shorter drag, 0.25 is a longer one).
            float effectiveRange = handleRange / GameSettings.JoystickSensitivity;
            Vector2 raw = Vector2.ClampMagnitude(local / effectiveRange, 1f);
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

            if (GameSettings.FixedJoystick)
            {
                if (background != null)
                {
                    background.anchoredPosition = FixedCorner();
                }
            }
            else if (floatingOrigin && background != null)
            {
                background.anchoredPosition = restPosition;
            }

            ShowStick(false);
        }

        private bool PointerOnStick(PointerEventData eventData)
        {
            if (background == null)
            {
                return false;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(background, eventData.position, uiCamera, out Vector2 local))
            {
                return false;
            }

            float radius = Mathf.Max(handleRange, background.rect.width * 0.5f);
            return local.magnitude <= radius * 1.15f;
        }

        private Vector2 FixedCorner()
        {
            RectTransform space = canvas != null ? (RectTransform)canvas.transform : touchZone;
            Rect area = space.rect;
            float half = background != null ? background.rect.width * 0.5f : handleRange;
            float inset = half + Mathf.Max(0f, edgePadding);
            float x = GameSettings.JoystickOnRight ? area.xMax - inset : area.xMin + inset;
            float y = area.yMin + inset;
            Vector3 world = space.TransformPoint(new Vector3(x, y, 0f));
            Vector3 local = touchZone.InverseTransformPoint(world);
            return local;
        }

        private void ShowStick(bool visible)
        {
            if (background == null)
            {
                return;
            }

            bool show = visible || !hideWhenIdle || GameSettings.FixedJoystick;
            background.gameObject.SetActive(show);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = show ? 1f : 0f;
                canvasGroup.blocksRaycasts = GameSettings.FixedJoystick;
            }
        }
    }
}
