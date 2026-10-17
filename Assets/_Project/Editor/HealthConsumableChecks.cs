using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Economy, save compatibility, animation and healing checks on isolated inventories.</summary>
public static class HealthConsumableChecks
{
    private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;
    private static int assertions;

    private static void Check(bool value, string message)
    {
        assertions++;
        if (!value) throw new InvalidOperationException("Pastillas: " + message);
    }

    public static string RunRepository()
    {
        assertions = 0;
        string directory = Path.Combine("Temp", "HealthConsumableChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            string path = Path.Combine(directory, "profile.json");
            var repository = new ProgressionRepository(path);
            Check(!repository.TryBuyHealthConsumable(false) && repository.HealthConsumables(false) == 0,
                "una compra sin saldo no concede pastillas");
            Check(!repository.TryConsumeHealthConsumable(false), "no consumir inventario vacío");
            Check(repository.TryCredit(1000), "saldo de prueba");
            for (int i = 0; i < 10; i++) Check(repository.TryBuyHealthConsumable(false), "compra unitaria " + i);
            int balance = repository.Monedas;
            Check(!repository.TryBuyHealthConsumable(false) && repository.Monedas == balance &&
                repository.HealthConsumables(false) == 10, "tope de diez sin cobro adicional");
            Check(balance == 1000 - 10 * ShopBalance.HealthConsumableCost, "precio por unidad");
            Check(repository.HealthConsumables(true) == 0 && repository.TryBuyHealthConsumable(true),
                "inventarios independientes por personaje");
            Check(repository.TryConsumeHealthConsumable(false), "consumo guardado");
            var loaded = new ProgressionRepository(path);
            Check(loaded.HealthConsumables(false) == 9 && loaded.HealthConsumables(true) == 1,
                "compras y consumo sobreviven a la recarga del archivo");
            Check(loaded.TryBuyHealthConsumable(false), "se puede reponer la unidad gastada");

            string legacy = Path.Combine(directory, "old-v2.json");
            string json = JsonUtility.ToJson(new ProgressionProfile { coins = 42 });
            json = json.Replace("\"healthConsumables\":0,", "");
            File.WriteAllText(legacy, json);
            var old = new ProgressionRepository(legacy);
            Check(old.CanSave && old.Monedas == 42 && old.HealthConsumables(false) == 0 &&
                old.HealthConsumables(true) == 0, "perfil anterior conserva monedas y empieza sin pastillas");

            string failingPath = Path.Combine(directory, "failure.json");
            var failing = new ProgressionRepository(failingPath);
            Check(failing.TryCredit(100) && failing.TryBuyHealthConsumable(false), "preparación del fallo de disco");
            Directory.CreateDirectory(failingPath + ".tmp");
            Check(!failing.TryBuyHealthConsumable(false) && failing.Monedas == 85 && failing.HealthConsumables(false) == 1,
                "fallo de guardado no cobra ni entrega inventario");
            Check(!failing.TryConsumeHealthConsumable(false) && failing.HealthConsumables(false) == 1,
                "fallo de guardado no elimina pastillas");

            string invalid = Path.Combine(directory, "invalid.json");
            var invalidProfile = new ProgressionProfile();
            invalidProfile.marco.healthConsumables = 11;
            File.WriteAllText(invalid, JsonUtility.ToJson(invalidProfile));
            Check(!new ProgressionRepository(invalid).CanSave, "inventario corrupto no se acepta ni sobrescribe");
            return Report("PASTILLAS_GUARDADO_OK");
        }
        finally { Directory.Delete(directory, true); }
    }

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || PistolUpgradeShop.IsOpen)
            throw new InvalidOperationException("Ejecuta en una partida activa con la tienda cerrada.");
        assertions = 0;
        string directory = Path.Combine("Temp", "HealthConsumableChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var repository = new ProgressionRepository(Path.Combine(directory, "runtime.json"));
        repository.TryCredit(2000);
        using (ProgressionService.UseVerificationRepository(repository))
        try
        {
            foreach (bool cesar in new[] { false, true }) CheckCharacter(cesar);
            CheckShop();
            return Report("PASTILLAS_PARTIDA_OK");
        }
        finally { Directory.Delete(directory, true); }
    }

    private static void CheckCharacter(bool cesar)
    {
        string character = cesar ? "Cesar" : "Marco";
        var go = new GameObject("Player_" + character + " Verification", typeof(SpriteRenderer), typeof(Animator), typeof(Rigidbody2D));
        go.transform.position = new Vector3(1000f, 0f, 0f);
        go.GetComponent<Rigidbody2D>().gravityScale = 0f;
        var animator = go.GetComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Project/Animations/Player_" + character + ".controller");
        go.AddComponent<PlayerMovement>();
        var actions = go.AddComponent<PlayerActions>();
        try
        {
            Check(ProgressionService.TryBuyHealthConsumable(cesar), character + " compra de prueba");
            actions.Health.Initialize(actions.MaxHealth, 0f);
            Check(!actions.TryUseHealthConsumable() && repositoryCount(cesar) == 1, "vida llena no gasta");
            actions.Health.TakeDamage(2);
            Idle(animator, character);
            int hurt = actions.CurrentHealth;
            int maximum = actions.MaxHealth;
            Check(actions.TryUseHealthConsumable(), character + " inicia curación");
            Check(!actions.TryUseHealthConsumable(), "no se solapan consumos");
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName(character + "_UseHealthConsumable"), "reproduce el clip correcto");
            int weapon = actions.Weapons.EquippedWeapon;
            actions.EquipWeapon((weapon + 1) % 3);
            actions.ReloadTouchWeapon();
            Check(actions.Weapons.EquippedWeapon == weapon && !actions.Weapons.IsReloading, "bloquea cambio de arma y recarga");
            Advance(actions, .30f);
            Check(actions.CurrentHealth == hurt && repositoryCount(cesar) == 1, "antes de tragar no hay curación ni gasto");
            actions.Health.TakeDamage(1);
            Check(!actions.IsUsingHealthConsumable && repositoryCount(cesar) == 1, "daño interrumpe sin gastar");
            Idle(animator, character);
            Check(actions.TryUseHealthConsumable(), "puede volver a usar tras interrupción");
            hurt = actions.CurrentHealth;
            Advance(actions, .80f);
            Check(actions.CurrentHealth == hurt + 1 && actions.MaxHealth == maximum && repositoryCount(cesar) == 0,
                "recupera exactamente un punto sin aumentar la vida máxima");
            Advance(actions, .50f);
            Check(!actions.IsUsingHealthConsumable && actions.CurrentHealth == hurt + 1 && !actions.TryUseHealthConsumable(),
                "termina, vuelve al reposo y no permite consumir inventario vacío");
            var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == character + "_UseHealthConsumable");
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).Single();
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Check(keys.Select(k => k.value).Distinct().Count() == 8 && !AnimationUtility.GetAnimationClipSettings(clip).loopTime,
                "ocho fotogramas existentes, sin bucle");
            foreach (var key in keys)
            {
                clip.SampleAnimation(go, key.time);
                Check(go.GetComponent<SpriteRenderer>().sprite == key.value, "fotograma enlazado al SpriteRenderer");
                var sprite = (Sprite)key.value;
                Check(sprite.bounds.min.y >= -2.8f && sprite.bounds.min.y <= -2.4f, "pies alineados con el cuerpo de partida");
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static int repositoryCount(bool cesar) => ProgressionService.HealthConsumables(cesar);
    private static void Advance(PlayerActions actions, float delta) => typeof(PlayerActions)
        .GetMethod("AdvanceHealthConsumable", Private).Invoke(actions, new object[] { delta });
    private static void Idle(Animator animator, string character)
    {
        animator.ResetTrigger("TakeDamage");
        animator.Play(character + "_Idle", 0, 0f);
        animator.Update(0f);
    }

    private static void CheckShop()
    {
        PistolUpgradeShop.Open();
        var shop = UnityEngine.Object.FindFirstObjectByType<PistolUpgradeShop>();
        try
        {
            var tabs = (Button[])typeof(PistolUpgradeShop).GetField("characterTabs", Private).GetValue(shop);
            tabs[0].onClick.Invoke();
            var buy = (Button)typeof(PistolUpgradeShop).GetField("consumableBuy", Private).GetValue(shop);
            int balance = ProgressionService.Coins;
            buy.onClick.Invoke();
            Check(repositoryCount(false) == 1 && repositoryCount(true) == 0 &&
                ProgressionService.Coins == balance - ShopBalance.HealthConsumableCost, "botón real compra una unidad para Marco");
            for (int i = 1; i < 10; i++) buy.onClick.Invoke();
            Check(!buy.interactable && buy.GetComponentInChildren<Text>().text.Contains("10"), "botón desactivado al límite");
            tabs[1].onClick.Invoke();
            Check(buy.interactable && repositoryCount(true) == 0, "César conserva inventario propio");
            Check(HealthConsumableAssets.Load().icon != null, "sprite del usuario disponible para tienda y HUD");
        }
        finally { UnityEngine.Object.DestroyImmediate(shop.gameObject); }
    }

    private static string Report(string prefix)
    {
        string result = prefix + ": " + assertions + " comprobaciones.";
        Debug.Log(result);
        return result;
    }
}
