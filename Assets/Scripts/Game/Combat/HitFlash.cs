using System.Collections;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Tints a sprite white for a moment when it is hit. Cheap, readable feedback
    /// that matters a lot when dozens of enemies overlap.
    /// </summary>
    public class HitFlash : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float duration = 0.07f;

        private Health health;
        private Color originalColor;
        private Coroutine routine;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponentInChildren<SpriteRenderer>();
            }

            if (target != null)
            {
                originalColor = target.color;
            }

            health = GetComponentInParent<Health>();
        }

        private void OnEnable()
        {
            if (health != null)
            {
                health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            if (health != null)
            {
                health.Damaged -= OnDamaged;
            }

            Restore();
        }

        public void OnSpawned()
        {
            Restore();
        }

        public void OnDespawned()
        {
            Restore();
        }

        public void Flash()
        {
            if (target == null || !isActiveAndEnabled)
            {
                return;
            }

            if (routine != null)
            {
                StopCoroutine(routine);
            }

            routine = StartCoroutine(FlashRoutine());
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            Flash();
        }

        private IEnumerator FlashRoutine()
        {
            target.color = flashColor;
            yield return new WaitForSeconds(duration);
            target.color = originalColor;
            routine = null;
        }

        private void Restore()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            if (target != null)
            {
                target.color = originalColor;
            }
        }
    }
}
