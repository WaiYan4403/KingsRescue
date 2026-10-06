using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// Scales a circle sprite so its world radius matches a combat hit radius.
    /// </summary>
    public class HitboxMarker : MonoBehaviour, IPoolable
    {
        [SerializeField] private SpriteRenderer spriteRenderer;

        private AutoDespawn autoDespawn;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            autoDespawn = GetComponent<AutoDespawn>();
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            SetPersistent(false);
        }

        public void SetRadius(float radius)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            float native = spriteRenderer.sprite.bounds.size.x;
            float diameter = Mathf.Max(0.01f, radius) * 2f;
            float scale = native > 0.0001f ? diameter / native : diameter;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        public void SetPersistent(bool persistent)
        {
            if (autoDespawn == null)
            {
                autoDespawn = GetComponent<AutoDespawn>();
            }

            if (autoDespawn != null)
            {
                autoDespawn.enabled = !persistent;
            }
        }
    }
}
