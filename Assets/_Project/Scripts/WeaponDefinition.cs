using UnityEngine;

/// <summary>Editable combat and upgrade balance, separate from animation and save data.</summary>
[CreateAssetMenu(menuName = "Lindavista/Weapon definition")]
public sealed class WeaponDefinition : ScriptableObject
{
    [SerializeField, Min(1)] private int baseDamage = 2;
    [SerializeField, Min(0.01f)] private float fireInterval = 0.4f;
    [SerializeField, Min(1f)] private float projectileSpeed = 22f;
    [SerializeField, Min(0.1f)] private float range = 18f;
    [SerializeField, Min(0.01f)] private float projectileRadius = 0.07f;
    [SerializeField, Min(1)] private int magazineSize = 12;
    [SerializeField, Min(0.1f)] private float reloadDuration = 1.2f;
    [SerializeField] private int[] upgradeCosts = { 25, 60 };
    private static WeaponDefinition pistol;

    public static WeaponDefinition Pistol
    {
        get
        {
            if (pistol == null)
            {
                pistol = Resources.Load<WeaponDefinition>("PistolDefinition");
                if (pistol == null)
                {
                    pistol = CreateInstance<WeaponDefinition>();
                    pistol.hideFlags = HideFlags.DontSave;
                }
            }
            return pistol;
        }
    }

    public float FireInterval => fireInterval;
    public float ProjectileSpeed => projectileSpeed;
    public float Range => range;
    public float ProjectileRadius => projectileRadius;
    public int MagazineSize => magazineSize;
    public float ReloadDuration => reloadDuration;
    public int MaxUpgrade => upgradeCosts.Length;
    public int DamageAt(int upgrade) => baseDamage + Mathf.Clamp(upgrade, 0, MaxUpgrade);
    public int UpgradeCost(int currentUpgrade) => currentUpgrade >= 0 && currentUpgrade < MaxUpgrade
        ? Mathf.Max(1, upgradeCosts[currentUpgrade]) : 0;
}
