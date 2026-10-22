using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Bakes the trimmed effect rectangles that ShotVisual and SniperShotVisual used to compute from
/// CPU pixels at runtime. The textures then ship without Read/Write and can be GPU-compressed.
/// Rectangles, pivots and pixels-per-unit reproduce the former runtime scan exactly.
/// </summary>
public static class EffectSpriteSetup
{
    internal const string ShotAtlasPath = "Assets/_Project/Art/UI/Ammo/Resources/ShotProjectileAtlas.png";
    internal const string TracerPath = "Assets/_Project/Art/Effects/Resources/SniperTracer.png";
    internal const string BloodPath = "Assets/_Project/Art/Effects/Resources/SniperBloodImpact.png";
    private const float PixelsPerUnit = 256f;

    private sealed class Plan
    {
        public string Path;
        public SpriteMeshType MeshType;
        public readonly List<(string name, Rect rect, Vector2 pivot)> Sprites = new();
    }

    [MenuItem("Tools/Lindavista/Bake effect sprite rectangles")]
    private static void ConfigureMenu() => Debug.Log(Configure());

    public static string Configure()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Bake effect sprites outside Play Mode.");
        var baked = new System.Text.StringBuilder();
        foreach (string path in new[] { ShotAtlasPath, TracerPath, BloodPath })
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!texture.isReadable)
            {
                // Already baked: the CPU pixels the runtime scan used are no longer available.
                if (importer.spriteImportMode != SpriteImportMode.Multiple)
                    throw new InvalidOperationException("Cannot bake without the readable import: " + path);
                continue;
            }
            // Scan the imported pixels, exactly like the former runtime code. The PNG itself can be
            // larger than the import (ShotProjectileAtlas is 1254 px, imported at its 1024 px maximum).
            var plan = BuildPlan(path, texture.GetPixels32(), texture.width, texture.height);
            foreach (var sprite in plan.Sprites) baked.Append($" {sprite.name}={sprite.rect}");

            // Sprite slices and PPU are authored in source-image pixels and scaled down with the import.
            importer.GetSourceTextureWidthAndHeight(out int sourceWidth, out int sourceHeight);
            var toSource = new Vector2(sourceWidth / (float)texture.width, sourceHeight / (float)texture.height);

            // Keep every sampling setting; only the sprite layout and CPU copy change.
            var previous = new TextureImporterSettings();
            importer.ReadTextureSettings(previous);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.spriteMode = (int)SpriteImportMode.Multiple;
            // The runtime sprites used 256 PPU on the imported pixels.
            settings.spritePixelsPerUnit = PixelsPerUnit * toSource.x;
            settings.spriteMeshType = plan.MeshType;
            settings.spriteExtrude = 0;
            settings.readable = false;
            settings.alphaIsTransparency = previous.alphaIsTransparency;
            settings.wrapMode = previous.wrapMode;
            settings.wrapModeU = previous.wrapModeU;
            settings.wrapModeV = previous.wrapModeV;
            settings.filterMode = previous.filterMode;
            settings.mipmapEnabled = previous.mipmapEnabled;
            settings.sRGBTexture = previous.sRGBTexture;
            settings.npotScale = previous.npotScale;
            importer.SetTextureSettings(settings);

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var existing = provider.GetSpriteRects();
            var rects = new List<SpriteRect>();
            foreach (var (name, rect, pivot) in plan.Sprites)
            {
                // Reuse IDs on re-runs so references to these sub-assets stay valid.
                var id = Array.Find(existing, r => r.name == name)?.spriteID ?? GUID.Generate();
                var sourceRect = new Rect(Vector2.Scale(rect.position, toSource), Vector2.Scale(rect.size, toSource));
                rects.Add(new SpriteRect { name = name, rect = sourceRect, alignment = SpriteAlignment.Custom, pivot = pivot, spriteID = id });
            }
            provider.SetSpriteRects(rects.ToArray());
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            var pairs = new List<SpriteNameFileIdPair>();
            foreach (var rect in rects) pairs.Add(new SpriteNameFileIdPair(rect.name, rect.spriteID));
            names?.SetNameFileIdPairs(pairs);
            provider.Apply();
            importer.SaveAndReimport();

            // The re-imported slices must reproduce the runtime scan exactly.
            var sprites = LoadSprites(path);
            foreach (var (name, rect, pivot) in plan.Sprites)
            {
                Vector2 expectedPivot = Vector2.Scale(pivot, rect.size);
                if (!sprites.TryGetValue(name, out var sprite) || !Close(sprite.rect, rect) ||
                    (sprite.pivot - expectedPivot).sqrMagnitude > 1e-3f)
                    throw new InvalidOperationException($"Baked sprite {name} differs from the runtime scan: {(sprite != null ? sprite.rect.ToString() : "missing")} vs {rect}");
            }
        }
        return Verify() + (baked.Length > 0 ? " Baked:" + baked : "");
    }

    /// <summary>Structure checks; exact rectangles are compared by Configure while the scan pixels exist.</summary>
    public static string Verify()
    {
        int count = 0;
        var expected = new (string path, string name, Vector2 pivot)[]
        {
            (ShotAtlasPath, ShotVisual.SpriteNames[0], new Vector2(0f, .5f)), (ShotAtlasPath, ShotVisual.SpriteNames[1], new Vector2(0f, .5f)),
            (ShotAtlasPath, ShotVisual.SpriteNames[2], new Vector2(0f, .5f)), (ShotAtlasPath, ShotVisual.SpriteNames[3], new Vector2(0f, .5f)),
            (TracerPath, SniperShotVisual.TracerName, new Vector2(1f, .5f)), (BloodPath, SniperShotVisual.BloodName, new Vector2(.5f, .5f))
        };
        foreach (var (path, name, pivot) in expected)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.isReadable) throw new InvalidOperationException("Effect texture is still Read/Write: " + path);
            if (importer.spriteImportMode != SpriteImportMode.Multiple) throw new InvalidOperationException("Expected baked sprite slices: " + path);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (!LoadSprites(path).TryGetValue(name, out var sprite)) throw new InvalidOperationException("Missing baked sprite " + name);
            Rect bounds = new Rect(0, 0, texture.width, texture.height);
            if (sprite.rect.width < 2 || sprite.rect.height < 2 || !bounds.Contains(sprite.rect.min) || !bounds.Contains(sprite.rect.max - Vector2.one) ||
                (sprite.pivot - Vector2.Scale(pivot, sprite.rect.size)).sqrMagnitude > 1e-3f ||
                Mathf.Abs(sprite.pixelsPerUnit - PixelsPerUnit) > 1e-2f)
                throw new InvalidOperationException($"Baked sprite {name} has an unexpected rect {sprite.rect}, pivot {sprite.pivot} or PPU {sprite.pixelsPerUnit}");
            count++;
        }
        return $"EFFECT_SPRITES_OK: {count} baked sprites (pivots and 256 PPU of the former runtime scan); three effect textures ship without Read/Write.";
    }

    // Source-pixel slices scaled back to the import can differ by float rounding only.
    private static bool Close(Rect a, Rect b) => Mathf.Abs(a.x - b.x) < .01f && Mathf.Abs(a.y - b.y) < .01f &&
        Mathf.Abs(a.width - b.width) < .01f && Mathf.Abs(a.height - b.height) < .01f;

    private static Dictionary<string, Sprite> LoadSprites(string path)
    {
        var sprites = new Dictionary<string, Sprite>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            if (asset is Sprite sprite) sprites[sprite.name] = sprite;
        return sprites;
    }

    private static Plan BuildPlan(string path, Color32[] pixels, int width, int height)
    {
        var rects = Scan(pixels, width, height, path);
        if (path == ShotAtlasPath)
        {
            var shot = new Plan { Path = path, MeshType = SpriteMeshType.Tight };
            for (int i = 0; i < rects.Count; i++)
                if (rects[i].width > 0) shot.Sprites.Add((ShotVisual.SpriteNames[i], rects[i], new Vector2(0f, .5f)));
            return shot;
        }
        var plan = new Plan { Path = path, MeshType = SpriteMeshType.FullRect };
        plan.Sprites.Add(path == TracerPath
            ? (SniperShotVisual.TracerName, rects[0], new Vector2(1f, .5f))
            : (SniperShotVisual.BloodName, rects[0], new Vector2(.5f, .5f)));
        return plan;
    }

    /// <summary>The exact rules of the former runtime code (alpha thresholds and atlas regions).</summary>
    private static List<Rect> Scan(Color32[] pixels, int width, int height, string path)
    {
        var result = new List<Rect>();
        if (path == ShotAtlasPath)
        {
            for (int index = 0; index < ShotVisual.SpriteNames.Length; index++)
            {
                Rect region = index == 0 ? new Rect(0, .5f, .5f, .5f) : index == 1 ? new Rect(.5f, .5f, .5f, .5f) :
                    index == 2 ? new Rect(0, 0, .57f, .5f) : new Rect(.58f, 0, .42f, .5f);
                int left = (int)(region.xMin * width), right = (int)(region.xMax * width);
                int bottom = (int)(region.yMin * height), top = (int)(region.yMax * height);
                result.Add(Trim(pixels, width, left, bottom, right, top, 24, false));
            }
            return result;
        }
        result.Add(Trim(pixels, width, 0, 0, width, height, 8, true));
        return result;
    }

    private static Rect Trim(Color32[] pixels, int width, int left, int bottom, int right, int top, byte threshold, bool allowSinglePixel)
    {
        int minX = right, minY = top, maxX = left - 1, maxY = bottom - 1;
        for (int y = bottom; y < top; y++)
            for (int x = left; x < right; x++)
                if (pixels[y * width + x].a > threshold)
                {
                    minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                    minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
                }
        bool empty = allowSinglePixel ? maxX < minX || maxY < minY : maxX <= minX || maxY <= minY;
        return empty ? new Rect(0, 0, 0, 0) : new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }
}
