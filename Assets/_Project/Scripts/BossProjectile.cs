using System.Collections.Generic;
using UnityEngine;

/// <summary>Swept, pooled hostile rounds; no instant damage at long range.</summary>
public sealed class BossProjectile : MonoBehaviour
{
    private static readonly SceneCombatPool<BossProjectile> pool = new("Boss round", 16);
    private static readonly HashSet<BossProjectile> flying = new();
    private static readonly List<BossProjectile> cancelled = new(16);
    private EnemyHealth owner;
    private Vector2 direction;
    private float speed, remaining;
    private int damage;
    private bool retired;
    private SpriteRenderer visual;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() { flying.Clear(); cancelled.Clear(); }
    internal static void Prewarm() => pool.Prewarm(8, shot => shot.PrepareVisual());
    private void PrepareVisual()
    {
        ShotVisual.Attach(gameObject, 1);
        if (visual == null) visual = GetComponent<SpriteRenderer>();
        visual.color = new Color(1f, .64f, .35f);
    }
    public static bool Fire(Vector2 origin, Vector2 direction, float speed, int damage, EnemyHealth owner)
    {
        if (owner == null || owner.IsDead || GameFlowController.IsMatchEnded || flying.Count >= 16 ||
            direction.sqrMagnitude < .0001f) return false;
        var shot = pool.Rent();
        shot.owner = owner;
        shot.direction = direction.normalized;
        shot.speed = Mathf.Max(1, speed);
        shot.damage = Mathf.Max(1, damage);
        shot.remaining = 24f;
        shot.retired = false;
        shot.transform.position = origin;
        shot.transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        shot.PrepareVisual();
        flying.Add(shot);
        shot.gameObject.SetActive(true);
        ShotVisual.Muzzle(origin, shot.direction, 1);
        return true;
    }
    private void FixedUpdate()
    {
        if (retired) return;
        if (owner == null || owner.IsDead || GameFlowController.IsMatchEnded) { Retire(); return; }
        if (GameFlowController.IsPaused || PistolUpgradeShop.IsOpen) return;
        float distance = Mathf.Min(speed * Time.fixedDeltaTime, remaining);
        Vector2 origin = transform.position;
        using var query = ConsultasFisicasCombate.Obtener();
        query.Barrer(origin, .07f, direction, distance);
        Collider2D nearest = null;
        float closest = float.PositiveInfinity;
        foreach (RaycastHit2D hit in query.Impactos)
        {
            var collider = hit.collider;
            if (collider == null || collider.GetComponentInParent<EnemyHealth>() != null ||
                collider.GetComponentInParent<CoinPickup>() != null) continue;
            var player = collider.GetComponentInParent<PlayerActions>();
            if (player != null && player.IsDefeated) continue;
            if (collider.isTrigger && player == null) continue;
            if (hit.distance < closest) { closest = hit.distance; nearest = collider; }
        }
        if (nearest != null)
        {
            nearest.GetComponentInParent<PlayerActions>()?.TakeDamage(damage);
            ShotVisual.Impact(origin + direction * closest, direction, 1);
            Retire();
            return;
        }
        transform.position = origin + direction * distance;
        remaining -= distance;
        if (remaining <= .001f) Retire();
    }
    private void Retire()
    {
        if (retired) return;
        retired = true;
        flying.Remove(this);
        owner = null;
        pool.Return(this);
    }
    private void OnDisable() { flying.Remove(this); }
    public static void CancelFor(EnemyHealth owner)
    {
        cancelled.Clear();
        foreach (var shot in flying) if (shot != null && shot.owner == owner) cancelled.Add(shot);
        foreach (var shot in cancelled) shot.Retire();
        cancelled.Clear();
    }
}
