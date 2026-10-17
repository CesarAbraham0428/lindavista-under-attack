using System;
using UnityEngine;

/// <summary>Trims transparent margins and makes a truly gray copy for empty HUD icons.</summary>
public sealed class HUDSpriteVariants : IDisposable
{
    public Sprite ColorSprite { get; private set; }
    public Sprite GraySprite { get; private set; }

    public HUDSpriteVariants(Sprite source)
    {
        if (source == null) return;
        var rect = source.textureRect;
        var colorTexture = new Texture2D((int)rect.width, (int)rect.height, TextureFormat.RGBA32, false);
        // Recortar en GPU evita reservar y leer todo el atlas para un icono pequeño.
        var target = RenderTexture.GetTemporary(colorTexture.width, colorTexture.height,
            0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        try
        {
            var escala = new Vector2(rect.width / source.texture.width, rect.height / source.texture.height);
            var desplazamiento = new Vector2(rect.x / source.texture.width, rect.y / source.texture.height);
            Graphics.Blit(source.texture, target, escala, desplazamiento);
            RenderTexture.active = target;
            colorTexture.ReadPixels(new Rect(0, 0, colorTexture.width, colorTexture.height), 0, 0, false);
        }
        catch
        {
            UnityEngine.Object.Destroy(colorTexture);
            throw;
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
        var pixels = colorTexture.GetPixels32();
        colorTexture.Apply(false, true);
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
        var grayTexture = new Texture2D(colorTexture.width, colorTexture.height, TextureFormat.RGBA32, false);
        grayTexture.SetPixels32(pixels);
        grayTexture.Apply(false, true);
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
