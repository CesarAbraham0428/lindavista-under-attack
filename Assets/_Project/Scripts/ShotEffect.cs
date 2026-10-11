using UnityEngine;

/// <summary>Bounded, self-cleaning particles using the sprite material in both URP and built-in renderers.</summary>
public sealed class ShotEffect : MonoBehaviour
{
    private static int active;
    private SpriteRenderer visual;
    private Vector2 velocity;
    private Color tint;
    private float age, duration, startSize, endSize;

    public static ShotEffect Create(string label, Sprite sprite, Vector2 position, Vector2 velocity,
        Color color, float size, float endSize, float lifetime, int sortingOrder = 31)
    {
        if (active >= 180 || GameFlowController.IsMatchEnded) return null;
        var go = new GameObject(label);
        go.transform.position = position;
        var effect = go.AddComponent<ShotEffect>();
        active++;
        effect.visual = go.AddComponent<SpriteRenderer>();
        effect.visual.sprite = sprite;
        effect.visual.sortingOrder = sortingOrder;
        effect.visual.color = effect.tint = color;
        effect.velocity = velocity;
        effect.duration = lifetime;
        effect.startSize = size / sprite.bounds.size.x;
        effect.endSize = endSize / sprite.bounds.size.x;
        go.transform.localScale = Vector3.one * effect.startSize;
        return effect;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration || GameFlowController.IsMatchEnded) { Destroy(gameObject); return; }
        float t = age / duration;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, t);
        visual.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - t));
    }

    private void OnDestroy() { active = Mathf.Max(0, active - 1); }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() { active = 0; }
}

