public static class ShopBalance
{
    public static readonly string[] Names = { "PISTOLA", "METRALLETA", "LANZACOHETES" };
    public static readonly int[] Capacity = { 60, 180, 12 };
    public static readonly int[] Pack = { 12, 30, 2 };
    public static readonly int[] AmmoCost = { 10, 20, 35 };
    public const int MaxHealthUpgrade = 5;
    public static int HealthCost(int level) => level < MaxHealthUpgrade ? 30 * (level + 1) : 0;
}
