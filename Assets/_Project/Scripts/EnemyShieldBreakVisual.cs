using UnityEngine;

/// <summary>Drops shield fragments independently of the actor's movement and hitbox.</summary>
[DisallowMultipleComponent]
public sealed class EnemyShieldBreakVisual : MonoBehaviour
{
    private EnemyHealth health;
    private Animator animator;
    private SpriteRenderer body;
    private EnemyShieldVisualLibrary library;
    private Sprite[] fragments;
    private Transform[] pieces;
    private Vector3[] starts;
    private float age;
    private float facing;
    public bool HasDroppedShield { get; private set; }
    public GameObject DroppedShield { get; private set; }

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        animator = GetComponent<Animator>();
        body = GetComponent<SpriteRenderer>();
        library = Resources.Load<EnemyShieldVisualLibrary>("Enemy3UnshieldedVisuals");
        health.ShieldBroken += DropShield;
        enabled = false;
    }

    private void DropShield()
    {
        if (HasDroppedShield) return;
        HasDroppedShield = true;
        if (library == null || library.controller == null || library.brokenShield == null)
        {
            Debug.LogWarning("Enemy3UnshieldedVisuals is missing its animations or shield.", this);
            return;
        }

        // Override only sprite clips: world scale, feet, collider and movement stay intact.
        Bounds previousBounds = body.bounds;
        bool moving = animator.GetBool("IsMoving");
        animator.runtimeAnimatorController = library.controller;
        animator.Play(moving ? "Enemy3_Walk" : "Enemy3_Idle", 0, 0f);
        animator.Update(0f);

        facing = body.flipX ? 1f : -1f;
        DroppedShield = new GameObject("Enemy3 dropped broken shield");
        DroppedShield.transform.position = previousBounds.center +
            new Vector3(facing * previousBounds.size.x * .3f, -.1f, 0f);
        Sprite shield = library.brokenShield;
        float scale = previousBounds.size.y * .64f / shield.bounds.size.y;
        DroppedShield.transform.localScale = Vector3.one * scale;
        fragments = new Sprite[2];
        pieces = new Transform[2];
        starts = new Vector3[2];
        for (int i = 0; i < 2; i++)
        {
            Rect rect = shield.rect;
            rect.height *= .5f;
            rect.y += i * rect.height;
            fragments[i] = Sprite.Create(shield.texture, rect, new Vector2(.5f, .5f),
                shield.pixelsPerUnit);
            var piece = new GameObject(i == 0 ? "Lower shield fragment" : "Upper shield fragment");
            pieces[i] = piece.transform;
            pieces[i].SetParent(DroppedShield.transform, false);
            starts[i] = new Vector3(0f, (i == 0 ? -.25f : .25f) * shield.bounds.size.y, 0f);
            pieces[i].localPosition = starts[i];
            var renderer = piece.AddComponent<SpriteRenderer>();
            renderer.sprite = fragments[i];
            renderer.sharedMaterial = body.sharedMaterial;
            renderer.sortingLayerID = body.sortingLayerID;
            renderer.sortingOrder = body.sortingOrder + 1;
            renderer.flipX = body.flipX;
        }
        enabled = true;
    }

    private void Update()
    {
        if (DroppedShield == null) { enabled = false; return; }
        if (GameFlowController.IsPaused || PistolUpgradeShop.IsOpen) return;
        age += Time.deltaTime;
        const float lifetime = 1.05f;
        if (age >= lifetime || GameFlowController.IsMatchEnded)
        {
            ClearFragments();
            enabled = false;
            return;
        }
        float inverseScale = 1f / DroppedShield.transform.localScale.x;
        for (int i = 0; i < pieces.Length; i++)
        {
            float spread = i == 0 ? .45f : -.35f;
            pieces[i].localPosition = starts[i] + new Vector3(facing * spread * age,
                (i == 0 ? .3f : .9f) * age - 4.5f * age * age, 0f) * inverseScale;
            pieces[i].localRotation = Quaternion.Euler(0f, 0f, facing * (i == 0 ? 90f : -120f) * age);
            var renderer = pieces[i].GetComponent<SpriteRenderer>();
            renderer.color = new Color(1f, 1f, 1f, 1f - Mathf.InverseLerp(.65f, lifetime, age));
        }
    }

    private void ClearFragments()
    {
        if (DroppedShield != null) Destroy(DroppedShield);
        DroppedShield = null;
        if (fragments != null)
            foreach (Sprite fragment in fragments) if (fragment != null) Destroy(fragment);
        fragments = null;
    }

    private void OnDestroy()
    {
        if (health != null) health.ShieldBroken -= DropShield;
        ClearFragments();
    }
}
