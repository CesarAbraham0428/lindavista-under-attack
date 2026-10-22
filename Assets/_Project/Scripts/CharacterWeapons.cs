using UnityEngine;

/// <summary>Three local weapon slots per character, with their own progression and artwork.</summary>
public static class CharacterWeapons
{
    private static readonly string[] MarcoStates = { "Pistol", "SMG", "RPG" };
    private static readonly string[] CesarStates = { "Escopeta", "Francotirador", "Lanzagranadas" };
    private static readonly string[] CesarNames = { "ESCOPETA", "FRANCOTIRADOR", "LANZAGRANADAS" };
    private static readonly WeaponHUDAssets[] ilustraciones = new WeaponHUDAssets[2];

    public static bool SelectedCesar => PlayerPrefs.GetInt("Lindavista.SelectedCharacter", 0) == 1;
    public static bool IsCesar(GameObject player)
    {
        var animator = player.GetComponent<Animator>();
        return animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.name.Contains("Cesar")
            : player.name.Contains("Cesar");
    }

    public static string State(int slot, bool cesar) => (cesar ? CesarStates : MarcoStates)[slot];
    public static string Name(int slot, bool cesar) => (cesar ? CesarNames : ShopBalance.Names)[slot];
    public static WeaponHUDAssets Artwork(bool cesar)
    {
        int indice = cesar ? 1 : 0;
        if (ilustraciones[indice] == null)
            ilustraciones[indice] = Resources.Load<WeaponHUDAssets>(cesar ? "CesarWeaponHUDAssets" : "WeaponHUDAssets");
        return ilustraciones[indice];
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void LimpiarCache() { ilustraciones[0] = ilustraciones[1] = null; }
}
