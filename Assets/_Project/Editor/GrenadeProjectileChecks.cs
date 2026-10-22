using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated ballistic, fuse and blast checks; restores scenes and never touches the real profile.</summary>
[InitializeOnLoad]
public static class GrenadeProjectileChecks
{
    private const string Key = "GrenadeProjectileChecks";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> created = new List<GameObject>();
    private static readonly List<Object> resources = new List<Object>();
    private static IEnumerator routine;
    private static int assertions;
    private static double deadline;
    private static float previousScale;
    private static bool previousBackground;
    public static string Result => SessionState.GetString(Key + ".Result", "Not run");

    [Serializable] private class SavedScenes { public SavedScene[] scenes; }
    [Serializable] private class SavedScene { public string path; public bool loaded, active; }

    static GrenadeProjectileChecks() { EditorApplication.playModeStateChanged += OnPlayState; }

    [MenuItem("Lindavista/Verify grenade flight and fuse (isolated Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode before grenade checks.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                throw new Exception("Save open scenes before isolated grenade checks.");
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(new SavedScenes {
            scenes = setup.Select(s => new SavedScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray()
        }));
        SessionState.SetString(Key + ".Result", "Running");
        SessionState.SetBool(Key, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(Key, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            assertions = 0;
            previousScale = Time.timeScale;
            previousBackground = Application.runInBackground;
            Application.runInBackground = true;
            Time.timeScale = 1f;
            deadline = EditorApplication.timeSinceStartup + 35;
            routine = Verify();
            EditorApplication.update += Tick;
        }
        if (state == PlayModeStateChange.ExitingPlayMode)
        {
            EditorApplication.update -= Tick;
            Clear();
            Time.timeScale = previousScale;
            Application.runInBackground = previousBackground;
            routine = null;
        }
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Key, false);
            var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(Key + ".Scenes", ""));
            EditorSceneManager.RestoreSceneManagerSetup(saved.scenes.Select(s =>
                new SceneSetup { path = s.path, isLoaded = s.loaded, isActive = s.active }).ToArray());
            SessionState.EraseString(Key + ".Scenes");
        }
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Grenade checks timed out.");
            if (routine.MoveNext()) return;
            SessionState.SetString(Key + ".Result", "GRENADE_CHECKS_OK: " + assertions +
                " assertions; actual ammo art, player routing, separate ammo, arc, enemy contact detonation, painted and physical floors, bounce, 0.5s fuse, pause, walls, range, splash and duplicate hitboxes.");
            Debug.Log(Result);
        }
        catch (Exception error)
        {
            SessionState.SetString(Key + ".Result", "GRENADE_CHECKS_FAILED after " + assertions + ": " + error.Message);
            Debug.LogException(error);
            Debug.LogError(Result);
        }
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator Verify()
    {
        yield return null;
        VerifyPlayerShot();
        Clear();
        VerifyContactDetonation();
        Clear();
        VerifyArcFuseAndBlast();
        Clear();
        VerifyPhysicalFloor();
        Clear();
        VerifyWallAndRange();
        Clear();
        var left = Fire(new Vector2(-1f, 0.4f).normalized);
        Vector2 origin = left.transform.position;
        Step(left);
        Check(left.transform.position.x < origin.x && left.transform.position.y > origin.y, "left and upward aim controls launch");
        Check(Vector2.Dot(left.transform.right, new Vector2(-1f, 0.4f).normalized) > .999f, "root retains original aim for muzzle integration");
        Check(Vector2.Dot(left.transform.Find("Grenade ground shadow").right, Vector2.right) > .999f,
            "ground shadow stays horizontal for angled aim");
        Clear();
    }

    private static void VerifyPlayerShot()
    {
        Directory.CreateDirectory("Library/GrenadeProjectileChecks");
        string path = "Library/GrenadeProjectileChecks/profile-" + Guid.NewGuid().ToString("N") + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(new ProgressionProfile()));
        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path)))
            {
                var player = Make("Cesar grenade verification owner", Vector2.zero);
                var controller = player.AddComponent<PlayerWeaponController>();
                Check(controller.TryEquip(2), "Cesar equips grenade launcher");
                int cesarAmmo = ProgressionService.Ammo(2, true);
                int marcoAmmo = ProgressionService.Ammo(2, false);
                int magazine = controller.Magazine;
                Check(controller.TryFire(new Vector2(0f, 2f), Vector2.right, player.transform), "actual weapon controller fires");
                var grenade = Object.FindFirstObjectByType<GrenadeProjectile>();
                Check(grenade != null, "Cesar third weapon routes to grenade");
                created.Add(grenade.gameObject);
                grenade.enabled = false;
                Check(ProgressionService.Ammo(2, true) == cesarAmmo - 1 &&
                    ProgressionService.Ammo(2, false) == marcoAmmo && controller.Magazine == magazine - 1,
                    "one Cesar round and magazine round consumed without changing Marco ammo");
                Check(!controller.TryFire(Vector2.zero, Vector2.right, player.transform), "grenade cooldown applies");
                var renderer = grenade.GetComponentsInChildren<SpriteRenderer>().Single(r => r.name == "Grenade round");
                Check(renderer.sprite == CharacterWeapons.Artwork(true).AmmoAt(2), "round reuses actual grenade ammunition artwork");
                Check(AssetDatabase.GetAssetPath(renderer.sprite).EndsWith("Municion_lanzagranadas.png"), "round is not rocket artwork");
                Check(grenade.GetComponent<ShotVisual>() == null, "grenade has no rocket flight visual or exhaust");
                Check(Mathf.Abs(Mathf.Max(renderer.bounds.size.x, renderer.bounds.size.y) - GrenadeProjectile.BodyLength) < .002f,
                    "grenade body has its own readable world scale");
                Clear();
                var marco = Make("Marco rocket verification owner", Vector2.zero).AddComponent<PlayerWeaponController>();
                Check(marco.TryEquip(2) && marco.TryFire(new Vector2(0f, 2f), Vector2.right, marco.transform), "Marco still fires rocket");
                var rocket = Object.FindFirstObjectByType<CombatProjectile>();
                created.Add(rocket.gameObject);
                rocket.enabled = false;
                Check(rocket.GetComponent<GrenadeProjectile>() == null && rocket.GetComponent<ShotVisual>() != null,
                    "Marco keeps straight rocket implementation");
            }
        }
        finally { File.Delete(path); File.Delete(path + ".bak"); }
    }

    private static void VerifyContactDetonation()
    {
        EnemyHealth touching = Enemy(new Vector2(0.4f, 2.1f), Vector2.one * 1.4f);
        var duplicate = Make("duplicate contact hitbox", Vector2.zero);
        duplicate.transform.SetParent(touching.transform, false);
        duplicate.AddComponent<BoxCollider2D>().isTrigger = true;
        var grenade = Fire(Vector2.right);
        Physics2D.SyncTransforms();
        Step(grenade);
        Check(touching.CurrentHealth == 4, "contact detonates and direct/splash damage is applied once");
        Check(Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Length == 1,
            "enemy contact has a visible explosion");
        Step(grenade);
        Check(touching.CurrentHealth == 4, "spent contact grenade cannot damage twice");
    }

    private static void VerifyArcFuseAndBlast()
    {
        var grenade = Fire(Vector2.right);
        float startY = grenade.transform.position.y;
        Step(grenade);
        float firstRise = grenade.transform.position.y - startY;
        float previousY = grenade.transform.position.y;
        Step(grenade);
        Check(firstRise > 0f && grenade.transform.position.y - previousY < firstRise, "gravity produces curved rising launch");
        Check(!grenade.HasLanded, "missed grenade continues its arc before reaching floor");
        int steps = 0;
        while (!grenade.HasLanded && steps++ < 200) Step(grenade);
        Check(grenade.HasLanded && steps < 200, "grenade reaches painted floor without floor collider");
        Check(Mathf.Abs(grenade.FuseRemaining - 0.5f) < .001f, "0.5 second fuse starts at first floor contact");
        Check(Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Length == 0,
            "flight and first floor contact cause no damage or explosion");
        float landingY = grenade.transform.position.y;
        Step(grenade);
        Check(grenade.transform.position.y > landingY && !grenade.IsSettled, "floor contact produces one small bounce");
        steps = 0;
        while (!grenade.IsSettled && steps++ < 80) Step(grenade);
        Check(grenade.IsSettled && steps < 80 && grenade.FuseRemaining > 0f, "grenade settles before fuse expires");
        Vector2 position = grenade.transform.position;
        EnemyHealth exposed = Enemy(position + Vector2.left * .6f);
        var duplicate = Make("duplicate grenade target hitbox", Vector2.zero);
        duplicate.transform.SetParent(exposed.transform, false);
        duplicate.AddComponent<BoxCollider2D>().isTrigger = true;
        EnemyHealth behindWall = Enemy(position + Vector2.right * 1.8f);
        EnemyHealth distant = Enemy(position + Vector2.left * 4.5f);
        Solid(position + Vector2.right, new Vector2(.15f, 6f));
        float fuse = grenade.FuseRemaining;
        Time.timeScale = 0f;
        Step(grenade);
        Check(Mathf.Approximately(grenade.FuseRemaining, fuse) && (Vector2)grenade.transform.position == position,
            "pause freezes fuse and body");
        Time.timeScale = 1f;
        while (grenade.FuseRemaining > Time.fixedDeltaTime + .0001f) Step(grenade);
        Check(exposed.CurrentHealth == 10 && Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Length == 0,
            "settled grenade waits for entire fuse");
        Step(grenade);
        Check(exposed.CurrentHealth == 4, "delayed blast deals full damage exactly once across duplicate hitboxes");
        Check(behindWall.CurrentHealth == 10 && distant.CurrentHealth == 10, "blast respects solid walls and splash radius");
        var impact = Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None);
        Check(impact.Length == 1 && Vector2.Distance(impact[0].transform.position, position) < .001f,
            "detonation reuses rocket explosion at resting grenade");
        Step(grenade);
        Check(exposed.CurrentHealth == 4, "spent grenade cannot deal damage twice");
    }

    private static void VerifyPhysicalFloor()
    {
        var owner = Make("Painted foot line owner", Vector2.zero);
        var texture = new Texture2D(100, 100, TextureFormat.RGBA32, false);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 100, 100), Vector2.one * .5f, 10f);
        resources.Add(sprite);
        resources.Add(texture);
        owner.AddComponent<SpriteRenderer>().sprite = sprite;
        Solid(new Vector2(0f, 1f), new Vector2(100f, .2f));
        var grenade = Fire(Vector2.right, owner.transform);
        int steps = 0;
        while (!grenade.HasLanded && steps++ < 200) Step(grenade);
        Check(grenade.HasLanded && grenade.transform.position.y > 1.29f && grenade.transform.position.y < 1.32f,
            "physical floor is swept before lower painted foot line");
        Check(Mathf.Abs(grenade.FuseRemaining - GrenadeProjectile.FuseDuration) < .001f,
            "physical floor starts same delayed fuse");
    }

    private static void VerifyWallAndRange()
    {
        Solid(new Vector2(1f, 2f), new Vector2(.2f, 8f));
        var grenade = Fire(Vector2.right);
        for (int i = 0; i < 8; i++) Step(grenade);
        Check(grenade.transform.position.x < .71f && !grenade.HasLanded,
            "solid wall deflects round without starting floor fuse or exploding");
        Clear();
        var ranged = Fire(Vector2.right, null, .5f, 40f);
        int steps = 0;
        while (!ranged.IsSettled && steps++ < 200) Step(ranged);
        Check(ranged.IsSettled && ranged.transform.position.x <= .501f,
            "finite weapon range stops horizontal travel while allowing round to fall and settle");
    }

    private static GrenadeProjectile Fire(Vector2 direction, Transform owner = null, float range = 18f, float speed = 10f)
    {
        Check(CombatProjectile.TrySpawn(new Vector2(0f, 2f), direction, 6, speed, .2f, range, owner, 2, 1, true), "grenade spawns");
        var grenade = Object.FindFirstObjectByType<GrenadeProjectile>();
        created.Add(grenade.gameObject);
        grenade.enabled = false;
        return grenade;
    }

    private static GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private static EnemyHealth Enemy(Vector2 position, Vector2? size = null)
    {
        var go = Make("grenade verification enemy", position);
        go.SetActive(false);
        go.AddComponent<BoxCollider2D>().size = size ?? Vector2.one * .3f;
        var enemy = go.AddComponent<EnemyHealth>();
        typeof(EnemyHealth).GetField("maxHealth", Private).SetValue(enemy, 10);
        go.SetActive(true);
        return enemy;
    }

    private static void Solid(Vector2 position, Vector2 size) => Make("grenade verification solid", position).AddComponent<BoxCollider2D>().size = size;
    private static void Step(GrenadeProjectile grenade)
    {
        Physics2D.SyncTransforms();
        typeof(GrenadeProjectile).GetMethod("FixedUpdate", Private).Invoke(grenade, null);
    }
    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception("Grenade check failed: " + label);
    }
    private static void Clear()
    {
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        foreach (var effect in Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None)) Object.DestroyImmediate(effect.gameObject);
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)) Object.DestroyImmediate(effect.gameObject);
        foreach (var resource in resources) if (resource != null) Object.DestroyImmediate(resource);
        resources.Clear();
        Physics2D.SyncTransforms();
    }
}
