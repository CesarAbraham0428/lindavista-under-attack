using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyLootDrop : MonoBehaviour
{
    [SerializeField, Min(1)] private int coinValue = 5;
    [SerializeField] private GameObject coinPrefab;
    [Header("Additional supplies (coins are always dropped)")]
    [SerializeField, Range(0f, 1f)] private float ammoDropChance = 0.4f;
    [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.15f;
    [SerializeField, Min(0.75f)] private float pickupSpacing = 0.9f;
    private EnemyHealth health;
    private bool dropped;
    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        health.Died += Drop;
    }
    private void OnDestroy() { if (health != null) health.Died -= Drop; }

    private void Drop(EnemyHealth enemy)
    {
        if (dropped || GameFlowController.IsMatchEnded) return;
        dropped = true;
        // Enemy sprites, death animations and spawn lanes have different baselines.
        // Every reward instead shares the player's fixed ground line.
        Vector3 position = LootPickupPlacement.PositionAtGround(transform.position);
        PlayerHealth player = FindFirstObjectByType<PlayerActions>()?.Health;
        bool cesar = player != null ? player.IsCesar : CharacterWeapons.SelectedCesar;
        int ammoSlot = Random.value < ammoDropChance ? SelectAmmoSlot(cesar) : -1;
        bool healthDrop = Random.value < healthDropChance;
        int count = 1 + (ammoSlot >= 0 ? 1 : 0) + (healthDrop ? 1 : 0);
        float firstOffset = -(count - 1) * pickupSpacing * 0.5f;
        position.x += firstOffset;
        GameObject template = coinPrefab != null ? coinPrefab : Resources.Load<GameObject>("CoinPickup");
        GameObject coin = template != null ? Instantiate(template, position, Quaternion.identity) : new GameObject("Coin");
        coin.transform.position = position;
        var pickup = coin.GetComponent<CoinPickup>();
        if (pickup == null) pickup = coin.AddComponent<CoinPickup>();
        pickup.Initialize(coinValue);

        int offset = 1;
        if (ammoSlot >= 0)
        {
            var ammo = new GameObject("Ammo Pickup").AddComponent<SupplyPickup>();
            ammo.transform.position = position + Vector3.right * (pickupSpacing * offset++);
            ammo.InitializeAmmo(ammoSlot, cesar, ShopBalance.Pack[ammoSlot]);
        }
        if (healthDrop)
        {
            var pill = new GameObject("Pill Pickup").AddComponent<SupplyPickup>();
            pill.transform.position = position + Vector3.right * (pickupSpacing * offset);
            pill.InitializeHealth(cesar);
        }
    }

    public static int SelectAmmoSlot(bool cesar)
    {
        int mask = 0;
        for (int slot = 0; slot < 3; slot++)
            if (ProgressionService.OwnsWeapon(slot, cesar)) mask |= 1 << slot;
        return SelectAmmoSlot(mask);
    }

    // Choosing within the eligible set gives every unlocked weapon equal weight.
    public static int SelectAmmoSlot(int availableMask)
    {
        int count = 0;
        for (int slot = 0; slot < 3; slot++)
            if ((availableMask & (1 << slot)) != 0) count++;
        if (count == 0) return -1;
        int choice = Random.Range(0, count);
        for (int slot = 0; slot < 3; slot++)
            if ((availableMask & (1 << slot)) != 0 && choice-- == 0) return slot;
        return -1;
    }
}
