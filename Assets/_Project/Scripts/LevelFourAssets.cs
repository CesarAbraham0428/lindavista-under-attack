using UnityEngine;

/// <summary>Build-safe references to the existing boss art and the configured truck.</summary>
[CreateAssetMenu(menuName = "Lindavista/Level 4 assets")]
public sealed class LevelFourAssets : ScriptableObject
{
    [SerializeField] private Sprite portrait;
    [SerializeField] private GameObject truckPrefab;
    public Sprite Portrait => portrait;
    public GameObject TruckPrefab => truckPrefab;
}
