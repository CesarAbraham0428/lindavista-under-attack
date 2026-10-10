using System;
using System.Collections.Generic;
using System.IO;
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
        File.WriteAllText(path, JsonUtility.ToJson(new ProgressionProfile { ammunition = new[] { 60, 180, 12 } }));
        var created = new List<GameObject>();
        var existingEffects = new HashSet<ShotEffect>(UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None));
        int checkedShots = 0;
        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path)))
            {
                foreach (string character in new[] { "Marco", "Cesar" })
                for (int weapon = 0; weapon < 3; weapon++)
                foreach (Vector2 aim in new[] { new Vector2(1,0), new Vector2(-1,0),
                    new Vector2(1,1), new Vector2(-1,1), new Vector2(1,-1), new Vector2(-1,-1) })
                {
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
                    actions.SetTouchAimAndFire(aim);
                    // Start facing opposite the target to verify aim wins over old facing.
                    renderer.flipX = aim.x > 0;
                    int ammo = ProgressionService.Ammo(weapon);
                    int magazine = actions.Weapons.Magazine;
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(PlayerActions).GetField("fireRequested", flags).SetValue(actions, true);
                    var lateUpdate = typeof(PlayerActions).GetMethod("LateUpdate", flags);
                    lateUpdate.Invoke(actions, null);
                    var shots = UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None);
                    Require(shots.Length == 1, "Exactly one projectile is emitted per accepted shot");
                    var shot = shots[0];
                    created.Add(shot.gameObject);
                    string weaponName = weapon == 0 ? "Pistol" : weapon == 1 ? "SMG" : "RPG";
                    Require(renderer.sprite.name == character + "_" + weaponName + "_Fire_8f_0",
                        "First projectile uses the firing sprite, including after switching weapons");
                    Require(renderer.flipX == (aim.x < 0), "Firing sprite faces the target");
                    // Independent measured barrel coordinates for the first firing frame.
                    Vector2 pixel = weapon == 0 ? new Vector2(character == "Marco" ? 374 : 375, 119) :
                        weapon == 1 ? new Vector2(character == "Marco" ? 371 : 370, 139) :
                        new Vector2(character == "Marco" ? 354 : 355, character == "Marco" ? 145 : 149);
                    Vector2 local = new Vector2((pixel.x - 192) / 100f, (256 - pixel.y) / 100f);
                    if (aim.x < 0) local.x = -local.x;
                    Vector2 expected = player.transform.TransformPoint(local);
                    Require(Vector2.Distance(shot.transform.position, expected) < .002f,
                        "Actual projectile spawns at the measured barrel, including diagonal aim");
                    Vector2 toTarget = (actions.AimTarget - expected).normalized;
                    Require(Vector2.Dot(shot.transform.right, toTarget) > .999f, "Projectile flies towards the aim target");
                    Require(ProgressionService.Ammo(weapon) == ammo - 1 && actions.Weapons.Magazine == magazine - 1,
                        "A shot consumes exactly one round");
                    lateUpdate.Invoke(actions, null);
                    Require(UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Length == 1 &&
                        actions.Weapons.Magazine == magazine - 1, "Holding fire respects the weapon cooldown");
                    UnityEngine.Object.DestroyImmediate(shot.gameObject);
                    UnityEngine.Object.DestroyImmediate(player);
                    checkedShots++;
                }
            }
            Debug.Log("PLAYER_MUZZLE_CHECKS_OK: " + checkedShots +
                " actual shots; both players, three weapons, left/right/diagonals, scaling, pose, ammo and cooldown verified.");
        }
        finally
        {
            foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            foreach (var effect in UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
                if (!existingEffects.Contains(effect)) UnityEngine.Object.DestroyImmediate(effect.gameObject);
            File.Delete(path);
            File.Delete(path + ".bak");
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
