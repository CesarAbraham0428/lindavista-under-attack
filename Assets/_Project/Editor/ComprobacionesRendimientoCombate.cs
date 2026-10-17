using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Regresiones de buffers físicos y reutilización de partículas en Play Mode.</summary>
public static class ComprobacionesRendimientoCombate
{
    private const BindingFlags privado = BindingFlags.Instance | BindingFlags.NonPublic;

    [MenuItem("Lindavista/Comprobar optimización de combate (Play Mode)")]
    public static void Ejecutar()
    {
        if (!Application.isPlaying || GameFlowController.IsMatchEnded)
            throw new Exception("Inicia una partida activa antes de comprobar el combate.");

        var creados = new List<GameObject>();
        bool incluirTriggers = Physics2D.queriesHitTriggers;
        ShotEffect efecto = null;
        try
        {
            Vector2 origen = new(4000f, 4000f);
            Physics2D.queriesHitTriggers = true;
            for (int indice = 0; indice < 80; indice++)
            {
                var objeto = new GameObject("Trigger de verificación de combate");
                creados.Add(objeto);
                objeto.transform.position = origen + Vector2.right * (1f + indice * 0.2f);
                var collider = objeto.AddComponent<BoxCollider2D>();
                collider.size = Vector2.one * 0.1f;
                collider.isTrigger = true;
            }

            var ignorado = new GameObject("Capa Ignore Raycast de verificación");
            creados.Add(ignorado);
            ignorado.layer = 2;
            ignorado.transform.position = origen + Vector2.right * 18f;
            ignorado.AddComponent<BoxCollider2D>();

            var objetivo = new GameObject("Objetivo de verificación de buffers");
            creados.Add(objetivo);
            objetivo.SetActive(false);
            objetivo.transform.position = origen + Vector2.right * 20f;
            objetivo.AddComponent<BoxCollider2D>().size = Vector2.one * 0.25f;
            var salud = objetivo.AddComponent<EnemyHealth>();
            typeof(EnemyHealth).GetField("maxHealth", privado).SetValue(salud, 10);
            objetivo.SetActive(true);
            Physics2D.SyncTransforms();

            var anteriores = new HashSet<CombatProjectile>(
                Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None));
            Exigir(CombatProjectile.TrySpawn(origen, Vector2.right, 3,
                30f / Mathf.Max(0.0001f, Time.fixedDeltaTime), 0.07f, 30f, null),
                "El disparo de verificación puede emitirse.");
            CombatProjectile disparo = null;
            foreach (var candidato in Object.FindObjectsByType<CombatProjectile>(FindObjectsSortMode.None))
                if (!anteriores.Contains(candidato)) { disparo = candidato; break; }
            Exigir(disparo != null, "Se encuentra el proyectil emitido.");
            creados.Add(disparo.gameObject);
            disparo.enabled = false;
            typeof(CombatProjectile).GetMethod("FixedUpdate", privado).Invoke(disparo, null);
            Exigir(salud.CurrentHealth == 7,
                "El buffer supera 80 contactos, conserva el enemigo lejano e ignora triggers y Ignore Raycast.");
            typeof(CombatProjectile).GetMethod("FixedUpdate", privado).Invoke(disparo, null);
            Exigir(salud.CurrentHealth == 7, "El proyectil consumido nunca repite el daño.");

            Sprite sprite = ShotVisual.GetSprite(3);
            var contador = typeof(ShotEffect).GetField("active", BindingFlags.Static | BindingFlags.NonPublic);
            int activosAnteriores = (int)contador.GetValue(null);
            efecto = ShotEffect.Create("Partícula de verificación", sprite, origen, Vector2.right,
                Color.red, 0.4f, 0.1f, 1f);
            Exigir(efecto != null, "Puede crearse una partícula.");
            efecto.transform.rotation = Quaternion.Euler(0f, 0f, 123f);
            typeof(ShotEffect).GetField("age", privado).SetValue(efecto, 2f);
            typeof(ShotEffect).GetMethod("Update", privado).Invoke(efecto, null);
            Exigir(!efecto.gameObject.activeSelf, "La partícula agotada se devuelve sin destruirse.");
            Exigir((int)contador.GetValue(null) == activosAnteriores,
                "Devolver una partícula libera su lugar en el límite de efectos.");
            ShotEffect reutilizado = ShotEffect.Create("Partícula reutilizada", sprite, origen + Vector2.up,
                Vector2.zero, Color.white, 0.2f, 0.1f, 1f);
            Exigir(ReferenceEquals(efecto, reutilizado), "La siguiente partícula reutiliza el mismo objeto.");
            Exigir(reutilizado.gameObject.activeSelf && reutilizado.transform.rotation == Quaternion.identity &&
                reutilizado.GetComponent<SpriteRenderer>().color == Color.white &&
                (float)typeof(ShotEffect).GetField("age", privado).GetValue(reutilizado) == 0f,
                "La reutilización reinicia activación, edad, orientación y color.");
            Exigir((int)contador.GetValue(null) == activosAnteriores + 1,
                "El contador incluye una sola vez la partícula reutilizada.");
            Exigir(ShotEffect.Create("Sin sprite", null, origen, Vector2.zero, Color.white, 1f, 1f, 1f) == null,
                "Una partícula sin sprite se rechaza sin provocar una excepción.");
            Debug.Log("RENDIMIENTO_COMBATE_OK: 80 contactos sin truncamiento, capas, daño único y reutilización de partículas verificados.");
        }
        finally
        {
            Physics2D.queriesHitTriggers = incluirTriggers;
            if (efecto != null) Object.DestroyImmediate(efecto.gameObject);
            foreach (var objeto in creados) if (objeto != null) Object.DestroyImmediate(objeto);
            Physics2D.SyncTransforms();
        }
    }

    private static void Exigir(bool condicion, string mensaje)
    {
        if (!condicion) throw new Exception("Comprobación de combate: " + mensaje);
    }
}
