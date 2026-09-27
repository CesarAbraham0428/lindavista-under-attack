using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;
    [SerializeField] private float maxHorizontalPosition = 117f;

    private Rigidbody2D rb;
    private Animator animator;
    private float horizontal;
    private float touchHorizontal;
    private bool movementLocked;

    public float HorizontalInput => horizontal;

    public void SetTouchHorizontal(float direction)
    {
        touchHorizontal = Mathf.Clamp(direction, -1f, 1f);
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (movementLocked)
        {
            animator.SetBool("IsMoving", false);
            return;
        }

        float keyboardHorizontal = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed)
                keyboardHorizontal = -1f;

            if (Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed)
                keyboardHorizontal = 1f;
        }

        horizontal = keyboardHorizontal != 0f ? keyboardHorizontal : touchHorizontal;

        if (horizontal > 0f && rb.position.x >= maxHorizontalPosition)
            horizontal = 0f;

        animator.SetBool("IsMoving", horizontal != 0f);
    }

    private void FixedUpdate()
    {
        if (movementLocked)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            return;
        }

        float nextX = rb.position.x + horizontal * speed * Time.fixedDeltaTime;

        if (horizontal > 0f && nextX >= maxHorizontalPosition)
        {
            Vector2 position = rb.position;
            position.x = maxHorizontalPosition;
            rb.position = position;
            horizontal = 0f;
            animator.SetBool("IsMoving", false);
        }
        else if (rb.position.x > maxHorizontalPosition)
        {
            Vector2 position = rb.position;
            position.x = maxHorizontalPosition;
            rb.position = position;
        }

        rb.linearVelocity = new Vector2(
            horizontal * speed,
            rb.linearVelocity.y
        );
    }

    public void StopForDefeat()
    {
        movementLocked = true;
        horizontal = 0f;
        touchHorizontal = 0f;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
        rb.bodyType = RigidbodyType2D.Kinematic;
        animator.SetBool("IsMoving", false);
    }
}
