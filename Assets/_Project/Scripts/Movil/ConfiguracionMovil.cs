using UnityEngine;

/// <summary>Política de pantalla y cadencia exclusiva de Android/iOS.</summary>
public static class ConfiguracionMovil
{
    private const string ClaveAhorro = "Lindavista.Android.Ahorro";
    public static bool ModoAhorro => PlayerPrefs.GetInt(ClaveAhorro, 0) != 0;

    public static void EstablecerModoAhorro(bool activo)
    {
        PlayerPrefs.SetInt(ClaveAhorro, activo ? 1 : 0);
        PlayerPrefs.Save();
        RendimientoAndroid.ActualizarConfiguracion();
    }

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
        if (Application.platform == RuntimePlatform.Android)
        {
            var politica = new GameObject("Rendimiento Android");
            Object.DontDestroyOnLoad(politica);
            politica.AddComponent<RendimientoAndroid>();
        }
    }

    public static int CalcularCadencia(double frecuencia, int maximo = 60)
    {
        maximo = Mathf.Clamp(maximo, 15, 60);
        if (frecuencia < 1d || double.IsNaN(frecuencia) || double.IsInfinity(frecuencia)) return maximo;
        int divisor = Mathf.Max(1, (int)System.Math.Ceiling(frecuencia / maximo));
        // Android chooses a display divisor below the requested integer. Rounding down
        // a fractional quotient would accidentally select the next, slower divisor.
        return Mathf.Clamp((int)System.Math.Ceiling(frecuencia / divisor), 1, maximo);
    }
}
