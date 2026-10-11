using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shows a small stage 5 preview, then creates the remaining wave gradually.</summary>
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
    [SerializeField, Min(1)] private int introEnemyCount = 3;
    [SerializeField, Min(0.5f)] private float spawnSpacing = 3f;
    [SerializeField] private float groundY = -1.88f;
    [SerializeField, Min(0f)] private float laneOffset = 0.2f;

    [Header("Wave release")]
    [SerializeField, Min(0.1f)] private float delayBetweenSpawns = 3.2f;
    [SerializeField, Min(0.5f)] private float minimumSpawnClearance = 3f;
    [SerializeField] private float entranceX = -6.72f;

    private readonly List<BasicEnemy> queuedEnemies = new();
    private List<GameObject> spawnOrder;
    private float objectiveX;
    private float reinforcementSpawnX;
    private bool prepared;
    private bool waveStarted;
    private bool releaseFinished;
    private readonly HashSet<EnemyHealth> livingEnemies = new();
    public int SelectedLevel { get; private set; }
    public int TotalEnemies { get; private set; }
    public int PendingEnemies { get; private set; }
    public int AliveEnemies => livingEnemies.Count;

    /// <summary>Camera X that centers the visible lineup on stage 5.</summary>
    public float IntroCameraX { get; private set; }

    /// <summary>Horizontal width occupied by the visible enemy lineup.</summary>
    public float LineupWidth => Mathf.Max(0, queuedEnemies.Count - 1) * spawnSpacing;

    /// <summary>Creates only the intro enemies, keeping the rest as pending prefab entries.</summary>
    public bool PrepareWave(int selectedLevel)
    {
        if (prepared)
            return TotalEnemies > 0;

        EnsureCompositionDefaults();
        int compositionIndex = ProgressionService.IsLevelUnlocked(selectedLevel)
            ? Mathf.Clamp(selectedLevel, 0, levelCompositions.Length - 1) : 0;
        SelectedLevel = compositionIndex;
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
        TotalEnemies = total;
        PendingEnemies = total;
        spawnOrder = BuildInterleavedSpawnOrder(prefabs, counts);
        PlayerActions startingPlayer = FindFirstObjectByType<PlayerActions>();
        objectiveX = startingPlayer != null
            ? startingPlayer.transform.position.x
            : entranceX;

        int previewCount = Mathf.Clamp(introEnemyCount, 1, spawnOrder.Count);
        for (int i = 0; i < previewCount; i++)
        {
            queuedEnemies.Add(CreateEnemy(i, firstSpawnX + i * spawnSpacing, false));
        }

        reinforcementSpawnX = firstSpawnX + (previewCount - 1) * spawnSpacing;
        IntroCameraX = firstSpawnX + (queuedEnemies.Count - 1) * spawnSpacing * 0.5f;
        prepared = true;
        return true;
    }

    private BasicEnemy CreateEnemy(int index, float spawnX, bool startMoving)
    {
        GameObject prefab = spawnOrder[index];
        float lane = ((index % 3) - 1) * laneOffset;
        Vector3 position = new(spawnX, groundY + lane, 0f);
        GameObject enemy = Instantiate(prefab, position, prefab.transform.rotation, transform);
        enemy.name = $"{prefab.name}_Level_{SelectedLevel + 1}_Wave_{index + 1:00}";

        BasicEnemy behavior = enemy.GetComponent<BasicEnemy>();
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        livingEnemies.Add(enemyHealth);
        enemyHealth.Died += OnEnemyDied;
        behavior.SetEntranceTargetX(objectiveX);
        behavior.enabled = startMoving;
        return behavior;
    }

    private bool IsReinforcementSpawnClear()
    {
        foreach (EnemyHealth enemy in livingEnemies)
        {
            if (enemy != null && Mathf.Abs(enemy.transform.position.x - reinforcementSpawnX) < minimumSpawnClearance)
                return false;
        }
        return true;
    }

    /// <summary>Releases the preview, then spawns one reinforcement at a time.</summary>
    public void BeginWave()
    {
        if (!prepared || waveStarted)
            return;

        waveStarted = true;
        StartCoroutine(ReleaseWave());
    }

    private IEnumerator ReleaseWave()
    {
        for (int i = 0; i < spawnOrder.Count; i++)
        {
            if (GameFlowController.IsDefeatActive)
                yield break;

            if (i < queuedEnemies.Count)
            {
                if (queuedEnemies[i] != null)
                    queuedEnemies[i].enabled = true;
            }
            else
            {
                while (!IsReinforcementSpawnClear())
                {
                    if (GameFlowController.IsDefeatActive)
                        yield break;
                    yield return null;
                }

                // Defeat can occur on the frame the spawn point becomes free.
                if (GameFlowController.IsDefeatActive)
                    yield break;
                CreateEnemy(i, reinforcementSpawnX, true);
            }
            PendingEnemies--;

            if (i + 1 < spawnOrder.Count)
                yield return new WaitForSeconds(Mathf.Max(0.1f, delayBetweenSpawns));
        }
        releaseFinished = true;
        CheckWaveComplete();
    }

    private void OnEnemyDied(EnemyHealth enemy)
    {
        if (!livingEnemies.Remove(enemy)) return;
        enemy.Died -= OnEnemyDied;
        GameFlowController.ReportEnemyKilled();
        CheckWaveComplete();
    }

    private void CheckWaveComplete()
    {
        if (prepared && waveStarted && releaseFinished && PendingEnemies == 0 && livingEnemies.Count == 0)
            GameFlowController.ReportWaveCleared(SelectedLevel);
    }

    private void OnDestroy()
    {
        foreach (EnemyHealth enemy in livingEnemies)
            if (enemy != null) enemy.Died -= OnEnemyDied;
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
