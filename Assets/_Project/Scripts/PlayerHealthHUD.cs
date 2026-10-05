using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Builds the player health HUD in the Testing and Gameplay scenes.</summary>
public sealed class PlayerHealthHUD : MonoBehaviour
{
    private const float Margin = 24f;
    private const float RowHeight = 64f;
    private const float RowSpacing = 76f;
    private static PlayerHealthHUD active;

    // All three HUDs share the same vertical rhythm, including two-player Testing.
    public static float SecondaryTop => Margin +
        Mathf.Max(1, active != null ? active.rows.Count : 1) * RowSpacing + 12f;

    private static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.09f, 0.94f);
    private static readonly Color RowColor = new Color(0.33f, 0.28f, 0.21f, 1f);
    private static readonly Color DamageFlashColor = new Color(0.55f, 0.12f, 0.08f, 1f);
    private static readonly Color FullHealthColor = new Color(0.88f, 0.19f, 0.10f, 1f);
    private static readonly Color EmptyHealthColor = new Color(0.19f, 0.10f, 0.09f, 1f);
    private static readonly Color TextColor = new Color(0.96f, 0.90f, 0.79f, 1f);

    private readonly List<PlayerRow> rows = new List<PlayerRow>();
    private RectTransform safeAreaRoot;
    private Font font;
    private bool hasDisplayFont;
    private int screenWidth;
    private int screenHeight;
    private Rect lastSafeArea;
    private float nextPlayerRefresh;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterSceneCallback()
    {
        active = null;
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
        font = Resources.Load<Font>("UI/LilitaOne");
        hasDisplayFont = font != null;
        if (font == null)
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
    }

    private void RefreshPlayers()
    {
        PlayerHealth[] players = FindObjectsByType<PlayerHealth>(FindObjectsSortMode.None);
        Array.Sort(players, (left, right) => string.CompareOrdinal(left.name, right.name));

        if (PlayersMatch(players))
            return;

        ClearRows();

        for (int i = 0; i < players.Length; i++)
            CreatePlayerRow(players[i], i, players.Length > 1);
    }

    private bool PlayersMatch(PlayerHealth[] players)
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

    private void CreatePlayerRow(PlayerHealth player, int index, bool multiplayer)
    {
        GameObject rowObject = CreateImage("Health " + player.name, safeAreaRoot, PanelColor);
        RectTransform rowRect = rowObject.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 1f);
        rowRect.anchorMax = new Vector2(1f / 3f, 1f);
        rowRect.pivot = new Vector2(0f, 1f);
        rowRect.sizeDelta = new Vector2(-Margin * 2f, RowHeight);
        rowRect.anchoredPosition = new Vector2(Margin, -Margin - index * RowSpacing);

        Text label = CreateText("Health Label", rowRect, "VIDA", 26,
            FontStyle.Bold, TextAnchor.MiddleLeft);
        float labelWidth = multiplayer ? 180f : 88f;
        if (multiplayer)
            label.text = player.name.Replace("Player_", string.Empty).ToUpperInvariant();
        SetTopLeft(label.rectTransform, new Vector2(16f, 0f), new Vector2(labelWidth, RowHeight));

        Image frame = CreateImage("Health Frame", rowRect, RowColor).GetComponent<Image>();
        Stretch(frame.rectTransform, new Vector2(labelWidth + 24f, 12f), new Vector2(110f, 12f));
        Image track = CreateImage("Health Track", frame.transform, EmptyHealthColor).GetComponent<Image>();
        Stretch(track.rectTransform, Vector2.one * 4f, Vector2.one * 4f);
        Image fill = CreateImage("Health Fill", track.transform, FullHealthColor).GetComponent<Image>();
        Stretch(fill.rectTransform, Vector2.zero, Vector2.zero);
        Image shine = CreateImage("Health Highlight", fill.transform,
            new Color(1f, 0.44f, 0.33f, 0.55f)).GetComponent<Image>();
        shine.rectTransform.anchorMin = new Vector2(0f, 1f);
        shine.rectTransform.anchorMax = Vector2.one;
        shine.rectTransform.pivot = new Vector2(0.5f, 1f);
        shine.rectTransform.sizeDelta = new Vector2(0f, 6f);
        shine.rectTransform.anchoredPosition = Vector2.zero;

        PlayerRow row = new PlayerRow
        {
            player = player,
            root = rowObject,
            background = track,
            fill = fill.rectTransform,
            current = player.CurrentHealth
        };

        row.value = CreateText("Health Value", rowRect, "", 28,
            FontStyle.Bold, TextAnchor.MiddleRight);
        row.value.rectTransform.anchorMin = row.value.rectTransform.anchorMax = new Vector2(1f, 1f);
        row.value.rectTransform.pivot = new Vector2(1f, 1f);
        row.value.rectTransform.anchoredPosition = new Vector2(-16f, 0f);
        row.value.rectTransform.sizeDelta = new Vector2(80f, RowHeight);

        row.healthChanged = (current, maximum) => SetHealth(row, current, maximum, true);
        player.HealthChanged += row.healthChanged;
        rows.Add(row);
        SetHealth(row, player.CurrentHealth, player.MaxHealth, false);
    }

    private void SetHealth(PlayerRow row, int current, int maximum, bool showDamageFeedback)
    {
        maximum = Mathf.Max(1, maximum);
        current = Mathf.Clamp(current, 0, maximum);
        bool lostHealth = current < row.current;
        row.current = current;
        row.value.text = current + " / " + maximum;
        row.fill.anchorMax = new Vector2((float)current / maximum, 1f);

        if (showDamageFeedback && lostHealth && isActiveAndEnabled)
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
            row.background.color = EmptyHealthColor;
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
        text.fontStyle = hasDisplayFont ? FontStyle.Normal : fontStyle;
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

    private static void Stretch(RectTransform rect, Vector2 insetMin, Vector2 insetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = insetMin;
        rect.offsetMax = -insetMax;
    }

    private sealed class PlayerRow
    {
        public PlayerHealth player;
        public GameObject root;
        public Image background;
        public RectTransform fill;
        public int current;
        public Text value;
        public Action<int, int> healthChanged;
        public Coroutine flash;
    }
}
