using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : MonoBehaviour
{
    private enum Weapon { Pistol, SMG, RPG }

    [Header("Mouse aim")]
    [SerializeField, Min(0.1f)] private float maxAimDistance = 18f;
    [Header("Minimum time between shots")]
    [SerializeField, Min(0.01f)] private float pistolFireInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float smgFireInterval = 0.12f;
    [SerializeField, Min(0.01f)] private float rpgFireInterval = 1.2f;

    private Animator animator;
    private PlayerMovement movement;
    private SpriteRenderer spriteRenderer;
    private Camera aimCamera;
    private LineRenderer shotLine;
    private Material shotMaterial;
    private Weapon selectedWeapon = Weapon.Pistol;
    private bool hasAimInViewport;
    private bool wasFiringHeld;
    private bool defeated;
    private float hideShotTime;
    private float nextShotTime;

    public Vector2 AimDirection { get; private set; } = Vector2.right;
    public Vector2 AimTarget { get; private set; }
    public Vector2 AimOrigin => spriteRenderer != null
        ? (Vector2)spriteRenderer.bounds.center
        : (Vector2)transform.position;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        aimCamera = Camera.main;
        if (animator != null)
            animator.SetBool("IsAiming", false);
        CreateShotLine();
    }

    private void Update()
    {
        UpdateAim();

        if (shotLine != null && shotLine.enabled && Time.time >= hideShotTime)
            shotLine.enabled = false;

        if (animator == null || defeated)
            return;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.digit1Key.wasPressedThisFrame)
                selectedWeapon = Weapon.Pistol;
            else if (Keyboard.current.digit2Key.wasPressedThisFrame)
                selectedWeapon = Weapon.SMG;
            else if (Keyboard.current.digit3Key.wasPressedThisFrame)
                selectedWeapon = Weapon.RPG;

            if (Keyboard.current.rKey.wasPressedThisFrame && !IsBusy())
            {
                ResetFireTriggers();
                animator.SetTrigger(selectedWeapon switch
                {
                    Weapon.Pistol => "ReloadPistol",
                    Weapon.SMG => "ReloadSMG",
                    _ => "ReloadRPG"
                });
                nextShotTime = Mathf.Max(nextShotTime, Time.time + 0.15f);
            }

            if (Keyboard.current.qKey.wasPressedThisFrame)
                animator.SetTrigger("TakeDamage");

            if (Keyboard.current.eKey.wasPressedThisFrame)
            {
                defeated = true;
                hasAimInViewport = false;
                ResetFireTriggers();
                if (shotLine != null)
                    shotLine.enabled = false;
                movement?.StopForDefeat();
                animator.SetTrigger("Defeat");
                return;
            }
        }

        bool firingHeld = Mouse.current != null &&
                          Mouse.current.leftButton.isPressed && hasAimInViewport;
        if (firingHeld)
            HoldFire();
        else
        {
            ResetFireTriggers();
            if (shotLine != null)
                shotLine.enabled = false;
            if (wasFiringHeld)
                StopFireAnimation();
        }

        wasFiringHeld = firingHeld;
    }

    private void LateUpdate()
    {
        if (defeated || spriteRenderer == null || Keyboard.current == null)
            return;

        // Only movement keys change facing. Keep the last direction when idle.
        bool left = Keyboard.current.aKey.isPressed ||
                    Keyboard.current.leftArrowKey.isPressed;
        bool right = Keyboard.current.dKey.isPressed ||
                     Keyboard.current.rightArrowKey.isPressed;
        if (left != right)
            spriteRenderer.flipX = left;
    }

    private void HoldFire()
    {
        if (Time.time < nextShotTime || IsBusy())
            return;

        string triggerName;
        float fireInterval;
        switch (selectedWeapon)
        {
            case Weapon.SMG:
                triggerName = "FireSMG";
                fireInterval = smgFireInterval;
                break;
            case Weapon.RPG:
                triggerName = "FireRPG";
                fireInterval = rpgFireInterval;
                break;
            default:
                triggerName = "FirePistol";
                fireInterval = pistolFireInterval;
                break;
        }

        animator.SetTrigger(triggerName);
        nextShotTime = Time.time + fireInterval;
        ShowShotDirection();
    }

    private void ResetFireTriggers()
    {
        animator.ResetTrigger("FirePistol");
        animator.ResetTrigger("FireSMG");
        animator.ResetTrigger("FireRPG");
    }

    private void StopFireAnimation()
    {
        if (!IsPlaying("Pistol_Fire") && !IsPlaying("SMG_Fire") &&
            !IsPlaying("RPG_Fire"))
            return;

        string character = animator.runtimeAnimatorController.name.Contains("Marco")
            ? "Marco" : "Cesar";
        animator.Play(character + "_Idle", 0, 0f);
    }

    private void UpdateAim()
    {
        hasAimInViewport = false;
        if (defeated || Mouse.current == null)
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

    private void CreateShotLine()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return;

        GameObject visual = new GameObject("Shot Direction Visual");
        visual.transform.SetParent(transform, false);

        shotMaterial = new Material(shader);
        shotLine = visual.AddComponent<LineRenderer>();
        shotLine.material = shotMaterial;
        shotLine.useWorldSpace = true;
        shotLine.positionCount = 2;
        shotLine.startWidth = 0.045f;
        shotLine.endWidth = 0.015f;
        shotLine.startColor = Color.yellow;
        shotLine.endColor = new Color(1f, 0.3f, 0.1f, 0.65f);
        shotLine.sortingOrder = 100;
        shotLine.enabled = false;
    }

    private void ShowShotDirection()
    {
        if (shotLine == null || aimCamera == null)
            return;

        Vector2 origin = AimOrigin;
        Vector2 target = origin + AimDirection * maxAimDistance;

        shotLine.SetPosition(0, new Vector3(origin.x, origin.y, 0f));
        shotLine.SetPosition(1, new Vector3(target.x, target.y, 0f));
        shotLine.enabled = true;
        hideShotTime = Time.time + 0.09f;
    }

    private void OnGUI()
    {
        if (defeated || aimCamera == null || !hasAimInViewport)
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

    private void OnDestroy()
    {
        if (shotMaterial != null)
            Destroy(shotMaterial);
    }

    private bool IsBusy()
    {
        return IsPlaying("Pistol_Fire") ||
               IsPlaying("SMG_Fire") ||
               IsPlaying("RPG_Fire") ||
               IsPlaying("Pistol_Reload") ||
               IsPlaying("SMG_Reload") ||
               IsPlaying("RPG_Reload") ||
               IsPlaying("Damage") ||
               IsPlaying("Defeat");
    }

    private bool IsPlaying(string stateSuffix)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName($"Cesar_{stateSuffix}") ||
               state.IsName($"Marco_{stateSuffix}");
    }
}
