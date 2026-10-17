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
        LootPickupPlacement.ConfigurePickup(gameObject);
        if (GetComponent<SpriteRenderer>() == null)
        {
            var visual = gameObject.AddComponent<SpriteRenderer>();
            visual.sprite = ProgressionVisuals.Coin;
            visual.sortingOrder = 20;
        }
    }

    private void Start()
    {
        transform.position = LootPickupPlacement.PositionAtGround(transform.position);
    }

    private void FixedUpdate()
    {
        if (!collected && !collecting && Time.unscaledTime >= nextAttempt && !GameFlowController.IsPaused)
            TryCollect(LootPickupPlacement.FindNearbyPlayer(transform.position));
    }

    public void Initialize(int amount) { value = Mathf.Max(1, amount); }
    private void OnTriggerEnter2D(Collider2D other) { TryCollect(other.GetComponentInParent<PlayerHealth>()); }
    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.unscaledTime >= nextAttempt) TryCollect(other.GetComponentInParent<PlayerHealth>());
    }

    public bool TryCollect(PlayerHealth player)
    {
        if (collected || collecting || !isActiveAndEnabled || player == null || !player.isActiveAndEnabled ||
            player.IsDead || GameFlowController.IsMatchEnded || GameFlowController.IsPaused) return false;
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
