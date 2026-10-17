using UnityEngine;

/// <summary>Encaja un diseño existente sin recortarlo en tabletas o pantallas panorámicas.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class ContenidoAdaptableUI : MonoBehaviour
{
    private RectTransform contenido;
    private RectTransform padre;
    private Vector2 referencia = new Vector2(1920f, 1080f);
    private Vector2 ultimoEspacio = new Vector2(-1f, -1f);

    public static RectTransform Crear(Transform padre, Vector2 referencia, string nombre = "Contenido adaptable")
    {
        var rect = new GameObject(nombre, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(padre, false);
        Configurar(rect, referencia);
        return rect;
    }

    public static void Configurar(RectTransform destino, Vector2 referencia)
    {
        var ajuste = destino.GetComponent<ContenidoAdaptableUI>() ?? destino.gameObject.AddComponent<ContenidoAdaptableUI>();
        ajuste.contenido = destino;
        ajuste.padre = destino.parent as RectTransform;
        ajuste.referencia = new Vector2(Mathf.Max(1f, referencia.x), Mathf.Max(1f, referencia.y));
        destino.anchorMin = destino.anchorMax = destino.pivot = Vector2.one * .5f;
        destino.anchoredPosition = Vector2.zero;
        destino.sizeDelta = ajuste.referencia;
        ajuste.ultimoEspacio = new Vector2(-1f, -1f);
        ajuste.Actualizar();
    }

    private void LateUpdate() => Actualizar();

    private void Actualizar()
    {
        if (padre == null || contenido == null || ultimoEspacio == padre.rect.size) return;
        ultimoEspacio = padre.rect.size;
        float escala = Mathf.Max(.001f, Mathf.Min(ultimoEspacio.x / referencia.x, ultimoEspacio.y / referencia.y));
        contenido.localScale = Vector3.one * escala;
    }
}
