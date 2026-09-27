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

    public static void Spawn(Vector2 origin, Vector2 direction, int weaponIndex,
        float maxRange, Transform owner)
    {
        if (GameFlowController.IsDefeatActive || activeProjectiles >= MaxActiveProjectiles ||
            direction.sqrMagnitude < 0.0001f)
            return;

        GameObject shot = new GameObject("Projectile");
        shot.transform.position = new Vector3(origin.x, origin.y, 0f);
        CombatProjectile projectile = shot.AddComponent<CombatProjectile>();
        projectile.direction = direction.normalized;
        projectile.owner = owner;
        projectile.remainingRange = Mathf.Max(0.1f, maxRange);

        switch (weaponIndex)
        {
            case 1: // SMG
                projectile.speed = 28f;
                projectile.damage = 1;
                projectile.radius = 0.055f;
                break;
            case 2: // RPG
                projectile.speed = 12f;
                projectile.damage = 3;
                projectile.radius = 0.14f;
                break;
            default: // Pistol
                projectile.speed = 22f;
                projectile.damage = 1;
                projectile.radius = 0.07f;
                break;
        }

        activeProjectiles++;
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
