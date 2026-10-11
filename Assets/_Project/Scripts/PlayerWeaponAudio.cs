using UnityEngine;

/// <summary>Each accepted shot plays one clip. Reloads have a separate, cancellable source.</summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponAudio : MonoBehaviour
{
    private WeaponAudioLibrary library;
    private AudioSource shotSource;
    private AudioSource reloadSource;

    private void Awake()
    {
        library = Resources.Load<WeaponAudioLibrary>("WeaponAudioLibrary");
        shotSource = CreateSource("Weapon Fire Audio", 64);
        reloadSource = CreateSource("Weapon Reload Audio", 100);
        if (library == null)
            Debug.LogWarning("WeaponAudioLibrary is missing from Resources.", this);
    }

    private AudioSource CreateSource(string sourceName, int priority)
    {
        var child = new GameObject(sourceName);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = priority;
        return source;
    }

    public void PlayShot(int weapon)
    {
        if (!isActiveAndEnabled || library == null) return;
        AudioClip clip = library.Shot(weapon);
        if (clip != null)
            shotSource.PlayOneShot(clip, library.ShotVolume(weapon));
    }

    public void PlayReload(int weapon, float duration)
    {
        StopReload();
        if (!isActiveAndEnabled || library == null) return;
        AudioClip clip = library.Reload(weapon);
        if (clip == null) return;
        reloadSource.clip = clip;
        reloadSource.volume = Mathf.Clamp01(library.reloadVolume);
        reloadSource.pitch = Mathf.Clamp(clip.length / Mathf.Max(0.01f, duration), 0.1f, 3f);
        reloadSource.Play();
    }

    public void StopReload()
    {
        if (reloadSource != null) reloadSource.Stop();
    }

    public void StopAll()
    {
        StopReload();
        if (shotSource != null) shotSource.Stop();
    }

    private void OnDisable() { StopAll(); }
}
