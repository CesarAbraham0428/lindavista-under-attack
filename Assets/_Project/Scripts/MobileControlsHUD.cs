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
    private Canvas canvas;
    private RectTransform safeAreaRoot;
    private PlayerMovement movement;
    private PlayerActions actions;
    private Font font;
    private float moveInput;
    private static Sprite circleSprite;
    private int screenWidth;
    private int screenHeight;
    private Rect lastSafeArea;
    private bool vistaSolicitada;
    private bool entradaPermitidaAnterior;
    private float proximaBusquedaJugador;

    private static readonly Color ButtonColor = new Color(0.08f, 0.15f, 0.22f, 0.70f);
    private static readonly Color ZoneColor = new Color(0.07f, 0.19f, 0.24f, 0.14f);

    public static bool IsVisible => active != null && active.canvas != null &&
                                    active.canvas.isActiveAndEnabled;

    public bool PuedeRecibirEntrada => actions != null && movement != null &&
        movement.isActiveAndEnabled && actions.CanReceiveCombatInput;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Register()
    {
        active = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Gameplay" ||
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
            SetVisible(!vistaSolicitada);
#endif

        ActualizarVisibilidad();
        bool entradaPermitida = PuedeRecibirEntrada;
        if (entradaPermitidaAnterior && !entradaPermitida) ClearTouches();
        entradaPermitidaAnterior = entradaPermitida;
        if (IsVisible && Time.unscaledTime >= proximaBusquedaJugador &&
            (movement == null || actions == null || !movement.gameObject.activeInHierarchy))
        {
            FindActivePlayer();
            proximaBusquedaJugador = Time.unscaledTime + 0.5f;
        }

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
        if (pressed && !PuedeRecibirEntrada) return;
        switch (kind)
        {
            case TouchControlKind.Pistol:
            case TouchControlKind.SMG:
            case TouchControlKind.RPG:
                if (pressed)
                    WeaponSelectionHUD.Select((int)kind - (int)TouchControlKind.Pistol);
                break;
            case TouchControlKind.Reload:
                if (pressed && actions != null)
                    actions.ReloadTouchWeapon();
                break;
        }
    }

    public void AimAndFire(Vector2 direction, bool pressed)
    {
        if (actions == null || (pressed && !PuedeRecibirEntrada))
            return;

        if (pressed)
            actions.SetTouchAimAndFire(direction);
        else
            actions.StopTouchAimAndFire();
    }

    public void SetMove(float horizontal)
    {
        if (horizontal != 0f && !PuedeRecibirEntrada) return;
        moveInput = Mathf.Clamp(horizontal, -1f, 1f);
        ApplyMovement();
    }

    private void ApplyMovement()
    {
        if (movement != null)
            movement.SetTouchHorizontal(moveInput);
    }

    private void FindActivePlayer()
    {
        GameObject player = GameObject.Find("Player_Cesar");
        if (player == null)
            player = GameObject.Find("Player_Marco");

        movement = player != null ? player.GetComponent<PlayerMovement>() : null;
        actions = player != null ? player.GetComponent<PlayerActions>() : null;
        ApplyMovement();
    }

    private void ClearTouches()
    {
        foreach (TouchControlRegion region in regions)
        {
            if (region != null)
                region.Cancel();
        }

        moveInput = 0f;
        ApplyMovement();
        if (actions != null)
            actions.StopTouchAimAndFire();
    }

    public static void CancelActiveTouches()
    {
        if (active != null) active.ClearTouches();
    }

    private void SetVisible(bool visible)
    {
        vistaSolicitada = visible;
        ActualizarVisibilidad();
    }

    private void ActualizarVisibilidad()
    {
        if (canvas == null)
            return;
        bool visible = vistaSolicitada && !GameFlowController.IsPaused &&
            !GameFlowController.IsMatchEnded && !PistolUpgradeShop.IsOpen && !BossIntroduction.IsActive;
        if (canvas.gameObject.activeSelf == visible) return;
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
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject safeRoot = new GameObject("Safe Area", typeof(RectTransform));
        safeRoot.transform.SetParent(canvasObject.transform, false);
        safeAreaRoot = safeRoot.GetComponent<RectTransform>();
        UpdateSafeArea();

        // Joysticks flotantes: la zona es amplia y el joystick aparece donde cae el dedo.
        CreateControl("Move Stick", "MOVER", TouchControlKind.MoveStick,
            Vector2.zero, new Vector2(270f, 190f), new Vector2(480f, 340f),
            ZoneColor, 30);
        CreateControl("Aim and Fire", "DISPARO\n(arrastra para apuntar)", TouchControlKind.AimAndFire,
            new Vector2(1f, 0f), new Vector2(-270f, 190f),
            new Vector2(480f, 340f), ZoneColor, 30);
        // La recarga queda fuera de la zona de disparo para no pulsarla sin querer.
        CreateControl("Reload", "", TouchControlKind.Reload,
            new Vector2(1f, 0f), new Vector2(-640f, 130f),
            new Vector2(150f, 150f), ButtonColor, 34);
    }

    // Icono de recarga: flecha circular dibujada por código (sin depender de fuentes).
    private static Sprite reloadSprite;

    private static Sprite ReloadSprite()
    {
        if (reloadSprite != null) return reloadSprite;
        const int size = 128;
        const float outer = 46f, inner = 33f, mid = (outer + inner) * 0.5f;
        const float startDeg = 330f, endDeg = 50f; // arco de 50° a 330° (hueco a la derecha)
        Vector2 c = new Vector2(size * 0.5f, size * 0.5f);
        float endRad = endDeg * Mathf.Deg2Rad;
        Vector2 radial = new Vector2(Mathf.Cos(endRad), Mathf.Sin(endRad));
        Vector2 tangent = new Vector2(Mathf.Sin(endRad), -Mathf.Cos(endRad)); // sentido horario
        Vector2 headBase = c + radial * mid;
        Vector2 a = headBase + radial * 19f;
        Vector2 b = headBase - radial * 19f;
        Vector2 tip = headBase + tangent * 30f;

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Vector2 d = p - c;
                float r = d.magnitude;
                float deg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (deg < 0f) deg += 360f;
                float alpha = 0f;
                if (deg >= endDeg && deg <= startDeg)
                    alpha = Mathf.Clamp01(Mathf.Min(r - inner, outer - r) + 0.5f);
                if (InTriangle(p, a, b, tip)) alpha = 1f;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply(false, true);
        reloadSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        reloadSprite.hideFlags = HideFlags.HideAndDontSave;
        return reloadSprite;
    }

    private static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
        bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
        bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(neg && pos);
    }

    private static float Cross(Vector2 p, Vector2 a, Vector2 b) =>
        (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);

    private static Sprite CircleSprite()
    {
        if (circleSprite != null) return circleSprite;
        const int size = 128;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Bilinear
        };
        float half = size * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f),
                    new Vector2(half, half));
                float alpha = Mathf.Clamp01((half - distance) / 1.5f);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        texture.Apply(false, true);
        circleSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f), 100f);
        circleSprite.hideFlags = HideFlags.HideAndDontSave;
        return circleSprite;
    }

    private static RectTransform CreateCircle(string objectName, Transform parent,
        float diameter, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(diameter, diameter);
        Image circle = go.GetComponent<Image>();
        circle.sprite = CircleSprite();
        circle.color = color;
        circle.raycastTarget = false;
        return rect;
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
        if (kind == TouchControlKind.Reload)
        {
            image.sprite = CircleSprite();
            RectTransform icon = CreateCircle("Reload Icon", rect, size.x * 0.62f, Color.white);
            icon.GetComponent<Image>().sprite = ReloadSprite();
        }
        else
            CreateLabel(rect, label, fontSize);

        RectTransform stickBase = null;
        RectTransform knob = null;
        if (kind == TouchControlKind.MoveStick || kind == TouchControlKind.AimAndFire)
        {
            bool aim = kind == TouchControlKind.AimAndFire;
            Color tint = aim ? new Color(1f, 0.62f, 0.25f, 1f) : new Color(0.35f, 0.80f, 1f, 1f);
            stickBase = CreateCircle("Stick Base", rect, TouchControlRegion.StickRadius * 2f,
                new Color(1f, 1f, 1f, 1f));
            knob = CreateCircle("Stick Knob", rect, TouchControlRegion.StickRadius * 0.85f, tint);
            // Las etiquetas quedan detrás del joystick, como pista de uso.
            rect.Find("Label").SetAsFirstSibling();
        }

        TouchControlRegion region = go.GetComponent<TouchControlRegion>();
        region.Initialize(this, kind, image, stickBase, knob);
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
