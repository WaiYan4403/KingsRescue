using UnityEngine;
using KingsRescue.Core;

namespace KingsRescue.World
{
    /// <summary>
    /// Infinite ground by scrolling a tiled material's UVs on a single static quad.
    /// Zero allocations and no tile recycling, which matters for WebGL.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class ScrollingBackground : MonoBehaviour
    {
        [Tooltip("1 = matches the world scroll exactly. Use smaller values for distant parallax layers.")]
        [SerializeField, Range(0f, 2f)] private float parallaxFactor = 1f;

        [Tooltip("How many texture tiles fit into one world unit. Must match the material tiling for the ground to line up.")]
        [SerializeField] private float tilesPerWorldUnit = 0.5f;

        [SerializeField] private string textureProperty = "_BaseMap";

        private Material instancedMaterial;
        private int propertyId;
        private float offset;

        private void Awake()
        {
            instancedMaterial = GetComponent<Renderer>().material;
            propertyId = Shader.PropertyToID(textureProperty);

            if (!instancedMaterial.HasProperty(propertyId))
            {
                propertyId = Shader.PropertyToID("_MainTex");
            }
        }

        private void Update()
        {
            float speed = ScrollDirector.Speed * parallaxFactor;
            if (speed == 0f)
            {
                return;
            }

            // Scrolling UVs up makes the texture appear to travel down the screen.
            offset = Mathf.Repeat(offset + speed * tilesPerWorldUnit * Time.deltaTime, 1f);
            instancedMaterial.SetTextureOffset(propertyId, new Vector2(0f, offset));
        }

        private void OnDestroy()
        {
            if (instancedMaterial != null)
            {
                Destroy(instancedMaterial);
            }
        }
    }
}
