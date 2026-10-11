using UnityEngine;

/// <summary>Prepared, single-shot clips in pistol, assault rifle and RPG order.</summary>
[CreateAssetMenu(menuName = "Lindavista/Weapon audio library")]
public sealed class WeaponAudioLibrary : ScriptableObject
{
    public AudioClip[] shots = new AudioClip[3];
    public AudioClip[] reloads = new AudioClip[3];
    public float[] shotVolumes = { 0.8f, 0.6f, 0.9f };
    [Range(0f, 1f)] public float reloadVolume = 0.7f;

    public AudioClip Shot(int weapon) => ClipAt(shots, weapon);
    public AudioClip Reload(int weapon) => ClipAt(reloads, weapon);
    public float ShotVolume(int weapon) => shotVolumes != null && weapon >= 0 && weapon < shotVolumes.Length
        ? Mathf.Clamp01(shotVolumes[weapon]) : 0.8f;

    private static AudioClip ClipAt(AudioClip[] clips, int weapon) =>
        clips != null && weapon >= 0 && weapon < clips.Length ? clips[weapon] : null;
}
