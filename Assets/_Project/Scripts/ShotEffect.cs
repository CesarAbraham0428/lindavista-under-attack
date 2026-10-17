using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Bounded, self-cleaning particles using the sprite material in both URP and built-in renderers.</summary>
public sealed class ShotEffect : MonoBehaviour
{
    private static int active;
    private const int limiteEfectos = 180;
    private static readonly Stack<ShotEffect> disponibles = new();
    private static Transform contenedor;
    private SpriteRenderer visual;
    private Vector2 velocity;
    private Color tint;
    private float age, duration, startSize, endSize;
    private bool enUso;

    public static ShotEffect Create(string label, Sprite sprite, Vector2 position, Vector2 velocity,
        Color color, float size, float endSize, float lifetime, int sortingOrder = 31)
    {
        if (active >= limiteEfectos || sprite == null || GameFlowController.IsMatchEnded) return null;
        ShotEffect effect = null;
        while (disponibles.Count > 0 && effect == null) effect = disponibles.Pop();
        if (effect == null)
        {
            var nuevo = new GameObject(label);
            nuevo.SetActive(false);
            effect = nuevo.AddComponent<ShotEffect>();
            effect.visual = nuevo.AddComponent<SpriteRenderer>();
        }
        var go = effect.gameObject;
        go.name = label;
        go.transform.SetParent(null, false);
        var escenaActual = SceneManager.GetActiveScene();
        if (go.scene != escenaActual) SceneManager.MoveGameObjectToScene(go, escenaActual);
        go.transform.SetPositionAndRotation(position, Quaternion.identity);
        effect.age = 0f;
        effect.enUso = true;
        active++;
        effect.visual.sprite = sprite;
        effect.visual.sortingOrder = sortingOrder;
        effect.visual.color = effect.tint = color;
        effect.velocity = velocity;
        effect.duration = Mathf.Max(0.001f, lifetime);
        float ancho = Mathf.Max(0.0001f, sprite.bounds.size.x);
        effect.startSize = size / ancho;
        effect.endSize = endSize / ancho;
        go.transform.localScale = Vector3.one * effect.startSize;
        go.SetActive(true);
        return effect;
    }

    private void Update()
    {
        age += Time.deltaTime;
        if (age >= duration || GameFlowController.IsMatchEnded) { Devolver(); return; }
        float t = age / duration;
        transform.position += (Vector3)(velocity * Time.deltaTime);
        transform.localScale = Vector3.one * Mathf.Lerp(startSize, endSize, t);
        visual.color = new Color(tint.r, tint.g, tint.b, tint.a * (1f - t));
    }

    private void Devolver()
    {
        if (!enUso) return;
        gameObject.SetActive(false);
        // El contenedor pertenece a la escena: cambiar de nivel libera también el pool.
        if (contenedor == null) contenedor = new GameObject("Efectos de disparo reutilizables").transform;
        transform.SetParent(contenedor, false);
        visual.sprite = null;
        if (disponibles.Count < limiteEfectos) disponibles.Push(this);
        else Destroy(gameObject);
    }

    private void OnDisable()
    {
        if (!enUso) return;
        enUso = false;
        active = Mathf.Max(0, active - 1);
    }

    internal static void Prewarm(int count)
    {
        var effects = new List<ShotEffect>(count);
        for (int i = 0; i < count; i++)
        {
            var effect = Create("Prepared shot effect", ShotVisual.GetSprite(3), Vector2.zero,
                Vector2.zero, Color.white, 0.1f, 0.1f, 1f);
            if (effect != null) effects.Add(effect);
        }
        foreach (var effect in effects) effect.Devolver();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCount()
    {
        active = 0;
        disponibles.Clear();
        contenedor = null;
    }
}

