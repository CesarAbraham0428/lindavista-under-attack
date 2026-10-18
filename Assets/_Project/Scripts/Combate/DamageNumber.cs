using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Scene-owned, pooled feedback for health actually lost by an enemy.</summary>
[DefaultExecutionOrder(1000)]
public sealed class DamageNumber : MonoBehaviour
{
    private const int Capacity = 96;
    private const float Lifetime = .72f;
    private const int FontSize = 26;
    private static readonly SceneCombatPool<DamageNumber> Pool = new("Damage number", Capacity);
    private static readonly List<DamageNumber> Active = new(Capacity);
    private static readonly string[] SmallAmounts = CreateAmountLabels();
    private static readonly Color Tint = new(1f, .96f, .82f);
    private static Canvas canvas;
    private static RectTransform canvasRect;
    private static Camera view;

    private Text label;
    private EnemyHealth target;
    private Vector3 worldAnchor, targetOffset;
    private float age, horizontalOffset;
    private int lane;
    private bool inUse;

    /// <summary>Call once after health is reduced, before death callbacks can remove the victim.</summary>
    public static DamageNumber Show(EnemyHealth enemy, int appliedDamage)
    {
        if (!Application.isPlaying || enemy == null || appliedDamage <= 0) return null;
        EnsureCanvas();
        if (view == null || !view.isActiveAndEnabled) view = Camera.main;

        // Keep the latest hit visible even if combat fills the entire visual budget.
        if (Active.Count >= Capacity) Active[0].Release();
        int nextLane = 0;
        for (int i = 0; i < Active.Count; i++)
            if (Active[i].target == enemy) nextLane = (Active[i].lane + 1) % 5;

        DamageNumber number = Pool.Rent();
        number.PrepareVisual();
        number.transform.SetParent(canvasRect, false);
        number.transform.localRotation = Quaternion.identity;
        number.transform.localScale = Vector3.one;
        number.target = enemy;
        number.worldAnchor = enemy.DamageNumberAnchor;
        number.targetOffset = number.worldAnchor - enemy.transform.position;
        number.age = 0f;
        number.lane = nextLane;
        number.horizontalOffset = nextLane == 0 ? 0f : ((nextLane + 1) / 2) * (nextLane % 2 == 1 ? -24f : 24f);
        number.label.text = appliedDamage < SmallAmounts.Length ? SmallAmounts[appliedDamage] :
            appliedDamage.ToString(CultureInfo.InvariantCulture);
        number.label.color = Tint;
        number.inUse = true;
        Active.Add(number);
        number.gameObject.SetActive(true);
        number.Draw();
        return number;
    }

    private static void EnsureCanvas()
    {
        if (canvas != null && canvas.gameObject.scene == SceneManager.GetActiveScene()) return;
        var go = new GameObject("Damage numbers canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50; // Above combat, below the HUD, pause and result screens.
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        canvasRect = (RectTransform)go.transform;
        view = Camera.main;
        // No GraphicRaycaster: this canvas never intercepts mouse or Android touches.
    }

    private void PrepareVisual()
    {
        if (label != null) return;
        var go = new GameObject("Amount", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
        go.transform.SetParent(transform, false);
        label = go.GetComponent<Text>();
        label.font = CartoonUI.Font != null ? CartoonUI.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = FontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        label.supportRichText = false;
        label.rectTransform.sizeDelta = new Vector2(240f, 48f);
        var outline = go.GetComponent<Outline>();
        outline.effectColor = new Color(.10f, .065f, .045f, .9f);
        outline.effectDistance = new Vector2(1.2f, -1.2f);
    }

    private void LateUpdate()
    {
        if (!inUse) return;
        if (!GameFlowController.IsPaused) age += Time.deltaTime;
        if (age >= Lifetime) { Release(); return; }
        // Freeze the last head position when the victim dies or is destroyed.
        if (target != null && target.isActiveAndEnabled && !target.IsDead)
            worldAnchor = target.transform.position + targetOffset;
        Draw();
    }

    private void Draw()
    {
        if (view == null || !view.isActiveAndEnabled || canvasRect == null)
        {
            label.enabled = false;
            return;
        }
        Vector3 screen = view.WorldToScreenPoint(worldAnchor);
        Rect viewport = view.pixelRect;
        bool visible = screen.z > 0f && viewport.Contains(new Vector2(screen.x, screen.y));
        label.enabled = visible;
        if (!visible) return;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 point)) return;
        float t = Mathf.Clamp01(age / Lifetime);
        transform.localPosition = new Vector3(point.x + horizontalOffset, point.y + 16f + 40f * t, 0f);
        transform.localScale = Vector3.one * (1f + .06f * (1f - t));
        // CanvasRenderer alpha multiplies the text and its outline exactly like the colour alpha
        // did, but only re-batches the canvas instead of regenerating the outlined text mesh.
        label.canvasRenderer.SetAlpha(1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.35f, 1f, t)));
    }

    private void Release()
    {
        if (!inUse) return;
        inUse = false;
        Active.Remove(this);
        target = null;
        Pool.Return(this);
    }

    private void OnDisable()
    {
        // External destruction or scene unload must also release the active budget.
        if (!inUse) return;
        inUse = false;
        target = null;
        Active.Remove(this);
    }

    internal static void Prewarm()
    {
        EnsureCanvas();
        // Fill the dynamic font atlas during the introduction instead of the first hit.
        CartoonUI.Font?.RequestCharactersInTexture("0123456789", FontSize, FontStyle.Normal);
        Pool.Prewarm(24, number => number.PrepareVisual());
    }

    private static string[] CreateAmountLabels()
    {
        var amounts = new string[101];
        for (int i = 0; i < amounts.Length; i++) amounts[i] = i.ToString(CultureInfo.InvariantCulture);
        return amounts;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        Active.Clear();
        canvas = null;
        canvasRect = null;
        view = null;
    }
}
