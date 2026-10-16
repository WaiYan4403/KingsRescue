using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// The visible ring of the music weapon. It stays parented to the hero.
    /// Damage and the on/off cycle live on the weapon.
    /// </summary>
    public class MusicField : MonoBehaviour, IPoolable
    {
        private const float SpriteRadius = 82f / 16f * 0.5f;

        [SerializeField] private Transform visual;
        private Animator animator;

        private void Awake()
        {
            if (visual == null)
            {
                Transform sprite = transform.Find("Sprite");
                if (sprite != null)
                {
                    visual = sprite;
                }
            }

            animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                animator.keepAnimatorStateOnDisable = true;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
        }

        public void OnDespawned()
        {
        }

        public void SetRadius(float radius)
        {
            float scale = Mathf.Max(0.05f, radius) / SpriteRadius;
            Transform target = visual != null ? visual : transform;
            //target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
