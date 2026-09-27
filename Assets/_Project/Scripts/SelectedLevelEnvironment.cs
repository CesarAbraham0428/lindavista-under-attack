using System;
using UnityEngine;

public sealed class SelectedLevelEnvironment : MonoBehaviour
{
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";
    private const int LevelTwoIndex = 1;

    private static readonly float[] SunsetGradientHeights =
    {
        0f, 0.14f, 0.32f, 0.50f, 0.68f, 0.84f, 0.94f, 0.98f, 1f
    };

    private static readonly Color[] SunsetGradientColors =
    {
        new Color(1f, 0.70f, 0.34f),
        new Color(1f, 0.49f, 0.19f),
        new Color(1f, 0.34f, 0.13f),
        new Color(0.98f, 0.25f, 0.15f),
        new Color(0.92f, 0.24f, 0.13f),
        new Color(0.82f, 0.22f, 0.13f),
        new Color(0.64f, 0.20f, 0.17f),
        new Color(0.24f, 0.22f, 0.34f),
        new Color(0.16f, 0.27f, 0.43f)
    };

    [SerializeField] private Sprite sunsetSunSprite;
    [SerializeField] private Color sunsetSkyColor = new Color(0.16f, 0.27f, 0.43f, 1f);

    private Texture2D runtimeGradientTexture;
    private Sprite runtimeGradientSprite;

    private void Awake()
    {
        bool isLevelTwo = PlayerPrefs.GetInt(SelectedLevelKey, 0) == LevelTwoIndex;

        Transform levelTwoDamage = transform.Find("Daño_Niveles/Daño_Nivel_2");
        if (levelTwoDamage != null)
            levelTwoDamage.gameObject.SetActive(isLevelTwo);

        if (!isLevelTwo)
            return;

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            gameplayCamera.backgroundColor = sunsetSkyColor;
            CreateSunsetGradientBackground(gameplayCamera);
        }

        if (sunsetSunSprite == null)
            return;

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer.gameObject.name.EndsWith("_Sol", StringComparison.Ordinal))
                spriteRenderer.sprite = sunsetSunSprite;
        }
    }

    private void CreateSunsetGradientBackground(Camera gameplayCamera)
    {
        float viewHeight = gameplayCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * gameplayCamera.aspect;
        const int textureWidth = 2;
        const int textureHeight = 128;
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; y++)
        {
            float gradientHeight = y / (float)(textureHeight - 1);
            Color color = EvaluateSunsetGradient(gradientHeight);
            pixels[y * textureWidth] = color;
            pixels[y * textureWidth + 1] = color;
        }

        runtimeGradientTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = "Level 2 Procedural Sunset Colors",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        runtimeGradientTexture.SetPixels(pixels);
        runtimeGradientTexture.Apply(false, false);
        runtimeGradientSprite = Sprite.Create(
            runtimeGradientTexture,
            new Rect(0f, 0f, textureWidth, textureHeight),
            new Vector2(0.5f, 0.5f),
            1f);

        GameObject skyObject = new GameObject("Level 2 Sunset Gradient");
        skyObject.transform.SetParent(gameplayCamera.transform, false);
        skyObject.transform.localPosition = new Vector3(0f, 0f, 10f);
        skyObject.transform.localScale = new Vector3(viewWidth / textureWidth, viewHeight / textureHeight, 1f);

        SpriteRenderer spriteRenderer = skyObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = runtimeGradientSprite;
        spriteRenderer.sortingOrder = short.MinValue + 1;
    }

    private static Color EvaluateSunsetGradient(float height)
    {
        for (int i = 0; i < SunsetGradientHeights.Length - 1; i++)
        {
            if (height > SunsetGradientHeights[i + 1])
                continue;

            float segmentHeight = SunsetGradientHeights[i + 1] - SunsetGradientHeights[i];
            float interpolation = (height - SunsetGradientHeights[i]) / segmentHeight;
            return Color.Lerp(SunsetGradientColors[i], SunsetGradientColors[i + 1], interpolation);
        }

        return SunsetGradientColors[SunsetGradientColors.Length - 1];
    }

    private void OnDestroy()
    {
        if (runtimeGradientSprite != null)
            Destroy(runtimeGradientSprite);

        if (runtimeGradientTexture != null)
            Destroy(runtimeGradientTexture);
    }
}
