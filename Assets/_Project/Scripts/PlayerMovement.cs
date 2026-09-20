using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private Rigidbody2D rb;
    private Animator animator;
    private float horizontal;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        horizontal = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed ||
                Keyboard.current.leftArrowKey.isPressed)
                horizontal = -1f;

            if (Keyboard.current.dKey.isPressed ||
                Keyboard.current.rightArrowKey.isPressed)
                horizontal = 1f;
        }

        animator.SetBool("IsMoving", horizontal != 0f);
    }

    private void FixedUpdate()
    {
        rb.linearVelocity = new Vector2(
            horizontal * speed,
            rb.linearVelocity.y
        );
    }
}