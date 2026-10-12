using UnityEngine;
using System.Collections.Generic;
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
        private float flashUntil;
        private bool listed;

        private static readonly List<HitFlash> flashing = new List<HitFlash>(64);

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

            listed = false;
            Restore();
        }

        public static void Tick()
        {
            float now = Time.time;
            for (int i = flashing.Count - 1; i >= 0; i--)
            {
                HitFlash flash = flashing[i];
                if (flash == null || !flash.listed || now >= flash.flashUntil)
                {
                    if (flash != null && flash.listed)
                    {
                        flash.listed = false;
                        flash.Paint(0f);
                    }

                    flashing.RemoveAt(i);
                }
            }
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

            flashUntil = Time.time + Mathf.Max(0.12f, duration);
            if (!listed)
            {
                listed = true;
                flashing.Add(this);
                Paint(1f);
            }
        }

        private void OnDamaged(Health source, DamageInfo info)
        {
            Flash();
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
            listed = false;
            Paint(0f);
        }
    }
}
