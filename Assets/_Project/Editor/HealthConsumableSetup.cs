using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Builds healing clips from the user's existing eight slices, retaining sprite IDs and rectangles.</summary>
public static class HealthConsumableSetup
{
    private const string Art = "Assets/_Project/Art/UI/Health/";
    public const float Duration = 1.2f;

    [MenuItem("Tools/Lindavista/Preparar pastillas y animaciones de vida")]
    public static void Generate()
    {
        foreach (string character in new[] { "Marco", "Cesar" }) BuildAnimation(character);
        const string path = "Assets/_Project/Resources/HealthConsumableAssets.asset";
        var assets = AssetDatabase.LoadAssetAtPath<HealthConsumableAssets>(path);
        if (assets == null)
        {
            assets = ScriptableObject.CreateInstance<HealthConsumableAssets>();
            AssetDatabase.CreateAsset(assets, path);
        }
        assets.icon = AssetDatabase.LoadAllAssetsAtPath(Art + "Pastillas_vida.png").OfType<Sprite>()
            .OrderByDescending(s => s.rect.width * s.rect.height).First();
        EditorUtility.SetDirty(assets);
        AssetDatabase.SaveAssets();
        Debug.Log("PASTILLAS_PREPARADAS: icono, dos clips de ocho fotogramas y estados de consumo.");
    }

    private static void BuildAnimation(string character)
    {
        string sheet = Art + character + "_Pastilla.png";
        NormalizeFeet(sheet);
        var frames = AssetDatabase.LoadAllAssetsAtPath(sheet).OfType<Sprite>()
            .OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray();
        if (frames.Length != 8) throw new InvalidOperationException("Se esperaban ocho recortes: " + sheet);
        string name = character + "_UseHealthConsumable";
        string path = "Assets/_Project/Animations/Players/" + character + "/" + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = 20f;
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = Duration * i / frames.Length, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = Duration, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(
            "Assets/_Project/Animations/Player_" + character + ".controller");
        if (controller == null) throw new InvalidOperationException("Falta el controlador: " + character);
        var machine = controller.layers[0].stateMachine;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name);
        if (state == null) state = machine.AddState(name, new Vector3(650f, 420f));
        state.motion = clip;
        state.speed = 1f;
        var idle = machine.states.Select(s => s.state).First(s => s.name == character + "_Idle");
        if (!state.transitions.Any(t => t.destinationState == idle))
        {
            var transition = state.AddTransition(idle);
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }
        EditorUtility.SetDirty(clip);
        EditorUtility.SetDirty(state);
        EditorUtility.SetDirty(controller);
    }

    private static void NormalizeFeet(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        var factories = new SpriteDataProviderFactories();
        factories.Init();
        var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        var slices = provider.GetSpriteRects().OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray();
        if (slices.Length != 8) throw new InvalidOperationException("Faltan recortes en " + path);
        var texture = new Texture2D(2, 2);
        try
        {
            texture.LoadImage(System.IO.File.ReadAllBytes(path));
            Rect first = BodyBounds(texture, slices[0].rect);
            // Match the existing five-unit standing body, with feet at local y = -2.5.
            float ppu = first.height / 5f;
            importer.spritePixelsPerUnit = ppu;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            foreach (var slice in slices)
            {
                Rect bounds = BodyBounds(texture, slice.rect);
                slice.alignment = SpriteAlignment.Custom;
                slice.pivot = new Vector2(.5f, (bounds.yMin + 2.5f * ppu) / slice.rect.height);
            }
            provider.SetSpriteRects(slices);
            provider.Apply();
            importer.SaveAndReimport();
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static Rect BodyBounds(Texture2D texture, Rect rect)
    {
        var pixels = texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        int bottom = (int)rect.height, top = 0;
        for (int y = 0; y < (int)rect.height; y++)
            for (int x = 0; x < (int)rect.width; x++)
                if (pixels[y * (int)rect.width + x].a > .5f)
                { bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1); }
        if (top <= bottom) throw new InvalidOperationException("Recorte vacío: " + rect);
        return new Rect(0f, bottom, rect.width, top - bottom);
    }
}
