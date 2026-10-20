using UnityEngine;

namespace ProjectLike.Phase1
{
    /// <summary>Procedural, softly feathered HDR sprite used as a small light halo under gameplay effects.</summary>
    public static class SoftGlowSprite
    {
        static Sprite sprite;
        static Material material;

        public static Sprite Sprite
        {
            get
            {
                if (sprite) return sprite;
                const int size = 64;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
                {
                    name = "SoftGlowRadial",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };
                var pixels = new Color32[size * size];
                for (var y = 0; y < size; y++)
                for (var x = 0; x < size; x++)
                {
                    var p = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                    var edge = Mathf.Clamp01(1f - p.magnitude);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Pow(edge, 2.4f) * .82f);
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                sprite = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(.5f, .5f), size);
                sprite.name = "SoftGlowRadialSprite";
                return sprite;
            }
        }

        public static Material Material
        {
            get
            {
                if (material) return material;
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (shader) material = new Material(shader) { name = "SoftGlowUnlitMaterial" };
                return material;
            }
        }

        public static Color Tint(Color color, float alphaScale = .52f, float intensity = 2.6f)
        {
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity,
                Mathf.Clamp01(color.a * alphaScale));
        }
    }
}
