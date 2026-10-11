using UnityEngine;

/// <summary>A one-shot cartoon fire-to-smoke flipbook, independent of damage.</summary>
public sealed class RocketImpact : MonoBehaviour
{
    public const int FrameCount = 9;
    public const float Duration = 0.54f;
    public const float Width = 6.6f;
    private const int MaxActive = 24;
    private static readonly Sprite[] frames = new Sprite[FrameCount];
    private static int active;
    private SpriteRenderer visual;
    private float age;

    public static RocketImpact Create(Vector2 position)
    {
        if (active >= MaxActive || GameFlowController.IsMatchEnded) return null;
        Sprite first = GetFrame(0);
        if (first == null) return null;
        var go = new GameObject("RPG impact");
        go.transform.position = position;
        go.transform.localScale = Vector3.one * Width / first.bounds.size.x;
        var effect = go.AddComponent<RocketImpact>();
        active++;
        effect.visual = go.AddComponent<SpriteRenderer>();
        effect.visual.sprite = first;
        effect.visual.sortingOrder = 32;
        return effect;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= Duration || GameFlowController.IsMatchEnded)
        {
            Destroy(gameObject);
            return;
        }
        visual.sprite = GetFrame(Mathf.Min(FrameCount - 1, (int)(age / Duration * FrameCount)));
        // Fade only the final smoke frames; keep the initial flash crisp and readable.
        float fade = Mathf.InverseLerp(Duration * 0.55f, Duration, age);
        visual.color = new Color(1f, 1f, 1f, 1f - fade);
    }

    public static Sprite GetFrame(int index)
    {
        index = Mathf.Clamp(index, 0, FrameCount - 1);
        if (frames[index] != null) return frames[index];
        var texture = Resources.Load<Texture2D>("RpgImpactAtlas");
        if (texture == null) return null;
        int column = index % 3, row = 2 - index / 3;
        int left = Mathf.RoundToInt(column * texture.width / 3f);
        int bottom = Mathf.RoundToInt(row * texture.height / 3f);
        int right = Mathf.RoundToInt((column + 1) * texture.width / 3f);
        int top = Mathf.RoundToInt((row + 1) * texture.height / 3f);
        // Equal canvas and a fixed pivot retain the drawn expansion instead of resizing every frame.
        frames[index] = Sprite.Create(texture, new Rect(left, bottom, right - left, top - bottom),
            Vector2.one * 0.5f, 256f, 0, SpriteMeshType.FullRect);
        frames[index].name = "RPG impact " + index;
        return frames[index];
    }

    private void OnDestroy() { active = Mathf.Max(0, active - 1); }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount() { active = 0; }
}
