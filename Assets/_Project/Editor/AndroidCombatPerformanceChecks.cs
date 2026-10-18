using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Boss pooling and staged preparation regressions; never starts a match or edits saves.</summary>
public static class AndroidCombatPerformanceChecks
{
    private const BindingFlags InstanceFields = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticFields = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Vector2 Origin = new(30000f, 30000f);
    private static int assertions;
    private static object Pool => typeof(BossProjectile).GetField("pool", StaticFields).GetValue(null);
    private static HashSet<BossProjectile> Flying =>
        (HashSet<BossProjectile>)typeof(BossProjectile).GetField("flying", StaticFields).GetValue(null);
    private static int Available => ((ICollection)Pool.GetType().GetField("available", InstanceFields).GetValue(Pool)).Count;
    private static T Get<T>(object instance, string name) =>
        (T)instance.GetType().GetField(name, InstanceFields).GetValue(instance);
    private static void Call(object instance, string name) =>
        instance.GetType().GetMethod(name, InstanceFields).Invoke(instance, null);
    private static void Check(bool ok, string name)
    {
        assertions++;
        if (!ok) throw new Exception("Android combat performance: " + name);
    }

    [MenuItem("Tools/Lindavista/Verify Android combat performance (empty Play Mode scene)")]
    public static void Verify() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused ||
            PistolUpgradeShop.IsOpen || Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None).Length > 0 ||
            Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length > 0 ||
            Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Length > 0 || Flying.Count != 0)
            throw new InvalidOperationException("Use an empty Play Mode scene with no match or active projectiles.");

        assertions = 0;
        var scene = SceneManager.GetActiveScene();
        var originalRoots = new HashSet<GameObject>(scene.GetRootGameObjects());
        GameObject ownerObject = null;
        try
        {
            int before = BossObjects().Length;
            Check(Warmup(false) >= 6, "ordinary warmup yields between preparation stages");
            Check(BossObjects().Length == before, "earlier levels do not allocate boss rounds");
            Check(Warmup(true) >= 7, "boss preparation is a separate introduction frame");
            Check(Available >= 8 && Flying.Count == 0, "boss warmup prepares inactive rounds only");
            var prepared = new HashSet<BossProjectile>(BossObjects());
            Warmup(true);
            Check(prepared.SetEquals(BossObjects()), "repeated warmup creates no additional boss objects");
            foreach (BossProjectile shot in BossObjects())
                Check(!shot.gameObject.activeSelf && shot.GetComponent<SpriteRenderer>() != null &&
                    shot.GetComponent<TrailRenderer>() != null, "prewarmed round includes inactive flight visuals");

            ownerObject = new GameObject("Boss performance check owner");
            ownerObject.transform.position = Origin + Vector2.up * 100f;
            var owner = ownerObject.AddComponent<EnemyHealth>();
            BossProjectile previous = null;
            for (int i = 0; i < 64; i++)
            {
                Vector2 direction = i % 2 == 0 ? Vector2.right : Vector2.left;
                Check(BossProjectile.Fire(Origin, direction, 10f, i + 1, owner), "boss round accepted");
                var shot = Object.FindFirstObjectByType<BossProjectile>();
                Check(previous == null || shot == previous, "boss rounds reuse the returned object");
                Check(Get<Vector2>(shot, "direction") == direction && Get<int>(shot, "damage") == i + 1 &&
                    Mathf.Approximately(Get<float>(shot, "remaining"), 24f) && !Get<bool>(shot, "retired"),
                    "direction, damage, lifetime and retired state reset");
                Call(shot, "FixedUpdate");
                Check(Vector2.Distance(shot.transform.position, Origin) > 0f, "swept hostile round advances");
                BossProjectile.CancelFor(owner);
                Check(!shot.gameObject.activeSelf && Flying.Count == 0 && Get<EnemyHealth>(shot, "owner") == null,
                    "cancellation immediately returns round and releases owner");
                Call(shot, "FixedUpdate");
                Check(Flying.Count == 0, "retired round cannot return twice");
                ReturnParticles();
                previous = shot;
            }

            for (int i = 0; i < 16; i++)
                Check(BossProjectile.Fire(Origin, Vector2.right, 10f, 1, owner), "bounded burst accepted");
            Check(Flying.Count == 16 && !BossProjectile.Fire(Origin, Vector2.right, 10f, 1, owner),
                "sixteen-round safety budget enforced");
            BossProjectile.CancelFor(owner);
            Check(Flying.Count == 0 && Available >= 16, "full burst returns to the pool");

            Check(BossProjectile.Fire(Origin, Vector2.right, 10f, 1, owner), "orphan round emitted");
            var orphan = Object.FindFirstObjectByType<BossProjectile>();
            Object.DestroyImmediate(ownerObject);
            Call(orphan, "FixedUpdate");
            Check(Flying.Count == 0 && !orphan.gameObject.activeSelf, "destroyed owner retires its round");
            return "ANDROID_COMBAT_PERFORMANCE_OK: " + assertions +
                " assertions; staged warmup, idempotent pools, boss reuse, budget and lifecycle. Profile untouched.";
        }
        finally
        {
            foreach (var shot in Object.FindObjectsByType<BossProjectile>(FindObjectsSortMode.None)) Call(shot, "Retire");
            ReturnParticles();
            if (ownerObject != null) Object.DestroyImmediate(ownerObject);
            foreach (var root in scene.GetRootGameObjects())
                if (!originalRoots.Contains(root)) Object.DestroyImmediate(root);
        }
    }

    private static int Warmup(bool boss)
    {
        Type type = typeof(BossProjectile).Assembly.GetType("CombatWarmup");
        var routine = (IEnumerator)type.GetMethod("Prepare", StaticFields).Invoke(null, new object[] { boss });
        int frames = 0;
        while (routine.MoveNext()) frames++;
        return frames;
    }

    private static BossProjectile[] BossObjects() =>
        Object.FindObjectsByType<BossProjectile>(FindObjectsInactive.Include, FindObjectsSortMode.None);

    private static void ReturnParticles()
    {
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)) Call(effect, "Devolver");
    }
}
