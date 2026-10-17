using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks the real level configuration in a preview scene and an isolated save file.</summary>
public static class LevelTwoChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Lindavista/Verify level 2 configuration")]
    public static void Verify() => Debug.Log(RunAssets());

    public static string RunAssets()
    {
        if (Application.isPlaying)
            throw new InvalidOperationException("Run outside Play Mode; no active match is modified.");

        var scene = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/Gameplay.unity");
        int assertions = 0;
        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Level 2: " + message);
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
                    prefab.GetComponent<EnemyHealth>() != null, "complete enemy prefab");

            var compositions = (Array)Get(spawner, "levelCompositions");
            int[][] expected = { new[] { 6, 4, 0, 0 }, new[] { 0, 11, 3, 0 },
                new[] { 0, 0, 10, 8 }, new[] { 8, 6, 5, 5 } };
            for (int level = 0; level < 4; level++)
            {
                var composition = compositions.GetValue(level);
                int[] counts = Enumerable.Range(1, 4).Select(i =>
                    (int)composition.GetType().GetField("enemy" + i + "Count").GetValue(composition)).ToArray();
                Check(counts.SequenceEqual(expected[level]), "composition preserved for level " + (level + 1));
                string method = level == 1 ? "BuildLevelTwoSpawnOrder" : "BuildInterleavedSpawnOrder";
                var order = (List<GameObject>)typeof(LevelOneEnemySpawner).GetMethod(method, Private)
                    .Invoke(spawner, new object[] { prefabs, counts });
                Check(order.Count == counts.Sum(), "full wave for level " + (level + 1));
                for (int type = 0; type < 4; type++)
                    Check(order.Count(p => p == prefabs[type]) == counts[type], "exact enemy count");
                if (level <= 2)
                {
                    Check(order[0] == prefabs[level] && order[1] == prefabs[level + 1],
                        "intro previews the matching pair for level " + (level + 1));
                    Check(order.All(p => p == prefabs[level] || p == prefabs[level + 1]),
                        "preview and every reinforcement obey the level roster");
                }
                else
                    Check(order[0] == prefabs[0] && order[1] == prefabs[1], "level-four opening preserved");
                if (level == 1)
                {
                    Check(order[1] == prefabs[2] && order[5] == prefabs[2] && order[9] == prefabs[2],
                        "shield introductions spaced throughout the wave");
                }
            }

            var restrictCounts = typeof(LevelOneEnemySpawner).GetMethod("RestrictCountsToLevel",
                BindingFlags.Static | BindingFlags.NonPublic);
            int[][] legacy = { new[] { 6, 4, 2, 1 }, new[] { 6, 5, 3, 0 },
                new[] { 6, 5, 4, 3 }, new[] { 8, 6, 5, 5 } };
            for (int level = 0; level < legacy.Length; level++)
            {
                int[] original = (int[])legacy[level].Clone();
                var counts = (int[])restrictCounts.Invoke(null, new object[] { level, legacy[level] });
                Check(legacy[level].SequenceEqual(original), "roster filtering preserves serialized counts");
                Check(counts.Sum() == original.Sum(), "old configurations retain wave size");
                if (level <= 2)
                    Check(Enumerable.Range(0, 4).All(type => type == level || type == level + 1 || counts[type] == 0),
                        "stale Inspector overrides cannot introduce a forbidden type");
                else
                    Check(counts.SequenceEqual(original), "level-four roster unchanged");
            }

            foreach (int[] counts in new[] { new[] { 0, 0, 3, 0 }, new[] { 0, 2, 0, 0 },
                new[] { 1, 0, 4, 2 }, new[] { 0, 0, 0, 0 } })
            {
                int[] original = (int[])counts.Clone();
                var order = (List<GameObject>)typeof(LevelOneEnemySpawner).GetMethod("BuildLevelTwoSpawnOrder", Private)
                    .Invoke(spawner, new object[] { prefabs, counts });
                Check(counts.SequenceEqual(original), "building order never consumes configured counts");
                Check(order.Count == counts.Sum(), "custom composition completes without losing actors");
                for (int type = 0; type < 4; type++)
                    Check(order.Count(p => p == prefabs[type]) == counts[type], "custom type count preserved");
            }

            Check((int)Get(spawner, "levelTwoMaximumAlive") == 6, "six-enemy concurrent limit");
            Check((float)Get(spawner, "delayBetweenSpawns") == 3.2f, "level-one release timing unchanged");
            Check((float)Get(spawner, "levelTwoSpawnDelay") >= 3.2f, "level-two pacing allows reaction time");
            Check((float)Get(spawner, "levelTwoShieldDelay") > (float)Get(spawner, "levelTwoSpawnDelay"),
                "extra breathing space after shields");
            Check((int)Get(spawner, "levelTwoHitsToBreakShield") == 4, "four-hit level-two opening");
            Check((float)Get(spawner, "levelTwoShieldBreakDuration") == 2.5f, "visible level-two vulnerability");
            var shield = prefabs[2].GetComponent<EnemyHealth>();
            Check((int)Get(shield, "hitsToBreakShield") == 6 &&
                (float)Get(shield, "shieldBreakDuration") == 1.5f, "shared shield prefab unchanged");
            Check((int)Get(shield, "maxHealth") == 30, "shield enemy health unchanged");

            var savePath = Path.Combine("Temp", "LevelTwoChecks-" + Guid.NewGuid().ToString("N") + ".json");
            var repository = new ProgressionRepository(savePath);
            Check(!repository.IsLevelUnlocked(1), "new profile cannot skip level one");
            Check(repository.TryCompleteLevel(0) && repository.IsLevelUnlocked(1), "level one unlocks level two");
            Check(repository.TryCompleteLevel(1) && repository.IsLevelUnlocked(2), "level two unlocks level three");
            var reloaded = new ProgressionRepository(savePath);
            Check(reloaded.IsLevelUnlocked(2) && !reloaded.IsLevelUnlocked(3), "unlock survives save/reload without skipping levels");
            return "Level 2 configuration passed: " + assertions + " assertions; real profile and active scene untouched.";
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }
}
