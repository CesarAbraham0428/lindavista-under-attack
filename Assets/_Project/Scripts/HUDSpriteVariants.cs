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
        var target = RenderTexture.GetTemporary(source.texture.width, source.texture.height,
            0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var previous = RenderTexture.active;
        try
        {
            Graphics.Blit(source.texture, target);
            RenderTexture.active = target;
            colorTexture.ReadPixels(rect, 0, 0);
            colorTexture.Apply();
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
        }
        var pixels = colorTexture.GetPixels32();
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
        grayTexture.Apply();
        colorTexture.filterMode = grayTexture.filterMode = FilterMode.Bilinear;
        colorTexture.wrapMode = grayTexture.wrapMode = TextureWrapMode.Clamp;
        var bounds = right > left && top > bottom ? new Rect(left, bottom, right - left, top - bottom) :
            new Rect(0, 0, colorTexture.width, colorTexture.height);
        ColorSprite = Sprite.Create(colorTexture, bounds, Vector2.one * 0.5f, 100f);
        GraySprite = Sprite.Create(grayTexture, bounds, Vector2.one * 0.5f, 100f);
    }

    public void Dispose()
    {
        if (ColorSprite != null)
        {
            UnityEngine.Object.Destroy(ColorSprite.texture);
            UnityEngine.Object.Destroy(ColorSprite);
        }
        if (GraySprite != null)
        {
            UnityEngine.Object.Destroy(GraySprite.texture);
            UnityEngine.Object.Destroy(GraySprite);
        }
    }
}
