using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class ProgressionProfile
{
    public string format = "Lindavista.Progression";
    public int version = 1;
    public int coins;
    public int pistolUpgrade;
    public int completedLevelsMask;
    public int healthUpgrade;
    public int[] weaponUpgrades = { 0, 0, 0 };
    public int[] ammunition = { 48, 90, 6 };
    public string[] ownedWeapons = { "pistol" };

    public ProgressionProfile Copy() => new ProgressionProfile
    {
        coins = coins, pistolUpgrade = pistolUpgrade, healthUpgrade = healthUpgrade,
        weaponUpgrades = (int[])weaponUpgrades.Clone(), ammunition = (int[])ammunition.Clone(),
        completedLevelsMask = completedLevelsMask,
        ownedWeapons = (string[])ownedWeapons.Clone()
    };
}

/// <summary>Commits complete transactions before publishing them; preserves a recoverable backup.</summary>
public sealed class ProgressionRepository
{
    private readonly string path;
    private bool writable = true;
    private ProgressionProfile profile;
    public ProgressionProfile Snapshot => profile.Copy();
    public string LastError { get; private set; }
    public bool CanSave => writable;

    public ProgressionRepository(string savePath)
    {
        path = savePath;
        bool mainExists = File.Exists(path);
        bool backupExists = File.Exists(path + ".bak");
        profile = Read(path) ?? Read(path + ".bak");
        if (profile == null)
        {
            profile = new ProgressionProfile();
            writable = !mainExists && !backupExists;
            if (!writable) LastError = "No se pudo recuperar el progreso. El archivo original se conserva.";
        }
    }

    private static ProgressionProfile Read(string file)
    {
        try
        {
            if (!File.Exists(file)) return null;
            string json = File.ReadAllText(file);
            // Fields must be present: JsonUtility alone can accept incomplete documents.
            if (!json.Contains("\"format\"") || !json.Contains("\"version\"") ||
                !json.Contains("\"coins\"") || !json.Contains("\"pistolUpgrade\"") ||
                !json.Contains("\"completedLevelsMask\"") || !json.Contains("\"ownedWeapons\"")) return null;
            var data = JsonUtility.FromJson<ProgressionProfile>(json);
            if (data == null || data.format != "Lindavista.Progression" || data.version != 1 ||
                data.coins < 0 || data.pistolUpgrade < 0 || data.pistolUpgrade > WeaponDefinition.Pistol.MaxUpgrade ||
                data.completedLevelsMask < 0 || data.completedLevelsMask > 15 || data.ownedWeapons == null ||
                data.ownedWeapons.Length != 1 || data.ownedWeapons[0] != "pistol") return null;
            if (!json.Contains("\"ammunition\"")) data.ammunition = new[] { 48, 90, 6 };
            if (!json.Contains("\"weaponUpgrades\"")) data.weaponUpgrades = new[] { data.pistolUpgrade, 0, 0 };
            if (data.ammunition == null || data.ammunition.Length != 3 || data.weaponUpgrades == null || data.weaponUpgrades.Length != 3 || data.healthUpgrade < 0 || data.healthUpgrade > ShopBalance.MaxHealthUpgrade) return null;
            for (int i = 0; i < 3; i++)
                if (data.ammunition[i] < 0 || data.ammunition[i] > ShopBalance.Capacity[i] || data.weaponUpgrades[i] < 0 || data.weaponUpgrades[i] > WeaponDefinition.At(i).MaxUpgrade) return null;
            data.weaponUpgrades[0] = data.pistolUpgrade;
            return data;
        }
        catch (Exception) { return null; }
    }

    public bool TryCredit(int amount)
    {
        if (amount <= 0 || (long)profile.coins + amount > int.MaxValue) return false;
        var next = profile.Copy();
        next.coins += amount;
        return Commit(next);
    }

    public bool TryUpgradePistol() => TryUpgradeWeapon(0);
    public bool TryUpgradeWeapon(int index)
    {
        if (index < 0 || index > 2) return false;
        int cost = WeaponDefinition.At(index).UpgradeCost(profile.weaponUpgrades[index]);
        if (cost == 0) { LastError = "Mejora máxima alcanzada."; return false; }
        if (profile.coins < cost) { LastError = "No tienes suficientes monedas."; return false; }
        var next = profile.Copy();
        next.coins -= cost;
        next.weaponUpgrades[index]++;
        next.pistolUpgrade = next.weaponUpgrades[0];
        return Commit(next);
    }
    public bool TryUpgradeHealth()
    {
        int cost = ShopBalance.HealthCost(profile.healthUpgrade);
        if (cost == 0 || profile.coins < cost) { LastError = "Mejora máxima o monedas insuficientes."; return false; }
        var next = profile.Copy(); next.coins -= cost; next.healthUpgrade++;
        return Commit(next);
    }
    public bool TryBuyAmmo(int index)
    {
        if (index < 0 || index > 2) return false;
        if (profile.ammunition[index] + ShopBalance.Pack[index] > ShopBalance.Capacity[index])
        { LastError = "No cabe un cargador completo."; return false; }
        if (profile.coins < ShopBalance.AmmoCost[index]) { LastError = "No tienes suficientes monedas."; return false; }
        var next = profile.Copy(); next.coins -= ShopBalance.AmmoCost[index]; next.ammunition[index] += ShopBalance.Pack[index];
        return Commit(next);
    }
    public bool TryConsumeAmmo(int index)
    {
        if (index < 0 || index > 2 || profile.ammunition[index] <= 0) return false;
        var next = profile.Copy(); next.ammunition[index]--;
        return Commit(next);
    }

    public bool IsLevelUnlocked(int level) => level >= 0 && level < 4 &&
        (level == 0 || (profile.completedLevelsMask & (1 << (level - 1))) != 0);

    public bool TryCompleteLevel(int level)
    {
        if (!IsLevelUnlocked(level)) return false;
        var next = profile.Copy();
        next.completedLevelsMask |= 1 << level;
        return Commit(next);
    }

    private bool Commit(ProgressionProfile next)
    {
        if (!writable) return false;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(next, true));
            // Never copy a corrupt primary over a valid backup during recovery.
            if (Read(path) != null) File.Copy(path, path + ".bak", true);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            profile = next;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            LastError = "No se pudo guardar el progreso: " + exception.Message;
            return false;
        }
    }
}
