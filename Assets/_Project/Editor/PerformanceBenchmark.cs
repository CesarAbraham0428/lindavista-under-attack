using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using Unity.Profiling;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.SceneManagement;

/// <summary>
/// Repeatable Play Mode combat capture for before/after comparisons. Editor numbers only:
/// they include editor overhead and never replace a Development Build on a device.
/// Usage (Play Mode): AndroidValidationSession.Begin(); PerformanceBenchmark.Start(3, false, 30f, "antes");
/// then poll PerformanceBenchmark.Status until it reports DONE, and read PerformanceBenchmark.Report.
/// </summary>
public static class PerformanceBenchmark
{
    private enum Stage { Idle, Loading, Introduction, Warmup, Recording, Done }

    private sealed class Counter
    {
        public readonly string Label;
        public readonly bool Nanoseconds;
        public ProfilerRecorder Recorder;
        public Counter(ProfilerCategory category, string name, string label, bool nanoseconds, int capacity)
        {
            Label = label;
            Nanoseconds = nanoseconds;
            Recorder = ProfilerRecorder.StartNew(category, name, capacity);
        }
    }

    private const int Capacity = 6000;
    private const float WarmupSeconds = 2f;
    private const float WeaponCycleSeconds = 4f;
    private static readonly BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

    private static Stage stage;
    private static int level;
    private static bool cesar, forceBoss;
    private static float duration, stageStart, nextWeapon, nextSample;
    private static int weapon, startFrame, endFrame, sampleCount;
    private static string label = "";
    private static readonly List<Counter> counters = new();
    private static double overdrawSum, overdrawMax, renderersSum, effectsSum, enemiesSum;
    private static int maxEnemies, maxRenderers;
    private static string endReason = "";

    public static string Report { get; private set; } = "";
    public static string Status => stage + (stage == Stage.Recording
        ? $" {Time.realtimeSinceStartup - stageStart:F1}/{duration:F0}s" : "") + (endReason.Length > 0 ? " " + endReason : "") +
        (suite.Count > 0 ? $" ({suite.Count} pending)" : "");

    private static readonly Queue<(int level, bool cesar, bool boss)> suite = new();
    private static string suiteFile;
    private static float loadStart, loadSeconds;

    /// <summary>Four fixed scenarios in sequence; each report is appended to Logs/Rendimiento/{label}.md.</summary>
    public static string RunSuite(string runLabel, float seconds = 30f)
    {
        suite.Clear();
        suite.Enqueue((0, false, false));
        suite.Enqueue((2, true, false));
        suite.Enqueue((3, false, true));
        suite.Enqueue((3, true, true));
        suiteFile = System.IO.Path.Combine("Logs", "Rendimiento", runLabel + ".md");
        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(suiteFile));
        System.IO.File.WriteAllText(suiteFile, $"# {runLabel} ({DateTime.Now:yyyy-MM-dd HH:mm})\n\n");
        var first = suite.Dequeue();
        return Start(first.level, first.cesar, seconds, runLabel, first.boss);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (stage == Stage.Loading && scene.name == "Gameplay") loadSeconds = Time.realtimeSinceStartup - loadStart;
    }

    public static string Start(int selectedLevel, bool useCesar, float seconds, string runLabel, bool spawnBossImmediately = false)
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Enter Play Mode and AndroidValidationSession.Begin() first.");
        Stop();
        level = Mathf.Clamp(selectedLevel, 0, 3);
        cesar = useCesar;
        duration = Mathf.Max(5f, seconds);
        label = runLabel ?? "";
        forceBoss = spawnBossImmediately && level == 3;
        Report = "";
        endReason = "";
        overdrawSum = overdrawMax = renderersSum = effectsSum = enemiesSum = 0;
        maxEnemies = maxRenderers = sampleCount = 0;
        UnityEngine.Random.InitState(1234);
        loadSeconds = -1f;
        loadStart = Time.realtimeSinceStartup;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        AndroidValidationSession.PlayLevel(level, cesar);
        stage = Stage.Loading;
        stageStart = Time.realtimeSinceStartup;
        EditorApplication.update += Tick;
        return $"BENCHMARK_STARTED level={level + 1} cesar={cesar} seconds={duration} boss={forceBoss}";
    }

    public static void Stop()
    {
        EditorApplication.update -= Tick;
        AllocationProbe.End();
        DisposeCounters();
        stage = Stage.Idle;
    }

    private static void Tick()
    {
        if (!Application.isPlaying) { endReason = "play mode ended"; Stop(); return; }
        float elapsed = Time.realtimeSinceStartup - stageStart;
        PlayerActions player = FindPlayer();
        switch (stage)
        {
            case Stage.Loading:
                if (SceneManager.GetActiveScene().name == "Gameplay" && player != null && elapsed > 0.5f)
                    Advance(Stage.Introduction);
                break;
            case Stage.Introduction:
                // Level 4 waits for one tap per sentence; Advance() rate-limits itself.
                UnityEngine.Object.FindFirstObjectByType<BossIntroduction>()?.Advance();
                if (player != null && player.CanReceiveCombatInput && !player.IsInputLocked)
                {
                    PrepareScenario();
                    Advance(Stage.Warmup);
                }
                else if (elapsed > 60f) Finish("introduction timeout");
                break;
            case Stage.Warmup:
                Drive(player);
                if (elapsed >= WarmupSeconds)
                {
                    StartCounters();
                    AllocationProbe.Begin();
                    startFrame = Time.frameCount;
                    Advance(Stage.Recording);
                }
                break;
            case Stage.Recording:
                Drive(player);
                SampleScene();
                if (GameFlowController.IsMatchEnded) Finish("match ended early");
                else if (elapsed >= duration) Finish("completed");
                break;
        }
    }

    private static void Advance(Stage next)
    {
        stage = next;
        stageStart = Time.realtimeSinceStartup;
    }

    private static PlayerActions FindPlayer()
    {
        foreach (var candidate in UnityEngine.Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None))
            if (candidate.isActiveAndEnabled) return candidate;
        return null;
    }

    private static void PrepareScenario()
    {
        // Android parity: the touch HUD is visible in every capture.
        var hud = UnityEngine.Object.FindFirstObjectByType<MobileControlsHUD>();
        if (hud != null) typeof(MobileControlsHUD).GetMethod("SetVisible", Flags)?.Invoke(hud, new object[] { true });
        nextWeapon = Time.realtimeSinceStartup + WeaponCycleSeconds;
        weapon = 0;
        // Drop assets the editor (inspectors, earlier queries) loaded outside the level.
        Resources.UnloadUnusedAssets();
        if (!forceBoss) return;
        var spawner = UnityEngine.Object.FindFirstObjectByType<LevelOneEnemySpawner>();
        var routine = typeof(LevelOneEnemySpawner).GetMethod("SpawnFinalBoss", Flags);
        if (spawner != null && routine != null)
            spawner.StartCoroutine((System.Collections.IEnumerator)routine.Invoke(spawner, null));
    }

    private static void Drive(PlayerActions player)
    {
        if (player == null || GameFlowController.IsMatchEnded) return;
        // The capture measures sustained combat: keep the character alive and armed.
        PlayerHealth health = player.Health;
        if (health != null && health.CurrentHealth < health.MaxHealth)
            typeof(PlayerHealth).GetProperty("CurrentHealth").GetSetMethod(true).Invoke(health, new object[] { health.MaxHealth });
        for (int i = 0; i < 3; i++)
            if (ProgressionService.Ammo(i, cesar) < 40) ProgressionService.TryGrantAmmo(i, 200, cesar);
        if (Time.realtimeSinceStartup >= nextWeapon)
        {
            weapon = (weapon + 1) % 3;
            player.EquipWeapon(weapon);
            nextWeapon = Time.realtimeSinceStartup + WeaponCycleSeconds;
        }
        player.SetTouchAimAndFire(Vector2.right);
        // Never let a runner end the capture: an enemy behind the player is removed.
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (!enemy.IsDead && enemy.transform.position.x < player.transform.position.x - 2.5f)
                enemy.TakeExplosionDamage(9999);
    }

    private static void SampleScene()
    {
        if (Time.realtimeSinceStartup < nextSample) return;
        nextSample = Time.realtimeSinceStartup + 0.25f;
        Camera camera = Camera.main;
        if (camera == null || !camera.orthographic) return;
        float height = camera.orthographicSize * 2f, width = height * camera.aspect;
        Vector3 c = camera.transform.position;
        var view = new Rect(c.x - width * 0.5f, c.y - height * 0.5f, width, height);
        double covered = 0;
        int visible = 0;
        foreach (var renderer in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            Bounds b = renderer.bounds;
            float xMin = Mathf.Max(view.xMin, b.min.x), xMax = Mathf.Min(view.xMax, b.max.x);
            float yMin = Mathf.Max(view.yMin, b.min.y), yMax = Mathf.Min(view.yMax, b.max.y);
            if (xMax <= xMin || yMax <= yMin) continue;
            visible++;
            covered += (xMax - xMin) * (yMax - yMin);
        }
        double overdraw = covered / (width * height);
        int enemies = 0, effects = 0;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            if (!enemy.IsDead) enemies++;
        effects = UnityEngine.Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Length;
        sampleCount++;
        overdrawSum += overdraw;
        overdrawMax = Math.Max(overdrawMax, overdraw);
        renderersSum += visible;
        maxRenderers = Math.Max(maxRenderers, visible);
        enemiesSum += enemies;
        maxEnemies = Math.Max(maxEnemies, enemies);
        effectsSum += effects;
    }

    private static void StartCounters()
    {
        DisposeCounters();
        void Time(ProfilerCategory category, string name, string shortName) => counters.Add(new Counter(category, name, shortName, true, Capacity));
        void Value(ProfilerCategory category, string name, string shortName) => counters.Add(new Counter(category, name, shortName, false, Capacity));
        Time(ProfilerCategory.Internal, "PlayerLoop", "PlayerLoop");
        Time(ProfilerCategory.Scripts, "Update.ScriptRunBehaviourUpdate", "Update");
        Time(ProfilerCategory.Scripts, "PreLateUpdate.ScriptRunBehaviourLateUpdate", "LateUpdate");
        Time(ProfilerCategory.Scripts, "FixedUpdate.ScriptRunBehaviourFixedUpdate", "FixedUpdate");
        Time(ProfilerCategory.Physics, "FixedUpdate.Physics2DFixedUpdate", "Physics2D");
        Time(ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationBegin", "AnimBegin");
        Time(ProfilerCategory.Animation, "PreLateUpdate.DirectorUpdateAnimationEnd", "AnimEnd");
        Time(ProfilerCategory.Gui, "PostLateUpdate.PlayerUpdateCanvases", "UI.Canvases");
        Time(ProfilerCategory.Gui, "Canvas.BuildBatch", "UI.BuildBatch");
        Time(ProfilerCategory.Gui, "PostLateUpdate.UpdateRectTransform", "UI.RectTransform");
        Time(ProfilerCategory.Render, "PostLateUpdate.FinishFrameRendering", "Rendering");
        Time(ProfilerCategory.Scripts, "Lindavista.EnemyAI", "EnemyAI");
        Time(ProfilerCategory.Scripts, "Lindavista.ProjectileSweep", "ProjectileSweep");
        Time(ProfilerCategory.Internal, "GC.Collect", "GC.Collect");
        Time(ProfilerCategory.Gui, "GUI.Repaint", "IMGUI.Repaint");
        Time(ProfilerCategory.Scripts, "Instantiate", "Instantiate");
        Time(ProfilerCategory.Internal, "Loading.ReadObject", "LoadObject");
        Value(ProfilerCategory.Memory, "GC Allocated In Frame", "GCAllocBytes");
        Value(ProfilerCategory.Memory, "GC Allocation In Frame Count", "GCAllocCount");
        Value(ProfilerCategory.Render, "Draw Calls Count", "DrawCalls");
        Value(ProfilerCategory.Render, "Batches Count", "Batches");
        Value(ProfilerCategory.Render, "SetPass Calls Count", "SetPass");
        Value(ProfilerCategory.Render, "Triangles Count", "Triangles");
        Value(ProfilerCategory.Render, "Vertices Count", "Vertices");
        Value(ProfilerCategory.Memory, "GC Used Memory", "GCUsed");
        Value(ProfilerCategory.Memory, "GC Reserved Memory", "GCReserved");
        Value(ProfilerCategory.Memory, "Texture Memory", "TextureMemory");
        Value(ProfilerCategory.Memory, "Texture Count", "TextureCount");
    }

    private static void DisposeCounters()
    {
        foreach (var counter in counters) counter.Recorder.Dispose();
        counters.Clear();
    }

    private static void Finish(string reason)
    {
        endFrame = Time.frameCount;
        endReason = reason;
        var text = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        int frames = Math.Max(0, endFrame - startFrame);
        text.AppendLine($"## {label} | nivel {level + 1} | {(cesar ? "César" : "Marco")}{(forceBoss ? " + jefe forzado" : "")}");
        text.AppendLine($"frames={frames} reason={reason} screen={Screen.width}x{Screen.height} platform={Application.platform} sceneLoad={loadSeconds:F2}s");
        foreach (var counter in counters)
        {
            if (!counter.Recorder.Valid) { text.AppendLine($"{counter.Label}: n/a"); continue; }
            var samples = new List<ProfilerRecorderSample>(counter.Recorder.Capacity);
            counter.Recorder.CopyTo(samples);
            int take = Math.Min(samples.Count, frames);
            var values = new List<double>(take);
            for (int i = samples.Count - take; i < samples.Count; i++)
                values.Add(counter.Nanoseconds ? samples[i].Value / 1e6 : samples[i].Value);
            if (values.Count == 0) { text.AppendLine($"{counter.Label}: no samples"); continue; }
            double sum = 0; int nonZero = 0;
            foreach (double v in values) { sum += v; if (v > 0) nonZero++; }
            values.Sort();
            double avg = sum / values.Count, p95 = values[(int)((values.Count - 1) * 0.95)],
                p99 = values[(int)((values.Count - 1) * 0.99)], max = values[values.Count - 1];
            string unit = counter.Nanoseconds ? "ms" : "";
            text.AppendLine(string.Format(inv, "{0}: avg={1:F3}{5} p95={2:F3} p99={3:F3} max={4:F3} nonzeroFrames={6} total={7:F1}",
                counter.Label, avg, p95, p99, max, unit, nonZero, sum));
            if (counter.Label == "PlayerLoop")
            {
                int over16 = 0, over33 = 0;
                foreach (double v in values) { if (v > 16.7) over16++; if (v > 33.3) over33++; }
                text.AppendLine($"PlayerLoop hitches: >16.7ms={over16} >33.3ms={over33}");
            }
        }
        text.Append(AllocationProbe.Summary(frames));
        if (sampleCount > 0)
            text.AppendLine(string.Format(inv, "Scene: overdrawAvg={0:F2} overdrawMax={1:F2} spritesInView avg={2:F0} max={3} enemies avg={4:F1} max={5} shotEffects avg={6:F1}",
                overdrawSum / sampleCount, overdrawMax, renderersSum / sampleCount, maxRenderers,
                enemiesSum / sampleCount, maxEnemies, effectsSum / sampleCount));
        text.Append(TextureResidency());
        Report = text.ToString();
        Stop();
        stage = Stage.Done;
        if (suiteFile != null) System.IO.File.AppendAllText(suiteFile, "```\n" + Report + "```\n\n");
        if (suite.Count > 0)
        {
            var next = suite.Dequeue();
            string nextLabel = label;
            EditorApplication.delayCall += () => Start(next.level, next.cesar, duration, nextLabel, next.boss);
            return;
        }
        Debug.Log("PERFORMANCE_BENCHMARK\n" + Report);
        suiteFile = null;
    }

    /// <summary>Textures loaded right now, priced with their imported Android format (estimate, not a device reading).</summary>
    public static string TextureResidency(int top = 12)
    {
        long projected = 0, readableExtra = 0, runtimeCreated = 0;
        int count = 0;
        var rows = new List<(long bytes, string row)>();
        foreach (var texture in Resources.FindObjectsOfTypeAll<Texture2D>())
        {
            string path = AssetDatabase.GetAssetPath(texture);
            bool projectAsset = path.StartsWith("Assets/", StringComparison.Ordinal);
            bool runtime = string.IsNullOrEmpty(path) && (texture.hideFlags & HideFlags.HideAndDontSave) == 0 &&
                           !EditorUtility.IsPersistent(texture);
            if (!projectAsset && !runtime) continue;
            long bytes = 0;
            for (int mip = 0; mip < texture.mipmapCount; mip++)
                bytes += GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1, texture.width >> mip),
                    Mathf.Max(1, texture.height >> mip), texture.format);
            if (texture.isReadable && projectAsset) readableExtra += bytes;
            if (runtime) runtimeCreated += bytes;
            projected += bytes;
            count++;
            if (projectAsset)
                rows.Add((bytes, $"{bytes / 1048576f:F1}MB {texture.format} {texture.width}x{texture.height}{(texture.isReadable ? " RW" : "")} {path}"));
        }
        rows.Sort((a, b) => b.bytes.CompareTo(a.bytes));
        var text = new StringBuilder();
        text.AppendLine($"Textures loaded={count} androidFormatEstimate={projected / 1048576f:F1}MB readableCpuCopies={readableExtra / 1048576f:F1}MB runtimeCreated={runtimeCreated / 1024f:F0}KB");
        for (int i = 0; i < Math.Min(top, rows.Count); i++) text.AppendLine("  " + rows[i].row);
        return text.ToString();
    }
}
