using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>Short-lived projectile swept through 2D physics so fast shots cannot skip enemies.</summary>
public sealed class CombatProjectile : MonoBehaviour
{
    private const int MaxActiveProjectiles = 64;
    // Cover the enlarged impact canvas so clustered enemies share the explosion.
    public const float RocketSplashRadius = RocketImpact.Width * 0.5f;
    private static int activeProjectiles;
    private static readonly ProfilerMarker SweepMarker = new("Lindavista.ProjectileSweep");
    private static readonly SceneCombatPool<CombatProjectile>[] pools =
    {
        new("Projectile", MaxActiveProjectiles), new("Projectile", MaxActiveProjectiles),
        new("Projectile", MaxActiveProjectiles), new("Projectile", MaxActiveProjectiles)
    };

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
    private int targetLimit;
    private HashSet<EnemyHealth> piercedEnemies;
    private SniperShotVisual sniperVisual;
    private Camera flightCamera;
    private SceneCombatPool<CombatProjectile> pool;
    private bool counted;
    private static readonly Plane[] cameraPlanes = new Plane[6];
    private static Camera cachedCamera;
    private static Matrix4x4 cachedViewProjection;

    public int Damage => damage;

    public static bool TrySpawn(Vector2 origin, Vector2 direction, int shotDamage,
        float shotSpeed, float shotRadius, float maxRange, Transform owner, int weaponIndex = 0,
        int maxTargets = 1, bool grenadeLauncher = false, Camera viewCamera = null)
    {
        if (GameFlowController.IsDefeatActive || activeProjectiles >= MaxActiveProjectiles ||
            direction.sqrMagnitude < 0.0001f)
            return false;

        var weapon = owner != null ? owner.GetComponent<PlayerWeaponController>() : null;
        if (weapon != null && !ProgressionService.TryConsumeAmmo(weapon.EquippedWeapon, weapon.IsCesar)) return false;

        int index = Mathf.Clamp(weaponIndex, 0, 2);
        bool piercing = !grenadeLauncher && index == 1 && maxTargets > 1;
        SceneCombatPool<CombatProjectile> pool = grenadeLauncher ? null : pools[piercing ? 3 : index];
        CombatProjectile projectile;
        if (pool != null) projectile = pool.Rent();
        else
        {
            var go = new GameObject("Projectile");
            go.SetActive(false);
            projectile = go.AddComponent<CombatProjectile>();
        }
        GameObject shot = projectile.gameObject;
        shot.transform.position = new Vector3(origin.x, origin.y, 0f);
        shot.transform.localScale = Vector3.one;
        projectile.enabled = true;
        projectile.pool = pool;
        projectile.spent = false;
        projectile.hasGround = false;
        projectile.groundY = 0f;
        projectile.flightCamera = null;
        projectile.piercedEnemies?.Clear();
        projectile.direction = direction.normalized;
        projectile.owner = owner;
        projectile.weaponIndex = index;
        projectile.remainingRange = Mathf.Max(0.1f, maxRange);

        projectile.speed = Mathf.Max(0.1f, shotSpeed);
        projectile.damage = Mathf.Max(1, shotDamage);
        projectile.radius = Mathf.Max(0.01f, shotRadius);
        if (grenadeLauncher)
        {
            // Keep the shared projectile budget and owner/ammo rules, while the
            // grenade controls its own ballistic flight and delayed detonation.
            projectile.enabled = false;
            GrenadeProjectile.Attach(shot, origin, projectile.direction, projectile.damage,
                projectile.speed, projectile.radius, projectile.remainingRange, owner);
            projectile.counted = true;
            activeProjectiles++;
            shot.SetActive(true);
            return true;
        }
        // Player bullets span the current view, regardless of pointer distance or
        // the weapon's old fixed range. Off-screen simulation and grenades retain
        // their own finite range. Keep following the view while the camera moves.
        if (IsInCamera(viewCamera, origin) || (owner != null && IsInCamera(viewCamera, owner.position)))
            projectile.flightCamera = viewCamera;
        projectile.targetLimit = projectile.weaponIndex == 1 ? Mathf.Clamp(maxTargets, 1, 4) : 1;
        if (projectile.targetLimit > 1) projectile.piercedEnemies ??= new HashSet<EnemyHealth>();
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
        if (projectile.piercedEnemies != null)
        {
            projectile.sniperVisual = SniperShotVisual.Create(origin, projectile.direction);
            SniperShotVisual.Muzzle(origin, projectile.direction);
        }
        else
        {
            ShotVisual.Attach(shot, projectile.weaponIndex);
            // Cesar's shotgun shell is twice as large; collision and muzzle stay unchanged.
            if (projectile.weaponIndex == 0 && weapon != null && weapon.IsCesar)
                shot.transform.localScale *= 2f;
            ShotVisual.Muzzle(origin, projectile.direction, projectile.weaponIndex);
        }

        projectile.counted = true;
        activeProjectiles++;
        shot.SetActive(true);
        return true;
    }

    private void FixedUpdate()
    {
        using var sample = SweepMarker.Auto();
        if (spent) return;
        if (GameFlowController.IsDefeatActive)
        {
            Retire();
            return;
        }

        Vector2 tail = transform.position;
        if (flightCamera != null && flightCamera.isActiveAndEnabled)
            remainingRange = DistanceToCameraEdge(tail);
        if (remainingRange <= 0.00001f)
        {
            // A camera shift may put an in-flight round outside the view.
            // Retire it before even an overlapping off-screen collider is swept.
            Retire();
            return;
        }
        float distance = Mathf.Min(speed * Time.fixedDeltaTime, remainingRange);
        if (piercedEnemies != null)
        {
            SweepSniper(tail, distance);
            return;
        }
        // Sweep the whole rocket through its next nose position. Starting at the tail
        // also catches point-blank targets already inside the newly spawned sprite.
        float noseOffset = weaponIndex == 2 ? Mathf.Max(0f, ShotVisual.RocketWidth - radius) : 0f;
        Vector2 origin = tail + direction * noseOffset;
        float castDistance = flightCamera != null ? Mathf.Min(distance + noseOffset, remainingRange) : distance + noseOffset;
        using var consulta = ConsultasFisicasCombate.Obtener();
        consulta.Barrer(tail, radius, direction, castDistance);
        float nearestDistance = float.PositiveInfinity;
        float nearestCastDistance = float.PositiveInfinity;
        Collider2D nearestCollider = null;
        Vector2 contact = Vector2.zero, normal = Vector2.zero;

        foreach (RaycastHit2D hit in consulta.Impactos)
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
                contact = hit.distance > 0f ? hit.point : collider.ClosestPoint(tail);
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
            EnemyHealth impactedEnemy = nearestCollider != null ? nearestCollider.GetComponentInParent<EnemyHealth>() : null;
            int healthBefore = impactedEnemy != null ? impactedEnemy.CurrentHealth : 0;
            ApplyImpactDamage(nearestCollider, contact + normal * 0.025f);
            if (impactedEnemy != null && weaponIndex != 2)
                ShotVisual.TargetImpact(contact, direction, weaponIndex, impactedEnemy.CurrentHealth == healthBefore);
            else
                ShotVisual.Impact(contact + normal * (weaponIndex == 2 ? 0.12f : 0f), direction, weaponIndex);
            Retire();
            return;
        }

        transform.position = tail + direction * distance;
        remainingRange -= distance;
        if (remainingRange <= 0f)
        {
            Retire();
        }
    }

    private static bool IsInCamera(Camera camera, Vector3 position)
    {
        if (camera == null || !camera.isActiveAndEnabled) return false;
        Vector3 point = camera.WorldToViewportPoint(position);
        return point.z > 0f && point.x >= 0f && point.x <= 1f && point.y >= 0f && point.y <= 1f;
    }

    private float DistanceToCameraEdge(Vector2 origin)
    {
        // All rounds share the same camera geometry. Refresh on matrix changes,
        // including camera motion/zoom within a frame, rather than once per round.
        Matrix4x4 viewProjection = flightCamera.projectionMatrix * flightCamera.worldToCameraMatrix;
        if (cachedCamera != flightCamera || cachedViewProjection != viewProjection)
        {
            GeometryUtility.CalculateFrustumPlanes(viewProjection, cameraPlanes);
            cachedViewProjection = viewProjection;
            cachedCamera = flightCamera;
        }
        float distance = float.PositiveInfinity;
        // The first four planes are left/right/bottom/top. The shot travels on
        // the gameplay plane, so near and far clipping are not travel limits.
        for (int i = 0; i < 4; i++)
        {
            float inside = cameraPlanes[i].GetDistanceToPoint(origin);
            if (inside < -0.0001f) return 0f;
            float outward = -Vector3.Dot(cameraPlanes[i].normal, direction);
            if (outward > 0.0001f)
                distance = Mathf.Min(distance, Mathf.Max(0f, inside) / outward);
        }
        return float.IsPositiveInfinity(distance) ? remainingRange : distance;
    }

    private void SweepSniper(Vector2 start, float distance)
    {
        // Resolve every contact along this step, nearest first. A wall wins a tie
        // so overlapping enemy hitboxes cannot carry a shot through solid scenery.
        using var consulta = ConsultasFisicasCombate.Obtener();
        consulta.Barrer(start, radius, direction, distance);
        consulta.OrdenarPenetracion();
        foreach (RaycastHit2D hit in consulta.Impactos)
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.transform == owner ||
                (owner != null && collider.transform.IsChildOf(owner)) ||
                collider.GetComponentInParent<PlayerActions>() != null ||
                collider.GetComponentInParent<CoinPickup>() != null) continue;

            EnemyHealth enemy = collider.GetComponentInParent<EnemyHealth>();
            if (enemy == null && collider.isTrigger) continue;
            if (enemy != null && (enemy.IsDead || piercedEnemies.Contains(enemy))) continue;

            Vector2 contact = hit.distance > 0f ? hit.point : collider.ClosestPoint(start);
            if (enemy != null)
            {
                piercedEnemies.Add(enemy);
                int healthBefore = enemy.CurrentHealth;
                enemy.TakeDamage(damage, direction);
                bool blocked = enemy.CurrentHealth == healthBefore;
                ShotVisual.TargetImpact(contact, direction, 1, blocked);
                if (!blocked) SniperShotVisual.Blood(contact, direction);
                // A blocked round cannot hit someone behind the shield. The shot
                // that opens the shield is still blocked; the next round can pierce.
                if (!blocked && piercedEnemies.Count < targetLimit) continue;
            }
            else ShotVisual.Impact(contact, direction, 1);

            MoveSniper(start + direction * Mathf.Clamp(hit.distance, 0f, distance));
            Retire();
            return;
        }
        MoveSniper(start + direction * distance);
        remainingRange -= distance;
        if (remainingRange <= 0f)
        {
            Retire();
        }
    }

    private void MoveSniper(Vector2 position)
    {
        transform.position = position;
        if (sniperVisual != null) sniperVisual.DrawTo(position);
    }

    private void ApplyImpactDamage(Collider2D hitCollider, Vector2 position)
    {
        EnemyHealth direct = hitCollider != null ? hitCollider.GetComponentInParent<EnemyHealth>() : null;
        if (weaponIndex != 2)
        {
            direct?.TakeDamage(damage, direction);
            return;
        }

        direct?.TakeExplosionDamage(damage);

        ApplySplashDamage(position, damage, direction, owner, direct);
    }

    internal static void ApplySplashDamage(Vector2 position, int damage, Vector2 direction,
        Transform owner, EnemyHealth direct = null)
    {
        using var consulta = ConsultasFisicasCombate.Obtener();
        var damaged = consulta.EnemigosDanados;
        if (direct != null) damaged.Add(direct);
        consulta.Solapar(position, RocketSplashRadius);
        foreach (Collider2D nearby in consulta.Solapamientos)
        {
            EnemyHealth enemy = nearby.GetComponentInParent<EnemyHealth>();
            if (enemy == null || enemy.IsDead || damaged.Contains(enemy)) continue;
            Vector2 target = nearby.ClosestPoint(position);
            if (IsSplashBlocked(position, target, owner)) continue;
            damaged.Add(enemy);
            enemy.TakeExplosionDamage(damage);
        }
    }

    private static bool IsSplashBlocked(Vector2 origin, Vector2 target, Transform owner)
    {
        using var consulta = ConsultasFisicasCombate.Obtener();
        consulta.TrazarLinea(origin, target);
        foreach (RaycastHit2D hit in consulta.Impactos)
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.isTrigger || collider.GetComponentInParent<EnemyHealth>() != null ||
                collider.GetComponentInParent<PlayerActions>() != null || collider.transform == owner ||
                (owner != null && collider.transform.IsChildOf(owner))) continue;
            return true;
        }
        return false;
    }

    private void ReleaseBudget()
    {
        if (!counted) return;
        counted = false;
        activeProjectiles = Mathf.Max(0, activeProjectiles - 1);
        if (sniperVisual != null) sniperVisual.Release();
        sniperVisual = null;
    }

    private void Retire()
    {
        spent = true;
        ReleaseBudget();
        owner = null;
        flightCamera = null;
        piercedEnemies?.Clear();
        if (pool != null) pool.Return(this);
        else Destroy(gameObject);
    }

    internal void Cancel() { if (!spent) Retire(); }

    private void OnDisable()
    {
        // Disabling just this component is also used by ballistic grenades.
        if (!gameObject.activeInHierarchy) ReleaseBudget();
    }

    private void OnDestroy() => ReleaseBudget();

    internal static void Prewarm()
    {
        pools[0].Prewarm(6, shot => ShotVisual.Attach(shot.gameObject, 0));
        pools[1].Prewarm(10, shot => ShotVisual.Attach(shot.gameObject, 1));
        pools[2].Prewarm(2, shot => ShotVisual.Attach(shot.gameObject, 2));
        pools[3].Prewarm(3, shot => shot.piercedEnemies ??= new HashSet<EnemyHealth>());
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetProjectiles()
    {
        activeProjectiles = 0;
        cachedCamera = null;
    }
}
