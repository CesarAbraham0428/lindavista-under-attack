using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Exercises the real shop buttons against a temporary save.</summary>
public static class CharacterShopChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Character shop check failed: " + message);
    }
    private static T Field<T>(PistolUpgradeShop shop, string name) =>
        (T)typeof(PistolUpgradeShop).GetField(name, Private).GetValue(shop);

    public static string RunRuntime()
    {
        if (!EditorApplication.isPlaying || PistolUpgradeShop.IsOpen)
            throw new Exception("Run in Play Mode with the shop closed.");
        assertions = 0;
        string directory = Path.Combine("Temp", "CharacterShopChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var repository = new ProgressionRepository(Path.Combine(directory, "profile.json"));
        repository.TryCredit(5000);
        PistolUpgradeShop shop = null;
        using (ProgressionService.UseVerificationRepository(repository))
        try
        {
            PistolUpgradeShop.Open();
            shop = UnityEngine.Object.FindFirstObjectByType<PistolUpgradeShop>();
            var tabs = Field<Button[]>(shop, "characterTabs");
            var upgrades = Field<Button[]>(shop, "upgrade");
            var ammunition = Field<Button[]>(shop, "ammo");
            var health = Field<Text>(shop, "health");
            tabs[0].onClick.Invoke();
            Check(health.text == "VIDA  10 / 15", "Marco base health and upgrade limit");
            Check(Field<Image[]>(shop, "life").Count(i => i.gameObject.activeSelf) == 15, "Marco meter capacity");
            Field<Button>(shop, "healthBuy").onClick.Invoke();
            ammunition[0].onClick.Invoke();
            upgrades[0].onClick.Invoke();
            Check(ProgressionService.MaxHealthFor(false) == 11 && ProgressionService.MaxHealthFor(true) == 12,
                "Marco health purchase leaves Cesar unchanged");
            Check(ProgressionService.Ammo(0, false) == 60 && ProgressionService.Ammo(0, true) == 48,
                "Marco ammo purchase leaves Cesar unchanged");
            Check(ProgressionService.WeaponUpgrade(0, false) == 1 && ProgressionService.WeaponUpgrade(0, true) == 0,
                "Marco weapon purchase leaves Cesar unchanged");
            tabs[1].onClick.Invoke();
            Check(health.text == "VIDA  12 / 17" && Field<Text>(shop, "speed").text.Contains("110%"), "Cesar stats shown");
            Check(Field<Image[]>(shop, "life").Count(i => i.gameObject.activeSelf) == 17, "Cesar meter capacity");
            Check(Field<Text[]>(shop, "weaponNames")[0].text == "ESCOPETA" &&
                Field<Image[]>(shop, "weaponIcons")[2].sprite == CharacterWeapons.Artwork(true).IconAt(2), "Cesar arsenal art and names");
            int coins = ProgressionService.Coins;
            int cost = WeaponDefinition.ForCharacter(0, true).UpgradeCost(0);
            upgrades[0].onClick.Invoke();
            Check(ProgressionService.Coins == coins - cost && ProgressionService.WeaponUpgrade(0, true) == 1,
                "Cesar purchase uses his weapon definition price");
            Field<Button>(shop, "healthBuy").onClick.Invoke();
            ammunition[2].onClick.Invoke();
            Check(ProgressionService.MaxHealthFor(true) == 13 && ProgressionService.MaxHealthFor(false) == 11,
                "Cesar health purchase leaves Marco unchanged");
            Check(ProgressionService.Ammo(2, true) == 8 && ProgressionService.Ammo(2, false) == 6,
                "Cesar grenade ammo leaves rockets unchanged");
            tabs[0].onClick.Invoke();
            Check(health.text == "VIDA  11 / 15" && Field<Text[]>(shop, "weaponNames")[0].text == "PISTOLA",
                "returning to Marco restores his own displayed progression");
            var loaded = new ProgressionRepository(Path.Combine(directory, "profile.json")).Snapshot;
            Check(loaded.Character(false).healthUpgrade == 1 && loaded.Character(true).healthUpgrade == 1,
                "shop transactions persist both characters");
            string result = "CHARACTER_SHOP_CHECKS_OK: " + assertions + " assertions.";
            Debug.Log(result);
            return result;
        }
        finally
        {
            if (shop != null) UnityEngine.Object.DestroyImmediate(shop.gameObject);
            Directory.Delete(directory, true);
        }
    }
}
