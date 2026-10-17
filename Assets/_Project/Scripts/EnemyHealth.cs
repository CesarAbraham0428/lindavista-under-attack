using System;
using UnityEngine;

/// <summary>Damage receiver for an enemy sprite and its defeat animation.</summary>
public sealed class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Min(0f)] private float defeatDespawnDelay = 1.2f;
    [SerializeField] private bool playDamageAnimation = true;
    [SerializeField] private bool shieldBlocksFront;
    [SerializeField, Min(1)] private int hitsToBreakShield = 3;
    [SerializeField, Min(0.1f)] private float shieldHitWindow = 1.5f;
    [SerializeField, Min(0.1f)] private float shieldBreakDuration = 2.5f;

    private Animator animator;
    private Collider2D hitbox;
    private SpriteRenderer spriteRenderer;
    private int currentHealth;
    private int consecutiveShieldHits;
    private float lastShieldHitTime;
    private bool shieldBroken;

    public bool IsDead { get; private set; }
    public int CurrentHealth => Mathf.Max(0, currentHealth);
    public int MaxHealth => maxHealth;
    public event Action<int, int> HealthChanged;
    public event Action<EnemyHealth> Died;
    public event Action ShieldBroken;
    public bool IsShieldBroken => shieldBlocksFront && shieldBroken;

    /// <summary>Applies wave-specific shield durability. A broken shield never regenerates.</summary>
    public void ConfigureShieldOpening(int hits, float duration)
    {
        if (!shieldBlocksFront || IsDead) return;
        hitsToBreakShield = Mathf.Max(1, hits);
        shieldBreakDuration = Mathf.Max(0.1f, duration);
        consecutiveShieldHits = 0;
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
        if (hitbox == null)
        {
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                Bounds bounds = spriteRenderer.sprite.bounds;
                box.offset = bounds.center;
                box.size = new Vector2(Mathf.Max(0.2f, bounds.size.x * 0.8f),
                    Mathf.Max(0.3f, bounds.size.y * 0.85f));
            }
            hitbox = box;
        }
        hitbox.isTrigger = true;
    }

    public void TakeDamage(int amount, Vector2 shotDirection)
    {
        if (IsDead || amount <= 0 || GameFlowController.IsMatchEnded)
            return;

        if (shieldBlocksFront && !IsShieldBroken && spriteRenderer != null &&
            (spriteRenderer.flipX ? shotDirection.x < -0.1f : shotDirection.x > 0.1f) &&
            !IsShieldOpen())
        {
            if (Time.time - lastShieldHitTime > shieldHitWindow)
                consecutiveShieldHits = 0;

            consecutiveShieldHits++;
            lastShieldHitTime = Time.time;
            if (consecutiveShieldHits >= hitsToBreakShield)
            {
                consecutiveShieldHits = 0;
                BreakShield();
            }
            return;
        }

        consecutiveShieldHits = 0;
        TakeDamage(amount);
    }

    private bool IsShieldOpen()
    {
        if (animator == null)
            return false;
        if (animator.GetCurrentAnimatorStateInfo(0).IsName("Enemy3_HeavyPunch"))
            return true;
        return animator.IsInTransition(0) &&
               animator.GetNextAnimatorStateInfo(0).IsName("Enemy3_HeavyPunch");
    }

    private void BreakShield()
    {
        if (!shieldBlocksFront || shieldBroken) return;
        shieldBroken = true;
        consecutiveShieldHits = 0;
        ShieldBroken?.Invoke();
    }

    /// <summary>Blast pressure damages through the shield regardless of impact direction or firing cadence.</summary>
    public void TakeExplosionDamage(int amount)
    {
        if (IsDead || amount <= 0 || GameFlowController.IsMatchEnded)
            return;

        consecutiveShieldHits = 0;
        BreakShield();
        TakeDamage(amount);
    }

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0 || GameFlowController.IsMatchEnded)
            return;

        currentHealth = Mathf.Max(0, currentHealth - amount);
        if (currentHealth <= 0)
        {
            IsDead = true;
            hitbox.enabled = false;
            if (animator != null)
            {
                animator.ResetTrigger("TakeDamage");
                animator.SetTrigger("Defeat");
            }
            Destroy(gameObject, GetDefeatDespawnDelay());
            HealthChanged?.Invoke(currentHealth, maxHealth);
            Died?.Invoke(this);
        }
        else if (animator != null && playDamageAnimation)
        {
            animator.SetTrigger("TakeDamage");
        }
        if (!IsDead) HealthChanged?.Invoke(currentHealth, maxHealth);
    }

    private float GetDefeatDespawnDelay()
    {
        float delay = defeatDespawnDelay;
        if (!IsShieldBroken || animator == null || animator.runtimeAnimatorController == null)
            return delay;
        // Let the exposed actor finish falling and hold the grounded pose.
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            if (clip.name.EndsWith("_Defeat", StringComparison.Ordinal))
                delay = Mathf.Max(delay, clip.length + .4f);
        return delay;
    }
}
