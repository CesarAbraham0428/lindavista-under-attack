using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Read-only asset checks. Does not load a match, unlock levels or touch saves.</summary>
public static class LevelFourChecks
{
    [MenuItem("Tools/Lindavista/Verify level 4 configuration")]
    public static void Verify() => Debug.Log(RunAssets());

    public static string RunAssets()
    {
        if (Application.isPlaying) throw new InvalidOperationException("Verify outside Play Mode.");
        int checks = 0;
        void Check(bool ok, string name) { checks++; if (!ok) throw new Exception("Level 4: " + name); }
        object Get(object o, string name) => o.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(o);
        var art = AssetDatabase.LoadAssetAtPath<LevelFourAssets>(LevelFourAssetSetup.AssetPath);
        Check(art != null && art.Portrait != null && art.TruckPrefab != null, "serialized build-safe references");
        Check(AssetDatabase.GetAssetPath(art.Portrait).EndsWith("Boss_Base.png"), "original portrait preserved");
        var truck = art.TruckPrefab.GetComponent<BossTruck>();
        var health = art.TruckPrefab.GetComponent<EnemyHealth>();
        Check(truck != null && health != null, "truck behavior and damage receiver");
        Check(health.MaxHealth == 300, "greater configurable boss health");
        Check(!(bool)Get(health, "playDamageAnimation"), "hit reactions do not show defeat art");
        Check((int)Get(truck, "damage") == 1 && (int)Get(truck, "shotsPerBurst") == 3, "bounded burst damage");
        Check((float)Get(truck, "warningDuration") >= .8f && (float)Get(truck, "recoveryDuration") >= 2f, "readable warning and recovery");
        var animator = art.TruckPrefab.GetComponent<Animator>();
        Check(animator != null && animator.runtimeAnimatorController != null, "existing animation controller");
        foreach (string name in new[] { "BossTruck_Idle", "BossTruck_Walk", "BossTruck_Fire", "BossTruck_Defeat" })
            Check(animator.runtimeAnimatorController.animationClips.Any(c => c.name == name), name + " present");
        Check(art.TruckPrefab.GetComponent<Collider2D>().isTrigger, "projectile damage collider");
        Check(BossIntroduction.Lines.Length == 4 && BossIntroduction.Lines.All(s => s.EndsWith(".")), "four sentences");
        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Gameplay.unity");
        try
        {
            var spawner = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<LevelOneEnemySpawner>(true)).Single();
            var compositions = (Array)Get(spawner, "levelCompositions");
            var final = compositions.GetValue(3);
            Check(Enumerable.Range(1, 4).Select(i => (int)final.GetType().GetField("enemy" + i + "Count").GetValue(final))
                .SequenceEqual(new[] { 8, 6, 5, 5 }), "all 24 original level-four enemies preserved");
            for (int i = 1; i <= 4; i++) Check(Get(spawner, "enemy" + i + "Prefab") != null, "enemy type " + i);
            Check((int)Get(spawner, "levelFourMaximumAlive") == 7, "bounded simultaneous combat");
            Check((float)Get(spawner, "levelFourBossArrivalDelay") >= 1f, "separation before final arrival");
            Check((int)Get(spawner, "levelTwoMaximumAlive") == 6 && (int)Get(spawner, "levelThreeMaximumAlive") == 7,
                "earlier levels unchanged");
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
        return $"Level 4 configuration passed: {checks} assertions; scene and real profile untouched.";
    }
}
