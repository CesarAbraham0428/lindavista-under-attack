using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class MobileControlsHUD : MonoBehaviour
{
    private static MobileControlsHUD active;

    private readonly List<TouchControlRegion> regions = new List<TouchControlRegion>();
    private readonly TouchControlRegion[] weaponRegions = new TouchControlRegion[3];
    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private PlayerMovement movement;
    private PlayerActions actions;
    private Font font;
    private bool leftHeld;
    private bool rightHeld;
    private int selectedWeapon;
    private int screenWidth;
    private int screenHeight;
    private Rect lastSafeArea;

    private static readonly Color ButtonColor = new Color(0.08f, 0.15f, 0.22f, 0.70f);
    private static readonly Color SelectedColor = new Color(0.08f, 0.45f, 0.57f, 0.88f);
    private static readonly Color AimColor = new Color(0.07f, 0.19f, 0.24f, 0.48f);

    public static bool IsVisible => active != null && active.canvas != null &&
                                    active.canvas.gameObject.activeInHierarchy;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if ((scene.name != "Testing" && scene.name != "Gameplay") ||
            (!Application.isMobilePlatform && !Application.isEditor))
            return;

        if (active == null)
            new GameObject("Mobile Controls HUD").AddComponent<MobileControlsHUD>();
    }

    private void Awake()
    {
        active = this;
        EnsureEventSystem();
        BuildHUD();
        for (int i = 1; i < weaponRegions.Length; i++)
            if (weaponRegions[i] != null) weaponRegions[i].gameObject.SetActive(false);
        SetVisible(Application.isMobilePlatform);
    }

    private void Start()
    {
        FindActivePlayer();
    }

    private void Update()
    {
#if UNITY_EDITOR
        // F9 previews the Android controls with a mouse inside the Unity Editor.
        if (Keyboard.current != null && Keyboard.current.f9Key.wasPressedThisFrame)
            SetVisible(!IsVisible);
#endif

        if (IsVisible && (movement == null || actions == null ||
                          !movement.gameObject.activeInHierarchy))
            FindActivePlayer();

        if (screenWidth != Screen.width || screenHeight != Screen.height ||
            lastSafeArea != Screen.safeArea)
            UpdateSafeArea();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
            ClearTouches();
    }

    private void OnApplicationFocus(bool focused)
    {
        if (!focused)
            ClearTouches();
    }

    private void OnDestroy()
    {
        ClearTouches();
        if (active == this)
            active = null;
    }

    public void Press(TouchControlKind kind, bool pressed)
    {
        switch (kind)
        {
            case TouchControlKind.MoveLeft:
                leftHeld = pressed;
                ApplyMovement();
                break;
            case TouchControlKind.MoveRight:
                rightHeld = pressed;
                ApplyMovement();
                break;
            case TouchControlKind.Pistol:
            case TouchControlKind.SMG:
            case TouchControlKind.RPG:
                if (pressed)
                    SelectWeapon((int)kind - (int)TouchControlKind.Pistol);
                break;
            case TouchControlKind.Reload:
                if (pressed && actions != null)
                    actions.ReloadTouchWeapon();
                break;
        }
    }

    public void AimAndFire(Vector2 direction, bool pressed)
    {
        if (actions == null)
            return;

        if (pressed)
            actions.SetTouchAimAndFire(direction);
        else
            actions.StopTouchAimAndFire();
    }

    private void SelectWeapon(int index)
    {
        if (!ProgressionService.OwnsWeapon(index)) return;
        selectedWeapon = index;
        if (actions != null)
            actions.SelectTouchWeapon(index);

        for (int i = 0; i < weaponRegions.Length; i++)
        {
            if (weaponRegions[i] != null)
                weaponRegions[i].SetRestingColor(i == selectedWeapon
                    ? SelectedColor : ButtonColor);
        }
    }

    private void ApplyMovement()
    {
        if (movement != null)
            movement.SetTouchHorizontal(leftHeld == rightHeld ? 0f : leftHeld ? -1f : 1f);
    }

    private void FindActivePlayer()
    {
        GameObject player = GameObject.Find("Player_Cesar");
        if (player == null)
            player = GameObject.Find("Player_Marco");

        movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        actions = player != null ? player.GetComponent<PlayerActions>() : null;
        ApplyMovement();
        if (actions != null)
            actions.SelectTouchWeapon(selectedWeapon);
    }

    private void ClearTouches()
    {
        foreach (TouchControlRegion region in regions)
        {
            if (region != null)
                region.Cancel();
        }

        leftHeld = false;
        rightHeld = false;
        ApplyMovement();
        if (actions != null)
            actions.StopTouchAimAndFire();
    }

    private void SetVisible(bool visible)
    {
        if (canvas == null)
            return;

        if (!visible)
            ClearTouches();
        canvas.gameObject.SetActive(visible);
    }

    private static void EnsureEventSystem()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
        {
            GameObject go = new GameObject("Touch Event System");
            eventSystem = go.AddComponent<EventSystem>();
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

    private void BuildHUD()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject canvasObject = new GameObject("Touch Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject safeRoot = new GameObject("Safe Area", typeof(RectTransform));
        safeRoot.transform.SetParent(canvasObject.transform, false);
        safeAreaRoot = safeRoot.GetComponent<RectTransform>();
        UpdateSafeArea();

        CreateControl("Move Left", "<", TouchControlKind.MoveLeft,
            Vector2.zero, new Vector2(130f, 135f), new Vector2(150f, 145f),
            ButtonColor, 72);
        CreateControl("Move Right", ">", TouchControlKind.MoveRight,
            Vector2.zero, new Vector2(310f, 135f), new Vector2(150f, 145f),
            ButtonColor, 72);

        weaponRegions[0] = CreateControl("Pistol", "1  P", TouchControlKind.Pistol,
            new Vector2(1f, 0f), new Vector2(-95f, 405f), new Vector2(112f, 86f),
            SelectedColor, 30);
        weaponRegions[1] = CreateControl("SMG", "2  SMG", TouchControlKind.SMG,
            new Vector2(1f, 0f), new Vector2(-225f, 405f), new Vector2(112f, 86f),
            ButtonColor, 25);
        weaponRegions[2] = CreateControl("RPG", "3  RPG", TouchControlKind.RPG,
            new Vector2(1f, 0f), new Vector2(-355f, 405f), new Vector2(112f, 86f),
            ButtonColor, 25);

        CreateControl("Reload", "REC", TouchControlKind.Reload,
            new Vector2(1f, 0f), new Vector2(-465f, 145f),
            new Vector2(125f, 110f), ButtonColor, 34);
        CreateControl("Aim and Fire", "APUNTA\nDISPARA", TouchControlKind.AimAndFire,
            new Vector2(1f, 0f), new Vector2(-185f, 170f),
            new Vector2(300f, 300f), AimColor, 34);
    }

    private TouchControlRegion CreateControl(string objectName, string label,
        TouchControlKind kind, Vector2 anchor, Vector2 position, Vector2 size,
        Color color, int fontSize)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image), typeof(TouchControlRegion));
        go.transform.SetParent(safeAreaRoot, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        CreateLabel(rect, label, fontSize);

        RectTransform knob = null;
        if (kind == TouchControlKind.AimAndFire)
        {
            GameObject knobObject = new GameObject("Aim Direction", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            knobObject.transform.SetParent(rect, false);
            knob = knobObject.GetComponent<RectTransform>();
            knob.anchorMin = knob.anchorMax = new Vector2(0.5f, 0.5f);
            knob.sizeDelta = new Vector2(62f, 62f);
            Image knobImage = knobObject.GetComponent<Image>();
            knobImage.color = new Color(0.30f, 0.85f, 0.90f, 0.70f);
            knobImage.raycastTarget = false;
        }

        TouchControlRegion region = go.GetComponent<TouchControlRegion>();
        region.Initialize(this, kind, image, knob);
        regions.Add(region);
        return region;
    }

    private void CreateLabel(RectTransform parent, string label, int fontSize)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = label;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 18;
        text.resizeTextMaxSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
    }

    private void UpdateSafeArea()
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        screenWidth = Screen.width;
        screenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        Rect area = lastSafeArea;
        safeAreaRoot.anchorMin = new Vector2(area.xMin / screenWidth,
            area.yMin / screenHeight);
        safeAreaRoot.anchorMax = new Vector2(area.xMax / screenWidth,
            area.yMax / screenHeight);
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }
}
