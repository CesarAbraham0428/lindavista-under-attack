using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

[Serializable]
public sealed class CharacterProgression
{
    public int healthUpgrade;
    // Additive v2 field: existing saves start with one speed bar (zero purchases).
    public int speedUpgrade;
    // Additive field: profiles created before consumables start with zero pills.
    public int healthConsumables;
    public int[] weaponUpgrades;
    public int[] ammunition;

    public static CharacterProgression Create() => new CharacterProgression
    {
        weaponUpgrades = new[] { 0, 0, 0 }, ammunition = new[] { 48, 90, 6 }
    };

    public CharacterProgression Copy() => new CharacterProgression
    {
        healthUpgrade = healthUpgrade, speedUpgrade = speedUpgrade, healthConsumables = healthConsumables,
        weaponUpgrades = (int[])weaponUpgrades.Clone(),
        ammunition = (int[])ammunition.Clone()
    };
}

[Serializable]
public sealed class ProgressionProfile
{
    public string format = "Lindavista.Progression";
    public int version = 2;
    public int coins;
    public int completedLevelsMask;
    public CharacterProgression marco = CharacterProgression.Create();
    public CharacterProgression cesar = CharacterProgression.Create();

    public CharacterProgression Character(bool isCesar) => isCesar ? cesar : marco;

    // Compatibility for old verification tools: the old inventory belongs to Marco.
    public int pistolUpgrade => marco.weaponUpgrades[0];
    public int healthUpgrade => marco.healthUpgrade;
    public int[] weaponUpgrades => marco.weaponUpgrades;
    public int[] ammunition => marco.ammunition;

    public ProgressionProfile Copy() => new ProgressionProfile
    {
        coins = coins, completedLevelsMask = completedLevelsMask,
        marco = marco.Copy(), cesar = cesar.Copy()
    };
}

/// <summary>One wallet and level map, with separately saved inventories for each character.</summary>
public sealed class ProgressionRepository
{
    [Serializable]
    private sealed class SaveHeader
    {
        public string format;
        public int version;
        public int coins;
        public int completedLevelsMask;
    }

    [Serializable]
    private sealed class LegacyProfile
    {
        public int pistolUpgrade;
        public int healthUpgrade;
        public int[] weaponUpgrades;
        public int[] ammunition;
        public string[] ownedWeapons;
    }

    [Serializable]
    private sealed class StoredCharacters
    {
        public CharacterProgression marco;
        public CharacterProgression cesar;
    }

    private readonly string path;
    private readonly bool diferirMunicion;
    private bool municionPendiente;
    private bool principalValido;
    private bool directorioPreparado;
    private bool writable = true;
    private ProgressionProfile profile;
    // Disk access is serialized; sequence numbers keep an older snapshot from replacing a newer one.
    private readonly object archivo = new();
    private long secuenciaSolicitada, secuenciaEscrita;
    private Task escrituraEnCurso;
    private volatile bool falloEnSegundoPlano;
    private volatile string errorSegundoPlano;
    public ProgressionProfile Snapshot => profile.Copy();
    // Las lecturas frecuentes no necesitan copiar los inventarios de ambos personajes.
    public int Monedas => profile.coins;
    public int MejoraVida(bool cesar) => profile.Character(cesar).healthUpgrade;
    public int HealthConsumables(bool cesar) => profile.Character(cesar).healthConsumables;
    public int MejoraArma(int indice, bool cesar) => profile.Character(cesar).weaponUpgrades[indice];
    public int Municion(int indice, bool cesar) => profile.Character(cesar).ammunition[indice];
    // Movement reads this every physics frame; return the scalar without cloning the inventory.
    public int SpeedUpgradeFor(bool cesar) => profile.Character(cesar).speedUpgrade;
    public string LastError { get; private set; }
    public bool CanSave => writable;

    public ProgressionRepository(string savePath, bool diferirMunicion = false)
    {
        path = savePath;
        this.diferirMunicion = diferirMunicion;
        bool mainExists = File.Exists(path);
        bool backupExists = File.Exists(path + ".bak");
        profile = Read(path);
        principalValido = profile != null;
        profile ??= Read(path + ".bak");
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
            if (!json.Contains("\"format\"") || !json.Contains("\"version\"") ||
                !json.Contains("\"coins\"") || !json.Contains("\"completedLevelsMask\"")) return null;
            var header = JsonUtility.FromJson<SaveHeader>(json);
            if (header == null || header.format != "Lindavista.Progression" ||
                header.coins < 0 || header.completedLevelsMask < 0 || header.completedLevelsMask > 15) return null;

            ProgressionProfile data;
            if (header.version == 1)
            {
                if (!json.Contains("\"pistolUpgrade\"") || !json.Contains("\"ownedWeapons\"")) return null;
                var legacy = JsonUtility.FromJson<LegacyProfile>(json);
                if (legacy == null || legacy.pistolUpgrade < 0 || legacy.pistolUpgrade > WeaponDefinition.Pistol.MaxUpgrade ||
                    legacy.ownedWeapons == null || legacy.ownedWeapons.Length != 1 || legacy.ownedWeapons[0] != "pistol") return null;
                if (!json.Contains("\"ammunition\"")) legacy.ammunition = new[] { 48, 90, 6 };
                if (!json.Contains("\"weaponUpgrades\"")) legacy.weaponUpgrades = new[] { legacy.pistolUpgrade, 0, 0 };
                if (legacy.weaponUpgrades == null || legacy.weaponUpgrades.Length != 3) return null;
                legacy.weaponUpgrades[0] = legacy.pistolUpgrade;
                data = new ProgressionProfile
                {
                    coins = header.coins, completedLevelsMask = header.completedLevelsMask,
                    marco = new CharacterProgression
                    {
                        healthUpgrade = legacy.healthUpgrade, weaponUpgrades = legacy.weaponUpgrades,
                        ammunition = legacy.ammunition
                    }
                };
            }
            else if (header.version == 2)
            {
                if (!json.Contains("\"marco\"") || !json.Contains("\"cesar\"")) return null;
                // Read without the new-profile defaults so missing inventories cannot be silently reset.
                var characters = JsonUtility.FromJson<StoredCharacters>(json);
                if (characters == null) return null;
                data = new ProgressionProfile
                {
                    coins = header.coins, completedLevelsMask = header.completedLevelsMask,
                    marco = characters.marco, cesar = characters.cesar
                };
            }
            else return null;

            if (data == null || !Valid(data.marco, false) || !Valid(data.cesar, true)) return null;
            return data;
        }
        catch (Exception) { return null; }
    }

    private static bool Valid(CharacterProgression character, bool cesar)
    {
        if (character == null || character.ammunition == null || character.ammunition.Length != 3 ||
            character.weaponUpgrades == null || character.weaponUpgrades.Length != 3 ||
            character.healthUpgrade < 0 || character.healthUpgrade > ShopBalance.MaxHealthUpgrade ||
            character.speedUpgrade < 0 || character.speedUpgrade >= CharacterStats.MaximumSpeedBars(cesar) ||
            character.healthConsumables < 0 || character.healthConsumables > ShopBalance.MaxHealthConsumables) return false;
        for (int i = 0; i < 3; i++)
            if (character.ammunition[i] < 0 || character.ammunition[i] > ShopBalance.CapacityFor(i, cesar) ||
                character.weaponUpgrades[i] < 0 || character.weaponUpgrades[i] > WeaponDefinition.ForCharacter(i, cesar).MaxUpgrade) return false;
        return true;
    }

    public bool TryCredit(int amount)
    {
        if (amount <= 0 || (long)profile.coins + amount > int.MaxValue) return false;
        var next = profile.Copy();
        next.coins += amount;
        return Commit(next);
    }

    public bool TryUpgradePistol() => TryUpgradeWeapon(0, false);
    public bool TryUpgradeWeapon(int index) => TryUpgradeWeapon(index, false);
    public bool TryUpgradeWeapon(int index, bool cesar)
    {
        if (index < 0 || index > 2) return false;
        int cost = WeaponDefinition.ForCharacter(index, cesar).UpgradeCost(profile.Character(cesar).weaponUpgrades[index]);
        if (cost == 0) { LastError = "Mejora máxima alcanzada."; return false; }
        if (profile.coins < cost) { LastError = "No tienes suficientes monedas."; return false; }
        var next = profile.Copy();
        next.coins -= cost;
        next.Character(cesar).weaponUpgrades[index]++;
        return Commit(next);
    }

    public bool TryUpgradeHealth() => TryUpgradeHealth(false);
    public bool TryUpgradeHealth(bool cesar)
    {
        int cost = ShopBalance.HealthCost(profile.Character(cesar).healthUpgrade);
        if (cost == 0 || profile.coins < cost) { LastError = "Mejora máxima o monedas insuficientes."; return false; }
        var next = profile.Copy();
        next.coins -= cost;
        next.Character(cesar).healthUpgrade++;
        return Commit(next);
    }

    public bool TryUpgradeSpeed(bool cesar)
    {
        int cost = ShopBalance.SpeedCost(profile.Character(cesar).speedUpgrade, cesar);
        if (cost == 0) { LastError = "Velocidad máxima alcanzada."; return false; }
        if (profile.coins < cost) { LastError = "No tienes suficientes monedas."; return false; }
        var next = profile.Copy();
        next.coins -= cost;
        next.Character(cesar).speedUpgrade++;
        return Commit(next);
    }

    public bool TryBuyAmmo(int index) => TryBuyAmmo(index, false);

    public bool TryBuyHealthConsumable(bool cesar)
    {
        if (HealthConsumables(cesar) >= ShopBalance.MaxHealthConsumables)
        { LastError = "Ya tienes diez pastillas para este personaje."; return false; }
        if (profile.coins < ShopBalance.HealthConsumableCost)
        { LastError = "No tienes suficientes monedas."; return false; }
        var next = profile.Copy();
        next.coins -= ShopBalance.HealthConsumableCost;
        next.Character(cesar).healthConsumables++;
        return Commit(next);
    }

    public bool TryConsumeHealthConsumable(bool cesar)
    {
        if (HealthConsumables(cesar) <= 0)
        { LastError = "No quedan pastillas para este personaje."; return false; }
        var next = profile.Copy();
        next.Character(cesar).healthConsumables--;
        return Commit(next);
    }

    // Enemy rewards use the same saved inventory as the shop, without spending coins.
    public bool TryGrantHealthConsumable(bool cesar)
    {
        if (HealthConsumables(cesar) >= ShopBalance.MaxHealthConsumables) return false;
        var next = profile.Copy();
        next.Character(cesar).healthConsumables++;
        return Commit(next);
    }

    public bool TryGrantAmmo(int index, int amount, bool cesar)
    {
        if (index < 0 || index >= 3 || amount <= 0) return false;
        int current = Municion(index, cesar);
        if (current >= ShopBalance.CapacityFor(index, cesar)) return false;
        var next = profile.Copy();
        next.Character(cesar).ammunition[index] = current + Mathf.Min(amount, ShopBalance.CapacityFor(index, cesar) - current);
        return Commit(next);
    }

    public bool TryBuyAmmo(int index, bool cesar)
    {
        if (index < 0 || index > 2) return false;
        if (profile.Character(cesar).ammunition[index] + ShopBalance.Pack[index] > ShopBalance.CapacityFor(index, cesar))
        { LastError = "No cabe un cargador completo."; return false; }
        if (profile.coins < ShopBalance.AmmoCost[index]) { LastError = "No tienes suficientes monedas."; return false; }
        var next = profile.Copy();
        next.coins -= ShopBalance.AmmoCost[index];
        next.Character(cesar).ammunition[index] += ShopBalance.Pack[index];
        return Commit(next);
    }

    public bool TryConsumeAmmo(int index) => TryConsumeAmmo(index, false);
    public bool TryConsumeAmmo(int index, bool cesar)
    {
        if (!writable || index < 0 || index > 2 || profile.Character(cesar).ammunition[index] <= 0) return false;
        if (diferirMunicion)
        {
            profile.Character(cesar).ammunition[index]--;
            municionPendiente = true;
            return true;
        }
        var next = profile.Copy();
        next.Character(cesar).ammunition[index]--;
        return Commit(next);
    }

    public bool IsLevelUnlocked(int level) => level >= 0 && level < 4 &&
        (level == 0 || (profile.completedLevelsMask & (1 << (level - 1))) != 0);

    public bool TryCompleteLevel(int level)
    {
        if (!IsLevelUnlocked(level)) return false;
        if ((profile.completedLevelsMask & (1 << level)) != 0) return GuardarPendientes();
        var next = profile.Copy();
        next.completedLevelsMask |= 1 << level;
        return Commit(next);
    }

    // Solo los consumos se agrupan; compras, monedas y niveles siguen confirmándose en disco.
    // Al suspender o salir, una escritura en segundo plano debe terminar (o repetirse aquí).
    public bool GuardarPendientes()
    {
        escrituraEnCurso?.Wait();
        RecuperarFalloEnSegundoPlano();
        return !municionPendiente || Commit(profile);
    }

    /// <summary>
    /// Confirma la munición diferida sin bloquear el cuadro: el JSON se toma aquí y el disco se
    /// escribe en otro hilo. Un fallo deja los consumos pendientes para el siguiente intento.
    /// </summary>
    public void GuardarPendientesEnSegundoPlano()
    {
        RecuperarFalloEnSegundoPlano();
        if (!municionPendiente || !writable || (escrituraEnCurso != null && !escrituraEnCurso.IsCompleted)) return;
        string json = JsonUtility.ToJson(profile);
        long secuencia = ++secuenciaSolicitada;
        municionPendiente = false;
        escrituraEnCurso = Task.Run(() =>
        {
            try
            {
                lock (archivo) Escribir(json, secuencia);
            }
            catch (Exception exception)
            {
                errorSegundoPlano = "No se pudo guardar el progreso: " + exception.Message;
                falloEnSegundoPlano = true;
            }
        });
    }

    private void RecuperarFalloEnSegundoPlano()
    {
        if (!falloEnSegundoPlano) return;
        falloEnSegundoPlano = false;
        municionPendiente = true;
        LastError = errorSegundoPlano;
    }

    private bool Commit(ProgressionProfile next)
    {
        if (!writable) return false;
        try
        {
            string json = JsonUtility.ToJson(next);
            long secuencia = ++secuenciaSolicitada;
            lock (archivo) Escribir(json, secuencia);
            profile = next;
            municionPendiente = false;
            LastError = null;
            return true;
        }
        catch (Exception exception)
        {
            LastError = "No se pudo guardar el progreso: " + exception.Message;
            return false;
        }
    }

    // Called with the file lock held, from the main thread or the background writer.
    private void Escribir(string json, long secuencia)
    {
        if (secuencia <= secuenciaEscrita) return;
        if (!directorioPreparado)
        {
            string directorio = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directorio)) Directory.CreateDirectory(directorio);
            directorioPreparado = true;
        }
        string temporary = path + ".tmp";
        File.WriteAllText(temporary, json);
        // Never copy a corrupt primary over a valid backup during recovery.
        if (File.Exists(path))
        {
            // Copiar el respaldo por separado evita borrar/reemplazar un .bak
            // que un sincronizador de Windows puede mantener abierto.
            if (principalValido) File.Copy(path, path + ".bak", true);
            File.Replace(temporary, path, null);
        }
        else File.Move(temporary, path);
        principalValido = true;
        secuenciaEscrita = secuencia;
    }
}
