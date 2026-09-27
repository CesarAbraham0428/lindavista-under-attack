using UnityEngine;

/// <summary>Damage receiver for an enemy sprite and its defeat animation.</summary>
public sealed class EnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField, Min(0f)] private float defeatDespawnDelay = 1.2f;

    private Animator animator;
    private Collider2D hitbox;
    private int currentHealth;

    public bool IsDead { get; private set; }

    private void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
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
