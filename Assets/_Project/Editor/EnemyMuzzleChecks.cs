using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated regressions for enemy barrel sockets, real emission, cover and range.</summary>
public static class EnemyMuzzleChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector2 Center = new(15000f, 15000f);
    private static readonly List<GameObject> created = new();
    private static readonly EditorCurveBinding SpriteBinding = new()
        { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };
    private static HashSet<ShotVisual> existingShots;
    private static HashSet<ShotEffect> existingEffects;
    private static int assertions;
    private const float Epsilon = .005f;

    // Independently measured contact points in the imported rectangles, top-left pixels.
    private static readonly Vector2[] PistolPixels =
    {
        new(38,119), new(65,127), new(32,128), new(31,128),
        new(35,130), new(46,128), new(44,130), new(42,133)
    };
    private static readonly Vector2[] SMGPixels =
    {
        new(103,140), new(113,136), new(97,128), new(73,130),
        new(77,134), new(101,126), new(97,129), new(88,138)
    };

    private static T Get<T>(object instance, string field) =>
        (T)instance.GetType().GetField(field, Private).GetValue(instance);
    private static object Call(object instance, string method, params object[] args) =>
        instance.GetType().GetMethod(method, Private).Invoke(instance, args);
    private static void Check(bool valid, string label)
    {
        assertions++;
        if (!valid) throw new Exception("Enemy muzzle: " + label);
    }
    private static bool Near(Vector2 actual, Vector2 expected) => Vector2.Distance(actual, expected) <= Epsilon;
    private static bool Fire(BasicEnemy enemy, PlayerActions victim)
    {
        Physics2D.SyncTransforms();
        return (bool)Call(enemy, "FireAt", victim);
    }
    private static Sprite[] Frames(int type, string state)
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(
            $"Assets/_Project/Animations/Enemies/Enemy{type}/Enemy{type}_{state}.anim");
        if (clip == null) throw new Exception("Missing enemy clip: " + type + "/" + state);
        return AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding)
            .Select(k => (Sprite)k.value).Distinct().ToArray();
    }
    private static Sprite[] FiringFrames(int type) => Frames(type, type == 4 ? "SMGFire" : "PistolFire");
    private static GameObject Prefab(int type) => AssetDatabase.LoadAssetAtPath<GameObject>(
        $"Assets/_Project/Prefabs/Enemy{type}_Base.prefab");

    [MenuItem("Tools/Lindavista/Verify enemy muzzles and doubled SMG range (empty Play Mode scene)")]
    public static void Run() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused ||
            PistolUpgradeShop.IsOpen || Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None).Length != 0 ||
            Object.FindObjectsByType<BasicEnemy>(FindObjectsSortMode.None).Length != 0)
            throw new InvalidOperationException("Use an empty, unpaused Play Mode scene, not an active match.");
        assertions = 0;
        existingShots = new(Object.FindObjectsByType<ShotVisual>(FindObjectsSortMode.None));
        existingEffects = new(Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None));
        Directory.CreateDirectory("Temp");
        string profilePath = Path.Combine("Temp", "EnemyMuzzle-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(profilePath, true)))
            {
                VerifyReferences();
                VerifyAllFrames();
                VerifyIdleAndWalkEmission();
                VerifyRange();
                VerifyCover();
                VerifyPointBlank();
                VerifyTraceLength();
                VerifyBurst();
            }
            return "ENEMY_MUZZLE_CHECKS_OK: " + assertions +
                " assertions; 16 firing poses, both directions, real/varied scales, flipY, " +
                "barrel/flash origins, damage/audio, idle/walk poses, 13-unit SMG range, " +
                "cover before/at/after barrel, contact fire, finite traces and full burst.";
        }
        finally
        {
            Clear();
            File.Delete(profilePath);
            File.Delete(profilePath + ".bak");
            File.Delete(profilePath + ".tmp");
        }
    }

    private static void VerifyReferences()
    {
        foreach (int type in new[] { 2, 4 })
        {
            var prefab = Prefab(type);
            Check(prefab != null, "ranged prefab exists: " + type);
            var enemy = prefab.GetComponent<BasicEnemy>();
            var frames = FiringFrames(type);
            Check(frames.Length == 8, "eight firing poses: " + type);
            Sprite reference = Get<Sprite>(enemy, "firingMuzzleReference");
            Check(reference == frames[0], "prefab binds its first real firing pose: " + type);
            Check(frames.All(s => EnemyMuzzle.IsFiringSprite(s, type == 4)) &&
                frames.All(s => !EnemyMuzzle.IsFiringSprite(s, type != 4)), "firing frames are weapon-specific: " + type);
            Check(Mathf.Approximately(Get<float>(enemy, "attackRange"), type == 4 ? 13f : 10f),
                "SMG doubles 6.5 to 13 while pistol retains 10");
            Check(!EnemyMuzzle.TryWorldPosition(null, reference, false, out _), "null renderer safely rejects socket");
        }
    }

    private static void VerifyAllFrames()
    {
        foreach (int type in new[] { 2, 4 })
        foreach (Vector3 scaleFactor in new[] { Vector3.one, new Vector3(.83f, 1.17f, 1f) })
        foreach (float forward in new[] { -1f, 1f })
        {
            var enemy = Enemy(type);
            enemy.transform.localScale = Vector3.Scale(enemy.transform.localScale, scaleFactor);
            var renderer = enemy.GetComponent<SpriteRenderer>();
            var reference = Get<Sprite>(enemy, "firingMuzzleReference");
            var player = Player(Center + Vector2.right * (forward * 7f));
            Sprite[] frames = FiringFrames(type);
            for (int frame = 0; frame < frames.Length; frame++)
            {
                renderer.sprite = frames[frame];
                renderer.flipX = forward < 0f; // Deliberately stale: actual emission must face its target.
                foreach (bool flipY in new[] { false, true })
                {
                    renderer.flipY = flipY;
                    bool oldFlip = renderer.flipX;
                    Check(EnemyMuzzle.TryWorldPosition(renderer, reference, forward > 0f, out Vector2 socket) &&
                        Near(socket, Expected(renderer, type, frame, forward > 0f, flipY)),
                        $"measured pixel socket, enemy {type}, frame {frame}, direction {forward}, scale {scaleFactor}, flipY {flipY}");
                    Check(renderer.flipX == oldFlip, "prospective socket does not mutate facing");
                }
                renderer.flipY = false;
                Physics2D.SyncTransforms();
                bool beforeQuery = renderer.flipX;
                Check((bool)Call(enemy, "CanHit", player) && renderer.flipX == beforeQuery,
                    "prospective line of sight keeps facing unchanged");
                player.Health.Initialize(100, 0f);
                Check(Fire(enemy, player), "valid firing frame emits");
                Vector2 expected = Expected(renderer, type, frame, forward > 0f, false);
                Check(renderer.sprite == frames[frame] && renderer.flipX == (forward > 0f),
                    "emission preserves firing pose and faces victim");
                CheckEmission(enemy, player, expected, forward, false);
                ReturnEffects();
                Get<AudioSource>(enemy, "weaponShotSource").Stop();
            }
            Clear();
        }
    }

    private static void VerifyIdleAndWalkEmission()
    {
        foreach (int type in new[] { 2, 4 })
        foreach (string state in new[] { "Idle", "Walk" })
        foreach (float forward in new[] { -1f, 1f })
        {
            var enemy = Enemy(type);
            var renderer = enemy.GetComponent<SpriteRenderer>();
            renderer.sprite = Frames(type, state)[0];
            var player = Player(Center + Vector2.right * (forward * 7f));
            Check(!EnemyMuzzle.TryWorldPosition(renderer, null, false, out _), "idle/walk has no fabricated socket");
            Check(EnemyMuzzle.TryWorldPosition(renderer, Get<Sprite>(enemy, "firingMuzzleReference"), forward > 0f, out _),
                "line of sight can preview first firing pose");
            Check(Fire(enemy, player) && EnemyMuzzle.IsFiringSprite(renderer.sprite, type == 4),
                "idle/walk emission evaluates visible firing pose first");
            CheckEmission(enemy, player, Expected(renderer, type, 0, forward > 0f, false), forward, false);
            Clear();
        }
    }

    private static void VerifyRange()
    {
        foreach (float forward in new[] { -1f, 1f })
        foreach (float distance in new[] { 12f, 13f, 13.1f })
        {
            var enemy = Enemy(4);
            var player = Player(Center + Vector2.right * (forward * distance));
            bool expected = distance <= 13f;
            Physics2D.SyncTransforms();
            Check((bool)Call(enemy, "CanHit", player) == expected, "SMG engagement boundary at " + distance);
            Check(Fire(enemy, player) == expected, "SMG emission boundary at " + distance);
            Check(player.CurrentHealth == (expected ? 99 : 100), "only in-range SMG damages at " + distance);
            Check(NewShots().Length == (expected ? 1 : 0), "out-of-range SMG creates no visual");
            Clear();
        }
    }

    private static void VerifyCover()
    {
        foreach (int type in new[] { 2, 4 })
        foreach (float forward in new[] { -1f, 1f })
        foreach (int placement in new[] { 0, 1, 2 })
        {
            var enemy = Enemy(type);
            var renderer = enemy.GetComponent<SpriteRenderer>();
            renderer.sprite = FiringFrames(type)[0];
            renderer.flipX = forward > 0f;
            var player = Player(Center + Vector2.right * (forward * 7f));
            Vector2 muzzle = Expected(renderer, type, 0, forward > 0f, false);
            Vector2 body = renderer.bounds.center;
            Vector2 wallAt = placement == 0 ? (body + muzzle) * .5f :
                placement == 1 ? muzzle : (muzzle + player.AimOrigin) * .5f;
            var wall = Make("Enemy muzzle solid cover", wallAt).AddComponent<BoxCollider2D>();
            wall.size = new Vector2(.12f, 4f);
            renderer.flipX = forward < 0f;
            Physics2D.SyncTransforms();
            Check(!(bool)Call(enemy, "CanHit", player) && renderer.flipX == (forward < 0f),
                "cover before/at/after barrel blocks prospective line of sight without turning");
            Check(!Fire(enemy, player) && player.CurrentHealth == 100 && NewShots().Length == 0,
                "cover prevents damage and muzzle effects even if gun would protrude");
            wall.isTrigger = true;
            Check(Fire(enemy, player) && player.CurrentHealth == 99, "decorative trigger permits actual shot");
            Clear();
        }
    }

    private static void VerifyPointBlank()
    {
        foreach (int type in new[] { 2, 4 })
        foreach (float forward in new[] { -1f, 1f })
        foreach (float distance in new[] { .7f, 1f })
        {
            var enemy = Enemy(type);
            var renderer = enemy.GetComponent<SpriteRenderer>();
            renderer.sprite = FiringFrames(type)[0];
            renderer.flipX = forward > 0f;
            var player = Player(Center + Vector2.right * (forward * distance));
            Vector2 muzzle = Expected(renderer, type, 0, forward > 0f, false);
            bool behindMuzzle = (player.AimOrigin.x - muzzle.x) * forward < 0f;
            Check(Fire(enemy, player), "contact target still permits firing");
            CheckEmission(enemy, player, muzzle, forward, behindMuzzle);
            Clear();
        }
    }

    private static void VerifyTraceLength()
    {
        foreach (int type in new[] { 2, 4 })
        foreach (float forward in new[] { -1f, 1f })
        {
            var enemy = Enemy(type);
            var renderer = enemy.GetComponent<SpriteRenderer>();
            renderer.sprite = FiringFrames(type)[0];
            renderer.flipX = forward > 0f;
            var player = Player(Center + Vector2.right * (forward * 7f));
            player.GetComponent<Collider2D>().enabled = false;
            Vector2 muzzle = Expected(renderer, type, 0, forward > 0f, false);
            Vector2 direction = (player.AimOrigin - muzzle).normalized;
            var beyondTarget = Make("Cover just past the target endpoint", player.AimOrigin + direction * .2f);
            beyondTarget.AddComponent<BoxCollider2D>().size = Vector2.one * .04f;
            Check(Fire(enemy, player), "finite no-hit trace emits");
            var shot = NewShots().Single();
            Check(!Get<bool>(shot, "impactAtEnd") &&
                Mathf.Abs(Get<float>(shot, "travelRemaining") - Vector2.Distance(muzzle, player.AimOrigin)) <= Epsilon,
                "trace ends at target instead of retaining old center-to-target distance");
            Check(player.CurrentHealth == 100, "no fabricated damage when target hitbox is disabled");
            Clear();
        }
    }

    private static void VerifyBurst()
    {
        var enemy = Enemy(4);
        var player = Player(Center + Vector2.left * 12f);
        var routine = (IEnumerator)Call(enemy, "PerformAttack", player);
        try
        {
            Check(routine.MoveNext(), "SMG burst includes preparation");
            for (int shot = 0; shot < 3; shot++)
            {
                Physics2D.SyncTransforms();
                bool more = routine.MoveNext();
                Check(more == (shot < 2) && player.CurrentHealth == 99 - shot,
                    "all three burst rounds hit at expanded distance");
                var renderer = enemy.GetComponent<SpriteRenderer>();
                CheckEmission(enemy, player, Expected(renderer, 4, 0, false, false), -1f, false, 99 - shot);
                ReturnEffects();
            }
        }
        finally { (routine as IDisposable)?.Dispose(); Clear(); }
    }

    private static Vector2 Expected(SpriteRenderer renderer, int type, int frame, bool flipX, bool flipY)
    {
        Sprite sprite = renderer.sprite;
        Vector2 pixel = (type == 4 ? SMGPixels : PistolPixels)[frame];
        Vector2 offset = new Vector2(pixel.x - sprite.pivot.x,
            sprite.rect.height - pixel.y - sprite.pivot.y) / sprite.pixelsPerUnit;
        if (flipX) offset.x = -offset.x;
        if (flipY) offset.y = -offset.y;
        return renderer.transform.TransformPoint(offset);
    }

    private static void CheckEmission(BasicEnemy enemy, PlayerActions player, Vector2 muzzle,
        float forward, bool pointBlank, int expectedHealth = 99)
    {
        ShotVisual[] shots = NewShots();
        var flashes = Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)
            .Where(e => !existingEffects.Contains(e) && e.name == "Muzzle flash").ToArray();
        Check(shots.Length == 1 && flashes.Length == 1, "one round and one muzzle flash per emission");
        var shot = shots[0];
        Check(Near(shot.transform.position, muzzle) && Near(flashes[0].transform.position, muzzle),
            "bullet and flash start at measured barrel tip");
        Vector2 direction = Get<Vector2>(shot, "flightDirection");
        float travel = Get<float>(shot, "travelRemaining");
        Check(direction.x * forward > 0f && direction.sqrMagnitude > .99f,
            "shot has normalized forward travel, including contact fire");
        Check(pointBlank ? travel == 0f : travel <= Vector2.Distance(muzzle, player.AimOrigin) + Epsilon,
            "contact shot does not draw backward; ordinary trace never exceeds target");
        Check(player.CurrentHealth == expectedHealth, "real physics shot applies one damage event");
        int weapon = Get<int>(shot, "weapon");
        var source = Get<AudioSource>(enemy, "weaponShotSource");
        var library = Resources.Load<WeaponAudioLibrary>("WeaponAudioLibrary");
        Check(source != null && library != null && source.clip == library.Shot(weapon) &&
            Mathf.Approximately(source.volume, library.ShotVolume(weapon) * (weapon == 0 ? .9f : 1f)) &&
            Mathf.Approximately(source.pitch, 1f) && source.isPlaying,
            "emission plays shared weapon audio at original volume and pitch");
    }

    private static BasicEnemy Enemy(int type)
    {
        var go = Object.Instantiate(Prefab(type), Center, Quaternion.identity);
        created.Add(go);
        var enemy = go.GetComponent<BasicEnemy>();
        enemy.enabled = false;
        enemy.ConfigureTactics();
        var source = Get<AudioSource>(enemy, "weaponShotSource");
        if (source != null) source.mute = true;
        return enemy;
    }
    private static PlayerActions Player(Vector2 position)
    {
        var go = Make("Marco enemy muzzle verification player", position);
        go.AddComponent<SpriteRenderer>().sprite = ProgressionVisuals.Projectile;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .5f;
        var player = go.AddComponent<PlayerActions>();
        player.Health.Initialize(100, 0f);
        return player;
    }
    private static GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }
    private static ShotVisual[] NewShots() => Object.FindObjectsByType<ShotVisual>(FindObjectsSortMode.None)
        .Where(s => !existingShots.Contains(s) && Get<bool>(s, "enemyFlight")).ToArray();
    private static void ReturnEffects()
    {
        foreach (var shot in NewShots()) Call(shot, "RetireEnemy");
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
            if (!existingEffects.Contains(effect)) Call(effect, "Devolver");
    }
    private static void Clear()
    {
        ReturnEffects();
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        Physics2D.SyncTransforms();
    }
}
