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
        /// <summary>World radius of the smallest music-field sprite, which the note prefab is tuned to.</summary>
        public const float BaselineRadius = 82f / 16f * 0.5f;

        private const float SpriteRadius = BaselineRadius;

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

        /// <summary>Half the sprite's world width, after the size animation has applied.</summary>
        public float Radius
        {
            get
            {
                SpriteRenderer renderer = ResolveRenderer();
                if (renderer != null && renderer.sprite != null)
                {
                    float pixels = Mathf.Max(renderer.sprite.rect.width, renderer.sprite.rect.height);
                    float units = pixels / Mathf.Max(1f, renderer.sprite.pixelsPerUnit);
                    float scale = Mathf.Max(Mathf.Abs(renderer.transform.lossyScale.x), Mathf.Abs(renderer.transform.lossyScale.y));
                    return units * 0.5f * scale;
                }

                return BaselineRadius;
            }
        }

        private SpriteRenderer ResolveRenderer()
        {
            if (visual != null)
            {
                SpriteRenderer onVisual = visual.GetComponent<SpriteRenderer>();
                if (onVisual != null)
                {
                    return onVisual;
                }
            }

            return GetComponentInChildren<SpriteRenderer>();
        }

        public void SetRadius(float radius)
        {
            float scale = Mathf.Max(0.05f, radius) / SpriteRadius;
            Transform target = visual != null ? visual : transform;
            //target.localScale = new Vector3(scale, scale, 1f);
        }
    }
}
