using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Isolated contact, shield and tactical positioning checks using real physics and enemy prefabs.</summary>
public static class CombatFeedbackAndAIChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly List<GameObject> created = new();
    private static HashSet<GameObject> existingEffects;
    private static int assertions;
    private static readonly Vector2 Center = new(20000f, 20000f);

    [MenuItem("Tools/Lindavista/Verify impact feedback and tactical enemies (Play Mode)")]
    public static void Run() => Debug.Log(RunRuntime());

    private static void Check(bool valid, string label)
    {
        assertions++;
        if (!valid) throw new Exception("Combat feedback/AI: " + label);
    }
    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, Private).Invoke(target, args);
    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, Private).SetValue(target, value);
    private static T Get<T>(object target, string name) =>
        (T)target.GetType().GetField(name, Private).GetValue(target);

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen)
            throw new InvalidOperationException("Run in unpaused Play Mode with no result or shop open.");
        if (Object.FindObjectsByType<PlayerActions>(FindObjectsSortMode.None).Length > 0 ||
            Object.FindObjectsByType<BasicEnemy>(FindObjectsSortMode.None).Length > 0)
            throw new InvalidOperationException("Use an empty verification scene in Play Mode, not an active match.");
        assertions = 0;
        existingEffects = new HashSet<GameObject>(Effects());
        string path = Path.Combine(Application.temporaryCachePath, "CombatFeedback-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path, true)))
            {
                foreach (int limit in new[] { 2, 3, 4 })
                {
                    var victims = Enumerable.Range(1, 5).Select(i => HealthAt(Center + Vector2.right * i)).ToArray();
                    var duplicate = Make("duplicate sniper collider", Vector2.zero);
                    duplicate.transform.SetParent(victims[0].transform, false);
                    duplicate.AddComponent<BoxCollider2D>().isTrigger = true;
                    var shot = Fire(Center, 1, limit);
                    Step(shot);
                    for (int i = 0; i < victims.Length; i++)
                        Check(victims[i].CurrentHealth == (i < limit ? 7 : 10), "sniper respects target limit without duplicate damage");
                    Check(EffectsNamed("Enemy contact flash").Length == limit, "a readable flash on each pierced target");
                    Check(EffectsNamed("Sniper blood impact").Length == limit, "damage feedback per actual enemy hit");
                    Check(Get<bool>(shot, "spent"), "last allowed target retires sniper");
                    Clear();
                }
                var shield = HealthAt(Center + Vector2.right);
                shield.gameObject.AddComponent<SpriteRenderer>();
                Set(shield, "spriteRenderer", shield.GetComponent<SpriteRenderer>());
                Set(shield, "shieldBlocksFront", true);
                shield.ConfigureShieldOpening(1, 2f);
                var behind = HealthAt(Center + Vector2.right * 2f);
                var stopped = Fire(Center, 1, 4);
                Step(stopped);
                Check(shield.CurrentHealth == 10 && behind.CurrentHealth == 10 && Get<bool>(stopped, "spent"), "blocked sniper cannot pass a shield");
                Check(shield.IsShieldBroken && EffectsNamed("Shield contact flash").Length == 1, "breaking hit has shield feedback");
                Check(EffectsNamed("Sniper blood impact").Length == 0, "blocked hit does not fake injury");
                Object.DestroyImmediate(stopped.gameObject);
                var followUp = Fire(Center, 1, 2);
                Step(followUp);
                Check(shield.CurrentHealth == 7 && behind.CurrentHealth == 7, "next round pierces an exposed shield");
                Clear();

                var beforeWall = HealthAt(Center + Vector2.right);
                var obstacle = Make("sniper solid obstacle", Center + Vector2.right * 2f);
                obstacle.AddComponent<BoxCollider2D>().size = new Vector2(.2f, 8f);
                var afterWall = HealthAt(Center + Vector2.right * 3f);
                var wallShot = Fire(Center, 1, 4);
                Step(wallShot);
                Check(beforeWall.CurrentHealth == 7 && afterWall.CurrentHealth == 10 && Get<bool>(wallShot, "spent"),
                    "sniper penetration still stops at solid scenery");
                Clear();

                foreach (int weapon in new[] { 0, 1 })
                {
                    var enemy = HealthAt(Center + Vector2.right);
                    var shot = Fire(Center, weapon, 1);
                    Step(shot);
                    Check(enemy.CurrentHealth == 7 && Get<bool>(shot, "spent"), "ordinary bullet still damages once");
                    var flash = EffectsNamed("Enemy contact flash").Single();
                    Check(Vector2.Distance(flash.transform.position, Center + Vector2.right) < .5f, "flash occurs at contact, not world origin");
                    Check(flash.GetComponent<SpriteRenderer>().sortingOrder == 40, "contact flash is in front of character sprites");
                    Step(shot);
                    Check(enemy.CurrentHealth == 7, "spent round never doubles contact damage");
                    Clear();
                }

                var contact = HealthAt(Center + new Vector2(.4f, .1f), Vector2.one * 1.4f);
                var extra = Make("duplicate grenade collider", Vector2.zero);
                extra.transform.SetParent(contact.transform, false);
                extra.AddComponent<BoxCollider2D>().isTrigger = true;
                var grenade = FireGrenade(Center);
                Step(grenade);
                Check(contact.CurrentHealth == 7 && Get<bool>(grenade, "spent"), "contact grenade detonates once across duplicate hitboxes");
                Check(Effects().Count(go => go.GetComponent<RocketImpact>() != null) == 1, "contact grenade has a visible explosion");
                Step(grenade);
                Check(contact.CurrentHealth == 7, "contact plus splash cannot double grenade damage");
                Clear();

                var covered = HealthAt(Center + Vector2.right);
                var grenadeWall = Make("grenade solid obstacle", Center + Vector2.right * .5f);
                grenadeWall.AddComponent<BoxCollider2D>().size = new Vector2(.1f, 8f);
                var deflected = FireGrenade(Center);
                Set(deflected, "velocity", Vector2.right * 500f);
                Step(deflected);
                Check(covered.CurrentHealth == 10 && !Get<bool>(deflected, "spent"),
                    "wall blocks contact with an enemy behind it instead of detonating through cover");
                Clear();

                var missed = FireGrenade(Center);
                int steps = 0;
                while (!missed.HasLanded && steps++ < 200) Step(missed);
                Check(missed.HasLanded && !Get<bool>(missed, "spent"), "miss still lands without detonating immediately");
                Check(Mathf.Abs(missed.FuseRemaining - GrenadeProjectile.FuseDuration) < .001f, "miss retains floor fuse");
                steps = 0;
                while (!Get<bool>(missed, "spent") && steps++ < 100) Step(missed);
                Check(Get<bool>(missed, "spent"), "floor fuse still detonates after bounce");
                Clear();

                for (int type = 1; type <= 4; type++)
                foreach (float forward in new[] { -1f, 1f })
                {
                    var enemy = Enemy(type, Center);
                    var player = Player(Center);
                    float reach = type == 1 || type == 3 ? 1f : 4f;
                    float y = enemy.GetComponent<SpriteRenderer>().bounds.center.y;
                    player.transform.position = new Vector2(Center.x + forward * reach, y);
                    enemy.SetEntranceTargetX(Center.x + forward * 50f);
                    Set(enemy, "target", player);
                    Set(enemy, "nextTargetSearch", float.PositiveInfinity);
                    Set(enemy, "nextAttack", float.PositiveInfinity);
                    Sync();
                    Check((bool)Call(enemy, "CanHit", player), "front target is attackable for each enemy role");
                    Vector3 before = enemy.transform.position;
                    Call(enemy, "Update");
                    Check(enemy.transform.position == before && !enemy.GetComponent<Animator>().GetBool("IsMoving"), "tactical enemy holds effective combat range");
                    var attack = (IEnumerator)Call(enemy, "PerformAttack", player);
                    Check(attack.MoveNext() && Get<bool>(enemy, "attackInProgress"), "windup owns attack state");
                    player.transform.position = new Vector2(Center.x - forward, y);
                    int health = player.CurrentHealth;
                    Check(!attack.MoveNext() && player.CurrentHealth == health && !Get<bool>(enemy, "attackInProgress"), "crossing cancels pending attack and releases state");
                    Call(enemy, "Update");
                    Check((enemy.transform.position.x - before.x) * forward > 0f, "passed player does not prevent advance to entrance");
                    Clear();
                }

                var ranged = Enemy(4, Center);
                var victim = Player(Center + Vector2.left * 4f);
                victim.transform.position = new Vector2(victim.transform.position.x, ranged.GetComponent<SpriteRenderer>().bounds.center.y);
                ranged.SetEntranceTargetX(Center.x - 50f);
                var wall = Make("solid cover", ((Vector2)ranged.GetComponent<SpriteRenderer>().bounds.center + victim.AimOrigin) * .5f);
                var wallCollider = wall.AddComponent<BoxCollider2D>();
                wallCollider.size = new Vector2(.2f, 8f);
                Sync();
                Check(!(bool)Call(ranged, "CanHit", victim), "solid cover prevents attack windup");
                var burst = (IEnumerator)Call(ranged, "PerformAttack", victim);
                burst.MoveNext();
                int healthBefore = victim.CurrentHealth;
                Check(!burst.MoveNext() && victim.CurrentHealth == healthBefore, "cover cancels an already prepared burst");
                Vector3 stoppedAt = (Vector3)Call(ranged, "ConstrainMovement", ranged.transform.position + Vector3.left * 10f);
                Check(stoppedAt.x > wall.transform.position.x, "body sweep cannot walk through solid cover");
                wallCollider.isTrigger = true;
                Sync();
                Check((bool)Call(ranged, "CanHit", victim), "decorative trigger does not block line of sight");
                burst = (IEnumerator)Call(ranged, "PerformAttack", victim);
                Check(burst.MoveNext(), "clear ranged attack prepares");
                burst.MoveNext();
                Check(victim.CurrentHealth < healthBefore, "tactical gunner still deals real damage with clear sight");
                ((IDisposable)burst).Dispose();
                Clear();

                foreach (float forward in new[] { -1f, 1f })
                {
                    var follower = Enemy(1, Center);
                    var front = Enemy(1, Center + Vector2.right * (forward * 2f));
                    Call(front, "OnEnable");
                    Vector3 step = (Vector3)Call(follower, "ConstrainMovement", follower.transform.position + Vector3.right * (forward * 5f));
                    Check(Mathf.Abs(step.x - Center.x) <= .751f && (step.x - Center.x) * forward >= 0f, "allies leave spacing without stepping backward");
                    Object.DestroyImmediate(front.gameObject);
                    var gunner = Enemy(2, Center + Vector2.right * (forward * .8f));
                    Call(gunner, "OnEnable");
                    step = (Vector3)Call(follower, "ConstrainMovement", follower.transform.position + Vector3.right * forward);
                    Check(Mathf.Abs(step.x - Center.x - forward) < .001f, "melee can overtake a ranged ally instead of deadlocking");
                    Clear();
                }
            }
            return "COMBAT_FEEDBACK_AI_OK: " + assertions + " assertions; per-target effects, shield stops, grenade contact/fuse, tactical ranges, cover, spacing, crossing and overtaking.";
        }
        finally
        {
            Clear(); existingEffects = null;
            File.Delete(path); File.Delete(path + ".bak"); File.Delete(path + ".tmp");
        }
    }

    private static GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }
    private static EnemyHealth HealthAt(Vector2 position, Vector2? size = null)
    {
        var go = Make("feedback enemy", position);
        go.SetActive(false);
        go.AddComponent<BoxCollider2D>().size = size ?? Vector2.one * .35f;
        var health = go.AddComponent<EnemyHealth>();
        Set(health, "maxHealth", 10);
        go.SetActive(true);
        return health;
    }
    private static BasicEnemy Enemy(int type, Vector2 position)
    {
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Enemy" + type + "_Base.prefab"), position, Quaternion.identity);
        created.Add(go);
        Object.DestroyImmediate(go.GetComponent<EnemyLootDrop>());
        var enemy = go.GetComponent<BasicEnemy>();
        enemy.enabled = false;
        enemy.ConfigureTactics();
        return enemy;
    }
    private static PlayerActions Player(Vector2 position)
    {
        var go = Make("Marco AI verification", position);
        go.AddComponent<SpriteRenderer>().sprite = ProgressionVisuals.Projectile;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .5f;
        var player = go.AddComponent<PlayerActions>();
        player.Health.Initialize(100, 0f);
        typeof(PlayerActions).Assembly.GetType("ObjetivosCombate").GetField("jugadores", BindingFlags.NonPublic | BindingFlags.Static).SetValue(null, null);
        return player;
    }
    private static CombatProjectile Fire(Vector2 origin, int weapon, int limit)
    {
        var previous = new HashSet<CombatProjectile>(Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
        Check(CombatProjectile.TrySpawn(origin, Vector2.right, 3, 500f, .07f, 20f, null, weapon, limit), "test bullet emits");
        var shot = Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None).First(s => !previous.Contains(s));
        created.Add(shot.gameObject);
        shot.enabled = false;
        return shot;
    }
    private static GrenadeProjectile FireGrenade(Vector2 origin)
    {
        Check(CombatProjectile.TrySpawn(origin, Vector2.right, 3, 10f, .2f, 18f, null, 2, 1, true), "test grenade emits");
        var shot = Object.FindFirstObjectByType<GrenadeProjectile>();
        created.Add(shot.gameObject); shot.enabled = false;
        return shot;
    }
    private static void Sync() => Physics2D.SyncTransforms();
    private static void Step(MonoBehaviour component) { Sync(); Call(component, "FixedUpdate"); }
    private static GameObject[] Effects() => Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Select(x => x.gameObject)
        .Concat(Object.FindObjectsByType<RocketImpact>(FindObjectsSortMode.None).Select(x => x.gameObject))
        .Concat(Object.FindObjectsByType<ShotVisual>(FindObjectsSortMode.None).Select(x => x.gameObject))
        .Concat(Object.FindObjectsByType<SniperShotVisual>(FindObjectsSortMode.None).Select(x => x.gameObject)).ToArray();
    private static GameObject[] EffectsNamed(string name) => Effects().Where(x => x.name == name && !existingEffects.Contains(x)).ToArray();
    private static void Clear()
    {
        foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
        created.Clear();
        foreach (var effect in Effects()) if (existingEffects != null && !existingEffects.Contains(effect)) Object.DestroyImmediate(effect);
        Sync();
    }
}
