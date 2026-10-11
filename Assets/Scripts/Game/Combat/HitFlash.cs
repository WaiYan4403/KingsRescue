using System.Collections;
using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Paints every sprite on this body white for a moment when it is hit.
    /// </summary>
    public class HitFlash : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer target;
        [SerializeField] private Color flashColor = Color.white;
        [SerializeField] private float duration = 0.12f;

        private Health health;
        private SpriteRenderer[] renderers;
        private Coroutine routine;

        private void Awake()
        {
            if (target == null)
            {
                target = GetComponentInChildren<SpriteRenderer>();
            }

            renderers = GetComponentsInChildren<SpriteRenderer>(true);
            health = GetComponent<Health>();
            if (health == null)
            {
                health = GetComponentInParent<Health>();
            }

            Prepare();
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

        public void SetBaseColor(Color color)
        {
            if (target != null)
            {
                target.color = color;
            }
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
            if (!isActiveAndEnabled || renderers == null || renderers.Length == 0)
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
            Paint(1f);
            yield return new WaitForSeconds(Mathf.Max(0.12f, duration));
            Paint(0f);
            routine = null;
        }

        private void Prepare()
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteFlash.Prepare(renderers[i]);
            }
        }

        private void Paint(float amount)
        {
            if (renderers == null)
            {
                return;
            }

            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (amount <= 0f)
                {
                    SpriteFlash.Clear(renderer);
                }
                else
                {
                    SpriteFlash.Apply(renderer, flashColor, amount);
                }
            }
        }

        private void Restore()
        {
            if (routine != null)
            {
                StopCoroutine(routine);
                routine = null;
            }

            Paint(0f);
        }
    }
}
