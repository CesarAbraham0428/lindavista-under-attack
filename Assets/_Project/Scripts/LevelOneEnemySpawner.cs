using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Builds the stage 5 enemy lineup and releases it toward Lindavista over time.</summary>
public sealed class LevelOneEnemySpawner : MonoBehaviour
{
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";

    [Serializable]
    private sealed class WaveComposition
    {
        [Min(0)] public int enemy1Count;
        [Min(0)] public int enemy2Count;
        [Min(0)] public int enemy3Count;
        [Min(0)] public int enemy4Count;
    }

    [Header("Enemy prefabs")]
    [SerializeField] private GameObject enemy1Prefab;
    [SerializeField] private GameObject enemy2Prefab;
    [SerializeField] private GameObject enemy3Prefab;
    [SerializeField] private GameObject enemy4Prefab;

    [Header("Enemy counts by selected level (levels 1 to 4)")]
    [SerializeField] private WaveComposition[] levelCompositions =
    {
        new WaveComposition { enemy1Count = 6, enemy2Count = 4 },
        new WaveComposition { enemy1Count = 6, enemy2Count = 5, enemy3Count = 3 },
        new WaveComposition { enemy1Count = 6, enemy2Count = 5, enemy3Count = 4, enemy4Count = 3 },
        new WaveComposition { enemy1Count = 8, enemy2Count = 6, enemy3Count = 5, enemy4Count = 5 }
    };

    [Header("Stage 5 lineup")]
    [SerializeField] private float firstSpawnX = 93f;
    [SerializeField, Min(0.5f)] private float spawnSpacing = 1.2f;
    [SerializeField] private float groundY = -1.88f;
    [SerializeField, Min(0f)] private float laneOffset = 0.2f;

    [Header("Wave release")]
    [SerializeField, Min(0f)] private float delayBetweenSpawns = 1.65f;
    [SerializeField] private float entranceX = -6.72f;

    private readonly List<BasicEnemy> queuedEnemies = new();
    private bool prepared;
    private bool waveStarted;

    /// <summary>Camera X that centers the visible lineup on stage 5.</summary>
    public float IntroCameraX { get; private set; }

    /// <summary>Horizontal width occupied by the visible enemy lineup.</summary>
    public float LineupWidth => Mathf.Max(0, queuedEnemies.Count - 1) * spawnSpacing;

    /// <summary>Creates the selected level's visible lineup with its movement AI held.</summary>
    public bool PrepareWave(int selectedLevel)
    {
        if (prepared)
            return queuedEnemies.Count > 0;

        EnsureCompositionDefaults();
        int compositionIndex = Mathf.Clamp(selectedLevel, 0, levelCompositions.Length - 1);
        WaveComposition composition = levelCompositions[compositionIndex];
        int[] counts =
        {
            composition.enemy1Count,
            composition.enemy2Count,
            composition.enemy3Count,
            composition.enemy4Count
        };
        GameObject[] prefabs = { enemy1Prefab, enemy2Prefab, enemy3Prefab, enemy4Prefab };
        string[] enemyNames = { "Enemy 1", "Enemy 2", "Enemy 3", "Enemy 4" };

        for (int type = 0; type < counts.Length; type++)
        {
            if (counts[type] <= 0)
                continue;

            if (prefabs[type] == null)
            {
                Debug.LogError($"Level {compositionIndex + 1} needs a prefab for {enemyNames[type]}.", this);
                return false;
            }

            if (prefabs[type].GetComponent<BasicEnemy>() == null)
            {
                Debug.LogError($"The {enemyNames[type]} prefab needs a BasicEnemy component.", prefabs[type]);
                return false;
            }
        }

        int total = counts[0] + counts[1] + counts[2] + counts[3];
        if (total <= 0)
        {
            Debug.LogError($"Level {compositionIndex + 1} has no enemies configured.", this);
            return false;
        }

        HideLegacyEnemyPreviews();
        List<GameObject> spawnOrder = BuildInterleavedSpawnOrder(prefabs, counts);

        for (int i = 0; i < spawnOrder.Count; i++)
        {
            GameObject prefab = spawnOrder[i];
            float lane = ((i % 3) - 1) * laneOffset;
            Vector3 position = new(firstSpawnX + i * spawnSpacing, groundY + lane, 0f);
            GameObject enemy = Instantiate(prefab, position, prefab.transform.rotation, transform);
            enemy.name = $"{prefab.name}_Level_{compositionIndex + 1}_Wave_{i + 1:00}";

            BasicEnemy behavior = enemy.GetComponent<BasicEnemy>();
            behavior.SetEntranceTargetX(entranceX);
            behavior.enabled = false;
            queuedEnemies.Add(behavior);
        }

        IntroCameraX = firstSpawnX + (queuedEnemies.Count - 1) * spawnSpacing * 0.5f;
        prepared = true;
        return true;
    }

    /// <summary>Starts releasing one prepared enemy at a time toward the entrance.</summary>
    public void BeginWave()
    {
        if (!prepared || waveStarted)
            return;

        waveStarted = true;
        StartCoroutine(ReleaseWave());
    }

    private IEnumerator ReleaseWave()
    {
        for (int i = 0; i < queuedEnemies.Count; i++)
        {
            if (queuedEnemies[i] != null)
                queuedEnemies[i].enabled = true;

            if (i + 1 < queuedEnemies.Count && delayBetweenSpawns > 0f)
                yield return new WaitForSeconds(delayBetweenSpawns);
        }
    }

    private List<GameObject> BuildInterleavedSpawnOrder(GameObject[] prefabs, int[] counts)
    {
        int total = counts[0] + counts[1] + counts[2] + counts[3];
        int largestCount = Mathf.Max(Mathf.Max(counts[0], counts[1]), Mathf.Max(counts[2], counts[3]));
        List<GameObject> order = new(total);

        for (int round = 0; round < largestCount; round++)
        {
            for (int type = 0; type < counts.Length; type++)
            {
                if (round < counts[type])
                    order.Add(prefabs[type]);
            }
        }

        return order;
    }

    private void HideLegacyEnemyPreviews()
    {
        GameObject previewRoot = GameObject.Find("Enemigos");
        if (previewRoot != null && previewRoot != gameObject)
            previewRoot.SetActive(false);
    }

    private void EnsureCompositionDefaults()
    {
        if (levelCompositions != null && levelCompositions.Length >= 4)
            return;

        levelCompositions = new[]
        {
            new WaveComposition { enemy1Count = 6, enemy2Count = 4 },
            new WaveComposition { enemy1Count = 6, enemy2Count = 5, enemy3Count = 3 },
            new WaveComposition { enemy1Count = 6, enemy2Count = 5, enemy3Count = 4, enemy4Count = 3 },
            new WaveComposition { enemy1Count = 8, enemy2Count = 6, enemy3Count = 5, enemy4Count = 5 }
        };
    }
}
