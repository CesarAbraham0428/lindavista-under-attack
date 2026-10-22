using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Inactive combat objects belong to their level, never to the saved profile.</summary>
internal sealed class SceneCombatPool<T> where T : Component
{
    private readonly Stack<T> available;
    private readonly string label;
    private readonly int capacity;
    private Transform root;

    internal SceneCombatPool(string label, int capacity)
    {
        this.label = label;
        this.capacity = Mathf.Max(0, capacity);
        available = new Stack<T>(this.capacity);
    }

    private void EnsureScene()
    {
        if (root != null && root.gameObject.scene == SceneManager.GetActiveScene()) return;
        available.Clear();
        root = new GameObject(label + " pool").transform;
    }

    // Rent inactive: callers reset all per-use state before enabling callbacks.
    internal T Rent()
    {
        EnsureScene();
        T item = null;
        while (available.Count > 0 && item == null) item = available.Pop();
        if (item != null) return item;
        return Create();
    }

    private T Create()
    {
        var go = new GameObject(label);
        go.SetActive(false);
        go.transform.SetParent(root, false);
        return go.AddComponent<T>();
    }

    internal void Return(T item)
    {
        item.gameObject.SetActive(false);
        // An additive scene change must not keep the old level's objects alive.
        if (root == null || item.gameObject.scene != root.gameObject.scene || available.Count >= capacity)
        {
            Object.Destroy(item.gameObject);
            return;
        }
        item.transform.SetParent(root, false);
        available.Push(item);
    }

    internal void Prewarm(int count, System.Action<T> prepare = null)
    {
        EnsureScene();
        count = Mathf.Clamp(count, 0, capacity);
        // Warm only the missing inactive objects. Repeated warmup (for example
        // when the boss arrives) must neither rent existing objects nor allocate
        // a temporary list or rebuild their visual components.
        while (available.Count < count)
        {
            T item = Create();
            prepare?.Invoke(item);
            available.Push(item);
        }
    }
}
