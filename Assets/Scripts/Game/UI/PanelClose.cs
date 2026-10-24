using UnityEngine;

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
    }
}
