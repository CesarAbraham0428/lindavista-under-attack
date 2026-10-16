using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class ShotVisualChecks
{
    [MenuItem("Lindavista/Verify player muzzle origins (Play Mode)")]
    public static void VerifyPlayerMuzzles()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode before checking actual player shots.");
        string folder = Path.Combine(Application.dataPath, "../Temp/ShotMuzzleChecks");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, Guid.NewGuid() + ".json");
        File.WriteAllText(path, JsonUtility.ToJson(new ProgressionProfile
        {
            marco = new CharacterProgression { weaponUpgrades = new[] { 0, 0, 0 }, ammunition = new[] { 60, 180, 12 } },
            cesar = new CharacterProgression { weaponUpgrades = new[] { 0, 0, 0 }, ammunition = new[] { 60, 180, 12 } }
        }));
        var created = new List<GameObject>();
        var existingProjectiles = new HashSet<GameObject>(Projectiles());
        var existingEffects = new HashSet<ShotEffect>(UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None));
        var existingTracers = new HashSet<SniperShotVisual>(UnityEngine.Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None));
        int checkedShots = 0;
        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path)))
            {
                foreach (string character in new[] { "Marco", "Cesar" })
                for (int weapon = 0; weapon < 3; weapon++)
                foreach (Vector2 aim in new[] { new Vector2(1,0), new Vector2(-1,0),
                    new Vector2(1,1), new Vector2(-1,1), new Vector2(1,-1), new Vector2(-1,-1) })
                foreach (float pointerDistance in new[] { .05f, 12f })
                {
                    bool cesar = character == "Cesar";
                    var player = new GameObject("Muzzle verification player");
                    created.Add(player);
                    player.transform.position = new Vector3(1000, 1000, 0);
                    // Include non-uniform scaling to catch world-space distance regressions.
                    player.transform.localScale = new Vector3(.8f, 1.2f, 1);
                    var renderer = player.AddComponent<SpriteRenderer>();
                    var animator = player.AddComponent<Animator>();
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/_Project/Animations/Player_" + character + ".controller");
                    animator.Update(0f);
                    var actions = player.AddComponent<PlayerActions>();
                    actions.enabled = false;
                    actions.SelectTouchWeapon(weapon);
                    // Check both a pointer behind the barrel (previously reversed
                    // shots) and a distant crosshair the muzzle ray must pass through.
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    Vector2 pointer = actions.AimOrigin + aim.normalized * pointerDistance;
                    typeof(PlayerActions).GetMethod("SetPointerAim", flags).Invoke(actions, new object[] { pointer });
                    Require(Vector2.Distance(actions.AimTarget, pointer) < .001f, "Crosshair stays on the actual pointer");
                    // Start facing opposite the target to verify aim wins over old facing.
                    renderer.flipX = aim.x > 0;
                    int ammo = ProgressionService.Ammo(weapon, cesar);
                    int otherAmmo = ProgressionService.Ammo(weapon, !cesar);
                    int magazine = actions.Weapons.Magazine;
                    var previousShots = new HashSet<GameObject>(Projectiles());
                    typeof(PlayerActions).GetField("fireRequested", flags).SetValue(actions, true);
                    var lateUpdate = typeof(PlayerActions).GetMethod("LateUpdate", flags);
                    lateUpdate.Invoke(actions, null);
                    var shots = Projectiles().Where(p => !previousShots.Contains(p)).ToArray();
                    Require(shots.Length == 1, "Exactly one projectile is emitted per accepted shot");
                    var shot = shots[0];
                    created.Add(shot);
                    string weaponName = CharacterWeapons.State(weapon, cesar);
                    string expectedFrame = character + "_" + weaponName + (cesar ? "_Fire_" + (weapon == 1 ? 3 : 0) : "_Fire_8f_0");
                    Require(renderer.sprite.name == expectedFrame,
                        "Projectile uses the current weapon firing sprite, including after switching weapons");
                    Require(renderer.flipX == (aim.x < 0), "Firing sprite faces the target");
                    // Independent measured barrel coordinates for the first firing frame.
                    Vector2 pixel = cesar ?
                        (weapon == 0 ? new Vector2(325, 361) : weapon == 1 ? new Vector2(322, 224) : new Vector2(373, 182)) :
                        (weapon == 0 ? new Vector2(374, 119) : weapon == 1 ? new Vector2(371, 139) : new Vector2(354, 145));
                    Sprite sprite = renderer.sprite;
                    Vector2 local = (Vector2.Scale(new Vector2(pixel.x / 384f, 1f - pixel.y / 512f), sprite.rect.size) -
                        sprite.pivot) / sprite.pixelsPerUnit;
                    if (aim.x < 0) local.x = -local.x;
                    Vector2 expected = player.transform.TransformPoint(local);
                    Require(Vector2.Distance(shot.transform.position, expected) < .002f,
                        "Actual projectile spawns at the measured barrel, including diagonal aim");
                    Vector2 launchDirection = shot.transform.right;
                    if (pointerDistance > 1f)
                    {
                        Vector2 toPointer = pointer - expected;
                        Require(Vector2.Dot(launchDirection, toPointer.normalized) > .999f,
                            "Distant mouse shot is aimed from the measured muzzle through the crosshair");
                        Require(Mathf.Abs(launchDirection.x * toPointer.y - launchDirection.y * toPointer.x) < .002f,
                            "Distant crosshair lies on the actual muzzle ray, including diagonals");
                    }
                    else
                    {
                        Require(Vector2.Dot(launchDirection, actions.AimDirection) > 0f,
                            "A close pointer cannot reverse the intended shot direction");
                        Require(launchDirection.x * aim.x >= 0f,
                            "Close left/right aiming keeps the correct horizontal direction");
                    }
                    Require((shot.GetComponent<GrenadeProjectile>() != null) == (cesar && weapon == 2),
                        "Only Cesar's grenade launcher emits a ballistic grenade");
                    Require(ProgressionService.Ammo(weapon, cesar) == ammo - 1 && actions.Weapons.Magazine == magazine - 1,
                        "A shot consumes exactly one round");
                    Require(ProgressionService.Ammo(weapon, !cesar) == otherAmmo,
                        "Firing preserves the other character's ammunition");
                    lateUpdate.Invoke(actions, null);
                    Require(Projectiles().Count(p => !previousShots.Contains(p)) == 1 &&
                        actions.Weapons.Magazine == magazine - 1, "Holding fire respects the weapon cooldown");
                    UnityEngine.Object.DestroyImmediate(shot);
                    UnityEngine.Object.DestroyImmediate(player);
                    checkedShots++;
                }
            }
            Debug.Log("PLAYER_MUZZLE_CHECKS_OK: " + checkedShots +
                " actual close/distant mouse shots; both players, three weapons, left/right/diagonals, crosshair accuracy, scaling, pose, ammo and cooldown verified.");
        }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            foreach (var projectile in Projectiles())
                if (!existingProjectiles.Contains(projectile)) UnityEngine.Object.DestroyImmediate(projectile);
            foreach (var effect in UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
                if (!existingEffects.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            foreach (var tracer in UnityEngine.Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None))
                if (!existingTracers.Contains(tracer)) UnityEngine.Object.DestroyImmediate(tracer.gameObject);
            File.Delete(path);
            File.Delete(path + ".bak");
        }
    }

    private static GameObject[] Projectiles() => UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)
        .Select(p => p.gameObject)
        .Concat(UnityEngine.Object.FindObjectsByType<GrenadeProjectile>(FindObjectsSortMode.None).Select(p => p.gameObject))
        .Distinct().ToArray();

    [MenuItem("Lindavista/Verify screen shooting (Play Mode)")]
    public static void VerifyScreenShooting()
    {
        if (!Application.isPlaying) throw new Exception("Enter Play Mode before screen shooting checks.");
        var created = new List<GameObject>();
        var existingEffects = new HashSet<ShotEffect>(UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None));
        var existingTracers = new HashSet<SniperShotVisual>(UnityEngine.Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None));
        var flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Vector2 center = new Vector2(1000f, 1000f);
        GameObject Make(string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            created.Add(go);
            return go;
        }
        EnemyHealth Enemy(Vector2 position)
        {
            var go = Make("Screen shooting verification enemy", position);
            go.SetActive(false);
            go.AddComponent<BoxCollider2D>().size = Vector2.one * .35f;
            var health = go.AddComponent<EnemyHealth>();
            typeof(EnemyHealth).GetField("maxHealth", flags).SetValue(health, 10);
            go.SetActive(true);
            return health;
        }
        CombatProjectile Fire(Camera camera, Vector2 direction, float speed = 2000f, int targets = 1, float range = 18f,
            Vector2? origin = null)
        {
            var existing = new HashSet<CombatProjectile>(UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
            Require(CombatProjectile.TrySpawn(origin ?? center, direction, 3, speed, .07f, range, null,
                targets > 1 ? 1 : 0, targets, false, camera), "Verification shot is accepted");
            var shot = UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Single(p => !existing.Contains(p));
            created.Add(shot.gameObject);
            shot.enabled = false;
            return shot;
        }
        void Step(CombatProjectile shot)
        {
            Physics2D.SyncTransforms();
            typeof(CombatProjectile).GetMethod("FixedUpdate", flags).Invoke(shot, null);
        }
        bool Spent(CombatProjectile shot) => (bool)typeof(CombatProjectile).GetField("spent", flags).GetValue(shot);
        void Remove(params UnityEngine.Object[] objects)
        {
            foreach (var value in objects) if (value != null)
                UnityEngine.Object.DestroyImmediate(value is Component component ? component.gameObject : value);
            Physics2D.SyncTransforms();
        }
        try
        {
            var cameraObject = Make("Screen shooting verification camera", center);
            cameraObject.transform.position += Vector3.back * 10f;
            var camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 12f;
            camera.aspect = 2f;

            foreach (Vector2 direction in new[] { Vector2.right, Vector2.left, Vector2.up, Vector2.down,
                new Vector2(1f, 1f).normalized, new Vector2(-1f, -.3f).normalized })
            {
                CombatProjectile shot = Fire(camera, direction);
                Step(shot);
                Vector3 edge = camera.WorldToViewportPoint(shot.transform.position);
                Require(Spent(shot), "Empty shot ends at the view edge: " + direction);
                Require(edge.x >= -.001f && edge.x <= 1.001f && edge.y >= -.001f && edge.y <= 1.001f,
                    "Shot never travels beyond the camera: " + direction);
                Require(Mathf.Min(Mathf.Min(Mathf.Abs(edge.x), Mathf.Abs(edge.x - 1f)),
                    Mathf.Min(Mathf.Abs(edge.y), Mathf.Abs(edge.y - 1f))) < .001f,
                    "Shot reaches an actual screen edge: " + direction);
                Require(Vector2.Dot((Vector2)shot.transform.position - center, direction) > 11f,
                    "Close pointer distance does not limit travel: " + direction);
                Remove(shot);
            }

            foreach (Vector2 direction in new[] { Vector2.left, Vector2.right })
            {
                CombatProjectile offset = Fire(camera, direction, 3000f, origin: center + Vector2.left * 14.4f);
                Step(offset);
                float edgeX = camera.WorldToViewportPoint(offset.transform.position).x;
                Require(Spent(offset) && Mathf.Abs(edgeX - (direction.x > 0f ? 1f : 0f)) < .001f,
                    "An off-center player reaches the appropriate left/right edge");
                Remove(offset);
            }

            // The horizontal edge is 24 units away, farther than the old 18-unit range.
            EnemyHealth beyondCursor = Enemy(center + Vector2.right * 5f);
            EnemyHealth behindFirst = Enemy(center + Vector2.right * 10f);
            CombatProjectile hit = Fire(camera, Vector2.right);
            Step(hit);
            Require(beyondCursor.CurrentHealth == 7 && behindFirst.CurrentHealth == 10 && Spent(hit),
                "Enemy beyond the pointer takes damage and stops a normal bullet");
            Remove(hit, beyondCursor, behindFirst);

            var wall = Make("Screen shooting verification wall", center + Vector2.left * 5f);
            wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 3f);
            EnemyHealth behindWall = Enemy(center + Vector2.left * 10f);
            CombatProjectile blocked = Fire(camera, Vector2.left);
            Step(blocked);
            Require(Spent(blocked) && blocked.transform.position.x > center.x - 5f && behindWall.CurrentHealth == 10,
                "Leftward shots stop at solid cover");
            Remove(blocked, wall, behindWall);

            EnemyHealth offscreen = Enemy(center + Vector2.right * 28f);
            CombatProjectile limited = Fire(camera, Vector2.right);
            Step(limited);
            Require(Spent(limited) && offscreen.CurrentHealth == 10, "Off-screen enemy in another stretch is untouched");
            Require(Mathf.Abs(limited.transform.position.x - center.x - 24f) < .002f,
                "Visible travel extends past the old fixed range");
            Remove(limited, offscreen);

            EnemyHealth first = Enemy(center + Vector2.right * 5f);
            EnemyHealth second = Enemy(center + Vector2.right * 10f);
            EnemyHealth third = Enemy(center + Vector2.right * 20f);
            CombatProjectile sniper = Fire(camera, Vector2.right, 2000f, 2);
            Step(sniper);
            Require(first.CurrentHealth == 7 && second.CurrentHealth == 7 && third.CurrentHealth == 10 && Spent(sniper),
                "Camera range preserves the sniper's permitted penetration");
            Remove(sniper, first, second, third);

            CombatProjectile moving = Fire(camera, Vector2.right, 100f);
            Step(moving);
            Require(!Spent(moving), "A slower visible bullet remains alive");
            camera.transform.position += Vector3.right * 5f;
            typeof(CombatProjectile).GetField("speed", flags).SetValue(moving, 2000f);
            Step(moving);
            Require(Spent(moving) && Mathf.Abs(moving.transform.position.x - center.x - 29f) < .002f,
                "In-flight bullet follows the moving camera edge");
            Remove(moving);

            CombatProjectile shiftedOutside = Fire(camera, Vector2.right, 100f);
            Step(shiftedOutside);
            EnemyHealth outsideAfterShift = Enemy(shiftedOutside.transform.position);
            camera.transform.position += Vector3.right * 40f;
            Step(shiftedOutside);
            Require(Spent(shiftedOutside) && outsideAfterShift.CurrentHealth == 10,
                "Camera shift retires an off-screen shot before it damages an overlapping enemy");
            Remove(shiftedOutside, outsideAfterShift);

            CombatProjectile fallback = Fire(null, Vector2.right, 2000f, 1, 3f);
            Step(fallback);
            Require(Spent(fallback) && Mathf.Abs(fallback.transform.position.x - center.x - 3f) < .002f,
                "Without a camera the explicit finite range still applies");
            Remove(fallback);
            Debug.Log("SCREEN_SHOOTING_CHECKS_OK: six directions and off-center origins reach the view edge, close-pointer travel, left cover, enemy stops, off-screen exclusion after camera shifts, sniper penetration, moving camera and finite fallback verified.");
        }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            foreach (var effect in UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
                if (!existingEffects.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            foreach (var tracer in UnityEngine.Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None))
                if (!existingTracers.Contains(tracer)) UnityEngine.Object.DestroyImmediate(tracer.gameObject);
        }
    }

    [MenuItem("Lindavista/Verify shooting visuals")]
    public static void Run()
    {
        var created = new List<GameObject>();
        try
        {
            var atlas = Resources.Load<Texture2D>("ShotProjectileAtlas");
            Require(atlas != null && atlas.isReadable, "Readable projectile atlas is included in Resources");
            for (int i = 0; i < 4; i++)
            {
                Sprite sprite = ShotVisual.GetSprite(i);
                Require(sprite != null && sprite.texture == atlas, "Generated sprite " + i + " loads without fallback");
                Require(sprite.rect.width > 20 && sprite.rect.height > 20, "Sprite " + i + " has visible art");
                for (int j = 0; j < i; j++)
                    Require(sprite != ShotVisual.GetSprite(j), "Each weapon has a distinct sprite");
            }
            for (int i = 0; i < 3; i++)
            {
                var shot = new GameObject("Verify shot " + i);
                created.Add(shot);
                shot.transform.rotation = Quaternion.Euler(0, 0, 135);
                ShotVisual.Attach(shot, i);
                var renderer = shot.GetComponent<SpriteRenderer>();
                float expectedWidth = i == 2 ? 1.96f : i == 1 ? .24f : .2f;
                Require(Mathf.Abs(renderer.sprite.bounds.size.x * shot.transform.localScale.x - expectedWidth) < .001f,
                    "Weapon " + i + " has correct world size");
                Require(Vector2.Dot(shot.transform.right, new Vector2(-1, 1).normalized) > .999f,
                    "Projectile preserves aim rotation");
                Require((shot.GetComponent<TrailRenderer>() != null) == (i != 2), "Only bullets use short tracer trails");
                Require(Mathf.Abs(renderer.sprite.bounds.min.x) < .001f, "Projectile tail starts at the muzzle");
            }
            Debug.Log("SHOT_VISUAL_CHECKS_OK: atlas, four distinct sprites, three weapon sizes, aim rotation and trail profiles verified.");
        }
        finally
        {
            foreach (var go in created) UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("Shooting visual check failed: " + label);
    }
}
