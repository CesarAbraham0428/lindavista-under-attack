using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>Rebuilds Cesar's eight-frame sheets and clips, retaining existing sprite IDs and controller GUID.</summary>
public static class CesarWeaponSetup
{
    private const string Art = "Assets/_Project/Art/Characters/Players/";
    private const string Animations = "Assets/_Project/Animations/Players/Cesar/";
    private const string ResourcesPath = "Assets/_Project/Resources/";
    private static readonly string[] Weapons = { "Escopeta", "Francotirador", "Lanzagranadas" };
    private static readonly string[] OldWeapons = { "Pistol", "SMG", "RPG" };

    [MenuItem("Tools/Lindavista/Set up Cesar weapons")]
    public static void Generate()
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animations/Player_Cesar.controller");
        if (controller == null) throw new InvalidOperationException("Cesar's controller is missing.");
        if (!controller.parameters.Any(p => p.name == "WeaponIndex"))
            controller.AddParameter("WeaponIndex", AnimatorControllerParameterType.Float);
        var machine = controller.layers[0].stateMachine;
        var poses = new AnimationClip[3];
        for (int i = 0; i < Weapons.Length; i++)
        {
            string weapon = Weapons[i];
            Normalize(Art + "Cesar_" + weapon + "_Fire.png");
            Sprite[] fire = Frames(Art + "Cesar_" + weapon + "_Fire.png");
            string reloadPath = Art + "Cesar_" + weapon + "_Reload.png";
            Sprite[] reload;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(reloadPath) != null)
            {
                Normalize(reloadPath);
                reload = Frames(reloadPath);
            }
            else
            {
                throw new InvalidOperationException("Missing reload sprite sheet: " + reloadPath);
            }
            var definition = Definition(i);
            poses[i] = Clip("Cesar_" + weapon + "_Idle", new[] { fire[0] }, 0.2f, true);
            SetAction(machine, i, "Fire", Clip("Cesar_" + weapon + "_Fire", fire, definition.FireInterval, false));
            SetAction(machine, i, "Reload", Clip("Cesar_" + weapon + "_Reload", reload, definition.ReloadDuration, false));
        }
        var tree = AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(controller))
            .OfType<BlendTree>().FirstOrDefault(t => t.name == "Cesar Equipped Pose");
        if (tree == null)
        {
            tree = new BlendTree { name = "Cesar Equipped Pose" };
            AssetDatabase.AddObjectToAsset(tree, controller);
        }
        tree.blendType = BlendTreeType.Simple1D;
        tree.blendParameter = "WeaponIndex";
        tree.useAutomaticThresholds = false;
        tree.children = poses.Select((p, i) => new ChildMotion { motion = p, threshold = i, timeScale = 1f }).ToArray();
        foreach (var state in machine.states.Select(s => s.state))
        {
            if (state.name == "Cesar_Idle" || state.name == "Cesar_Aim") { state.motion = tree; state.speed = 1f; }
            if (state.name == "Cesar_Damage")
                foreach (var transition in state.transitions) { transition.exitTime = 1f; transition.duration = 0f; }
        }
        BuildArtwork();
        BuildAudio();
        EditorUtility.SetDirty(tree);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        Debug.Log("Cesar weapons ready: shotgun, sniper and grenade launcher with fire/reload animations and audio.");
    }

    private static Sprite[] Frames(string path)
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
            .OrderBy(s => s.rect.center.y > texture.height / 2f ? 0 : 1).ThenBy(s => s.rect.x).ToArray();
    }

    private static void Normalize(string path)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        if (importer == null) throw new InvalidOperationException("Missing sprite sheet: " + path);
        var pixels = new Texture2D(2, 2);
        try
        {
            pixels.LoadImage(System.IO.File.ReadAllBytes(path));
            if (pixels.width % 4 != 0 || pixels.height % 2 != 0)
                throw new InvalidOperationException("Expected a four-column, two-row sheet: " + path);
            int width = pixels.width / 4, height = pixels.height / 2;
            float ppu = OpaqueBounds(pixels, new Rect(0, height, width, height)).height / 5f;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = ppu;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            // Re-slice every sheet, including incomplete or incorrectly sized manual slices.
            // Keep matching IDs so other asset references survive subsequent rebuilds.
            var factories = new SpriteDataProviderFactories();
            factories.Init();
            var provider = factories.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var previous = provider.GetSpriteRects();
            var slices = new SpriteRect[8];
            string prefix = System.IO.Path.GetFileNameWithoutExtension(path);
            for (int i = 0; i < slices.Length; i++)
            {
                string name = prefix + "_" + i;
                Rect rect = new Rect(i % 4 * width, (1 - i / 4) * height, width, height);
                Rect bounds = OpaqueBounds(pixels, rect);
                var old = previous.FirstOrDefault(s => s.name == name);
                slices[i] = new SpriteRect
                {
                    name = name, rect = rect,
                    spriteID = old != null ? old.spriteID : GUID.Generate(),
                    alignment = SpriteAlignment.Custom,
                    // Five-unit standing body, feet at -2.5, matching walk/colliders.
                    pivot = new Vector2(FeetCenter(pixels, rect, bounds) / width,
                        (bounds.yMin + 2.5f * ppu) / height)
                };
            }
            provider.SetSpriteRects(slices);
            provider.GetDataProvider<ISpriteNameFileIdDataProvider>().SetNameFileIdPairs(
                slices.Select(s => new SpriteNameFileIdPair(s.name, s.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }
        finally { UnityEngine.Object.DestroyImmediate(pixels); }
    }

    private static Rect OpaqueBounds(Texture2D texture, Rect rect)
    {
        int bottom = (int)rect.height, top = 0;
        Color[] pixels = texture.GetPixels((int)rect.x, (int)rect.y, (int)rect.width, (int)rect.height);
        for (int y = 0; y < (int)rect.height; y++)
            for (int x = 0; x < (int)rect.width; x++)
                if (pixels[y * (int)rect.width + x].a > 0.1f) { bottom = Mathf.Min(bottom, y); top = Mathf.Max(top, y + 1); }
        if (top <= bottom) throw new InvalidOperationException("Empty animation cell: " + rect);
        return new Rect(0, bottom, rect.width, top - bottom);
    }

    private static float FeetCenter(Texture2D texture, Rect rect, Rect bounds)
    {
        int width = (int)rect.width;
        int height = Mathf.Max(1, (int)(bounds.height * 0.12f));
        var pixels = texture.GetPixels((int)rect.x, (int)(rect.y + bounds.yMin), width, height);
        int left = width, right = 0;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                if (pixels[y * width + x].a > 0.1f) { left = Mathf.Min(left, x); right = Mathf.Max(right, x + 1); }
        return (left + right) * 0.5f;
    }

    private static WeaponDefinition Definition(int index)
    {
        string path = ResourcesPath + "Cesar" + Weapons[index] + "Definition.asset";
        var definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);
        if (definition != null) return definition;
        definition = UnityEngine.Object.Instantiate(WeaponDefinition.At(index));
        definition.name = "Cesar" + Weapons[index] + "Definition";
        definition.hideFlags = HideFlags.None;
        var data = new SerializedObject(definition);
        data.FindProperty("baseDamage").intValue = new[] { 4, 8, 10 }[index];
        data.FindProperty("fireInterval").floatValue = new[] { 0.85f, 1.1f, 1.2f }[index];
        data.FindProperty("magazineSize").intValue = new[] { 6, 5, 2 }[index];
        data.FindProperty("reloadDuration").floatValue = new[] { 2f, 2.2f, 2f }[index];
        data.FindProperty("projectileSpeed").floatValue = new[] { 22f, 40f, 14f }[index];
        data.FindProperty("range").floatValue = new[] { 18f, 26f, 18f }[index];
        data.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(definition, path);
        return definition;
    }

    private static AnimationClip Clip(string name, Sprite[] frames, float duration, bool loop)
    {
        if (frames.Length == 0) throw new InvalidOperationException("Missing sprites for " + name);
        string path = Animations + name + ".anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null) { clip = new AnimationClip { name = name }; AssetDatabase.CreateAsset(clip, path); }
        clip.frameRate = 30f;
        var keys = new ObjectReferenceKeyframe[frames.Length + 1];
        for (int i = 0; i < frames.Length; i++)
            keys[i] = new ObjectReferenceKeyframe { time = duration * i / frames.Length, value = frames[i] };
        keys[frames.Length] = new ObjectReferenceKeyframe { time = duration, value = frames[frames.Length - 1] };
        AnimationUtility.SetObjectReferenceCurve(clip,
            new EditorCurveBinding { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" }, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    private static void SetAction(AnimatorStateMachine machine, int index, string action, AnimationClip clip)
    {
        string name = "Cesar_" + Weapons[index] + "_" + action;
        var state = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == name || s.name == "Cesar_" + OldWeapons[index] + "_" + action);
        if (state == null) throw new InvalidOperationException("Missing state: " + name);
        state.name = name;
        state.motion = clip;
        state.speed = 1f;
        state.speedParameterActive = action == "Reload";
        state.speedParameter = action == "Reload" ? "ReloadSpeed" : "";
        foreach (var transition in state.transitions)
        {
            transition.hasExitTime = true;
            transition.exitTime = 1f;
            transition.duration = 0f;
        }
        EditorUtility.SetDirty(state);
    }

    private static Sprite LargestSprite(string path) => AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>()
        .OrderByDescending(s => s.rect.width * s.rect.height).First();

    private static void BuildArtwork()
    {
        string path = ResourcesPath + "CesarWeaponHUDAssets.asset";
        var artwork = AssetDatabase.LoadAssetAtPath<WeaponHUDAssets>(path);
        if (artwork == null)
        {
            artwork = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<WeaponHUDAssets>(ResourcesPath + "WeaponHUDAssets.asset"));
            artwork.name = "CesarWeaponHUDAssets";
            AssetDatabase.CreateAsset(artwork, path);
        }
        var data = new SerializedObject(artwork);
        string[] slots = { "pistol", "smg", "rpg" };
        for (int i = 0; i < 3; i++)
        {
            data.FindProperty(slots[i]).objectReferenceValue = LargestSprite("Assets/_Project/Art/Weapons/" + Weapons[i] + ".png");
            data.FindProperty(slots[i] + "Ammo").objectReferenceValue = LargestSprite("Assets/_Project/Art/UI/Ammo/Municion_" + Weapons[i].ToLowerInvariant() + ".png");
        }
        data.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(artwork);
    }

    private static void BuildAudio()
    {
        string path = ResourcesPath + "CesarWeaponAudioLibrary.asset";
        var audio = AssetDatabase.LoadAssetAtPath<WeaponAudioLibrary>(path);
        if (audio == null) { audio = ScriptableObject.CreateInstance<WeaponAudioLibrary>(); AssetDatabase.CreateAsset(audio, path); }
        for (int i = 0; i < 3; i++)
        {
            audio.shots[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Weapons/Prepared/Cesar_" + Weapons[i] + "_Shot.wav");
            audio.reloads[i] = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/_Project/Audio/SFX/Weapons/Prepared/Cesar_" + Weapons[i] + "_Reload.wav");
            if (audio.shots[i] == null || audio.reloads[i] == null) throw new InvalidOperationException("Missing prepared audio for " + Weapons[i]);
        }
        EditorUtility.SetDirty(audio);
    }
}
