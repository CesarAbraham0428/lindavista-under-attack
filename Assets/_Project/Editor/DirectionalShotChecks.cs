using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises real mouse-fired rounds beyond the pointer in both camera directions.</summary>
public static class DirectionalShotChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Lindavista/Verify directional player shots (Play Mode)")]
    public static void Run() => RunRuntime();

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused)
            throw new Exception("Run directional shot checks in an active, unpaused Play Mode scene.");

        var created = new List<GameObject>();
        var previousShots = new HashSet<CombatProjectile>(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
        var previousEffects = new HashSet<ShotEffect>(Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None));
        var previousTracers = new HashSet<SniperShotVisual>(Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None));
        int assertions = 0, shots = 0;
        Vector2 center = new Vector2(1000f, 1000f);
        string directory = Path.Combine(Application.temporaryCachePath, "DirectionalShotChecks");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".json");
        var profile = new ProgressionProfile();
        profile.marco.ammunition = new[] { 60, 180, 12 };
        profile.cesar.ammunition = new[] { 60, 180, 12 };
        File.WriteAllText(path, JsonUtility.ToJson(profile));

        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Directional shot check failed: " + message);
        }
        GameObject Make(string name, Vector2 position)
        {
            var go = new GameObject(name);
            go.transform.position = position;
            created.Add(go);
            return go;
        }
        EnemyHealth Enemy(Vector2 position, Vector2? size = null)
        {
            var go = Make("Directional verification target", position);
            go.SetActive(false);
            go.AddComponent<BoxCollider2D>().size = size ?? Vector2.one * .35f;
            var health = go.AddComponent<EnemyHealth>();
            typeof(EnemyHealth).GetField("maxHealth", Private).SetValue(health, 100);
            go.SetActive(true);
            return health;
        }
        void Step(CombatProjectile projectile)
        {
            Physics2D.SyncTransforms();
            typeof(CombatProjectile).GetMethod("FixedUpdate", Private).Invoke(projectile, null);
        }
        void Remove(params Object[] values)
        {
            foreach (var value in values)
                if (value != null) Object.DestroyImmediate(value is Component component ? component.gameObject : value);
            Physics2D.SyncTransforms();
        }

        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path, true)))
            {
                var cameraObject = Make("Directional verification camera", center);
                cameraObject.transform.position += Vector3.back * 10f;
                var camera = cameraObject.AddComponent<Camera>();
                camera.orthographic = true;
                camera.orthographicSize = 20f;
                camera.aspect = 2f;

                foreach (string character in new[] { "Marco", "Cesar" })
                for (int weapon = 0; weapon < (character == "Cesar" ? 2 : 3); weapon++)
                foreach (float side in new[] { -1f, 1f })
                // ±24 places the actor at 20%/80% of this 80-unit view. The
                // short left half matches the player-passed-by-enemy scenario.
                foreach (float offset in new[] { -24f, 24f })
                // The grenade launcher retains its ballistic arc and fuse. These
                // cases cover every weapon that travels along a straight shot ray.
                for (int aimCase = 0; aimCase < 4; aimCase++)
                {
                    string label = $"{character}, weapon {weapon}, side {side}, offset {offset}, aim {aimCase}";
                    ProgressionService.TryGrantAmmo(weapon, int.MaxValue, character == "Cesar");
                    var player = Make("Directional verification " + character, center + Vector2.right * offset);
                    player.transform.localScale = new Vector3(.65f, .8f, 1f);
                    var renderer = player.AddComponent<SpriteRenderer>();
                    var animator = player.AddComponent<Animator>();
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                        "Assets/_Project/Animations/Player_" + character + ".controller");
                    animator.Update(0f);
                    var actions = player.AddComponent<PlayerActions>();
                    // Keep the real input eligibility guard enabled. This entire
                    // shot check is synchronous and destroys the player before a new frame.
                    actions.SelectTouchWeapon(weapon);
                    var controller = actions.Weapons;
                    typeof(PlayerActions).GetField("aimCamera", Private).SetValue(actions, camera);
                    typeof(PlayerWeaponController).GetField("camaraDisparo", Private).SetValue(controller, camera);
                    renderer.flipX = side < 0f;
                    float firingTime = character == "Cesar" ? weapon == 0 ? .25f : weapon == 1 ? .38f : 0f : 0f;
                    animator.Play(character + "_" + CharacterWeapons.State(weapon, character == "Cesar") + "_Fire", 0, firingTime);
                    animator.Update(0f);
                    Vector2 muzzle = PlayerMuzzle.WorldPosition(renderer, weapon);
                    Vector2 desired = new Vector2(side, aimCase == 3 ? .25f : 0f).normalized;
                    float viewDistance = DistanceToViewEdge(camera, muzzle, desired);
                    float nearDistance = Mathf.Min(2.15f, Vector2.Distance(actions.AimOrigin, muzzle) * 1.9f);
                    Vector2 pointer = aimCase < 2 ? actions.AimOrigin + desired * (aimCase == 0 ? .05f : nearDistance) :
                        muzzle + desired * 12f;
                    typeof(PlayerActions).GetMethod("SetPointerAim", Private).Invoke(actions, new object[] { pointer });
                    // The shot must turn toward the target, even if the old walk
                    // pose was facing in the opposite direction before LateUpdate.
                    renderer.flipX = side > 0f;
                    int ammoBefore = ProgressionService.Ammo(weapon, controller.IsCesar);
                    var existing = new HashSet<CombatProjectile>(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
                    typeof(PlayerActions).GetField("fireRequested", Private).SetValue(actions, true);
                    typeof(PlayerActions).GetMethod("LateUpdate", Private).Invoke(actions, null);
                    var emitted = Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None)
                        .Where(p => !existing.Contains(p)).ToArray();
                    Check(emitted.Length == 1, "Exactly one actual player round: " + label);
                    var shot = emitted[0];
                    created.Add(shot.gameObject);
                    shot.enabled = false;
                    Vector2 direction = shot.transform.right;
                    Check(renderer.flipX == (side < 0f) && Vector2.Dot(direction, desired) > .999f,
                        "Mouse ray keeps the intended mirrored heading: " + label);
                    Check(Vector2.Distance(shot.transform.position, muzzle) < .002f,
                        "Round starts at the current mirrored muzzle: " + label);
                    Check(ProgressionService.Ammo(weapon, controller.IsCesar) == ammoBefore - 1,
                        "Actual action spends exactly one round: " + label);
                    if (aimCase >= 2)
                    {
                        Vector2 toPointer = pointer - (Vector2)shot.transform.position;
                        Check(Mathf.Abs(direction.x * toPointer.y - direction.y * toPointer.x) < .002f,
                            "Distant pointer lies on the shot ray: " + label);
                    }
                    Vector2 targetPosition = aimCase == 1 ?
                        (Vector2)player.transform.position + new Vector2(side * 6f, 0f) :
                        muzzle + desired * (aimCase == 0 ? viewDistance * .8f : 12f + (viewDistance - 12f) * .55f);
                    Check(Vector2.Dot(targetPosition - pointer, desired) > 0f,
                        "Regression target is farther along the ray than the pointer: " + label);
                    EnemyHealth target = Enemy(targetPosition, aimCase == 1 ? new Vector2(.75f, 3f) : (Vector2?)null);
                    EnemyHealth beyondView = Enemy(muzzle + desired * (viewDistance + CombatProjectile.RocketSplashRadius + 3f));
                    typeof(CombatProjectile).GetField("speed", Private).SetValue(shot, 5000f);
                    Step(shot);
                    Check(target.CurrentHealth == 100 - controller.Damage,
                        "Visible enemy beyond the cursor takes full damage: " + label);
                    Check(beyondView.CurrentHealth == 100,
                        "Enemy beyond the camera stays untouched: " + label);
                    // A sniper continues until its target budget or the view edge;
                    // ordinary rounds stop on this target.
                    if (controller.Definition.IsPiercing)
                    {
                        Check((bool)typeof(CombatProjectile).GetField("spent", Private).GetValue(shot),
                            "Sniper finishes at the view edge after its first target: " + label);
                        Vector3 edge = camera.WorldToViewportPoint(shot.transform.position);
                        Check(Mathf.Min(Mathf.Min(Mathf.Abs(edge.x), Mathf.Abs(edge.x - 1f)),
                            Mathf.Min(Mathf.Abs(edge.y), Mathf.Abs(edge.y - 1f))) < .002f,
                            "Off-center sniper reaches a visible camera edge: " + label);
                    }
                    Remove(shot, target, beyondView, player);
                    shots++;
                }
            }
            string result = "DIRECTIONAL_SHOT_CHECKS_OK: " + assertions + " assertions, " + shots +
                " actual mouse shots; both players, all straight-firing weapons, mirrored/off-center origins, close/distant pointers, diagonals, opposite facing, full damage beyond cursor and camera exclusion.";
            Debug.Log(result);
            return result;
        }
        finally
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            foreach (var shot in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if (!previousShots.Contains(shot)) Object.DestroyImmediate(shot.gameObject);
            foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
                if (!previousEffects.Contains(effect)) Object.DestroyImmediate(effect.gameObject);
            foreach (var tracer in Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None))
                if (!previousTracers.Contains(tracer)) Object.DestroyImmediate(tracer.gameObject);
            File.Delete(path);
            File.Delete(path + ".bak");
            File.Delete(path + ".tmp");
        }
    }

    private static float DistanceToViewEdge(Camera camera, Vector2 origin, Vector2 direction)
    {
        Vector2 bottomLeft = camera.ViewportToWorldPoint(new Vector3(0f, 0f, 10f));
        Vector2 topRight = camera.ViewportToWorldPoint(new Vector3(1f, 1f, 10f));
        float x = direction.x > 0f ? (topRight.x - origin.x) / direction.x :
            (bottomLeft.x - origin.x) / direction.x;
        float y = Mathf.Abs(direction.y) < .0001f ? float.PositiveInfinity :
            direction.y > 0f ? (topRight.y - origin.y) / direction.y :
            (bottomLeft.y - origin.y) / direction.y;
        return Mathf.Min(x, y);
    }
}
