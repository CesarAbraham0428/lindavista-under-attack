using UnityEngine;

/// <summary>Barrel sockets measured on the 384 x 512 player animation cells.</summary>
public static class PlayerMuzzle
{
    // Pixel coordinates from the top-left of each cell. Do not use visible bounds:
    // recoil, muzzle flashes and smoke change those bounds without moving the pivot.
    private static readonly Vector2[][] marco =
    {
        new[] { P(374,119), P(364,118), P(331,110), P(343,111), P(358,123), P(357,123), P(359,123), P(362,122) },
        new[] { P(371,139), P(371,140), P(361,105), P(352,112), P(324,93), P(366,138), P(363,138), P(365,138) },
        new[] { P(354,145), P(338,146), P(222,170), P(208,160), P(315,143), P(346,151), P(347,142), P(338,140) }
    };
    private static readonly Vector2[][] cesar =
    {
        // Shotgun frame 2 is horizontal; its barrel is at (334, 228), not above the head.
        new[] { P(325,361), P(365,118), P(334,228), P(343,110), P(359,123), P(359,123), P(351,123), P(359,122) },
        new[] { P(370,139), P(332,135), P(328,136), P(314,137), P(315,120), P(316,123), P(315,125), P(338,129) },
        new[] { P(373,182), P(336,147), P(241,150), P(187,156), P(302,151), P(337,151), P(340,151), P(335,151) }
    };
    // The newer sniper sheet has its own barrel positions; the legacy SMG
    // sockets above belong to a different drawing and sit above this rifle.
    private static readonly Vector2[] cesarSniper =
    {
        P(348,352), P(367,192), P(367,223), P(322,224),
        P(383,176), P(381,170), P(347,323), P(342,321)
    };

    public static Vector2 WorldPosition(SpriteRenderer renderer, int weaponIndex)
    {
        if (renderer == null || renderer.sprite == null)
            return renderer != null ? (Vector2)renderer.transform.position : Vector2.zero;

        Sprite sprite = renderer.sprite;
        int frame = 0;
        int separator = sprite.name.LastIndexOf('_');
        if (sprite.name.Contains("_Fire_") && separator >= 0)
            int.TryParse(sprite.name.Substring(separator + 1), out frame);
        Vector2 pixel = sprite.name.StartsWith("Cesar_Francotirador_Fire_") ?
            cesarSniper[Mathf.Clamp(frame, 0, 7)] :
            (sprite.name.StartsWith("Cesar_") ? cesar : marco)
                [Mathf.Clamp(weaponIndex, 0, 2)][Mathf.Clamp(frame, 0, 7)];
        Vector2 local = (Vector2.Scale(pixel, sprite.rect.size) - sprite.pivot) / sprite.pixelsPerUnit;
        if (renderer.flipX) local.x = -local.x;
        if (renderer.flipY) local.y = -local.y;
        return renderer.transform.TransformPoint(local);
    }

    private static Vector2 P(float x, float y) => new Vector2(x / 384f, 1f - y / 512f);
}
