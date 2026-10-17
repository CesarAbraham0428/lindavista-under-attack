using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Imports shieldless art at the shielded actor's exact standing height and foot anchor.</summary>
public static class EnemyShieldVisualSetup
{
    private const string Art = "Assets/_Project/Art/Characters/Enemies/";
    private const string Folder = "Assets/_Project/Animations/Enemies/Enemy3/";
    private static readonly EditorCurveBinding SpriteBinding = new()
        { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };

    [MenuItem("Tools/Lindavista/Build enemy 3 shield loss animations")]
    public static void Build()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before importing art.");
        Sprite original = (Sprite)AnimationUtility.GetObjectReferenceCurve(
            AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Enemy3_Idle.anim"), SpriteBinding)[0].value;
        var oldImage = Load(AssetDatabase.GetAssetPath(original));
        try
        {
            Rect oldBounds = Opaque(oldImage, original.rect);
            float standingHeight = oldBounds.height / original.pixelsPerUnit;
            float foot = (oldBounds.yMin - original.pivot.y) / original.pixelsPerUnit;
            float footX = (FeetCenter(oldImage, original.rect, oldBounds) - original.pivot.x) / original.pixelsPerUnit;
            ImportSheet("Enemy3_Unshielded_8f", standingHeight, foot, footX, 4);
            ImportSheet("Enemy3_Unshielded_Walk_8f", standingHeight, foot, footX, 8);
            ImportSheet("Enemy3_Unshielded_Defeat_8f", standingHeight, foot, footX, 1, true);
        }
        finally { Object.DestroyImmediate(oldImage); }

        var shieldImporter = (TextureImporter)AssetImporter.GetAtPath(Art + "Enemy3_BrokenShield.png");
        shieldImporter.textureType = TextureImporterType.Sprite;
        shieldImporter.spriteImportMode = SpriteImportMode.Single;
        shieldImporter.spritePixelsPerUnit = 200f;
        shieldImporter.alphaIsTransparency = true;
        shieldImporter.mipmapEnabled = false;
        shieldImporter.textureCompression = TextureImporterCompression.Uncompressed;
        shieldImporter.SaveAndReimport();

        Sprite[] frames = Frames("Enemy3_Unshielded_8f");
        Sprite[] walk = Frames("Enemy3_Unshielded_Walk_8f");
        Sprite[] defeat = Frames("Enemy3_Unshielded_Defeat_8f");
        var replacements = new Dictionary<string, AnimationClip>
        {
            ["Enemy3_Idle"] = Clip("Idle", new[] { frames[0] }, 1f, true),
            ["Enemy3_Walk"] = Clip("Walk", walk, 1f, true),
            ["Enemy3_HeavyPunch"] = Clip("HeavyPunch", new[] { frames[4], frames[5], frames[6], frames[7], frames[0] }, 1f, false),
            ["Enemy3_Damage"] = Clip("Damage", new[] { frames[4], frames[0] }, .4f, false),
            ["Enemy3_Defeat"] = Clip("Defeat", defeat, 1.1f, false)
        };

        // Keep the controller's existing clip IDs while retiring the obsolete shielded death sheet.
        // Both intact and exposed deaths use the current fall; shielded damage stays on shield poses.
        var baseDefeat = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Enemy3_Defeat.anim");
        EditorUtility.CopySerialized(replacements["Enemy3_Defeat"], baseDefeat);
        baseDefeat.name = "Enemy3_Defeat";
        EditorUtility.SetDirty(baseDefeat);
        var shieldFrames = Frames("Enemy3_Shield_HeavyPunch_8f");
        var baseDamage = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Enemy3_Damage.anim");
        baseDamage.ClearCurves();
        baseDamage.frameRate = 5f;
        AnimationUtility.SetObjectReferenceCurve(baseDamage, SpriteBinding, new[]
        {
            new ObjectReferenceKeyframe { time = 0f, value = shieldFrames[1] },
            new ObjectReferenceKeyframe { time = .2f, value = shieldFrames[0] }
        });
        var damageSettings = AnimationUtility.GetAnimationClipSettings(baseDamage);
        damageSettings.startTime = 0f; damageSettings.stopTime = .4f; damageSettings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(baseDamage, damageSettings);
        EditorUtility.SetDirty(baseDamage);

        const string controllerPath = Folder + "Enemy3_Unshielded.overrideController";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
        if (controller == null)
        {
            controller = new AnimatorOverrideController();
            AssetDatabase.CreateAsset(controller, controllerPath);
        }
        controller.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Project/Animations/Enemy3.controller");
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        controller.GetOverrides(overrides);
        for (int i = 0; i < overrides.Count; i++)
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(overrides[i].Key, replacements[overrides[i].Key.name]);
        controller.ApplyOverrides(overrides);

        const string libraryPath = "Assets/_Project/Resources/Enemy3UnshieldedVisuals.asset";
        var library = AssetDatabase.LoadAssetAtPath<EnemyShieldVisualLibrary>(libraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<EnemyShieldVisualLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
        }
        library.controller = controller;
        library.brokenShield = AssetDatabase.LoadAssetAtPath<Sprite>(Art + "Enemy3_BrokenShield.png");
        EditorUtility.SetDirty(library); EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("ENEMY3_SHIELD_ANIMATIONS_READY: eight-step walk cycle, eight-pose grounded fall, aligned feet and height, independent shield fragments.");
    }

    private static Sprite[] Frames(string prefix) => AssetDatabase.LoadAllAssetsAtPath(Art + prefix + ".png")
        .OfType<Sprite>().OrderBy(s => int.Parse(s.name.Substring(s.name.LastIndexOf('_') + 1))).ToArray();

    private static void ImportSheet(string prefix, float standingHeight, float foot, float footX,
        int standingFrames, bool falling = false)
    {
        string path = Art + prefix + ".png";
        var image = Load(path);
        try
        {
            int w = image.width / 4, h = image.height / 2;
            if (image.width % 4 != 0 || image.height % 2 != 0)
                throw new InvalidOperationException("Expected a 4 by 2 sprite sheet: " + path);
            Rect Cell(int i)
            {
                if (prefix != "Enemy3_Unshielded_Walk_8f")
                    return new Rect(i % 4 * w, (1 - i / 4) * h, w, h);
                // The last stride extends across the nominal grid: keep every boot whole
                // and prevent that boot from leaking into its neighbouring passing pose.
                int[,] bounds = { {28,34,371,482}, {435,34,741,481}, {851,33,1037,485}, {1157,32,1500,482},
                    {17,530,382,977}, {436,530,751,976}, {849,529,1048,977}, {1144,529,1507,976} };
                return new Rect(bounds[i,0] - 4, image.height - bounds[i,3] - 4,
                    bounds[i,2] - bounds[i,0] + 8, bounds[i,3] - bounds[i,1] + 8);
            }
            float ppu = Enumerable.Range(0, standingFrames).Max(i => Opaque(image, Cell(i)).height) / standingHeight;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 4096;
            var factories = new SpriteDataProviderFactories(); factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects();
            var slices = new SpriteRect[8];
            for (int i = 0; i < slices.Length; i++)
            {
                string name = prefix + "_" + i;
                Rect rect = Cell(i), bounds = Opaque(image, rect);
                var old = previous.FirstOrDefault(s => s.name == name);
                slices[i] = new SpriteRect
                {
                    name = name, rect = rect, spriteID = old != null ? old.spriteID : GUID.Generate(),
                    alignment = SpriteAlignment.Custom,
                    // A fallen body stays centred over its root instead of snapping to its outstretched boots.
                    pivot = new Vector2(((falling ? rect.width * .5f : FeetCenter(image, rect, bounds)) - footX * ppu) / rect.width,
                        (bounds.yMin - foot * ppu) / rect.height)
                };
            }
            provider.SetSpriteRects(slices);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply(); importer.SaveAndReimport();
        }
        finally { Object.DestroyImmediate(image); }
    }

    private static AnimationClip Clip(string state, Sprite[] frames, float duration, bool loop)
    {
        string path = Folder + "Enemy3_Unshielded_" + state + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, path); }
        clip.ClearCurves(); clip.frameRate = frames.Length / duration;
        var keys = new ObjectReferenceKeyframe[frames.Length];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = duration * i / frames.Length, value = frames[i] };
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.startTime = 0f;
        settings.stopTime = duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static Texture2D Load(string path)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!image.LoadImage(File.ReadAllBytes(path))) throw new InvalidOperationException("Cannot read " + path);
        return image;
    }
    private static Rect Opaque(Texture2D image, Rect rect)
    {
        Color32[] pixels = image.GetPixels32();
        int left = (int)rect.width, right = 0, bottom = (int)rect.height, top = 0;
        for (int y = 0; y < rect.height; y++)
        for (int x = 0; x < rect.width; x++)
            if (pixels[((int)rect.y + y) * image.width + (int)rect.x + x].a >= 128)
            {
                left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1);
                bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1);
            }
        if (right <= left || top <= bottom) throw new InvalidOperationException("Empty sprite cell: " + rect);
        return new Rect(left, bottom, right - left, top - bottom);
    }
    private static float FeetCenter(Texture2D image, Rect rect, Rect bounds)
    {
        Color32[] pixels = image.GetPixels32();
        int left = (int)rect.width, right = 0;
        for (int y = (int)bounds.yMin; y < bounds.yMin + bounds.height * .12f; y++)
        for (int x = 0; x < rect.width; x++)
            if (pixels[((int)rect.y + y) * image.width + (int)rect.x + x].a >= 128)
            { left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1); }
        return (left + right) * .5f;
    }
}
