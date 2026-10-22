using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Isolated rocket collision and flipbook checks; never saves a gameplay scene or the real profile.</summary>
[InitializeOnLoad]
public static class RpgImpactChecks
{
    private const string RunningKey = "RpgImpactChecks.Running";
    private const string BatchKey = "RpgImpactChecks.Batch";
    private const string SceneKey = "RpgImpactChecks.Scenes";
    private const string ResultKey = "RpgImpactChecks.Result";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> created = new List<GameObject>();
    private static IEnumerator routine;
    private static IDisposable profileScope;
    private static string directory;
    private static int assertions;
    private static double deadline;
    private static float previousScale;
    private static bool previousBackground;

    [Serializable] private class SavedScenes { public SavedScene[] scenes; }
    [Serializable] private class SavedScene { public string path; public bool loaded; public bool active; }

    static RpgImpactChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
        if (SessionState.GetBool(RunningKey, false))
            EditorApplication.delayCall += Resume;
    }

    /// <summary>Unity -batchmode -projectPath ... -executeMethod RpgImpactChecks.RunBatch -logFile ...; omit -quit.</summary>
    public static void RunBatch() { Start(true); }

    [MenuItem("Lindavista/Verify RPG impact (isolated Play Mode)")]
    public static void Run() { Start(false); }

    private static void Start(bool batch)
    {
        if (SessionState.GetBool(RunningKey, false) || EditorApplication.isPlayingOrWillChangePlaymode)
            throw new Exception("Stop Play Mode before starting isolated RPG checks.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        if (!batch)
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (scene.isDirty || string.IsNullOrEmpty(scene.path))
                    throw new Exception("Save open scenes before running RPG checks so their setup can be restored.");
            }
        var saved = new SavedScenes { scenes = new SavedScene[setup.Length] };
        for (int i = 0; i < setup.Length; i++)
            saved.scenes[i] = new SavedScene { path = setup[i].path, loaded = setup[i].isLoaded, active = setup[i].isActive };
        SessionState.SetString(SceneKey, JsonUtility.ToJson(saved));
        SessionState.SetBool(BatchKey, batch);
        SessionState.SetInt(ResultKey, 1);
        SessionState.SetBool(RunningKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.isPlaying = true;
    }

    private static void Resume()
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (EditorApplication.isPlaying) Begin();
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) RestoreAndExit();
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(RunningKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode) Begin();
        if (state == PlayModeStateChange.ExitingPlayMode) Cleanup();
        if (state == PlayModeStateChange.EnteredEditMode) RestoreAndExit();
    }

    private static void Begin()
    {
        if (routine != null) return;
        previousScale = Time.timeScale;
        previousBackground = Application.runInBackground;
        Time.timeScale = 1f;
        Application.runInBackground = true;
        // Unity deletes Temp when the batch editor exits; retain the preview in its ignored cache.
        directory = Path.GetFullPath(Path.Combine("Library", "RpgImpactChecks"));
        Directory.CreateDirectory(directory);
        string profilePath = Path.Combine(directory, "profile-" + Guid.NewGuid().ToString("N") + ".json");
        var profile = new ProgressionProfile();
        profile.marco.ammunition = new[] { 60, 180, 12 };
        File.WriteAllText(profilePath, JsonUtility.ToJson(profile));
        profileScope = ProgressionService.UseVerificationRepository(new ProgressionRepository(profilePath));
        assertions = 0;
        deadline = EditorApplication.timeSinceStartup + 45;
        routine = Verify();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("RPG checks timed out waiting for Play Mode frames.");
            if (routine.MoveNext()) return;
            Debug.Log("RPG_IMPACT_CHECKS_OK: " + assertions + " assertions; direct and splash damage, duplicate colliders, walls, physical and painted floor, nose contact, bullet isolation, range expiry, nine transparent frames, fade, pause and lifetime. Preview: " + Path.Combine(directory, "preview.png"));
            Finish(0);
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Debug.LogError("RPG_IMPACT_CHECKS_FAILED after " + assertions + " assertions.");
            Finish(1);
        }
    }

    private static IEnumerator Verify()
    {
        yield return null; // Allow runtime reset hooks and the first Play Mode frame to finish.
        Check(!GameFlowController.IsMatchEnded, "isolated scene has no ended match");
        Check(ProgressionService.Ammo(2, false) == 12, "temporary profile is active");
        VerifyAtlas();
        VerifyEnemyImpact();
        ClearObjects();
        VerifyPointBlankImpact();
        ClearObjects();
        VerifyWallImpact();
        ClearObjects();
        VerifyPhysicalFloor();
        ClearObjects();
        VerifyPaintedFloor();
        ClearObjects();
        VerifyGroundOrdering(true);
        ClearObjects();
        VerifyGroundOrdering(false);
        ClearObjects();
        VerifyBulletsAndExpiry();
        ClearObjects();
        VerifyFrames();
        ClearObjects();

        RocketImpact effect = RocketImpact.Create(Vector2.zero);
        Check(effect != null, "timed impact creates");
        created.Add(effect.gameObject);
        var renderer = effect.GetComponent<SpriteRenderer>();
        float started = Time.time;
        while (Time.time < started + .16f) yield return null;
        Sprite pausedFrame = renderer.sprite;
        float pausedAlpha = renderer.color.a;
        float pausedAge = (float)typeof(RocketImpact).GetField("age", Private).GetValue(effect);
        Time.timeScale = 0f;
        double until = EditorApplication.timeSinceStartup + .12;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(effect != null && renderer.sprite == pausedFrame && Mathf.Approximately(renderer.color.a, pausedAlpha), "pause freezes frame and alpha");
        Check(Mathf.Abs((float)typeof(RocketImpact).GetField("age", Private).GetValue(effect) - pausedAge) < .001f, "pause freezes impact lifetime");
        Time.timeScale = 1f;
        while (Time.time < started + RocketImpact.Duration + .08f) yield return null;
        Check(effect == null, "impact destroys after its scaled lifetime");
        ClearObjects();

        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
        {
            Debug.Log("RPG preview skipped: batch renderer has no graphics device.");
            yield break;
        }
        // A real camera rendering exercises sprite material, transparent edges and fixed world scale.
        float previewSpacing = RocketImpact.Width + .2f;
        var cameraObject = Make("RPG preview camera", new Vector2(previewSpacing, -previewSpacing));
        cameraObject.transform.position += Vector3.back * 10f;
        var camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = previewSpacing * 1.6f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.22f, .25f, .22f, 1f);
        var target = new RenderTexture(900, 900, 24);
        camera.targetTexture = target;
        for (int i = 0; i < RocketImpact.FrameCount; i++)
        {
            var go = Make("Preview frame " + i, new Vector2(i % 3 * previewSpacing, -(i / 3) * previewSpacing));
            var sprite = go.AddComponent<SpriteRenderer>();
            sprite.sprite = RocketImpact.GetFrame(i);
            go.transform.localScale = Vector3.one * RocketImpact.Width / sprite.sprite.bounds.size.x;
        }
        until = EditorApplication.timeSinceStartup + .15;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        var oldTarget = RenderTexture.active;
        var capture = new Texture2D(900, 900, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, 900, 900), 0, 0);
            capture.Apply();
            File.WriteAllBytes(Path.Combine(directory, "preview.png"), capture.EncodeToPNG());
            Check(File.Exists(Path.Combine(directory, "preview.png")), "camera preview exported");
        }
        finally
        {
            RenderTexture.active = oldTarget;
            camera.targetTexture = null;
            target.Release();
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(capture);
        }
    }

    private static void VerifyAtlas()
    {
        var atlas = Resources.Load<Texture2D>("RpgImpactAtlas");
        Check(atlas != null && atlas.width % 3 == 0 && atlas.height % 3 == 0, "3 by 3 atlas loads from Resources");
        string path = AssetDatabase.GetAssetPath(atlas);
        var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Check(readable.LoadImage(File.ReadAllBytes(path)), "PNG source decodes");
            Color32[] pixels = readable.GetPixels32();
            int cellWidth = readable.width / 3, cellHeight = readable.height / 3;
            var hashes = new HashSet<long>();
            for (int i = 0; i < RocketImpact.FrameCount; i++)
            {
                Sprite frame = RocketImpact.GetFrame(i);
                Check(frame != null && frame.texture == atlas, "frame " + i + " uses generated atlas");
                Check(frame.rect.x == i % 3 * cellWidth && frame.rect.y == (2 - i / 3) * cellHeight,
                    "frame " + i + " uses left-to-right, top-to-bottom ordering");
                int count = 0;
                long hash = 17;
                for (int y = 0; y < cellHeight; y++)
                    for (int x = 0; x < cellWidth; x++)
                    {
                        Color32 pixel = pixels[((2 - i / 3) * cellHeight + y) * readable.width + i % 3 * cellWidth + x];
                        if (pixel.a > 24) count++;
                        unchecked { hash = hash * 31 + pixel.a + pixel.r * 3 + pixel.g * 5 + pixel.b * 7; }
                    }
                Check(count > cellWidth * cellHeight * .002f && count < cellWidth * cellHeight * .85f,
                    "frame " + i + " contains visible art and transparent canvas");
                Check(hashes.Add(hash), "frame " + i + " differs from previous frames");
                Check(frame.pivot == new Vector2(cellWidth * .5f, cellHeight * .5f), "frame " + i + " keeps a fixed center pivot");
            }
        }
        finally { Object.DestroyImmediate(readable); }
    }

    private static void VerifyEnemyImpact()
    {
        EnemyHealth direct = Enemy(new Vector2(3f, 0f), new Vector2(.5f, .5f));
        DuplicateCollider(direct, new Vector2(0f, .15f));
        EnemyHealth nearby = Enemy(new Vector2(3.05f, .6f), Vector2.one * .3f);
        DuplicateCollider(nearby, new Vector2(0f, .03f));
        EnemyHealth fartherInGroup = Enemy(new Vector2(5f, 0f), Vector2.one * .3f);
        DuplicateCollider(fartherInGroup, new Vector2(0f, .03f));
        EnemyHealth overlapping = Enemy(new Vector2(3.05f, 0f), Vector2.one * .3f);
        EnemyHealth far = Enemy(new Vector2(7f, 0f), Vector2.one * .3f);
        var shot = Fire(Vector2.zero, Vector2.right, 2);
        Check(Mathf.Abs(shot.GetComponent<SpriteRenderer>().bounds.size.x - 1.96f) < .002f, "rocket art is enlarged to 1.96 world units");
        Step(shot);
        Check(direct.CurrentHealth == 4, "direct enemy receives full six damage once");
        Check(nearby.CurrentHealth == 4, "nearby enemy receives full damage once across child colliders");
        Check(fartherInGroup.CurrentHealth == 4, "enemy beyond the old splash radius receives full damage once");
        Check(overlapping.CurrentHealth == 4, "overlapping enemy also receives full explosion damage");
        Check(far.CurrentHealth == 10, "enemy outside splash radius stays unharmed");
        RocketImpact impact = OneImpact();
        Check(Vector2.Distance(impact.transform.position, new Vector2(2.63f, 0f)) < .01f, "impact is anchored to the enemy surface");
        Check(Mathf.Abs(shot.transform.position.x + ShotVisual.RocketWidth - 2.75f) < .01f, "rocket nose stops at the enemy surface");
        Step(shot);
        Check(direct.CurrentHealth == 4 && nearby.CurrentHealth == 4 &&
            fartherInGroup.CurrentHealth == 4 && overlapping.CurrentHealth == 4, "spent shot cannot damage the group twice");
    }

    private static void VerifyWallImpact()
    {
        Solid(new Vector2(3f, 0f), new Vector2(.4f, 4f));
        EnemyHealth front = Enemy(new Vector2(2.4f, .7f), Vector2.one * .3f);
        EnemyHealth behind = Enemy(new Vector2(3.5f, 0f), Vector2.one * .3f);
        var shot = Fire(Vector2.zero, Vector2.right, 2);
        Step(shot);
        Check(front.CurrentHealth == 4, "wall detonation damages a nearby enemy on the exposed side");
        Check(behind.CurrentHealth == 10, "solid wall blocks splash reaching an enemy behind it");
        Check(Vector2.Distance(OneImpact().transform.position, new Vector2(2.68f, 0f)) < .01f, "wall detonation appears at its visible surface");
    }

    private static void VerifyPointBlankImpact()
    {
        // Both targets start inside the newly emitted rocket's visible length. The
        // closer target must receive direct damage even when the nose is past it.
        EnemyHealth closest = Enemy(new Vector2(.6f, 0f), Vector2.one * .4f);
        EnemyHealth farther = Enemy(new Vector2(1.05f, 0f), Vector2.one * .2f);
        var shot = Fire(Vector2.zero, Vector2.right, 2);
        Step(shot);
        Check(closest.CurrentHealth == 4, "point-blank enemy between muzzle and nose receives direct damage");
        Check(farther.CurrentHealth == 4, "farther point-blank enemy receives full explosion damage");
        Check(Vector2.Distance(OneImpact().transform.position, new Vector2(.28f, 0f)) < .01f,
            "point-blank impact uses the closest enemy surface");
        Check(Vector2.Distance(shot.transform.position, Vector2.zero) < .001f,
            "point-blank collision never moves the projectile backwards");
    }

    private static void VerifyPhysicalFloor()
    {
        Solid(new Vector2(0f, -1f), new Vector2(20f, .2f));
        EnemyHealth near = Enemy(new Vector2(.5f, -.6f), Vector2.one * .2f);
        var shot = Fire(new Vector2(0f, 1f), Vector2.down, 2);
        Step(shot);
        Check(near.CurrentHealth == 4, "physical floor explosion applies full nearby damage");
        Check(Vector2.Distance(OneImpact().transform.position, new Vector2(0f, -.78f)) < .01f, "physical floor impact stays at contact");
    }

    private static void VerifyPaintedFloor()
    {
        var owner = Make("Painted-floor verification owner", Vector2.zero);
        var renderer = owner.AddComponent<SpriteRenderer>();
        var texture = new Texture2D(64, 128);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 128), Vector2.one * .5f, 64f);
        renderer.sprite = sprite;
        try
        {
            float feet = renderer.bounds.min.y + renderer.bounds.size.y * .03f;
            EnemyHealth near = Enemy(new Vector2(.55f, -.55f), Vector2.one * .3f);
            var shot = Fire(new Vector2(0f, 1.5f), Vector2.down, 2, owner.transform);
            owner.transform.position += Vector3.up * 3f;
            Step(shot);
            Check(near.CurrentHealth == 4, "painted floor explosion applies full nearby damage without a floor collider");
            Check(Vector2.Distance(OneImpact().transform.position, new Vector2(0f, feet + .12f)) < .01f, "painted floor is snapshotted from launch-time feet");
            Check(Mathf.Abs(shot.transform.position.y - ShotVisual.RocketWidth - feet) < .01f, "downward rocket nose reaches painted floor");
        }
        finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
    }

    private static void VerifyGroundOrdering(bool enemyFirst)
    {
        var owner = Make("Ground-order verification owner", Vector2.zero);
        var renderer = owner.AddComponent<SpriteRenderer>();
        var texture = new Texture2D(64, 128);
        var sprite = Sprite.Create(texture, new Rect(0, 0, 64, 128), Vector2.one * .5f, 64f);
        renderer.sprite = sprite;
        try
        {
            float feet = renderer.bounds.min.y + renderer.bounds.size.y * .03f;
            EnemyHealth enemy = Enemy(new Vector2(0f, enemyFirst ? 0f : -4.6f), Vector2.one * .4f);
            var shot = Fire(new Vector2(0f, ShotVisual.RocketWidth + .6f), Vector2.down, 2, owner.transform);
            Step(shot);
            if (enemyFirst)
            {
                // Raw collider cast distance includes the rocket length, whereas
                // ground nose travel does not. Comparing them would choose ground.
                Check(enemy.CurrentHealth == 4, "enemy above painted floor wins after converting cast distance to tail travel");
                Check(Vector2.Distance(OneImpact().transform.position, new Vector2(0f, .32f)) < .01f,
                    "earlier enemy impact uses enemy surface rather than floor");
                Check(Mathf.Abs(shot.transform.position.y - ShotVisual.RocketWidth - .2f) < .01f,
                    "enemy-first downward impact stops the visible nose at the target");
            }
            else
            {
                Check(enemy.CurrentHealth == 10, "painted floor wins over a farther collider beyond splash radius");
                Check(Vector2.Distance(OneImpact().transform.position, new Vector2(0f, feet + .12f)) < .01f,
                    "earlier painted-floor impact uses feet rather than farther enemy surface");
            }
        }
        finally { Object.DestroyImmediate(sprite); Object.DestroyImmediate(texture); }
    }

    private static void VerifyBulletsAndExpiry()
    {
        EnemyHealth direct = Enemy(new Vector2(3f, 0f), Vector2.one * .5f);
        EnemyHealth near = Enemy(new Vector2(3.05f, .6f), Vector2.one * .3f);
        var bullet = Fire(Vector2.zero, Vector2.right, 0);
        Step(bullet);
        Check(direct.CurrentHealth == 4 && near.CurrentHealth == 10, "pistol preserves direct damage with no splash");
        Check(Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Length == 0, "bullet creates no rocket flipbook");
        ClearObjects();
        direct = Enemy(new Vector2(3f, 0f), Vector2.one * .5f);
        near = Enemy(new Vector2(3.05f, .6f), Vector2.one * .3f);
        var automatic = Fire(Vector2.zero, Vector2.right, 1);
        Step(automatic);
        Check(direct.CurrentHealth == 4 && near.CurrentHealth == 10, "SMG preserves direct damage with no splash");
        Check(Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Length == 0, "SMG creates no rocket flipbook");
        ClearObjects();
        var expired = Fire(Vector2.zero, Vector2.up, 2, null, .1f);
        Step(expired);
        Check((bool)typeof(CombatProjectile).GetField("spent", Private).GetValue(expired), "range-expired rocket is spent");
        Check(Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Length == 0, "range expiry produces no impact");
    }

    private static void VerifyFrames()
    {
        RocketImpact effect = RocketImpact.Create(Vector2.zero);
        Check(effect != null, "flipbook creates");
        created.Add(effect.gameObject);
        effect.enabled = false;
        var renderer = effect.GetComponent<SpriteRenderer>();
        var age = typeof(RocketImpact).GetField("age", Private);
        var update = typeof(RocketImpact).GetMethod("Update", Private);
        float initialWidth = renderer.bounds.size.x;
        float previousAlpha = 1f;
        for (int i = 0; i < RocketImpact.FrameCount; i++)
        {
            age.SetValue(effect, RocketImpact.Duration * (i + .35f) / RocketImpact.FrameCount - Time.deltaTime);
            update.Invoke(effect, null);
            Check(renderer.sprite == RocketImpact.GetFrame(i), "flipbook plays frame " + i);
            Check(Mathf.Abs(renderer.bounds.size.x - initialWidth) < .002f, "frame " + i + " preserves drawn expansion on a fixed canvas");
            Check(renderer.color.a <= previousAlpha + .001f, "frame " + i + " alpha never increases");
            previousAlpha = renderer.color.a;
        }
        Check(Mathf.Abs(initialWidth - 4.4f * 1.5f) < .002f, "impact canvas is 50 percent larger than its original width");
        Check(previousAlpha < .2f, "last smoke frame fades before removal");
    }

    private static GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private static EnemyHealth Enemy(Vector2 position, Vector2 size)
    {
        var go = Make("RPG verification enemy", position);
        go.SetActive(false);
        go.AddComponent<BoxCollider2D>().size = size;
        var health = go.AddComponent<EnemyHealth>();
        typeof(EnemyHealth).GetField("maxHealth", Private).SetValue(health, 10);
        go.SetActive(true);
        return health;
    }

    private static void DuplicateCollider(EnemyHealth enemy, Vector2 offset)
    {
        var child = Make("Duplicate enemy collider", Vector2.zero);
        child.transform.SetParent(enemy.transform, false);
        child.transform.localPosition = offset;
        var collider = child.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one * .3f;
        collider.isTrigger = true;
    }

    private static void Solid(Vector2 position, Vector2 size) { Make("RPG verification solid", position).AddComponent<BoxCollider2D>().size = size; }

    private static CombatProjectile Fire(Vector2 origin, Vector2 direction, int weapon, Transform owner = null, float range = 20f)
    {
        Check(CombatProjectile.TrySpawn(origin, direction, 6, 500f, .2f, range, owner, weapon), "verification shot spawns");
        var shots = Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None);
        Check(shots.Length == 1, "exactly one verification projectile exists");
        shots[0].enabled = false;
        shots[0].GetComponent<ShotVisual>().enabled = false;
        created.Add(shots[0].gameObject);
        return shots[0];
    }

    private static void Step(CombatProjectile projectile)
    {
        Physics2D.SyncTransforms();
        typeof(CombatProjectile).GetMethod("FixedUpdate", Private).Invoke(projectile, null);
    }

    private static RocketImpact OneImpact()
    {
        var effects = Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None);
        Check(effects.Length == 1, "collision creates exactly one RPG impact");
        return effects[0];
    }

    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception("RPG impact check failed: " + label);
    }

    private static void ClearObjects()
    {
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        foreach (var effect in Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None)) Object.DestroyImmediate(effect.gameObject);
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)) Object.DestroyImmediate(effect.gameObject);
        Physics2D.SyncTransforms();
    }

    private static void Finish(int result)
    {
        SessionState.SetInt(ResultKey, result);
        Cleanup();
        EditorApplication.isPlaying = false;
    }

    private static void Cleanup()
    {
        EditorApplication.update -= Tick;
        routine = null;
        ClearObjects();
        profileScope?.Dispose();
        profileScope = null;
        Time.timeScale = previousScale;
        Application.runInBackground = previousBackground;
        // Only verification profile files are removed; keep the exported preview for inspection.
        if (!string.IsNullOrEmpty(directory) && Directory.Exists(directory))
            foreach (string file in Directory.GetFiles(directory, "profile-*.json*")) File.Delete(file);
    }

    private static void RestoreAndExit()
    {
        bool batch = SessionState.GetBool(BatchKey, false);
        int result = SessionState.GetInt(ResultKey, 1);
        SessionState.SetBool(RunningKey, false);
        var saved = JsonUtility.FromJson<SavedScenes>(SessionState.GetString(SceneKey, ""));
        if (!batch && saved != null)
        {
            var setup = new List<SceneSetup>();
            foreach (var scene in saved.scenes)
                if (!string.IsNullOrEmpty(scene.path)) setup.Add(new SceneSetup { path = scene.path, isLoaded = scene.loaded, isActive = scene.active });
            if (setup.Count > 0) EditorSceneManager.RestoreSceneManagerSetup(setup.ToArray());
        }
        SessionState.EraseString(SceneKey);
        if (batch) EditorApplication.Exit(result);
    }
}
