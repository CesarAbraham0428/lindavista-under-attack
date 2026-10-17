using System.Collections;
using UnityEngine;

/// <summary>Each accepted shot plays one clip. Reloads have a separate, cancellable source.</summary>
[DisallowMultipleComponent]
public sealed class PlayerWeaponAudio : MonoBehaviour
{
    private WeaponAudioLibrary library;
    private AudioSource shotSource;
    private AudioSource reloadSource;
    private Coroutine pendingReloadSound;

    private void Awake()
    {
        library = Resources.Load<WeaponAudioLibrary>(CharacterWeapons.IsCesar(gameObject)
            ? "CesarWeaponAudioLibrary" : "WeaponAudioLibrary");
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
        if (library.NaturalReload(weapon))
        {
            // Short mechanical foley must not turn into a low rumble when stretched
            // across a long reload. Line it up with the final rack/ready movement.
            reloadSource.pitch = 1f;
            float delay = Mathf.Max(0f, duration - clip.length - 0.05f);
            if (delay > 0f) pendingReloadSound = StartCoroutine(DelayedReload(delay));
            else reloadSource.Play();
            return;
        }
        reloadSource.pitch = Mathf.Clamp(clip.length / Mathf.Max(0.01f, duration), 0.1f, 3f);
        reloadSource.Play();
    }

    private IEnumerator DelayedReload(float delay)
    {
        // Use the same scaled game clock as PlayerWeaponController.Reload.
        yield return new WaitForSeconds(delay);
        pendingReloadSound = null;
        reloadSource.Play();
    }

    public void StopReload()
    {
        if (pendingReloadSound != null)
        {
            StopCoroutine(pendingReloadSound);
            pendingReloadSound = null;
        }
        if (reloadSource != null) reloadSource.Stop();
    }

    public void StopAll()
    {
        StopReload();
        if (shotSource != null) shotSource.Stop();
    }

    private void OnDisable() { StopAll(); }
}
