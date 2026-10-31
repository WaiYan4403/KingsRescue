using UnityEngine;
using UnityEngine.InputSystem;
using KingsRescue.Core;

namespace KingsRescue.InputControl
{
    /// <summary>
    /// Collapses the on-screen stick, a gamepad and the keyboard into one movement axis.
    /// Movement is the only input in this game; attacking is automatic.
    /// </summary>
    public class PlayerInputReader : SingletonBehaviour<PlayerInputReader>
    {
        [Header("Touch")]
        [SerializeField] private VirtualJoystick joystick;

        [Header("Input System (optional)")]
        [Tooltip("Assign Assets/InputSystem_Actions. Leave empty to use the built-in keyboard and gamepad fallback.")]
        [SerializeField] private InputActionAsset actionAsset;
        [SerializeField] private string actionMapName = "Player";
        [SerializeField] private string moveActionName = "Move";

        private InputAction moveAction;

        /// <summary>Normalised-ish movement request, magnitude clamped to 1.</summary>
        public Vector2 MoveAxis { get; private set; }

        /// <summary>True when the player is actively steering this frame.</summary>
        public bool IsMoving => MoveAxis.sqrMagnitude > 0.0001f;

        public static Vector2 Axis => Instance != null ? Instance.MoveAxis : Vector2.zero;

        protected override void Awake()
        {
            base.Awake();

            if (actionAsset != null)
            {
                InputActionMap map = actionAsset.FindActionMap(actionMapName, false);
                moveAction = map != null ? map.FindAction(moveActionName, false) : null;

                if (moveAction == null)
                {
                    Debug.LogWarning($"PlayerInputReader could not find action '{actionMapName}/{moveActionName}'. Falling back to direct device polling.", this);
                }
            }
        }

        private void OnEnable()
        {
            moveAction?.Enable();
        }

        private void OnDisable()
        {
            moveAction?.Disable();
        }

        private void Update()
        {
            Vector2 axis = Vector2.zero;

            if (joystick == null)
            {
                joystick = VirtualJoystick.Instance;
            }

            if (joystick != null && joystick.IsHeld)
            {
                axis = joystick.Value;
            }
            else if (moveAction != null)
            {
                axis = moveAction.ReadValue<Vector2>();
                Vector2 dpad = ReadDpad();
                if (dpad.sqrMagnitude > axis.sqrMagnitude)
                {
                    axis = dpad;
                }
            }
            else
            {
                axis = ReadFallbackDevices();
            }

            MoveAxis = Vector2.ClampMagnitude(axis, 1f);
        }

        private static Vector2 ReadFallbackDevices()
        {
            Gamepad pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    return stick;
                }

                Vector2 dpad = pad.dpad.ReadValue();
                if (dpad.sqrMagnitude > 0.01f)
                {
                    return dpad;
                }
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return Vector2.zero;
            }

            var axis = new Vector2(
                (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f),
                (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f)
                    - (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f));

            return axis;
        }

        static Vector2 ReadDpad()
        {
            Gamepad pad = Gamepad.current;
            return pad != null ? pad.dpad.ReadValue() : Vector2.zero;
        }
    }
}
