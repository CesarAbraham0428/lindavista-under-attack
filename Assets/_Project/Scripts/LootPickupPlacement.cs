using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One street-level baseline and generous collection area for every kind of loot.</summary>
public static class LootPickupPlacement
{
    public const float CollectionRadius = 0.7f;
    private const float CenterAboveGround = 0.3f;
    // The street is painted scenery, without a floor collider in Gameplay and Testing.
    private const float DefaultGroundY = -4.44f;
    private static readonly RaycastHit2D[] groundHits = new RaycastHit2D[16];
    private static PlayerHealth[] players;
    private static float nextPlayerSearch;
    private static int sceneHandle = -1;
    private static float groundY;
    private static bool hasGround;

    /// <summary>Preserves only horizontal spread; enemy canvas, size and lane never alter loot height.</summary>
    public static Vector3 PositionAtGround(Vector3 sourcePosition, float horizontalOffset = 0f)
    {
        EnsureScene();
        if (!hasGround) CaptureGround();
        return new Vector3(sourcePosition.x + horizontalOffset, groundY + CenterAboveGround, 0f);
    }

    public static void ConfigurePickup(GameObject pickup)
    {
        pickup.layer = 2; // Ignore Raycast: collectables must never obstruct a shot.
        CircleCollider2D trigger = pickup.GetComponent<CircleCollider2D>();
        if (trigger == null) trigger = pickup.AddComponent<CircleCollider2D>();
        trigger.enabled = true;
        trigger.isTrigger = true;
        trigger.offset = Vector2.zero;
        Vector3 scale = pickup.transform.lossyScale;
        trigger.radius = CollectionRadius / Mathf.Max(0.0001f, Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));

        Rigidbody2D body = pickup.GetComponent<Rigidbody2D>();
        if (body == null) body = pickup.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Kinematic;
        body.gravityScale = 0f;
        body.linearVelocity = Vector2.zero;
        body.angularVelocity = 0f;
        body.constraints = RigidbodyConstraints2D.FreezeAll;
        body.simulated = true;
    }

    /// <summary>Also catches overlaps missed by trigger callbacks or narrow player colliders.</summary>
    public static PlayerHealth FindNearbyPlayer(Vector3 position)
    {
        EnsureScene();
        RefreshPlayers();
        if (players == null) return null;
        float nearest = CollectionRadius * CollectionRadius;
        PlayerHealth result = null;
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.isActiveAndEnabled || player.IsDead) continue;
            Collider2D body = PlayerBody(player);
            if (body == null) continue;
            // ClosestPoint uses the player's actual body, so jumping above an item
            // cannot collect it merely by sharing the same horizontal position.
            float distance = ((Vector2)position - body.ClosestPoint(position)).sqrMagnitude;
            if (distance > nearest) continue;
            nearest = distance;
            result = player;
        }
        return result;
    }

    private static Collider2D PlayerBody(PlayerHealth player)
    {
        Collider2D body = player.GetComponent<Collider2D>();
        return body != null && body.enabled && !body.isTrigger ? body : null;
    }

    private static void EnsureScene()
    {
        int activeScene = SceneManager.GetActiveScene().handle;
        if (sceneHandle == activeScene) return;
        sceneHandle = activeScene;
        players = null;
        nextPlayerSearch = 0f;
        groundY = DefaultGroundY;
        hasGround = false;
    }

    private static void RefreshPlayers()
    {
        if (players != null && Time.unscaledTime < nextPlayerSearch) return;
        // Keep both character objects in the cache even when selection changes their
        // active state later in sceneLoaded; collection still filters inactive players.
        players = Object.FindObjectsByType<PlayerHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        nextPlayerSearch = Time.unscaledTime + 0.25f;
    }

    private static void CaptureGround()
    {
        RefreshPlayers();
        if (players == null) return;
        foreach (PlayerHealth player in players)
        {
            if (player == null || !player.isActiveAndEnabled || player.IsDead) continue;
            Collider2D body = PlayerBody(player);
            if (body == null) continue;

            groundY = body.bounds.min.y;
            var filter = new ContactFilter2D();
            filter.useTriggers = false;
            filter.SetLayerMask(Physics2D.DefaultRaycastLayers);
            int count = Physics2D.Raycast(body.bounds.center, Vector2.down, filter, groundHits, 8f);
            float closestFloor = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = groundHits[i].collider;
                if (hit == null || hit.GetComponentInParent<PlayerHealth>() != null ||
                    hit.GetComponentInParent<EnemyHealth>() != null || groundHits[i].normal.y < 0.5f)
                    continue;
                if (groundHits[i].distance >= closestFloor) continue;
                closestFloor = groundHits[i].distance;
                groundY = groundHits[i].point.y;
            }
            hasGround = true;
            return;
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        EnsureScene();
        // Snapshot before players move; first-kill or jump poses cannot raise the baseline.
        if (!hasGround) CaptureGround();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        sceneHandle = -1;
        players = null;
        nextPlayerSearch = 0f;
        groundY = DefaultGroundY;
        hasGround = false;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
}
