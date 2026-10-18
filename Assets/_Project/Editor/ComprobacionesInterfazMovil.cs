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
            if (control is Scrollbar && !control.interactable) continue;
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

    /// <summary>Audita botones y sliders de menús; ignora las filas ocultas por el scroll.</summary>
    public static string ValidarObjetivosTactilesMenu()
    {
        ValidarDisposicionActual();
        int comprobados = 0;
        // Se compara con el mismo encaje que usan los menús: 72 píxeles en 1280x720.
        // No se infiere la densidad física desde Screen.dpi (en muchos Android no es fiable).
        float minimo = 108f * Mathf.Min(Screen.width / 1920f, Screen.height / 1080f);
        foreach (var control in UnityEngine.Object.FindObjectsByType<Selectable>(FindObjectsSortMode.None))
        {
            if (!control.isActiveAndEnabled || control is Scrollbar) continue;
            var canvas = control.GetComponentInParent<Canvas>();
            if (canvas == null || (canvas.GetComponent<MainMenuController>() == null &&
                canvas.GetComponent<CharacterLevelSelectionController>() == null &&
                canvas.GetComponentInParent<PistolUpgradeShop>() == null)) continue;
            Rect bounds = ObtenerLimites((RectTransform)control.transform, canvas);
            bool visible = true;
            foreach (var mask in control.GetComponentsInParent<RectMask2D>())
            {
                Rect viewport = ObtenerLimites(mask.rectTransform, canvas);
                if (!viewport.Overlaps(bounds)) visible = false;
            }
            if (!visible) continue;
            if (bounds.width + 2f < minimo || bounds.height + 2f < minimo)
                throw new InvalidOperationException("Objetivo táctil pequeño: " + Ruta(control.transform) +
                    " " + bounds.size + "; mínimo " + minimo.ToString("F1") + " px.");
            comprobados++;
        }
        string result = "OBJETIVOS_TACTILES_OK: " + comprobados + " botones y sliders con tamaño suficiente.";
        Debug.Log(result);
        return result;
    }

    /// <summary>Desplaza sin comprar y comprueba que cada acción de la tienda puede verse entera.</summary>
    public static string ValidarTiendaDesplazable()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Ejecuta esta comprobación en Play Mode.");
        var shop = UnityEngine.Object.FindFirstObjectByType<PistolUpgradeShop>();
        if (shop == null) throw new InvalidOperationException("Abre la tienda antes de comprobarla.");
        var scroll = shop.GetComponentInChildren<ScrollRect>();
        if (scroll == null) throw new InvalidOperationException("Esta pantalla no usa la distribución móvil de la tienda.");
        Canvas.ForceUpdateCanvases();
        var canvas = scroll.GetComponentInParent<Canvas>();
        Vector2 previous = scroll.content.anchoredPosition;
        Vector2 velocity = scroll.velocity;
        int checkedActions = 0;
        try
        {
            scroll.StopMovement();
            foreach (var action in scroll.content.GetComponentsInChildren<Button>())
            {
                Bounds relative = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.content, action.transform);
                float range = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
                float offset = Mathf.Clamp(-relative.center.y - scroll.viewport.rect.height * .5f, 0f, range);
                scroll.verticalNormalizedPosition = range > 0f ? 1f - offset / range : 1f;
                Canvas.ForceUpdateCanvases();
                Rect viewport = ObtenerLimites(scroll.viewport, canvas);
                Rect target = ObtenerLimites((RectTransform)action.transform, canvas);
                if (target.xMin < viewport.xMin - 2f || target.xMax > viewport.xMax + 2f ||
                    target.yMin < viewport.yMin - 2f || target.yMax > viewport.yMax + 2f)
                    throw new InvalidOperationException("Acción inaccesible al desplazar la tienda: " + Ruta(action.transform));
                checkedActions++;
            }
        }
        finally
        {
            scroll.content.anchoredPosition = previous;
            scroll.velocity = velocity;
            Canvas.ForceUpdateCanvases();
        }
        string result = "TIENDA_DESPLAZABLE_OK: " + checkedActions + " acciones alcanzables sin compras.";
        Debug.Log(result);
        return result;
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
