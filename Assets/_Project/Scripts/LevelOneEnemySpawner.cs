using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shows a small stage 5 preview, then creates the remaining wave gradually.</summary>
public sealed class LevelOneEnemySpawner : MonoBehaviour
{
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
        new WaveComposition { enemy2Count = 11, enemy3Count = 3 },
        new WaveComposition { enemy3Count = 10, enemy4Count = 8 },
        new WaveComposition { enemy1Count = 8, enemy2Count = 6, enemy3Count = 5, enemy4Count = 5 }
    };

    [Header("Stage 5 lineup")]
    [SerializeField] private float firstSpawnX = 93f;
    [SerializeField, Min(1)] private int introEnemyCount = 3;
    [SerializeField, Min(0.5f)] private float spawnSpacing = 3f;
    [Tooltip("World Y of the street surface, shared by every enemy's visible soles.")]
    [SerializeField] private float groundY = -4.44f;

    [Header("Wave release")]
    [SerializeField, Min(0.1f)] private float delayBetweenSpawns = 3.2f;
    [SerializeField, Min(0.5f)] private float minimumSpawnClearance = 3f;
    [SerializeField] private float entranceX = -6.72f;

    [Header("Level 2: gradual shield introduction")]
    [SerializeField, Min(1)] private int levelTwoEnemiesBetweenShields = 3;
    [SerializeField, Min(3)] private int levelTwoMaximumAlive = 6;
    [SerializeField, Min(0.1f)] private float levelTwoSpawnDelay = 3.6f;
    [SerializeField, Min(0.1f)] private float levelTwoShieldDelay = 5.5f;
    [SerializeField, Min(1)] private int levelTwoHitsToBreakShield = 4;
    [SerializeField, Min(0.1f)] private float levelTwoShieldBreakDuration = 2.5f;

    [Header("Level 3: shield and SMG encounters")]
    [SerializeField, Min(3)] private int levelThreeMaximumAlive = 7;
    [SerializeField, Min(0.1f)] private float levelThreeSpawnDelay = 3.2f;
    [SerializeField, Min(0.1f)] private float levelThreeShieldDelay = 4.8f;
    [SerializeField, Min(0.1f)] private float levelThreeSMGDelay = 4.5f;
    [SerializeField, Min(1)] private int levelThreeHitsToBreakShield = 5;
    [SerializeField, Min(0.1f)] private float levelThreeShieldBreakDuration = 2f;

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

    private int MaximumAlive => SelectedLevel == 1 ? Mathf.Max(3, levelTwoMaximumAlive)
        : SelectedLevel == 2 ? Mathf.Max(3, levelThreeMaximumAlive) : int.MaxValue;

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
        int[] counts = RestrictCountsToLevel(compositionIndex, new[]
        {
            composition.enemy1Count,
            composition.enemy2Count,
            composition.enemy3Count,
            composition.enemy4Count
        });
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

            if (prefabs[type].GetComponent<BasicEnemy>() == null ||
                prefabs[type].GetComponent<EnemyHealth>() == null)
            {
                Debug.LogError($"The {enemyNames[type]} prefab needs BasicEnemy and EnemyHealth components.", prefabs[type]);
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
        spawnOrder = SelectedLevel == 1
            ? BuildLevelTwoSpawnOrder(prefabs, counts)
            : BuildInterleavedSpawnOrder(prefabs, counts);
        PlayerActions startingPlayer = FindFirstObjectByType<PlayerActions>();
        objectiveX = startingPlayer != null
            ? startingPlayer.transform.position.x
            : entranceX;

        // Both level-specific types must be visible before the camera returns
        // to the player, even if an older scene requested only one intro actor.
        int minimumPreviewCount = SelectedLevel <= 2 ? Mathf.Min(2, spawnOrder.Count) : 1;
        int previewCount = Mathf.Clamp(introEnemyCount, minimumPreviewCount, spawnOrder.Count);
        previewCount = Mathf.Min(previewCount, MaximumAlive);
        for (int i = 0; i < previewCount; i++)
        {
            queuedEnemies.Add(CreateEnemy(i, firstSpawnX + i * spawnSpacing, false));
        }

        reinforcementSpawnX = firstSpawnX + (previewCount - 1) * spawnSpacing;
        IntroCameraX = firstSpawnX + (queuedEnemies.Count - 1) * spawnSpacing * 0.5f;
        prepared = true;
        StartCoroutine(CombatWarmup.Prepare());
        return true;
    }

    private BasicEnemy CreateEnemy(int index, float spawnX, bool startMoving)
    {
        GameObject prefab = spawnOrder[index];
        Vector3 position = new(spawnX, groundY, 0f);
        GameObject enemy = Instantiate(prefab, position, prefab.transform.rotation, transform);
        enemy.name = $"{prefab.name}_Level_{SelectedLevel + 1}_Wave_{index + 1:00}";

        BasicEnemy behavior = enemy.GetComponent<BasicEnemy>();
        behavior.PlaceOnGround(groundY);
        EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
        if (SelectedLevel == 1 && prefab == enemy3Prefab)
            enemyHealth.ConfigureShieldOpening(levelTwoHitsToBreakShield, levelTwoShieldBreakDuration);
        else if (SelectedLevel == 2 && prefab == enemy3Prefab)
            enemyHealth.ConfigureShieldOpening(levelThreeHitsToBreakShield, levelThreeShieldBreakDuration);
        livingEnemies.Add(enemyHealth);
        enemyHealth.Died += OnEnemyDied;
        behavior.SetEntranceTargetX(objectiveX);
        if (SelectedLevel <= 2) behavior.ConfigureTactics();
        behavior.enabled = startMoving;
        return behavior;
    }

    private bool IsReinforcementSpawnClear()
    {
        // Pending preview actors are released before this gate is used. Never count
        // uninstantiated reinforcements as alive, or the wave could deadlock.
        if (livingEnemies.Count >= MaximumAlive)
            return false;

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
                yield return new WaitForSeconds(GetReleaseDelay(i));
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

    private float GetReleaseDelay(int index)
    {
        if (SelectedLevel == 2)
        {
            float interval = Mathf.Max(0.1f, levelThreeSpawnDelay);
            if (spawnOrder[index] == enemy3Prefab)
                return Mathf.Max(interval, levelThreeShieldDelay);
            if (spawnOrder[index] == enemy4Prefab)
                return Mathf.Max(interval, levelThreeSMGDelay);
            return interval;
        }

        if (SelectedLevel != 1)
            return Mathf.Max(0.1f, delayBetweenSpawns);

        float delay = Mathf.Max(0.1f, levelTwoSpawnDelay);
        return spawnOrder[index] == enemy3Prefab
            ? Mathf.Max(delay, levelTwoShieldDelay) : delay;
    }

    private List<GameObject> BuildLevelTwoSpawnOrder(GameObject[] prefabs, int[] counts)
    {
        int[] remaining = (int[])counts.Clone();
        List<GameObject> order = new(counts[0] + counts[1] + counts[2] + counts[3]);
        int ordinarySinceShield = 0;
        int nextOrdinaryType = 0;
        bool introducedShield = false;

        while (remaining[0] + remaining[1] + remaining[2] + remaining[3] > 0)
        {
            int ordinaryLeft = remaining[0] + remaining[1] + remaining[3];
            int requiredGap = introducedShield
                ? Mathf.Max(1, levelTwoEnemiesBetweenShields) : 1;
            if (remaining[2] > 0 && (ordinaryLeft == 0 || ordinarySinceShield >= requiredGap))
            {
                order.Add(prefabs[2]);
                remaining[2]--;
                ordinarySinceShield = 0;
                introducedShield = true;
                continue;
            }

            // Introduce the shield in the preview, then leave room between
            // shield reinforcements throughout the rest of the wave.
            for (int attempt = 0; attempt < 4; attempt++)
            {
                int type = nextOrdinaryType;
                nextOrdinaryType = (nextOrdinaryType + 1) % 4;
                if (type == 2 || remaining[type] <= 0) continue;
                order.Add(prefabs[type]);
                remaining[type]--;
                ordinarySinceShield++;
                break;
            }
        }
        return order;
    }

    private static int[] RestrictCountsToLevel(int level, int[] configuredCounts)
    {
        int[] counts = (int[])configuredCounts.Clone();
        for (int type = 0; type < counts.Length; type++)
            counts[type] = Mathf.Max(0, counts[type]);

        // Enforce the roster for previews AND reinforcements. Remap old scene
        // counts to the allowed pair so saved Inspector overrides retain the
        // full wave size without bringing back enemies from another level.
        switch (level)
        {
            case 0:
                counts[0] += counts[2];
                counts[1] += counts[3];
                counts[2] = counts[3] = 0;
                break;
            case 1:
                counts[1] += counts[0];
                counts[2] += counts[3];
                counts[0] = counts[3] = 0;
                break;
            case 2:
                counts[2] += counts[0];
                counts[3] += counts[1];
                counts[0] = counts[1] = 0;
                break;
        }
        return counts;
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
            new WaveComposition { enemy2Count = 11, enemy3Count = 3 },
            new WaveComposition { enemy3Count = 10, enemy4Count = 8 },
            new WaveComposition { enemy1Count = 8, enemy2Count = 6, enemy3Count = 5, enemy4Count = 5 }
        };
    }
}
