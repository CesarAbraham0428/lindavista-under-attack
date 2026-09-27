using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the player health HUD in the Testing and Gameplay scenes.</summary>
public sealed class PlayerHealthHUD : MonoBehaviour
{
    private const int CellCount = 5;
    private static PlayerHealthHUD active;

    private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.09f, 0.94f);
    private static readonly Color RowColor = new Color(0.18f, 0.16f, 0.15f, 0.98f);
    private static readonly Color DamageFlashColor = new Color(0.55f, 0.12f, 0.08f, 1f);
    private static readonly Color FullHealthColor = new Color(0.88f, 0.19f, 0.10f, 1f);
    private static readonly Color EmptyHealthColor = new Color(0.29f, 0.17f, 0.16f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.90f, 0.79f, 1f);

    private readonly List<PlayerRow> rows = new List<PlayerRow>();
    private RectTransform safeAreaRoot;
    private Text title;
    private Font font;
    private int screenWidth;
    private int screenHeight;
    private Rect lastSafeArea;
    private float nextPlayerRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if ((scene.name != "Testing" && scene.name != "Gameplay") || active != null)
            return;

        new GameObject("Player Health HUD").AddComponent<PlayerHealthHUD>();
    }

    private void Awake()
    {
        active = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        BuildCanvas();
    }

    private void Start()
    {
        RefreshPlayers();
    }

    private void Update()
    {
        if (screenWidth != Screen.width || screenHeight != Screen.height ||
            lastSafeArea != Screen.safeArea)
            UpdateSafeArea();

        if (Time.unscaledTime >= nextPlayerRefresh)
        {
            RefreshPlayers();
            nextPlayerRefresh = Time.unscaledTime + 0.5f;
        }
    }

    private void OnDestroy()
    {
        ClearRows();
        if (active == this)
            active = null;
    }

    private void BuildCanvas()
    {
        GameObject canvasObject = new GameObject("Health Canvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 110;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject safeAreaObject = new GameObject("Safe Area", typeof(RectTransform));
        safeAreaObject.transform.SetParent(canvasObject.transform, false);
        safeAreaRoot = safeAreaObject.GetComponent<RectTransform>();
        UpdateSafeArea();

        GameObject panelObject = CreateImage("Player Health Panel", safeAreaRoot, PanelColor);
        RectTransform panel = panelObject.GetComponent<RectTransform>();
        SetTopLeft(panel, new Vector2(24f, -24f), new Vector2(440f, 146f));

        GameObject accentObject = CreateImage("Accent", panel, FullHealthColor);
        RectTransform accent = accentObject.GetComponent<RectTransform>();
        SetTopLeft(accent, new Vector2(0f, 0f), new Vector2(5f, 146f));

        title = CreateText("Title", panel, "VIDA", 24, FontStyle.Bold, TextAnchor.MiddleLeft);
        SetTopLeft(title.rectTransform, new Vector2(16f, -7f), new Vector2(390f, 32f));
    }

    private void RefreshPlayers()
    {
        PlayerActions[] players = FindObjectsByType<PlayerActions>(FindObjectsSortMode.None);
        Array.Sort(players, (left, right) => string.CompareOrdinal(left.name, right.name));

        if (PlayersMatch(players))
            return;

        ClearRows();
        if (title != null)
            title.text = players.Length == 0 ? "VIDA · SIN PERSONAJE" : "VIDA";

        for (int i = 0; i < players.Length; i++)
            CreatePlayerRow(players[i], i);
    }

    private bool PlayersMatch(PlayerActions[] players)
    {
        if (players.Length != rows.Count)
            return false;

        for (int i = 0; i < players.Length; i++)
        {
            if (rows[i].player != players[i])
                return false;
        }

        return true;
    }

    private void CreatePlayerRow(PlayerActions player, int index)
    {
        GameObject rowObject = CreateImage("Health " + player.name, safeAreaRoot, RowColor);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        SetTopLeft(rowRect, new Vector2(40f, -64f - index * 42f), new Vector2(398f, 38f));

        PlayerRow row = new PlayerRow
        {
            player = player,
            root = rowObject,
            background = rowObject.GetComponent<Image>(),
            segments = new Image[CellCount]
        };

        string displayName = player.gameObject.name.Replace("Player_", string.Empty).ToUpperInvariant();
        Text nameText = CreateText("Character", rowRect, displayName, 24,
            FontStyle.Bold, TextAnchor.MiddleLeft);
        SetTopLeft(nameText.rectTransform, new Vector2(12f, 0f), new Vector2(96f, 38f));

        for (int i = 0; i < CellCount; i++)
        {
            GameObject segment = CreateImage("Health " + (i + 1), rowRect, EmptyHealthColor);
            RectTransform segmentRect = segment.GetComponent<RectTransform>();
            SetTopLeft(segmentRect, new Vector2(112f + i * 43f, -7f), new Vector2(36f, 23f));
            row.segments[i] = segment.GetComponent<Image>();
        }

        row.value = CreateText("Health Value", rowRect, "5/5", 22,
            FontStyle.Bold, TextAnchor.MiddleRight);
        SetTopLeft(row.value.rectTransform, new Vector2(326f, 0f), new Vector2(60f, 38f));

        row.healthChanged = (current, maximum) => SetHealth(row, current, maximum, true);
        player.HealthChanged += row.healthChanged;
        rows.Add(row);
        SetHealth(row, player.CurrentHealth, player.MaxHealth, false);
    }

    private void SetHealth(PlayerRow row, int current, int maximum, bool showDamageFeedback)
    {
        current = Mathf.Clamp(current, 0, maximum);
        row.value.text = current + "/" + maximum;
        for (int i = 0; i < row.segments.Length; i++)
            row.segments[i].color = i < current ? FullHealthColor : EmptyHealthColor;

        if (showDamageFeedback && isActiveAndEnabled)
        {
            if (row.flash != null)
                StopCoroutine(row.flash);
            row.flash = StartCoroutine(FlashRow(row));
        }
    }

    private IEnumerator FlashRow(PlayerRow row)
    {
        row.background.color = DamageFlashColor;
        yield return new WaitForSecondsRealtime(0.16f);
        if (row.background != null)
            row.background.color = RowColor;
        row.flash = null;
    }

    private void ClearRows()
    {
        foreach (PlayerRow row in rows)
        {
            if (row.player != null && row.healthChanged != null)
                row.player.HealthChanged -= row.healthChanged;
            if (row.flash != null)
                StopCoroutine(row.flash);
            if (row.root != null)
                Destroy(row.root);
        }

        rows.Clear();
    }

    private void UpdateSafeArea()
    {
        if (safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            return;

        screenWidth = Screen.width;
        screenHeight = Screen.height;
        lastSafeArea = Screen.safeArea;
        safeAreaRoot.anchorMin = new Vector2(lastSafeArea.xMin / screenWidth,
            lastSafeArea.yMin / screenHeight);
        safeAreaRoot.anchorMax = new Vector2(lastSafeArea.xMax / screenWidth,
            lastSafeArea.yMax / screenHeight);
        safeAreaRoot.offsetMin = Vector2.zero;
        safeAreaRoot.offsetMax = Vector2.zero;
    }

    private static GameObject CreateImage(string objectName, Transform parent, Color color)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return go;
    }

    private Text CreateText(string objectName, Transform parent, string value,
        int fontSize, FontStyle fontStyle, TextAnchor alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform),
            typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        Text text = go.GetComponent<Text>();
        text.font = font;
        text.text = value;
        text.fontSize = fontSize;
        text.fontStyle = fontStyle;
        text.alignment = alignment;
        text.color = TextColor;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = fontSize;
        text.raycastTarget = false;
        return text;
    }

    private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }

    private sealed class PlayerRow
    {
        public PlayerActions player;
        public GameObject root;
        public Image background;
        public Image[] segments;
        public Text value;
        public Action<int, int> healthChanged;
        public Coroutine flash;
    }
}
