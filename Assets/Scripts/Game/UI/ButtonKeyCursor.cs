using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using KingsRescue.Audio;
using ToggleSwitch = Poker.UI.ToggleSwitch.ToggleSwitch;

namespace KingsRescue.UI
{
    /// <summary>
    /// Highlights the controls under an open menu and drives them from the keyboard or a gamepad.
    /// Nothing is highlighted until a direction is pressed. The highlight is the Outline_UI_M material.
    /// Space, Enter, or the south button activates the highlighted control.
    /// Call <see cref="Tick"/> only while that menu is open so the same keys still walk the hero.
    /// </summary>
    public static class ButtonKeyCursor
    {
        const float StickThreshold = 0.55f;
        const float RepeatDelay = 0.38f;
        const float RepeatRate = 0.13f;

        static readonly List<Entry> found = new List<Entry>(24);
        static readonly List<Entry> pool = new List<Entry>(24);
        static readonly List<Button> buttons = new List<Button>(16);
        static readonly List<Slider> sliders = new List<Slider>(8);
        static readonly List<ToggleSwitch> toggles = new List<ToggleSwitch>(4);
        static readonly List<TMP_InputField> fields = new List<TMP_InputField>(2);
        static readonly List<AchievementRowView> rows = new List<AchievementRowView>(24);
        static readonly List<CollectionSlotView> slots = new List<CollectionSlotView>(24);
        static readonly List<Selectable> owned = new List<Selectable>(24);
        static readonly List<Navigation> ownedNav = new List<Navigation>(24);
        static readonly int enableOutlineId = Shader.PropertyToID("_EnableOutline");

        static Entry current;
        static Component currentComponent;
        static Graphic outlined;
        static Material previousMaterial;
        static bool restoreDefaultMaterial;
        static Material outlineTemplate;
        static Material outlineInstance;
        static float rowBand = 1f;
        static CursorDriver driver;
        static Hold verticalHold;
        static Hold horizontalHold;
        static int pollFrame = -1;

        public static int Vertical { get; private set; }
        public static int Horizontal { get; private set; }
        public static bool SubmitPressed { get; private set; }
        public static bool CancelPressed { get; private set; }
        public static bool PausePressed { get; private set; }

        /// <summary>Drops this frame's horizontal move so a screen can use it for something else.</summary>
        public static void ConsumeHorizontal()
        {
            Horizontal = 0;
        }

        /// <summary>Reads keyboard and gamepad once per frame. Safe to call before <see cref="Tick"/>.</summary>
        public static void Poll()
        {
            if (pollFrame == Time.frameCount)
            {
                return;
            }

            pollFrame = Time.frameCount;
            Keyboard keyboard = Keyboard.current;
            Gamepad pad = Gamepad.current;
            Vector2 stick = pad != null ? pad.leftStick.ReadValue() : Vector2.zero;

            bool up = Held(keyboard, keyboard != null ? keyboard.wKey : null)
                || Held(keyboard, keyboard != null ? keyboard.upArrowKey : null)
                || (pad != null && pad.dpad.up.isPressed)
                || stick.y > StickThreshold;
            bool down = Held(keyboard, keyboard != null ? keyboard.sKey : null)
                || Held(keyboard, keyboard != null ? keyboard.downArrowKey : null)
                || (pad != null && pad.dpad.down.isPressed)
                || stick.y < -StickThreshold;
            bool left = Held(keyboard, keyboard != null ? keyboard.aKey : null)
                || Held(keyboard, keyboard != null ? keyboard.leftArrowKey : null)
                || (pad != null && pad.dpad.left.isPressed)
                || stick.x < -StickThreshold;
            bool right = Held(keyboard, keyboard != null ? keyboard.dKey : null)
                || Held(keyboard, keyboard != null ? keyboard.rightArrowKey : null)
                || (pad != null && pad.dpad.right.isPressed)
                || stick.x > StickThreshold;

            Vertical = ReadHold(up, down, ref verticalHold);
            Horizontal = ReadHold(left, right, ref horizontalHold);
            SubmitPressed = (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame
                    || keyboard.enterKey.wasPressedThisFrame
                    || keyboard.numpadEnterKey.wasPressedThisFrame))
                || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            CancelPressed = pad != null && pad.buttonEast.wasPressedThisFrame;
            PausePressed = pad != null && pad.startButton.wasPressedThisFrame;
        }

        public static void Tick(Transform root)
        {
            Poll();
            if (root == null || !root.gameObject.activeInHierarchy)
            {
                Release();
                return;
            }

            if (TryFocusedField(root, out TMP_InputField field))
            {
                if (SubmitPressed && Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame)
                {
                    field.onSubmit.Invoke(field.text);
                    field.DeactivateInputField();
                }

                return;
            }

            Collect(root);
            if (found.Count == 0)
            {
                Release();
                return;
            }

            Arm();
            current = Match(currentComponent);
            if (current != null)
            {
                ShowOutline(current);
            }
            else
            {
                HideOutline();
                currentComponent = null;
            }

            if (Vertical != 0)
            {
                Step(Vertical);
            }
            else if (Horizontal != 0)
            {
                if (current != null && current.kind == Entry.Kind.Slider)
                {
                    Nudge((Slider)current.component, Horizontal);
                }
                else
                {
                    Step(Horizontal);
                }
            }

            if (Vertical != 0 || Horizontal != 0 || !SubmitPressed || current == null)
            {
                return;
            }

            Activate(current);
        }

        public static void Release()
        {
            HideOutline();
            current = null;
            currentComponent = null;
            RecycleFound();

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
            RecycleFound();
            PruneOwned(root);

            root.GetComponentsInChildren(false, buttons);
            for (int i = 0; i < buttons.Count; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.IsInteractable())
                {
                    continue;
                }

                // Left and right change the hero or the upgrade card. Those controls stay out of the up/down list.
                if (IsCharacterArrow(button)
                    || button.GetComponent<CharacterSelectButton>() != null
                    || button.GetComponentInParent<UpgradeRowView>() != null)
                {
                    Own(button);
                    continue;
                }

                Add(Entry.Kind.Button, button, button, button.targetGraphic);
            }

            root.GetComponentsInChildren(false, sliders);
            for (int i = 0; i < sliders.Count; i++)
            {
                Slider slider = sliders[i];
                if (slider == null || !slider.IsInteractable() || slider.GetComponent<ToggleSwitch>() != null)
                {
                    continue;
                }

                Add(Entry.Kind.Slider, slider, slider, slider.targetGraphic);
            }

            root.GetComponentsInChildren(false, toggles);
            for (int i = 0; i < toggles.Count; i++)
            {
                ToggleSwitch toggle = toggles[i];
                if (toggle == null || !toggle.isActiveAndEnabled)
                {
                    continue;
                }

                Graphic graphic = toggle.GetComponent<Graphic>();
                if (graphic == null)
                {
                    graphic = toggle.GetComponentInChildren<Graphic>();
                }

                Add(Entry.Kind.Toggle, toggle, null, graphic);
            }

            root.GetComponentsInChildren(false, fields);
            for (int i = 0; i < fields.Count; i++)
            {
                TMP_InputField input = fields[i];
                if (input != null && input.IsInteractable())
                {
                    Add(Entry.Kind.Field, input, input, input.targetGraphic);
                }
            }

            root.GetComponentsInChildren(false, rows);
            for (int i = 0; i < rows.Count; i++)
            {
                AchievementRowView row = rows[i];
                if (row != null && row.isActiveAndEnabled && row.Achievement != null)
                {
                    Add(Entry.Kind.Row, row, null, row.GetComponent<Graphic>());
                }
            }

            root.GetComponentsInChildren(false, slots);
            for (int i = 0; i < slots.Count; i++)
            {
                CollectionSlotView slot = slots[i];
                if (slot != null && slot.isActiveAndEnabled && slot.Collected)
                {
                    Add(Entry.Kind.Slot, slot, null, slot.GetComponent<Graphic>());
                }
            }

            rowBand = float.MaxValue;
            for (int i = 0; i < found.Count; i++)
            {
                rowBand = Mathf.Min(rowBand, Height(found[i].rect) * 0.5f);
            }

            if (rowBand < 0.001f || float.IsInfinity(rowBand))
            {
                rowBand = 1f;
            }

            found.Sort(Compare);
        }

        static void Add(Entry.Kind kind, Component component, Selectable selectable, Graphic graphic)
        {
            Entry entry = pool.Count > 0 ? pool[pool.Count - 1] : new Entry();
            if (pool.Count > 0)
            {
                pool.RemoveAt(pool.Count - 1);
            }

            entry.kind = kind;
            entry.component = component;
            entry.rect = component.transform as RectTransform;
            entry.graphic = graphic;
            found.Add(entry);
            Own(selectable);
        }

        static bool IsCharacterArrow(Button button)
        {
            int count = button.onClick.GetPersistentEventCount();
            for (int i = 0; i < count; i++)
            {
                string method = button.onClick.GetPersistentMethodName(i);
                if (method == "SelectPreviousCharacter" || method == "SelectNextCharacter")
                {
                    return true;
                }
            }

            return false;
        }

        static void Own(Selectable selectable)
        {
            if (selectable == null || owned.Contains(selectable))
            {
                return;
            }

            owned.Add(selectable);
            ownedNav.Add(selectable.navigation);
            Navigation nav = selectable.navigation;
            nav.mode = Navigation.Mode.None;
            selectable.navigation = nav;
        }

        static void PruneOwned(Transform root)
        {
            for (int i = owned.Count - 1; i >= 0; i--)
            {
                Selectable selectable = owned[i];
                if (selectable != null && (selectable.transform == root || selectable.transform.IsChildOf(root)))
                {
                    continue;
                }

                if (selectable != null)
                {
                    selectable.navigation = ownedNav[i];
                }

                owned.RemoveAt(i);
                ownedNav.RemoveAt(i);
            }
        }

        static void RecycleFound()
        {
            for (int i = 0; i < found.Count; i++)
            {
                pool.Add(found[i]);
            }

            found.Clear();
        }

        static int Compare(Entry a, Entry b)
        {
            Vector3 positionA = a.rect != null ? a.rect.position : a.component.transform.position;
            Vector3 positionB = b.rect != null ? b.rect.position : b.component.transform.position;
            int rowA = Mathf.FloorToInt(positionA.y / rowBand);
            int rowB = Mathf.FloorToInt(positionB.y / rowBand);
            if (rowA != rowB)
            {
                return rowB.CompareTo(rowA);
            }

            return positionA.x.CompareTo(positionB.x);
        }

        static float Height(RectTransform rect)
        {
            if (rect == null)
            {
                return 24f;
            }

            return Mathf.Max(0.001f, Mathf.Abs(rect.rect.height * rect.lossyScale.y));
        }

        static void Step(int delta)
        {
            if (current == null)
            {
                Focus(delta > 0 ? found[0] : found[found.Count - 1], true);
                return;
            }

            if (found.Count < 2)
            {
                return;
            }

            int index = IndexOf(currentComponent);
            if (index < 0)
            {
                index = 0;
            }

            index = (index + delta + found.Count) % found.Count;
            Focus(found[index], true);
        }

        static void Focus(Entry entry, bool playSound)
        {
            if (entry == null)
            {
                return;
            }

            if (entry.component == currentComponent)
            {
                current = entry;
                ShowOutline(entry);
                return;
            }

            HideOutline();
            current = entry;
            currentComponent = entry.component;
            ShowOutline(entry);

            EnsureVisible(entry.rect);

            if (entry.kind == Entry.Kind.Row)
            {
                ((AchievementRowView)entry.component).Choose();
            }
            else if (entry.kind == Entry.Kind.Slot)
            {
                ((CollectionSlotView)entry.component).Choose();
            }

            if (playSound)
            {
                SoundEffects.Play(SoundId.UiBrowse);
            }
        }

        static void Activate(Entry entry)
        {
            switch (entry.kind)
            {
                case Entry.Kind.Button:
                    Button button = (Button)entry.component;
                    if (button.interactable && button.isActiveAndEnabled)
                    {
                        button.onClick.Invoke();
                    }

                    break;
                case Entry.Kind.Toggle:
                    ((ToggleSwitch)entry.component).Press();
                    break;
                case Entry.Kind.Field:
                    ((TMP_InputField)entry.component).ActivateInputField();
                    break;
                case Entry.Kind.Row:
                    ((AchievementRowView)entry.component).Choose();
                    break;
                case Entry.Kind.Slot:
                    ((CollectionSlotView)entry.component).Choose();
                    break;
            }
        }

        static void Nudge(Slider slider, int direction)
        {
            if (slider == null)
            {
                return;
            }

            float span = slider.maxValue - slider.minValue;
            float step = slider.wholeNumbers ? 1f : Mathf.Max(span * 0.05f, 0.01f);
            slider.value = Mathf.Clamp(slider.value + direction * step, slider.minValue, slider.maxValue);
        }

        static void ShowOutline(Entry entry)
        {
            Graphic graphic = OutlineGraphic(entry);
            if (graphic == null)
            {
                return;
            }

            Material template = OutlineTemplate();
            if (template == null)
            {
                return;
            }

            if (outlineInstance == null)
            {
                outlineInstance = new Material(template);
                outlineInstance.name = "Outline_UI_M (cursor)";
                if (outlineInstance.HasProperty(enableOutlineId))
                {
                    outlineInstance.SetFloat(enableOutlineId, 1f);
                }
            }

            if (outlined == graphic)
            {
                Material currentMaterial = graphic.material;
                if (currentMaterial != outlineInstance && currentMaterial != graphic.defaultMaterial)
                {
                    previousMaterial = currentMaterial;
                    restoreDefaultMaterial = false;
                }

                if (graphic.material != outlineInstance)
                {
                    graphic.material = outlineInstance;
                }

                return;
            }

            HideOutline();
            Material existing = graphic.material;
            restoreDefaultMaterial = existing == null || existing == graphic.defaultMaterial;
            previousMaterial = restoreDefaultMaterial ? null : existing;
            outlined = graphic;
            graphic.material = outlineInstance;
        }

        static void HideOutline()
        {
            if (outlined == null)
            {
                return;
            }

            outlined.material = restoreDefaultMaterial ? null : previousMaterial;
            outlined = null;
            previousMaterial = null;
            restoreDefaultMaterial = false;
        }

        static Graphic OutlineGraphic(Entry entry)
        {
            if (IsSolid(entry.graphic))
            {
                return entry.graphic;
            }

            Graphic[] graphics = entry.component.GetComponentsInChildren<Graphic>(false);
            Graphic best = null;
            float bestArea = 0f;
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (!IsSolid(graphic))
                {
                    continue;
                }

                RectTransform rect = graphic.transform as RectTransform;
                float area = rect != null ? Mathf.Abs(rect.rect.width * rect.rect.height) : 0f;
                if (area >= bestArea)
                {
                    best = graphic;
                    bestArea = area;
                }
            }

            return best != null ? best : entry.graphic;
        }

        static bool IsSolid(Graphic graphic)
        {
            if (graphic == null || !graphic.enabled || graphic is TMP_Text)
            {
                return false;
            }

            return graphic.color.a >= 0.2f;
        }

        static Material OutlineTemplate()
        {
            if (outlineTemplate == null)
            {
                outlineTemplate = Resources.Load<Material>("Outline_UI_M");
            }

            return outlineTemplate;
        }

        static Entry Match(Component component)
        {
            int index = IndexOf(component);
            return index >= 0 ? found[index] : null;
        }

        static int IndexOf(Component component)
        {
            if (component == null)
            {
                return -1;
            }

            for (int i = 0; i < found.Count; i++)
            {
                if (found[i].component == component)
                {
                    return i;
                }
            }

            return -1;
        }

        static bool TryFocusedField(Transform root, out TMP_InputField field)
        {
            field = null;
            EventSystem events = EventSystem.current;
            if (events == null || events.currentSelectedGameObject == null)
            {
                return false;
            }

            field = events.currentSelectedGameObject.GetComponent<TMP_InputField>();
            if (field == null || !field.isFocused)
            {
                return false;
            }

            return field.transform == root || field.transform.IsChildOf(root);
        }

        /// <summary>Scrolls the parent scroll view just enough to show this rectangle.</summary>
        public static void Reveal(RectTransform item)
        {
            EnsureVisible(item);
        }

        static void EnsureVisible(RectTransform item)
        {
            if (item == null)
            {
                return;
            }

            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = item.GetComponentInParent<ScrollRect>();
            if (scroll == null || scroll.viewport == null || scroll.content == null)
            {
                return;
            }

            RectTransform view = scroll.viewport;
            Bounds bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(view, item);
            Rect rect = view.rect;
            const float pad = 12f;
            float dy = 0f;
            if (bounds.max.y > rect.yMax - pad)
            {
                dy = bounds.max.y - (rect.yMax - pad);
            }
            else if (bounds.min.y < rect.yMin + pad)
            {
                dy = bounds.min.y - (rect.yMin + pad);
            }

            if (Mathf.Abs(dy) < 1f)
            {
                return;
            }

            Vector3 local = scroll.content.InverseTransformVector(view.TransformVector(new Vector3(0f, dy, 0f)));
            scroll.content.anchoredPosition -= new Vector2(0f, local.y);
        }

        static int ReadHold(bool negative, bool positive, ref Hold hold)
        {
            int dir = 0;
            if (negative && !positive)
            {
                dir = -1;
            }
            else if (positive && !negative)
            {
                dir = 1;
            }

            if (dir == 0)
            {
                hold.dir = 0;
                return 0;
            }

            if (dir != hold.dir)
            {
                hold.dir = dir;
                hold.next = Time.unscaledTime + RepeatDelay;
                return dir;
            }

            if (Time.unscaledTime >= hold.next)
            {
                hold.next = Time.unscaledTime + RepeatRate;
                return dir;
            }

            return 0;
        }

        static bool Held(Keyboard keyboard, KeyControl key)
        {
            return keyboard != null && key != null && key.isPressed;
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

        sealed class Entry
        {
            public enum Kind
            {
                Button,
                Slider,
                Toggle,
                Field,
                Row,
                Slot
            }

            public Kind kind;
            public Component component;
            public RectTransform rect;
            public Graphic graphic;
        }

        struct Hold
        {
            public int dir;
            public float next;
        }

        /// <summary>
        /// Drops the EventSystem selection after UI input has run, so submit is handled once here
        /// and the built-in move actions do not also step the highlight. A focused text field is left alone.
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

                if (events.currentSelectedGameObject.GetComponent<TMP_InputField>() is TMP_InputField input && input.isFocused)
                {
                    return;
                }

                Selectable selected = events.currentSelectedGameObject.GetComponentInParent<Selectable>();
                if (selected != null && owned.Contains(selected))
                {
                    events.SetSelectedGameObject(null);
                }
            }
        }
    }
}
