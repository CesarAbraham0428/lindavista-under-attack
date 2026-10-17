using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Regression checks for a permanent visual shield loss without movement or ally blockades.</summary>
public static class EnemyShieldBreakChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3 Center = new Vector3(24000f, 24000f, 0f);
    private static readonly EditorCurveBinding SpriteBinding = new EditorCurveBinding
        { path = "", type = typeof(SpriteRenderer), propertyName = "m_Sprite" };

    [MenuItem("Tools/Lindavista/Verify shield break animation assets")]
    public static void VerifyAssets() => Debug.Log(RunAssets());

    public static string RunAssets()
    {
        int assertions = 0;
        void Check(bool valid, string label)
        {
            assertions++;
            if (!valid) throw new InvalidOperationException("Shield break assets: " + label);
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy3_Base.prefab");
        Check(prefab != null, "real shield enemy prefab exists");
        var library = Resources.Load<EnemyShieldVisualLibrary>("Enemy3UnshieldedVisuals");
        Check(library != null && library.controller != null && library.brokenShield != null,
            "unshielded animations and detached shield sprite are available at runtime");
        var original = prefab.GetComponent<Animator>().runtimeAnimatorController;
        var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        library.controller.GetOverrides(overrides);
        var originalClips = original.animationClips.Distinct().ToArray();
        var shieldFrames = new HashSet<Sprite>(originalClips.Where(c => c.name != "Enemy3_Defeat").SelectMany(Frames));
        Check(originalClips.Any(c => c.name == "Enemy3_Defeat"), "original defeat state is included in validation");
        foreach (var clip in originalClips)
        {
            var replacement = overrides.FirstOrDefault(pair => pair.Key == clip).Value;
            Check(replacement != null && replacement != clip, clip.name + " has an unshielded replacement");
            var frames = Frames(replacement);
            Check(frames.Length > 0 && frames.All(frame => !shieldFrames.Contains(frame)),
                clip.name + " never restores a shielded frame after the shield is lost");
            Check(!AnimationUtility.GetCurveBindings(replacement).Any(binding =>
                binding.path == "" && binding.type == typeof(Transform) &&
                binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal)),
                clip.name + " does not enlarge the enemy root");
            foreach (var binding in AnimationUtility.GetCurveBindings(replacement).Where(binding =>
                binding.type == typeof(SpriteRenderer) && binding.propertyName == "m_Color.a"))
            {
                var alpha = AnimationUtility.GetEditorCurve(replacement, binding);
                Check(alpha.keys.All(key => key.value >= .999f) &&
                    Enumerable.Range(0, 17).All(step => alpha.Evaluate(replacement.length * step / 16f) >= .999f),
                    clip.name + " preserves full body opacity");
            }
        }
        AnimationClip walk = overrides.First(pair => pair.Key.name == "Enemy3_Walk").Value;
        AnimationClip defeat = overrides.First(pair => pair.Key.name == "Enemy3_Defeat").Value;
        Sprite[] walkFrames = Frames(walk).Distinct().ToArray();
        Sprite[] defeatFrames = Frames(defeat).Distinct().ToArray();
        void CheckDedicatedSheet(Sprite[] frames, string file)
        {
            string path = "Assets/_Project/Art/Characters/Enemies/" + file;
            Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().ToArray();
            Check(sprites.Length == 8 && frames.Length == 8 &&
                new HashSet<Sprite>(sprites).SetEquals(frames), file + " supplies all eight distinct animation poses");
            Check(sprites.All(sprite => sprite.rect.xMin >= 0 && sprite.rect.yMin >= 0 &&
                    sprite.rect.xMax <= sprite.texture.width && sprite.rect.yMax <= sprite.texture.height &&
                    sprite.pixelsPerUnit > 0 && Mathf.Approximately(sprite.pixelsPerUnit, sprites[0].pixelsPerUnit)) &&
                !sprites.Any(a => sprites.Any(b => a != b && a.rect.Overlaps(b.rect))),
                file + " contains eight separate, unclipped poses at a consistent body scale");
        }
        CheckDedicatedSheet(walkFrames, "Enemy3_Unshielded_Walk_8f.png");
        CheckDedicatedSheet(defeatFrames, "Enemy3_Unshielded_Defeat_8f.png");
        Check(Mathf.Abs(walk.length - 1f) < .01f && AnimationUtility.GetAnimationClipSettings(walk).loopTime,
            "unshielded walk plays a complete eight-pose gait in a one-second loop");
        Check(Mathf.Abs(defeat.length - 1.1f) < .085f && !AnimationUtility.GetAnimationClipSettings(defeat).loopTime,
            "unshielded defeat plays a single complete fall lasting about 1.1 seconds");
        Rect initial = Opaque(defeatFrames[0]);
        Rect final = Opaque(defeatFrames[defeatFrames.Length - 1]);
        Check(final.height / defeatFrames[defeatFrames.Length - 1].pixelsPerUnit <
            initial.height / defeatFrames[0].pixelsPerUnit * .6f && final.width > final.height,
            "the last defeat pose is a horizontal corpse below sixty percent of standing height");
        return "ENEMY_SHIELD_BREAK_ASSETS_OK: " + assertions +
            " assertions; shieldless states, eight-frame walk/fall, opaque horizontal corpse and detached shield art.";
    }

    [MenuItem("Tools/Lindavista/Verify shield break and continued advance (Play Mode)")]
    public static void VerifyRuntime() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen)
            throw new InvalidOperationException("Run in active, unpaused Play Mode with no shop open.");
        var created = new List<GameObject>();
        int assertions = 0;
        void Check(bool valid, string label)
        {
            assertions++;
            if (!valid) throw new InvalidOperationException("Shield break runtime: " + label);
        }
        BasicEnemy Make(int type, Vector3 position)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy" + type + "_Base.prefab");
            var go = Object.Instantiate(prefab, position, Quaternion.identity);
            created.Add(go);
            Object.DestroyImmediate(go.GetComponent<EnemyLootDrop>());
            var enemy = go.GetComponent<BasicEnemy>();
            enemy.enabled = false;
            enemy.ConfigureTactics();
            enemy.PlaceOnGround(Center.y);
            Set(enemy, "nextTargetSearch", float.PositiveInfinity);
            Set(enemy, "nextAttack", float.PositiveInfinity);
            Set(enemy, "target", null);
            return enemy;
        }
        try
        {
            foreach (float forward in new[] { -1f, 1f })
            {
                var follower = Make(1, Center);
                var enemy = Make(3, Center + Vector3.right * (2f * forward));
                var health = enemy.GetComponent<EnemyHealth>();
                var renderer = enemy.GetComponent<SpriteRenderer>();
                var animator = enemy.GetComponent<Animator>();
                var visual = enemy.GetComponent<EnemyShieldBreakVisual>();
                Check(visual != null && !visual.HasDroppedShield, "shield starts attached");
                float normalDespawn = new SerializedObject(health).FindProperty("defeatDespawnDelay").floatValue;
                Check(Mathf.Abs((float)Call(health, "GetDefeatDespawnDelay") - normalDespawn) < .001f,
                    "an intact enemy preserves its original defeat despawn delay");
                animator.Play("Enemy3_Idle", 0, 0f);
                animator.Update(0f);
                renderer.flipX = forward > 0f;
                Vector2 frontalShot = forward > 0f ? Vector2.left : Vector2.right;
                enemy.SetEntranceTargetX(Center.x + 50f * forward);
                Vector3 scale = enemy.transform.localScale;
                Call(enemy, "OnEnable");
                Physics2D.SyncTransforms();
                Vector3 request = follower.transform.position + Vector3.right * (5f * forward);
                Vector3 beforeBreak = (Vector3)Call(follower, "ConstrainMovement", request);
                Check(Mathf.Abs(beforeBreak.x - follower.transform.position.x) <= .751f,
                    "intact shield unit is covered by normal ally spacing");

                int breaks = 0;
                health.ShieldBroken += () => breaks++;
                health.ConfigureShieldOpening(2, .1f);
                health.TakeDamage(1, frontalShot);
                Check(!health.IsShieldBroken && health.CurrentHealth == health.MaxHealth && breaks == 0,
                    "shield absorbs hits below its break threshold");
                Set(enemy, "attackInProgress", true);
                health.TakeDamage(1, frontalShot);
                Check(health.IsShieldBroken && health.CurrentHealth == health.MaxHealth && breaks == 1,
                    "breaking hit loses the shield permanently without damaging the body");
                Check(visual.HasDroppedShield && visual.DroppedShield != null,
                    "breaking hit creates the animated detached shield");
                GameObject dropped = visual.DroppedShield;
                created.Add(dropped);
                Check(animator.runtimeAnimatorController ==
                    Resources.Load<EnemyShieldVisualLibrary>("Enemy3UnshieldedVisuals").controller,
                    "breaking hit switches all future animation states to the unshielded body");

                Vector3 previous = enemy.transform.position;
                Call(enemy, "Update");
                Check((enemy.transform.position.x - previous.x) * forward > 0f && animator.GetBool("IsMoving"),
                    "breaking shield interrupts a pending windup and continues walking in the same update");
                Check(enemy.transform.localScale == scale, "shield loss never enlarges the enemy");
                Vector3 afterBreak = (Vector3)Call(follower, "ConstrainMovement", request);
                Check(Mathf.Abs(afterBreak.x - request.x) < .003f,
                    "nearby following enemies can advance past the broken shield unit");

                Call(follower, "OnEnable");
                follower.transform.position = enemy.transform.position + Vector3.right * (.8f * forward);
                follower.PlaceOnGround(Center.y);
                Physics2D.SyncTransforms();
                request = enemy.transform.position + Vector3.right * (5f * forward);
                afterBreak = (Vector3)Call(enemy, "ConstrainMovement", request);
                Check(Mathf.Abs(afterBreak.x - request.x) < .003f,
                    "the exposed enemy also keeps advancing past nearby allies");

                Set(health, "lastShieldHitTime", Time.time - 100f);
                health.ConfigureShieldOpening(6, 5f);
                health.TakeDamage(1, frontalShot);
                health.TakeExplosionDamage(1);
                Check(health.CurrentHealth == health.MaxHealth - 2 && health.IsShieldBroken,
                    "later frontal bullets and explosions penetrate after the old hit window or reconfiguration");
                Check(breaks == 1 && visual.DroppedShield == dropped,
                    "later damage cannot emit another break event or create another dropped shield");

                var overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
                ((AnimatorOverrideController)animator.runtimeAnimatorController).GetOverrides(overrides);
                AnimationClip walk = overrides.First(pair => pair.Key.name == "Enemy3_Walk").Value;
                AnimationClip defeat = overrides.First(pair => pair.Key.name == "Enemy3_Defeat").Value;
                animator.ResetTrigger("TakeDamage");
                animator.ResetTrigger("Attack");
                animator.ResetTrigger("Defeat");
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(walk, SpriteBinding).Take(8))
                {
                    animator.Play("Enemy3_Walk", 0, (key.time + .01f) / walk.length);
                    animator.Update(0f);
                    Check(renderer.sprite == key.value && renderer.flipX == (forward > 0f) &&
                        renderer.color.a >= .999f && enemy.transform.localScale == scale,
                        "actual unshielded walking animator advances every pose at full opacity in both facings");
                }

                health.TakeDamage(health.MaxHealth);
                Check(health.IsDead && health.IsShieldBroken && breaks == 1 && visual.DroppedShield == dropped &&
                    animator.runtimeAnimatorController == Resources.Load<EnemyShieldVisualLibrary>("Enemy3UnshieldedVisuals").controller,
                    "defeat preserves the unshielded body and never drops a second shield");
                Check(animator.enabled && !enemy.GetComponent<Collider2D>().enabled &&
                    (float)Call(health, "GetDefeatDespawnDelay") >= defeat.length + .4f - .001f,
                    "true death leaves the animator running through the full fall and a visible corpse hold");
                animator.ResetTrigger("Defeat");
                Sprite finalPose = null;
                foreach (var key in AnimationUtility.GetObjectReferenceCurve(defeat, SpriteBinding).Take(8))
                {
                    animator.Play("Enemy3_Defeat", 0, (key.time + .01f) / defeat.length);
                    animator.Update(0f);
                    finalPose = key.value as Sprite;
                    Check(renderer.sprite == finalPose && renderer.flipX == (forward > 0f) &&
                        renderer.color.a >= .999f && enemy.transform.localScale == scale,
                        "actual defeated animator completes every fall pose without fading or restoring the shield");
                }
                animator.Play("Enemy3_Defeat", 0, 1.1f);
                animator.Update(0f);
                Check(renderer.sprite == finalPose && renderer.color.a >= .999f,
                    "completed defeat holds the final visible horizontal corpse pose");
                foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
                created.Clear();
            }
            return "ENEMY_SHIELD_BREAK_RUNTIME_OK: " + assertions +
                " assertions; both facings, permanent shield loss, continued advance, eight-pose walk/fall and full visible corpse hold.";
        }
        finally
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            Physics2D.SyncTransforms();
        }
    }

    private static Sprite[] Frames(AnimationClip clip) =>
        (AnimationUtility.GetObjectReferenceCurve(clip, SpriteBinding) ?? Array.Empty<ObjectReferenceKeyframe>())
            .Select(frame => frame.value).OfType<Sprite>().ToArray();
    private static Rect Opaque(Sprite sprite)
    {
        var image = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            string path = AssetDatabase.GetAssetPath(sprite);
            if (!image.LoadImage(File.ReadAllBytes(path)))
                throw new InvalidOperationException("Cannot measure defeat sprite: " + path);
            Color32[] pixels = image.GetPixels32();
            Rect rect = sprite.rect;
            int left = (int)rect.width, right = 0, bottom = (int)rect.height, top = 0;
            for (int y = 0; y < rect.height; y++)
            for (int x = 0; x < rect.width; x++)
                if (pixels[((int)rect.y + y) * image.width + (int)rect.x + x].a >= 128)
                {
                    left = Math.Min(left, x); right = Math.Max(right, x + 1);
                    bottom = Math.Min(bottom, y); top = Math.Max(top, y + 1);
                }
            if (right <= left || top <= bottom) throw new InvalidOperationException("Defeat sprite has no opaque body.");
            return new Rect(left, bottom, right - left, top - bottom);
        }
        finally { Object.DestroyImmediate(image); }
    }
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);
}
