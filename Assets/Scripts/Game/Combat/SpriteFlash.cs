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

        const string ShaderName = "KingsRescue/SpriteFlash";
        const string ResourceName = "SpriteFlash";

        static Material material;
        static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        public static void Prepare(SpriteRenderer renderer)
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
