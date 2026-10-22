using UnityEngine;
using UnityEngine.UI;

public static class AdaptacionMenusUI
{
    /// <summary>Conserva el fondo a pantalla completa y encaja los controles en el área segura.</summary>
    public static void Configurar(Canvas lienzo)
    {
        if (lienzo == null || lienzo.transform.Find("Área segura de menú") != null) return;
        var escala = lienzo.GetComponent<CanvasScaler>();
        if (escala != null) escala.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        int cantidad = lienzo.transform.childCount;
        var hijos = new Transform[cantidad];
        for (int i = 0; i < cantidad; i++) hijos[i] = lienzo.transform.GetChild(i);
        var zona = AreaSeguraUI.Crear(lienzo.transform, "Área segura de menú");
        var contenido = ContenidoAdaptableUI.Crear(zona, new Vector2(1920f, 1080f));
        foreach (var hijo in hijos)
        {
            if (hijo.name == "Background")
            {
                var imagen = hijo.GetComponent<Image>();
                if (imagen != null && imagen.sprite != null)
                {
                    var proporcion = hijo.GetComponent<AspectRatioFitter>() ?? hijo.gameObject.AddComponent<AspectRatioFitter>();
                    proporcion.aspectRatio = imagen.sprite.rect.width / Mathf.Max(1f, imagen.sprite.rect.height);
                    proporcion.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                }
                continue;
            }
            hijo.SetParent(contenido, false);
        }
    }
}
