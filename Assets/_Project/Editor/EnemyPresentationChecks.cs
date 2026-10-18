using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Measure soles/silhouettes offline; no readable textures or pixel scans in the game.</summary>
public static class EnemyPresentationChecks
{
    private static readonly EditorCurveBinding Binding = new() { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
    private static readonly Dictionary<string, (Color32[] pixels, int width)> sources = new();
    private const float GroundY = -4.44f;
    private static Sprite[] Frames(int type, string state) => AnimationUtility.GetObjectReferenceCurve(
        AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Project/Animations/Enemies/Enemy{type}/Enemy{type}_{state}.anim"), Binding)
        .Select(k => (Sprite)k.value).Distinct().ToArray();

    private static Rect Opaque(Sprite sprite)
    {
        string path = AssetDatabase.GetAssetPath(sprite);
        if (!sources.TryGetValue(path, out var source))
        {
            var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (!image.LoadImage(File.ReadAllBytes(path))) throw new Exception("Unreadable PNG: " + path);
                source = (image.GetPixels32(), image.width);
                sources.Add(path, source);
            }
            finally { Object.DestroyImmediate(image); }
        }
        Rect r = sprite.rect;
        int minX = (int)r.width, minY = (int)r.height, maxX = 0, maxY = 0;
        for (int y = 0; y < r.height; y++)
        for (int x = 0; x < r.width; x++)
            if (source.pixels[((int)r.y + y) * source.width + (int)r.x + x].a >= 128)
            {
                minX = Math.Min(x, minX); minY = Math.Min(y, minY);
                maxX = Math.Max(x + 1, maxX); maxY = Math.Max(y + 1, maxY);
            }
        if (maxX <= minX || maxY <= minY) throw new Exception("Empty silhouette: " + sprite.name);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }
    private static float Foot(Sprite sprite) => (Opaque(sprite).yMin - sprite.pivot.y) / sprite.pixelsPerUnit;
    private static float Height(Sprite sprite) => Opaque(sprite).height / sprite.pixelsPerUnit;

    [MenuItem("Tools/Lindavista/Normalize walking height of enemies 1-4")]
    public static void NormalizeWalk()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Stop Play Mode before changing sprite imports.");
        sources.Clear();
        try
        {
            for (int type = 1; type <= 4; type++)
            {
                Sprite idle = Frames(type, "Idle")[0];
                Sprite[] walk = Frames(type, "Walk");
                if (walk.Length != 8) throw new Exception("Expected eight existing walking frames.");
                var measured = walk.ToDictionary(s => s.name, Opaque);
                float ppu = measured.Values.Max(r => r.height) / Height(idle);
                float foot = Foot(idle);
                float center = (Opaque(idle).center.x - idle.pivot.x) / idle.pixelsPerUnit;
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(walk[0]));
                var factory = new SpriteDataProviderFactories(); factory.Init();
                var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
                provider.InitSpriteEditorDataProvider();
                var rects = provider.GetSpriteRects();
                foreach (var frame in rects)
                {
                    if (!measured.TryGetValue(frame.name, out var opaque)) continue;
                    frame.alignment = SpriteAlignment.Custom;
                    frame.pivot = new Vector2((opaque.center.x - center * ppu) / frame.rect.width,
                        (opaque.yMin - foot * ppu) / frame.rect.height);
                }
                // Match the tallest pose, not the median: walking may flex down,
                // but no frame can grow above the standing silhouette.
                importer.spritePixelsPerUnit = ppu;
                provider.SetSpriteRects(rects); provider.Apply(); importer.SaveAndReimport();
                Debug.Log($"ENEMY_WALK_SIZE_NORMALIZED: Enemy{type} PPU={ppu:F6}; sprite IDs/rects/clip timings preserved.");
            }
        }
        finally { sources.Clear(); }
    }

    [MenuItem("Tools/Lindavista/Verify enemy height, ground and draw order")]
    public static void Verify() => Debug.Log(RunAssets());
    public static string RunAssets()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Run outside Play Mode.");
        sources.Clear();
        int checks = 0;
        void Check(bool valid, string message) { checks++; if (!valid) throw new Exception("Enemy presentation: " + message); }
        try
        {
            for (int type = 1; type <= 4; type++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemy{type}_Base.prefab");
                var data = new SerializedObject(prefab.GetComponent<BasicEnemy>());
                Sprite idle = Frames(type, "Idle")[0]; Sprite[] walk = Frames(type, "Walk");
                Check(walk.Length == 8, "all eight walking poses preserved");
                Check(data.FindProperty("hasStandingFootAnchor").boolValue &&
                    Mathf.Abs(data.FindProperty("standingFootY").floatValue - Foot(idle)) < .0001f, "baked anchor matches visible soles");
                Check(Mathf.Abs(prefab.transform.TransformPoint(new Vector3(0, Foot(idle), 0)).y - GroundY) < .0001f, "prefab soles share the street");
                Check(prefab.GetComponent<SpriteRenderer>().sortingOrder == (type >= 3 ? 6 : 8), "large enemies behind smaller enemies");
                foreach (var frame in walk)
                {
                    Check(Height(frame) <= Height(idle) + .001f && Height(frame) >= Height(idle) * .96f,
                        "walk never enlarges silhouette; natural flexion remains");
                    Check(Mathf.Abs(Foot(frame) - Foot(idle)) < .0001f, "walk has no vertical foot drift");
                }
            }
            return "ENEMY_PRESENTATION_OK: " + checks + " assertions; four sizes, 32 walk poses, visible soles and draw order.";
        }
        finally { sources.Clear(); }
    }

    [MenuItem("Tools/Lindavista/Verify enemy ground at runtime (empty Play Mode scene)")]
    public static void VerifyRuntime() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded ||
            Object.FindObjectsByType<BasicEnemy>(FindObjectsSortMode.None).Length != 0 ||
            Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None).Length != 0)
            throw new InvalidOperationException("Use an empty Play Mode verification scene, not an active match.");
        var created = new List<GameObject>();
        using var profile = ProgressionService.UseVerificationRepository(new ProgressionRepository(
            Path.Combine(Application.temporaryCachePath, "EnemyGround-" + Guid.NewGuid().ToString("N") + ".json"), true));
        sources.Clear();
        int checks = 0;
        void Check(bool valid, string message) { checks++; if (!valid) throw new Exception("Runtime presentation: " + message); }
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        BasicEnemy Make(int type, float x)
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Project/Prefabs/Enemy{type}_Base.prefab"),
                new Vector3(x, 20000f, 0), Quaternion.identity);
            created.Add(go);
            var behavior = go.GetComponent<BasicEnemy>();
            behavior.enabled = false;
            behavior.ConfigureTactics();
            return behavior;
        }
        try
        {
            for (int type = 1; type <= 4; type++)
            {
                var enemy = Make(type, 20000f + type * 10f);
                var collider = enemy.GetComponent<Collider2D>();
                Vector2 size = ((BoxCollider2D)collider).size;
                Vector2 offset = ((BoxCollider2D)collider).offset;
                Vector3 scale = enemy.transform.localScale;
                enemy.PlaceOnGround(20000f);
                Vector3 grounded = enemy.transform.position;
                enemy.PlaceOnGround(20000f);
                Check(enemy.transform.position == grounded && Mathf.Abs(enemy.StandingGroundY - 20000f) < .003f,
                    "ground placement is idempotent in world space");
                Check(enemy.transform.localScale == scale && ((BoxCollider2D)collider).size == size && ((BoxCollider2D)collider).offset == offset,
                    "placement preserves scale and collider dimensions/offset");
                Check(enemy.GetComponent<SpriteRenderer>().sortingOrder == (type >= 3 ? 6 : 8), "runtime draw order");
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"Assets/_Project/Animations/Enemies/Enemy{type}/Enemy{type}_Walk.anim");
                for (int frame = 0; frame < 8; frame++)
                {
                    clip.SampleAnimation(enemy.gameObject, (frame + .25f) / clip.frameRate);
                    var sprite = enemy.GetComponent<SpriteRenderer>().sprite;
                    float foot = enemy.transform.TransformPoint(new Vector3(0, Foot(sprite), 0)).y;
                    Check(Mathf.Abs(foot - 20000f) < .003f && enemy.transform.localScale == scale,
                        "walking keeps soles at the shared floor without animating root scale");
                }
            }
            foreach (var go in created) Object.DestroyImmediate(go);
            created.Clear();
            for (int type = 1; type <= 4; type++)
            foreach (float forward in new[] { -1f, 1f })
            {
                var enemy = Make(type, 20000f);
                enemy.PlaceOnGround(20000f);
                enemy.SetEntranceTargetX(20000f + forward * 50f);
                var playerObject = new GameObject("Marco ground verification player");
                created.Add(playerObject);
                playerObject.transform.position = new Vector3(20000f + forward * (type == 1 || type == 3 ? 1f : 3f), 20002.56f, 0);
                playerObject.AddComponent<SpriteRenderer>().sprite = ProgressionVisuals.Projectile;
                playerObject.AddComponent<BoxCollider2D>().size = Vector2.one * .5f;
                var player = playerObject.AddComponent<PlayerActions>();
                player.Health.Initialize(100, 0f);
                Physics2D.SyncTransforms();
                Check((bool)typeof(BasicEnemy).GetMethod("CanHit", Private).Invoke(enemy, new object[] { player }),
                    "shared-floor alignment preserves useful combat range for every role/direction");
                var attack = (IEnumerator)typeof(BasicEnemy).GetMethod("PerformAttack", Private).Invoke(enemy, new object[] { player });
                attack.MoveNext(); attack.MoveNext();
                Check(player.CurrentHealth < 100, "grounded melee/ranged roles still deal real damage after windup");
                ((IDisposable)attack).Dispose();
                foreach (var go in created) Object.DestroyImmediate(go);
                created.Clear();
            }
            foreach (float forward in new[] { -1f, 1f })
            {
                var small = Make(1, 20000f);
                var tall = Make(3, 20000f + 2f * forward);
                small.PlaceOnGround(20000f); tall.PlaceOnGround(20000f);
                Check(Mathf.Abs(tall.transform.position.y - small.transform.position.y) > .65f &&
                    Mathf.Abs(tall.StandingGroundY - small.StandingGroundY) < .003f,
                    "different pivots/sizes are still the same ground lane");
                typeof(BasicEnemy).GetMethod("OnEnable", Private).Invoke(tall, null);
                Physics2D.SyncTransforms();
                var constrained = (Vector3)typeof(BasicEnemy).GetMethod("ConstrainMovement", Private).Invoke(small,
                    new object[] { small.transform.position + Vector3.right * (5f * forward) });
                Check(Mathf.Abs(constrained.x - small.transform.position.x) <= .751f,
                    "ally spacing survives mixed-size ground alignment in both directions");
                foreach (var go in created) Object.DestroyImmediate(go);
                created.Clear();
            }
            return "ENEMY_GROUND_RUNTIME_OK: " + checks + " assertions; unchanged hitbox sizes, animation soles, depth and mixed-height ally spacing.";
        }
        finally
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            sources.Clear();
        }
    }
}
