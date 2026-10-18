using UnityEngine;

/// <summary>A finite moving streak whose tip follows the projectile and whose tail never precedes the muzzle.</summary>
public sealed class SniperShotVisual : MonoBehaviour
{
    public const float MaxLength = 8f;
    public const float Height = 0.14f;
    public const float FadeDuration = 0.1f;
    private static Sprite tracerSprite, bloodSprite;
    private SpriteRenderer visual;
    private Vector2 origin, direction;
    private bool released;
    private float fadeAge;
    private static readonly SceneCombatPool<SniperShotVisual> pool = new("Sniper tracer", 32);
    private bool retired;

    public static SniperShotVisual Create(Vector2 origin, Vector2 direction)
    {
        var effect = pool.Rent();
        var go = effect.gameObject;
        go.transform.position = origin;
        go.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        effect.origin = origin;
        effect.direction = direction.normalized;
        effect.released = effect.retired = false;
        effect.fadeAge = 0f;
        if (effect.visual == null) effect.visual = go.AddComponent<SpriteRenderer>();
        effect.visual.sprite = GetTracerSprite();
        effect.visual.color = Color.white;
        effect.visual.sortingOrder = 30;
        effect.visual.enabled = false;
        go.SetActive(true);
        return effect;
    }

    public void DrawTo(Vector2 tip)
    {
        transform.position = tip;
        float length = Mathf.Min(MaxLength, Mathf.Max(0f, Vector2.Dot(tip - origin, direction)));
        Sprite sprite = visual.sprite;
        visual.enabled = length > 0.001f;
        transform.localScale = new Vector3(length / sprite.bounds.size.x, Height / sprite.bounds.size.y, 1f);
    }

    public void Release() { released = true; }

    private void Update()
    {
        if (retired) return;
        if (GameFlowController.IsMatchEnded) { Retire(); return; }
        if (!released) return;
        fadeAge += Time.deltaTime;
        if (fadeAge >= FadeDuration) { Retire(); return; }
        // Fade at the last valid position: the streak cannot travel past a wall or its range.
        visual.color = new Color(1f, 1f, 1f, 1f - fadeAge / FadeDuration);
    }

    private void Retire() { retired = true; pool.Return(this); }

    internal static void Prewarm()
    {
        GetTracerSprite();
        GetBloodSprite();
        pool.Prewarm(4, effect =>
        {
            if (effect.visual == null) effect.visual = effect.gameObject.AddComponent<SpriteRenderer>();
        });
    }

    public static void Muzzle(Vector2 position, Vector2 direction)
    {
        var flash = ShotEffect.Create("Sniper muzzle flash", ShotVisual.GetSprite(3), position,
            Vector2.zero, Color.white, 0.52f, 0.18f, 0.075f);
        if (flash != null)
            flash.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    public static void Blood(Vector2 position, Vector2 direction)
    {
        var blood = ShotEffect.Create("Sniper blood impact", GetBloodSprite(), position,
            direction * 0.45f, Color.white, 0.62f, 0.82f, 0.18f, 32);
        if (blood != null)
            blood.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

    public const string TracerName = "SniperTracer";
    public const string BloodName = "SniperBloodImpact";

    public static Sprite GetTracerSprite() => tracerSprite != null ? tracerSprite :
        tracerSprite = LoadBaked(TracerName);

    public static Sprite GetBloodSprite() => bloodSprite != null ? bloodSprite :
        bloodSprite = LoadBaked(BloodName);

    private static Sprite LoadBaked(string resource)
    {
        // Trimmed rectangle and pivot are baked at import (Tools/Lindavista/Bake effect sprite rectangles).
        foreach (var sprite in Resources.LoadAll<Sprite>(resource))
            if (sprite.name == resource) return sprite;
        Debug.LogError("Missing baked sniper effect sprite: " + resource);
        return ProgressionVisuals.Projectile;
    }
}
