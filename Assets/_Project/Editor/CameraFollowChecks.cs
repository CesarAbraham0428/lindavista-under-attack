using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>Deterministic follow-response checks without a scene or player save.</summary>
public static class CameraFollowChecks
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Tools/Lindavista/Verify camera direction response")]
    public static void Verify() => Run();

    public static string Run()
    {
        GameObject owner = new GameObject("Camera response verification");
        owner.SetActive(false);
        CameraFollow2D camera = owner.AddComponent<CameraFollow2D>();
        try
        {
            float minimumFinal = 1f;
            float maximumFinal = 0f;
            foreach (int fps in new[] { 30, 60, 120 })
            {
                Reset(camera);
                float deltaTime = 1f / fps;
                for (int frame = 0; frame < fps; frame++)
                {
                    float before = Read<float>(camera, "currentScreenX");
                    Step(camera, -1f, deltaTime);
                    float after = Read<float>(camera, "currentScreenX");
                    Require(after >= before && (after - before) / deltaTime <= 0.801f,
                        "A sustained turn pans smoothly at a bounded speed (" + fps + " fps)");
                }
                float final = Read<float>(camera, "currentScreenX");
                Require(final > 0.575f && final <= 0.6f,
                    "Left framing is almost settled within one second (" + fps + " fps)");
                minimumFinal = Mathf.Min(minimumFinal, final);
                maximumFinal = Mathf.Max(maximumFinal, final);
            }
            Require(maximumFinal - minimumFinal < 0.015f, "Response is consistent across frame rates");

            Reset(camera);
            for (int frame = 0; frame < 120; frame++)
                Step(camera, frame % 2 == 0 ? -1f : 1f, 1f / 60f);
            Require(Mathf.Approximately(Read<float>(camera, "currentScreenX"), 0.2f),
                "Rapid left/right input does not shake the framing");

            // The pan is already moving left; a new turn should decelerate it,
            // preserving velocity instead of instantly snapping to a stopped pan.
            Write(camera, "currentScreenX", 0.35f);
            Write(camera, "framingDirection", -1);
            Write(camera, "framingVelocity", 0.5f);
            Write(camera, "pendingDirection", 1);
            Write(camera, "pendingDirectionTime", 0.08f);
            Step(camera, 1f, 0.001f);
            Require(Read<float>(camera, "framingVelocity") > 0.45f &&
                Read<float>(camera, "currentScreenX") > 0.35f,
                "Interrupting a pan preserves velocity and decelerates through the reversal");
            for (int frame = 0; frame < 120; frame++) Step(camera, 1f, 1f / 60f);
            Require(Read<float>(camera, "currentScreenX") < 0.201f,
                "Interrupted pan reaches the new composition");

            float pausedPosition = Read<float>(camera, "currentScreenX");
            float pausedVelocity = Read<float>(camera, "framingVelocity");
            Step(camera, -1f, 0f);
            Require(Read<float>(camera, "currentScreenX") == pausedPosition &&
                Read<float>(camera, "framingVelocity") == pausedVelocity,
                "A paused frame does not move the camera or invalidate pan velocity");

            ComprobarLimitesPantalla(camera, owner);

            string result = "CAMERA_FOLLOW_CHECKS_OK: quick framing at 30/60/120 fps, bounded pan speed, " +
                "input jitter, interrupted reversals, pause and narrow-screen entrance bounds verified.";
            Debug.Log(result);
            return result;
        }
        finally { UnityEngine.Object.DestroyImmediate(owner); }
    }

    private static void Reset(CameraFollow2D camera)
    {
        Write(camera, "currentScreenX", 0.2f);
        Write(camera, "framingDirection", 1);
        Write(camera, "framingVelocity", 0f);
        Write(camera, "pendingDirection", 0);
        Write(camera, "pendingDirectionTime", 0f);
    }

    private static void ComprobarLimitesPantalla(CameraFollow2D seguimiento, GameObject objeto)
    {
        var camara = objeto.AddComponent<Camera>();
        camara.orthographic = true;
        camara.orthographicSize = 6f;
        Write(seguimiento, "attachedCamera", camara);
        Write(seguimiento, "normalOrthographicSize", 6f);
        Write(seguimiento, "startCameraX", 0f);
        var minimo = typeof(CameraFollow2D).GetProperty("MinimoCamaraX", PrivateInstance);
        float bordeReferencia = -6f * 16f / 9f;
        foreach (float proporcion in new[] { 4f / 3f, 3f / 2f, 16f / 9f, 20f / 9f, 4f / 3f })
        {
            camara.aspect = proporcion;
            float limite = (float)minimo.GetValue(seguimiento);
            Require(limite <= 0f, "El ajuste conserva el inicio original en pantallas anchas");
            if (proporcion < 16f / 9f)
                Require(Mathf.Approximately(limite - 6f * proporcion, bordeReferencia),
                    "Una pantalla estrecha conserva el borde izquierdo de referencia 16:9");
            else
                Require(Mathf.Approximately(limite, 0f), "16:9 y panorámico conservan la cámara original");
            Require(Mathf.Approximately(Read<float>(seguimiento, "startCameraX"), 0f),
                "Cambiar la proporción no acumula desplazamiento en el origen");
        }

        camara.orthographicSize = 18f;
        Require(Mathf.Approximately((float)minimo.GetValue(seguimiento), 6f * (4f / 3f - 16f / 9f)),
            "El retorno de introducción usa el tamaño normal aunque el zoom siga ampliado");
        Write(seguimiento, "startCameraX", 10f);
        Require(Mathf.Approximately((float)minimo.GetValue(seguimiento), 10f + 6f * (4f / 3f - 16f / 9f)),
            "Una posición inicial distinta conserva su borde de referencia");
    }

    private static void Step(CameraFollow2D camera, float input, float deltaTime) =>
        typeof(CameraFollow2D).GetMethod("StepDirectionFraming", PrivateInstance)
            .Invoke(camera, new object[] { input, deltaTime });

    private static T Read<T>(CameraFollow2D camera, string field) =>
        (T)typeof(CameraFollow2D).GetField(field, PrivateInstance).GetValue(camera);

    private static void Write(CameraFollow2D camera, string field, object value) =>
        typeof(CameraFollow2D).GetField(field, PrivateInstance).SetValue(camera, value);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("Camera response check failed: " + message);
    }
}
