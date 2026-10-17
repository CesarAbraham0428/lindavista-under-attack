using UnityEngine;

/// <summary>Shieldless animations use the original controller's states and combat timing.</summary>
public sealed class EnemyShieldVisualLibrary : ScriptableObject
{
    public AnimatorOverrideController controller;
    public Sprite brokenShield;
}
