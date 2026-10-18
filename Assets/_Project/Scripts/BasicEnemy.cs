using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;

/// <summary>Enemy combat and optional advance toward a scene entrance.</summary>
[RequireComponent(typeof(EnemyHealth), typeof(Animator), typeof(SpriteRenderer))]
public sealed class BasicEnemy : MonoBehaviour
{
    private enum AttackStyle { Melee, Pistol, ShieldMelee, SMG }
    private static readonly int movimiento = Animator.StringToHash("IsMoving");
    private static readonly int attack = Animator.StringToHash("Attack");
    private static readonly int takeDamage = Animator.StringToHash("TakeDamage");
    private static readonly int defeat = Animator.StringToHash("Defeat");
    private static readonly HashSet<BasicEnemy> activeEnemies = new();
    private static readonly ProfilerMarker AIMarker = new("Lindavista.EnemyAI");

    [Header("Basic enemy stats")]
    [SerializeField, Min(0)] private int damage = 1;
    [SerializeField, Min(0f)] private float speed = 1.5f;
    [SerializeField, Range(0.5f, 2f)] private float sizeMultiplier = 1f;
    [Header("Attack")]
    [SerializeField] private AttackStyle attackStyle = AttackStyle.Melee;
    [SerializeField, Min(0.1f)] private float attackRange = 1.6f;
    [SerializeField, Min(0.1f)] private float meleeHitRange = 1.5f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 1.4f;
    [SerializeField, Min(0f)] private float attackWindup = 0.25f;
    [SerializeField, Min(1)] private int shotsPerAttack = 1;
    [SerializeField, Min(0.01f)] private float timeBetweenShots = 0.15f;
    [Header("Ranged muzzle reference (first firing pose)")]
    [SerializeField] private Sprite firingMuzzleReference;
    [Header("Objective (optional)")]
    [SerializeField] private Transform entranceTarget;
    [Header("Visual ground anchor (idle sprite, local units)")]
    [SerializeField] private bool hasStandingFootAnchor;
    [SerializeField] private float standingFootY;

    public float StandingGroundY => hasStandingFootAnchor
        ? transform.TransformPoint(new Vector3(0f, standingFootY, 0f)).y : transform.position.y;

    /// <summary>Aligns visible soles, not canvas centres; does not resize the body/hitbox.</summary>
    public void PlaceOnGround(float worldY)
    {
        if (!hasStandingFootAnchor) return;
        Vector3 position = transform.position;
        position.y += worldY - StandingGroundY;
        transform.position = position;
    }

    private EnemyHealth health;
    private Animator animator;
    private SpriteRenderer sprite;
    private WeaponAudioLibrary weaponAudioLibrary;
    private AudioSource weaponShotSource;
    private int ShotAudioWeapon => attackStyle == AttackStyle.SMG ? 1 : 0;
    private float ShotAudioVolume => weaponAudioLibrary.ShotVolume(ShotAudioWeapon) *
        (attackStyle == AttackStyle.Pistol ? 0.9f : 1f);
    private PlayerActions target;
    private float nextTargetSearch;
    private float nextAttack;
    private bool hasFixedEntranceTarget;
    private bool hasReachedEntrance;
    private float fixedEntranceX;
    private bool coordinatedCombat;
    private bool attackInProgress;
    private Collider2D bodyCollider;
    private bool moving, movementInitialized;
    private const float AllySpacing = 1.25f;
    private bool IsRanged => attackStyle == AttackStyle.Pistol || attackStyle == AttackStyle.SMG;

    /// <summary>Enables readable combat positioning for the first three level waves only.</summary>
    public void ConfigureTactics() => coordinatedCombat = true;
    private float EffectiveAttackRange =>
        attackStyle == AttackStyle.Melee || attackStyle == AttackStyle.ShieldMelee
            ? Mathf.Min(attackRange, meleeHitRange)
            : attackRange;

    /// <summary>Sets the fixed world X coordinate the enemy advances toward.</summary>
    public void SetEntranceTargetX(float worldX)
    {
        fixedEntranceX = worldX;
        hasFixedEntranceTarget = true;
        entranceTarget = null;
    }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
        sprite = GetComponent<SpriteRenderer>();
        // All actors remain in front of scenery (orders <= 5). Tall enemies
        // stand behind players (7) and smaller enemies (8), without changing Z.
        sprite.sortingOrder = attackStyle == AttackStyle.ShieldMelee || attackStyle == AttackStyle.SMG ? 6 : 8;
        bodyCollider = GetComponent<Collider2D>();
        health.Died += DetenerAlMorir;
        if (attackStyle == AttackStyle.ShieldMelee)
        {
            if (GetComponent<EnemyShieldBreakVisual>() == null)
                gameObject.AddComponent<EnemyShieldBreakVisual>();
            health.ShieldBroken += ContinueAfterShieldBreak;
        }
        animator.ResetTrigger(attack);
        animator.ResetTrigger(takeDamage);
        animator.ResetTrigger(defeat);
        nextAttack = Time.time + attackCooldown;
        transform.localScale *= sizeMultiplier;
        InitializeWeaponAudio();
    }

    private void InitializeWeaponAudio()
    {
        if (attackStyle != AttackStyle.Pistol && attackStyle != AttackStyle.SMG) return;
        // Ranged enemies share Marco's weapon clips; Enemy2 remains 10% quieter.
        weaponAudioLibrary = Resources.Load<WeaponAudioLibrary>("WeaponAudioLibrary");
        if (weaponAudioLibrary == null || weaponAudioLibrary.Shot(ShotAudioWeapon) == null)
        {
            Debug.LogWarning("Enemy ranged shot is missing from WeaponAudioLibrary.", this);
            return;
        }
        var audioObject = new GameObject(attackStyle == AttackStyle.SMG ? "Enemy SMG Fire Audio" : "Enemy Pistol Fire Audio");
        audioObject.transform.SetParent(transform, false);
        weaponShotSource = audioObject.AddComponent<AudioSource>();
        weaponShotSource.playOnAwake = false;
        weaponShotSource.loop = false;
        weaponShotSource.spatialBlend = 0f;
        weaponShotSource.dopplerLevel = 0f;
        weaponShotSource.priority = 96;
        weaponShotSource.clip = weaponAudioLibrary.Shot(ShotAudioWeapon);
        weaponShotSource.volume = ShotAudioVolume;
    }

    private void Update()
    {
        using var sample = AIMarker.Auto();
        if (health.IsDead)
        {
            SetMoving(false);
            return;
        }

        if (GameFlowController.IsDefeatActive)
        {
            StopForGameOver();
            return;
        }

        if (GameFlowController.IsPaused || PistolUpgradeShop.IsOpen)
        {
            SetMoving(false);
            return;
        }

        if (Time.time >= nextTargetSearch || (coordinatedCombat && target != null &&
            (!target.isActiveAndEnabled || target.IsDefeated || !IsOnApproach(target))))
        {
            FindTarget();
            nextTargetSearch = Time.time + 0.25f;
        }

        if (coordinatedCombat && attackInProgress && !health.IsShieldBroken)
        {
            SetMoving(false);
            return;
        }

        if (entranceTarget != null || hasFixedEntranceTarget)
        {
            // Hold a useful combat distance instead of walking through the
            // player during the windup. Passing players are never chased back.
            if (coordinatedCombat && !health.IsShieldBroken && target != null && CanHit(target))
            {
                Face(target.transform.position.x - transform.position.x);
                SetMoving(false);
                if (Time.time >= nextAttack)
                {
                    nextAttack = Time.time + attackCooldown;
                    animator.SetTrigger(attack);
                    StartCoroutine(PerformAttack(target));
                }
                return;
            }
            Vector3 beforeAdvance = transform.position;
            AdvanceToEntrance();
            if (GameFlowController.IsDefeatActive)
                return;

            // If a tactical unit could not engage and did not move, the exact
            // same physics/line-of-sight query cannot produce a different answer.
            bool canAttack = (!coordinatedCombat || transform.position != beforeAdvance) && CanHit(target);
            if (canAttack && Time.time >= nextAttack)
            {
                nextAttack = Time.time + attackCooldown;
                animator.SetTrigger(attack);
                StartCoroutine(PerformAttack(target));
            }
            else if (!canAttack)
                animator.ResetTrigger(attack);
            return;
        }

        if (target == null || target.IsDefeated)
        {
            SetMoving(false);
            return;
        }

        Vector2 delta = target.transform.position - transform.position;
        Face(delta.x);
        float reach = EffectiveAttackRange;
        if (Mathf.Abs(delta.x) > reach)
        {
            Vector3 position = transform.position;
            float stopAt = target.transform.position.x - Mathf.Sign(delta.x) * reach;
            position.x = Mathf.MoveTowards(position.x, stopAt, speed * Time.deltaTime);
            if (coordinatedCombat) position = ConstrainMovement(position);
            SetMoving(position.x != transform.position.x);
            transform.position = position;
            return;
        }

        SetMoving(false);
        if (Mathf.Abs(delta.y) <= 1.5f && Time.time >= nextAttack)
        {
            nextAttack = Time.time + attackCooldown;
            animator.SetTrigger(attack);
            StartCoroutine(PerformAttack(target));
        }
    }

    private void AdvanceToEntrance()
    {
        Vector3 position = transform.position;
        float destinationX = entranceTarget != null ? entranceTarget.position.x : fixedEntranceX;
        Face(destinationX - position.x);
        position.x = Mathf.MoveTowards(position.x, destinationX, speed * Time.deltaTime);
        if (coordinatedCombat) position = ConstrainMovement(position);
        SetMoving(position.x != transform.position.x);
        transform.position = position;

        if (!hasReachedEntrance && Mathf.Abs(position.x - destinationX) <= 0.001f)
        {
            hasReachedEntrance = true;
            GameFlowController.ReportEnemyReachedEntrance();
        }
    }

    public void StopForGameOver()
    {
        StopAllCoroutines();
        attackInProgress = false;
        if (animator != null)
            SetMoving(false);
        enabled = false;
    }

    private void DetenerAlMorir(EnemyHealth enemigo) => StopForGameOver();

    private void ContinueAfterShieldBreak()
    {
        // Losing equipment interrupts the old shield attack, never locomotion.
        StopAllCoroutines();
        attackInProgress = false;
        animator.ResetTrigger(attack);
        animator.ResetTrigger(takeDamage);
        movementInitialized = false;
    }

    private void OnEnable()
    {
        activeEnemies.Add(this);
        movementInitialized = false;
    }

    private void OnDisable()
    {
        activeEnemies.Remove(this);
        StopAllCoroutines();
        attackInProgress = false;
        if (weaponShotSource != null) weaponShotSource.Stop();
    }

    private void OnDestroy()
    {
        activeEnemies.Remove(this);
        if (health != null)
        {
            health.Died -= DetenerAlMorir;
            health.ShieldBroken -= ContinueAfterShieldBreak;
        }
    }

    private void FindTarget()
    {
        target = null;
        float nearest = float.PositiveInfinity;
        foreach (PlayerActions player in ObjetivosCombate.Obtener())
        {
            if (player == null || !player.isActiveAndEnabled || player.IsDefeated || !IsOnApproach(player))
                continue;
            if (coordinatedCombat && Mathf.Abs(player.transform.position.y - transform.position.y) > 1.5f)
                continue;
            float distance = ((Vector2)(player.transform.position - transform.position)).sqrMagnitude;
            // Prefer a player we can actually engage over a nearer blocked one.
            if (coordinatedCombat && !CanHit(player)) distance += 10000f;
            if (distance < nearest)
            {
                nearest = distance;
                target = player;
            }
        }
    }

    private void Face(float horizontal)
    {
        if (Mathf.Abs(horizontal) > 0.05f && sprite.flipX != (horizontal > 0f))
            sprite.flipX = horizontal > 0f; // Enemy1's source art faces left.
    }

    private void SetMoving(bool value)
    {
        if (movementInitialized && moving == value) return;
        movementInitialized = true;
        moving = value;
        animator.SetBool(movimiento, value);
    }

    private IEnumerator PerformAttack(PlayerActions victim)
    {
        attackInProgress = true;
        try
        {
            yield return new WaitForSeconds(attackWindup);
            if (attackStyle == AttackStyle.Melee || attackStyle == AttackStyle.ShieldMelee)
            {
                if (CanHit(victim)) victim.TakeDamage(damage);
                yield break;
            }

            int count = attackStyle == AttackStyle.SMG ? shotsPerAttack : 1;
            for (int shot = 0; shot < count; shot++)
            {
                if (!FireAt(victim)) yield break;
                if (shot + 1 < count) yield return new WaitForSeconds(timeBetweenShots);
            }
        }
        finally { attackInProgress = false; }
    }

    private bool CanHit(PlayerActions victim)
    {
        return !health.IsDead && !GameFlowController.IsMatchEnded &&
               !GameFlowController.IsPaused && !PistolUpgradeShop.IsOpen &&
               victim != null && victim.isActiveAndEnabled && !victim.IsDefeated &&
               IsOnApproach(victim) &&
               Mathf.Abs(victim.transform.position.x - transform.position.x) <= EffectiveAttackRange &&
               Mathf.Abs(victim.transform.position.y - transform.position.y) <= 1.5f &&
               (!coordinatedCombat || HasLineOfSight(victim));
    }

    private bool HasLineOfSight(PlayerActions victim)
    {
        Vector2 origin = sprite.bounds.center;
        if (IsRanged)
        {
            float horizontal = victim.transform.position.x - transform.position.x;
            bool flipX = Mathf.Abs(horizontal) > 0.05f ? horizontal > 0f : sprite.flipX;
            if (!EnemyMuzzle.TryWorldPosition(sprite, firingMuzzleReference, flipX, out Vector2 muzzle) ||
                !HasClearShotPath(origin, muzzle)) return false;
            origin = muzzle;
        }
        return HasClearShotPath(origin, victim.AimOrigin);
    }

    private bool HasClearShotPath(Vector2 origin, Vector2 destination)
    {
        using var query = ConsultasFisicasCombate.Obtener();
        if (IsRanged)
        {
            Vector2 delta = destination - origin;
            float distance = delta.magnitude;
            if (distance < 0.0001f) return true;
            query.Barrer(origin, 0.06f, delta / distance, distance);
        }
        else query.TrazarLinea(origin, destination);
        foreach (RaycastHit2D hit in query.Impactos)
        {
            Collider2D collider = hit.collider;
            if (collider == null || collider.isTrigger ||
                collider.GetComponentInParent<EnemyHealth>() != null ||
                collider.GetComponentInParent<PlayerActions>() != null) continue;
            return false;
        }
        return true;
    }

    private Vector3 ConstrainMovement(Vector3 requested)
    {
        Vector3 current = transform.position;
        float direction = Mathf.Sign(requested.x - current.x);
        float distance = Mathf.Abs(requested.x - current.x);
        if (distance < 0.00001f) return current;
        float ground = StandingGroundY;

        foreach (BasicEnemy ally in activeEnemies)
        {
            if (ally == null || ally == this || !ally.coordinatedCombat || ally.health.IsDead ||
                health.IsShieldBroken || ally.health.IsShieldBroken ||
                ally.transform.parent != transform.parent ||
                Mathf.Abs(ally.StandingGroundY - ground) > 0.65f) continue;
            bool melee = attackStyle == AttackStyle.Melee || attackStyle == AttackStyle.ShieldMelee;
            bool rangedAlly = ally.attackStyle == AttackStyle.Pistol || ally.attackStyle == AttackStyle.SMG;
            // Melee units may overtake ranged allies; a gunner holding firing
            // range must not trap every punch/shield unit in a permanent queue.
            if (melee && rangedAlly) continue;
            float ahead = (ally.transform.position.x - current.x) * direction;
            if (ahead > 0f) distance = Mathf.Min(distance, Mathf.Max(0f, ahead - AllySpacing));
        }

        if (bodyCollider != null && distance > 0f)
        {
            using var query = ConsultasFisicasCombate.Obtener();
            query.BarrerCaja(bodyCollider.bounds.center, bodyCollider.bounds.size * 0.85f,
                Vector2.right * direction, distance);
            foreach (RaycastHit2D hit in query.Impactos)
            {
                Collider2D collider = hit.collider;
                if (collider == null || collider.isTrigger ||
                    collider.GetComponentInParent<EnemyHealth>() != null ||
                    collider.GetComponentInParent<PlayerActions>() != null) continue;
                distance = Mathf.Min(distance, Mathf.Max(0f, hit.distance - 0.02f));
            }
        }
        current.x += direction * distance;
        return current;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetActiveEnemies() => activeEnemies.Clear();

    private bool IsOnApproach(PlayerActions victim)
    {
        if (entranceTarget == null && !hasFixedEntranceTarget) return true;
        float destinationX = entranceTarget != null ? entranceTarget.position.x : fixedEntranceX;
        float advance = destinationX - transform.position.x;
        float toPlayer = victim.transform.position.x - transform.position.x;
        // An entrance-bound enemy never turns or fires back after passing a player.
        return advance * toPlayer >= 0f;
    }

    private bool FireAt(PlayerActions victim)
    {
        // Recheck at emission too: a windup or burst may outlive the crossing.
        if (!IsRanged || !CanHit(victim)) return false;
        Face(victim.transform.position.x - transform.position.x);
        // Idle has no raised weapon. Evaluate a firing pose before locating its
        // barrel, including when a damage pose interrupted the animation.
        if (!PrepareFiringPose() ||
            !EnemyMuzzle.TryWorldPosition(sprite, firingMuzzleReference, sprite.flipX, out Vector2 origin)) return false;
        Vector2 destination = victim.AimOrigin;
        // A muzzle beyond thin cover must never let the shot skip that cover.
        if (!HasClearShotPath(sprite.bounds.center, origin) ||
            (coordinatedCombat && !HasClearShotPath(origin, destination))) return false;
        float forward = sprite.flipX ? 1f : -1f;
        bool pointBlank = (destination.x - origin.x) * forward < 0f;
        // Preserve contact hits when the player overlaps the weapon, without
        // drawing a bullet backwards from the barrel into the enemy's body.
        Vector2 traceOrigin = pointBlank ? (Vector2)sprite.bounds.center : origin;
        Vector2 direction = destination - traceOrigin;
        float distance = direction.magnitude;
        direction = distance > 0.0001f ? direction / distance : Vector2.right * forward;

        Collider2D nearest = null;
        float nearestDistance = float.PositiveInfinity;
        using var consulta = ConsultasFisicasCombate.Obtener();
        consulta.Barrer(traceOrigin, 0.06f, direction, distance);
        foreach (RaycastHit2D hit in consulta.Impactos)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;
            if (hit.collider.GetComponentInParent<CoinPickup>() != null ||
                (hit.collider.isTrigger && hit.collider.GetComponentInParent<PlayerHealth>() == null))
                continue;
            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearest = hit.collider;
            }
        }

        if (nearest != null)
            nearest.GetComponentInParent<PlayerActions>()?.TakeDamage(damage);
        ShotVisual.EnemyShot(origin, pointBlank ? Vector2.right * forward : direction,
            pointBlank ? 0f : nearest != null ? nearestDistance : distance,
            attackStyle == AttackStyle.SMG ? 1 : 0, nearest != null);
        if (weaponShotSource != null)
        {
            weaponShotSource.volume = ShotAudioVolume;
            weaponShotSource.PlayOneShot(weaponAudioLibrary.Shot(ShotAudioWeapon));
        }
        return true;
    }

    private bool PrepareFiringPose()
    {
        bool smg = attackStyle == AttackStyle.SMG;
        if (EnemyMuzzle.IsFiringSprite(sprite.sprite, smg)) return true;
        int state = Animator.StringToHash(smg ? "Enemy4_SMGFire" : "Enemy2_PistolFire");
        if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, state)) return false;
        animator.ResetTrigger(attack);
        animator.Play(state, 0, 0f);
        animator.Update(0f);
        return EnemyMuzzle.IsFiringSprite(sprite.sprite, smg);
    }
}
