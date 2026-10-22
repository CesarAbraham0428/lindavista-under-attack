using UnityEngine;

/// <summary>Character attributes, including independently saved health and speed upgrades.</summary>
public static class CharacterStats
{
    public static int BaseHealth(bool cesar) => cesar ? 12 : 10;
    public static int MaximumHealth(bool cesar) => BaseHealth(cesar) + ShopBalance.MaxHealthUpgrade;
    public static int MaximumSpeedBars(bool cesar) => cesar ? 2 : 3;
    public static float BaseSpeedMultiplier(bool cesar) => cesar ? 1f : 1.1f;
    public static float SpeedMultiplierAt(bool cesar, int upgrade) =>
        BaseSpeedMultiplier(cesar) + .1f * Mathf.Clamp(upgrade, 0, MaximumSpeedBars(cesar) - 1);
    public static float SpeedMultiplier(bool cesar) => SpeedMultiplierAt(cesar, ProgressionService.SpeedUpgradeFor(cesar));
}
