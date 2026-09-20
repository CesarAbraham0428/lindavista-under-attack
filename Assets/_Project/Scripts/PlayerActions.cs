using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

public class PlayerActions : MonoBehaviour
{
    private Animator animator;
    private PlayerMovement movement;
    private bool isAiming;
    private bool defeated;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        movement = GetComponent<PlayerMovement>();
    }

    private void Update()
    {
        if (Keyboard.current == null || animator == null || defeated)
            return;

        // Las armas de fuego se repiten mientras se mantenga presionada la tecla.
        // No reiniciamos la animación mientras todavía está reproduciéndose.
        HoldFire(Keyboard.current.digit1Key, "FirePistol");
        HoldFire(Keyboard.current.digit3Key, "FireSMG");
        HoldFire(Keyboard.current.digit5Key, "FireRPG");

        if (Keyboard.current.digit2Key.wasPressedThisFrame)
            TriggerIfNotPlaying("ReloadPistol");

        if (Keyboard.current.digit4Key.wasPressedThisFrame)
            TriggerIfNotPlaying("ReloadSMG");

        if (Keyboard.current.digit6Key.wasPressedThisFrame)
            TriggerIfNotPlaying("ReloadRPG");

        if (Keyboard.current.qKey.wasPressedThisFrame)
            animator.SetTrigger("TakeDamage");

        if (Keyboard.current.eKey.wasPressedThisFrame)
        {
            defeated = true;
            movement?.StopForDefeat();
            animator.SetTrigger("Defeat");
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            isAiming = !isAiming;
            animator.SetBool("IsAiming", isAiming);
        }
    }

    private void HoldFire(KeyControl key, string triggerName)
    {
        if (key.isPressed && !IsBusy())
            animator.SetTrigger(triggerName);
    }

    private void TriggerIfNotPlaying(string triggerName)
    {
        if (!IsBusy())
            animator.SetTrigger(triggerName);
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
