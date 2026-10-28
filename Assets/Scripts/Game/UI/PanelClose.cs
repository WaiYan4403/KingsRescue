using UnityEngine;
using UnityEngine.UI;

namespace KingsRescue.UI
{
    /// <summary>
    /// Starts an authored close clip (ScrollCover_Close and the like) and reports how long the
    /// panel has to stay active for that clip to finish.
    /// </summary>
    public static class PanelClose
    {
        public static bool TryPlay(GameObject panel, out float duration)
        {
            duration = 0f;
            if (panel == null || !panel.activeInHierarchy)
            {
                return false;
            }

            Animator animator = panel.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimationClip close = null;
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name.IndexOf("Close", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    close = clip;
                    break;
                }
            }

            if (close == null)
            {
                return false;
            }

            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.speed = 1f;
            animator.Play(close.name, 0, 0f);
            animator.Update(0f);
            duration = Mathf.Max(0.05f, close.length);
            return true;
        }

        /// <summary>
        /// Samples ScrollCover_Open at the first frame before the canvas lays the card out.
        /// An inactive animator otherwise shows the saved open pose for a frame, and a layout
        /// group then drops a zero-size card into that pose. Later opens look fine because the
        /// close clip leaves the card rolled up.
        /// </summary>
        public static void PlayOpen(GameObject panel)
        {
            if (panel == null || !panel.activeInHierarchy)
            {
                return;
            }

            Animator animator = panel.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            AnimationClip open = null;
            AnimationClip[] clips = animator.runtimeAnimatorController.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                AnimationClip clip = clips[i];
                if (clip != null && clip.name == "ScrollCover_Open")
                {
                    open = clip;
                    break;
                }
            }

            if (open == null)
            {
                return;
            }

            Transform card = panel.transform.Find("Parent/Card");
            LayoutElement element = card != null ? card.GetComponent<LayoutElement>() : null;
            RestoreCollapsed(panel.transform.Find("Parent") as RectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 0.5f), new Vector2(540f, -870f), new Vector2(1000f, 1600f), false);
            bool cardWasCollapsed = RestoreCollapsed(card as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 644f), new Vector2(900f, 1200f), true);
            RestoreCollapsed(panel.transform.Find("Parent/Scroll") as RectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.2f), Vector2.zero, new Vector2(1012.5f, 243.75f), false);
            if (cardWasCollapsed && card is RectTransform rolled)
            {
                rolled.localScale = new Vector3(1f, 0f, 1f);
            }

            if (element != null)
            {
                element.ignoreLayout = true;
            }

            Transform place = panel.transform.Find("Your Place");
            if (place != null)
            {
                place.gameObject.SetActive(false);
            }

            Canvas.ForceUpdateCanvases();
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.speed = 1f;
            animator.Play(open.name, 0, 0f);
            animator.Update(0f);
        }

        /// <summary>
        /// A layout group collapses an active scroll panel to a zero rect. The open clip
        /// never keys that size, so a collapsed card unrolls as a line. Put the closed pose back.
        /// </summary>
        private static bool RestoreCollapsed(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 fallbackSize, bool usePreferred)
        {
            if (rect == null || (rect.rect.width > 1f && rect.rect.height > 1f))
            {
                return false;
            }

            float width = fallbackSize.x;
            float height = fallbackSize.y;
            if (usePreferred)
            {
                LayoutElement element = rect.GetComponent<LayoutElement>();
                if (element != null && element.preferredWidth > 1f)
                {
                    width = element.preferredWidth;
                }

                if (element != null && element.preferredHeight > 1f)
                {
                    height = element.preferredHeight;
                }
            }

            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, height);
            return true;
        }
    }
}
