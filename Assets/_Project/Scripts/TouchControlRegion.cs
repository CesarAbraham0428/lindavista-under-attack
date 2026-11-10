using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TouchControlKind
{
    MoveStick,
    AimAndFire,
    Pistol,
    SMG,
    RPG,
    Reload
}

/// <summary>
/// Botón táctil o joystick flotante. En los joysticks el punto donde cae el dedo es el
/// centro: lo que cuenta es hacia dónde se arrastra, no en qué parte de la zona se toca.
/// </summary>
public sealed class TouchControlRegion : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler, IPointerExitHandler, ICancelHandler
{
    private const int NoPointer = int.MinValue;

    /// <summary>Radio del joystick en unidades del canvas.</summary>
    public const float StickRadius = 95f;
    /// <summary>Fracción del recorrido por debajo de la cual no se mueve ni se dispara.</summary>
    public const float DeadZone = 0.3f;

    private const float RestAlpha = 0.30f;
    private const float ActiveAlpha = 0.60f;

    private MobileControlsHUD hud;
    private TouchControlKind kind;
    private RectTransform area;
    private RectTransform stickBase;
    private RectTransform stickKnob;
    private Image baseImage;
    private Image knobImage;
    private Image image;
    private Color restingColor;
    private int pointerId = NoPointer;
    private Vector2 origin;

    private bool IsStick => kind == TouchControlKind.MoveStick || kind == TouchControlKind.AimAndFire;

    public void Initialize(MobileControlsHUD owner, TouchControlKind controlKind,
        Image background, RectTransform baseRect = null, RectTransform knobRect = null)
    {
        hud = owner;
        kind = controlKind;
        area = (RectTransform)transform;
        image = background;
        stickBase = baseRect;
        stickKnob = knobRect;
        baseImage = baseRect != null ? baseRect.GetComponent<Image>() : null;
        knobImage = knobRect != null ? knobRect.GetComponent<Image>() : null;
        restingColor = image.color;
        ResetStickVisuals();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            pointerId != NoPointer || hud == null || !hud.PuedeRecibirEntrada)
            return;

        if (!IsStick)
        {
            pointerId = eventData.pointerId;
            image.color = Color.Lerp(restingColor, Color.white, 0.25f);
            hud.Press(kind, true);
            return;
        }

        if (!ToLocal(eventData, out Vector2 local))
            return;

        pointerId = eventData.pointerId;
        origin = ClampOrigin(local);
        if (stickBase != null) stickBase.anchoredPosition = origin;
        if (stickKnob != null) stickKnob.anchoredPosition = origin;
        SetStickAlpha(ActiveAlpha);
        ReportStick(Vector2.zero);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (eventData.pointerId != pointerId) return;
        if (hud == null || !hud.PuedeRecibirEntrada)
        {
            Cancel();
            return;
        }
        if (!IsStick || !ToLocal(eventData, out Vector2 local))
            return;

        Vector2 stick = Vector2.ClampMagnitude((local - origin) / StickRadius, 1f);
        if (stickKnob != null)
            stickKnob.anchoredPosition = origin + stick * StickRadius;
        ReportStick(stick);
    }

    public void OnCancel(BaseEventData eventData) => Cancel();

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
            Cancel();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // Un joystick sigue activo aunque el dedo salga de su zona; un botón se suelta.
        if (!IsStick && eventData.pointerId == pointerId)
            Cancel();
    }

    public void Cancel()
    {
        if (pointerId == NoPointer)
            return;

        pointerId = NoPointer;
        if (image != null && !IsStick)
            image.color = restingColor;
        ResetStickVisuals();

        if (hud == null)
            return;

        switch (kind)
        {
            case TouchControlKind.MoveStick:
                hud.SetMove(0f);
                break;
            case TouchControlKind.AimAndFire:
                hud.AimAndFire(Vector2.zero, false);
                break;
            default:
                hud.Press(kind, false);
                break;
        }
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void ReportStick(Vector2 stick)
    {
        bool beyondDeadZone = stick.magnitude >= DeadZone;
        if (kind == TouchControlKind.MoveStick)
            hud.SetMove(Mathf.Abs(stick.x) >= DeadZone ? Mathf.Sign(stick.x) : 0f);
        else
            // Tocar ya dispara hacia delante; arrastrar más allá de la zona muerta apunta.
            hud.AimAndFire(beyondDeadZone ? stick.normalized : Vector2.zero, true);
    }

    private bool ToLocal(PointerEventData eventData, out Vector2 local) =>
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            area, eventData.position, eventData.pressEventCamera, out local);

    // Mantiene todo el joystick dentro de la zona aunque se toque en su borde.
    private Vector2 ClampOrigin(Vector2 local)
    {
        Rect rect = area.rect;
        float limitX = Mathf.Max(0f, rect.width * 0.5f - StickRadius);
        float limitY = Mathf.Max(0f, rect.height * 0.5f - StickRadius);
        return new Vector2(Mathf.Clamp(local.x, -limitX, limitX),
            Mathf.Clamp(local.y, -limitY, limitY));
    }

    private void ResetStickVisuals()
    {
        if (stickBase != null) stickBase.anchoredPosition = Vector2.zero;
        if (stickKnob != null) stickKnob.anchoredPosition = Vector2.zero;
        SetStickAlpha(RestAlpha);
    }

    private void SetStickAlpha(float alpha)
    {
        if (baseImage != null)
        {
            Color c = baseImage.color;
            c.a = alpha * 0.8f;
            baseImage.color = c;
        }
        if (knobImage != null)
        {
            Color c = knobImage.color;
            c.a = alpha + 0.1f;
            knobImage.color = c;
        }
    }
}
