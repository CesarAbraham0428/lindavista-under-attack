using UnityEngine;

/// <summary>Mantiene el contenido fuera de cámaras, recortes y barras del sistema.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class AreaSeguraUI : MonoBehaviour
{
    private RectTransform zona;
    private Rect ultimaArea;
    private int ultimoAncho = -1;
    private int ultimoAlto = -1;

    public static AreaSeguraUI Configurar(RectTransform destino)
    {
        if (destino == null) return null;
        var ajuste = destino.GetComponent<AreaSeguraUI>() ?? destino.gameObject.AddComponent<AreaSeguraUI>();
        ajuste.Aplicar();
        return ajuste;
    }

    public static RectTransform Crear(Transform padre, string nombre = "Área segura")
    {
        var zona = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        zona.SetParent(padre, false);
        Configurar(zona);
        return zona;
    }

    private void OnEnable() => Aplicar();

    private void Update()
    {
        if (ultimoAncho != Screen.width || ultimoAlto != Screen.height || ultimaArea != Screen.safeArea)
            Aplicar();
    }

    private void Aplicar()
    {
        if (Screen.width <= 0 || Screen.height <= 0) return;
        if (zona == null) zona = (RectTransform)transform;
        ultimoAncho = Screen.width;
        ultimoAlto = Screen.height;
        ultimaArea = Screen.safeArea;
        Rect areaAplicada = ultimaArea;
        // Android puede entregar un rectángulo vacío durante una rotación o al recuperar foco.
        // Conservamos una zona útil hasta que el sistema publique los nuevos márgenes.
        if (ultimaArea.width <= 0f || ultimaArea.height <= 0f)
            areaAplicada = new Rect(0f, 0f, ultimoAncho, ultimoAlto);
        zona.anchorMin = new Vector2(Mathf.Clamp01(areaAplicada.xMin / ultimoAncho), Mathf.Clamp01(areaAplicada.yMin / ultimoAlto));
        zona.anchorMax = new Vector2(Mathf.Clamp01(areaAplicada.xMax / ultimoAncho), Mathf.Clamp01(areaAplicada.yMax / ultimoAlto));
        zona.offsetMin = zona.offsetMax = Vector2.zero;
    }
}
