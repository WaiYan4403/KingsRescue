using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Holds the idle slash while the shot flies, then plays disappear as the lifetime runs out.
    /// </summary>
    public class SlashVisual : MonoBehaviour, IPoolable
    {
        private static readonly int DisappearHash = Animator.StringToHash("Disappear");

        [SerializeField] private float disappearLead = 0.35f;

        private Animator animator;
        private Projectile projectile;
        private bool hasDisappear;
        private bool disappearing;

        private void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            projectile = GetComponent<Projectile>();
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
            disappearing = false;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return;
            }

            animator.ResetTrigger(DisappearHash);
            animator.Rebind();
            animator.Update(0f);
        }

        public void OnDespawned()
        {
            disappearing = false;
        }

        private void Update()
        {
            if (disappearing || !hasDisappear || animator == null || projectile == null)
            {
                return;
            }

            float lead = disappearLead > 0.05f ? disappearLead : 0.35f;
            if (projectile.TimeLeft > lead)
            {
                return;
            }

            disappearing = true;
            animator.SetTrigger(DisappearHash);
        }
    }
}
