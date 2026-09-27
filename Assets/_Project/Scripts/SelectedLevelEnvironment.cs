using System;
using UnityEngine;

public sealed class SelectedLevelEnvironment : MonoBehaviour
{
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";
    private const int LevelTwoIndex = 1;
    private const int LevelThreeIndex = 2;
    private const int LevelFourIndex = 3;

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

    private static readonly float[] NightGradientHeights =
    {
        0f, 0.18f, 0.36f, 0.54f, 0.68f, 0.80f, 0.89f, 0.96f, 1f
    };

    private static readonly Color[] NightGradientColors =
    {
        new Color(0.30f, 0.44f, 0.66f),
        new Color(0.25f, 0.39f, 0.62f),
        new Color(0.18f, 0.32f, 0.55f),
        new Color(0.13f, 0.27f, 0.49f),
        new Color(0.11f, 0.23f, 0.45f),
        new Color(0.095f, 0.21f, 0.41f),
        new Color(0.075f, 0.17f, 0.35f),
        new Color(0.025f, 0.07f, 0.20f),
        new Color(0.004f, 0.012f, 0.055f)
    };

    private static readonly float[] ThreeAmGradientHeights =
    {
        0f, 0.18f, 0.36f, 0.54f, 0.70f, 0.82f, 0.90f, 0.96f, 1f
    };

    private static readonly Color[] ThreeAmGradientColors =
    {
        new Color(0.08f, 0.09f, 0.28f),
        new Color(0.06f, 0.07f, 0.24f),
        new Color(0.045f, 0.052f, 0.20f),
        new Color(0.032f, 0.04f, 0.16f),
        new Color(0.022f, 0.03f, 0.125f),
        new Color(0.012f, 0.02f, 0.09f),
        new Color(0.006f, 0.012f, 0.055f),
        new Color(0.0025f, 0.006f, 0.028f),
        new Color(0.0008f, 0.0015f, 0.01f)
    };

    [SerializeField] private Sprite sunsetSunSprite;
    [SerializeField] private Sprite nightMoonSprite;
    [SerializeField] private Sprite threeAmMoonSprite;
    [SerializeField] private Color sunsetSkyColor = new Color(0.16f, 0.27f, 0.43f, 1f);
    [SerializeField] private Color nightSkyColor = new Color(0.02f, 0.028f, 0.095f, 1f);
    [SerializeField] private Color threeAmSkyColor = new Color(0.0008f, 0.0015f, 0.01f, 1f);

    private Texture2D runtimeGradientTexture;
    private Sprite runtimeGradientSprite;

    private void Awake()
    {
        int selectedLevel = PlayerPrefs.GetInt(SelectedLevelKey, 0);
        bool isLevelTwo = selectedLevel == LevelTwoIndex;
        bool isLevelThree = selectedLevel == LevelThreeIndex;
        bool isLevelFour = selectedLevel == LevelFourIndex;

        Transform damageLevels = transform.Find("Daño_Niveles");
        if (damageLevels != null)
        {
            SetDamageLevelVisibility(damageLevels, "Daño_Nivel_2", isLevelTwo);
            SetDamageLevelVisibility(damageLevels, "Daño_Nivel_3", isLevelThree);
            SetDamageLevelVisibility(damageLevels, "Daño_Nivel_4", isLevelFour);
        }

        if (!isLevelTwo && !isLevelThree && !isLevelFour)
            return;

        Camera gameplayCamera = Camera.main;
        if (gameplayCamera != null)
        {
            if (isLevelTwo)
            {
                gameplayCamera.backgroundColor = sunsetSkyColor;
                CreateGradientBackground(
                    gameplayCamera,
                    SunsetGradientHeights,
                    SunsetGradientColors,
                    "Level 2 Procedural Sunset Colors",
                    "Level 2 Sunset Gradient");
            }
            else if (isLevelThree)
            {
                gameplayCamera.backgroundColor = nightSkyColor;
                CreateGradientBackground(
                    gameplayCamera,
                    NightGradientHeights,
                    NightGradientColors,
                    "Level 3 Procedural Night Colors",
                    "Level 3 Night Gradient");
            }
            else
            {
                gameplayCamera.backgroundColor = threeAmSkyColor;
                CreateGradientBackground(
                    gameplayCamera,
                    ThreeAmGradientHeights,
                    ThreeAmGradientColors,
                    "Level 4 Procedural 3 AM Colors",
                    "Level 4 3 AM Gradient");
            }
        }

        Sprite celestialSprite = isLevelTwo
            ? sunsetSunSprite
            : isLevelThree ? nightMoonSprite : threeAmMoonSprite;
        if (celestialSprite == null)
            return;

        SpriteRenderer[] spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer.gameObject.name.EndsWith("_Sol", StringComparison.Ordinal))
                spriteRenderer.sprite = celestialSprite;
        }
    }

    private static void SetDamageLevelVisibility(Transform damageLevels, string objectName, bool isVisible)
    {
        Transform damageLevel = damageLevels.Find(objectName);
        if (damageLevel != null)
            damageLevel.gameObject.SetActive(isVisible);
    }

    private void CreateGradientBackground(
        Camera gameplayCamera,
        float[] gradientHeights,
        Color[] gradientColors,
        string textureName,
        string objectName)
    {
        float viewHeight = gameplayCamera.orthographicSize * 2f;
        float viewWidth = viewHeight * gameplayCamera.aspect;
        const int textureWidth = 2;
        const int textureHeight = 128;
        Color[] pixels = new Color[textureWidth * textureHeight];
        for (int y = 0; y < textureHeight; y++)
        {
            float gradientHeight = y / (float)(textureHeight - 1);
            Color color = EvaluateGradient(gradientHeight, gradientHeights, gradientColors);
            pixels[y * textureWidth] = color;
            pixels[y * textureWidth + 1] = color;
        }

        runtimeGradientTexture = new Texture2D(textureWidth, textureHeight, TextureFormat.RGBA32, false)
        {
            name = textureName,
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

        GameObject skyObject = new GameObject(objectName);
        skyObject.transform.SetParent(gameplayCamera.transform, false);
        skyObject.transform.localPosition = new Vector3(0f, 0f, 10f);
        skyObject.transform.localScale = new Vector3(viewWidth / textureWidth, viewHeight / textureHeight, 1f);

        SpriteRenderer spriteRenderer = skyObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = runtimeGradientSprite;
        spriteRenderer.sortingOrder = short.MinValue + 1;
    }

    private static Color EvaluateGradient(float height, float[] gradientHeights, Color[] gradientColors)
    {
        for (int i = 0; i < gradientHeights.Length - 1; i++)
        {
            if (height > gradientHeights[i + 1])
                continue;

            float segmentHeight = gradientHeights[i + 1] - gradientHeights[i];
            float interpolation = (height - gradientHeights[i]) / segmentHeight;
            return Color.Lerp(gradientColors[i], gradientColors[i + 1], interpolation);
        }

        return gradientColors[gradientColors.Length - 1];
    }

    private void OnDestroy()
    {
        if (runtimeGradientSprite != null)
            Destroy(runtimeGradientSprite);

        if (runtimeGradientTexture != null)
            Destroy(runtimeGradientTexture);
    }
}
