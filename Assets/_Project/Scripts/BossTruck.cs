using UnityEngine;

/// <summary>Final encounter: approach, aim warning, dodgeable burst, recovery.</summary>
[RequireComponent(typeof(EnemyHealth), typeof(SpriteRenderer), typeof(Animator))]
public sealed class BossTruck : MonoBehaviour
{
    public enum CombatPhase { Entering, Recovery, Warning, Firing, Defeated }
    [Header("Movement and ground")]
    [SerializeField, Min(.1f)] private float entrySpeed = 2.8f;
    [SerializeField, Min(6f)] private float combatDistance = 9f;
    [SerializeField] private float standingFootY = -2f;
    [SerializeField] private float maximumX = 115f;
    [Header("Burst (damage occurs only at projectile contact)")]
    [SerializeField, Min(1)] private int damage = 1;
    [SerializeField, Min(1)] private int shotsPerBurst = 3;
    [SerializeField, Min(.1f)] private float shotInterval = .22f;
    [SerializeField, Min(.4f)] private float warningDuration = .9f;
    [SerializeField, Min(.5f)] private float recoveryDuration = 2.8f;
    [SerializeField, Min(1f)] private float projectileSpeed = 11f;
    [SerializeField] private Vector2 localMuzzle = new(-.78f, .83f);

    private EnemyHealth health;
    private SpriteRenderer body;
    private Animator animator;
    private PlayerActions target;
    private Camera view;
    private BossHealthHUD hud;
    private float groundY, minimumX, deadline, nextTargetSearch;
    private int remainingShots;
    private Vector2 lockedAim;
    private bool initialized;
    public CombatPhase Phase { get; private set; } = CombatPhase.Entering;
    public EnemyHealth Health => health;
    public bool Enraged => health != null && health.CurrentHealth <= health.MaxHealth * .5f;
    public Vector2 Muzzle => transform.TransformPoint(new Vector3(
        body != null && body.flipX ? -localMuzzle.x : localMuzzle.x, localMuzzle.y, 0));

    private void Awake()
    {
        health = GetComponent<EnemyHealth>();
        body = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        health.Died += OnDied;
        body.sortingOrder = 6;
        view = Camera.main;
    }

    public void Initialize(float streetY, float entranceX)
    {
        groundY = streetY;
        minimumX = entranceX + 7.5f;
        initialized = true;
        PlaceOnGround();
        FindTarget();
        BossProjectile.Prewarm();
        hud = gameObject.AddComponent<BossHealthHUD>();
        hud.Bind(this);
        Phase = CombatPhase.Entering;
        animator.SetBool("IsMoving", true);
    }

    private void FindTarget()
    {
        target = null;
        foreach (var player in FindObjectsByType<PlayerActions>(FindObjectsSortMode.None))
            if (!player.IsDefeated && player.isActiveAndEnabled) { target = player; break; }
        nextTargetSearch = Time.time + .4f;
    }

    private void Update()
    {
        if (!initialized || health.IsDead || Phase == CombatPhase.Defeated) return;
        if (GameFlowController.IsMatchEnded) { StopCombat(); return; }
        if (GameFlowController.IsPaused || PistolUpgradeShop.IsOpen || BossIntroduction.IsActive) return;
        if ((target == null || !target.isActiveAndEnabled || target.IsDefeated) && Time.time >= nextTargetSearch)
            FindTarget();
        if (target == null || target.IsDefeated) return;
        float dx = target.transform.position.x - transform.position.x;
        if (Phase == CombatPhase.Entering || Phase == CombatPhase.Recovery)
            body.flipX = dx > 0f; // Keep the warned muzzle/aim locked throughout the burst.
        switch (Phase)
        {
            case CombatPhase.Entering:
                float side = dx > 0 ? -1 : 1;
                float destination = Mathf.Clamp(target.transform.position.x + side * DesiredCombatDistance(dx), minimumX, maximumX);
                MoveTowards(destination, entrySpeed);
                if (Mathf.Abs(transform.position.x - destination) < .12f)
                    Recover(1.4f);
                break;
            case CombatPhase.Recovery:
                // Reposition between bursts, never move a locked firing origin mid-warning.
                float recoveryX = Mathf.Clamp(target.transform.position.x - Mathf.Sign(dx) * DesiredCombatDistance(dx), minimumX, maximumX);
                if (Mathf.Abs(transform.position.x - recoveryX) > .15f)
                    MoveTowards(recoveryX, entrySpeed * .65f);
                else animator.SetBool("IsMoving", false);
                if (Time.time >= deadline && CanEngage()) BeginWarning();
                break;
            case CombatPhase.Warning:
                if (Time.time < deadline) break;
                Phase = CombatPhase.Firing;
                remainingShots = shotsPerBurst + (Enraged ? 2 : 0);
                animator.SetTrigger("Attack");
                deadline = Time.time + .25f;
                break;
            case CombatPhase.Firing:
                if (Time.time < deadline) break;
                Vector2 direction = lockedAim - Muzzle;
                if (direction.sqrMagnitude > .01f)
                    BossProjectile.Fire(Muzzle, direction.normalized, projectileSpeed, damage, health);
                remainingShots--;
                if (remainingShots <= 0) Recover(Enraged ? recoveryDuration * .8f : recoveryDuration);
                else deadline = Time.time + shotInterval;
                break;
        }
    }

    private bool CanEngage()
    {
        if (Vector2.Distance(Muzzle, target.AimOrigin) > 19f) return false;
        if (view == null) return true;
        Vector3 screen = view.WorldToViewportPoint(body.bounds.center);
        return screen.z > 0 && screen.x >= -.05f && screen.x <= 1.05f;
    }
    private float DesiredCombatDistance(float playerDelta)
    {
        if (view == null || !view.orthographic) return combatDistance;
        float halfView = view.orthographicSize * view.aspect;
        float room = playerDelta <= 0
            ? view.transform.position.x + halfView - target.transform.position.x
            : target.transform.position.x - (view.transform.position.x - halfView);
        // Directional camera framing and narrow phones must not strand the boss off-screen.
        return Mathf.Clamp(room * .7f, 5f, combatDistance);
    }
    private void BeginWarning()
    {
        animator.SetBool("IsMoving", false);
        lockedAim = target.AimOrigin;
        Phase = CombatPhase.Warning;
        deadline = Time.time + warningDuration;
    }
    private void Recover(float duration)
    {
        animator.SetBool("IsMoving", false);
        Phase = CombatPhase.Recovery;
        deadline = Time.time + duration;
    }
    private void MoveTowards(float x, float speed)
    {
        Vector3 position = transform.position;
        position.x = Mathf.MoveTowards(position.x, x, speed * Time.deltaTime);
        transform.position = position;
        animator.SetBool("IsMoving", true);
    }
    private void PlaceOnGround()
    {
        Vector3 position = transform.position;
        position.y = groundY - standingFootY * transform.lossyScale.y;
        transform.position = position;
    }
    private void LateUpdate()
    {
        if (initialized && !health.IsDead) PlaceOnGround();
    }
    private void OnDied(EnemyHealth victim)
    {
        Phase = CombatPhase.Defeated;
        BossProjectile.CancelFor(health);
        animator.SetBool("IsMoving", false);
        animator.ResetTrigger("Attack");
        animator.ResetTrigger("TakeDamage");
        animator.Play("BossTruck_Defeat", 0, 0);
    }
    private void StopCombat()
    {
        animator.SetBool("IsMoving", false);
        animator.ResetTrigger("Attack");
        Phase = CombatPhase.Defeated;
        BossProjectile.CancelFor(health);
    }
    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
        BossProjectile.CancelFor(health);
    }
}
