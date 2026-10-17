using UnityEngine;

/// <summary>Shared, imported pickup sprites. Runtime pickups never allocate textures.</summary>
public static class LootPickupVisuals
{
    public const float WorldSize = 0.72f;
    private static readonly Sprite[] ammo = new Sprite[6];
    private static readonly bool[] ammoLoaded = new bool[6];
    private static Sprite health;
    private static bool healthLoaded;

    public static Sprite Ammo(int slot, bool cesar)
    {
        slot = Mathf.Clamp(slot, 0, 2);
        int index = slot + (cesar ? 3 : 0);
        if (!ammoLoaded[index])
        {
            ammoLoaded[index] = true;
            ammo[index] = Resources.Load<Sprite>("Loot/" + (cesar ? "CesarAmmo" : "MarcoAmmo") + slot);
            if (ammo[index] == null)
            {
                var artwork = CharacterWeapons.Artwork(cesar);
                ammo[index] = artwork != null ? artwork.AmmoAt(slot) : null;
                Debug.LogWarning("Missing loot ammo sprite. Run Tools/Lindavista/Build loot pickup sprites in the editor.");
            }
        }
        return ammo[index];
    }

    public static Sprite Health
    {
        get
        {
            if (!healthLoaded)
            {
                healthLoaded = true;
                health = Resources.Load<Sprite>("Loot/Health");
                if (health == null)
                    Debug.LogWarning("Missing health pickup sprite. Run Tools/Lindavista/Build loot pickup sprites in the editor.");
            }
            return health;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearCache()
    {
        for (int i = 0; i < ammo.Length; i++)
        {
            ammo[i] = null;
            ammoLoaded[i] = false;
        }
        health = null;
        healthLoaded = false;
    }
}
