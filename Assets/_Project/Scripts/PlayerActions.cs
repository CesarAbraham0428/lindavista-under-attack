using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : MonoBehaviour
{
    [Header("Mouse aim")]
    [SerializeField, Min(0.1f)] private float maxAimDistance = 18f;

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
    private bool fireRequested;
    private int estadoDisparo;
    private int estadoRecarga;
    private int estadoDano;
    private int estadoDerrota;
    private int estadoReposo;
    private string nombreRecarga;
    private float duracionRecarga;
    private RuntimeAnimatorController controladorAnterior;
    private int armaAnterior = -1;
    private int estadoPastilla;
    private float duracionPastilla;
    private float tiempoPastilla;
    private bool pastillaAplicada;

    public bool IsUsingHealthConsumable { get; private set; }
    public bool CanUseHealthConsumable => isActiveAndEnabled && !inputLocked && !defeated &&
        !GameFlowController.IsMatchEnded && !GameFlowController.IsPaused && !PistolUpgradeShop.IsOpen &&
        health != null && health.CanHeal && weapons != null && !IsBusyExceptFire() &&
        ProgressionService.HealthConsumables(health.IsCesar) > 0 &&
        animator != null && animator.runtimeAnimatorController != null &&
        duracionPastilla > 0f && animator.HasState(0, estadoPastilla);

    public bool IsDefeated => defeated;
    public bool IsInputLocked => inputLocked;
    public int CurrentHealth => health != null ? health.CurrentHealth : 0;
    public int MaxHealth => health != null ? health.MaxHealth : CharacterStats.BaseHealth(CharacterWeapons.IsCesar(gameObject));
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

        CancelHealthConsumable();
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
        weapons.Changed += OnWeaponChanged;
        OnWeaponChanged();
    }

    private void Update()
    {
        fireRequested = false;
        if (IsUsingHealthConsumable)
        {
            if (inputLocked || defeated || GameFlowController.IsMatchEnded || PistolUpgradeShop.IsOpen)
                CancelHealthConsumable();
            else if (!GameFlowController.IsPaused)
                AdvanceHealthConsumable(Time.deltaTime);
            return;
        }
        if (inputLocked || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen)
            return;

        UpdateAim();

        if (defeated)
            return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.hKey.wasPressedThisFrame && TryUseHealthConsumable()) return;
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                EquipWeapon(0);
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                EquipWeapon(1);
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
                EquipWeapon(2);

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
            fireRequested = true;
        else
        {
            ResetFireTriggers();
        }

    }

    private void LateUpdate()
    {
        if (inputLocked || IsUsingHealthConsumable || GameFlowController.IsPaused || defeated || spriteRenderer == null)
            return;

        // Firing poses must face the target, even while walking the other way.
        bool firingPose = animator != null && animator.GetCurrentAnimatorStateInfo(0).shortNameHash == estadoDisparo;
        float direction = fireRequested || firingPose ? AimDirection.x :
            movement != null ? movement.HorizontalInput : 0f;
        if (direction != 0f)
            spriteRenderer.flipX = direction < 0f;
        if (fireRequested)
            HoldFire();
    }

    public void SelectTouchWeapon(int index)
    {
        if (!inputLocked && !GameFlowController.IsPaused && !defeated && index >= 0 && index <= 2)
            EquipWeapon(index);
    }

    public void EquipWeapon(int index)
    {
        if (IsUsingHealthConsumable) return;
        int previous = weapons.EquippedWeapon;
        if (!weapons.TryEquip(index) || previous == index || animator == null ||
            animator.runtimeAnimatorController == null) return;

        ResetFireTriggers();
        animator.Play(estadoReposo, 0, 0f);
        animator.Update(0f);
    }

    private void OnWeaponChanged()
    {
        maxAimDistance = weapons.Definition.Range;
        ActualizarEstadosAnimacion();
        if (animator != null && weapons.IsCesar)
            animator.SetFloat("WeaponIndex", weapons.EquippedWeapon);
    }

    private void ActualizarEstadosAnimacion()
    {
        RuntimeAnimatorController controlador = animator != null ? animator.runtimeAnimatorController : null;
        if (armaAnterior == weapons.EquippedWeapon && controladorAnterior == controlador) return;
        armaAnterior = weapons.EquippedWeapon;
        controladorAnterior = controlador;
        string personaje = weapons.IsCesar ? "Cesar" : "Marco";
        string arma = weapons.AnimationWeapon;
        estadoDisparo = Animator.StringToHash(personaje + "_" + arma + "_Fire");
        nombreRecarga = personaje + "_" + arma + "_Reload";
        estadoRecarga = Animator.StringToHash(nombreRecarga);
        estadoDano = Animator.StringToHash(personaje + "_Damage");
        estadoDerrota = Animator.StringToHash(personaje + "_Defeat");
        estadoReposo = Animator.StringToHash(personaje + "_Idle");
        string nombrePastilla = personaje + "_UseHealthConsumable";
        estadoPastilla = Animator.StringToHash(nombrePastilla);
        duracionPastilla = 0f;
        duracionRecarga = 0f;
        if (animator == null || animator.runtimeAnimatorController == null) return;
        foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == nombreRecarga) duracionRecarga = clip.length;
            if (clip.name == nombrePastilla) duracionPastilla = clip.length;
        }
    }

    public void ReloadTouchWeapon()
    {
        if (!inputLocked && !GameFlowController.IsPaused && !PistolUpgradeShop.IsOpen)
            TryReload();
    }

    public void TakeDamage(int amount)
    {
        health?.TakeDamage(amount);
    }

    public bool TryUseHealthConsumable()
    {
        if (!CanUseHealthConsumable) return false;
        StopTouchAimAndFire();
        fireRequested = false;
        IsUsingHealthConsumable = true;
        tiempoPastilla = 0f;
        pastillaAplicada = false;
        movement?.SetMovementLocked(true);
        animator.SetBool("IsAiming", false);
        animator.Play(estadoPastilla, 0, 0f);
        animator.Update(0f);
        return true;
    }

    private void AdvanceHealthConsumable(float deltaTime)
    {
        tiempoPastilla += deltaTime;
        // The sixth frame is the swallow pose. Charge one unit only when healing.
        if (!pastillaAplicada && tiempoPastilla >= duracionPastilla * 5f / 8f)
        {
            if (!health.CanHeal || !ProgressionService.TryConsumeHealthConsumable(health.IsCesar))
            {
                CancelHealthConsumable();
                return;
            }
            pastillaAplicada = true;
            health.Heal(1);
        }
        if (tiempoPastilla >= duracionPastilla) CancelHealthConsumable();
    }

    private void CancelHealthConsumable()
    {
        if (!IsUsingHealthConsumable) return;
        IsUsingHealthConsumable = false;
        movement?.SetMovementLocked(false);
        if (animator != null && animator.runtimeAnimatorController != null)
            animator.Play(estadoReposo, 0, 0f);
    }

    private void OnDamaged()
    {
        // An interrupted swallow before the healing frame does not spend a pill.
        CancelHealthConsumable();
        weapons?.CancelReload();
        if (animator != null) animator.SetTrigger("TakeDamage");
    }

    private void OnDestroy()
    {
        if (weapons != null) weapons.Changed -= OnWeaponChanged;
        if (health == null) return;
        health.Damaged -= OnDamaged;
        health.Died -= Defeat;
    }

    private void OnDisable()
    {
        CancelHealthConsumable();
        fireRequested = false;
        hasAimInViewport = false;
        StopTouchAimAndFire();
    }

    private void OnApplicationFocus(bool tieneFoco)
    {
        if (!tieneFoco) OnDisable();
    }

    private void Defeat()
    {
        if (defeated)
            return;

        defeated = true;
        CancelHealthConsumable();
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
        if (inputLocked || IsUsingHealthConsumable || GameFlowController.IsPaused || PistolUpgradeShop.IsOpen || defeated)
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
        if (inputLocked || GameFlowController.IsPaused || defeated || IsBusyExceptFire() || !weapons.TryReload())
            return;

        ResetFireTriggers();
        PlayReloadAnimation();
    }

    private void PlayReloadAnimation()
    {
        if (animator == null || animator.runtimeAnimatorController == null) return;
        if (duracionRecarga > 0f)
            animator.SetFloat("ReloadSpeed", duracionRecarga / weapons.Definition.ReloadDuration);
        animator.Play(estadoRecarga, 0, 0f);
        animator.Update(0f);
    }

    private void HoldFire()
    {
        if (inputLocked || IsBusyExceptFire())
            return;

        if (weapons.Magazine == 0) { TryReload(); return; }
        if (!weapons.CanFire) return;

        Vector2 aimOrigin = AimOrigin;
        // Evaluate the firing sprite before locating its barrel. A trigger alone
        // blends for 80 ms and would emit while the previous idle/walk pose is visible.
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            ResetFireTriggers();
            // Cesar's shotgun starts lowered: its third frame is the horizontal
            // firing pose. The sniper is ready on its fourth frame.
            float firingPoseTime = weapons.IsCesar
                ? weapons.EquippedWeapon == 0 ? 0.25f : weapons.Definition.IsPiercing ? 0.38f : 0f
                : 0f;
            animator.Play(estadoDisparo, 0, firingPoseTime);
            animator.Update(0f);
        }
        Vector2 spawnPosition = PlayerMuzzle.WorldPosition(spriteRenderer, weapons.EquippedWeapon);
        Vector2 shotDirection = AimTarget - spawnPosition;
        // Distant mouse shots pass through the crosshair and continue to the camera edge.
        // A pointer behind the barrel must not reverse the intended direction;
        // touch aiming already supplies a direction without a world-space target.
        bool reversesHorizontalAim = Mathf.Abs(AimDirection.x) > 0.0001f && shotDirection.x * AimDirection.x < 0f;
        if (touchAimActive || shotDirection.sqrMagnitude < 0.0001f ||
            Vector2.Dot(shotDirection, AimDirection) <= 0f || reversesHorizontalAim)
            shotDirection = AimDirection;
        else
        {
            // A nearby cursor just beyond the barrel otherwise turns a horizontal
            // aim almost vertical. This is especially visible when firing left,
            // where the camera leaves less space behind the player. Keep nearby
            // shots parallel to the player's aim, then ease into precise distant aim.
            float muzzleReach = Vector2.Distance(aimOrigin, spawnPosition);
            float pointerDistance = Vector2.Distance(aimOrigin, AimTarget);
            float preciseAim = muzzleReach > 0.0001f
                ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(muzzleReach * 2f, muzzleReach * 4f, pointerDistance))
                : 1f;
            shotDirection = Vector2.Lerp(AimDirection, shotDirection.normalized, preciseAim);
        }
        weapons.TryFire(spawnPosition, shotDirection.normalized, transform);
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

        SetPointerAim(mouseWorld);
    }

    private void SetPointerAim(Vector2 pointerWorld)
    {
        AimTarget = pointerWorld;
        Vector2 fromPlayer = pointerWorld - AimOrigin;
        if (fromPlayer.sqrMagnitude < 0.0001f)
            return;

        AimDirection = fromPlayer.normalized;
    }

    private void OnGUI()
    {
        if (inputLocked || GameFlowController.IsPaused || defeated || aimCamera == null || !hasAimInViewport || MobileControlsHUD.IsVisible)
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
        if (IsUsingHealthConsumable) return true;
        if (weapons.IsReloading) return true;
        if (animator == null) return false;
        int estado = animator.GetCurrentAnimatorStateInfo(0).shortNameHash;
        return estado == estadoRecarga || estado == estadoDano || estado == estadoDerrota;
    }
}
