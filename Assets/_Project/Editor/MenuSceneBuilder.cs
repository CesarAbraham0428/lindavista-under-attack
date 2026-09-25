using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MenuSceneBuilder
{
    private const string UiAssetPath = "Assets/_Project/Art/UI/Menu/";
    private const string ScenePath = "Assets/_Project/Scenes/";

    [MenuItem("Tools/Lindavista/Build menu screens")]
    public static void Build()
    {
        BuildMainMenu();
        BuildCharacterSelection();
        ConfigureBuildScenes();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(ScenePath + "MainMenu.unity", OpenSceneMode.Single);
        Debug.Log("Lindavista menu screens are ready. MainMenu is the first build scene.");
    }

    private static void BuildMainMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath + "MainMenu.unity", OpenSceneMode.Single);
        RemoveExistingMenuObjects(scene);

        Canvas canvas = CreateCanvas("Main Menu Canvas");
        AddFullScreenImage(canvas.transform, "Background", LoadMenuSprite("fondo_inicio.png"));

        AddText(canvas.transform, "Game Title", "LINDAVISTA", 132,
            new Vector2(190f, 404f), new Vector2(1040f, 170f), new Color(1f, 0.96f, 0.84f));
        AddText(canvas.transform, "Game Subtitle", "UNDER ATTACK", 92,
            new Vector2(190f, 300f), new Vector2(920f, 120f), new Color(0.97f, 0.08f, 0.08f));

        AddImage(canvas.transform, "Lindavista Street Sign", LoadMenuSprite("cartel_lindavista.png"),
            new Vector2(-646f, 22f), new Vector2(340f, 114f));
        AddImage(canvas.transform, "Marco Portrait", LoadSprite("Assets/_Project/Art/Characters/Players/Marco_Walk_8f.png"),
            new Vector2(-712f, -302f), new Vector2(228f, 358f));
        AddImage(canvas.transform, "Cesar Portrait", LoadSprite("Assets/_Project/Art/Characters/Players/Cesar_Walk_8f.png"),
            new Vector2(-466f, -302f), new Vector2(228f, 358f));

        Button playButton = AddButton(canvas.transform, "Play Button", LoadMenuSprite("cartel_jugar.png"),
            new Vector2(190f, 105f), new Vector2(470f, 160f));
        Button storeButton = AddButton(canvas.transform, "Store Button", LoadMenuSprite("cartel_tienda.png"),
            new Vector2(190f, -82f), new Vector2(470f, 160f));
        Button settingsButton = AddButton(canvas.transform, "Settings Button", LoadMenuSprite("cartel_ajustes.png"),
            new Vector2(190f, -269f), new Vector2(470f, 160f));

        GameObject storePanel = CreateOverlayPanel(canvas.transform, "Store Panel");
        AddImage(storePanel.transform, "Store Heading", LoadMenuSprite("cartel_tienda.png"),
            new Vector2(0f, 158f), new Vector2(540f, 180f));
        AddText(storePanel.transform, "Store Message", "PRÓXIMAMENTE", 46,
            new Vector2(0f, 0f), new Vector2(720f, 90f), new Color(1f, 0.91f, 0.72f));
        Button closeStoreButton = AddButton(storePanel.transform, "Back Button", LoadMenuSprite("carte_flecha_hacia_atras.png"),
            new Vector2(0f, -180f), new Vector2(150f, 112f));

        GameObject settingsPanel = CreateOverlayPanel(canvas.transform, "Settings Panel");
        AddImage(settingsPanel.transform, "Settings Heading", LoadMenuSprite("cartel_ajustes.png"),
            new Vector2(0f, 176f), new Vector2(540f, 180f));
        AddText(settingsPanel.transform, "Master Volume Label", "Volumen general", 38,
            new Vector2(-245f, 22f), new Vector2(380f, 84f), Color.white);
        Slider volumeSlider = AddVolumeSlider(settingsPanel.transform,
            new Vector2(260f, 22f), new Vector2(420f, 54f));
        Button closeSettingsButton = AddButton(settingsPanel.transform, "Back Button", LoadMenuSprite("carte_flecha_hacia_atras.png"),
            new Vector2(0f, -180f), new Vector2(150f, 112f));

        MainMenuController controller = canvas.gameObject.AddComponent<MainMenuController>();
        SetObjectReference(controller, "playButton", playButton);
        SetObjectReference(controller, "storeButton", storeButton);
        SetObjectReference(controller, "settingsButton", settingsButton);
        SetObjectReference(controller, "closeStoreButton", closeStoreButton);
        SetObjectReference(controller, "closeSettingsButton", closeSettingsButton);
        SetObjectReference(controller, "storePanel", storePanel);
        SetObjectReference(controller, "settingsPanel", settingsPanel);
        SetObjectReference(controller, "masterVolumeSlider", volumeSlider);

        CreateEventSystem();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static void BuildCharacterSelection()
    {
        Scene scene = EditorSceneManager.OpenScene(ScenePath + "CharacterSelection.unity", OpenSceneMode.Single);
        RemoveExistingMenuObjects(scene);

        Canvas canvas = CreateCanvas("Character Selection Canvas");
        AddFullScreenImage(canvas.transform, "Background", LoadMenuSprite("fondo_eleccion_personaje_nivel.png"));

        Button backButton = AddButton(canvas.transform, "Back Button", LoadMenuSprite("carte_flecha_hacia_atras.png"),
            new Vector2(-842f, 452f), new Vector2(142f, 106f));
        AddImage(canvas.transform, "Character Heading", LoadMenuSprite("cartel_seleccionar_personaje.png"),
            new Vector2(-610f, 424f), new Vector2(510f, 170f));
        AddImage(canvas.transform, "Level Heading", LoadMenuSprite("cartel_seleccionar_nivel.png"),
            new Vector2(320f, 424f), new Vector2(510f, 170f));

        Button[] characterButtons = new Button[2];
        Outline[] characterOutlines = new Outline[2];
        Sprite[] characterSprites =
        {
            LoadSprite("Assets/_Project/Art/Characters/Players/Marco_Walk_8f.png"),
            LoadSprite("Assets/_Project/Art/Characters/Players/Cesar_Walk_8f.png")
        };
        string[] characterNames = { "MARCO", "CÉSAR" };
        float[] characterX = { -754f, -466f };

        for (int i = 0; i < characterButtons.Length; i++)
        {
            characterButtons[i] = AddButton(canvas.transform, characterNames[i] + " Character Card",
                null, new Vector2(characterX[i], -28f), new Vector2(286f, 626f));
            characterButtons[i].GetComponent<Image>().color = new Color(0.045f, 0.055f, 0.07f, 0.88f);
            characterOutlines[i] = characterButtons[i].GetComponent<Outline>();
            AddImage(characterButtons[i].transform, characterNames[i] + " Portrait", characterSprites[i],
                new Vector2(0f, 38f), new Vector2(248f, 430f));
            AddText(characterButtons[i].transform, characterNames[i] + " Label", characterNames[i], 42,
                new Vector2(0f, -263f), new Vector2(250f, 70f), Color.white);
        }

        string[] levelFiles =
        {
            "portada_1_pm.png", "portada_6_pm.png", "portada_9_pm.png", "portada_3_am.png"
        };
        Button[] levelButtons = new Button[levelFiles.Length];
        Outline[] levelOutlines = new Outline[levelFiles.Length];
        Image[] levelLockIcons = new Image[levelFiles.Length];
        Vector2[] levelPositions =
        {
            new Vector2(-150f, 42f), new Vector2(134f, 42f),
            new Vector2(418f, 42f), new Vector2(702f, 42f)
        };

        for (int i = 0; i < levelFiles.Length; i++)
        {
            levelButtons[i] = AddButton(canvas.transform, "Level " + (i + 1) + " Card", null,
                levelPositions[i], new Vector2(260f, 406f));
            levelButtons[i].GetComponent<Image>().color = new Color(0.06f, 0.055f, 0.07f, 0.92f);
            levelOutlines[i] = levelButtons[i].GetComponent<Outline>();
            AddImage(levelButtons[i].transform, "Level " + (i + 1) + " Artwork", LoadMenuSprite(levelFiles[i]),
                new Vector2(0f, 0f), new Vector2(244f, 366f));

            Image numberBoard = AddImage(canvas.transform, "Level " + (i + 1) + " Number Board",
                LoadMenuSprite("cartel_vacio.png"), new Vector2(levelPositions[i].x, -202f), new Vector2(164f, 74f));
            numberBoard.raycastTarget = false;
            AddText(canvas.transform, "Level " + (i + 1) + " Number", (i + 1).ToString(), 46,
                new Vector2(levelPositions[i].x, -202f), new Vector2(120f, 62f), Color.white);

            if (i > 0)
            {
                levelLockIcons[i] = AddImage(canvas.transform, "Level " + (i + 1) + " Lock",
                    LoadMenuSprite("candado.png"), new Vector2(levelPositions[i].x, -133f), new Vector2(70f, 82f));
                levelLockIcons[i].raycastTarget = false;
            }
        }

        Button startButton = AddButton(canvas.transform, "Start Game Button", LoadMenuSprite("cartel_jugar.png"),
            new Vector2(320f, -360f), new Vector2(390f, 132f));

        CharacterLevelSelectionController controller = canvas.gameObject.AddComponent<CharacterLevelSelectionController>();
        SetObjectArray(controller, "characterButtons", characterButtons);
        SetObjectArray(controller, "characterOutlines", characterOutlines);
        SetObjectArray(controller, "levelButtons", levelButtons);
        SetObjectArray(controller, "levelOutlines", levelOutlines);
        SetBoolArray(controller, "levelUnlocked", new[] { true, false, false, false });
        SetObjectArray(controller, "levelLockIcons", levelLockIcons);
        SetObjectReference(controller, "backButton", backButton);
        SetObjectReference(controller, "startButton", startButton);

        CreateEventSystem();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    private static Canvas CreateCanvas(string name)
    {
        GameObject canvasObject = new GameObject(name, typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }

    private static void AddFullScreenImage(Transform parent, string name, Sprite sprite)
    {
        Image image = CreateImageObject(parent, name, sprite, Vector2.zero, Vector2.zero, Color.white, false);
        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.SetAsFirstSibling();
    }

    private static Image AddImage(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image image = CreateImageObject(parent, name, sprite, position, size, Color.white, true);
        image.raycastTarget = false;
        return image;
    }

    private static Image CreateImageObject(Transform parent, string name, Sprite sprite,
        Vector2 position, Vector2 size, Color color, bool preserveAspect)
    {
        GameObject imageObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        imageObject.transform.SetParent(parent, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Image image = imageObject.GetComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = preserveAspect;
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    private static Button AddButton(Transform parent, string name, Sprite sprite, Vector2 position, Vector2 size)
    {
        Image image = CreateImageObject(parent, name, sprite, position, size, Color.white, true);
        image.raycastTarget = true;

        Button button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = new Color(1f, 0.86f, 0.56f, 1f),
            pressedColor = new Color(0.78f, 0.66f, 0.48f, 1f),
            selectedColor = Color.white,
            disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.72f),
            colorMultiplier = 1f,
            fadeDuration = 0.12f
        };

        Outline outline = image.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(1f, 0.73f, 0.18f, 1f);
        outline.effectDistance = new Vector2(7f, -7f);
        outline.useGraphicAlpha = true;
        outline.enabled = false;
        return button;
    }

    private static Text AddText(Transform parent, string name, string value, int fontSize,
        Vector2 position, Vector2 size, Color color)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        textObject.transform.SetParent(parent, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;

        Text text = textObject.GetComponent<Text>();
        text.text = value;
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.color = color;
        text.raycastTarget = false;

        Outline outline = textObject.AddComponent<Outline>();
        outline.effectColor = new Color(0.08f, 0.05f, 0.03f, 0.95f);
        outline.effectDistance = new Vector2(2f, -2f);
        return text;
    }

    private static GameObject CreateOverlayPanel(Transform parent, string name)
    {
        GameObject overlay = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
        overlay.transform.SetParent(parent, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        Image dimmer = overlay.AddComponent<Image>();
        dimmer.color = new Color(0.025f, 0.018f, 0.014f, 0.78f);
        dimmer.raycastTarget = true;

        GameObject frame = new GameObject("Panel Frame", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        frame.transform.SetParent(overlay.transform, false);
        RectTransform frameRect = frame.GetComponent<RectTransform>();
        frameRect.anchorMin = frameRect.anchorMax = new Vector2(0.5f, 0.5f);
        frameRect.sizeDelta = new Vector2(1040f, 610f);
        Image frameImage = frame.GetComponent<Image>();
        frameImage.color = new Color(0.12f, 0.075f, 0.045f, 0.97f);
        frameImage.raycastTarget = false;
        Outline frameOutline = frame.GetComponent<Outline>();
        frameOutline.effectColor = new Color(0.94f, 0.55f, 0.17f, 1f);
        frameOutline.effectDistance = new Vector2(4f, -4f);
        frameOutline.useGraphicAlpha = false;

        CanvasGroup group = overlay.GetComponent<CanvasGroup>();
        group.interactable = true;
        group.blocksRaycasts = true;
        overlay.SetActive(false);
        return overlay;
    }

    private static Slider AddVolumeSlider(Transform parent, Vector2 position, Vector2 size)
    {
        GameObject sliderObject = new GameObject("Master Volume Slider", typeof(RectTransform), typeof(Slider));
        sliderObject.transform.SetParent(parent, false);
        RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
        sliderRect.anchorMin = sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
        sliderRect.sizeDelta = size;
        sliderRect.anchoredPosition = position;

        Image track = CreateImageObject(sliderObject.transform, "Track", null, Vector2.zero,
            new Vector2(size.x, 14f), new Color(0.18f, 0.12f, 0.08f, 1f), false);
        RectTransform trackRect = track.rectTransform;
        trackRect.anchorMin = Vector2.zero;
        trackRect.anchorMax = Vector2.one;
        trackRect.offsetMin = new Vector2(0f, 14f);
        trackRect.offsetMax = new Vector2(0f, -14f);

        GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
        fillArea.transform.SetParent(sliderObject.transform, false);
        RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
        fillAreaRect.anchorMin = Vector2.zero;
        fillAreaRect.anchorMax = Vector2.one;
        fillAreaRect.offsetMin = new Vector2(4f, 14f);
        fillAreaRect.offsetMax = new Vector2(-4f, -14f);

        Image fill = CreateImageObject(fillArea.transform, "Fill", null, Vector2.zero,
            Vector2.zero, new Color(0.97f, 0.57f, 0.15f, 1f), false);
        RectTransform fillRect = fill.rectTransform;
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
        handleArea.transform.SetParent(sliderObject.transform, false);
        RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
        handleAreaRect.anchorMin = Vector2.zero;
        handleAreaRect.anchorMax = Vector2.one;
        handleAreaRect.offsetMin = new Vector2(10f, 0f);
        handleAreaRect.offsetMax = new Vector2(-10f, 0f);

        Image handle = CreateImageObject(handleArea.transform, "Handle", null, Vector2.zero,
            new Vector2(34f, 34f), new Color(1f, 0.86f, 0.59f, 1f), false);
        handle.raycastTarget = true;

        Slider slider = sliderObject.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.wholeNumbers = false;
        slider.direction = Slider.Direction.LeftToRight;
        slider.fillRect = fillRect;
        slider.handleRect = handle.rectTransform;
        slider.targetGraphic = handle;
        slider.value = 1f;
        return slider;
    }

    private static void CreateEventSystem()
    {
        GameObject eventSystemObject = new GameObject("Menu Event System", typeof(EventSystem), typeof(MenuInputModuleBootstrap));
    }

    private static void RemoveExistingMenuObjects(Scene scene)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            if (root.name.EndsWith(" Canvas", StringComparison.Ordinal) || root.name == "Menu Event System")
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ConfigureBuildScenes()
    {
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath + "MainMenu.unity", true),
            new EditorBuildSettingsScene(ScenePath + "CharacterSelection.unity", true),
            new EditorBuildSettingsScene(ScenePath + "Testing.unity", true)
        };
    }

    private static Sprite LoadMenuSprite(string fileName)
    {
        return LoadSprite(UiAssetPath + fileName);
    }

    private static Sprite LoadSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderByDescending(candidate => candidate.rect.width * candidate.rect.height)
            .FirstOrDefault();

        if (sprite == null)
            throw new InvalidOperationException("No sprite could be loaded from " + path);

        return sprite;
    }

    private static void SetObjectReference(UnityEngine.Object target, string propertyName, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Serialized field not found: " + propertyName);

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetObjectArray(UnityEngine.Object target, string propertyName, UnityEngine.Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Serialized field not found: " + propertyName);

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetBoolArray(UnityEngine.Object target, string propertyName, bool[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
            throw new InvalidOperationException("Serialized field not found: " + propertyName);

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            property.GetArrayElementAtIndex(i).boolValue = values[i];

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
