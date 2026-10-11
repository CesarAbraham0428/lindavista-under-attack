using UnityEngine;

[CreateAssetMenu(menuName = "Lindavista/Menu music library")]
public sealed class MenuMusicLibrary : ScriptableObject
{
    public AudioClip[] tracks;
    public string[] titles;
    public string[] artists;
    public int Count => tracks == null ? 0 : tracks.Length;
}
