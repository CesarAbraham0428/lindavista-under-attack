using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Windows/macOS/Linux player policy. Players, walls and street carry about 1000 texels per
/// screen height, so above 1440 lines the gameplay world renders at a reduced scale and is
/// upscaled; overlay UI stays at native resolution. Menus use Screen Space Camera canvases
/// and keep full resolution.
/// </summary>
public sealed class RendimientoEscritorio : MonoBehaviour
{
    public const int LineasMundo = 1440;
    private UniversalRenderPipelineAsset pipeline;
    private RenderPipelineAsset previousPipeline;
    private float nextCheck;
    private int lastWidth, lastHeight, lastVSync;
    private double lastRate;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Instalar()
    {
        if (!EsEscritorio(Application.platform)) return;
        var politica = new GameObject("Rendimiento Escritorio");
        DontDestroyOnLoad(politica);
        politica.AddComponent<RendimientoEscritorio>();
    }

    public static bool EsEscritorio(RuntimePlatform platform) => platform == RuntimePlatform.WindowsPlayer ||
        platform == RuntimePlatform.OSXPlayer || platform == RuntimePlatform.LinuxPlayer;

    public static float CalcularEscala(int width, int height)
    {
        int shortSide = Mathf.Max(1, Mathf.Min(width, height));
        return Mathf.Clamp(LineasMundo / (float)shortSide, .5f, 1f);
    }

    /// <summary>Frame cap used only when V-Sync is off: the display rate, never uncapped.</summary>
    public static int CalcularCadencia(double frecuencia)
    {
        if (frecuencia < 1d || double.IsNaN(frecuencia) || double.IsInfinity(frecuencia)) return 60;
        // 59.94 Hz -> 60 and 143.86 Hz -> 144: rounding down would request a slower cadence.
        return Mathf.Clamp((int)System.Math.Ceiling(frecuencia - .001d), 30, 360);
    }

    private void Awake()
    {
        previousPipeline = QualitySettings.renderPipeline;
        var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (source != null)
        {
            pipeline = Instantiate(source);
            pipeline.name = "Desktop Runtime Pipeline";
            QualitySettings.renderPipeline = pipeline;
        }
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        Apply();
    }

    private void OnActiveSceneChanged(Scene previous, Scene next) => Apply();

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        if (lastWidth != Screen.width || lastHeight != Screen.height || lastVSync != QualitySettings.vSyncCount ||
            lastRate != Screen.currentResolution.refreshRateRatio.value) Apply();
    }

    private void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastVSync = QualitySettings.vSyncCount;
        lastRate = Screen.currentResolution.refreshRateRatio.value;
        bool gameplay = SceneManager.GetActiveScene().name == "Gameplay";
        // With V-Sync the display paces frames and targetFrameRate is ignored. If V-Sync is off,
        // cap at the refresh rate so menus and pauses do not run uncapped at 100% GPU.
        Application.targetFrameRate = lastVSync > 0 ? -1 : CalcularCadencia(lastRate);
        if (pipeline != null) pipeline.renderScale = gameplay ? CalcularEscala(lastWidth, lastHeight) : 1f;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        if (pipeline != null)
        {
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
            Destroy(pipeline);
        }
    }
}
