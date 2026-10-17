using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One blurred snapshot of the ended match, behind the readable result UI.</summary>
public sealed class DefeatBackdrop : MonoBehaviour
{
    private Canvas resultCanvas;
    private RawImage background;
    private Texture2D blurred;

    private bool subtle;

    public void Initialize(Canvas canvas, bool subtleBlur = false)
    {
        resultCanvas = canvas;
        subtle = subtleBlur;
        var imageObject = new GameObject("Blurred Background", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(canvas.transform, false);
        imageObject.transform.SetAsFirstSibling();
        background = imageObject.GetComponent<RawImage>();
        background.raycastTarget = false;
        background.enabled = false;
        RectTransform rect = background.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        StartCoroutine(Capture());
    }

    private IEnumerator Capture()
    {
        // Capture the scene and its HUD, without blurring the wooden signs.
        resultCanvas.enabled = false;
        yield return new WaitForEndOfFrame();
        RenderTexture captura = null;
        RenderTexture reducida = null;
        RenderTexture anterior = RenderTexture.active;
        try
        {
            if (Screen.width < 1 || Screen.height < 1) yield break;
            int width = Mathf.Min(subtle ? 640 : 320, Screen.width);
            int height = Mathf.Max(1, Mathf.RoundToInt(width * Screen.height / (float)Screen.width));
            // Reducir en GPU antes de leer: una pantalla 4K ya no crea un array
            // de millones de píxeles administrados al mostrar el resultado.
            captura = RenderTexture.GetTemporary(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
            reducida = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            ScreenCapture.CaptureScreenshotIntoRenderTexture(captura);
            // La captura usa el origen de la API gráfica; ReadPixels y RawImage
            // esperan el origen inferior izquierdo. Normalizar al reducir en GPU.
            bool invertirY = SystemInfo.graphicsUVStartsAtTop;
            Graphics.Blit(captura, reducida,
                invertirY ? new Vector2(1f, -1f) : Vector2.one,
                invertirY ? new Vector2(0f, 1f) : Vector2.zero);
            RenderTexture.active = reducida;
            blurred = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Result Background Snapshot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            blurred.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            Color32[] pixels = blurred.GetPixels32();
            Color32[] scratch = new Color32[pixels.Length];
            for (int pass = 0; pass < (subtle ? 1 : 2); pass++)
            {
                Blur(pixels, scratch, width, height, true, subtle ? 2 : 5);
                Blur(scratch, pixels, width, height, false, subtle ? 2 : 5);
            }
            blurred.SetPixels32(pixels);
            blurred.Apply(false, true);
            background.texture = blurred;
            background.enabled = true;
        }
        finally
        {
            RenderTexture.active = anterior;
            if (reducida != null) RenderTexture.ReleaseTemporary(reducida);
            if (captura != null) RenderTexture.ReleaseTemporary(captura);
            if (resultCanvas != null) resultCanvas.enabled = true;
        }
    }

    private static void Blur(Color32[] source, Color32[] destination, int width, int height, bool horizontal, int radius)
    {
        int samples = radius * 2 + 1;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int red = 0, green = 0, blue = 0;
                for (int offset = -radius; offset <= radius; offset++)
                {
                    int sx = horizontal ? Mathf.Clamp(x + offset, 0, width - 1) : x;
                    int sy = horizontal ? y : Mathf.Clamp(y + offset, 0, height - 1);
                    Color32 color = source[sy * width + sx];
                    red += color.r; green += color.g; blue += color.b;
                }
                destination[y * width + x] = new Color32((byte)(red / samples),
                    (byte)(green / samples), (byte)(blue / samples), 255);
            }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        if (resultCanvas != null) resultCanvas.enabled = true;
    }

    private void OnDestroy()
    {
        if (blurred != null) Destroy(blurred);
    }
}
