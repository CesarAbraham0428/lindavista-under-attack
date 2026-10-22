using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One soundtrack source shared by menu scenes and shop overlays.</summary>
public sealed class MenuMusicPlayer : MonoBehaviour
{
    public const string MasterVolumeKey = "Lindavista.MasterVolume";
    public const string MusicVolumeKey = "Lindavista.MusicVolume";
    public const string SelectedTrackKey = "Lindavista.MenuTrack";
    private static MenuMusicPlayer instance;
    private AudioSource source;
    private MenuMusicLibrary library;
    private int selectedTrack;
    private bool shopOpen;
    public event Action Changed;

    public static MenuMusicPlayer Instance
    {
        get
        {
            if (instance == null && Application.isPlaying)
                new GameObject("Menu Soundtrack").AddComponent<MenuMusicPlayer>();
            return instance;
        }
    }
    public int SelectedTrack => selectedTrack;
    public float MusicVolume => source.volume;
    public MenuMusicLibrary Library => library;
    public static bool IsMenuScene(string name) => name == "MainMenu" || name == "CharacterSelection";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { instance = null; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap() { var player = Instance; }

    private void Awake()
    {
        if (instance != null && instance != this) { Destroy(gameObject); return; }
        instance = this;
        DontDestroyOnLoad(gameObject);
        library = Resources.Load<MenuMusicLibrary>("MenuMusicLibrary");
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, .65f));
        AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
        selectedTrack = library != null && library.Count > 0
            ? Mathf.Clamp(PlayerPrefs.GetInt(SelectedTrackKey, 0), 0, library.Count - 1) : 0;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        ApplyScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { ApplyScene(); }
    private void OnActiveSceneChanged(Scene previous, Scene current) { shopOpen = false; ApplyScene(); }

    public static void NotifyShop(bool open)
    {
        if (instance == null) return;
        instance.shopOpen = open;
        instance.ApplyScene();
    }

    private void ApplyScene()
    {
        if (!IsMenuScene(SceneManager.GetActiveScene().name) && !shopOpen)
        {
            source.Stop();
            return;
        }
        if (library == null || library.Count == 0) return;
        AudioClip clip = library.tracks[selectedTrack];
        if (source.clip != clip) { source.Stop(); source.clip = clip; }
        if (clip != null && !source.isPlaying) source.Play();
    }

    public void SelectTrack(int index)
    {
        if (library == null || index < 0 || index >= library.Count || library.tracks[index] == null) return;
        if (selectedTrack == index) return;
        selectedTrack = index;
        PlayerPrefs.SetInt(SelectedTrackKey, selectedTrack);
        PlayerPrefs.Save();
        ApplyScene();
        Changed?.Invoke();
    }

    public void SetMusicVolume(float volume)
    {
        float volumen = Mathf.Clamp01(volume);
        if (Mathf.Approximately(source.volume, volumen)) return;
        source.volume = volumen;
        PlayerPrefs.SetFloat(MusicVolumeKey, source.volume);
        Changed?.Invoke();
    }

    public static void SetMasterVolume(float volume)
    {
        AudioListener.volume = Mathf.Clamp01(volume);
        PlayerPrefs.SetFloat(MasterVolumeKey, AudioListener.volume);
    }

    private void OnApplicationPause(bool paused) { if (paused) PlayerPrefs.Save(); }
    private void OnApplicationQuit() { PlayerPrefs.Save(); }
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (instance == this) instance = null;
    }
}
