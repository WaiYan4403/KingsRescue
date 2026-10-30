using System.Collections.Generic;
using UnityEngine;

namespace KingsRescue.Combat
{
    /// <summary>
    /// White or red hit overlay. SpriteRenderer.color multiplies the texture, so
    /// painting it white does not read as a flash.
    /// </summary>
    public static class SpriteFlash
    {
        public static readonly int AmountId = Shader.PropertyToID("_FlashAmount");
        public static readonly int ColorId = Shader.PropertyToID("_FlashColor");
        public static readonly int EnableOutlineId = Shader.PropertyToID("_EnableOutline");

        const string ShaderName = "KingsRescue/SpriteFlash";
        const string ResourceName = "SpriteFlash";

        static Material material;
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();
        static readonly Dictionary<int, Material> parked = new Dictionary<int, Material>();

        public static bool KeepsOutline(SpriteRenderer renderer)
        {
            Material source = renderer != null ? renderer.sharedMaterial : null;
            return source != null && source.HasProperty(EnableOutlineId);
        }

        /// <summary>
        /// A hit flash may be showing this renderer. The material to return to
        /// after the flash becomes <paramref name="material"/>.
        /// </summary>
        public static void ReplaceParked(SpriteRenderer renderer, Material material)
        {
            if (renderer == null)
            {
                return;
            }

            int id = renderer.GetInstanceID();
            if (parked.ContainsKey(id))
            {
                parked[id] = material;
                return;
            }

            renderer.sharedMaterial = material;
        }

        public static void Prepare(SpriteRenderer renderer)
        {
            if (renderer == null || KeepsOutline(renderer))
            {
                return;
            }

            Material mat = SharedMaterial();
            if (mat == null)
            {
                return;
            }

            if (renderer.sharedMaterial != mat)
            {
                renderer.sharedMaterial = mat;
            }

            Clear(renderer);
        }

        public static void Apply(SpriteRenderer renderer, Color color, float amount)
        {
            if (renderer == null)
            {
                return;
            }

            Material mat = SharedMaterial();
            if (mat == null)
            {
                return;
            }

            if (renderer.sharedMaterial != mat)
            {
                parked[renderer.GetInstanceID()] = renderer.sharedMaterial;
                renderer.sharedMaterial = mat;
            }

            renderer.GetPropertyBlock(Block);
            Block.SetColor(ColorId, color);
            Block.SetFloat(AmountId, amount);
            renderer.SetPropertyBlock(Block);
        }

        public static void Clear(SpriteRenderer renderer)
        {
            if (renderer == null)
            {
                return;
            }

            int id = renderer.GetInstanceID();
            if (parked.TryGetValue(id, out Material saved))
            {
                parked.Remove(id);
                if (saved != null)
                {
                    renderer.sharedMaterial = saved;
                }
            }

            renderer.GetPropertyBlock(Block);
            Block.SetFloat(AmountId, 0f);
            renderer.SetPropertyBlock(Block);
        }

        static Material SharedMaterial()
        {
            if (material != null)
            {
                return material;
            }

            material = Resources.Load<Material>(ResourceName);
            if (material != null)
            {
                return material;
            }

            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                return null;
            }

            material = new Material(shader)
            {
                name = "SpriteFlash (runtime)",
                hideFlags = HideFlags.HideAndDontSave
            };
            material.SetFloat(AmountId, 0f);
            material.SetColor(ColorId, Color.white);
            return material;
        }
    }
}
