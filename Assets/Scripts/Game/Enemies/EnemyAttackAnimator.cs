using UnityEngine;

namespace KingsRescue.Enemies
{
    /// <summary>
    /// Plays the Attack trigger when this enemy lands a hit or fires a shot.
    /// Controllers without that parameter are left alone.
    /// </summary>
    public class EnemyAttackAnimator : MonoBehaviour
    {
        private static readonly int AttackHash = Animator.StringToHash("Attack");

        private Animator animator;
        private ContactDamage contact;
        private bool hasAttack;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            contact = GetComponent<ContactDamage>();
            hasAttack = HasAttackTrigger(animator);
        }

        private void OnEnable()
        {
            if (contact != null)
            {
                contact.HitPlayer += PlayAttack;
            }
        }

        private void OnDisable()
        {
            if (contact != null)
            {
                contact.HitPlayer -= PlayAttack;
            }
        }

        public void PlayAttack()
        {
            if (!hasAttack || animator == null || !isActiveAndEnabled)
            {
                return;
            }

            animator.ResetTrigger(AttackHash);
            animator.SetTrigger(AttackHash);
        }

        private static bool HasAttackTrigger(Animator body)
        {
            if (body == null || body.runtimeAnimatorController == null)
            {
                return false;
            }

            AnimatorControllerParameter[] parameters = body.parameters;
            for (int i = 0; i < parameters.Length; i++)
            {
                if (parameters[i].type == AnimatorControllerParameterType.Trigger && parameters[i].nameHash == AttackHash)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
