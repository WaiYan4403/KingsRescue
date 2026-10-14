using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Restarts a pooled slash so each shot plays from the first frame.
    /// </summary>
    public class SlashVisual : MonoBehaviour, IPoolable
    {
        private static readonly int DisappearHash = Animator.StringToHash("Disappear");

        private Animator animator;
        private bool hasDisappear;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            if (animator == null)
            {
                return;
            }

            animator.keepAnimatorStateOnDisable = true;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            for (int i = 0; i < animator.parameterCount; i++)
            {
                if (animator.GetParameter(i).nameHash == DisappearHash)
                {
                    hasDisappear = true;
                    break;
                }
            }
        }

        public void OnSpawned()
        {
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.Rebind();
            animator.Update(0f);
            if (hasDisappear)
            {
                animator.SetTrigger(DisappearHash);
            }
        }

        public void OnDespawned()
        {
        }
    }
}
