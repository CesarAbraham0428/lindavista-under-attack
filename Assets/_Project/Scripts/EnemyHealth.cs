using UnityEngine;

/// <summary>Damage receiver for an enemy sprite and its defeat animation.</summary>
public sealed class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Min(0f)] private float defeatDespawnDelay = 1.2f;
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
    private float shieldBrokenUntil;

    public bool IsDead { get; private set; }
    public bool IsShieldBroken => shieldBlocksFront && !IsDead && Time.time < shieldBrokenUntil;

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        hitbox = GetComponent<Collider2D>();
        if (hitbox == null)
        {
            SpriteRenderer sprite = GetComponent<SpriteRenderer>();
            BoxCollider2D box = gameObject.AddComponent<BoxCollider2D>();
            if (sprite != null && sprite.sprite != null)
            {
                Bounds bounds = sprite.sprite.bounds;
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
        if (IsDead || amount <= 0)
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
                shieldBrokenUntil = Time.time + shieldBreakDuration;
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

    public void TakeDamage(int amount)
    {
        if (IsDead || amount <= 0)
            return;

        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            IsDead = true;
            hitbox.enabled = false;
            if (animator != null)
            {
                animator.ResetTrigger("TakeDamage");
                animator.SetTrigger("Defeat");
            }
            Destroy(gameObject, defeatDespawnDelay);
        }
        else if (animator != null)
        {
            animator.SetTrigger("TakeDamage");
        }
    }
}
