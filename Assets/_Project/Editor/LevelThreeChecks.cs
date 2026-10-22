using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Verifies level-three pacing without changing the active scene or real profile.</summary>
public static class LevelThreeChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Lindavista/Verify level 3 configuration")]
    public static void Verify() => Debug.Log(RunAssets());

    public static string RunAssets()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Run outside Play Mode; the active match is not modified.");

        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Gameplay.unity");
        int assertions = 0;
        void Check(bool valid, string label)
        {
            assertions++;
            if (!valid) throw new Exception("Level 3: " + label);
        }
        object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        try
        {
            var spawner = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<LevelOneEnemySpawner>(true)).Single();
            var prefabs = Enumerable.Range(1, 4)
                .Select(i => (GameObject)Get(spawner, "enemy" + i + "Prefab")).ToArray();
            foreach (var prefab in prefabs)
                Check(prefab != null && prefab.GetComponent<BasicEnemy>() != null &&
                    prefab.GetComponent<EnemyHealth>() != null, "usable enemy prefab");

            var compositions = (Array)Get(spawner, "levelCompositions");
            var composition = compositions.GetValue(2);
            int[] configured = Enumerable.Range(1, 4).Select(i =>
                (int)composition.GetType().GetField("enemy" + i + "Count").GetValue(composition)).ToArray();
            Check(configured.SequenceEqual(new[] { 0, 0, 10, 8 }), "18 enemies from the level-three roster");
            var method = typeof(LevelOneEnemySpawner).GetMethod("BuildInterleavedSpawnOrder", Private);
            var order = (List<GameObject>)method.Invoke(spawner, new object[] { prefabs, configured });
            Check(order.Count == 18, "complete level-three wave");
            Check(order[0] == prefabs[2] && order[1] == prefabs[3] && order[2] == prefabs[2],
                "intro preview shows shield and SMG enemies");
            Check(order.All(p => p == prefabs[2] || p == prefabs[3]), "no level-one or level-two enemies");
            for (int i = 0; i < 16; i++)
                Check(order[i] == prefabs[2 + i % 2], "shield and SMG alternate while both remain");

            foreach (int[] counts in new[] { configured, new[] { 0, 0, 0, 0 }, new[] { 0, 0, 0, 5 },
                new[] { 0, 0, 6, 0 }, new[] { 2, 0, 1, 1 }, new[] { 12, 8, 7, 6 } })
            {
                int[] original = (int[])counts.Clone();
                var customOrder = (List<GameObject>)method.Invoke(spawner, new object[] { prefabs, counts });
                Check(counts.SequenceEqual(original), "configuration never consumed while building order");
                Check(customOrder.Count == counts.Sum(), "custom composition completes");
                for (int type = 0; type < 4; type++)
                    Check(customOrder.Count(p => p == prefabs[type]) == counts[type], "custom type count retained");
            }

            Check((int)Get(spawner, "levelThreeMaximumAlive") == 7, "seven concurrent enemies");
            Check((int)Get(spawner, "levelTwoMaximumAlive") == 6, "level-two cap unchanged");
            Check((float)Get(spawner, "levelThreeSpawnDelay") == 3.2f, "regular level-three release interval");
            Check((float)Get(spawner, "levelThreeShieldDelay") > (float)Get(spawner, "levelThreeSpawnDelay"), "shield recovery gap");
            Check((float)Get(spawner, "levelThreeSMGDelay") > (float)Get(spawner, "levelThreeSpawnDelay"), "SMG recovery gap");
            Check((int)Get(spawner, "levelThreeHitsToBreakShield") == 5, "intermediate shield difficulty");
            Check((float)Get(spawner, "levelThreeShieldBreakDuration") == 2f, "two-second opening");
            Check((int)Get(spawner, "levelTwoHitsToBreakShield") == 4 &&
                (float)Get(spawner, "levelTwoShieldBreakDuration") == 2.5f, "level-two shield unchanged");
            var shield = prefabs[2].GetComponent<EnemyHealth>();
            Check((int)Get(shield, "hitsToBreakShield") == 6 &&
                (float)Get(shield, "shieldBreakDuration") == 1.5f, "level-four and Testing prefab unchanged");
            var gunner = prefabs[3].GetComponent<BasicEnemy>();
            Check((int)Get(gunner, "damage") == 1 && (int)Get(gunner, "shotsPerAttack") == 3 &&
                (float)Get(gunner, "attackCooldown") == 2f, "SMG attack stats not inflated");

            var repository = new ProgressionRepository(Path.Combine("Temp", "LevelThreeChecks-" + Guid.NewGuid().ToString("N") + ".json"));
            Check(!repository.IsLevelUnlocked(2), "fresh profile cannot skip to level three");
            Check(repository.TryCompleteLevel(0) && repository.TryCompleteLevel(1), "earlier levels unlock level three");
            Check(repository.IsLevelUnlocked(2) && !repository.IsLevelUnlocked(3), "level four stays locked");
            Check(repository.TryCompleteLevel(2) && repository.IsLevelUnlocked(3), "level-three victory unlocks level four");
            return "Level 3 configuration passed: " + assertions + " assertions; scene and player profile untouched.";
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }
}
