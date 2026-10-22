using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Agrupa consumos de munición y los confirma también al abandonar la aplicación o una escena.</summary>
[DisallowMultipleComponent]
public sealed class GuardadoProgreso : MonoBehaviour
{
    private float proximoGuardado;

    private void Awake() { SceneManager.sceneUnloaded += AlDescargarEscena; }

    private void Update()
    {
        if (Time.unscaledTime < proximoGuardado) return;
        proximoGuardado = Time.unscaledTime + 1f;
        // During combat the periodic write used to block a frame every second (~24 ms in the editor).
        // Suspension, quitting and scene changes below still wait until the data is on disk.
        ProgressionService.GuardarPendientesEnSegundoPlano();
    }

    private void AlDescargarEscena(Scene escena) { ProgressionService.GuardarPendientes(); }
    private void OnApplicationPause(bool pausado) { if (pausado) ProgressionService.GuardarPendientes(); }
    private void OnApplicationFocus(bool enfocado) { if (!enfocado) ProgressionService.GuardarPendientes(); }
    private void OnApplicationQuit() { ProgressionService.GuardarPendientes(); }
    private void OnDestroy()
    {
        SceneManager.sceneUnloaded -= AlDescargarEscena;
        ProgressionService.GuardarPendientes();
    }
}
