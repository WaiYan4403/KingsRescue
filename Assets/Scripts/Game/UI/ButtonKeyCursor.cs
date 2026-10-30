using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using KingsRescue.Audio;

namespace KingsRescue.UI
{
    /// <summary>
    /// Highlights the buttons under an open menu and drives them from the keyboard.
    /// Nothing is highlighted until W, S, or an arrow key is pressed. Space presses the highlighted button.
    /// Call <see cref="Tick"/> only while that menu is open so the same keys still walk the hero.
    /// </summary>
    public static class ButtonKeyCursor
    {
        static readonly List<Button> found = new List<Button>(12);
        static readonly List<Button> owned = new List<Button>(12);
        static readonly List<Navigation> ownedNav = new List<Navigation>(12);
        static readonly Color highlight = new Color(1f, 0.86f, 0.35f, 1f);

        static Button current;
        static Color baseColor;
        static bool colored;
        static float rowBand = 1f;
        static CursorDriver driver;

        public static void Tick(Transform root)
        {
            if (root == null || !root.gameObject.activeInHierarchy || Keyboard.current == null)
            {
                Release();
                return;
            }

            Collect(root);
            if (found.Count == 0)
            {
                Release();
                return;
            }

            Arm();

            if (current != null && found.Contains(current))
            {
                ApplyTint();
            }
            else
            {
                RestoreColor();
                current = null;
            }

            Keyboard keyboard = Keyboard.current;
            int delta = 0;
            if (Pressed(keyboard.wKey) || Pressed(keyboard.upArrowKey))
            {
                delta = -1;
            }
            else if (Pressed(keyboard.sKey) || Pressed(keyboard.downArrowKey))
            {
                delta = 1;
            }

            if (delta != 0)
            {
                if (current == null)
                {
                    Focus(delta > 0 ? found[0] : found[found.Count - 1], true);
                }
                else if (found.Count > 1)
                {
                    int index = found.IndexOf(current);
                    if (index < 0)
                    {
                        index = 0;
                    }

                    index = (index + delta + found.Count) % found.Count;
                    Focus(found[index], true);
                }
            }

            if (Pressed(keyboard.spaceKey) && current != null && current.interactable && current.isActiveAndEnabled)
            {
                current.onClick.Invoke();
            }
        }

        public static void Release()
        {
            RestoreColor();
            current = null;
            found.Clear();

            for (int i = 0; i < owned.Count; i++)
            {
                if (owned[i] != null)
                {
                    owned[i].navigation = ownedNav[i];
                }
            }

            owned.Clear();
            ownedNav.Clear();
        }

        static void Collect(Transform root)
        {
            found.Clear();
            root.GetComponentsInChildren(false, found);
            for (int i = found.Count - 1; i >= 0; i--)
            {
                Button button = found[i];
                if (button == null || !button.isActiveAndEnabled || !button.interactable)
                {
                    found.RemoveAt(i);
                    continue;
                }

                Own(button);
            }

            rowBand = float.MaxValue;
            for (int i = 0; i < found.Count; i++)
            {
                rowBand = Mathf.Min(rowBand, ButtonHeight(found[i]) * 0.5f);
            }

            if (rowBand < 0.001f || float.IsInfinity(rowBand))
            {
                rowBand = 1f;
            }

            found.Sort(Compare);
        }

        static void Own(Button button)
        {
            if (owned.Contains(button))
            {
                return;
            }

            owned.Add(button);
            ownedNav.Add(button.navigation);
            Navigation nav = button.navigation;
            nav.mode = Navigation.Mode.None;
            button.navigation = nav;
        }

        static int Compare(Button a, Button b)
        {
            int rowA = Mathf.FloorToInt(a.transform.position.y / rowBand);
            int rowB = Mathf.FloorToInt(b.transform.position.y / rowBand);
            if (rowA != rowB)
            {
                return rowB.CompareTo(rowA);
            }

            return a.transform.position.x.CompareTo(b.transform.position.x);
        }

        static float ButtonHeight(Button button)
        {
            RectTransform rect = button.transform as RectTransform;
            if (rect == null)
            {
                return 24f;
            }

            return Mathf.Max(0.001f, Mathf.Abs(rect.rect.height * rect.lossyScale.y));
        }

        static void Focus(Button button, bool playSound)
        {
            if (button == current)
            {
                ApplyTint();
                return;
            }

            RestoreColor();
            current = button;
            Graphic graphic = button.targetGraphic;
            if (graphic != null)
            {
                baseColor = graphic.color;
                colored = true;
                ApplyTint();
            }

            if (playSound)
            {
                SoundEffects.Play(SoundId.UiBrowse);
            }
        }

        static void ApplyTint()
        {
            if (!colored || current == null || current.targetGraphic == null)
            {
                return;
            }

            current.targetGraphic.color = Color.Lerp(baseColor, highlight, 0.55f);
        }

        static void RestoreColor()
        {
            if (!colored || current == null)
            {
                colored = false;
                return;
            }

            if (current.targetGraphic != null)
            {
                current.targetGraphic.color = baseColor;
            }

            colored = false;
        }

        static bool Pressed(UnityEngine.InputSystem.Controls.KeyControl key)
        {
            return key != null && key.wasPressedThisFrame;
        }

        static void Arm()
        {
            if (driver == null)
            {
                GameObject host = new GameObject(nameof(ButtonKeyCursor));
                host.hideFlags = HideFlags.HideAndDontSave;
                Object.DontDestroyOnLoad(host);
                driver = host.AddComponent<CursorDriver>();
            }

            driver.armed = true;
        }

        /// <summary>
        /// Drops the EventSystem selection after UI input has run, so Space is handled once here
        /// and the built-in move actions do not also step the highlight.
        /// </summary>
        sealed class CursorDriver : MonoBehaviour
        {
            public bool armed;

            void LateUpdate()
            {
                if (!armed)
                {
                    return;
                }

                armed = false;
                EventSystem events = EventSystem.current;
                if (events == null || events.currentSelectedGameObject == null)
                {
                    return;
                }

                Button selected = events.currentSelectedGameObject.GetComponentInParent<Button>();
                if (selected != null && owned.Contains(selected))
                {
                    events.SetSelectedGameObject(null);
                }
            }
        }
    }
}
