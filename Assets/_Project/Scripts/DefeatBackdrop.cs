using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>One blurred snapshot of the lost match, behind the readable result UI.</summary>
public sealed class DefeatBackdrop : MonoBehaviour
{
    private Canvas resultCanvas;
    private RawImage background;
    private Texture2D blurred;

    public void Initialize(Canvas canvas)
    {
        resultCanvas = canvas;
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
        Texture2D snapshot = null;
        try
        {
            snapshot = ScreenCapture.CaptureScreenshotAsTexture();
            if (snapshot == null || snapshot.width < 1 || snapshot.height < 1) yield break;
            int width = Mathf.Min(320, snapshot.width);
            int height = Mathf.Max(1, Mathf.RoundToInt(width * snapshot.height / (float)snapshot.width));
            Color32[] source = snapshot.GetPixels32();
            Color32[] pixels = new Color32[width * height];
            Color32[] scratch = new Color32[pixels.Length];
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Min(snapshot.height - 1, (int)((y + 0.5f) * snapshot.height / height));
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(snapshot.width - 1, (int)((x + 0.5f) * snapshot.width / width));
                    pixels[y * width + x] = source[sourceY * snapshot.width + sourceX];
                }
            }
            for (int pass = 0; pass < 2; pass++)
            {
                Blur(pixels, scratch, width, height, true);
                Blur(scratch, pixels, width, height, false);
            }
            blurred = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Defeat Background Snapshot",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            blurred.SetPixels32(pixels);
            blurred.Apply(false, true);
            background.texture = blurred;
            background.enabled = true;
        }
        finally
        {
            if (snapshot != null) Destroy(snapshot);
            if (resultCanvas != null) resultCanvas.enabled = true;
        }
    }

    private static void Blur(Color32[] source, Color32[] destination, int width, int height, bool horizontal)
    {
        const int radius = 5;
        const int samples = radius * 2 + 1;
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
