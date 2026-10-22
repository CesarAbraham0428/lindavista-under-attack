using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Updates just the settings panel and audio assets, preserving the rest of the menu.</summary>
public static class MenuAudioSettingsSetup
{
    private const string Root = "Assets/_Project/";
    private const string LibraryPath = Root + "Resources/MenuMusicLibrary.asset";

    [MenuItem("Tools/Lindavista/Configure menu soundtrack and settings")]
    public static void Configure()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play Mode before configuring menus.");
        Scene original = SceneManager.GetActiveScene();
        if (original.isDirty) EditorSceneManager.SaveScene(original);
        string originalPath = original.path;
        EnsureResources();
        Scene scene = original.name == "MainMenu" ? original : EditorSceneManager.OpenScene(Root + "Scenes/MainMenu.unity");
        var controller = Object.FindFirstObjectByType<MainMenuController>();
        if (controller == null) throw new System.InvalidOperationException("MainMenuController is missing.");
        ConfigurePanel(controller);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        if (!string.IsNullOrEmpty(originalPath) && originalPath != scene.path) EditorSceneManager.OpenScene(originalPath);
        Debug.Log("Menu soundtrack and radio settings installed. Music is restricted to MainMenu and CharacterSelection.");
    }

    public static void ConfigurePanel(MainMenuController controller)
    {
        EnsureResources();
        var serialized = new SerializedObject(controller);
        var panel = (GameObject)serialized.FindProperty("settingsPanel").objectReferenceValue;
        if (panel == null) throw new System.InvalidOperationException("Settings Panel is missing.");
        var view = panel.GetComponent<MenuSettingsPanel>() ?? panel.AddComponent<MenuSettingsPanel>();
        var library = AssetDatabase.LoadAssetAtPath<MenuMusicLibrary>(LibraryPath);
        view.BuildLayout(library);
        serialized.FindProperty("masterVolumeSlider").objectReferenceValue = view.MasterVolume;
        serialized.FindProperty("closeSettingsButton").objectReferenceValue = view.BackButton;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(view);
        panel.SetActive(false);
        foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (source.gameObject.scene != controller.gameObject.scene || System.Array.IndexOf(library.tracks, source.clip) < 0) continue;
            // The persistent player replaces manually placed soundtrack sources.
            source.playOnAwake = false;
            source.enabled = false;
            EditorUtility.SetDirty(source);
        }
    }

    private static void EnsureResources()
    {
        Directory.CreateDirectory(Root + "Resources/UI");
        CreateSprite("SettingsFrame", 128, new Vector4(22, 22, 22, 22), false, false);
        CreateSprite("SettingsBoard", 256, new Vector4(32, 32, 32, 32), true, false);
        CreateSprite("SettingsSpeaker", 256, Vector4.zero, false, true);
        var library = AssetDatabase.LoadAssetAtPath<MenuMusicLibrary>(LibraryPath);
        if (library == null)
        {
            library = ScriptableObject.CreateInstance<MenuMusicLibrary>();
            AssetDatabase.CreateAsset(library, LibraryPath);
        }
        library.tracks = new[]
        {
            AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/Instrumental Cartel de Santa Suena mamalona.mp3"),
            AssetDatabase.LoadAssetAtPath<AudioClip>(Root + "Audio/Music/Duro 2 Horas (Instrumental) - Faraon Love Shady.mp3")
        };
        library.titles = new[] { "SUENA MAMALONA", "DURO 2 HORAS" };
        library.artists = new[] { "Cartel de Santa", "Faraón Love Shady" };
        if (library.tracks[0] == null || library.tracks[1] == null) throw new System.InvalidOperationException("Both menu tracks must exist in Audio/Music.");
        foreach (var clip in library.tracks)
        {
            var importer = (AudioImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(clip));
            var settings = importer.defaultSampleSettings;
            if (settings.loadType == AudioClipLoadType.Streaming && settings.compressionFormat == AudioCompressionFormat.Vorbis && !settings.preloadAudioData) continue;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = .45f;
            settings.preloadAudioData = false;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }
        EditorUtility.SetDirty(library);
    }

    private static void CreateSprite(string name, int size, Vector4 border, bool board, bool speaker)
    {
        string path = Root + "Resources/UI/" + name + ".png";
        if (File.Exists(path)) return;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var pixels = new Color[size * size];
        var random = new System.Random(271);
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
        {
            Color color;
            float noise = (float)random.NextDouble() * .035f;
            if (speaker)
            {
                float radius = Vector2.Distance(new Vector2(x, y), Vector2.one * (size - 1) * .5f) / (size * .5f);
                float mesh = ((x + y) % 9 < 2 || (x - y + size * 2) % 9 < 2) ? .025f : 0f;
                float v = .12f + noise - mesh + Mathf.Sin(radius * 105) * .014f;
                color = radius > .98f ? Color.clear : radius > .92f ? new Color(.06f, .07f, .05f) : radius > .87f ? new Color(.48f, .42f, .26f) : radius > .79f ? new Color(.085f, .10f, .085f) : new Color(v, v * 1.12f, v);
                if (radius < .22f) color = new Color(.075f + noise, .085f + noise, .07f + noise);
            }
            else
            {
                float corner = board ? 24f : 18f;
                float dx = Mathf.Max(corner - x, x - (size - 1 - corner));
                float dy = Mathf.Max(corner - y, y - (size - 1 - corner));
                float distance = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                if (distance > corner) color = Color.clear;
                else if (distance > corner - 6 || edge < 6) color = board ? new Color(.055f, .065f, .045f) : new Color(.22f, .26f, .21f);
                else if (distance > corner - 10 || edge < 10) color = board ? new Color(.53f, .48f, .29f) : new Color(.70f, .75f, .61f);
                else color = board ? new Color(.19f + noise, .235f + noise, .18f + noise) : new Color(.92f + noise, .92f + noise, .92f + noise);
                if (board && edge > 10 && y % 5 == 0) color *= .975f;
                if (board)
                {
                    foreach (var bolt in new[] {new Vector2(20,20),new Vector2(size-21,20),new Vector2(20,size-21),new Vector2(size-21,size-21)})
                    {
                        float d = Vector2.Distance(new Vector2(x,y), bolt);
                        if (d < 6) color = d > 4.5f ? new Color(.045f,.055f,.04f) : Mathf.Abs(y-bolt.y)<1 ? new Color(.14f,.16f,.12f) : new Color(.60f,.63f,.47f);
                    }
                }
            }
            pixels[y * size + x] = color;
        }
        texture.SetPixels(pixels); texture.Apply();
        File.WriteAllBytes(path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spriteBorder = border;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 256;
        importer.SaveAndReimport();
    }
}
