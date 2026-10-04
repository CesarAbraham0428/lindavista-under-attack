using UnityEngine;

[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D))]
public sealed class CoinPickup : MonoBehaviour
{
    [SerializeField, Min(1)] private int value = 5;
    private bool collected;
    private bool collecting;
    private float nextAttempt;
    public int Value => value;
    public bool IsCollected => collected;

    private void Awake()
    {
        gameObject.layer = 2; // Ignore Raycast: loot must never stop a shot.
        GetComponent<CircleCollider2D>().isTrigger = true;
        var body = GetComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        if (GetComponent<SpriteRenderer>() == null)
        {
            var visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sprite = ProgressionVisuals.Coin;
            visual.sortingOrder = 20;
        }
    }

    public void Initialize(int amount) { value = Mathf.Max(1, amount); }
    private void OnTriggerEnter2D(Collider2D other) { TryCollect(other.GetComponentInParent<PlayerHealth>()); }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.unscaledTime >= nextAttempt) TryCollect(other.GetComponentInParent<PlayerHealth>());
    }

    public bool TryCollect(PlayerHealth player)
    {
        if (collected || collecting || player == null || !player.gameObject.activeInHierarchy ||
            player.IsDead || GameFlowController.IsMatchEnded) return false;
        collecting = true;
        if (!ProgressionService.TryCredit(value))
        {
            collecting = false;
            nextAttempt = Time.unscaledTime + 1f;
            return false;
        }
        collected = true;
        GetComponent<CircleCollider2D>().enabled = false;
        GameFlowController.ReportCoinsCollected(value);
        Destroy(gameObject);
        return true;
    }
}
