using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Isolated manual/automated mobile review across scenes, without spending real inventory.</summary>
public static class AndroidValidationSession
{
    private static IDisposable scope;
    private static string directory;
    private static bool previousBackground;
    private static bool hadCharacter, hadLevel;
    private static int previousCharacter, previousLevel;
    private static bool hadSaving, reloadLocked;
    private static int previousSaving;
    private static int previousSize = -1;
    private static EditorWindow gameView;
    private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    public static string Begin()
    {
        if (!Application.isPlaying || scope != null) throw new InvalidOperationException("Start Play Mode, then begin once.");
        directory = Path.Combine(Path.GetTempPath(), "LindavistaAndroidReview", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var repository = new ProgressionRepository(Path.Combine(directory, "profile.json"), true);
        repository.TryCredit(10000);
        repository.TryCompleteLevel(0);
        repository.TryCompleteLevel(1);
        repository.TryCompleteLevel(2);
        scope = ProgressionService.UseVerificationRepository(repository);
        hadCharacter = PlayerPrefs.HasKey("Lindavista.SelectedCharacter");
        hadLevel = PlayerPrefs.HasKey("Lindavista.SelectedLevel");
        previousCharacter = PlayerPrefs.GetInt("Lindavista.SelectedCharacter");
        previousLevel = PlayerPrefs.GetInt("Lindavista.SelectedLevel");
        hadSaving = PlayerPrefs.HasKey("Lindavista.Android.Ahorro");
        previousSaving = PlayerPrefs.GetInt("Lindavista.Android.Ahorro");
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        // Avoid reconnecting a running verification match to the real repository on script reload.
        EditorApplication.LockReloadAssemblies();
        reloadLocked = true;
        EditorApplication.playModeStateChanged += OnPlayState;
        return "ANDROID_REVIEW_STARTED: temporary profile, four levels unlocked; real profile preserved.";
    }

    public static void PlayLevel(int level, bool cesar)
    {
        if (scope == null) throw new InvalidOperationException("Begin isolated review first.");
        PlayerPrefs.SetInt("Lindavista.SelectedLevel", Mathf.Clamp(level, 0, 3));
        PlayerPrefs.SetInt("Lindavista.SelectedCharacter", cesar ? 1 : 0);
        SceneManager.LoadScene("Gameplay");
    }

    public static void EmptyScene()
    {
        if (!Application.isPlaying || scope == null)
            throw new InvalidOperationException("Begin an isolated Play Mode review before clearing its runtime scene.");
        var previous = SceneManager.GetActiveScene();
        var empty = SceneManager.CreateScene("Android Runtime Checks");
        SceneManager.SetActiveScene(empty);
        new GameObject("Verification Camera", typeof(Camera));
        var light = new GameObject("Verification Light", typeof(Light)).GetComponent<Light>();
        light.type = LightType.Directional;
        foreach (var root in previous.GetRootGameObjects()) UnityEngine.Object.DestroyImmediate(root);
        SceneManager.UnloadSceneAsync(previous);
    }

    public static string Resolution(int width, int height)
    {
        var assembly = typeof(Editor).Assembly;
        var sizesType = assembly.GetType("UnityEditor.GameViewSizes");
        var singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes = singleton.GetProperty("instance", BindingFlags.Public | BindingFlags.Static).GetValue(null);
        var group = sizesType.GetProperty("currentGroup", Flags).GetValue(sizes);
        var groupType = group.GetType();
        var gameViewType = assembly.GetType("UnityEditor.GameView");
        gameView = EditorWindow.GetWindow(gameViewType);
        var selection = gameViewType.GetProperty("selectedSizeIndex", Flags);
        if (previousSize < 0) previousSize = (int)selection.GetValue(gameView);
        string label = "Android review " + width + "x" + height;
        var texts = (string[])groupType.GetMethod("GetDisplayTexts", Flags).Invoke(group, null);
        int index = Array.FindIndex(texts, t => t.StartsWith(label, StringComparison.Ordinal));
        if (index < 0)
        {
            var sizeType = assembly.GetType("UnityEditor.GameViewSize");
            var kindType = assembly.GetType("UnityEditor.GameViewSizeType");
            var size = Activator.CreateInstance(sizeType, new object[] { Enum.Parse(kindType, "FixedResolution"), width, height, label });
            groupType.GetMethod("AddCustomSize", Flags).Invoke(group, new[] { size });
            index = (int)groupType.GetMethod("GetTotalCount", Flags).Invoke(group, null) - 1;
        }
        selection.SetValue(gameView, index);
        gameView.Repaint();
        return "Game View requested " + width + "x" + height;
    }

    public static void End()
    {
        // Restore the real repository only after gameplay has stopped.
        if (Application.isPlaying) { EditorApplication.isPlaying = false; return; }
        Cleanup();
    }

    private static void Cleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayState;
        scope?.Dispose();
        scope = null;
        if (directory != null)
        {
            if (hadCharacter) PlayerPrefs.SetInt("Lindavista.SelectedCharacter", previousCharacter); else PlayerPrefs.DeleteKey("Lindavista.SelectedCharacter");
            if (hadLevel) PlayerPrefs.SetInt("Lindavista.SelectedLevel", previousLevel); else PlayerPrefs.DeleteKey("Lindavista.SelectedLevel");
            if (hadSaving) PlayerPrefs.SetInt("Lindavista.Android.Ahorro", previousSaving); else PlayerPrefs.DeleteKey("Lindavista.Android.Ahorro");
            PlayerPrefs.Save();
            Application.runInBackground = previousBackground;
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
            directory = null;
        }
        if (previousSize >= 0 && gameView != null)
        {
            gameView.GetType().GetProperty("selectedSizeIndex", Flags).SetValue(gameView, previousSize);
            gameView.Repaint();
        }
        previousSize = -1;
        if (reloadLocked) { reloadLocked = false; EditorApplication.UnlockReloadAssemblies(); }
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
    }
}
