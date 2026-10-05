using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Ends a match and presents the restart and menu options.</summary>
public sealed class GameFlowController : MonoBehaviour
{
    private static GameFlowController active;

    private static readonly Color TextColor = new Color(0.96f, 0.93f, 0.85f, 1f);

    private bool defeatActive;
    private bool victoryActive;
    private bool collectionPhase;
    private int clearedLevel;
    private int enemiesKilled;
    private int coinsCollected;
    private string restartSceneName;
    private Font font;
    private RectTransform defeatSafeArea;
    private RectTransform defeatContent;

    // Compatibility for existing terminal checks in movement and enemy AI.
    public static bool IsDefeatActive => IsMatchEnded;
    public static bool IsMatchEnded => active != null && (active.defeatActive || active.victoryActive);
    public static bool IsCollectionPhase => active != null && active.collectionPhase;
    public static bool IsVictoryActive => active != null && active.victoryActive;
    public static int EnemiesKilled => active != null ? active.enemiesKilled : 0;
    public static int CoinsCollected => active != null ? active.coinsCollected : 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        active = null;
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
        Canvas.willRenderCanvases -= UpdateDefeatLayout;
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

    public static void ReportEnemyKilled()
    {
        var controller = EnsureActiveController();
        if (controller != null && !IsMatchEnded) controller.enemiesKilled++;
    }

    public static void ReportCoinsCollected(int amount)
    {
        var controller = EnsureActiveController();
        if (controller != null && !IsMatchEnded) controller.coinsCollected += amount;
    }

    public static void ReportWaveCleared(int level)
    {
        var controller = EnsureActiveController();
        if (controller == null || IsMatchEnded || controller.collectionPhase) return;
        controller.clearedLevel = level;
        controller.collectionPhase = true;
    }

    public static void FinishLevel()
    {
        if (active == null || IsMatchEnded || !active.collectionPhase) return;
        if (!ProgressionService.TryCompleteLevel(active.clearedLevel)) return;
        active.collectionPhase = false;
        active.victoryActive = true;
        active.LockActors();
        active.BuildDefeatUI("Nivel " + (active.clearedLevel + 1) + " completado.");
    }

    private void ShowDefeat(string message)
    {
        if (IsMatchEnded)
            return;

        defeatActive = true;
        collectionPhase = false;
        LockActors();
        BuildDefeatUI(message);
    }

    private void LockActors()
    {

        foreach (PlayerActions actions in FindObjectsByType<PlayerActions>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            actions.SetInputLocked(true);

        foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            movement.SetMovementLocked(true);

        foreach (BasicEnemy enemy in FindObjectsByType<BasicEnemy>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            enemy.StopForGameOver();

    }

    private void BuildDefeatUI(string message)
    {
        EnsureEventSystem();

        BuildCartoonResultUI();
    }

    private void BuildCartoonResultUI()
    {
        Canvas canvas = ProgressionUI.Canvas(transform, victoryActive ? "Victory Canvas" : "Defeat Canvas", 250);
        Image dimmer = ProgressionUI.Image(canvas.transform, "Dim Background",
            victoryActive ? new Color(0f, 0f, 0f, 0.16f) : new Color(0.28f, 0.008f, 0.018f, 0.75f), true);
        StretchToParent(dimmer.rectTransform);

        defeatSafeArea = new GameObject("Safe Area", typeof(RectTransform)).GetComponent<RectTransform>();
        defeatSafeArea.SetParent(canvas.transform, false);
        defeatContent = new GameObject("Result Content", typeof(RectTransform)).GetComponent<RectTransform>();
        defeatContent.SetParent(defeatSafeArea, false);
        SetCentered(defeatContent, Vector2.zero, new Vector2(1120f, 820f));

        ResultScreenAssets artwork = Resources.Load<ResultScreenAssets>("ResultScreenAssets");
        Sprite wood = artwork != null ? artwork.WoodSign : null;
        Image heading = CreateWoodSign(defeatContent, victoryActive ? "Victory Sign" : "Defeat Sign", wood,
            new Vector2(0f, 210f), new Vector2(1040f, 250f));
        heading.rectTransform.localRotation = Quaternion.Euler(0f, 0f, victoryActive ? 3f : -3f);
        Text title = CreateCartoonText(heading.transform, "Title", victoryActive ? "¡GANASTE!" : "¡PERDISTE!", 120);
        ProgressionUI.Percent(title.rectTransform, 0.10f, 0.17f, 0.90f, 0.85f);
        title.color = new Color(1f, 0.92f, 0.65f);

        Text message = CreateCartoonText(defeatContent, "Message",
            victoryActive ? "¡Nivel completado!" : "¡Inténtalo otra vez!", 42);
        message.fontStyle = FontStyle.Bold;
        SetCentered(message.rectTransform, new Vector2(0f, 70f), new Vector2(900f, 80f));

        Button retry = CreateCartoonButton(defeatContent, "Retry Button", victoryActive ? "REPETIR" : "REINTENTAR", wood,
            new Vector2(0f, -65f), new Vector2(660f, 158f), 65, RestartCurrentScene);
        CreateCartoonButton(defeatContent, "Shop Button", "TIENDA", wood,
            new Vector2(-210f, -235f), new Vector2(360f, 112f), 44, PistolUpgradeShop.Open);
        CreateCartoonButton(defeatContent, "Menu Button", "MENÚ", wood,
            new Vector2(210f, -235f), new Vector2(360f, 112f), 44, ReturnToMenu);
        Canvas.willRenderCanvases += UpdateDefeatLayout;
        Canvas.ForceUpdateCanvases();
        UpdateDefeatLayout();
        EventSystem.current.SetSelectedGameObject(retry.gameObject);
        canvas.gameObject.AddComponent<DefeatBackdrop>().Initialize(canvas, victoryActive);
    }

    private Image CreateWoodSign(Transform parent, string name, Sprite wood, Vector2 position, Vector2 size)
    {
        Image image = ProgressionUI.Image(parent, name, wood != null ? Color.white : new Color(0.35f, 0.16f, 0.06f));
        image.sprite = wood;
        SetCentered(image.rectTransform, position, size);
        Shadow shadow = image.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.65f);
        shadow.effectDistance = new Vector2(10f, -12f);
        return image;
    }

    private Text CreateCartoonText(Transform parent, string name, string label, int size)
    {
        Text text = CreateText(name, parent, label, size, FontStyle.BoldAndItalic);
        text.resizeTextMinSize = 22;
        Outline outline = text.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.075f, 0.035f, 0.015f);
        outline.effectDistance = new Vector2(4f, -4f);
        Shadow shadow = text.gameObject.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
        shadow.effectDistance = new Vector2(5f, -7f);
        return text;
    }

    private Button CreateCartoonButton(Transform parent, string name, string label, Sprite wood,
        Vector2 position, Vector2 size, int textSize, UnityEngine.Events.UnityAction action)
    {
        Image image = CreateWoodSign(parent, name, wood, position, size);
        image.raycastTarget = true;
        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(1f, 0.85f, 0.55f);
        colors.selectedColor = new Color(1f, 0.92f, 0.72f);
        colors.pressedColor = new Color(0.80f, 0.63f, 0.40f);
        colors.fadeDuration = 0.10f;
        button.colors = colors;
        button.onClick.AddListener(action);
        Text text = CreateCartoonText(image.transform, "Label", label, textSize);
        ProgressionUI.Percent(text.rectTransform, 0.12f, 0.18f, 0.88f, 0.84f);
        return button;
    }

    private void UpdateDefeatLayout()
    {
        if (defeatSafeArea == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect area = Screen.safeArea;
        defeatSafeArea.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        defeatSafeArea.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        defeatSafeArea.offsetMin = defeatSafeArea.offsetMax = Vector2.zero;
        Vector2 available = defeatSafeArea.rect.size;
        float scale = Mathf.Min(1.15f, available.x / 1120f, available.y / 820f);
        defeatContent.localScale = Vector3.one * Mathf.Max(0.01f, scale);
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

    private static void SetCentered(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
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
