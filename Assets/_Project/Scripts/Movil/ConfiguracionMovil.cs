using UnityEngine;

/// <summary>Política de pantalla y cadencia exclusiva de Android/iOS.</summary>
public static class ConfiguracionMovil
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Aplicar()
    {
        if (!Application.isMobilePlatform) return;
        Screen.autorotateToPortrait = false;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft = true;
        Screen.autorotateToLandscapeRight = true;
        Screen.orientation = ScreenOrientation.AutoRotation;
        // Una cadencia que divide la frecuencia de pantalla, con un máximo de 60.
        Application.targetFrameRate = CalcularCadencia(Screen.currentResolution.refreshRateRatio.value);
    }

    public static int CalcularCadencia(double frecuencia)
    {
        if (frecuencia < 1d || double.IsNaN(frecuencia) || double.IsInfinity(frecuencia)) return 60;
        int divisor = Mathf.Max(1, Mathf.CeilToInt((float)(frecuencia / 60.5d)));
        return Mathf.Clamp(Mathf.RoundToInt((float)(frecuencia / divisor)), 1, 60);
    }
}
