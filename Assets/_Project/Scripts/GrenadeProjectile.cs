using UnityEngine;

/// <summary>Ballistic grenade: one small bounce, then a fuse timed from its first floor contact.</summary>
public sealed class GrenadeProjectile : MonoBehaviour
{
    public const float FuseDuration = 0.5f;
    public const float Gravity = 18f;
    public const float BodyLength = 0.72f;
    private static Sprite shadowSprite;
    private Vector2 velocity;
    private Vector2 shotDirection;
    private Transform owner;
    private SpriteRenderer visual;
    private SpriteRenderer shadow;
    private float radius;
    private float groundY;
    private float remainingRange;
    private float fuseRemaining = FuseDuration;
    private int damage;
    private bool landed, settled, spent;

    public bool HasLanded => landed;
    public bool IsSettled => settled;
    public float FuseRemaining => fuseRemaining;

    internal static GrenadeProjectile Attach(GameObject shot, Vector2 origin, Vector2 direction,
        int shotDamage, float speed, float shotRadius, float range, Transform shotOwner)
    {
        shot.name = "Grenade projectile";
        shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        var grenade = shot.AddComponent<GrenadeProjectile>();
        grenade.owner = shotOwner;
        grenade.shotDirection = direction;
        grenade.radius = shotRadius;
        grenade.damage = shotDamage;
        grenade.remainingRange = range;
        // The street is painted scenery. Use the same fixed-canvas foot line as
        // rockets, so a grenade still lands when there is no physical floor.
        grenade.groundY = origin.y - 1.5f;
        var body = shotOwner != null ? shotOwner.GetComponent<SpriteRenderer>() : null;
        if (body != null && body.sprite != null)
        {
            Sprite sprite = body.sprite;
            float feetY = (sprite.rect.height * 0.03f - sprite.pivot.y) / sprite.pixelsPerUnit;
            grenade.groundY = body.transform.TransformPoint(new Vector3(0f, feetY, 0f)).y;
        }
        grenade.groundY = Mathf.Min(grenade.groundY, origin.y - shotRadius);
        // A modest upward launch remains visible for horizontal aim; aiming up
        // or down adjusts the lob without turning it back into a straight rocket.
        grenade.velocity = new Vector2(direction.x * speed * 0.8f,
            5f + direction.y * speed * 0.6f);

        var artwork = CharacterWeapons.Artwork(true);
        Sprite ammo = artwork != null ? artwork.AmmoAt(2) : null;
        if (ammo == null) ammo = ProgressionVisuals.Projectile;
        var art = new GameObject("Grenade round");
        art.transform.SetParent(shot.transform, false);
        grenade.visual = art.AddComponent<SpriteRenderer>();
        grenade.visual.sprite = ammo;
        grenade.visual.sortingOrder = 30;
        float scale = BodyLength / Mathf.Max(ammo.bounds.size.x, ammo.bounds.size.y);
        art.transform.localScale = Vector3.one * scale;
        art.transform.localPosition = -ammo.bounds.center * scale;
        art.transform.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

        var shadowObject = new GameObject("Grenade ground shadow");
        shadowObject.transform.SetParent(shot.transform, false);
        grenade.shadow = shadowObject.AddComponent<SpriteRenderer>();
        grenade.shadow.sprite = GetShadow();
        grenade.shadow.sortingOrder = 29;
        grenade.Draw();
        ShotVisual.Muzzle(origin, direction, 2);
        return grenade;
    }

    private void FixedUpdate()
    {
        if (GameFlowController.IsMatchEnded) { Destroy(gameObject); return; }
        if (spent || GameFlowController.IsPaused || Time.timeScale <= 0f) return;
        float step = Time.fixedDeltaTime;
        if (landed)
        {
            fuseRemaining = Mathf.Max(0f, fuseRemaining - step);
            if (fuseRemaining <= 0.0001f) { Explode(); return; }
        }
        if (!settled) Move(step);
        Draw();
    }

    private void Move(float step)
    {
        Vector2 start = transform.position;
        Vector2 displacement = velocity * step + Vector2.down * (Gravity * step * step * 0.5f);
        velocity.y -= Gravity * step;
        float horizontalTravel = Mathf.Min(Mathf.Abs(displacement.x), remainingRange);
        displacement.x = Mathf.Sign(displacement.x) * horizontalTravel;
        remainingRange -= horizontalTravel;
        if (remainingRange <= 0f) velocity.x = 0f;

        float distance = displacement.magnitude;
        float fraction = 1f;
        Vector2 normal = Vector2.zero;
        bool hit = false;
        if (distance > 0.0001f)
        {
            using var consulta = ConsultasFisicasCombate.Obtener();
            consulta.Barrer(start, radius, displacement / distance, distance);
            foreach (RaycastHit2D contact in consulta.Impactos)
            {
                Collider2D collider = contact.collider;
                // Grenades do not deal contact damage or detonate against an enemy.
                if (collider == null || collider.isTrigger || collider.transform == owner ||
                    (owner != null && collider.transform.IsChildOf(owner)) ||
                    collider.GetComponentInParent<EnemyHealth>() != null ||
                    collider.GetComponentInParent<PlayerActions>() != null ||
                    collider.GetComponentInParent<CoinPickup>() != null) continue;
                float candidate = Mathf.Clamp01(contact.distance / distance);
                if (hit && candidate >= fraction) continue;
                hit = true;
                fraction = candidate;
                normal = contact.normal;
            }
        }

        if (displacement.y < 0f && start.y + displacement.y <= groundY + radius)
        {
            float floorFraction = Mathf.Clamp01((groundY + radius - start.y) / displacement.y);
            if (!hit || floorFraction < fraction)
            {
                hit = true;
                fraction = floorFraction;
                normal = Vector2.up;
            }
        }
        Vector2 position = start + displacement * fraction;
        if (hit)
        {
            position += normal * 0.005f;
            if (normal.y > 0.5f && velocity.y <= 0f)
            {
                groundY = position.y - radius;
                if (!landed)
                {
                    landed = true;
                    fuseRemaining = FuseDuration;
                    velocity = new Vector2(velocity.x * 0.28f,
                        Mathf.Clamp(-velocity.y * 0.25f, 1f, 2.6f));
                }
                else
                {
                    settled = true;
                    velocity = Vector2.zero;
                    visual.transform.rotation = Quaternion.Euler(0f, 0f, -90f);
                }
            }
            else velocity = Vector2.Reflect(velocity, normal) * 0.35f;
        }
        transform.position = position;
        if (!settled) visual.transform.Rotate(0f, 0f, -Mathf.Sign(shotDirection.x) * 280f * step);
    }

    private void Draw()
    {
        shadow.transform.position = new Vector3(transform.position.x, groundY + 0.015f, transform.position.z);
        shadow.transform.rotation = Quaternion.identity;
        float height = Mathf.Max(0f, transform.position.y - groundY - radius);
        float size = Mathf.Lerp(0.65f, 0.35f, Mathf.Clamp01(height / 4f));
        shadow.transform.localScale = new Vector3(size, size * 0.25f, 1f);
        shadow.color = new Color(0f, 0f, 0f, Mathf.Lerp(0.32f, 0.1f, Mathf.Clamp01(height / 4f)));
        // A subtle pulse helps distinguish the live round while it waits on the floor.
        visual.color = landed && fuseRemaining < 0.55f && Mathf.Sin(fuseRemaining * 45f) > 0f
            ? new Color(1f, 0.7f, 0.55f) : Color.white;
    }

    private void Explode()
    {
        if (spent) return;
        spent = true;
        Vector2 position = transform.position;
        RocketImpact.Create(position);
        CombatProjectile.ApplySplashDamage(position, damage, shotDirection, owner);
        Destroy(gameObject);
    }

    private static Sprite GetShadow()
    {
        if (shadowSprite != null) return shadowSprite;
        var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        var pixels = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), new Vector2(15.5f, 15.5f)) / 15.5f;
                pixels[y * 32 + x] = new Color(1f, 1f, 1f, Mathf.Clamp01((1f - distance) * 5f));
            }
        texture.SetPixels32(pixels);
        texture.Apply();
        shadowSprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), Vector2.one * 0.5f, 32f);
        shadowSprite.name = "Grenade ground shadow";
        return shadowSprite;
    }
}
