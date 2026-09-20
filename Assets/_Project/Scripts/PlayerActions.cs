using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private bool isAiming;

    private void Awake()
    {
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (Keyboard.current.digit1Key.wasPressedThisFrame)
            animator.SetTrigger("FirePistol");

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            animator.SetTrigger("ReloadPistol");

        if (Keyboard.current.digit3Key.wasPressedThisFrame)
            animator.SetTrigger("FireSMG");

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            animator.SetTrigger("ReloadSMG");

        if (Keyboard.current.digit5Key.wasPressedThisFrame)
            animator.SetTrigger("FireRPG");

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
            animator.SetTrigger("ReloadRPG");

        if (Keyboard.current.qKey.wasPressedThisFrame)
            animator.SetTrigger("TakeDamage");

        if (Keyboard.current.eKey.wasPressedThisFrame)
            animator.SetTrigger("Defeat");

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isAiming = !isAiming;
            animator.SetBool("IsAiming", isAiming);
        }
    }
}