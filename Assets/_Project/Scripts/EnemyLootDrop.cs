using UnityEngine;

[RequireComponent(typeof(EnemyHealth))]
public sealed class EnemyLootDrop : MonoBehaviour
{
    [SerializeField, Min(1)] private int coinValue = 5;
    [SerializeField] private GameObject coinPrefab;
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
        var renderer = GetComponent<SpriteRenderer>();
        Vector3 position = transform.position;
        if (renderer != null) position.y = renderer.bounds.min.y + 0.3f;
        position.z = 0f;
        GameObject template = coinPrefab != null ? coinPrefab : Resources.Load<GameObject>("CoinPickup");
        GameObject coin = template != null ? Instantiate(template, position, Quaternion.identity) : new GameObject("Coin");
        coin.transform.position = position;
        var pickup = coin.GetComponent<CoinPickup>();
        if (pickup == null) pickup = coin.AddComponent<CoinPickup>();
        pickup.Initialize(coinValue);
    }
}
