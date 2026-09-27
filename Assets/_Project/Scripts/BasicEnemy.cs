using System.Collections;
using UnityEngine;

/// <summary>Enemy combat and optional advance toward a scene entrance.</summary>
[RequireComponent(typeof(EnemyHealth), typeof(Animator), typeof(SpriteRenderer))]
public sealed class BasicEnemy : MonoBehaviour
{
    private enum AttackStyle { Melee, Pistol, ShieldMelee, SMG }

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
    [Header("Objective (optional)")]
    [SerializeField] private Transform entranceTarget;
    [Header("Shield enemy")]
    [SerializeField] private Sprite exposedSprite;

    private EnemyHealth health;
    private Animator animator;
    private SpriteRenderer sprite;
    private PlayerActions target;
    private float nextTargetSearch;
    private float nextAttack;
    private bool wasShieldBroken;
    private bool hasFixedEntranceTarget;
    private float fixedEntranceX;
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
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("TakeDamage");
        animator.ResetTrigger("Defeat");
        nextAttack = Time.time + attackCooldown;
        transform.localScale *= sizeMultiplier;
    }

    private void Update()
    {
        if (health.IsDead)
        {
            animator.SetBool("IsMoving", false);
            return;
        }

        bool shieldBroken = attackStyle == AttackStyle.ShieldMelee && health.IsShieldBroken;
        if (shieldBroken != wasShieldBroken)
        {
            animator.ResetTrigger("Attack");
            animator.Play("Enemy3_Idle", 0, 0f);
            wasShieldBroken = shieldBroken;
        }
        if (shieldBroken)
        {
            animator.SetBool("IsMoving", false);
            nextAttack = Time.time + 0.5f;
            return;
        }

        if (Time.time >= nextTargetSearch)
        {
            FindTarget();
            nextTargetSearch = Time.time + 0.25f;
        }

        if (entranceTarget != null || hasFixedEntranceTarget)
        {
            AdvanceToEntrance();
            if (target != null && !target.IsDefeated && CanHit(target) && Time.time >= nextAttack)
            {
                Face(target.transform.position.x - transform.position.x);
                nextAttack = Time.time + attackCooldown;
                animator.SetTrigger("Attack");
                StartCoroutine(PerformAttack(target));
            }
            return;
        }

        if (target == null || target.IsDefeated)
        {
            animator.SetBool("IsMoving", false);
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
            animator.SetBool("IsMoving", !Mathf.Approximately(position.x, transform.position.x));
            transform.position = position;
            return;
        }

        animator.SetBool("IsMoving", false);
        if (Mathf.Abs(delta.y) <= 1.5f && Time.time >= nextAttack)
        {
            nextAttack = Time.time + attackCooldown;
            animator.SetTrigger("Attack");
            StartCoroutine(PerformAttack(target));
        }
    }

    private void AdvanceToEntrance()
    {
        Vector3 position = transform.position;
        float destinationX = entranceTarget != null ? entranceTarget.position.x : fixedEntranceX;
        Face(destinationX - position.x);
        position.x = Mathf.MoveTowards(position.x, destinationX, speed * Time.deltaTime);
        animator.SetBool("IsMoving", !Mathf.Approximately(position.x, transform.position.x));
        transform.position = position;
    }

    private void LateUpdate()
    {
        // Animator writes SpriteRenderer.sprite each frame; override it after evaluation.
        if (attackStyle == AttackStyle.ShieldMelee && health.IsShieldBroken && exposedSprite != null)
            sprite.sprite = exposedSprite;
    }

    private void FindTarget()
    {
        target = null;
        float nearest = float.PositiveInfinity;
        foreach (PlayerActions player in FindObjectsByType<PlayerActions>(FindObjectsSortMode.None))
        {
            if (player.IsDefeated)
                continue;
            float distance = ((Vector2)(player.transform.position - transform.position)).sqrMagnitude;
            if (distance < nearest)
            {
                nearest = distance;
                target = player;
            }
        }
    }

    private void Face(float horizontal)
    {
        if (Mathf.Abs(horizontal) > 0.05f)
            sprite.flipX = horizontal > 0f; // Enemy1's source art faces left.
    }

    private IEnumerator PerformAttack(PlayerActions victim)
    {
        yield return new WaitForSeconds(attackWindup);
        if (attackStyle == AttackStyle.Melee || attackStyle == AttackStyle.ShieldMelee)
        {
            if (CanHit(victim))
                victim.TakeDamage(damage);
            yield break;
        }

        int count = attackStyle == AttackStyle.SMG ? shotsPerAttack : 1;
        for (int shot = 0; shot < count; shot++)
        {
            if (!CanHit(victim))
                yield break;
            FireAt(victim);
            if (shot + 1 < count)
                yield return new WaitForSeconds(timeBetweenShots);
        }
    }

    private bool CanHit(PlayerActions victim)
    {
        return !health.IsDead &&
               !(attackStyle == AttackStyle.ShieldMelee && health.IsShieldBroken) &&
               victim != null && !victim.IsDefeated &&
               Mathf.Abs(victim.transform.position.x - transform.position.x) <= EffectiveAttackRange &&
               Mathf.Abs(victim.transform.position.y - transform.position.y) <= 1.5f;
    }

    private void FireAt(PlayerActions victim)
    {
        Vector2 origin = sprite.bounds.center;
        Vector2 destination = victim.AimOrigin;
        Vector2 direction = destination - origin;
        float distance = direction.magnitude;
        if (distance < 0.01f)
            return;
        direction /= distance;
        origin += direction * 0.35f;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(origin, 0.06f, direction, distance);
        Collider2D nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.GetComponentInParent<EnemyHealth>() != null)
                continue;
            if (hit.distance < nearestDistance)
            {
                nearestDistance = hit.distance;
                nearest = hit.collider;
            }
        }

        if (nearest != null)
            nearest.GetComponentInParent<PlayerActions>()?.TakeDamage(damage);
    }
}
