using UnityEngine;

public class CameraFollow2D : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float startFollowingAtX = 3f;
    [SerializeField] private float horizontalOffset = 2f;
    [SerializeField] private float maximumCameraX = 79.5f;
    [SerializeField] private float smoothTime = 0.3f;

    private float startCameraX;
    private float fixedY;
    private float fixedZ;
    private float horizontalVelocity;
    private bool hasStartedFollowing;

    private void Awake()
    {
        Vector3 initialPosition = transform.position;
        startCameraX = initialPosition.x;
        fixedY = initialPosition.y;
        fixedZ = initialPosition.z;
    }

    private void LateUpdate()
    {
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
}
