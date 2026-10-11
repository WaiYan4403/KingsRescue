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
        private int defaultSortingLayer;
        private int defaultSortingOrder;

        private void Awake()
        {
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }

            autoDespawn = GetComponent<AutoDespawn>();
            if (spriteRenderer != null)
            {
                defaultSortingLayer = spriteRenderer.sortingLayerID;
                defaultSortingOrder = spriteRenderer.sortingOrder;
            }
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

            RestoreSorting();
            transform.localRotation = Quaternion.identity;
            float native = spriteRenderer.sprite.bounds.size.x;
            float diameter = Mathf.Max(0.01f, radius) * 2f;
            float scale = native > 0.0001f ? diameter / native : diameter;
            transform.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>
        /// Stretches the marker into a forward lance. <paramref name="length"/> and
        /// <paramref name="thickness"/> are full world sizes, with length along local up.
        /// </summary>
        public void SetCapsule(float length, float thickness, float angleDegrees)
        {
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                return;
            }

            spriteRenderer.sortingLayerID = SortingLayer.NameToID("Player");
            spriteRenderer.sortingOrder = 40;
            float native = spriteRenderer.sprite.bounds.size.x;
            float unit = native > 0.0001f ? native : 1f;
            transform.localScale = new Vector3(
                Mathf.Max(0.05f, thickness) / unit,
                Mathf.Max(0.05f, length) / unit,
                1f);
            transform.localRotation = Quaternion.Euler(0f, 0f, angleDegrees);
        }

        private void RestoreSorting()
        {
            if (spriteRenderer == null)
            {
                return;
            }

            spriteRenderer.sortingLayerID = defaultSortingLayer;
            spriteRenderer.sortingOrder = defaultSortingOrder;
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
