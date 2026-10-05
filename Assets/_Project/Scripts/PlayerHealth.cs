using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 5;
    [SerializeField, Min(0f)] private float damageCooldown = 0.75f;
    private float nextDamageTime;
    private int appliedUpgrade;
    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public event Action<int, int> HealthChanged;
    public event Action Damaged;
    public event Action Died;

    private void Awake() { appliedUpgrade = ProgressionService.HealthUpgrade; Initialize(maxHealth + appliedUpgrade, damageCooldown); }
    private void OnEnable() { ProgressionService.Changed += ApplyUpgrade; ApplyUpgrade(); }
    private void OnDisable() { ProgressionService.Changed -= ApplyUpgrade; }
    private void ApplyUpgrade()
    {
        int difference = ProgressionService.HealthUpgrade - appliedUpgrade;
        if (difference == 0) return;
        appliedUpgrade = ProgressionService.HealthUpgrade;
        maxHealth = Mathf.Max(1, maxHealth + difference);
        if (!IsDead) CurrentHealth = Mathf.Clamp(CurrentHealth + difference, 0, maxHealth);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void Initialize(int maximum, float cooldown)
    {
        maxHealth = Mathf.Max(1, maximum);
        damageCooldown = Mathf.Max(0f, cooldown);
        CurrentHealth = maxHealth;
        nextDamageTime = float.NegativeInfinity;
        IsDead = false;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public bool TakeDamage(int amount)
    {
        if (IsDead || amount <= 0 || Time.time < nextDamageTime || GameFlowController.IsMatchEnded) return false;
        CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
        nextDamageTime = Time.time + damageCooldown;
        IsDead = CurrentHealth == 0;
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (IsDead) Died?.Invoke();
        else Damaged?.Invoke();
        return true;
    }
}
