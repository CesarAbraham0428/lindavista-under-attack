/// <summary>Base attributes before each character's independent health upgrades.</summary>
public static class CharacterStats
{
    public static int BaseHealth(bool cesar) => cesar ? 12 : 10;
    public static int MaximumHealth(bool cesar) => BaseHealth(cesar) + ShopBalance.MaxHealthUpgrade;
    public static float SpeedMultiplier(bool cesar) => cesar ? 1.1f : 1f;
}
