using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic checks against isolated save files. Never changes the real wallet.</summary>
public static class ProgressionChecks
{
    private static IDisposable sceneScope;
    private static string sceneDirectory;
    private static GameObject physicsPlayer, physicsEnemy;
    private static bool previousBackground;

    // Keeps an isolated wallet across frames to exercise real physics and reload timing.
    public static string BeginSceneChecks()
    {
        if (!EditorApplication.isPlaying || GameFlowController.IsMatchEnded)
            throw new Exception("Start an unfinished Gameplay match first.");
        if (sceneScope != null) throw new Exception("Scene checks already running.");
        previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        sceneDirectory = Path.Combine("Temp", "ProgressionChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sceneDirectory);
        sceneScope = ProgressionService.UseVerificationRepository(new ProgressionRepository(Path.Combine(sceneDirectory, "scene.json")));
        EditorApplication.playModeStateChanged += OnPlayState;
        physicsPlayer = new GameObject("Physics Verification Player", typeof(BoxCollider2D), typeof(Rigidbody2D), typeof(PlayerHealth), typeof(PlayerWeaponController));
        physicsPlayer.transform.position = new Vector3(1000, 0, 0);
        physicsPlayer.GetComponent<Rigidbody2D>().gravityScale = 0;
        var coin = UnityEngine.Object.Instantiate(Resources.Load<GameObject>("CoinPickup"), physicsPlayer.transform.position, Quaternion.identity);
        coin.GetComponent<CoinPickup>().Initialize(5);
        physicsEnemy = new GameObject("Physics Verification Enemy", typeof(EnemyHealth));
        physicsEnemy.transform.position = new Vector3(1004, 0, 0);
        var weapon = physicsPlayer.GetComponent<PlayerWeaponController>();
        Check(weapon.TryFire(new Vector2(1002, 0), Vector2.right, physicsPlayer.transform), "physics shot spawned");
        Check(weapon.TryReload(), "timed reload started");
        return "Isolated scene checks started. Wait for the intro and enemy release, then CompleteSceneChecks.";
    }

    public static string CompleteSceneChecks()
    {
        if (sceneScope == null) throw new Exception("Call BeginSceneChecks first.");
        assertions = 0;
        Check(ProgressionService.Coins == 5, "real trigger pickup credits once");
        Check(physicsEnemy.GetComponent<EnemyHealth>().CurrentHealth == 1, "physics projectile applies base damage");
        var weapon = physicsPlayer.GetComponent<PlayerWeaponController>();
        Check(!weapon.IsReloading && weapon.Magazine == 12, "timed reload refills magazine");
        var wave = UnityEngine.Object.FindFirstObjectByType<LevelOneEnemySpawner>();
        Check(wave != null && wave.TotalEnemies == 10 && wave.PendingEnemies == 0, "level-one release finished");
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerActions>();
        int killedBefore = GameFlowController.EnemiesKilled;
        foreach (var enemy in wave.GetComponentsInChildren<EnemyHealth>()) enemy.TakeDamage(999);
        Check(wave.AliveEnemies == 0 && GameFlowController.EnemiesKilled - killedBefore == 10, "actual wave counted once");
        Check(GameFlowController.IsCollectionPhase && !GameFlowController.IsMatchEnded, "last death allows collection before victory");
        int lootTotal = 0;
        foreach (var coin in UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None))
        {
            if (coin.transform.position.x > 900) continue;
            lootTotal += coin.Value;
            Check(coin.TryCollect(player.Health), "wave coin collected");
        }
        Check(lootTotal == 70 && ProgressionService.Coins == 75, "six normal and four pistol rewards total 70");
        GameFlowController.FinishLevel();
        Check(GameFlowController.IsVictoryActive && player.IsInputLocked && ProgressionService.IsLevelUnlocked(1), "victory locks input and saves next level");
        PistolUpgradeShop.Open();
        var shop = UnityEngine.Object.FindFirstObjectByType<PistolUpgradeShop>();
        var buy = shop.GetComponentsInChildren<UnityEngine.UI.Button>().First(b => b.GetComponentInChildren<UnityEngine.UI.Text>().text.StartsWith("MEJORAR"));
        Check(buy.interactable, "shop purchase available");
        buy.onClick.Invoke();
        Check(ProgressionService.Coins == 50 && ProgressionService.PistolUpgrade == 1 && player.Weapons.Damage == 3, "shop button buys first upgrade atomically");
        Check(GameObject.Find("Defeat Canvas").GetComponentsInChildren<UnityEngine.UI.Text>().Any(t => t.text.Contains("Saldo: 50")), "result balance refreshes after purchase");
        Check(!buy.interactable, "second upgrade disabled without sufficient funds");
        Debug.Log("Progression scene checks passed: " + assertions + " assertions. Call EndSceneChecks after inspecting UI.");
        return assertions + " scene assertions passed; wallet isolated until EndSceneChecks.";
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingPlayMode) EndSceneChecks();
    }

    public static void EndSceneChecks()
    {
        if (sceneScope == null) return;
        EditorApplication.playModeStateChanged -= OnPlayState;
        sceneScope.Dispose();
        sceneScope = null;
        Application.runInBackground = previousBackground;
        if (physicsPlayer != null) UnityEngine.Object.DestroyImmediate(physicsPlayer);
        if (physicsEnemy != null) UnityEngine.Object.DestroyImmediate(physicsEnemy);
        foreach (var coin in UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None))
            if (coin.transform.position.x > 900) UnityEngine.Object.DestroyImmediate(coin.gameObject);
        Directory.Delete(sceneDirectory, true);
    }

    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception("Progression check failed: " + message);
    }

    [MenuItem("Tools/Lindavista/Verify progression storage")]
    public static void RunStorage()
    {
        assertions = 0;
        string directory = Path.Combine("Temp", "ProgressionChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "profile.json");
        try
        {
            var repo = new ProgressionRepository(path);
            Check(repo.Snapshot.coins == 0 && repo.Snapshot.pistolUpgrade == 0, "new profile");
            Check(repo.IsLevelUnlocked(0) && !repo.IsLevelUnlocked(1), "initial level lock");
            Check(!repo.TryCredit(0) && !repo.TryCredit(-1), "reject invalid rewards");
            Check(!repo.TryUpgradePistol(), "reject unaffordable upgrade");
            Check(repo.TryCredit(70), "credit level-one rewards");
            Check(repo.TryUpgradePistol(), "first purchase");
            Check(repo.Snapshot.coins == 45 && repo.Snapshot.pistolUpgrade == 1, "atomic purchase balance");
            Check(!repo.TryUpgradePistol() && repo.Snapshot.coins == 45, "failed purchase preserves wallet");
            Check(repo.TryCredit(15) && repo.TryUpgradePistol(), "second purchase");
            Check(repo.Snapshot.coins == 0 && repo.Snapshot.pistolUpgrade == 2, "second price");
            Check(!repo.TryUpgradePistol(), "maximum upgrade");
            Check(!repo.TryCompleteLevel(2), "cannot complete a locked level");
            Check(repo.TryCompleteLevel(0) && repo.IsLevelUnlocked(1), "victory unlocks next level");
            var loaded = new ProgressionRepository(path);
            Check(loaded.Snapshot.pistolUpgrade == 2 && loaded.IsLevelUnlocked(1), "reload from disk");
            Check(loaded.TryCredit(7), "create backup of valid save");
            File.WriteAllText(path, "broken save");
            var recovered = new ProgressionRepository(path);
            Check(recovered.CanSave && recovered.Snapshot.pistolUpgrade == 2 && recovered.IsLevelUnlocked(1), "backup recovery");
            Check(recovered.TryCredit(3), "commit after recovery");
            File.WriteAllText(path, "broken");
            File.WriteAllText(path + ".bak", "broken");
            var blocked = new ProgressionRepository(path);
            Check(!blocked.CanSave && !blocked.TryCredit(1), "corrupt files never silently overwritten");
            Check(WeaponDefinition.Pistol.DamageAt(0) == 2 && WeaponDefinition.Pistol.DamageAt(2) == 4, "pistol damage tiers");
            Check(ProgressionService.OwnsWeapon(0) && !ProgressionService.OwnsWeapon(1) && !ProgressionService.OwnsWeapon(2), "pistol-only inventory");
            Debug.Log("Progression storage checks passed: " + assertions + " assertions.");
        }
        finally { Directory.Delete(directory, true); }
    }

    // Run in Play Mode, after the intro. Scene restart afterward restores its transient actors.
    public static string RunRuntime()
    {
        if (!EditorApplication.isPlaying || GameFlowController.IsMatchEnded)
            throw new Exception("Runtime checks require an active, unfinished match.");
        assertions = 0;
        string directory = Path.Combine("Temp", "ProgressionChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var repository = new ProgressionRepository(Path.Combine(directory, "runtime.json"));
        GameObject proxy = null, enemy = null;
        using (ProgressionService.UseVerificationRepository(repository))
        {
            try
            {
                proxy = new GameObject("Verification Player", typeof(PlayerHealth), typeof(PlayerWeaponController));
                proxy.transform.position = new Vector3(1000, 0, 0);
                var health = proxy.GetComponent<PlayerHealth>();
                Check(health.CurrentHealth == 5, "initial player health");
                Check(health.TakeDamage(1) && !health.TakeDamage(1) && health.CurrentHealth == 4, "damage protection");
                health.Initialize(5, 0);
                int playerDeaths = 0;
                health.Died += () => playerDeaths++;
                health.TakeDamage(99);
                health.TakeDamage(99);
                Check(health.CurrentHealth == 0 && playerDeaths == 1, "one player death event");
                health.Initialize(5, 0);

                enemy = new GameObject("Verification Enemy", typeof(EnemyHealth), typeof(EnemyLootDrop));
                enemy.transform.position = new Vector3(1000, 0, 0);
                var receiver = enemy.GetComponent<EnemyHealth>();
                int deaths = 0;
                receiver.Died += _ => deaths++;
                receiver.TakeDamage(99);
                receiver.TakeDamage(99);
                Check(receiver.IsDead && receiver.CurrentHealth == 0 && deaths == 1, "one enemy death event");
                var coins = UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None)
                    .Where(c => c.transform.position.x > 900).ToArray();
                Check(coins.Length == 1 && ProgressionService.Coins == 0, "death drops exactly one coin without credit");
                Check(coins[0].gameObject.layer == 2, "loot excluded from raycasts");
                Check(coins[0].TryCollect(health) && !coins[0].TryCollect(health), "single collection");
                Check(ProgressionService.Coins == 5, "physical pickup credits wallet");

                var weapon = proxy.GetComponent<PlayerWeaponController>();
                Check(!weapon.TryEquip(1) && !weapon.TryEquip(2) && weapon.TryEquip(0), "locked weapons");
                Check(weapon.TryFire(new Vector2(1002, 0), Vector2.right, proxy.transform), "pistol fires");
                Check(weapon.Magazine == 11 && !weapon.TryFire(new Vector2(1002, 0), Vector2.right, proxy.transform), "ammo consumption and cadence");
                var projectile = UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).First(p => p.transform.position.x > 900);
                Check(projectile.Damage == 2, "projectile snapshots base damage");
                Check(ProgressionService.TryCredit(20) && ProgressionService.TryUpgradePistol(), "upgrade from pickup earnings");
                Check(weapon.Damage == 3 && projectile.Damage == 2, "upgrade affects future shots only");
                Check(weapon.TryReload() && weapon.IsReloading && !weapon.TryFire(new Vector2(1002, 0), Vector2.right, proxy.transform), "reload blocks fire");
                weapon.CancelReload();
                Check(!weapon.IsReloading && weapon.Magazine == 11, "cancelled reload does not refill");
                Debug.Log("Progression runtime checks passed: " + assertions + " assertions.");
                return assertions + " runtime assertions passed; real save untouched.";
            }
            finally
            {
                foreach (CoinPickup coin in UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None))
                    if (coin.transform.position.x > 900) UnityEngine.Object.DestroyImmediate(coin.gameObject);
                foreach (CombatProjectile shot in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                    if (shot.transform.position.x > 900) UnityEngine.Object.DestroyImmediate(shot.gameObject);
                if (proxy != null) UnityEngine.Object.DestroyImmediate(proxy);
                if (enemy != null) UnityEngine.Object.DestroyImmediate(enemy);
                Directory.Delete(directory, true);
            }
        }
    }
}
