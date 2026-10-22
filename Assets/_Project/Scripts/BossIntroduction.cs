using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>One complete sentence per click/tap. Never consumes gameplay time or fires a shot.</summary>
public sealed class BossIntroduction : MonoBehaviour
{
    public static readonly string[] Lines =
    {
        "Creyeron que podían detenerme,\npero esta noche Lindavista\nconocerá a su verdadero dueño.",
        "Mis hombres abrirán el camino;\nveremos cuánto tiempo pueden\ndefender esa entrada.",
        "Si consiguen detenerlos, iré\npersonalmente a terminar\nlo que ellos empezaron.",
        "Cuando escuchen el motor de mi camión,\nsabrán que se acabaron\nlas advertencias."
    };
    private static BossIntroduction active;
    public static bool IsActive => active != null && active.presenting;
    public int LineIndex { get; private set; }
    public bool IsComplete { get; private set; }
    private readonly List<Canvas> hiddenCanvases = new();
    private Canvas canvas;
    private Text dialogue, counter;
    private bool presenting;
    private float nextAdvance;
    private bool applicationPaused;
    private bool applicationFocused = true;
    private bool CanAdvance => presenting && !IsComplete && !applicationPaused && applicationFocused &&
        !GameFlowController.IsPaused && !GameFlowController.IsMatchEnded && !PistolUpgradeShop.IsOpen;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => active = null;

    public IEnumerator Play(Sprite portrait)
    {
        // A touch pause/shop may have opened on the camera's first yielded frame.
        // Keep its resume controls visible until it closes, before hiding any HUD.
        while (GameFlowController.IsPaused || PistolUpgradeShop.IsOpen) yield return null;
        active = this;
        presenting = true;
        IsComplete = false;
        LineIndex = 0;
        MobileControlsHUD.CancelActiveTouches();
        foreach (Canvas other in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (other.enabled && other.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                hiddenCanvases.Add(other);
                other.enabled = false;
            }
        Build(portrait);
        nextAdvance = Time.unscaledTime + 0.25f;
        while (!IsComplete && !GameFlowController.IsMatchEnded) yield return null;
        // The last tap belongs to the dialogue, including a held pointer and touch release.
        while (!GameFlowController.IsMatchEnded &&
            (PointerHeld() || applicationPaused || !applicationFocused || GameFlowController.IsPaused)) yield return null;
        yield return null;
        Restore();
    }

    private void Update()
    {
        if (!CanAdvance) return;
        bool pressed = !Application.isMobilePlatform && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
        if (Touchscreen.current != null)
            foreach (var touch in Touchscreen.current.touches)
                pressed |= touch.press.wasPressedThisFrame;
        pressed |= Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame ||
            Keyboard.current.enterKey.wasPressedThisFrame);
        if (pressed) Advance();
    }

    public void Advance()
    {
        if (!CanAdvance || Time.unscaledTime < nextAdvance) return;
        nextAdvance = Time.unscaledTime + 0.18f;
        if (LineIndex + 1 >= Lines.Length) { IsComplete = true; return; }
        LineIndex++;
        RefreshText();
    }

    private static bool PointerHeld()
    {
        if (!Application.isMobilePlatform && Mouse.current != null && Mouse.current.leftButton.isPressed) return true;
        if (Touchscreen.current != null)
            foreach (var touch in Touchscreen.current.touches)
                if (touch.press.isPressed) return true;
        return false;
    }

    private void OnApplicationPause(bool paused)
    {
        applicationPaused = paused;
        if (paused) MobileControlsHUD.CancelActiveTouches();
    }

    private void OnApplicationFocus(bool focused)
    {
        applicationFocused = focused;
        if (!focused) MobileControlsHUD.CancelActiveTouches();
    }

    private void Build(Sprite portrait)
    {
        canvas = ProgressionUI.Canvas(transform, "Boss Introduction Canvas", 350);
        var dim = ProgressionUI.Image(canvas.transform, "Cinematic Dimmer", new Color(0, 0, 0, .50f), true);
        ProgressionUI.Percent(dim.rectTransform, 0, 0, 1, 1);
        var top = ProgressionUI.Image(canvas.transform, "Top Letterbox", Color.black, true);
        ProgressionUI.Percent(top.rectTransform, 0, .935f, 1, 1);
        var bottom = ProgressionUI.Image(canvas.transform, "Bottom Letterbox", Color.black, true);
        ProgressionUI.Percent(bottom.rectTransform, 0, 0, 1, .09f);
        var safe = AreaSeguraUI.Crear(canvas.transform, "Cinematic Safe Area");
        Label(safe, "Chapter", "LINDAVISTA · NIVEL 4", 33, .025f, .94f, .65f, .995f);
        Label(safe, "Hour", "03:00 AM", 30, .80f, .94f, .97f, .995f, TextAnchor.MiddleRight).color = ProgressionUI.Gold;
        var crop = new GameObject("Boss Portrait Crop", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
        crop.SetParent(safe, false);
        ProgressionUI.Percent(crop, .49f, .09f, 1, .935f);
        var image = ProgressionUI.Image(crop, "Boss_Base Portrait", Color.white);
        image.sprite = portrait;
        image.preserveAspect = true;
        // Enlarge the original PNG, cropping the legs, never stretch or redraw the face.
        ProgressionUI.Percent(image.rectTransform, -.03f, -.78f, 1.05f, 1.01f);
        var panel = ProgressionUI.Image(safe, "Dialogue Frame", new Color(.56f, .36f, .12f));
        CartoonUI.Frame(panel, new Color(.56f, .36f, .12f));
        ProgressionUI.Percent(panel.rectTransform, .022f, .13f, .58f, .425f);
        var ink = ProgressionUI.Image(panel.transform, "Dialogue Ink", new Color(.045f, .05f, .055f, .99f));
        ProgressionUI.Percent(ink.rectTransform, .009f, .025f, .991f, .975f);
        dialogue = Label(ink.transform, "Dialogue", Lines[0], 50, .055f, .24f, .95f, .93f);
        dialogue.lineSpacing = 1.1f;
        counter = Label(ink.transform, "Sentence Counter", "1 / 4", 27, .035f, .045f, .23f, .20f);
        counter.color = ProgressionUI.Gold;
        var sign = ProgressionUI.Image(safe, "Boss Name Sign", Color.white);
        sign.sprite = CartoonUI.Wood;
        ProgressionUI.Percent(sign.rectTransform, .03f, .41f, .245f, .50f);
        Label(sign.transform, "Boss Name", "EL JEFE", 47, .08f, .12f, .92f, .90f, TextAnchor.MiddleCenter);
        Label(safe, "Continue Hint", "CLIC O TOCA PARA CONTINUAR   ›", 30,
            .10f, .012f, .90f, .075f, TextAnchor.MiddleCenter);
    }

    private static Text Label(Transform parent, string name, string text, int size,
        float l, float b, float r, float t, TextAnchor alignment = TextAnchor.MiddleLeft)
    {
        var label = ProgressionUI.Text(parent, name, text, size, alignment);
        CartoonUI.Typography(label);
        label.resizeTextMinSize = 22;
        ProgressionUI.Percent(label.rectTransform, l, b, r, t);
        return label;
    }

    private void RefreshText()
    {
        dialogue.text = Lines[LineIndex];
        counter.text = $"{LineIndex + 1} / {Lines.Length}";
    }

    private void Restore()
    {
        presenting = false;
        if (canvas != null) { canvas.enabled = false; Destroy(canvas.gameObject); }
        foreach (Canvas other in hiddenCanvases) if (other != null) other.enabled = true;
        hiddenCanvases.Clear();
        MobileControlsHUD.CancelActiveTouches();
        if (active == this) active = null;
    }
    private void OnDisable() => Restore();
}
