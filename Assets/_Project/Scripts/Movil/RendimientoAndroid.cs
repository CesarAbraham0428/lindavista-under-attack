using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>Limits world rendering cost while keeping overlay UI at native resolution.</summary>
public sealed class RendimientoAndroid : MonoBehaviour
{
    private static RendimientoAndroid active;
    private UniversalRenderPipelineAsset pipeline;
    private RenderPipelineAsset previousPipeline;
    private float nextCheck;
    private int lastWidth, lastHeight, lastRate;
    private bool lastSaving;

    private void Awake()
    {
        active = this;
        previousPipeline = QualitySettings.renderPipeline;
        var source = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        if (source != null)
        {
            pipeline = Instantiate(source);
            pipeline.name = "Android Runtime Pipeline";
            pipeline.supportsHDR = false;
            pipeline.msaaSampleCount = 1;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            QualitySettings.renderPipeline = pipeline;
        }
        Apply();
    }

    public static void ActualizarConfiguracion() { if (active != null) active.Apply(); }

    public static float CalcularEscala(int width, int height, bool ahorro)
    {
        int shortSide = Mathf.Max(1, Mathf.Min(width, height));
        return Mathf.Clamp((ahorro ? 720f : 900f) / shortSide, .35f, 1f);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextCheck) return;
        nextCheck = Time.unscaledTime + 1f;
        int rate = Mathf.RoundToInt((float)Screen.currentResolution.refreshRateRatio.value);
        if (lastWidth != Screen.width || lastHeight != Screen.height || lastRate != rate ||
            lastSaving != ConfiguracionMovil.ModoAhorro) Apply();
    }

    private void Apply()
    {
        lastWidth = Screen.width;
        lastHeight = Screen.height;
        lastSaving = ConfiguracionMovil.ModoAhorro;
        double refreshRate = Screen.currentResolution.refreshRateRatio.value;
        lastRate = Mathf.RoundToInt((float)refreshRate);
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = ConfiguracionMovil.CalcularCadencia(refreshRate, lastSaving ? 30 : 60);
        if (pipeline != null) pipeline.renderScale = CalcularEscala(lastWidth, lastHeight, lastSaving);
    }

    private void OnApplicationPause(bool suspended) { if (!suspended) Apply(); }

    private void OnDestroy()
    {
        if (pipeline != null)
        {
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = previousPipeline;
            Destroy(pipeline);
        }
        if (active == this) active = null;
    }
}
