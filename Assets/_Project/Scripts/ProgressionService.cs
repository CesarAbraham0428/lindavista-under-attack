using System;
using System.IO;
using UnityEngine;

/// <summary>One shared profile for both characters. Scene objects never own the wallet.</summary>
public static class ProgressionService
{
    private static ProgressionRepository repository;
    public static event Action Changed;
    private static ProgressionRepository Repository => repository ??=
        new ProgressionRepository(Path.Combine(Application.persistentDataPath, "progression-v1.json"));
    public static int Coins => Repository.Snapshot.coins;
    public static int PistolUpgrade => Repository.Snapshot.pistolUpgrade;
    public static string LastError => Repository.LastError;
    public static bool CanSave => Repository.CanSave;
    public static bool OwnsWeapon(int index) => index == 0;
    public static bool IsLevelUnlocked(int level) => Repository.IsLevelUnlocked(level);
    public static bool TryCredit(int amount) => Publish(Repository.TryCredit(amount));
    public static bool TryUpgradePistol() => Publish(Repository.TryUpgradePistol());
    public static bool TryCompleteLevel(int level) => Publish(Repository.TryCompleteLevel(level));

    private static bool Publish(bool success)
    {
        if (success) Changed?.Invoke();
        return success;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { repository = null; Changed = null; }

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
