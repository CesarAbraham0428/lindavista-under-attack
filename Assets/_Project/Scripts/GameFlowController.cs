using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Ends a match and presents the restart and menu options.</summary>
public sealed class GameFlowController : MonoBehaviour
{
    private static GameFlowController active;

    private static readonly Color OverlayColor = new Color(0.025f, 0.035f, 0.045f, 0.82f);
    private static readonly Color PanelColor = new Color(0.075f, 0.09f, 0.10f, 0.98f);
    private static readonly Color AccentColor = new Color(0.82f, 0.16f, 0.11f, 1f);
    private static readonly Color ButtonColor = new Color(0.16f, 0.21f, 0.22f, 1f);
    private static readonly Color ButtonHighlightColor = new Color(0.23f, 0.31f, 0.32f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.93f, 0.85f, 1f);

    private bool defeatActive;
    private string restartSceneName;
    private Font font;

    public static bool IsDefeatActive => active != null && active.defeatActive;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Gameplay" && scene.name != "Testing")
            return;

        if (active == null || active.gameObject.scene != scene)
            new GameObject("Game Flow Controller").AddComponent<GameFlowController>();
    }

    private void Awake()
    {
        active = this;
        restartSceneName = gameObject.scene.name;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void OnDestroy()
    {
        if (active == this)
            active = null;
    }

    public static void ReportPlayerDefeat()
    {
        EnsureActiveController()?.ShowDefeat("El personaje fue derrotado.");
    }

    public static void ReportEnemyReachedEntrance()
    {
        EnsureActiveController()?.ShowDefeat(
            "Un enemigo llegó a la entrada de Lindavista.");
    }

    private static GameFlowController EnsureActiveController()
    {
        if (active != null)
            return active;

        Scene scene = SceneManager.GetActiveScene();
        if (scene.name != "Gameplay" && scene.name != "Testing")
            return null;

        return new GameObject("Game Flow Controller").AddComponent<GameFlowController>();
    }

    private void ShowDefeat(string message)
    {
        if (defeatActive)
            return;

        defeatActive = true;

        foreach (PlayerActions actions in FindObjectsByType<PlayerActions>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            actions.SetInputLocked(true);

        foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            movement.SetMovementLocked(true);

        foreach (BasicEnemy enemy in FindObjectsByType<BasicEnemy>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            enemy.StopForGameOver();

        BuildDefeatUI(message);
    }

    private void BuildDefeatUI(string message)
    {
        EnsureEventSystem();

        GameObject canvasObject = new GameObject("Defeat Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 250;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform canvasRect = canvasObject.GetComponent<RectTransform>();
        GameObject overlay = CreateImage("Dim Background", canvasRect, OverlayColor, true);
        StretchToParent(overlay.GetComponent<RectTransform>());

        GameObject panelObject = CreateImage("Defeat Panel", canvasRect, PanelColor, false);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        SetCenteredPercent(panel, 0.7f, 0.7f);

        GameObject accent = CreateImage("Accent", panel, AccentColor, false);
        RectTransform accentRect = accent.GetComponent<RectTransform>();
        accentRect.anchorMin = new Vector2(0f, 1f);
        accentRect.anchorMax = new Vector2(1f, 1f);
        accentRect.pivot = new Vector2(0.5f, 1f);
        accentRect.anchoredPosition = Vector2.zero;
        accentRect.sizeDelta = new Vector2(0f, 12f);

        Text title = CreateText("Title", panel, "DERROTA", 116, FontStyle.Bold);
        SetCentered(title.rectTransform, new Vector2(0f, 188f), new Vector2(1100f, 160f));

        Text body = CreateText("Message", panel, message, 50, FontStyle.Normal);
        SetCentered(body.rectTransform, new Vector2(0f, 24f), new Vector2(1120f, 112f));

        CreateButton(panel, "REINTENTAR", new Vector2(-260f, -188f), RestartCurrentScene);
        CreateButton(panel, "SALIR AL MENÚ", new Vector2(260f, -188f), ReturnToMenu);
    }

    private void CreateButton(Transform parent, string label, Vector2 position,
        UnityEngine.Events.UnityAction onClick)
    {
        GameObject buttonObject = new GameObject(label, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetCentered(buttonRect, position, new Vector2(470f, 126f));

        Image image = buttonObject.GetComponent<Image>();
        image.color = ButtonColor;

        Button button = buttonObject.GetComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = ButtonColor;
        colors.highlightedColor = ButtonHighlightColor;
        colors.pressedColor = AccentColor;
        colors.selectedColor = ButtonHighlightColor;
        button.colors = colors;
        button.onClick.AddListener(onClick);

        Text buttonText = CreateText("Label", buttonRect, label, 38, FontStyle.Bold);
        StretchToParent(buttonText.rectTransform);
    }

    private Text CreateText(string objectName, Transform parent, string value,
        int fontSize, FontStyle fontStyle)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);

        Text text = textObject.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = TextColor;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 16;
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static GameObject CreateImage(string objectName, Transform parent,
        Color color, bool receivesRaycasts)
    {
        GameObject imageObject = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);

        Image image = imageObject.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = receivesRaycasts;
        return imageObject;
    }

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private static void SetCenteredPercent(RectTransform rect, float width, float height)
    {
        Vector2 size = new Vector2(Mathf.Clamp01(width), Mathf.Clamp01(height));
        rect.anchorMin = (Vector2.one - size) * 0.5f;
        rect.anchorMax = (Vector2.one + size) * 0.5f;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void StretchToParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject eventSystemObject = new GameObject("Game UI Event System",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem = eventSystemObject.GetComponent<EventSystem>();
        }

        InputSystemUIInputModule module =
            eventSystem.GetComponent<InputSystemUIInputModule>();
        if (module == null)
        {
            foreach (BaseInputModule oldModule in eventSystem.GetComponents<BaseInputModule>())
                oldModule.enabled = false;
            module = eventSystem.gameObject.AddComponent<InputSystemUIInputModule>();
        }

        if (module.actionsAsset == null)
            module.AssignDefaultActions();
    }

    private void RestartCurrentScene()
    {
        SceneManager.LoadScene(restartSceneName);
    }

    private void ReturnToMenu()
    {
        SceneManager.LoadScene("MainMenu");
    }
}
