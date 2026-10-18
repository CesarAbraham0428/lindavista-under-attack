using System;
using UnityEngine;

/// <summary>Barrel sockets measured in the imported enemy firing sprite rectangles.</summary>
public static class EnemyMuzzle
{
    private const string PistolPrefix = "Enemy2_Pistol_Fire_8f_";
    private const string SMGPrefix = "Enemy4_SMG_Fire_8f_";

    // Raw pixels from each rectangle's top-left, excluding flashes and smoke.
    // Enemy2 frames 0 and 1 have widths 368 and 401, so these are not normalized.
    private static readonly Vector2[] pistol =
    {
        new Vector2(38, 119), new Vector2(65, 127),
        new Vector2(32, 128), new Vector2(31, 128),
        new Vector2(35, 130), new Vector2(46, 128),
        new Vector2(44, 130), new Vector2(42, 133)
    };
    private static readonly Vector2[] smg =
    {
        new Vector2(103, 140), new Vector2(113, 136),
        new Vector2(97, 128), new Vector2(73, 130),
        new Vector2(77, 134), new Vector2(101, 126),
        new Vector2(97, 129), new Vector2(88, 138)
    };

    public static bool IsFiringSprite(Sprite sprite, bool smg) =>
        TryFrame(sprite, smg ? SMGPrefix : PistolPrefix, out _);

    /// <summary>
    /// Uses the visible firing frame, or a firing reference to check line of sight
    /// before an attack. The caller must ensure a firing pose before emitting a shot.
    /// </summary>
    public static bool TryWorldPosition(SpriteRenderer renderer, Sprite firingReference,
        bool flipX, out Vector2 position)
    {
        position = Vector2.zero;
        if (renderer == null) return false;

        Sprite firingSprite = renderer.sprite;
        if (!TryPixel(firingSprite, out Vector2 pixel))
        {
            firingSprite = firingReference;
            if (!TryPixel(firingSprite, out pixel)) return false;
        }
        if (firingSprite.pixelsPerUnit <= 0f) return false;

        Vector2 local = (new Vector2(pixel.x, firingSprite.rect.height - pixel.y) -
            firingSprite.pivot) / firingSprite.pixelsPerUnit;
        if (flipX) local.x = -local.x;
        if (renderer.flipY) local.y = -local.y;
        position = renderer.transform.TransformPoint(local);
        return true;
    }

    private static bool TryPixel(Sprite sprite, out Vector2 pixel)
    {
        pixel = Vector2.zero;
        if (TryFrame(sprite, PistolPrefix, out int frame))
            pixel = pistol[frame];
        else if (TryFrame(sprite, SMGPrefix, out frame))
            pixel = smg[frame];
        else
            return false;
        return true;
    }

    private static bool TryFrame(Sprite sprite, string prefix, out int frame)
    {
        frame = -1;
        if (sprite == null) return false;
        string name = sprite.name;
        if (name.Length != prefix.Length + 1 ||
            !name.StartsWith(prefix, StringComparison.Ordinal)) return false;
        int index = name[prefix.Length] - '0';
        if (index < 0 || index >= 8) return false;
        frame = index;
        return true;
    }
}
