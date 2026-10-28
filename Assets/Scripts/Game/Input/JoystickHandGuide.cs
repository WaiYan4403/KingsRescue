using UnityEngine;
using UnityEngine.UI;
using KingsRescue.Core;

namespace KingsRescue.InputControl
{
    /// <summary>
    /// Shows the authored Hand Guide after the run has been going for a moment,
    /// and only when the player still has not touched the joystick.
    /// One touch hides it for the rest of the run.
    /// </summary>
    public class JoystickHandGuide : MonoBehaviour
    {
        const float RevealDelay = 1.5f;

        VirtualJoystick joystick;
        GameObject panel;
        bool dismissed;
        bool visible;
        float elapsed;

        public static void Ensure(VirtualJoystick stick)
        {
            if (stick == null)
            {
                return;
            }

            JoystickHandGuide guide = stick.GetComponent<JoystickHandGuide>();
            if (guide == null)
            {
                guide = stick.gameObject.AddComponent<JoystickHandGuide>();
            }

            guide.joystick = stick;
        }

        /// <summary>The stick was touched. The guide stays hidden for the rest of this run.</summary>
        public static void NotifyUsed()
        {
            JoystickHandGuide guide = Instance;
            if (guide != null)
            {
                guide.Dismiss();
            }
        }

        static JoystickHandGuide Instance
        {
            get
            {
                VirtualJoystick stick = VirtualJoystick.Instance;
                return stick != null ? stick.GetComponent<JoystickHandGuide>() : null;
            }
        }

        private void Awake()
        {
            panel = FindPanel();
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Update()
        {
            if (dismissed)
            {
                return;
            }

            if (joystick != null && joystick.IsHeld)
            {
                Dismiss();
                return;
            }

            if (visible || !RunHasStarted())
            {
                return;
            }

            elapsed += Time.deltaTime;
            if (elapsed < RevealDelay)
            {
                return;
            }

            Reveal();
        }

        private void Dismiss()
        {
            dismissed = true;
            visible = false;
            if (panel != null)
            {
                panel.SetActive(false);
            }
        }

        private void Reveal()
        {
            if (panel == null)
            {
                panel = FindPanel();
            }

            if (panel == null)
            {
                dismissed = true;
                return;
            }

            visible = true;
            panel.SetActive(true);
            LetTouchesThrough(panel);

            Animator animator = panel.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.HasState(0, Animator.StringToHash("HandTuto")))
            {
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.Play("HandTuto", 0, 0f);
                animator.Update(0f);
            }
        }

        static bool RunHasStarted()
        {
            GameManager game = GameManager.Instance;
            if (game == null)
            {
                return false;
            }

            GameState state = game.State;
            return state == GameState.Intro || state == GameState.Playing;
        }

        static GameObject FindPanel()
        {
            Transform[] transforms = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include);
            for (int i = 0; i < transforms.Length; i++)
            {
                Transform transform = transforms[i];
                if (transform != null && IsHandGuide(transform.name))
                {
                    return transform.gameObject;
                }
            }

            return null;
        }

        static bool IsHandGuide(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return false;
            }

            string trimmed = name.Trim();
            return trimmed.Equals("Hand Guide", System.StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("Hand Guide Panel", System.StringComparison.OrdinalIgnoreCase)
                || trimmed.Equals("HandGuide", System.StringComparison.OrdinalIgnoreCase);
        }

        static void LetTouchesThrough(GameObject root)
        {
            CanvasGroup group = root.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = root.AddComponent<CanvasGroup>();
            }

            group.blocksRaycasts = false;
            group.interactable = false;

            Graphic[] graphics = root.GetComponentsInChildren<Graphic>(true);
            for (int i = 0; i < graphics.Length; i++)
            {
                graphics[i].raycastTarget = false;
            }
        }
    }
}
