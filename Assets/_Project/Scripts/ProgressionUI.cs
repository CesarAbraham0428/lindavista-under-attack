using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public static class ProgressionUI
{
    public static readonly Color PanelColor = new Color(0.07f, 0.08f, 0.09f, 0.96f);
    public static readonly Color Gold = new Color(1f, 0.78f, 0.26f);
    public static Canvas Canvas(Transform parent, string name, int order)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = order;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return canvas;
    }
    public static Image Image(Transform parent, string name, Color color, bool blocks = false)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = blocks;
        return image;
    }
    public static Text Text(Transform parent, string name, string value, int size, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.text = value;
        text.color = new Color(0.96f, 0.93f, 0.85f);
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 12;
        text.resizeTextMaxSize = size;
        text.raycastTarget = false;
        return text;
    }
    public static Button Button(Transform parent, string label, UnityAction action)
    {
        EnsureEventSystem();
        var image = Image(parent, label, new Color(0.22f, 0.29f, 0.31f), true);
        var button = image.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(action);
        Percent(Text(image.transform, "Label", label, 36, TextAnchor.MiddleCenter).rectTransform, 0.04f, 0.08f, 0.96f, 0.92f);
        return button;
    }
    public static void Percent(RectTransform rect, float left, float bottom, float right, float top)
    {
        rect.anchorMin = new Vector2(left, bottom);
        rect.anchorMax = new Vector2(right, top);
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
    public static void Top(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null) return;
        var go = new GameObject("Progression Event System", typeof(EventSystem), typeof(InputSystemUIInputModule));
        go.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
    }
}
