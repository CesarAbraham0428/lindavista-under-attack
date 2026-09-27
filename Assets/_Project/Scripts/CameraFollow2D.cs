using System.Collections;
using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    private const string SelectedLevelKey = "Lindavista.SelectedLevel";

    [SerializeField] private Transform target;
    [SerializeField] private float startFollowingAtX = 3f;
    [SerializeField] private float horizontalOffset = 2f;
    [SerializeField] private float maximumCameraX = 115f;
    [SerializeField] private float smoothTime = 0.3f;

    [Header("Level start presentation")]
    [SerializeField, Min(0f)] private float lineupHoldDuration = 1.1f;
    [SerializeField, Min(0.1f)] private float returnPanDuration = 3.5f;
    [SerializeField, Min(0f)] private float lineupCameraPadding = 3f;

    private float startCameraX;
    private float fixedY;
    private float fixedZ;
    private float horizontalVelocity;
    private float normalOrthographicSize;
    private bool hasStartedFollowing;
    private bool introInProgress;
    private Camera attachedCamera;
    private PlayerActions playerActions;
    private PlayerMovement playerMovement;

    private void Awake()
    {
        Vector3 initialPosition = transform.position;
        startCameraX = initialPosition.x;
        fixedY = initialPosition.y;
        fixedZ = initialPosition.z;
        attachedCamera = GetComponent<Camera>();
        if (attachedCamera != null && attachedCamera.orthographic)
            normalOrthographicSize = attachedCamera.orthographicSize;

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
            float cameraX = Mathf.Lerp(lineupPosition.x, startCameraX, easedTime);
            transform.position = new Vector3(cameraX, fixedY, fixedZ);

            if (attachedCamera != null && attachedCamera.orthographic)
                attachedCamera.orthographicSize = Mathf.Lerp(lineupOrthographicSize, normalOrthographicSize, easedTime);

            yield return null;
        }

        transform.position = new Vector3(startCameraX, fixedY, fixedZ);
        if (attachedCamera != null && attachedCamera.orthographic)
            attachedCamera.orthographicSize = normalOrthographicSize;

        introInProgress = false;
        hasStartedFollowing = false;
        SetAllPlayerControlsLocked(false);
    }

    private void LateUpdate()
    {
        if (introInProgress)
            return;

        if (target == null || !target.gameObject.activeInHierarchy)
            FindActivePlayer();

        if (target == null)
            return;

        if (!hasStartedFollowing)
        {
            if (target.position.x <= startFollowingAtX)
                return;

            hasStartedFollowing = true;
        }

        float desiredX = Mathf.Clamp(
            target.position.x - horizontalOffset,
            startCameraX,
            maximumCameraX
        );

        float nextX = Mathf.SmoothDamp(
            transform.position.x,
            desiredX,
            ref horizontalVelocity,
            Mathf.Max(0.01f, smoothTime)
        );

        nextX = Mathf.Clamp(nextX, startCameraX, maximumCameraX);
        transform.position = new Vector3(nextX, fixedY, fixedZ);
    }

    private void FindActivePlayer()
    {
        GameObject player = GameObject.Find("Player_Marco");

        if (player == null)
            player = GameObject.Find("Player_Cesar");

        target = player != null ? player.transform : null;
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
