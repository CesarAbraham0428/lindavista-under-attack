using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Bakes compact pickup badges from each character's existing ammo artwork.</summary>
public static class LootPickupVisualSetup
{
    private const string Folder = "Assets/_Project/Resources/Loot";
    private const int Size = 128;

    [MenuItem("Tools/Lindavista/Build loot pickup sprites")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
            throw new InvalidOperationException("Stop Play Mode before building pickup artwork.");

        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        for (int character = 0; character < 2; character++)
        {
            bool cesar = character == 1;
            WeaponHUDAssets artwork = CharacterWeapons.Artwork(cesar);
            if (artwork == null)
                throw new InvalidOperationException("Missing weapon HUD artwork for " + (cesar ? "Cesar" : "Marco"));

            for (int slot = 0; slot < 3; slot++)
            {
                Sprite source = artwork.AmmoAt(slot);
                if (source == null) throw new InvalidOperationException("Missing ammo icon for slot " + slot);
                Texture2D texture = MakeAmmo(source);
                try { Save(texture, (cesar ? "CesarAmmo" : "MarcoAmmo") + slot); }
                finally { UnityEngine.Object.DestroyImmediate(texture); }
            }
        }

        Texture2D health = MakeHealth();
        try { Save(health, "Health"); }
        finally { UnityEngine.Object.DestroyImmediate(health); }
        AssetDatabase.SaveAssets();
        Debug.Log("Loot pickup artwork built: six existing ammo icons in circular badges and one capsule pill badge.");
    }

    private static Texture2D MakeAmmo(Sprite source)
    {
        Color[] pixels = Badge(false);
        Texture2D readable = ReadSprite(source);
        try
        {
            Color[] original = readable.GetPixels();
            int left = readable.width, bottom = readable.height, right = -1, top = -1;
            for (int y = 0; y < readable.height; y++)
                for (int x = 0; x < readable.width; x++)
                    if (original[y * readable.width + x].a > 0.08f)
                    {
                        left = Mathf.Min(left, x); right = Mathf.Max(right, x);
                        bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y);
                    }
            if (right < left) throw new InvalidOperationException("Ammo artwork is fully transparent: " + source.name);

            int width = right - left + 1, height = top - bottom + 1;
            float scale = Mathf.Min(78f / width, 82f / height,
                94f / Mathf.Sqrt(width * (float)width + height * (float)height));
            int drawWidth = Mathf.Max(1, Mathf.RoundToInt(width * scale));
            int drawHeight = Mathf.Max(1, Mathf.RoundToInt(height * scale));
            int startX = (Size - drawWidth) / 2, startY = (Size - drawHeight) / 2;
            var icon = new Color[Size * Size];
            for (int y = 0; y < drawHeight; y++)
                for (int x = 0; x < drawWidth; x++)
                {
                    float sourceX = left + (x + 0.5f) * width / drawWidth;
                    float sourceY = bottom + (y + 0.5f) * height / drawHeight;
                    icon[(startY + y) * Size + startX + x] = readable.GetPixelBilinear(sourceX / readable.width, sourceY / readable.height);
                }

            // A narrow light rim keeps the original dark magazines readable on the badge.
            for (int y = 2; y < Size - 2; y++)
                for (int x = 2; x < Size - 2; x++)
                {
                    float alpha = 0;
                    for (int oy = -2; oy <= 2; oy++)
                        for (int ox = -2; ox <= 2; ox++)
                            if (ox * ox + oy * oy <= 4)
                                alpha = Mathf.Max(alpha, icon[(y + oy) * Size + x + ox].a);
                    if (alpha > 0.05f)
                        pixels[y * Size + x] = Over(pixels[y * Size + x], new Color(0.94f, 0.89f, 0.67f, alpha * 0.92f));
                }
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Over(pixels[i], icon[i]);
            return Texture(pixels);
        }
        finally { UnityEngine.Object.DestroyImmediate(readable); }
    }

    private static Color[] Badge(bool healing)
    {
        var pixels = new Color[Size * Size];
        Color edge = healing ? new Color(0.035f, 0.14f, 0.1f) : new Color(0.12f, 0.1f, 0.055f);
        Color ring = healing ? new Color(0.39f, 0.92f, 0.59f) : new Color(1f, 0.77f, 0.28f);
        Color center = healing ? new Color(0.1f, 0.39f, 0.25f) : new Color(0.17f, 0.2f, 0.22f);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f));
                float alpha = Mathf.Clamp01(61.5f - distance);
                Color color = edge;
                if (distance < 57f) color = ring;
                if (distance < 53f)
                {
                    float light = Mathf.Clamp01((y - 25) / 105f);
                    color = Color.Lerp(center, center * 1.45f, light);
                }
                color.a = alpha;
                pixels[y * Size + x] = color;
            }
        return pixels;
    }

    private static Texture2D MakeHealth()
    {
        Color[] pixels = Badge(true);
        Vector2 along = new Vector2(0.7071068f, 0.7071068f);
        Vector2 across = new Vector2(-along.y, along.x);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 position = new Vector2(x - 63.5f, y - 63.5f);
                float u = Vector2.Dot(position, along);
                float v = Vector2.Dot(position, across);
                float distance = Mathf.Sqrt(Mathf.Pow(Mathf.Max(Mathf.Abs(u) - 21f, 0), 2) + v * v);
                if (distance > 17f) continue;
                Color color = new Color(0.12f, 0.13f, 0.15f);
                if (distance < 13.5f)
                {
                    color = u < 0 ? new Color(0.91f, 0.17f, 0.23f) : new Color(0.97f, 0.97f, 0.9f);
                    if (Mathf.Abs(u) < 1.25f) color = new Color(0.36f, 0.36f, 0.34f);
                    else if (v > 4 && v < 8 && Mathf.Abs(u) < 25)
                        color = Color.Lerp(color, Color.white, 0.48f);
                    else if (v < -8) color *= 0.8f;
                    color.a = 1;
                }
                color.a *= Mathf.Clamp01(17f - distance);
                pixels[y * Size + x] = Over(pixels[y * Size + x], color);
            }
        return Texture(pixels);
    }

    private static Texture2D ReadSprite(Sprite sprite)
    {
        Texture source = sprite.texture;
        RenderTexture temporary = RenderTexture.GetTemporary(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        RenderTexture previous = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        Texture2D copy = null;
        try
        {
            // Preserve the source's sRGB colors in this project's linear color space.
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            Rect rect = sprite.rect;
            copy = new Texture2D(Mathf.RoundToInt(rect.width), Mathf.RoundToInt(rect.height), TextureFormat.RGBA32, false);
            copy.ReadPixels(rect, 0, 0);
            copy.Apply();
            return copy;
        }
        catch
        {
            if (copy != null) UnityEngine.Object.DestroyImmediate(copy);
            throw;
        }
        finally
        {
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);
        }
    }

    private static Color Over(Color underneath, Color over)
    {
        float alpha = over.a + underneath.a * (1 - over.a);
        if (alpha <= 0) return Color.clear;
        Color result = (over * over.a + underneath * underneath.a * (1 - over.a)) / alpha;
        result.a = alpha;
        return result;
    }

    private static Texture2D Texture(Color[] pixels)
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static void Save(Texture2D texture, string name)
    {
        string path = Folder + "/" + name + ".png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = Size / LootPickupVisuals.WorldSize;
        importer.spritePivot = new Vector2(0.5f, 0.5f);
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }
}
