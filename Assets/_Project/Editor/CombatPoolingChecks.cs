using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Pool lifecycle regressions; run in an empty Play Mode scene, never a match.</summary>
public static class CombatPoolingChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> created = new();
    private static int assertions;
    private static readonly Vector2 Origin = new(30000f, 30000f);
    private static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, Private).GetValue(instance);
    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Private).SetValue(instance, value);
    private static object Call(object instance, string method, params object[] args) => instance.GetType().GetMethod(method, Private).Invoke(instance, args);
    private static int Budget => (int)typeof(CombatProjectile).GetField("activeProjectiles", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception("Combat pooling: " + label);
    }

    [MenuItem("Tools/Lindavista/Verify projectile pools (empty Play Mode scene)")]
    public static void Run() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen ||
            Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None).Length > 0 ||
            Object.FindObjectsByType<BasicEnemy>(FindObjectsSortMode.None).Length > 0 || Budget != 0)
            throw new InvalidOperationException("Use an empty Play Mode scene with no active match/projectiles.");
        assertions = 0;
        try
        {
            for (int mode = 0; mode < 4; mode++)
            {
                CombatProjectile previous = null;
                for (int i = 0; i < 128; i++)
                {
                    Vector2 direction = i % 2 == 0 ? Vector2.right : Vector2.left;
                    var shot = Fire(mode, direction, i + 1);
                    if (previous != null) Check(shot == previous, "same object reused for each weapon mode");
                    Check(!Get<bool>(shot, "spent") && shot.Damage == i + 1 &&
                        Get<Vector2>(shot, "direction") == direction && !Get<bool>(shot, "hasGround"),
                        "damage, direction, ground and spent state reset");
                    Check(Get<Camera>(shot, "flightCamera") == null && shot.enabled && Budget == 1,
                        "camera, callbacks and budget reset");
                    Call(shot, "FixedUpdate");
                    Check(!shot.gameObject.activeSelf && Get<bool>(shot, "spent") && Budget == 0,
                        "range expiration returns round and frees budget immediately");
                    Call(shot, "FixedUpdate");
                    Check(Budget == 0, "spent round cannot be returned/count decremented twice");
                    if (mode == 3)
                    {
                        Check(Get<HashSet<EnemyHealth>>(shot, "piercedEnemies").Count == 0,
                            "sniper does not retain earlier victims");
                        foreach (var tracer in Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None))
                        {
                            Set(tracer, "fadeAge", 1f);
                            Call(tracer, "Update");
                        }
                    }
                    else Check(shot.GetComponent<TrailRenderer>() == null || shot.GetComponent<TrailRenderer>().positionCount == 0,
                        "retired round has no stale trail");
                    ReturnParticles();
                    previous = shot;
                }
            }

            var live = new List<CombatProjectile>();
            for (int i = 0; i < 64; i++) live.Add(Fire(0, Vector2.right));
            Check(Budget == 64 && !CombatProjectile.TrySpawn(Origin, Vector2.right, 1, 500f, .07f, .1f, null),
                "64 live rounds still enforce shared budget");
            foreach (var shot in live) Call(shot, "FixedUpdate");
            Check(Budget == 0, "returning full batch restores budget");
            var destroyed = Fire(0, Vector2.right);
            Object.DestroyImmediate(destroyed.gameObject);
            Check(Budget == 0, "external destruction releases budget exactly once");
            var afterDestroyed = Fire(0, Vector2.right);
            Check(afterDestroyed != null, "destroyed entries cannot poison pool");
            Call(afterDestroyed, "FixedUpdate");

            var tracer1 = SniperShotVisual.Create(Origin, Vector2.right);
            tracer1.DrawTo(Origin + Vector2.right * 4f);
            tracer1.Release(); Set(tracer1, "fadeAge", 1f); Call(tracer1, "Update");
            var tracer2 = SniperShotVisual.Create(Origin + Vector2.up, Vector2.left);
            Check(tracer2 == tracer1 && !Get<bool>(tracer2, "released") && Get<float>(tracer2, "fadeAge") == 0f &&
                tracer2.GetComponent<SpriteRenderer>().color == Color.white && !tracer2.GetComponent<SpriteRenderer>().enabled,
                "sniper reuse resets direction, fade and visibility");
            tracer2.Release(); Set(tracer2, "fadeAge", 1f); Call(tracer2, "Update");

            var explosion1 = RocketImpact.Create(Origin);
            Set(explosion1, "age", 1f); Call(explosion1, "Update");
            var explosion2 = RocketImpact.Create(Origin + Vector2.right);
            Check(explosion2 == explosion1 && Get<float>(explosion2, "age") == 0f &&
                explosion2.GetComponent<SpriteRenderer>().sprite == RocketImpact.GetFrame(0) &&
                explosion2.GetComponent<SpriteRenderer>().color == Color.white,
                "explosion reuse resets first frame, lifetime and opacity");
            Set(explosion2, "age", 1f); Call(explosion2, "Update");

            ShotVisual.EnemyShot(Origin, Vector2.right, 0f, 0, false);
            var visual1 = Object.FindFirstObjectByType<ShotVisual>();
            Call(visual1, "Update");
            ShotVisual.EnemyShot(Origin + Vector2.up, Vector2.left, 0f, 1, false);
            var visual2 = Object.FindFirstObjectByType<ShotVisual>();
            Check(visual1 == visual2 && visual2.transform.position == (Vector3)(Origin + Vector2.up) &&
                Get<Vector2>(visual2, "flightDirection") == Vector2.left && Get<int>(visual2, "weapon") == 1,
                "enemy visual reused across pistol/SMG without old flight data");
            Call(visual2, "Update");

            Check(CombatProjectile.TrySpawn(Origin, Vector2.right, 3, 10f, .2f, 18f, null, 2, 1, true), "grenade emits");
            var grenade = Object.FindFirstObjectByType<GrenadeProjectile>();
            created.Add(grenade.gameObject);
            Check(!grenade.GetComponent<CombatProjectile>().enabled && Budget == 1, "disabled straight-flight component still counts ballistic grenade");
            Object.DestroyImmediate(grenade.gameObject);
            Check(Budget == 0, "ballistic destruction frees shared budget");

            var cameraObject = new GameObject("Pool camera check"); created.Add(cameraObject);
            var camera = cameraObject.AddComponent<Camera>();
            camera.transform.position = new Vector3(Origin.x, Origin.y, -10f);
            camera.orthographic = true; camera.orthographicSize = 5f; camera.aspect = 1f;
            Check(CombatProjectile.TrySpawn(Origin, Vector2.right, 3, 1f, .07f, .1f, null, 0, 1, false, camera), "view-bound round emits");
            var viewShot = Object.FindFirstObjectByType<CombatProjectile>();
            Check(Mathf.Abs((float)Call(viewShot, "DistanceToCameraEdge", Origin) - 5f) < .01f, "camera edge is correct");
            camera.transform.position += Vector3.right * 2f;
            Check(Mathf.Abs((float)Call(viewShot, "DistanceToCameraEdge", Origin) - 7f) < .01f, "cached frustum updates immediately on movement");
            camera.orthographicSize = 3f;
            Check(Mathf.Abs((float)Call(viewShot, "DistanceToCameraEdge", Origin) - 5f) < .01f, "cached frustum updates immediately on zoom");
            Object.DestroyImmediate(viewShot.gameObject);
            Check(Budget == 0, "no leaked rounds after lifecycle checks");
            return "COMBAT_POOLS_OK: " + assertions + " assertions; reuse, full budget, destruction, grenades, trails, fade and moving camera.";
        }
        finally
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            created.Clear();
            foreach (var shot in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)) Object.DestroyImmediate(shot.gameObject);
            ReturnParticles();
        }
    }

    private static CombatProjectile Fire(int mode, Vector2 direction, int damage = 3)
    {
        var before = new HashSet<CombatProjectile>(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
        Check(CombatProjectile.TrySpawn(Origin, direction, damage, 500f, .07f, .1f, null,
            mode == 3 ? 1 : mode, mode == 3 ? 4 : 1), "round accepted");
        foreach (var shot in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
            if (!before.Contains(shot)) return shot;
        throw new Exception("New active round not found");
    }

    private static void ReturnParticles()
    {
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)) Call(effect, "Devolver");
    }
}
