using System;
using System.IO;
using UnityEngine;

/// <summary>Shared wallet and levels; health, speed, weapon upgrades and ammunition belong to each character.</summary>
public static class ProgressionService
{
    private static ProgressionRepository repository;
    public static event Action Changed;
    private static ProgressionRepository Repository => repository ??=
        new ProgressionRepository(Path.Combine(Application.persistentDataPath, "progression-v1.json"), true);
    public static int Coins => Repository.Monedas;
    public static int HealthConsumables(bool cesar) => Repository.HealthConsumables(cesar);
    public static bool TryBuyHealthConsumable(bool cesar) => Publish(Repository.TryBuyHealthConsumable(cesar));
    public static bool TryConsumeHealthConsumable(bool cesar) => Publish(Repository.TryConsumeHealthConsumable(cesar));
    public static bool TryGrantHealthConsumable(bool cesar) => Publish(Repository.TryGrantHealthConsumable(cesar));
    public static bool TryGrantAmmo(int index, int amount, bool cesar) =>
        OwnsWeapon(index, cesar) && Publish(Repository.TryGrantAmmo(index, amount, cesar));
    public static int PistolUpgrade => WeaponUpgrade(0);
    public static string LastError => Repository.LastError;
    public static bool CanSave => Repository.CanSave;
    public static bool OwnsWeapon(int index) => OwnsWeapon(index, CharacterWeapons.SelectedCesar);
    public static bool OwnsWeapon(int index, bool cesar) => index >= 0 && index < 3;
    public static int HealthUpgrade => HealthUpgradeFor(CharacterWeapons.SelectedCesar);
    public static int HealthUpgradeFor(bool cesar) => Repository.MejoraVida(cesar);
    public static int MaxHealthFor(bool cesar) => CharacterStats.BaseHealth(cesar) + HealthUpgradeFor(cesar);
    public static int SpeedUpgrade => SpeedUpgradeFor(CharacterWeapons.SelectedCesar);
    public static int SpeedUpgradeFor(bool cesar) => Repository.SpeedUpgradeFor(cesar);
    public static int SpeedBarsFor(bool cesar) => 1 + SpeedUpgradeFor(cesar);
    public static int WeaponUpgrade(int index) => WeaponUpgrade(index, CharacterWeapons.SelectedCesar);
    public static int WeaponUpgrade(int index, bool cesar) => Repository.MejoraArma(index, cesar);
    public static int Ammo(int index) => Ammo(index, CharacterWeapons.SelectedCesar);
    public static int Ammo(int index, bool cesar) => Repository.Municion(index, cesar);
    public static bool TryUpgradeWeapon(int index) => TryUpgradeWeapon(index, CharacterWeapons.SelectedCesar);
    public static bool TryUpgradeWeapon(int index, bool cesar) => Publish(Repository.TryUpgradeWeapon(index, cesar));
    public static bool TryUpgradeHealth() => TryUpgradeHealth(CharacterWeapons.SelectedCesar);
    public static bool TryUpgradeHealth(bool cesar) => Publish(Repository.TryUpgradeHealth(cesar));
    public static bool TryUpgradeSpeed() => TryUpgradeSpeed(CharacterWeapons.SelectedCesar);
    public static bool TryUpgradeSpeed(bool cesar) => Publish(Repository.TryUpgradeSpeed(cesar));
    public static bool TryBuyAmmo(int index) => TryBuyAmmo(index, CharacterWeapons.SelectedCesar);
    public static bool TryBuyAmmo(int index, bool cesar) => Publish(Repository.TryBuyAmmo(index, cesar));
    public static bool TryConsumeAmmo(int index) => TryConsumeAmmo(index, CharacterWeapons.SelectedCesar);
    public static bool TryConsumeAmmo(int index, bool cesar) => Publish(Repository.TryConsumeAmmo(index, cesar));
    public static bool IsLevelUnlocked(int level) => Repository.IsLevelUnlocked(level);
    public static bool TryCredit(int amount) => Publish(Repository.TryCredit(amount));
    public static bool TryUpgradePistol() => TryUpgradeWeapon(0);
    public static bool TryCompleteLevel(int level) => Publish(Repository.TryCompleteLevel(level));

    private static bool Publish(bool success)
    {
        if (success) Changed?.Invoke();
        return success;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { repository = null; Changed = null; }

    public static bool GuardarPendientes() => repository == null || repository.GuardarPendientes();
    public static void GuardarPendientesEnSegundoPlano() => repository?.GuardarPendientesEnSegundoPlano();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void PrepararGuardado()
    {
        var objeto = new GameObject("Guardado de progreso");
        UnityEngine.Object.DontDestroyOnLoad(objeto);
        objeto.AddComponent<GuardadoProgreso>();
    }

#if UNITY_EDITOR
    // Scoped verification never writes the player's real profile.
    public static IDisposable UseVerificationRepository(ProgressionRepository temporary)
    {
        var scope = new VerificationScope(Repository);
        repository = temporary;
        Changed?.Invoke();
        return scope;
    }
    private sealed class VerificationScope : IDisposable
    {
        private ProgressionRepository previous;
        public VerificationScope(ProgressionRepository value) { previous = value; }
        public void Dispose()
        {
            if (previous == null) return;
            repository = previous;
            previous = null;
            Changed?.Invoke();
        }
    }
#endif
}
