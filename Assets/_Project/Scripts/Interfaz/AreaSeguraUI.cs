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
        zona.anchorMin = new Vector2(Mathf.Clamp01(ultimaArea.xMin / ultimoAncho), Mathf.Clamp01(ultimaArea.yMin / ultimoAlto));
        zona.anchorMax = new Vector2(Mathf.Clamp01(ultimaArea.xMax / ultimoAncho), Mathf.Clamp01(ultimaArea.yMax / ultimoAlto));
        zona.offsetMin = zona.offsetMax = Vector2.zero;
    }
}
