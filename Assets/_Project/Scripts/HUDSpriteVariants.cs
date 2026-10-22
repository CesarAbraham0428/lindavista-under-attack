using System;
using UnityEngine;

/// <summary>Trims transparent margins and makes a truly gray copy for empty HUD icons.</summary>
public sealed class HUDSpriteVariants : IDisposable
{
    public Sprite ColorSprite { get; private set; }
    public Sprite GraySprite { get; private set; }

    // HUD icons are drawn under ~90 px tall even at 4K. César's ammo art is 1774 px tall:
    // full-size RGBA32 copies cost ~28 MB for three cartridge icons.
    private const int MaxSide = 256;

    public HUDSpriteVariants(Sprite source)
    {
        if (source == null) return;
        var rect = source.textureRect;
        float reduction = Mathf.Min(1f, MaxSide / Mathf.Max(rect.width, rect.height));
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width * reduction));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height * reduction));
        // Mipmaps filter the small on-screen size instead of sampling a large texture sparsely.
        var colorTexture = new Texture2D(width, height, TextureFormat.RGBA32, true);
        var previous = RenderTexture.active;
        RenderTexture target = null;
        try
        {
            // Recortar en GPU evita reservar y leer todo el atlas para un icono pequeño.
            // Halving steps average 2x2 texels, so the reduction stays smooth.
            int stepWidth = (int)rect.width, stepHeight = (int)rect.height;
            Texture input = source.texture;
            var escala = new Vector2(rect.width / source.texture.width, rect.height / source.texture.height);
            var desplazamiento = new Vector2(rect.x / source.texture.width, rect.y / source.texture.height);
            do
            {
                stepWidth = Mathf.Max(width, stepWidth / 2);
                stepHeight = Mathf.Max(height, stepHeight / 2);
                var step = RenderTexture.GetTemporary(stepWidth, stepHeight, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                step.filterMode = FilterMode.Bilinear;
                Graphics.Blit(input, step, escala, desplazamiento);
                if (target != null) RenderTexture.ReleaseTemporary(target);
                target = step;
                input = step;
                escala = Vector2.one;
                desplazamiento = Vector2.zero;
            } while (stepWidth != width || stepHeight != height);
            RenderTexture.active = target;
            colorTexture.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
        }
        catch
        {
            UnityEngine.Object.Destroy(colorTexture);
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            if (target != null) RenderTexture.ReleaseTemporary(target);
        }
        var pixels = colorTexture.GetPixels32();
        colorTexture.Apply(true, true);
        int left = colorTexture.width, bottom = colorTexture.height, right = 0, top = 0;
        for (int y = 0; y < colorTexture.height; y++)
            for (int x = 0; x < colorTexture.width; x++)
            {
                int index = y * colorTexture.width + x;
                var pixel = pixels[index];
                if (pixel.a > 24)
                {
                    left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1);
                    bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1);
                }
                byte gray = (byte)((pixel.r * 299 + pixel.g * 587 + pixel.b * 114) / 1000);
                pixels[index] = new Color32(gray, gray, gray, pixel.a);
            }
        var grayTexture = new Texture2D(colorTexture.width, colorTexture.height, TextureFormat.RGBA32, true);
        grayTexture.SetPixels32(pixels);
        grayTexture.Apply(true, true);
        colorTexture.filterMode = grayTexture.filterMode = FilterMode.Bilinear;
        colorTexture.wrapMode = grayTexture.wrapMode = TextureWrapMode.Clamp;
        var bounds = right > left && top > bottom ? new Rect(left, bottom, right - left, top - bottom) :
            new Rect(0, 0, colorTexture.width, colorTexture.height);
        ColorSprite = Sprite.Create(colorTexture, bounds, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
        GraySprite = Sprite.Create(grayTexture, bounds, Vector2.one * 0.5f, 100f, 0, SpriteMeshType.FullRect);
    }

    public void Dispose()
    {
        if (ColorSprite != null)
        {
            UnityEngine.Object.Destroy(ColorSprite.texture);
            UnityEngine.Object.Destroy(ColorSprite);
            ColorSprite = null;
        }
        if (GraySprite != null)
        {
            UnityEngine.Object.Destroy(GraySprite.texture);
            UnityEngine.Object.Destroy(GraySprite);
            GraySprite = null;
        }
    }
}
