public static class ShopBalance
{
    public static readonly string[] Names = { "PISTOLA", "METRALLETA", "LANZACOHETES" };
    public static readonly int[] Capacity = { 60, 180, 12 };
    public static int CapacityFor(int index, bool cesar) => cesar && index == 2 ? 20 : Capacity[index];
    public static readonly int[] Pack = { 12, 30, 2 };
    public static readonly int[] AmmoCost = { 10, 20, 35 };
    public const int MaxHealthUpgrade = 5;
    public const int MaxHealthConsumables = 10;
    public const int HealthConsumableCost = 15;
    public static int HealthCost(int level) => level < MaxHealthUpgrade ? 30 * (level + 1) : 0;
    public static int SpeedCost(int level, bool cesar) =>
        level >= 0 && level < CharacterStats.MaximumSpeedBars(cesar) - 1 ? 40 * (level + 1) : 0;
}
