using UnityEngine;

/// <summary>Small code-native UI and pickup sprites; no external art dependencies.</summary>
public static class ProgressionVisuals
{
    private static Sprite coin, pistol, projectile;
    public static Sprite Coin => coin != null ? coin : coin = MakeCoin();
    public static Sprite Pistol => pistol != null ? pistol : pistol = MakePistol();
    public static Sprite Projectile => projectile != null ? projectile : projectile = MakeProjectile();

    public static Texture2D CoinTexture()
    {
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        var pixels = new Color[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f));
                Color color = Color.clear;
                if (distance < 14f) color = new Color(0.55f, 0.29f, 0.03f);
                if (distance < 12f) color = new Color(1f, 0.74f, 0.12f);
                if (distance < 10f && x < 15) color = new Color(1f, 0.89f, 0.35f);
                if (distance < 9f && x >= 14 && x <= 17 && y >= 7 && y <= 24)
                    color = new Color(0.66f, 0.36f, 0.03f);
                pixels[y * 32 + x] = color;
            }
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static Sprite MakeCoin() => Sprite.Create(CoinTexture(), new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 64f);
    private static Sprite MakePistol()
    {
        var texture = new Texture2D(40, 24, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        var pixels = new Color[40 * 24];
        for (int y = 0; y < 24; y++)
            for (int x = 0; x < 40; x++)
                if ((x >= 5 && x <= 35 && y >= 14 && y <= 20) ||
                    (x >= 8 && x <= 15 && y >= 3 && y <= 16))
                    pixels[y * 40 + x] = y >= 18 ? new Color(0.94f, 0.9f, 0.79f) : new Color(0.53f, 0.55f, 0.57f);
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 40, 24), Vector2.one * 0.5f, 64f);
    }
    private static Sprite MakeProjectile()
    {
        var texture = new Texture2D(12, 3, TextureFormat.RGBA32, false);
        var pixels = new Color[36];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 0.88f, 0.4f);
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 12, 3), Vector2.one * 0.5f, 64f);
    }
}
