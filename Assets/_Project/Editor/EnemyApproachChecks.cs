using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Checks entrance-bound attacks with disposable players, enemies and inventory.</summary>
public static class EnemyApproachChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Lindavista/Verify enemy approach attacks (Play Mode)")]
    public static void Run() => Debug.Log(RunRuntime());

    public static string RunRuntime()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded || GameFlowController.IsPaused)
            throw new InvalidOperationException("Run in active, unpaused Play Mode.");
        var created = new List<GameObject>();
        var previousEffects = new HashSet<ShotEffect>(Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None));
        int assertions = 0;
        string path = Path.Combine("Temp", "EnemyApproach-" + Guid.NewGuid().ToString("N") + ".json");

        void Check(bool condition, string message)
        {
            assertions++;
            if (!condition) throw new Exception("Enemy approach: " + message);
        }
        void Set(BasicEnemy enemy, string field, object value) =>
            typeof(BasicEnemy).GetField(field, Private).SetValue(enemy, value);
        bool CanHit(BasicEnemy enemy, PlayerActions player) =>
            (bool)typeof(BasicEnemy).GetMethod("CanHit", Private).Invoke(enemy, new object[] { player });
        IEnumerator Attack(BasicEnemy enemy, PlayerActions player) =>
            (IEnumerator)typeof(BasicEnemy).GetMethod("PerformAttack", Private).Invoke(enemy, new object[] { player });

        try
        {
            using (ProgressionService.UseVerificationRepository(new ProgressionRepository(path, true)))
            {
                var playerObject = new GameObject("Marco approach verification player");
                created.Add(playerObject);
                playerObject.AddComponent<SpriteRenderer>().sprite = ProgressionVisuals.Projectile;
                playerObject.AddComponent<BoxCollider2D>().size = Vector2.one;
                var player = playerObject.AddComponent<PlayerActions>();
                player.Health.Initialize(100, 0f);
                var center = new Vector3(12000f, 12000f, 0f);

                for (int type = 1; type <= 4; type++)
                foreach (float forward in new[] { -1f, 1f })
                foreach (bool transformTarget in new[] { false, true })
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                        "Assets/_Project/Prefabs/Enemy" + type + "_Base.prefab");
                    var enemyObject = Object.Instantiate(prefab, center, Quaternion.identity);
                    created.Add(enemyObject);
                    var enemy = enemyObject.GetComponent<BasicEnemy>();
                    enemy.enabled = false;
                    var renderer = enemyObject.GetComponent<SpriteRenderer>();
                    var animator = enemyObject.GetComponent<Animator>();
                    if (transformTarget)
                    {
                        var entrance = new GameObject("Approach verification entrance");
                        created.Add(entrance);
                        entrance.transform.position = center + Vector3.right * (forward * 50f);
                        Set(enemy, "entranceTarget", entrance.transform);
                    }
                    else enemy.SetEntranceTargetX(center.x + forward * 50f);
                    Set(enemy, "target", player);
                    Set(enemy, "nextTargetSearch", float.PositiveInfinity);
                    Set(enemy, "nextAttack", float.NegativeInfinity);
                    float y = renderer.bounds.center.y;
                    playerObject.transform.position = new Vector3(center.x + forward, y, 0f);
                    Check(CanHit(enemy, player), "front player is attackable, enemy " + type);

                    // Cross the enemy during the attack preparation, before damage is applied.
                    var pending = Attack(enemy, player);
                    Check(pending.MoveNext(), "attack has a preparation phase");
                    playerObject.transform.position = new Vector3(center.x - forward, y, 0f);
                    int health = player.CurrentHealth;
                    int effects = Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Length;
                    Check(!CanHit(enemy, player), "passed player is outside the attack direction");
                    Check(!pending.MoveNext() && player.CurrentHealth == health &&
                        Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Length == effects,
                        "crossing cancels melee/pistol/SMG damage and shot effects");
                    typeof(BasicEnemy).GetMethod("FireAt", Private).Invoke(enemy, new object[] { player });
                    Check(player.CurrentHealth == health &&
                        Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Length == effects,
                        "emission cannot bypass the direction check");

                    float before = enemy.transform.position.x;
                    typeof(BasicEnemy).GetMethod("Update", Private).Invoke(enemy, null);
                    Check((enemy.transform.position.x - before) * forward > 0f &&
                        renderer.flipX == (forward > 0f) && animator.GetBool("IsMoving"),
                        "passed enemy keeps walking toward the entrance without turning back");
                    Check(float.IsNegativeInfinity((float)typeof(BasicEnemy).GetField("nextAttack", Private).GetValue(enemy)),
                        "passed enemy never schedules a new attack");

                    if (type == 2 || type == 4)
                    {
                        // Front-facing gunfire still causes real physics damage.
                        enemy.transform.position = center;
                        playerObject.transform.position = new Vector3(center.x + forward, y, 0f);
                        Physics2D.SyncTransforms();
                        var burst = Attack(enemy, player);
                        Check(burst.MoveNext(), "front ranged attack prepares");
                        bool more = burst.MoveNext();
                        Check(player.CurrentHealth < health, "front ranged attack still hits");
                        if (type == 4)
                        {
                            Check(more, "SMG has another shot pending");
                            health = player.CurrentHealth;
                            effects = Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Length;
                            playerObject.transform.position = new Vector3(center.x - forward, y, 0f);
                            Check(!burst.MoveNext() && player.CurrentHealth == health &&
                                Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None).Length == effects,
                                "crossing cancels the remaining SMG burst");
                        }
                    }
                    Object.DestroyImmediate(enemyObject);
                }

                // Testing enemies without an entrance keep their normal pursuit behavior.
                var pursuer = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/_Project/Prefabs/Enemy2_Base.prefab"), center, Quaternion.identity);
                created.Add(pursuer);
                var behavior = pursuer.GetComponent<BasicEnemy>();
                behavior.enabled = false;
                foreach (float side in new[] { -1f, 1f })
                {
                    playerObject.transform.position = center + Vector3.right * side;
                    Check(CanHit(behavior, player), "non-objective enemy can still pursue either side");
                }
            }
            return "ENEMY_APPROACH_CHECKS_OK: " + assertions +
                " assertions; four enemy types, both entrance directions, fixed/Transform objectives, " +
                "crossing during windup and SMG bursts, real front damage and continued advance.";
        }
        finally
        {
            foreach (var go in created) if (go != null) Object.DestroyImmediate(go);
            foreach (var effect in Object.FindObjectsByType<ShotEffect>(FindObjectsSortMode.None))
                if (!previousEffects.Contains(effect)) Object.DestroyImmediate(effect.gameObject);
            File.Delete(path);
            File.Delete(path + ".bak");
            File.Delete(path + ".tmp");
        }
    }
}
