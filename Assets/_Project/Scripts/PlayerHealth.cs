using System;
using UnityEngine;

public sealed class PlayerHealth : MonoBehaviour
{
    private int maxHealth;
    [SerializeField, Min(0f)] private float damageCooldown = 0.75f;
    private float nextDamageTime;
    private int appliedUpgrade;
    private bool isCesar;
    public int CurrentHealth { get; private set; }
    public int MaxHealth => maxHealth;
    public bool IsDead { get; private set; }
    public bool IsCesar => isCesar;
    public bool CanHeal => !IsDead && CurrentHealth < maxHealth && !GameFlowController.IsMatchEnded;
    public event Action<int, int> HealthChanged;
    public event Action Damaged;
    public event Action Died;

    private void Awake()
    {
        isCesar = CharacterWeapons.IsCesar(gameObject);
        appliedUpgrade = ProgressionService.HealthUpgradeFor(isCesar);
        Initialize(CharacterStats.BaseHealth(isCesar) + appliedUpgrade, damageCooldown);
    }
    private void OnEnable() { ProgressionService.Changed += ApplyUpgrade; ApplyUpgrade(); }
    private void OnDisable() { ProgressionService.Changed -= ApplyUpgrade; }
    private void ApplyUpgrade()
    {
        int difference = ProgressionService.HealthUpgradeFor(isCesar) - appliedUpgrade;
        if (difference == 0) return;
        appliedUpgrade = ProgressionService.HealthUpgradeFor(isCesar);
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

    public bool Heal(int amount)
    {
        if (amount <= 0 || !CanHeal) return false;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        HealthChanged?.Invoke(CurrentHealth, maxHealth);
        return true;
    }
}
