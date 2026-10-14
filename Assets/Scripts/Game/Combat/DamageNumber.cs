using UnityEngine;
using TMPro;
using KingsRescue.Core;

namespace KingsRescue.Combat
{
    /// <summary>
    /// One floating damage number. Every number, including small hits and crits, keeps a black outline.
    /// </summary>
    public class DamageNumber : MonoBehaviour, IPoolable
    {
        [SerializeField] private TextMeshPro label;
        [SerializeField] private float lifetime = 0.7f;
        [SerializeField] private float rise = 0.9f;
        [SerializeField] private float baseScale = 1f;
        [SerializeField] private float critScale = 1.45f;
        [SerializeField] private float outlineWidth = 0.3f;

        private float age;
        private Vector3 start;
        private Color color;
        private bool playing;

        private void Awake()
        {
            if (label == null)
            {
                label = GetComponent<TextMeshPro>();
            }

            if (label == null)
            {
                return;
            }

            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Overflow;
            label.extraPadding = true;
            label.sortingLayerID = SortingLayer.NameToID("Effects");
            label.sortingOrder = 80;
            ApplyOutline();
        }

        public void OnSpawned()
        {
            playing = false;
            age = 0f;
        }

        public void OnDespawned()
        {
            playing = false;
        }

        public void Show(float amount, bool critical)
        {
            if (label == null)
            {
                return;
            }

            ApplyOutline();
            int shown = Mathf.Max(1, Mathf.RoundToInt(amount));
            label.text = shown.ToString();
            color = ColorFor(shown);
            label.color = color;
            float scale = baseScale * (critical ? critScale : 1f);
            transform.localScale = new Vector3(scale, scale, 1f);
            start = transform.position;
            age = 0f;
            playing = true;
            label.ForceMeshUpdate();
        }

        private void Update()
        {
            if (!playing || label == null)
            {
                return;
            }

            age += Time.deltaTime;
            float duration = lifetime > 0.05f ? lifetime : 0.7f;
            float t = Mathf.Clamp01(age / duration);
            Vector3 position = start;
            position.y += rise * t;
            transform.position = position;

            if (t > 0.55f)
            {
                color.a = 1f - Mathf.InverseLerp(0.55f, 1f, t);
                label.color = color;
            }

            if (t < 1f)
            {
                return;
            }

            playing = false;
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool != null)
            {
                pool.Return(gameObject);
            }
        }

        private void ApplyOutline()
        {
            Material shared = label.fontSharedMaterial;
            if (shared == null || shared.IsKeywordEnabled("OUTLINE_ON"))
            {
                return;
            }

            float width = Mathf.Max(0.2f, outlineWidth);
            shared.EnableKeyword("OUTLINE_ON");
            if (shared.HasProperty("_OutlineWidth"))
            {
                shared.SetFloat("_OutlineWidth", width);
            }

            if (shared.HasProperty("_OutlineColor"))
            {
                shared.SetColor("_OutlineColor", Color.black);
            }
        }

        private static Color ColorFor(int amount)
        {
            if (amount >= 45)
            {
                return new Color(0.95f, 0.2f, 0.16f);
            }

            if (amount >= 28)
            {
                return new Color(1f, 0.52f, 0.1f);
            }

            if (amount >= 15)
            {
                return new Color(1f, 0.88f, 0.2f);
            }

            return Color.white;
        }
    }
}
