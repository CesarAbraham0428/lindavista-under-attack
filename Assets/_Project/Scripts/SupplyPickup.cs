using UnityEngine;

/// <summary>A saved ammunition pack or pill for the character playing this match.</summary>
[RequireComponent(typeof(CircleCollider2D), typeof(Rigidbody2D), typeof(SpriteRenderer))]
public sealed class SupplyPickup : MonoBehaviour
{
    private int weaponIndex = -1;
    private int amount = 1;
    private bool isCesar;
    private bool initialized;
    private bool collected;
    private bool collecting;
    private float nextAttempt;

    public int WeaponIndex => weaponIndex;
    public int Amount => amount;
    public bool IsCesar => isCesar;
    public bool IsHealth => weaponIndex < 0;
    public bool IsCollected => collected;

    private void Awake() => LootPickupPlacement.ConfigurePickup(gameObject);
    private void Start() => transform.position = LootPickupPlacement.PositionAtGround(transform.position);

    public void InitializeAmmo(int slot, bool cesar, int quantity)
    {
        if (initialized || slot < 0 || slot >= 3 || quantity <= 0) return;
        weaponIndex = slot;
        amount = quantity;
        isCesar = cesar;
        InitializeVisual(LootPickupVisuals.Ammo(slot, cesar));
    }

    public void InitializeHealth(bool cesar)
    {
        if (initialized) return;
        weaponIndex = -1;
        amount = 1;
        isCesar = cesar;
        InitializeVisual(LootPickupVisuals.Health);
    }

    private void InitializeVisual(Sprite sprite)
    {
        var visual = GetComponent<SpriteRenderer>();
        visual.sprite = sprite;
        visual.sortingOrder = 20;
        initialized = true;
    }

    private void FixedUpdate()
    {
        if (initialized && !collected && Time.unscaledTime >= nextAttempt && !GameFlowController.IsPaused)
            TryCollect(LootPickupPlacement.FindNearbyPlayer(transform.position));
    }

    private void OnTriggerEnter2D(Collider2D other) => TryCollect(other.GetComponentInParent<PlayerHealth>());
    private void OnTriggerStay2D(Collider2D other)
    {
        if (Time.unscaledTime >= nextAttempt) TryCollect(other.GetComponentInParent<PlayerHealth>());
    }

    public bool TryCollect(PlayerHealth player)
    {
        if (!initialized || collected || collecting || !isActiveAndEnabled || player == null ||
            !player.isActiveAndEnabled || player.IsDead || player.IsCesar != isCesar ||
            GameFlowController.IsMatchEnded || GameFlowController.IsPaused) return false;
        collecting = true;
        bool success = IsHealth
            ? ProgressionService.TryGrantHealthConsumable(isCesar)
            : ProgressionService.TryGrantAmmo(weaponIndex, amount, isCesar);
        collecting = false;
        if (!success)
        {
            // Full inventories and save failures leave the reward available to retry.
            nextAttempt = Time.unscaledTime + 1f;
            return false;
        }
        collected = true;
        GetComponent<CircleCollider2D>().enabled = false;
        Destroy(gameObject);
        return true;
    }
}
