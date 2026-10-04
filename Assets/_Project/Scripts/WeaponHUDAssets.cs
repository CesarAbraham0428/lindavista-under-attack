using UnityEngine;

/// <summary>References HUD artwork without duplicating the source textures.</summary>
[CreateAssetMenu(menuName = "Lindavista/Weapon HUD assets")]
public sealed class WeaponHUDAssets : ScriptableObject
{
    [SerializeField] private Sprite pistol;
    [SerializeField] private Sprite smg;
    [SerializeField] private Sprite rpg;
    [SerializeField] private Sprite pistolAmmo;
    [SerializeField] private Sprite smgAmmo;
    [SerializeField] private Sprite rpgAmmo;
    [SerializeField] private Sprite coin;

    public Sprite Coin => coin;
    public Sprite AmmoAt(int index) => index == 0 ? pistolAmmo : index == 1 ? smgAmmo : rpgAmmo;

    public Sprite IconAt(int index) => index == 0 ? pistol : index == 1 ? smg : rpg;
}
