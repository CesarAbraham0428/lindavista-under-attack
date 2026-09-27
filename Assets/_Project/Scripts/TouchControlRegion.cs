using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum TouchControlKind
{
    MoveLeft,
    MoveRight,
    AimAndFire,
    Pistol,
    SMG,
    RPG,
    Reload
}

public sealed class TouchControlRegion : MonoBehaviour,
    IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    private const int NoPointer = int.MinValue;

    private MobileControlsHUD hud;
    private TouchControlKind kind;
    private RectTransform area;
    private RectTransform aimKnob;
    private Image image;
    private Color restingColor;
    private int pointerId = NoPointer;

    public void Initialize(MobileControlsHUD owner, TouchControlKind controlKind,
        Image background, RectTransform knob = null)
    {
        hud = owner;
        kind = controlKind;
        area = (RectTransform)transform;
        image = background;
        aimKnob = knob;
        restingColor = image.color;
    }

    public void SetRestingColor(Color color)
    {
        restingColor = color;
        if (pointerId == NoPointer && image != null)
            image.color = color;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (pointerId != NoPointer || hud == null)
            return;

        pointerId = eventData.pointerId;
        image.color = Color.Lerp(restingColor, Color.white, 0.25f);

        if (kind == TouchControlKind.AimAndFire)
            UpdateAim(eventData);
        else
            hud.Press(kind, true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (kind == TouchControlKind.AimAndFire && eventData.pointerId == pointerId)
            UpdateAim(eventData);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (eventData.pointerId == pointerId)
            Cancel();
    }

    public void Cancel()
    {
        if (pointerId == NoPointer)
            return;

        pointerId = NoPointer;
        if (image != null)
            image.color = restingColor;
        if (aimKnob != null)
            aimKnob.anchoredPosition = Vector2.zero;

        if (hud == null)
            return;

        if (kind == TouchControlKind.AimAndFire)
            hud.AimAndFire(Vector2.zero, false);
        else
            hud.Press(kind, false);
    }

    private void OnDisable()
    {
        Cancel();
    }

    private void UpdateAim(PointerEventData eventData)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                area, eventData.position, eventData.pressEventCamera, out Vector2 local))
            return;

        float radius = Mathf.Min(area.rect.width, area.rect.height) * 0.5f;
        Vector2 direction = radius > 0f
            ? Vector2.ClampMagnitude(local / radius, 1f)
            : Vector2.right;

        if (aimKnob != null)
            aimKnob.anchoredPosition = direction * radius * 0.65f;

        hud.AimAndFire(direction, true);
    }
}
