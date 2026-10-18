using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Exercises real projectile damage and floating-number lifetimes without touching saved profiles or ammunition.</summary>
public static class DamageNumberChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    private static readonly Vector2 Origin = new(24000f, 24000f);
    private static readonly List<GameObject> created = new();
    private static readonly List<GameObject> fixtures = new();
    private static readonly List<Object> resources = new();
    private static Sprite enemySprite;
    private static int assertions;

    [MenuItem("Tools/Lindavista/Verify damage numbers (empty Play Mode scene)")]
    public static void Run() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen ||
            Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None).Length > 0 ||
            Object.FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None).Length > 0 ||
            Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).Length > 0 || Active().Length > 0)
            throw new InvalidOperationException("Use an empty, unpaused Play Mode scene with no enemies, players or live projectiles.");

        assertions = 0;
        float scale = Time.timeScale;
        try
        {
            Time.timeScale = 1f;
            BuildFixtures();
            foreach (bool cesar in new[] { false, true })
                for (int weapon = 0; weapon < 3; weapon++)
                {
                    VerifyWeapon(weapon, cesar);
                    Clear();
                }
            VerifyShield();
            Clear();
            VerifyActualDamageAndDeath();
            Clear();
            VerifySeveralEnemies();
            Clear();
            VerifyFollowPauseAndReuse();
            Clear();
            VerifyLaneRecycling();
            Clear();
            VerifyBoundedBurst();
            Clear();
            Check(Active().Length == 0, "all numbers return after isolated checks");
            return "DAMAGE_NUMBERS_OK: " + assertions +
                " assertions; all six weapon definitions and real projectile paths, applied damage, shield blocks, lethal hits, " +
                "piercing and splash deduplication, target motion/destruction, pause, bounded bursts and pool reuse; no profile or ammunition changes.";
        }
        finally
        {
            Clear();
            foreach (var go in fixtures) if (go != null) Object.DestroyImmediate(go);
            fixtures.Clear();
            foreach (var resource in resources) if (resource != null) Object.DestroyImmediate(resource);
            resources.Clear();
            enemySprite = null;
            Time.timeScale = scale;
        }
    }

    private static void BuildFixtures()
    {
        var texture = new Texture2D(4, 8, TextureFormat.RGBA32, false);
        enemySprite = Sprite.Create(texture, new Rect(0, 0, 4, 8), Vector2.one * .5f, 8f);
        resources.Add(enemySprite);
        resources.Add(texture);
        var go = new GameObject("Damage number verification camera");
        fixtures.Add(go);
        go.tag = "MainCamera";
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.transform.position = new Vector3(Origin.x, Origin.y, -10f);
    }

    private static void VerifyWeapon(int weapon, bool cesar)
    {
        string name = (cesar ? "Cesar " : "Marco ") + CharacterWeapons.State(weapon, cesar);
        WeaponDefinition definition = WeaponDefinition.ForCharacter(weapon, cesar);
        Check(definition != null, name + " has its real weapon definition");
        var enemy = Enemy(Origin + Vector2.right * .5f, 100);
        var shot = Fire(Origin, Vector2.right, definition, weapon, cesar);
        for (int step = 0; step < 30 && enemy.CurrentHealth == 100; step++) Step(shot);
        int damage = definition.DamageAt(0);
        Check(enemy.CurrentHealth == 100 - damage, name + " projectile actually damages the target");
        Check(Active().Length == 1, name + " impact displays exactly one number");
        var number = Active()[0];
        Check(Label(number).text == damage.ToString() && Get<EnemyHealth>(number, "target") == enemy,
            name + " shows its applied damage above the hit enemy");
        var text = Label(number);
        Check(text.font != null && text.fontSize <= 30 && text.fontSize >= 16 && !text.raycastTarget,
            name + " number is modest, readable and cannot intercept controls");
        Vector3 anchor = Get<Vector3>(number, "worldAnchor");
        Check(anchor.y >= enemy.GetComponent<SpriteRenderer>().bounds.max.y - .005f && Mathf.Abs(anchor.x - enemy.transform.position.x) < .1f,
            name + " anchor is above enemy art rather than at the projectile contact");
        var canvas = number.GetComponentInParent<Canvas>();
        Check(canvas != null && canvas.GetComponent<GraphicRaycaster>() == null && canvas.sortingOrder > 32,
            name + " number renders above impact effects without a blocking UI raycaster");
        for (int step = 0; step < 3; step++) Step(shot);
        Check(enemy.CurrentHealth == 100 - damage && Active().Length == 1,
            name + " spent or piercing shot does not repeat the same victim's number");
    }

    private static void VerifyShield()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy3_Base.prefab");
        Check(prefab != null, "actual shield prefab is available");
        var go = Object.Instantiate(prefab, Origin + Vector2.right * 2f, Quaternion.identity);
        created.Add(go);
        Object.DestroyImmediate(go.GetComponent<EnemyLootDrop>());
        go.GetComponent<BasicEnemy>().enabled = false;
        var animator = go.GetComponent<Animator>();
        animator.Play("Enemy3_Idle", 0, 0f);
        animator.Update(0f);
        animator.enabled = false;
        go.GetComponent<SpriteRenderer>().flipX = false;
        var health = go.GetComponent<EnemyHealth>();
        Physics2D.SyncTransforms();
        Bounds bounds = health.GetComponent<Collider2D>().bounds;
        var shot = Fire((Vector2)bounds.center - Vector2.right * (bounds.extents.x + .1f), Vector2.right,
            WeaponDefinition.At(0), 0, false);
        Step(shot);
        Check(health.CurrentHealth == health.MaxHealth && Active().Length == 0,
            "actual frontal pistol impact blocked by shield creates no damage number");
        health.TakeExplosionDamage(10);
        Check(health.CurrentHealth == health.MaxHealth - 10 && Active().Length == 1 && Label(Active()[0]).text == "10",
            "shield-bypassing blast shows only health damage");
    }

    private static void VerifyActualDamageAndDeath()
    {
        var enemy = Enemy(Origin, 3);
        int initial = All().Length;
        enemy.TakeDamage(0);
        enemy.TakeDamage(-12);
        Check(Active().Length == 0 && All().Length == initial, "nonpositive damage creates no number or pooled allocation");
        bool seenBeforeDeath = false;
        enemy.Died += _ => seenBeforeDeath = Active().Length == 1 && Label(Active()[0]).text == "3";
        enemy.TakeDamage(100);
        Check(enemy.IsDead && enemy.CurrentHealth == 0 && seenBeforeDeath,
            "overkill shows three actual health lost before death subscribers run");
        var number = Active()[0];
        Vector3 anchor = Get<Vector3>(number, "worldAnchor");
        enemy.transform.position += Vector3.down * 4f;
        Animate(number);
        Check(number.gameObject.activeSelf && Get<Vector3>(number, "worldAnchor") == anchor,
            "lethal number remains at impact height while dead enemy falls");
        enemy.TakeDamage(10);
        enemy.TakeExplosionDamage(10);
        Check(Active().Length == 1 && Label(number).text == "3", "later hits on corpse produce no duplicate number");
        Object.DestroyImmediate(enemy.gameObject);
        Animate(number);
        Check(number.gameObject.activeSelf && Get<Vector3>(number, "worldAnchor") == anchor,
            "number survives immediate enemy destruction until its own lifetime finishes");
        Expire();
        Check(Active().Length == 0, "lethal number returns to pool without a live target");
    }

    private static void VerifySeveralEnemies()
    {
        var a = Enemy(Origin + Vector2.right * .5f, 100);
        var b = Enemy(Origin + Vector2.right * 1.1f, 100);
        DuplicateHitbox(a);
        DuplicateHitbox(b);
        var sniper = WeaponDefinition.ForCharacter(1, true);
        var shot = Fire(Origin, Vector2.right, sniper, 1, true);
        for (int step = 0; step < 10 && b.CurrentHealth == 100; step++) Step(shot);
        Check(a.CurrentHealth == 100 - sniper.DamageAt(0) && b.CurrentHealth == 100 - sniper.DamageAt(0),
            "actual sniper pierces two enemies");
        Check(CountFor(a) == 1 && CountFor(b) == 1 && Active().Length == 2,
            "sniper displays one number per enemy despite extra hitboxes");
        Clear();

        foreach (bool cesar in new[] { false, true })
        {
            a = Enemy(Origin + Vector2.right * .5f, 100);
            b = Enemy(Origin + Vector2.right * .5f + Vector2.up * .8f, 100);
            DuplicateHitbox(a);
            DuplicateHitbox(b);
            var definition = WeaponDefinition.ForCharacter(2, cesar);
            shot = Fire(Origin, Vector2.right, definition, 2, cesar);
            for (int step = 0; step < 30 && a.CurrentHealth == 100; step++) Step(shot);
            int damage = definition.DamageAt(0);
            Check(a.CurrentHealth == 100 - damage && b.CurrentHealth == 100 - damage,
                (cesar ? "grenade" : "rocket") + " actual direct hit also damages nearby enemy");
            Check(CountFor(a) == 1 && CountFor(b) == 1 && Active().Length == 2,
                "direct plus splash is one displayed number per victim across duplicate hitboxes");
            Clear();
        }
    }

    private static void VerifyFollowPauseAndReuse()
    {
        var enemy = Enemy(Origin, 1000);
        enemy.TakeDamage(7);
        var number = Active()[0];
        Vector3 anchor = Get<Vector3>(number, "worldAnchor");
        Vector3 delta = new(.8f, .25f, 0f);
        enemy.transform.position += delta;
        Animate(number);
        Check(Vector3.Distance(Get<Vector3>(number, "worldAnchor"), anchor + delta) < .01f,
            "living victim keeps its number overhead as it moves");

        FieldInfo activeFlow = typeof(GameFlowController).GetField("active", StaticPrivate);
        object previousFlow = activeFlow.GetValue(null);
        var pauseOwner = Make("Inactive pause verification owner", Origin);
        pauseOwner.SetActive(false);
        var flow = pauseOwner.AddComponent<GameFlowController>();
        float scale = Time.timeScale;
        try
        {
            Set(flow, "paused", true);
            activeFlow.SetValue(null, flow);
            Time.timeScale = 0f;
            float age = Get<float>(number, "age");
            Vector3 position = number.transform.position;
            Animate(number);
            Check(Get<float>(number, "age") == age && number.transform.position == position && number.gameObject.activeSelf,
                "pause freezes number lifetime and animation without hiding it");
        }
        finally { activeFlow.SetValue(null, previousFlow); Time.timeScale = scale; }

        Set(number, "age", 1f);
        Animate(number);
        Check(!number.gameObject.activeSelf && Get<EnemyHealth>(number, "target") == null,
            "expiration returns pooled number and releases its enemy reference");
        int count = All().Length;
        enemy.TakeDamage(12);
        var reused = Active()[0];
        Check(reused == number && All().Length == count && Label(reused).text == "12" && Get<float>(reused, "age") == 0f,
            "reused number resets its value and lifetime without allocating a new object");
        Check(Label(reused).color.a * Label(reused).canvasRenderer.GetAlpha() > .95f && Get<EnemyHealth>(reused, "target") == enemy,
            "reused number restores opacity and the current victim");
    }

    private static void VerifyBoundedBurst()
    {
        var enemy = Enemy(Origin, 10000);
        for (int hit = 0; hit < 200; hit++) enemy.TakeDamage(1);
        Check(enemy.CurrentHealth == 9800, "full UI pool never drops or changes combat damage");
        Check(Active().Length == 96 && All().Length <= 96, "two hundred simultaneous hits keep at most ninety-six number objects");
        foreach (var number in Active())
            Check(Label(number).text == "1" && Get<EnemyHealth>(number, "target") == enemy,
                "recycled burst entries retain current value and victim");
        Expire();
        Check(Active().Length == 0, "burst expires completely");
        int count = All().Length;
        for (int hit = 1; hit <= 64; hit++)
        {
            enemy.TakeDamage(hit);
            Check(Active().Length == 1 && Label(Active()[0]).text == hit.ToString(), "sequential reuse never leaks an older value");
            Expire();
        }
        Check(All().Length == count, "sequential sustained fire reuses bounded pool without growth");
    }

    private static void VerifyLaneRecycling()
    {
        var enemy = Enemy(Origin, 1000);
        enemy.TakeDamage(1);
        var oldest = Active()[0];
        for (int hit = 0; hit < 4; hit++) enemy.TakeDamage(1);
        var lanes = new HashSet<int>();
        foreach (var number in Active()) lanes.Add(Get<int>(number, "lane"));
        Check(Active().Length == 5 && lanes.Count == 5, "rapid hits fan out across five readable positions");
        Set(oldest, "age", 1f);
        Animate(oldest);
        enemy.TakeDamage(1);
        lanes.Clear();
        foreach (var number in Active()) lanes.Add(Get<int>(number, "lane"));
        Check(Active().Length == 5 && lanes.Count == 5 && oldest.gameObject.activeSelf && Get<int>(oldest, "lane") == 0,
            "after oldest expires the next hit advances from latest lane instead of overlapping surviving text");
    }

    private static GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private static EnemyHealth Enemy(Vector2 position, int health)
    {
        var go = Make("Damage number verification enemy", position);
        go.SetActive(false);
        go.AddComponent<SpriteRenderer>().sprite = enemySprite;
        go.AddComponent<BoxCollider2D>().size = new Vector2(.4f, .8f);
        var enemy = go.AddComponent<EnemyHealth>();
        Set(enemy, "maxHealth", health);
        go.SetActive(true);
        return enemy;
    }

    private static void DuplicateHitbox(EnemyHealth enemy)
    {
        var go = new GameObject("Duplicate verification hitbox");
        go.transform.SetParent(enemy.transform, false);
        go.AddComponent<BoxCollider2D>().size = new Vector2(.3f, .6f);
        go.GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private static CombatProjectile Fire(Vector2 origin, Vector2 direction, WeaponDefinition definition, int weapon, bool cesar)
    {
        var before = new HashSet<CombatProjectile>(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
        Check(CombatProjectile.TrySpawn(origin, direction, definition.DamageAt(0), definition.ProjectileSpeed,
            definition.ProjectileRadius, definition.Range, null, weapon, definition.TargetsAt(0), cesar && weapon == 2),
            "real projectile route accepts verification shot without player inventory");
        foreach (var shot in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
            if (!before.Contains(shot))
            {
                created.Add(shot.gameObject);
                shot.enabled = false;
                var grenade = shot.GetComponent<GrenadeProjectile>();
                if (grenade != null) grenade.enabled = false;
                return shot;
            }
        throw new Exception("Damage number verification projectile was not found.");
    }

    private static void Step(CombatProjectile shot)
    {
        Physics2D.SyncTransforms();
        var grenade = shot != null ? shot.GetComponent<GrenadeProjectile>() : null;
        if (grenade != null) Call(grenade, "FixedUpdate");
        else if (shot != null) Call(shot, "FixedUpdate");
    }

    private static DamageNumber[] Active() => Object.FindObjectsByType<DamageNumber>(FindObjectsSortMode.None);
    private static DamageNumber[] All() => Object.FindObjectsByType<DamageNumber>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    private static Text Label(DamageNumber number)
    {
        var labels = number.GetComponentsInChildren<Text>(true);
        Check(labels.Length == 1, "number has one text label");
        return labels[0];
    }
    private static int CountFor(EnemyHealth enemy)
    {
        int count = 0;
        foreach (var number in Active()) if (Get<EnemyHealth>(number, "target") == enemy) count++;
        return count;
    }
    private static T Get<T>(object instance, string field) => (T)instance.GetType().GetField(field, Private).GetValue(instance);
    private static void Set(object instance, string field, object value) => instance.GetType().GetField(field, Private).SetValue(instance, value);
    private static void Call(object instance, string method) => instance.GetType().GetMethod(method, Private).Invoke(instance, null);
    private static void Animate(DamageNumber number) => Call(number, "LateUpdate");
    private static void Expire()
    {
        foreach (var number in Active()) { Set(number, "age", 1f); Animate(number); }
    }
    private static void Clear()
    {
        foreach (var number in Active()) Call(number, "Release");
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        foreach (var effect in Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None))
        { Set(effect, "age", 1f); Call(effect, "Update"); }
        foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None)) Call(effect, "Devolver");
        foreach (var effect in Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None))
        { Set(effect, "fadeAge", 1f); Call(effect, "Update"); }
        Physics2D.SyncTransforms();
    }
    private static void Check(bool condition, string label)
    {
        assertions++;
        if (!condition) throw new Exception("Damage number check failed: " + label);
    }
}
