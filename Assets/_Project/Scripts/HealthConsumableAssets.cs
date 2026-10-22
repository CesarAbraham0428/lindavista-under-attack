using UnityEngine;

/// <summary>Existing pill artwork shared by the shop and the in-game use button.</summary>
public sealed class HealthConsumableAssets : ScriptableObject
{
    public Sprite icon;
    public static HealthConsumableAssets Load() => Resources.Load<HealthConsumableAssets>("HealthConsumableAssets");
}
