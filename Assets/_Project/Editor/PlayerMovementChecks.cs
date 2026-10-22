using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Verifies entrance/end limits against the actual movement component.</summary>
public static class PlayerMovementChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    public static string RunRuntime()
    {
        if (!EditorApplication.isPlaying) throw new Exception("Enter Play Mode first.");
        var player = new GameObject("Movement verification Marco");
        player.SetActive(false);
        var body = player.AddComponent<Rigidbody2D>();
        body.gravityScale = 0f;
        var movement = player.AddComponent<PlayerMovement>();
        player.SetActive(true);
        try
        {
            const float entrance = -6.72f;
            Set(movement, "minHorizontalPosition", entrance);
            Set(movement, "maxHorizontalPosition", 117f);
            body.position = new Vector2(entrance, -1.88f);
            body.linearVelocity = new Vector2(-20f, 2f);
            Step(movement, -1f);
            Check(Mathf.Approximately(body.position.x, entrance) && body.linearVelocity.x == 0f,
                "Holding left at the entrance cannot move behind the sign");
            Check(body.linearVelocity.y == 2f, "Boundary preserves vertical movement");

            body.position = new Vector2(entrance + 0.001f, -1.88f);
            Step(movement, -1f);
            Check(Mathf.Approximately(body.position.x, entrance) && body.linearVelocity.x == 0f,
                "A step that would cross the entrance stops exactly at the boundary");

            body.position = new Vector2(entrance - 2f, -1.88f);
            Step(movement, 0f);
            Check(Mathf.Approximately(body.position.x, entrance),
                "An external displacement behind the entrance is corrected");
            Step(movement, 1f);
            Check(body.linearVelocity.x > 0f, "Player can leave the entrance toward the street");

            body.position = new Vector2(10f, -1.88f);
            Step(movement, -1f);
            Check(body.linearVelocity.x < 0f && body.position.x == 10f,
                "Player can walk left while inside the playable street");

            body.position = new Vector2(116.999f, -1.88f);
            Step(movement, 1f);
            Check(body.position.x == 117f && body.linearVelocity.x == 0f,
                "The existing right boundary still blocks overshoot");
            Step(movement, -1f);
            Check(body.linearVelocity.x < 0f, "Player can walk back from the right boundary");

            movement.SetMovementLocked(true);
            Step(movement, -1f);
            Check(body.linearVelocity == Vector2.zero, "Intro/defeat movement locks still stop the body");
            string result = "PLAYER_MOVEMENT_CHECKS_OK: entrance, overshoot, external displacement, both directions, end limit and input lock.";
            Debug.Log(result);
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(player); }
    }

    private static void Step(PlayerMovement movement, float input)
    {
        Set(movement, "horizontal", input);
        typeof(PlayerMovement).GetMethod("FixedUpdate", Private).Invoke(movement, null);
    }

    private static void Set(PlayerMovement movement, string field, float value) =>
        typeof(PlayerMovement).GetField(field, Private).SetValue(movement, value);

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Movement check failed: " + message);
    }
}
