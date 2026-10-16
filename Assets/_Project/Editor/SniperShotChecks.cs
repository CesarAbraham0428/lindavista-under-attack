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

/// <summary>Isolated physics and visual checks. Restores scenes and never saves the real player profile.</summary>
[InitializeOnLoad]
public static class SniperShotChecks
{
    private const string Key = "SniperShotChecks.Running";
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> created = new List<GameObject>();
    private static IEnumerator routine;
    private static int assertions;
    private static double deadline;
    private static float previousScale;
    private static bool previousBackground;

    [Serializable] private class SavedScenes { public SavedScene[] scenes; }
    [Serializable] private class SavedScene { public string path; public bool loaded, active; }

    static SniperShotChecks()
    {
        EditorApplication.playModeStateChanged += OnPlayState;
    }

    [MenuItem("Lindavista/Verify sniper piercing (isolated Play Mode)")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Stop Play Mode before sniper checks.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty || string.IsNullOrEmpty(SceneManager.GetSceneAt(i).path))
                throw new Exception("Save open scenes before isolated sniper checks.");
        SessionState.SetString(Key + ".Scenes", JsonUtility.ToJson(new SavedScenes {
            scenes = setup.Select(s => new SavedScene { path = s.path, loaded = s.isLoaded, active = s.isActive }).ToArray()
        }));
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
            deadline = EditorApplication.timeSinceStartup + 40;
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
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Sniper checks timed out.");
            if (routine.MoveNext()) return;
            Debug.Log("SNIPER_CHECKS_OK: " + assertions + " assertions; 2/3/4 targets, full damage, duplicate hitboxes, repeat steps, walls, range, directions, shields, SMG isolation, alpha, clipping, fade and pause. Preview: Library/SniperShotChecks/preview.png");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            Debug.LogError("SNIPER_CHECKS_FAILED after " + assertions + " assertions.");
        }
        EditorApplication.isPlaying = false;
    }

    private static IEnumerator Verify()
    {
        yield return null;
        var definition = WeaponDefinition.ForCharacter(1, true);
        for (int i = 0; i <= definition.MaxUpgrade; i++)
            Check(definition.TargetsAt(i) == Mathf.Min(4, 2 + i / 2), "sniper tier " + i);
        Check(definition.TargetsAt(999) == 4 && definition.TargetsAt(-1) == 2, "tier bounds");
        Check(!WeaponDefinition.At(1).IsPiercing && WeaponDefinition.At(1).TargetsAt(5) == 1, "Marco SMG stays single target");
        VerifyPlayerShot();
        Clear();
        foreach (Sprite sprite in new[] { SniperShotVisual.GetTracerSprite(), SniperShotVisual.GetBloodSprite() })
        {
            Check(sprite != null && sprite.texture.isReadable && sprite.name.StartsWith("Sniper"), "generated sprite loads");
            Color32[] pixels = sprite.texture.GetPixels32();
            int transparent = pixels.Count(p => p.a == 0), visible = pixels.Count(p => p.a > 24);
            Check(transparent > pixels.Length / 5 && visible > pixels.Length / 1000, "true alpha and visible sprite");
        }
        Check(Mathf.Abs(SniperShotVisual.GetTracerSprite().bounds.max.x) < .001f, "tracer pivot is at leading tip");

        foreach (int limit in new[] { 2, 3, 4 })
        {
            var enemies = new List<EnemyHealth>();
            for (int i = 1; i <= 5; i++) enemies.Add(Enemy(Vector2.right * i));
            var child = Make("extra enemy hitbox", Vector2.zero);
            child.transform.SetParent(enemies[0].transform, false);
            child.AddComponent<BoxCollider2D>().isTrigger = true;
            CombatProjectile shot = Fire(Vector2.right, limit);
            Step(shot);
            for (int i = 0; i < 5; i++)
                Check(enemies[i].CurrentHealth == (i < limit ? 7 : 10), "limit " + limit + ", enemy " + i);
            Check(Spent(shot), "last permitted enemy stops shot");
            Step(shot);
            Check(enemies[0].CurrentHealth == 7, "spent projectile cannot damage twice");
            Clear();
        }

        EnemyHealth repeated = Enemy(new Vector2(.5f, 0), Vector2.one);
        EnemyHealth next = Enemy(new Vector2(1.8f, 0));
        var slow = Fire(Vector2.right, 2, 5f, 10f);
        for (int i = 0; i < 4; i++) Step(slow);
        Check(repeated.CurrentHealth == 7 && next.CurrentHealth == 10, "large hitbox damaged once across steps");
        for (int i = 0; i < 8; i++) Step(slow);
        Check(next.CurrentHealth == 7 && Spent(slow), "continuation reaches next enemy");
        Clear();

        EnemyHealth front = Enemy(Vector2.right);
        Make("wall", new Vector2(2, 0)).AddComponent<BoxCollider2D>().size = new Vector2(.2f, 4f);
        EnemyHealth behind = Enemy(Vector2.right * 3);
        var blocked = Fire(Vector2.right, 4);
        Step(blocked);
        Check(front.CurrentHealth == 7 && behind.CurrentHealth == 10 && Spent(blocked), "solid obstacle stops penetration");
        Check(blocked.transform.position.x < 2f, "tip stays before wall");
        Clear();

        EnemyHealth inside = Enemy(Vector2.right * 2);
        EnemyHealth outside = Enemy(Vector2.right * 4);
        var ranged = Fire(Vector2.right, 4, 3f);
        Step(ranged);
        Check(inside.CurrentHealth == 7 && outside.CurrentHealth == 10, "range excludes farther target");
        Check(Spent(ranged) && Mathf.Abs(ranged.transform.position.x - 3f) < .001f, "exact finite range");
        Clear();

        foreach (Vector2 direction in new[] { Vector2.left, Vector2.up, new Vector2(1, -1).normalized })
        {
            EnemyHealth first = Enemy(direction);
            EnemyHealth second = Enemy(direction * 2);
            EnemyHealth side = Enemy(direction + new Vector2(-direction.y, direction.x) * .8f);
            var shot = Fire(direction, 2);
            Step(shot);
            Check(first.CurrentHealth == 7 && second.CurrentHealth == 7 && side.CurrentHealth == 10, "aligned targets only: " + direction);
            var tracer = Object.FindFirstObjectByType<SniperShotVisual>();
            Check(Vector2.Dot(tracer.transform.right, direction) > .999f, "visual follows aim");
            Clear();
        }

        EnemyHealth close = Enemy(Vector2.zero);
        EnemyHealth farther = Enemy(Vector2.right);
        var blank = Fire(Vector2.right, 2);
        Step(blank);
        Check(close.CurrentHealth == 7 && farther.CurrentHealth == 7, "point blank overlap");
        Clear();

        EnemyHealth shield = Enemy(Vector2.right);
        shield.gameObject.AddComponent<SpriteRenderer>();
        typeof(EnemyHealth).GetField("spriteRenderer", Private).SetValue(shield, shield.GetComponent<SpriteRenderer>());
        typeof(EnemyHealth).GetField("shieldBlocksFront", Private).SetValue(shield, true);
        EnemyHealth unshielded = Enemy(Vector2.right * 2);
        var shieldShot = Fire(Vector2.right, 2);
        Step(shieldShot);
        Check(shield.CurrentHealth == 10 && unshielded.CurrentHealth == 7, "shield keeps damage rules and counts as target");
        Check(Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Count(e => e.name == "Sniper blood impact") == 1, "no blood when shield blocks");
        Clear();

        EnemyHealth smgFirst = Enemy(Vector2.right);
        EnemyHealth smgSecond = Enemy(Vector2.right * 2);
        var smg = Fire(Vector2.right, 1);
        Step(smg);
        Check(smgFirst.CurrentHealth == 7 && smgSecond.CurrentHealth == 10, "SMG remains first hit only");
        Clear();

        var visual = SniperShotVisual.Create(Vector2.zero, Vector2.right);
        visual.DrawTo(Vector2.right * 2);
        var renderer = visual.GetComponent<SpriteRenderer>();
        Check(Mathf.Abs(renderer.bounds.min.x) < .001f && renderer.bounds.max.x <= 2.001f, "no streak before muzzle or ahead of tip");
        visual.DrawTo(Vector2.right * 20);
        Check(renderer.bounds.size.x <= SniperShotVisual.MaxLength + .001f, "visual length is capped");
        yield return null;
        Time.timeScale = 0f;
        visual.Release();
        float alpha = renderer.color.a;
        double pauseUntil = EditorApplication.timeSinceStartup + .12;
        while (EditorApplication.timeSinceStartup < pauseUntil) yield return null;
        Check(visual != null && Mathf.Approximately(renderer.color.a, alpha), "pause freezes fading");
        Time.timeScale = 1f;
        float until = Time.time + SniperShotVisual.FadeDuration + .05f;
        while (Time.time < until) yield return null;
        Check(visual == null, "released streak cleans up");
        Clear();

        CapturePreview();
    }

    private static GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private static void VerifyPlayerShot()
    {
        Directory.CreateDirectory("Library/SniperShotChecks");
        string path = "Library/SniperShotChecks/profile-" + Guid.NewGuid().ToString("N") + ".json";
        File.WriteAllText(path, JsonUtility.ToJson(new ProgressionProfile { ammunition = new[] { 60, 180, 12 } }));
        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path)))
            {
                var player = Make("Cesar verification owner", Vector2.left * 5);
                var renderer = player.AddComponent<SpriteRenderer>();
                var animator = player.AddComponent<Animator>();
                animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                    "Assets/_Project/Animations/Player_Cesar.controller");
                animator.Update(0f);
                var controller = player.AddComponent<PlayerWeaponController>();
                var actions = player.AddComponent<PlayerActions>();
                actions.enabled = false;
                actions.SelectTouchWeapon(1);
                Check(controller.EquippedWeapon == 1, "Cesar equips sniper");
                actions.SetTouchAimAndFire(Vector2.right);
                int ammo = ProgressionService.Ammo(1), magazine = controller.Magazine;
                typeof(PlayerActions).GetField("fireRequested", Private).SetValue(actions, true);
                typeof(PlayerActions).GetMethod("LateUpdate", Private).Invoke(actions, null);
                var shot = Object.FindFirstObjectByType<CombatProjectile>();
                Check(shot != null, "actual player action fires sniper");
                created.Add(shot.gameObject);
                shot.enabled = false;
                Check(renderer.sprite.name == "Cesar_Francotirador_Fire_3", "shot uses aimed muzzle flash pose");
                Sprite sprite = renderer.sprite;
                Vector2 measured = (Vector2.Scale(new Vector2(322f / 384f, 1f - 224f / 512f), sprite.rect.size) - sprite.pivot) / sprite.pixelsPerUnit;
                Vector2 expected = player.transform.TransformPoint(measured);
                Check(Vector2.Distance(shot.transform.position, expected) < .002f, "actual shot begins at current sniper barrel");
                Check((int)typeof(CombatProjectile).GetField("targetLimit", Private).GetValue(shot) == 2, "controller passes base penetration");
                Check(ProgressionService.Ammo(1) == ammo - 1 && controller.Magazine == magazine - 1, "single round consumed");
                Check(!controller.TryFire(Vector2.zero, Vector2.right, player.transform), "sniper cooldown applies");
            }
        }
        finally
        {
            File.Delete(path);
            File.Delete(path + ".bak");
        }
    }

    private static EnemyHealth Enemy(Vector2 position, Vector2? size = null)
    {
        var go = Make("sniper verification enemy", position);
        go.SetActive(false);
        go.AddComponent<BoxCollider2D>().size = size ?? Vector2.one * .35f;
        var enemy = go.AddComponent<EnemyHealth>();
        typeof(EnemyHealth).GetField("maxHealth", Private).SetValue(enemy, 10);
        go.SetActive(true);
        return enemy;
    }

    private static CombatProjectile Fire(Vector2 direction, int limit, float range = 20, float speed = 500)
    {
        Check(CombatProjectile.TrySpawn(Vector2.zero, direction, 3, speed, .07f, range, null, 1, limit), "shot spawns");
        var shot = Object.FindFirstObjectByType<CombatProjectile>();
        created.Add(shot.gameObject);
        shot.enabled = false;
        return shot;
    }

    private static void Step(CombatProjectile shot)
    {
        Physics2D.SyncTransforms();
        typeof(CombatProjectile).GetMethod("FixedUpdate", Private).Invoke(shot, null);
    }
    private static bool Spent(CombatProjectile shot) => (bool)typeof(CombatProjectile).GetField("spent", Private).GetValue(shot);
    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception("Sniper check failed: " + label);
    }
    private static void Clear()
    {
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        foreach (var go in Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None)) Object.DestroyImmediate(go.gameObject);
        foreach (var go in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)) Object.DestroyImmediate(go.gameObject);
        Physics2D.SyncTransforms();
    }

    private static void CapturePreview()
    {
        var cameraGo = Make("preview camera", new Vector2(5f, 0));
        cameraGo.transform.position += Vector3.back * 10f;
        var camera = cameraGo.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 3.2f;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.09f, .12f, .16f);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            "Assets/_Project/Animations/Players/Cesar/Cesar_Francotirador_Fire.anim");
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip).First();
        Sprite playerSprite = (Sprite)AnimationUtility.GetObjectReferenceCurve(clip, binding)[3].value;
        var player = PreviewActor(playerSprite, .4f);
        Vector2 muzzle = PlayerMuzzle.WorldPosition(player, 1);
        var streak = SniperShotVisual.Create(muzzle, Vector2.right);
        streak.DrawTo(new Vector2(10.5f, muzzle.y));
        Sprite enemySprite = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Characters/Enemies/Enemy1_Base.png").OfType<Sprite>().First();
        foreach (float x in new[] { 4.6f, 6.9f, 9.2f })
        {
            PreviewActor(enemySprite, x).flipX = true;
            SniperShotVisual.Blood(new Vector2(x, muzzle.y), Vector2.right);
        }
        var target = new RenderTexture(1280, 640, 24);
        var capture = new Texture2D(1280, 640, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        try
        {
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            capture.ReadPixels(new Rect(0, 0, 1280, 640), 0, 0);
            capture.Apply();
            Directory.CreateDirectory("Library/SniperShotChecks");
            File.WriteAllBytes("Library/SniperShotChecks/preview.png", capture.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(capture);
        }
    }

    private static SpriteRenderer PreviewActor(Sprite sprite, float x)
    {
        var actor = Make("preview actor", Vector2.zero);
        var renderer = actor.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        float scale = 3f / sprite.bounds.size.y;
        actor.transform.localScale = Vector3.one * scale;
        actor.transform.position = new Vector3(x - sprite.bounds.center.x * scale,
            -1.8f - sprite.bounds.min.y * scale, 0);
        return renderer;
    }
}

