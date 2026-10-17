using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Comparte la búsqueda de jugadores entre todos los enemigos de la escena.</summary>
internal static class ObjetivosCombate
{
    private static PlayerActions[] jugadores;
    private static float siguienteBusqueda;
    private static int escena = -1;

    internal static PlayerActions[] Obtener()
    {
        int escenaActual = SceneManager.GetActiveScene().handle;
        if (jugadores == null || escena != escenaActual || Time.time >= siguienteBusqueda)
        {
            jugadores = Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None);
            siguienteBusqueda = Time.time + 0.25f;
            escena = escenaActual;
        }
        return jugadores;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        jugadores = null;
        siguienteBusqueda = 0f;
        escena = -1;
    }
}
