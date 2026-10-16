using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

/// <summary>Integration checks use a temporary wallet and disposable actors, never the player's save.</summary>
public static class CesarWeaponChecks
{
    public static string Result { get; private set; } = "Not run";
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Cesar weapon check failed: " + message);
    }

    [MenuItem("Tools/Lindavista/Verify Cesar weapon assets")]
    public static void VerifyAssets()
    {
        assertions = 0;
        var cesar = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animations/Player_Cesar.controller");
        var marco = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/_Project/Animations/Player_Marco.controller");
        var cesarAudio = Resources.Load<WeaponAudioLibrary>("CesarWeaponAudioLibrary");
        var marcoAudio = Resources.Load<WeaponAudioLibrary>("WeaponAudioLibrary");
        for (int i = 0; i < 3; i++)
        {
            string weapon = CharacterWeapons.State(i, true);
            Check(WeaponDefinition.ForCharacter(i, true) != WeaponDefinition.At(i), "character-specific definition " + weapon);
            Check(CharacterWeapons.Artwork(true).IconAt(i) != CharacterWeapons.Artwork(false).IconAt(i), "exclusive HUD icon " + weapon);
            Check(CharacterWeapons.Artwork(true).AmmoAt(i) != null, "ammo artwork " + weapon);
            Check(cesarAudio.Shot(i) != null && cesarAudio.Reload(i) != null, "audio assigned " + weapon);
            Check(cesarAudio.Shot(i) != marcoAudio.Shot(i), "exclusive sound " + weapon);
            foreach (string action in new[] { "Fire", "Reload" })
            {
                string stateName = "Cesar_" + weapon + "_" + action;
                var state = cesar.layers[0].stateMachine.states.First(s => s.state.name == stateName).state;
                var clip = (AnimationClip)state.motion;
                var keys = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
                Check(keys.Select(k => k.value).Distinct().Count() == 8, "eight sliced frames " + stateName);
                Check(!AnimationUtility.GetAnimationClipSettings(clip).loopTime, "non-looping action " + stateName);
                Check(keys.All(k => k.value != null && k.value.name.Contains(weapon)), "correct weapon in every frame " + stateName);
                string sheetPath = "Assets/_Project/Art/Characters/Players/" + stateName + ".png";
                var sheet = AssetDatabase.LoadAssetAtPath<Texture2D>(sheetPath);
                var frames = AssetDatabase.LoadAllAssetsAtPath(sheetPath).OfType<Sprite>()
                    .OrderByDescending(s => s.rect.y).ThenBy(s => s.rect.x).ToArray();
                Check(frames.Length == 8, "complete sheet " + stateName);
                for (int frame = 0; frame < frames.Length; frame++)
                {
                    var expected = new Rect(frame % 4 * sheet.width / 4, (1 - frame / 4) * sheet.height / 2,
                        sheet.width / 4, sheet.height / 2);
                    Check(frames[frame].rect == expected, "full cell " + stateName + " frame " + frame);
                    Check(keys[frame].value == frames[frame], "current sprite in visual order " + stateName + " frame " + frame);
                    Check(frames[frame].pixelsPerUnit > 50f && frames[frame].pixelsPerUnit < 110f &&
                          frames[frame].pivot.y > 150f, "standing scale and pivot " + stateName + " frame " + frame);
                }
                Check(state.transitions.All(t => t.hasExitTime && t.exitTime == 1f), "complete animation before exit " + stateName);
                Check(!marco.layers[0].stateMachine.states.Any(s => s.state.name.Contains(weapon)), "Marco excludes " + stateName);
            }
        }
        Check(!cesar.animationClips.Any(c => c.name.Contains("Pistol") || c.name.Contains("SMG") || c.name.Contains("RPG")), "Cesar excludes Marco weapon clips");
        Result = assertions + " asset assertions passed.";
        Debug.Log(Result);
    }

    public static string BeginRuntime()
    {
        if (!EditorApplication.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused)
            throw new InvalidOperationException("Run during an unfinished, unpaused Gameplay match.");
        Result = "Running";
        var host = new GameObject("Cesar Weapon Verification", typeof(PlayerWeaponController));
        host.GetComponent<PlayerWeaponController>().StartCoroutine(Runtime(host));
        return Result;
    }

    private static GameObject Actor(bool cesar)
    {
        var actor = new GameObject(cesar ? "Verification Cesar" : "Verification Marco");
        actor.SetActive(false);
        actor.transform.position = new Vector3(1000, 0, 0);
        actor.AddComponent<SpriteRenderer>();
        var animator = actor.AddComponent<Animator>();
        animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
            "Assets/_Project/Animations/Player_" + (cesar ? "Cesar" : "Marco") + ".controller");
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        actor.AddComponent<PlayerWeaponController>();
        actor.AddComponent<PlayerActions>().enabled = false;
        actor.SetActive(true);
        return actor;
    }

    private static IEnumerator Runtime(GameObject host)
    {
        assertions = 0;
        bool previousBackground = Application.runInBackground;
        Application.runInBackground = true;
        string directory = Path.Combine("Temp", "CesarWeaponChecks", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        GameObject cesar = null, marco = null;
        var scope = ProgressionService.UseVerificationRepository(new ProgressionRepository(Path.Combine(directory, "profile.json")));
        try
        {
            cesar = Actor(true);
            marco = Actor(false);
            var actions = cesar.GetComponent<PlayerActions>();
            var weapons = cesar.GetComponent<PlayerWeaponController>();
            var animator = cesar.GetComponent<Animator>();
            var renderer = cesar.GetComponent<SpriteRenderer>();
            var holdFire = typeof(PlayerActions).GetMethod("HoldFire", BindingFlags.NonPublic | BindingFlags.Instance);
            Check(weapons.IsCesar && !marco.GetComponent<PlayerWeaponController>().IsCesar, "actor identity independent of menu selection");
            Check(!weapons.TryEquip(-1) && !weapons.TryEquip(3), "reject out-of-range weapons");
            for (int i = 0; i < 3; i++)
            {
                actions.EquipWeapon(i);
                animator.Play("Cesar_Idle", 0, 0f);
                animator.Update(0f);
                string weapon = CharacterWeapons.State(i, true);
                Check(renderer.sprite.name.Contains(weapon), "equipped idle pose " + weapon);
                // Evaluate the actual controller at every key; missing sub-assets previously
                // made these states render null even though gameplay and audio still worked.
                foreach (string action in new[] { "Fire", "Reload" })
                {
                    string stateName = "Cesar_" + weapon + "_" + action;
                    var clip = animator.runtimeAnimatorController.animationClips.First(c => c.name == stateName);
                    var keys = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
                    for (int frame = 0; frame < 8; frame++)
                    {
                        animator.Play(stateName, 0, (keys[frame].time + 0.001f) / clip.length);
                        animator.Update(0f);
                        Check(renderer.sprite != null && renderer.sprite == keys[frame].value,
                            "visible Animator frame " + stateName + " " + frame);
                    }
                }
                animator.Play("Cesar_Idle", 0, 0f);
                animator.Update(0f);
                Check(weapons.Magazine == weapons.Definition.MagazineSize, "initial magazine " + weapon);
                int ammo = ProgressionService.Ammo(i);
                actions.SetTouchAimAndFire(Vector2.right);
                holdFire.Invoke(actions, null);
                actions.StopTouchAimAndFire();
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Cesar_" + weapon + "_Fire"), "real fire input selects animation " + weapon);
                Check(weapons.Magazine == weapons.Definition.MagazineSize - 1 && ProgressionService.Ammo(i) == ammo - 1, "exactly one round consumed " + weapon);
                Check(cesar.GetComponentsInChildren<AudioSource>().Any(s => s.isPlaying), "shot sound starts " + weapon);
                Check(!weapons.TryFire(new Vector2(1000, 0), Vector2.right, cesar.transform), "cadence blocks duplicate shot " + weapon);
                actions.ReloadTouchWeapon();
                Check(weapons.IsReloading && animator.GetCurrentAnimatorStateInfo(0).IsName("Cesar_" + weapon + "_Reload"), "reload input starts animation " + weapon);
                var reloadAudio = cesar.transform.Find("Weapon Reload Audio").GetComponent<AudioSource>();
                Check(reloadAudio.clip == Resources.Load<WeaponAudioLibrary>("CesarWeaponAudioLibrary").Reload(i), "correct reload sound " + weapon);
                Check(!weapons.TryFire(new Vector2(1000, 0), Vector2.right, cesar.transform), "reload blocks shooting " + weapon);
                float reloadWait = weapons.Definition.ReloadDuration + 0.15f;
                if (i == 0)
                {
                    Check(Mathf.Approximately(reloadAudio.pitch, 1f), "shotgun reload retains original pitch");
                    float delay = Mathf.Max(0f, weapons.Definition.ReloadDuration - reloadAudio.clip.length - 0.05f);
                    yield return new WaitForSeconds(delay + 0.08f);
                    reloadWait -= delay + 0.08f;
                    Check(reloadAudio.isPlaying && reloadAudio.timeSamples > 0, "shotgun reload foley actually plays at final rack");
                    Check(reloadAudio.time < 0.2f, "short shotgun foley is scheduled, not stretched");
                }
                else Check(reloadAudio.isPlaying, "reload sound starts " + weapon);
                yield return new WaitForSeconds(reloadWait);
                Check(!weapons.IsReloading && weapons.Magazine == weapons.Definition.MagazineSize, "timed refill " + weapon);
                Check(!reloadAudio.isPlaying, "reload sound stops on completion " + weapon);
                animator.Update(0f);
                Check(!animator.GetCurrentAnimatorStateInfo(0).IsName("Cesar_" + weapon + "_Reload"), "reload exits without getting stuck " + weapon);
                Check(weapons.TryFire(new Vector2(1000, 0), Vector2.right, cesar.transform), "fire resumes after reload " + weapon);
                yield return new WaitForSeconds(weapons.Definition.FireInterval + 0.05f);
            }
            // Changing slots cancels both animation and reload audio through the same UI entry point.
            animator.Play("Cesar_Idle", 0, 0f); animator.Update(0f);
            actions.ReloadTouchWeapon();
            Check(weapons.IsReloading, "start interruptible reload");
            actions.EquipWeapon(0);
            Check(!weapons.IsReloading && !cesar.transform.Find("Weapon Reload Audio").GetComponent<AudioSource>().isPlaying, "switch cancels reload and sound");
            // Stop must also cancel a scheduled shotgun sound before it becomes audible.
            actions.ReloadTouchWeapon();
            Check(weapons.IsReloading, "start scheduled shotgun reload");
            var pendingAudio = cesar.transform.Find("Weapon Reload Audio").GetComponent<AudioSource>();
            float pendingDelay = Mathf.Max(0f, weapons.Definition.ReloadDuration - pendingAudio.clip.length - 0.05f);
            actions.EquipWeapon(1);
            // Check during the interval that would be audible without cancellation.
            yield return new WaitForSeconds(pendingDelay + 0.08f);
            Check(!pendingAudio.isPlaying && pendingAudio.timeSamples == 0,
                "weapon switch cancels pending shotgun foley");
            for (int i = 0; i < 3; i++)
            {
                var other = marco.GetComponent<PlayerWeaponController>();
                Check(other.TryEquip(i) && other.AnimationWeapon == CharacterWeapons.State(i, false) && other.Definition == WeaponDefinition.At(i), "Marco retains original slot " + i);
            }
            Result = assertions + " runtime assertions passed; real wallet untouched.";
            Debug.Log(Result);
        }
        finally
        {
            Application.runInBackground = previousBackground;
            scope.Dispose();
            if (cesar != null) UnityEngine.Object.Destroy(cesar);
            if (marco != null) UnityEngine.Object.Destroy(marco);
            foreach (var projectile in UnityEngine.Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if (projectile.transform.position.x > 900) UnityEngine.Object.Destroy(projectile.gameObject);
            UnityEngine.Object.Destroy(host);
        }
    }
}
