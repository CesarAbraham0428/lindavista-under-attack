using System.Collections.Generic;
using UnityEngine;

/// <summary>Short-lived projectile swept through 2D physics so fast shots cannot skip enemies.</summary>
public sealed class CombatProjectile : MonoBehaviour
{
    private const int MaxActiveProjectiles = 64;
    // Cover the enlarged impact canvas so clustered enemies share the explosion.
    public const float RocketSplashRadius = RocketImpact.Width * 0.5f;
    private static int activeProjectiles;

    private Vector2 direction;
    private Transform owner;
    private float speed;
    private float remainingRange;
    private float radius;
    private int damage;
    private bool spent;
    private int weaponIndex;
    private bool hasGround;
    private float groundY;

    public int Damage => damage;

    public static bool TrySpawn(Vector2 origin, Vector2 direction, int shotDamage,
        float shotSpeed, float shotRadius, float maxRange, Transform owner, int weaponIndex = 0)
    {
        if (GameFlowController.IsDefeatActive || activeProjectiles >= MaxActiveProjectiles ||
            direction.sqrMagnitude < 0.0001f)
            return false;

        var weapon = owner != null ? owner.GetComponent<PlayerWeaponController>() : null;
        if (weapon != null && !ProgressionService.TryConsumeAmmo(weapon.EquippedWeapon)) return false;

        GameObject shot = new GameObject("Projectile");
        shot.transform.position = new Vector3(origin.x, origin.y, 0f);
        CombatProjectile projectile = shot.AddComponent<CombatProjectile>();
        projectile.direction = direction.normalized;
        projectile.owner = owner;
        projectile.weaponIndex = Mathf.Clamp(weaponIndex, 0, 2);
        projectile.remainingRange = Mathf.Max(0.1f, maxRange);

        projectile.speed = Mathf.Max(0.1f, shotSpeed);
        projectile.damage = Mathf.Max(1, shotDamage);
        projectile.radius = Mathf.Max(0.01f, shotRadius);
        if (projectile.weaponIndex == 2 && owner != null)
        {
            var body = owner.GetComponent<SpriteRenderer>();
            if (body != null && body.sprite != null)
            {
                // Streets are painted scenery with no floor collider. Snapshot the foot line
                // from the fixed animation canvas, slightly above its transparent bottom margin.
                projectile.hasGround = true;
                Sprite sprite = body.sprite;
                float feetY = (sprite.rect.height * 0.03f - sprite.pivot.y) / sprite.pixelsPerUnit;
                projectile.groundY = body.transform.TransformPoint(new Vector3(0f, feetY, 0f)).y;
            }
        }
        shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        ShotVisual.Attach(shot, projectile.weaponIndex);
        ShotVisual.Muzzle(origin, projectile.direction, projectile.weaponIndex);

        activeProjectiles++;
        return true;
    }

    private void FixedUpdate()
    {
        if (GameFlowController.IsDefeatActive)
        {
            spent = true;
            Destroy(gameObject);
            return;
        }

        if (spent)
            return;

        float distance = Mathf.Min(speed * Time.fixedDeltaTime, remainingRange);
        Vector2 tail = transform.position;
        // Sweep the whole rocket through its next nose position. Starting at the tail
        // also catches point-blank targets already inside the newly spawned sprite.
        float noseOffset = weaponIndex == 2 ? Mathf.Max(0f, ShotVisual.RocketWidth - radius) : 0f;
        Vector2 origin = tail + direction * noseOffset;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(tail, radius, direction, distance + noseOffset);
        float nearestDistance = float.PositiveInfinity;
        float nearestCastDistance = float.PositiveInfinity;
        Collider2D nearestCollider = null;
        Vector2 contact = Vector2.zero, normal = Vector2.zero;

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.transform == owner ||
                (owner != null && collider.transform.IsChildOf(owner)) ||
                collider.GetComponentInParent<PlayerActions>() != null)
                continue;

            EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
            if (collider.GetComponentInParent<CoinPickup>() != null || (collider.isTrigger && enemy == null))
                continue;
            if (enemy != null && enemy.IsDead)
                continue;

            if (hit.distance < nearestCastDistance)
            {
                nearestCastDistance = hit.distance;
                nearestDistance = Mathf.Max(0f, hit.distance - noseOffset);
                nearestCollider = collider;
                contact = hit.point;
                normal = hit.normal;
            }
        }

        bool groundHit = false;
        if (hasGround && direction.y < -0.0001f)
        {
            float groundDistance = Mathf.Max(0f, (origin.y - radius - groundY) / -direction.y);
            if (groundDistance <= distance && groundDistance < nearestDistance)
            {
                nearestDistance = groundDistance;
                nearestCollider = null;
                contact = new Vector2(origin.x + direction.x * groundDistance, groundY);
                normal = Vector2.up;
                groundHit = true;
            }
        }

        if (nearestCollider != null || groundHit)
        {
            spent = true;
            transform.position = tail + direction * nearestDistance;
            ShotVisual.Impact(contact + normal * (weaponIndex == 2 ? 0.12f : 0f), direction, weaponIndex);
            ApplyImpactDamage(nearestCollider, contact + normal * 0.025f);
            Destroy(gameObject);
            return;
        }

        transform.position = tail + direction * distance;
        remainingRange -= distance;
        if (remainingRange <= 0f)
        {
            spent = true;
            Destroy(gameObject);
        }
    }

    private void ApplyImpactDamage(Collider2D hitCollider, Vector2 position)
    {
        EnemyHealth direct = hitCollider != null ? hitCollider.GetComponentInParent<EnemyHealth>() : null;
        direct?.TakeDamage(damage, direction);
        if (weaponIndex != 2) return;

        var damaged = new HashSet<EnemyHealth>();
        if (direct != null) damaged.Add(direct);
        foreach (Collider2D nearby in Physics2D.OverlapCircleAll(position, RocketSplashRadius))
        {
            EnemyHealth enemy = nearby.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || damaged.Contains(enemy)) continue;
            Vector2 target = nearby.ClosestPoint(position);
            if (IsSplashBlocked(position, target)) continue;
            damaged.Add(enemy);
            Vector2 outward = (Vector2)nearby.bounds.center - position;
            enemy.TakeDamage(damage, outward.sqrMagnitude > 0.0001f ? outward.normalized : direction);
        }
    }

    private bool IsSplashBlocked(Vector2 origin, Vector2 target)
    {
        foreach (RaycastHit2D hit in Physics2D.LinecastAll(origin, target))
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<EnemyHealth>() != null ||
                collider.GetComponentInParent<PlayerActions>() != null || collider.transform == owner ||
                (owner != null && collider.transform.IsChildOf(owner))) continue;
            return true;
        }
        return false;
    }

    private void OnDestroy()
    {
        activeProjectiles = Mathf.Max(0, activeProjectiles - 1);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProjectiles()
    {
        activeProjectiles = 0;
    }
}
