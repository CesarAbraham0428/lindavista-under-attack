using UnityEngine;

/// <summary>Short-lived projectile swept through 2D physics so fast shots cannot skip enemies.</summary>
public sealed class CombatProjectile : MonoBehaviour
{
    private const int MaxActiveProjectiles = 64;
    private static int activeProjectiles;

    private Vector2 direction;
    private Transform owner;
    private float speed;
    private float remainingRange;
    private float radius;
    private int damage;
    private bool spent;

    public int Damage => damage;

    public static bool TrySpawn(Vector2 origin, Vector2 direction, int shotDamage,
        float shotSpeed, float shotRadius, float maxRange, Transform owner)
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
        projectile.remainingRange = Mathf.Max(0.1f, maxRange);

        projectile.speed = Mathf.Max(0.1f, shotSpeed);
        projectile.damage = Mathf.Max(1, shotDamage);
        projectile.radius = Mathf.Max(0.01f, shotRadius);
        SpriteRenderer visual = shot.AddComponent<SpriteRenderer>();
        visual.sprite = ProgressionVisuals.Projectile;
        visual.sortingOrder = 30;
        shot.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);

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
        Vector2 origin = transform.position;
        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, radius, direction, distance);
        float nearestDistance = float.PositiveInfinity;
        Collider2D nearestCollider = null;

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.transform == owner ||
                collider.transform.IsChildOf(owner) ||
                collider.GetComponentInParent<PlayerActions>() != null)
                continue;

            EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
            if (collider.GetComponentInParent<CoinPickup>() != null || (collider.isTrigger && enemy == null))
                continue;
            if (enemy != null && enemy.IsDead)
                continue;

            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearestCollider = collider;
            }
        }

        if (nearestCollider != null)
        {
            nearestCollider.GetComponentInParent<EnemyHealth>()?.TakeDamage(damage, direction);
            spent = true;
            Destroy(gameObject);
            return;
        }

        transform.position = origin + direction * distance;
        remainingRange -= distance;
        if (remainingRange <= 0f)
        {
            spent = true;
            Destroy(gameObject);
        }
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
