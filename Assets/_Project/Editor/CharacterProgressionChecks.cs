using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Isolated checks for character inventories, legacy migration and runtime attributes.</summary>
public static class CharacterProgressionChecks
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Character progression check failed: " + message);
    }

    [MenuItem("Tools/Lindavista/Verify independent character progression")]
    public static void VerifyStorage() => RunStorage();

    public static string RunStorage()
    {
        assertions = 0;
        string directory = Path.Combine("Temp", "CharacterProgressionChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "profile.json");
            var repo = new ProgressionRepository(path);
            Check(repo.Snapshot.version == 2 && repo.Snapshot.marco != repo.Snapshot.cesar, "two distinct character records");
            Check(repo.TryCredit(2000), "shared wallet funded");
            Check(repo.TryUpgradeHealth(false) && repo.TryUpgradeHealth(false), "Marco health upgraded twice");
            Check(repo.TryUpgradeWeapon(0, false) && repo.TryUpgradeWeapon(1, false), "Marco weapons upgraded");
            Check(repo.TryBuyAmmo(0, false) && repo.TryConsumeAmmo(2, false), "Marco ammunition changed");
            Check(repo.Snapshot.cesar.healthUpgrade == 0 && repo.Snapshot.cesar.weaponUpgrades[0] == 0 &&
                repo.Snapshot.cesar.weaponUpgrades[1] == 0 && repo.Snapshot.cesar.ammunition[0] == 48 &&
                repo.Snapshot.cesar.ammunition[2] == 6, "Marco transactions leave Cesar untouched");
            Check(repo.TryUpgradeHealth(true) && repo.TryUpgradeWeapon(2, true) && repo.TryBuyAmmo(2, true), "Cesar purchases");
            Check(repo.Snapshot.marco.healthUpgrade == 2 && repo.Snapshot.marco.weaponUpgrades[2] == 0 &&
                repo.Snapshot.marco.ammunition[2] == 5, "Cesar transactions leave Marco untouched");
            for (int i = 0; i < 8; i++) Check(repo.TryConsumeAmmo(2, true), "consume Cesar grenade reserve " + i);
            Check(!repo.TryConsumeAmmo(2, true) && repo.Snapshot.marco.ammunition[2] == 5,
                "empty Cesar reserve cannot borrow Marco rockets");
            int balance = repo.Snapshot.coins;
            Check(!repo.TryBuyAmmo(0, false) && repo.Snapshot.coins == balance && repo.Snapshot.cesar.ammunition[0] == 48,
                "Marco full capacity rejection does not charge or touch Cesar");
            var loaded = new ProgressionRepository(path);
            Check(loaded.CanSave && loaded.Snapshot.marco.healthUpgrade == 2 && loaded.Snapshot.cesar.healthUpgrade == 1 &&
                loaded.Snapshot.marco.weaponUpgrades[0] == 1 && loaded.Snapshot.cesar.weaponUpgrades[0] == 0 &&
                loaded.Snapshot.cesar.weaponUpgrades[2] == 1 && loaded.Snapshot.cesar.ammunition[2] == 0 &&
                loaded.Snapshot.marco.ammunition[2] == 5, "different character progress survives restart");
            var snapshot = loaded.Snapshot;
            snapshot.marco.ammunition[0] = 0;
            snapshot.cesar.healthUpgrade = 5;
            Check(loaded.Snapshot.marco.ammunition[0] == 60 && loaded.Snapshot.cesar.healthUpgrade == 1,
                "snapshots cannot mutate stored character records");

            string legacyPath = Path.Combine(directory, "legacy.json");
            const string legacyJson = "{\"format\":\"Lindavista.Progression\",\"version\":1,\"coins\":100,\"pistolUpgrade\":2," +
                "\"completedLevelsMask\":3,\"healthUpgrade\":3,\"weaponUpgrades\":[2,4,3],\"ammunition\":[7,8,0],\"ownedWeapons\":[\"pistol\"]}";
            File.WriteAllText(legacyPath, legacyJson);
            var migrated = new ProgressionRepository(legacyPath);
            Check(migrated.CanSave && migrated.Snapshot.version == 2 && migrated.Snapshot.coins == 100 && migrated.IsLevelUnlocked(2),
                "version-one wallet and level progress preserved");
            Check(migrated.Snapshot.marco.healthUpgrade == 3 && migrated.Snapshot.marco.weaponUpgrades[0] == 2 &&
                migrated.Snapshot.marco.weaponUpgrades[1] == 4 && migrated.Snapshot.marco.weaponUpgrades[2] == 3 &&
                migrated.Snapshot.marco.ammunition[0] == 7 && migrated.Snapshot.marco.ammunition[2] == 0,
                "legacy inventory and upgrades belong to Marco");
            Check(migrated.Snapshot.cesar.healthUpgrade == 0 && migrated.Snapshot.cesar.weaponUpgrades[1] == 0 &&
                migrated.Snapshot.cesar.ammunition[0] == 48 && migrated.Snapshot.cesar.ammunition[2] == 6,
                "legacy progress does not grant Cesar upgrades or consumed reserves");
            Check(File.ReadAllText(legacyPath) == legacyJson, "migration reads without overwriting original save");
            Check(migrated.TryConsumeAmmo(2, true), "first migrated transaction commits new schema");
            Check(File.ReadAllText(legacyPath + ".bak") == legacyJson, "original legacy save retained as backup");
            var remigrated = new ProgressionRepository(legacyPath);
            Check(remigrated.Snapshot.marco.healthUpgrade == 3 && remigrated.Snapshot.cesar.ammunition[2] == 5,
                "migration survives save and reload");
            File.WriteAllText(legacyPath, "invalid");
            var recovered = new ProgressionRepository(legacyPath);
            Check(recovered.CanSave && recovered.Snapshot.marco.ammunition[0] == 7 && recovered.Snapshot.cesar.ammunition[2] == 6,
                "legacy backup recovery preserves original and fresh Cesar defaults");

            string oldestPath = Path.Combine(directory, "oldest.json");
            File.WriteAllText(oldestPath, "{\"format\":\"Lindavista.Progression\",\"version\":1,\"coins\":5," +
                "\"pistolUpgrade\":1,\"completedLevelsMask\":0,\"ownedWeapons\":[\"pistol\"]}");
            var oldest = new ProgressionRepository(oldestPath);
            Check(oldest.CanSave && oldest.Snapshot.marco.weaponUpgrades[0] == 1 && oldest.Snapshot.marco.ammunition[0] == 48 &&
                oldest.Snapshot.cesar.weaponUpgrades[0] == 0, "oldest optional fields migrate safely");
            string invalidPath = Path.Combine(directory, "invalid.json");
            var invalid = new ProgressionProfile();
            invalid.cesar.ammunition[1] = -1;
            File.WriteAllText(invalidPath, JsonUtility.ToJson(invalid));
            var blocked = new ProgressionRepository(invalidPath);
            Check(!blocked.CanSave && !blocked.TryCredit(1), "invalid character data never overwritten");
            string incompletePath = Path.Combine(directory, "incomplete.json");
            File.WriteAllText(incompletePath, "{\"format\":\"Lindavista.Progression\",\"version\":2,\"coins\":0," +
                "\"completedLevelsMask\":0,\"marco\":{},\"cesar\":{}}");
            Check(!new ProgressionRepository(incompletePath).CanSave, "incomplete nested character inventories rejected");
            string missingPath = Path.Combine(directory, "missing.json");
            File.WriteAllText(missingPath, "{\"format\":\"Lindavista.Progression\",\"version\":2,\"coins\":0," +
                "\"completedLevelsMask\":0,\"marco\":{\"healthUpgrade\":0,\"weaponUpgrades\":[0,0,0],\"ammunition\":[48,90,6]}}");
            Check(!new ProgressionRepository(missingPath).CanSave, "missing Cesar record rejected");
            string result = assertions + " character storage assertions passed; real save untouched.";
            Debug.Log(result);
            return result;
        }
        finally { Directory.Delete(directory, true); }
    }

    // Can run in an empty Play Mode scene. Character identity must beat the menu preference.
    public static string RunRuntime()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        assertions = 0;
        string directory = Path.Combine("Temp", "CharacterProgressionChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var repo = new ProgressionRepository(Path.Combine(directory, "runtime.json"));
        GameObject marco = null, cesar = null;
        const string selectionKey = "Lindavista.SelectedCharacter";
        bool hadSelection = PlayerPrefs.HasKey(selectionKey);
        int previousSelection = PlayerPrefs.GetInt(selectionKey, 0);
        try
        {
            using (ProgressionService.UseVerificationRepository(repo))
            {
                try
                {
                    PlayerPrefs.SetInt(selectionKey, 1);
                    marco = CreatePlayer("Verification Marco");
                    PlayerPrefs.SetInt(selectionKey, 0);
                    cesar = CreatePlayer("Verification Cesar");
                    var marcoHealth = marco.GetComponent<PlayerHealth>();
                    var cesarHealth = cesar.GetComponent<PlayerHealth>();
                    var marcoWeapons = marco.GetComponent<PlayerWeaponController>();
                    var cesarWeapons = cesar.GetComponent<PlayerWeaponController>();
                    Check(!marcoHealth.IsCesar && cesarHealth.IsCesar && marcoHealth.MaxHealth == 10 && cesarHealth.MaxHealth == 12,
                        "runtime health uses character identity despite opposite selection");
                    Check(marcoHealth.CurrentHealth == 10 && cesarHealth.CurrentHealth == 12, "each match starts at its own full health");
                    Check(Mathf.Approximately(cesar.GetComponent<PlayerMovement>().MovementSpeed,
                        marco.GetComponent<PlayerMovement>().MovementSpeed * 1.1f), "Cesar movement is ten percent faster");
                    Check(ProgressionService.TryCredit(1000) && ProgressionService.TryUpgradeHealth(false) &&
                        ProgressionService.TryUpgradeWeapon(0, false), "Marco purchases with Cesar object active");
                    Check(marcoHealth.MaxHealth == 11 && marcoHealth.CurrentHealth == 11 && cesarHealth.MaxHealth == 12 &&
                        marcoWeapons.Damage == WeaponDefinition.ForCharacter(0, false).DamageAt(1) &&
                        cesarWeapons.Damage == WeaponDefinition.ForCharacter(0, true).DamageAt(0),
                        "only Marco runtime attributes react to Marco purchases");
                    Check(ProgressionService.TryUpgradeHealth(true) && ProgressionService.TryUpgradeWeapon(0, true), "Cesar purchases");
                    PlayerPrefs.SetInt(selectionKey, 1);
                    Check(marcoHealth.MaxHealth == 11 && cesarHealth.MaxHealth == 13 &&
                        marcoWeapons.Damage == WeaponDefinition.ForCharacter(0, false).DamageAt(1) &&
                        cesarWeapons.Damage == WeaponDefinition.ForCharacter(0, true).DamageAt(1),
                        "changing menu selection does not replace active object progress");
                    Check(ProgressionService.TryConsumeAmmo(0, true) && ProgressionService.Ammo(0, true) == 47 &&
                        ProgressionService.Ammo(0, false) == 48, "runtime reserve belongs to firing character");
                    string result = assertions + " character runtime assertions passed; real save untouched.";
                    Debug.Log(result);
                    return result;
                }
                finally
                {
                    if (marco != null) UnityEngine.Object.DestroyImmediate(marco);
                    if (cesar != null) UnityEngine.Object.DestroyImmediate(cesar);
                }
            }
        }
        finally
        {
            if (hadSelection) PlayerPrefs.SetInt(selectionKey, previousSelection);
            else PlayerPrefs.DeleteKey(selectionKey);
            Directory.Delete(directory, true);
        }
    }

    private static GameObject CreatePlayer(string name)
    {
        var player = new GameObject(name);
        player.SetActive(false);
        player.AddComponent<Animator>();
        player.AddComponent<Rigidbody2D>().gravityScale = 0;
        player.AddComponent<PlayerMovement>();
        player.AddComponent<PlayerHealth>();
        player.AddComponent<PlayerWeaponController>();
        player.SetActive(true);
        return player;
    }
}
