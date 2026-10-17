using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Exercises the real shield prefab and projectile damage paths without touching player inventories.</summary>
public static class ExplosiveDamageChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Vector2 TestPosition = new Vector2(12000f, 12000f);
    private static readonly List<GameObject> created = new List<GameObject>();
    private static HashSet<GameObject> originalEffects;
    private static int assertions;

    [MenuItem("Lindavista/Verify explosives against shields (Play Mode)")]
    public static void Run() { Debug.Log(RunRuntime()); }

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded)
            throw new InvalidOperationException("Run shield checks in active Play Mode.");
        assertions = 0;
        originalEffects = new HashSet<GameObject>();
        foreach (var effect in Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None))
            originalEffects.Add(effect.gameObject);
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
            originalEffects.Add(effect.gameObject);
        try
        {
            Check(WeaponDefinition.At(2).DamageAt(0) == 10, "base RPG retains ten damage");
            foreach (bool flipped in new[] { false, true })
            {
                VerifyBullets(flipped);
                Clear();
                VerifyDirectRockets(flipped, true);
                Clear();
                VerifyDirectRockets(flipped, false);
                Clear();
                VerifySplash(flipped);
                Clear();
                VerifyGrenade(flipped);
                Clear();
            }
            return "EXPLOSIVE_DAMAGE_CHECKS_OK: " + assertions +
                " assertions; actual shield prefab, both facings, front/rear RPG hits, slow reload cadence, " +
                "three base rockets to defeat, splash deduplication, cover, grenade blasts and ordinary bullet shield rules.";
        }
        finally { Clear(); originalEffects = null; }
    }

    private static void VerifyBullets(bool flipped)
    {
        EnemyHealth shield = Shield(flipped);
        Vector2 front = flipped ? Vector2.left : Vector2.right;
        shield.TakeDamage(2, front);
        Check(shield.CurrentHealth == 30 && !shield.IsShieldBroken, "ordinary frontal bullet is blocked");
        shield.TakeDamage(2, -front);
        Check(shield.CurrentHealth == 28, "ordinary rear bullet damages health");
        Clear();

        shield = Shield(flipped);
        for (int hit = 0; hit < 6; hit++) shield.TakeDamage(2, front);
        Check(shield.CurrentHealth == 30 && shield.IsShieldBroken, "six rapid frontal bullets break the shield without hurting the body");
        ExpireShieldHitWindow(shield);
        Check(shield.IsShieldBroken, "the broken shield remains lost after the hit window expires");
        shield.TakeDamage(2, front);
        Check(shield.CurrentHealth == 28, "ordinary frontal bullet damages after the shield breaks permanently");
        Clear();

        shield = Shield(flipped);
        Set(shield, "consecutiveShieldHits", 5);
        ExpireShieldHitWindow(shield);
        shield.TakeDamage(2, front);
        Check(shield.CurrentHealth == 30 && !shield.IsShieldBroken &&
            (int)typeof(EnemyHealth).GetField("consecutiveShieldHits", Private).GetValue(shield) == 1,
            "slow ordinary bullets still reset the consecutive hit window");
        shield.TakeExplosionDamage(0);
        shield.TakeExplosionDamage(-10);
        Check(shield.CurrentHealth == 30 && !shield.IsShieldBroken, "invalid blast damage cannot damage or open shield");
    }

    private static void VerifyDirectRockets(bool flipped, bool frontal)
    {
        EnemyHealth shield = Shield(flipped);
        // Extra hitboxes must not turn a direct hit plus its blast into multiple damage events.
        var duplicate = new GameObject("Additional shield hitbox");
        duplicate.transform.SetParent(shield.transform, false);
        duplicate.AddComponent<BoxCollider2D>().isTrigger = true;
        Vector2 direction = (flipped ? Vector2.left : Vector2.right) * (frontal ? 1f : -1f);
        int notifications = 0, defeats = 0;
        shield.HealthChanged += (_, __) => notifications++;
        shield.Died += _ => defeats++;
        int damage = WeaponDefinition.At(2).DamageAt(0);
        for (int hit = 1; hit <= 3; hit++)
        {
            // Model reload gaps without spending wall-clock seconds. A broken shield never resets.
            ExpireShieldHitWindow(shield);
            Physics2D.SyncTransforms();
            Bounds bounds = shield.GetComponent<Collider2D>().bounds;
            Vector2 origin = (Vector2)bounds.center - direction * (bounds.extents.x + 3f);
            CombatProjectile shot = Fire(origin, direction, damage);
            Step(shot);
            Check(shield.CurrentHealth == Mathf.Max(0, 30 - hit * damage),
                "actual " + (frontal ? "front" : "rear") + " RPG hit " + hit + " damages across reload gaps");
            Check(notifications == hit && defeats == (hit == 3 ? 1 : 0),
                "direct plus splash and duplicate colliders report exactly one damage event per rocket");
            if (hit < 3) Check(shield.IsShieldBroken, "surviving a blast permanently breaks the shield");
            Step(shot);
            Check(shield.CurrentHealth == Mathf.Max(0, 30 - hit * damage) && notifications == hit,
                "spent rocket cannot damage again");
            Object.DestroyImmediate(shot.gameObject);
        }
        Check(shield.IsDead && !shield.GetComponent<Collider2D>().enabled, "third base rocket defeats real thirty-health shield enemy");
        shield.TakeExplosionDamage(10);
        Check(shield.CurrentHealth == 0 && notifications == 3 && defeats == 1, "dead enemy ignores later explosions");
    }

    private static void VerifySplash(bool flipped)
    {
        EnemyHealth shield = Shield(flipped);
        var duplicate = new GameObject("Splash duplicate hitbox");
        duplicate.transform.SetParent(shield.transform, false);
        duplicate.AddComponent<BoxCollider2D>().isTrigger = true;
        Physics2D.SyncTransforms();
        Bounds bounds = shield.GetComponent<Collider2D>().bounds;
        Vector2 front = flipped ? Vector2.left : Vector2.right;
        Vector2 origin = (Vector2)bounds.center - front * (bounds.extents.x + .2f);
        Splash(origin, 10, front);
        Check(shield.CurrentHealth == 20 && shield.IsShieldBroken,
            "frontal area blast bypasses shield and damages exactly once across duplicate colliders");
        Clear();

        shield = Shield(flipped);
        Physics2D.SyncTransforms();
        bounds = shield.GetComponent<Collider2D>().bounds;
        origin = (Vector2)bounds.center - front * (bounds.extents.x + .8f);
        var wall = new GameObject("Shield verification solid cover");
        created.Add(wall);
        wall.transform.position = origin + front * .4f;
        wall.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 20f);
        Splash(origin, 10, front);
        Check(shield.CurrentHealth == 30 && !shield.IsShieldBroken, "solid cover still blocks blast damage");
    }

    private static void VerifyGrenade(bool flipped)
    {
        EnemyHealth shield = Shield(flipped);
        Physics2D.SyncTransforms();
        Bounds bounds = shield.GetComponent<Collider2D>().bounds;
        Vector2 front = flipped ? Vector2.left : Vector2.right;
        var go = new GameObject("Shield verification grenade");
        created.Add(go);
        go.transform.position = (Vector2)bounds.center - front * (bounds.extents.x + .2f);
        var grenade = go.AddComponent<GrenadeProjectile>();
        grenade.enabled = false;
        int damage = WeaponDefinition.ForCharacter(2, true).DamageAt(0);
        typeof(GrenadeProjectile).GetField("damage", Private).SetValue(grenade, damage);
        typeof(GrenadeProjectile).GetField("shotDirection", Private).SetValue(grenade, front);
        typeof(GrenadeProjectile).GetMethod("Explode", Private).Invoke(grenade, null);
        Check(shield.CurrentHealth == Mathf.Max(0, 30 - damage), "Cesar grenade uses the same shield-safe blast damage");
        typeof(GrenadeProjectile).GetMethod("Explode", Private).Invoke(grenade, null);
        Check(shield.CurrentHealth == Mathf.Max(0, 30 - damage), "spent grenade cannot repeat blast damage");
    }

    private static EnemyHealth Shield(bool flipped)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy3_Base.prefab");
        Check(prefab != null, "real shield enemy prefab is available");
        var go = Object.Instantiate(prefab, TestPosition, Quaternion.identity);
        created.Add(go);
        // Prevent defeat loot and autonomous movement; preserve the actual shield health, art and hitbox.
        Object.DestroyImmediate(go.GetComponent<EnemyLootDrop>());
        go.GetComponent<BasicEnemy>().enabled = false;
        var animator = go.GetComponent<Animator>();
        animator.Play("Enemy3_Idle", 0, 0f);
        animator.Update(0f);
        animator.enabled = false;
        go.GetComponent<SpriteRenderer>().flipX = flipped;
        var health = go.GetComponent<EnemyHealth>();
        Check(health.CurrentHealth == 30 && health.MaxHealth == 30, "prefab retains thirty health");
        Check((bool)typeof(EnemyHealth).GetField("shieldBlocksFront", Private).GetValue(health), "prefab shield is enabled");
        return health;
    }

    private static CombatProjectile Fire(Vector2 origin, Vector2 direction, int damage)
    {
        var previous = new HashSet<CombatProjectile>(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
        Check(CombatProjectile.TrySpawn(origin, direction, damage, 500f, .2f, 20f, null, 2), "RPG verification shot spawns");
        foreach (var shot in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
            if (!previous.Contains(shot))
            {
                shot.enabled = false;
                created.Add(shot.gameObject);
                return shot;
            }
        throw new InvalidOperationException("Shield check could not find its RPG.");
    }

    private static void Step(CombatProjectile shot)
    {
        Physics2D.SyncTransforms();
        typeof(CombatProjectile).GetMethod("FixedUpdate", Private).Invoke(shot, null);
    }

    private static void Splash(Vector2 position, int damage, Vector2 direction)
    {
        Physics2D.SyncTransforms();
        typeof(CombatProjectile).GetMethod("ApplySplashDamage", StaticPrivate)
            .Invoke(null, new object[] { position, damage, direction, null, null });
    }

    private static void ExpireShieldHitWindow(EnemyHealth shield)
    {
        Set(shield, "lastShieldHitTime", Time.time - 10f);
    }

    private static void Set(EnemyHealth shield, string field, object value) =>
        typeof(EnemyHealth).GetField(field, Private).SetValue(shield, value);

    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new InvalidOperationException("Shield check failed: " + label);
    }

    private static void Clear()
    {
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        foreach (var effect in Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None))
            if (originalEffects == null || !originalEffects.Contains(effect.gameObject)) Object.DestroyImmediate(effect.gameObject);
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
            if (originalEffects == null || !originalEffects.Contains(effect.gameObject)) Object.DestroyImmediate(effect.gameObject);
        Physics2D.SyncTransforms();
    }
}
