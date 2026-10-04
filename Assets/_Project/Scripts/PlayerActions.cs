using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : MonoBehaviour
{
    [Header("Mouse aim")]
    [SerializeField, Min(0.1f)] private float maxAimDistance = 18f;
    [Header("Projectile spawn (world units from sprite center)")]
    [SerializeField, Min(0f)] private float pistolMuzzleDistance = 0.4f;
    [SerializeField] private float muzzleHeight = 0.08f;

    private Animator animator;
    private PlayerMovement movement;
    private SpriteRenderer spriteRenderer;
    private Camera aimCamera;
    private PlayerHealth health;
    private PlayerWeaponController weapons;
    private bool hasAimInViewport;
    private bool touchAimActive;
    private bool touchFireHeld;
    private bool inputLocked;
    private bool defeated;

    public bool IsDefeated => defeated;
    public bool IsInputLocked => inputLocked;
    public int CurrentHealth => health != null ? health.CurrentHealth : 0;
    public int MaxHealth => health != null ? health.MaxHealth : 5;
    public PlayerHealth Health => health;
    public PlayerWeaponController Weapons => weapons;
    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 AimTarget { get; private set; }
    public Vector2 AimOrigin => spriteRenderer != null
        ? (Vector2)spriteRenderer.bounds.center
        : (Vector2)transform.position;

    public void SetInputLocked(bool locked)
    {
        inputLocked = locked || defeated || GameFlowController.IsDefeatActive;
        if (!locked)
            return;

        hasAimInViewport = false;
        touchAimActive = false;
        touchFireHeld = false;
        weapons?.CancelReload();
        if (animator != null)
            ResetFireTriggers();
    }

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        if (health == null) health = gameObject.AddComponent<PlayerHealth>();
        weapons = GetComponent<PlayerWeaponController>();
        if (weapons == null) weapons = gameObject.AddComponent<PlayerWeaponController>();
        health.Damaged += OnDamaged;
        health.Died += Defeat;
        maxAimDistance = weapons.Definition.Range;
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        aimCamera = Camera.main;
        if (animator != null)
            animator.SetBool("IsAiming", false);
    }

    private void Update()
    {
        if (inputLocked || PistolUpgradeShop.IsOpen)
            return;

        UpdateAim();

        if (defeated)
            return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                weapons.TryEquip(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                weapons.TryEquip(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
                weapons.TryEquip(2);

            if (Keyboard.current.rKey.wasPressedThisFrame)
                TryReload();

            if (gameObject.scene.name == "Testing" && Keyboard.current.qKey.wasPressedThisFrame)
                TakeDamage(1);

            if (gameObject.scene.name == "Testing" && Keyboard.current.eKey.wasPressedThisFrame)
            {
                TakeDamage(MaxHealth);
                return;
            }
        }

        bool mouseFireHeld = !Application.isMobilePlatform &&
                             !MobileControlsHUD.IsVisible &&
                             Mouse.current != null &&
                             !(UnityEngine.EventSystems.EventSystem.current != null &&
                               UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) &&
                             Mouse.current.leftButton.isPressed && hasAimInViewport;
        bool firingHeld = touchFireHeld || mouseFireHeld;
        if (firingHeld)
            HoldFire();
        else
        {
            ResetFireTriggers();
        }

    }

    private void LateUpdate()
    {
        if (inputLocked || defeated || spriteRenderer == null || movement == null)
            return;

        // Only movement keys change facing. Keep the last direction when idle.
        float direction = movement.HorizontalInput;
        if (direction != 0f)
            spriteRenderer.flipX = direction < 0f;
    }

    public void SelectTouchWeapon(int index)
    {
        if (!inputLocked && !defeated && index >= 0 && index <= 2)
            weapons.TryEquip(index);
    }

    public void ReloadTouchWeapon()
    {
        if (!inputLocked)
            TryReload();
    }

    public void TakeDamage(int amount)
    {
        health?.TakeDamage(amount);
    }

    private void OnDamaged()
    {
        weapons?.CancelReload();
        if (animator != null) animator.SetTrigger("TakeDamage");
    }

    private void OnDestroy()
    {
        if (health == null) return;
        health.Damaged -= OnDamaged;
        health.Died -= Defeat;
    }

    private void Defeat()
    {
        if (defeated)
            return;

        defeated = true;
        hasAimInViewport = false;
        touchFireHeld = false;
        weapons?.CancelReload();
        if (animator != null)
        {
            ResetFireTriggers();
            animator.SetTrigger("Defeat");
        }
        movement?.StopForDefeat();
        GameFlowController.ReportPlayerDefeat();
    }

    public void SetTouchAimAndFire(Vector2 direction)
    {
        if (inputLocked || defeated)
            return;

        touchAimActive = true;
        touchFireHeld = true;
        if (direction.sqrMagnitude > 0.0025f)
            AimDirection = direction.normalized;
        AimTarget = AimOrigin + AimDirection * maxAimDistance;
    }

    public void StopTouchAimAndFire()
    {
        touchAimActive = false;
        touchFireHeld = false;
        if (animator != null)
        {
            ResetFireTriggers();
        }
    }

    private void TryReload()
    {
        if (inputLocked || defeated || IsBusyExceptFire() || !weapons.TryReload())
            return;

        ResetFireTriggers();
        if (animator != null) animator.SetTrigger("ReloadPistol");
    }

    private void HoldFire()
    {
        if (inputLocked || IsBusyExceptFire())
            return;

        if (weapons.Magazine == 0) { TryReload(); return; }
        Vector2 spawnPosition = AimOrigin + AimDirection * pistolMuzzleDistance + Vector2.up * muzzleHeight;
        Vector2 shotDirection = AimTarget - spawnPosition;
        if (shotDirection.sqrMagnitude < 0.0001f)
            shotDirection = AimDirection;
        if (weapons.TryFire(spawnPosition, shotDirection.normalized, transform) && animator != null)
            animator.SetTrigger("FirePistol");
    }

    private void ResetFireTriggers()
    {
        if (animator == null) return;
        animator.ResetTrigger("FirePistol");
        animator.ResetTrigger("FireSMG");
        animator.ResetTrigger("FireRPG");
    }

    private void UpdateAim()
    {
        hasAimInViewport = false;
        if (inputLocked || defeated)
            return;

        if (touchAimActive)
        {
            AimTarget = AimOrigin + AimDirection * maxAimDistance;
            return;
        }

        if (Application.isMobilePlatform || MobileControlsHUD.IsVisible || Mouse.current == null)
            return;

        if (aimCamera == null)
            aimCamera = Camera.main;

        if (aimCamera == null)
            return;

        Vector2 mousePosition = Mouse.current.position.ReadValue();
        Rect playableView = aimCamera.pixelRect;

        // Outside the Game view, keep the last valid aim instead of generating
        // a direction towards the Inspector, toolbar, or an invalid screen point.
        if (!playableView.Contains(mousePosition))
            return;

        hasAimInViewport = true;

        float distanceFromCamera = Mathf.Abs(transform.position.z - aimCamera.transform.position.z);
        Vector3 mouseWorld = aimCamera.ScreenToWorldPoint(
            new Vector3(mousePosition.x, mousePosition.y, distanceFromCamera));

        Vector2 fromPlayer = (Vector2)mouseWorld - AimOrigin;
        if (fromPlayer.sqrMagnitude < 0.0001f)
            return;

        AimDirection = fromPlayer.normalized;
        AimTarget = AimOrigin + Vector2.ClampMagnitude(fromPlayer, maxAimDistance);

    }

    private void OnGUI()
    {
        if (inputLocked || defeated || aimCamera == null || !hasAimInViewport || MobileControlsHUD.IsVisible)
            return;

        Vector3 screen = aimCamera.WorldToScreenPoint(AimTarget);
        if (screen.z <= 0f)
            return;

        float x = screen.x;
        float y = Screen.height - screen.y;
        Color previousColor = GUI.color;
        GUI.color = Color.yellow;
        GUI.DrawTexture(new Rect(x - 1f, y - 10f, 2f, 7f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 1f, y + 3f, 2f, 7f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x - 10f, y - 1f, 7f, 2f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 3f, y - 1f, 7f, 2f), Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private bool IsBusyExceptFire()
    {
        return IsPlaying("Pistol_Reload") ||
               IsPlaying("SMG_Reload") ||
               IsPlaying("RPG_Reload") ||
               IsPlaying("Damage") ||
               IsPlaying("Defeat");
    }

    private bool IsPlaying(string stateSuffix)
    {
        if (animator == null) return false;
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName($"Cesar_{stateSuffix}") ||
               state.IsName($"Marco_{stateSuffix}");
    }
}
