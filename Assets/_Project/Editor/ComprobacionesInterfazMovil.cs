using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Comprueba la geometría real de la pantalla activa, incluidos menús y listas desplazables.</summary>
public static class ComprobacionesInterfazMovil
{
    [MenuItem("Tools/Lindavista/Verificar interfaz en pantalla actual")]
    public static void Verificar() => ValidarDisposicionActual();

    public static string ValidarDisposicionActual()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Ejecuta esta comprobación en Play Mode.");
        Canvas.ForceUpdateCanvases();
        int comprobados = 0;
        Rect zonaSegura = Screen.safeArea;
        foreach (var control in UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
        {
            if (!control.isActiveAndEnabled) continue;
            var rect = control.transform as RectTransform;
            var lienzo = control.GetComponentInParent<Canvas>();
            if (rect == null || lienzo == null || lienzo.renderMode == RenderMode.WorldSpace) continue;
            Rect limites = ObtenerLimites(rect, lienzo);
            foreach (var mascara in control.GetComponentsInParent<RectMask2D>())
            {
                if (!mascara.isActiveAndEnabled) continue;
                Rect ventana = ObtenerLimites(mascara.rectTransform, lienzo);
                limites = Rect.MinMaxRect(Mathf.Max(limites.xMin, ventana.xMin), Mathf.Max(limites.yMin, ventana.yMin),
                    Mathf.Min(limites.xMax, ventana.xMax), Mathf.Min(limites.yMax, ventana.yMax));
            }
            if (limites.width <= 0f || limites.height <= 0f) continue;
            if (limites.xMin < zonaSegura.xMin - 2f || limites.yMin < zonaSegura.yMin - 2f ||
                limites.xMax > zonaSegura.xMax + 2f || limites.yMax > zonaSegura.yMax + 2f)
                throw new InvalidOperationException("Control fuera del área segura: " + Ruta(control.transform) + " " + limites);
            if (control.targetGraphic != null && !control.targetGraphic.raycastTarget)
                throw new InvalidOperationException("Control sin superficie táctil: " + Ruta(control.transform));
            comprobados++;
        }
        string resultado = "INTERFAZ_MOVIL_OK: " + comprobados + " controles dentro del área segura en " +
            Screen.width + "x" + Screen.height + ".";
        Debug.Log(resultado);
        return resultado;
    }

    private static Rect ObtenerLimites(RectTransform rect, Canvas lienzo)
    {
        var esquinas = new Vector3[4];
        rect.GetWorldCorners(esquinas);
        Camera camara = lienzo.renderMode == RenderMode.ScreenSpaceOverlay ? null : lienzo.worldCamera;
        Vector2 minimo = RectTransformUtility.WorldToScreenPoint(camara, esquinas[0]);
        Vector2 maximo = minimo;
        for (int i = 1; i < esquinas.Length; i++)
        {
            Vector2 punto = RectTransformUtility.WorldToScreenPoint(camara, esquinas[i]);
            minimo = Vector2.Min(minimo, punto);
            maximo = Vector2.Max(maximo, punto);
        }
        return Rect.MinMaxRect(minimo.x, minimo.y, maximo.x, maximo.y);
    }

    private static string Ruta(Transform destino) => destino.parent != null ? Ruta(destino.parent) + "/" + destino.name : destino.name;
}
