using System.Collections;
using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";

    [SerializeField] private Transform target;
    [SerializeField, Range(0.05f, 0.95f)] private float targetScreenX = 0.2f;
    [SerializeField, Range(0.05f, 0.95f)] private float leftTargetScreenX = 0.6f;
    [SerializeField] private float maximumCameraX = 115f;
    [SerializeField, Min(0.01f)] private float smoothTime = 0.1f;

    [Header("Direction framing")]
    [SerializeField, Min(0f)] private float directionConfirmDuration = 0.08f;
    [SerializeField, Min(0.01f)] private float directionTransitionDuration = 0.9f;

    [Header("Level start presentation")]
    [SerializeField, Min(0f)] private float lineupHoldDuration = 1.1f;
    [SerializeField, Min(0.1f)] private float returnPanDuration = 3.5f;
    [SerializeField, Min(0f)] private float lineupCameraPadding = 3f;

    private float startCameraX;
    private float fixedY;
    private float fixedZ;
    private float horizontalVelocity;
    private float normalOrthographicSize;
    private bool introInProgress;
    private Camera attachedCamera;
    private PlayerActions playerActions;
    private PlayerMovement playerMovement;
    private int framingDirection = 1;
    private int pendingDirection;
    private float pendingDirectionTime;
    private float currentScreenX;
    private float framingVelocity;
    private float proximaBusquedaJugador;

    // Mantiene el borde de entrada de la vista 16:9 al usar una pantalla más estrecha.
    // Se calcula siempre desde la posición original, sin acumular ajustes al rotar.
    private float MinimoCamaraX => attachedCamera != null && attachedCamera.orthographic
        ? startCameraX + normalOrthographicSize * Mathf.Min(0f, attachedCamera.aspect - 16f / 9f)
        : startCameraX;

    private void Awake()
    {
        Vector3 initialPosition = transform.position;
        startCameraX = initialPosition.x;
        fixedY = initialPosition.y;
        fixedZ = initialPosition.z;
        attachedCamera = GetComponent<Camera>();
        currentScreenX = targetScreenX;
        if (attachedCamera != null && attachedCamera.orthographic)
            normalOrthographicSize = attachedCamera.orthographicSize;
        transform.position = new Vector3(MinimoCamaraX, fixedY, fixedZ);

        FindActivePlayer();
        if (FindFirstObjectByType<LevelOneEnemySpawner>() != null)
            SetAllPlayerControlsLocked(true);
    }

    private IEnumerator Start()
    {
        LevelOneEnemySpawner spawner = FindFirstObjectByType<LevelOneEnemySpawner>();
        if (target == null || spawner == null)
        {
            SetAllPlayerControlsLocked(false);
            yield break;
        }

        playerActions = target.GetComponent<PlayerActions>();
        playerMovement = target.GetComponent<PlayerMovement>();
        playerActions?.SetInputLocked(true);
        playerMovement?.SetMovementLocked(true);

        int selectedLevel = PlayerPrefs.GetInt(SelectedLevelKey, 0);
        if (!spawner.PrepareWave(selectedLevel))
        {
            SetAllPlayerControlsLocked(false);
            yield break;
        }

        introInProgress = true;
        transform.position = new Vector3(spawner.IntroCameraX, fixedY, fixedZ);
        float lineupOrthographicSize = normalOrthographicSize;
        if (attachedCamera != null && attachedCamera.orthographic)
        {
            float requiredSize = (spawner.LineupWidth + lineupCameraPadding) /
                                 (2f * Mathf.Max(0.1f, attachedCamera.aspect));
            lineupOrthographicSize = Mathf.Max(normalOrthographicSize, requiredSize);
            attachedCamera.orthographicSize = lineupOrthographicSize;
            // Keep the bottom of the normal view covered by the street during the intro zoom.
            transform.position = new Vector3(spawner.IntroCameraX,
                fixedY + lineupOrthographicSize - normalOrthographicSize, fixedZ);
        }

        // The character-selection bootstrap can swap the active player in Start.
        // Keep both characters locked for this frame, then bind the chosen one.
        yield return null;
        FindActivePlayer();
        playerActions = target != null ? target.GetComponent<PlayerActions>() : null;
        playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
        playerActions?.SetInputLocked(true);
        playerMovement?.SetMovementLocked(true);

        if (lineupHoldDuration > 0f)
            yield return new WaitForSeconds(lineupHoldDuration);

        // The first enemy is released as the camera starts traveling back to the entrance.
        spawner.BeginWave();

        Vector3 lineupPosition = transform.position;
        float elapsed = 0f;
        float duration = Mathf.Max(0.1f, returnPanDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            float easedTime = normalizedTime * normalizedTime * (3f - 2f * normalizedTime);
            float cameraX = Mathf.Lerp(lineupPosition.x, MinimoCamaraX, easedTime);
            transform.position = new Vector3(cameraX, fixedY, fixedZ);

            if (attachedCamera != null && attachedCamera.orthographic)
            {
                attachedCamera.orthographicSize = Mathf.Lerp(lineupOrthographicSize, normalOrthographicSize, easedTime);
                transform.position = new Vector3(cameraX,
                    fixedY + attachedCamera.orthographicSize - normalOrthographicSize, fixedZ);
            }

            yield return null;
        }

        transform.position = new Vector3(MinimoCamaraX, fixedY, fixedZ);
        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = normalOrthographicSize;

        introInProgress = false;
        SetAllPlayerControlsLocked(false);
    }

    private void LateUpdate()
    {
        if (introInProgress)
            return;

        if ((target == null || !target.gameObject.activeInHierarchy) && Time.unscaledTime >= proximaBusquedaJugador)
        {
            FindActivePlayer();
            proximaBusquedaJugador = Time.unscaledTime + 0.5f;
        }

        if (target == null || !target.gameObject.activeInHierarchy)
            return;

        UpdateDirectionFraming();

        float worldOffsetFromCenter = 0f;
        if (attachedCamera != null && attachedCamera.orthographic)
        {
            float worldViewWidth = 2f * attachedCamera.orthographicSize * attachedCamera.aspect;
            worldOffsetFromCenter = (currentScreenX - 0.5f) * worldViewWidth;
        }

        // The start boundary holds the entrance view until the player reaches
        // the desired screen position, so following begins without a catch-up pan.
        float minimoX = MinimoCamaraX;
        float desiredX = Mathf.Clamp(
            target.position.x - worldOffsetFromCenter,
            minimoX,
            maximumCameraX
        );

        float nextX = Mathf.SmoothDamp(
            transform.position.x,
            desiredX,
            ref horizontalVelocity,
            Mathf.Max(0.01f, smoothTime)
        );

        nextX = Mathf.Clamp(nextX, minimoX, maximumCameraX);
        transform.position = new Vector3(nextX, fixedY, fixedZ);
    }

    private void FindActivePlayer()
    {
        GameObject player = GameObject.Find("Player_Marco");

        if (player == null)
            player = GameObject.Find("Player_Cesar");

        target = player != null ? player.transform : null;
        playerMovement = target != null ? target.GetComponent<PlayerMovement>() : null;
    }

    private void UpdateDirectionFraming()
    {
        // Movement determines the view; aiming and firing can face independently.
        float horizontal = playerMovement != null ? playerMovement.HorizontalInput : 0f;
        StepDirectionFraming(horizontal, Time.deltaTime);
    }

    private void StepDirectionFraming(float horizontal, float deltaTime)
    {
        if (deltaTime <= 0f)
            return;

        int movementDirection = horizontal > 0.01f ? 1 : horizontal < -0.01f ? -1 : 0;
        if (movementDirection == 0 || movementDirection == framingDirection)
        {
            pendingDirection = 0;
            pendingDirectionTime = 0f;
        }
        else
        {
            if (pendingDirection != movementDirection)
            {
                pendingDirection = movementDirection;
                pendingDirectionTime = 0f;
            }

            pendingDirectionTime += deltaTime;
            if (pendingDirectionTime >= directionConfirmDuration)
            {
                framingDirection = movementDirection;
                pendingDirection = 0;
                pendingDirectionTime = 0f;
            }
        }

        // Preserve pan velocity when turning again, rather than restarting an easing
        // curve at zero speed. Brief input changes do not shake the view, while a
        // sustained turn starts reframing in 80 ms and settles in about one second.
        float desiredScreenX = framingDirection > 0 ? targetScreenX : leftTargetScreenX;
        currentScreenX = Mathf.SmoothDamp(currentScreenX, desiredScreenX,
            ref framingVelocity, Mathf.Max(0.01f, directionTransitionDuration / 3f),
            0.8f, deltaTime);
    }

    private void SetAllPlayerControlsLocked(bool locked)
    {
        foreach (PlayerActions actions in FindObjectsByType<PlayerActions>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            actions.SetInputLocked(locked);

        foreach (PlayerMovement movement in FindObjectsByType<PlayerMovement>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            movement.SetMovementLocked(locked);
    }
}
