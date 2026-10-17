using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Checks loot against disposable profiles; never edits the player's inventory.</summary>
public static class LootDropChecks
{
    private static int assertions;

    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Botin: " + message);
    }

    [MenuItem("Tools/Lindavista/Verify loot storage")]
    public static void VerifyStorage() => RunRepository();

    public static string RunRepository()
    {
        assertions = 0;
        string directory = Path.Combine("Temp", "LootDropChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "profile.json");
            var repository = new ProgressionRepository(path);
            foreach (bool cesar in new[] { false, true })
            {
                string character = cesar ? "Cesar" : "Marco";
                for (int slot = 0; slot < 3; slot++)
                {
                    int other = repository.Municion(slot, !cesar);
                    int initial = repository.Municion(slot, cesar);
                    Check(repository.TryGrantAmmo(slot, ShopBalance.Pack[slot], cesar), character + " recibe cargador " + slot);
                    Check(repository.Municion(slot, cesar) == Math.Min(ShopBalance.CapacityFor(slot, cesar), initial + ShopBalance.Pack[slot]) &&
                        repository.Municion(slot, !cesar) == other && repository.Monedas == 0,
                        "municion gratuita pertenece al personaje y ranura correctos");
                    Check(repository.TryConsumeAmmo(slot, cesar), "hueco para verificar recogida parcial");
                    Check(repository.TryGrantAmmo(slot, int.MaxValue, cesar) &&
                        repository.Municion(slot, cesar) == ShopBalance.CapacityFor(slot, cesar),
                        "recogida parcial respeta capacidad sin desbordamiento " + slot);
                    Check(!repository.TryGrantAmmo(slot, 1, cesar) &&
                        repository.Municion(slot, cesar) == ShopBalance.CapacityFor(slot, cesar), "no recoge con reserva llena " + slot);
                }
                Check(!repository.TryGrantAmmo(-1, 1, cesar) && !repository.TryGrantAmmo(3, 1, cesar) &&
                    !repository.TryGrantAmmo(0, 0, cesar) && !repository.TryGrantAmmo(0, -1, cesar),
                    "indices y cantidades invalidos no conceden municion");
                int otherPills = repository.HealthConsumables(!cesar);
                for (int i = 0; i < ShopBalance.MaxHealthConsumables; i++)
                    Check(repository.TryGrantHealthConsumable(cesar), character + " recibe pastilla " + i);
                Check(repository.HealthConsumables(cesar) == ShopBalance.MaxHealthConsumables &&
                    repository.HealthConsumables(!cesar) == otherPills && repository.Monedas == 0 &&
                    !repository.TryGrantHealthConsumable(cesar), "pastillas gratuitas con inventarios independientes y limite");
            }
            var loaded = new ProgressionRepository(path);
            foreach (bool cesar in new[] { false, true })
            {
                for (int slot = 0; slot < 3; slot++)
                    Check(loaded.Municion(slot, cesar) == ShopBalance.CapacityFor(slot, cesar), "municion recogida persiste " + cesar + "/" + slot);
                Check(loaded.HealthConsumables(cesar) == ShopBalance.MaxHealthConsumables, "pastillas recogidas persisten");
            }

            string failingPath = Path.Combine(directory, "failure.json");
            var failing = new ProgressionRepository(failingPath);
            int ammo = failing.Municion(0, false);
            Directory.CreateDirectory(failingPath + ".tmp");
            Check(!failing.TryGrantAmmo(0, 1, false) && failing.Municion(0, false) == ammo,
                "fallo de disco no concede municion sin guardar");
            Check(!failing.TryGrantHealthConsumable(true) && failing.HealthConsumables(true) == 0,
                "fallo de disco no concede pastilla sin guardar");

            string deferredPath = Path.Combine(directory, "deferred.json");
            var deferred = new ProgressionRepository(deferredPath, true);
            Check(deferred.TryConsumeAmmo(0, false) && deferred.TryGrantAmmo(0, 1, false),
                "botin confirma tambien consumos de disparo pendientes");
            Check(new ProgressionRepository(deferredPath).Municion(0, false) == 48,
                "recoger tras disparar conserva la reserva real al recargar");
            return Report("BOTIN_GUARDADO_OK");
        }
        finally { Directory.Delete(directory, true); }
    }

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen)
            throw new InvalidOperationException("Ejecuta en una partida activa sin pausa y con la tienda cerrada.");
        assertions = 0;
        string directory = Path.Combine("Temp", "LootDropChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "runtime.json");
        var repository = new ProgressionRepository(path);
        var temporaryObjects = new List<GameObject>();
        var previousRandom = UnityEngine.Random.state;
        using (ProgressionService.UseVerificationRepository(repository))
        try
        {
            CheckSelection();
            CheckGround();
            UnityEngine.Random.InitState(85931);
            CheckEnemyDrop(temporaryObjects);
            foreach (bool cesar in new[] { false, true }) CheckCollection(cesar, temporaryObjects);
            CheckProximity(temporaryObjects);
            CheckExistingHudRefresh();
            var loaded = new ProgressionRepository(path);
            foreach (bool cesar in new[] { false, true })
            {
                Check(loaded.HealthConsumables(cesar) == 1, "recogida real de pastilla persiste para " + cesar);
                for (int slot = 0; slot < 3; slot++)
                    Check(loaded.Municion(slot, cesar) == repository.Municion(slot, cesar), "recogida real de municion persiste");
            }
            return Report("BOTIN_PARTIDA_OK");
        }
        finally
        {
            UnityEngine.Random.state = previousRandom;
            foreach (var go in temporaryObjects) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            Directory.Delete(directory, true);
        }
    }

    private static void CheckSelection()
    {
        var randomState = UnityEngine.Random.state;
        try
        {
            UnityEngine.Random.InitState(74103);
            foreach (bool cesar in new[] { false, true })
            {
                int seen = 0;
                for (int i = 0; i < 128; i++)
                {
                    int slot = EnemyLootDrop.SelectAmmoSlot(cesar);
                    Check(slot >= 0 && slot < 3 && ProgressionService.OwnsWeapon(slot, cesar),
                        "seleccion aleatoria solo utiliza armas disponibles del personaje");
                    seen |= 1 << slot;
                }
                for (int slot = 0; slot < 3; slot++)
                    if (ProgressionService.OwnsWeapon(slot, cesar))
                        Check((seen & (1 << slot)) != 0, "cada arma disponible puede aparecer en botin");
            }
            Check(EnemyLootDrop.SelectAmmoSlot(0) == -1, "sin armas disponibles no crea municion");
            int seenPistolAndRpg = 0;
            for (int i = 0; i < 128; i++)
            {
                Check(EnemyLootDrop.SelectAmmoSlot(1) == 0, "pistola sola nunca recibe municion bloqueada");
                int slot = EnemyLootDrop.SelectAmmoSlot(5);
                Check(slot == 0 || slot == 2, "pistola y RPG excluyen metralleta bloqueada");
                seenPistolAndRpg |= 1 << slot;
            }
            Check(seenPistolAndRpg == 5, "aleatoriedad permite ambas armas desbloqueadas");
        }
        finally { UnityEngine.Random.state = randomState; }
    }

    private static void CheckGround()
    {
        Vector3 low = LootPickupPlacement.PositionAtGround(new Vector3(1000f, -50f, 8f), -.4f);
        Vector3 high = LootPickupPlacement.PositionAtGround(new Vector3(1000f, 50f, -8f), .4f);
        Check(Mathf.Approximately(low.y, high.y) && low.z == 0f && high.z == 0f,
            "altura unica para botin sin depender de sprite, animacion o salto del enemigo");
        Check(Mathf.Approximately(low.x, 999.6f) && Mathf.Approximately(high.x, 1000.4f),
            "dispersion solamente horizontal conserva posiciones de las recompensas");
    }

    private static void CheckEnemyDrop(List<GameObject> temporaryObjects)
    {
        var existingCoins = new HashSet<CoinPickup>(UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None));
        var existingSupplies = new HashSet<SupplyPickup>(UnityEngine.Object.FindObjectsByType<SupplyPickup>(FindObjectsSortMode.None));
        var enemy = new GameObject("Enemy Loot Verification", typeof(SpriteRenderer), typeof(EnemyHealth));
        temporaryObjects.Add(enemy);
        enemy.transform.position = new Vector3(1000f, 10f, 5f);
        var loot = enemy.AddComponent<EnemyLootDrop>();
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(EnemyLootDrop).GetField("ammoDropChance", fields).SetValue(loot, 1f);
        typeof(EnemyLootDrop).GetField("healthDropChance", fields).SetValue(loot, 1f);
        var health = enemy.GetComponent<EnemyHealth>();
        health.TakeDamage(health.MaxHealth);
        int coinCount = 0, ammoCount = 0, pillCount = 0;
        float groundY = LootPickupPlacement.PositionAtGround(enemy.transform.position).y;
        foreach (var coin in UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None))
        {
            if (existingCoins.Contains(coin)) continue;
            temporaryObjects.Add(coin.gameObject);
            coinCount++;
            Check(Mathf.Approximately(coin.transform.position.y, groundY), "moneda de muerte en altura comun");
        }
        foreach (var supply in UnityEngine.Object.FindObjectsByType<SupplyPickup>(FindObjectsSortMode.None))
        {
            if (existingSupplies.Contains(supply)) continue;
            temporaryObjects.Add(supply.gameObject);
            if (supply.IsHealth) pillCount++; else ammoCount++;
            Check(Mathf.Approximately(supply.transform.position.y, groundY), "suministro de muerte en altura comun");
            Check(supply.IsHealth || ProgressionService.OwnsWeapon(supply.WeaponIndex, supply.IsCesar),
                "enemigo produce municion de un arma disponible del personaje destinatario");
        }
        Check(coinCount == 1 && ammoCount == 1 && pillCount == 1,
            "evento real de muerte conserva moneda y permite cargador mas pastilla");
        int coinsAfter = UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None).Length;
        int suppliesAfter = UnityEngine.Object.FindObjectsByType<SupplyPickup>(FindObjectsSortMode.None).Length;
        typeof(EnemyLootDrop).GetMethod("Drop", fields).Invoke(loot, new object[] { health });
        Check(UnityEngine.Object.FindObjectsByType<CoinPickup>(FindObjectsSortMode.None).Length == coinsAfter &&
            UnityEngine.Object.FindObjectsByType<SupplyPickup>(FindObjectsSortMode.None).Length == suppliesAfter,
            "repetir notificacion de muerte no duplica botin");
    }

    private static void CheckCollection(bool cesar, List<GameObject> temporaryObjects)
    {
        var playerObject = new GameObject("Player_" + (cesar ? "Cesar" : "Marco") + " Loot Verification");
        temporaryObjects.Add(playerObject);
        playerObject.transform.position = new Vector3(1000f, 0f, 0f);
        var player = playerObject.AddComponent<PlayerHealth>();
        var wrongCharacter = new GameObject("Player_" + (!cesar ? "Cesar" : "Marco") + " Wrong Loot Verification");
        temporaryObjects.Add(wrongCharacter);
        wrongCharacter.transform.position = new Vector3(1010f, 0f, 0f);
        var other = wrongCharacter.AddComponent<PlayerHealth>();
        Check(player.IsCesar == cesar && other.IsCesar != cesar, "identidad de personajes de verificacion");

        for (int slot = 0; slot < 3; slot++)
        {
            int before = ProgressionService.Ammo(slot, cesar);
            int otherAmmo = ProgressionService.Ammo(slot, !cesar);
            var pickup = NewSupply(temporaryObjects);
            pickup.InitializeAmmo(slot, cesar, ShopBalance.Pack[slot]);
            Check(pickup.WeaponIndex == slot && pickup.IsCesar == cesar && !pickup.IsHealth &&
                pickup.Amount == ShopBalance.Pack[slot], "recogible identifica municion y cantidad correctas");
            Check(pickup.GetComponent<SpriteRenderer>().sprite != null, "municion del personaje tiene sprite de recogible");
            Check(!pickup.TryCollect(null) && !pickup.TryCollect(other) && !pickup.IsCollected,
                "no recoge sin jugador ni entrega municion del otro personaje");
            playerObject.SetActive(false);
            Check(!pickup.TryCollect(player), "jugador inactivo no recoge");
            playerObject.SetActive(true);
            player.enabled = false;
            Check(!pickup.TryCollect(player), "componente de jugador desactivado no recoge");
            player.enabled = true;
            Check(pickup.TryCollect(player) && pickup.IsCollected &&
                ProgressionService.Ammo(slot, cesar) == Mathf.Min(ShopBalance.CapacityFor(slot, cesar), before + ShopBalance.Pack[slot]) &&
                ProgressionService.Ammo(slot, !cesar) == otherAmmo && ProgressionService.Coins == 0,
                "recogida entrega solo la reserva elegida sin cobrar");
            int after = ProgressionService.Ammo(slot, cesar);
            Check(!pickup.TryCollect(player) && ProgressionService.Ammo(slot, cesar) == after &&
                !pickup.GetComponent<CircleCollider2D>().enabled, "varios colliders no duplican recompensa");
        }

        var pill = NewSupply(temporaryObjects);
        pill.InitializeHealth(cesar);
        Check(pill.GetComponent<SpriteRenderer>().sprite != null, "pastilla tiene sprite de recogible");
        int health = player.CurrentHealth;
        int otherPills = ProgressionService.HealthConsumables(!cesar);
        Check(pill.IsHealth && pill.IsCesar == cesar && pill.TryCollect(player) &&
            ProgressionService.HealthConsumables(cesar) == 1 &&
            ProgressionService.HealthConsumables(!cesar) == otherPills && player.CurrentHealth == health,
            "pastilla se guarda en inventario aunque tenga vida llena");
        Check(!pill.TryCollect(player) && ProgressionService.HealthConsumables(cesar) == 1,
            "pastilla solo se concede una vez");

        ProgressionService.TryGrantAmmo(0, int.MaxValue, cesar);
        var full = NewSupply(temporaryObjects);
        full.InitializeAmmo(0, cesar, 1);
        Check(!full.TryCollect(player) && !full.IsCollected && full.GetComponent<CircleCollider2D>().enabled,
            "reserva llena conserva recogible disponible");
        Check(ProgressionService.TryConsumeAmmo(0, cesar) && full.TryCollect(player),
            "tras gastar municion se puede recoger el mismo objeto");

        var deadOnly = NewSupply(temporaryObjects);
        deadOnly.InitializeHealth(cesar);
        player.TakeDamage(player.MaxHealth);
        Check(player.IsDead && !deadOnly.TryCollect(player) && !deadOnly.IsCollected,
            "jugador muerto no consume recompensas");
    }

    private static void CheckProximity(List<GameObject> temporaryObjects)
    {
        const BindingFlags fields = BindingFlags.Static | BindingFlags.NonPublic;
        var playerCache = typeof(LootPickupPlacement).GetField("players", fields);
        var searchTime = typeof(LootPickupPlacement).GetField("nextPlayerSearch", fields);
        object previousPlayers = playerCache.GetValue(null);
        object previousSearch = searchTime.GetValue(null);
        try
        {
            var go = new GameObject("Player_Marco Narrow Loot Verification");
            temporaryObjects.Add(go);
            Vector3 ground = LootPickupPlacement.PositionAtGround(new Vector3(1200f, 0f, 0f));
            go.transform.position = ground + new Vector3(0f, .2f, 0f);
            var box = go.AddComponent<BoxCollider2D>();
            box.size = new Vector2(.1f, .4f);
            var player = go.AddComponent<PlayerHealth>();
            var pickup = NewSupply(temporaryObjects);
            pickup.InitializeAmmo(1, false, 1);
            pickup.transform.position = ground + new Vector3(.65f, 0f, 0f);
            Physics2D.SyncTransforms();
            searchTime.SetValue(null, 0f);
            Check(LootPickupPlacement.FindNearbyPlayer(pickup.transform.position) == player,
                "cuerpo estrecho recoge en borde del radio aunque no entre al centro del objeto");
            go.transform.position += Vector3.up * 3f;
            Physics2D.SyncTransforms();
            Check(LootPickupPlacement.FindNearbyPlayer(pickup.transform.position) == null,
                "compartir eje horizontal desde mucha altura no recoge");
            go.transform.position -= Vector3.up * 3f;
            Physics2D.SyncTransforms();
            int before = ProgressionService.Ammo(1, false);
            typeof(SupplyPickup).GetMethod("FixedUpdate", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(pickup, null);
            Check(pickup.IsCollected && ProgressionService.Ammo(1, false) == before + 1,
                "respaldo de proximidad recoge sin depender de callback fisico");
        }
        finally
        {
            playerCache.SetValue(null, previousPlayers);
            searchTime.SetValue(null, previousSearch);
        }
    }

    private static SupplyPickup NewSupply(List<GameObject> temporaryObjects)
    {
        var go = new GameObject("Supply Loot Verification");
        temporaryObjects.Add(go);
        go.transform.position = new Vector3(1000f, 0f, 0f);
        var pickup = go.AddComponent<SupplyPickup>();
        var circle = go.GetComponent<CircleCollider2D>();
        var body = go.GetComponent<Rigidbody2D>();
        Check(circle != null && circle.isTrigger && circle.radius >= .5f && body != null &&
            body.bodyType == RigidbodyType2D.Kinematic && body.gravityScale == 0f && go.layer == 2,
            "recogible con area amplia, sin gravedad y sin bloquear proyectiles");
        return pickup;
    }

    private static void CheckExistingHudRefresh()
    {
        // In an empty verification scene there is no UI to update. Never create or
        // replace a live HUD solely for a test, because its Awake owns a static instance.
        var hud = UnityEngine.Object.FindFirstObjectByType<WeaponSelectionHUD>();
        if (hud == null) return;
        const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
        var weapons = (PlayerWeaponController)typeof(WeaponSelectionHUD).GetField("weapons", fields).GetValue(hud);
        if (weapons == null) return;
        var counts = (Text[])typeof(WeaponSelectionHUD).GetField("ammoCounts", fields).GetValue(hud);
        int before = ProgressionService.Ammo(1, weapons.IsCesar);
        Check(ProgressionService.TryGrantAmmo(1, 1, weapons.IsCesar), "municion de prueba para evento de HUD");
        Check(counts[1].text == (before + 1).ToString(), "HUD actualiza reserva inmediatamente al recoger municion");
    }

    private static string Report(string prefix)
    {
        string result = prefix + ": " + assertions + " comprobaciones; perfil real intacto.";
        Debug.Log(result);
        return result;
    }
}
