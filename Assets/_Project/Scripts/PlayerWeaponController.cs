using System;
using System.Collections;
using UnityEngine;

public sealed class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private WeaponDefinition definition;
    private float nextShotTime;
    private Coroutine reload;
    public WeaponDefinition Definition => EquippedWeapon == 0 && definition != null ? definition : WeaponDefinition.At(EquippedWeapon);
    public int Damage => Definition.DamageAt(ProgressionService.WeaponUpgrade(EquippedWeapon));
    public int Magazine { get; private set; }
    public bool IsReloading => reload != null;
    public int EquippedWeapon { get; private set; }
    private readonly int[] magazines = new int[3];
    public event Action Changed;

    private void Awake()
    {
        for (int i = 0; i < 3; i++) magazines[i] = Mathf.Min(WeaponDefinition.At(i).MagazineSize, ProgressionService.Ammo(i));
        Magazine = magazines[0];
    }
    public bool TryEquip(int index)
    {
        if (!ProgressionService.OwnsWeapon(index)) return false;
        CancelReload(); magazines[EquippedWeapon] = Magazine;
        EquippedWeapon = index; Magazine = Mathf.Min(magazines[index], ProgressionService.Ammo(index));
        Changed?.Invoke(); return true;
    }

    public bool TryFire(Vector2 origin, Vector2 direction, Transform owner)
    {
        if (!isActiveAndEnabled || IsReloading || Magazine <= 0 || Time.time < nextShotTime ||
            GameFlowController.IsMatchEnded) return false;
        if (!CombatProjectile.TrySpawn(origin, direction, Damage, Definition.ProjectileSpeed,
            Definition.ProjectileRadius, Definition.Range, owner)) return false;
        Magazine--;
        nextShotTime = Time.time + Definition.FireInterval;
        Changed?.Invoke();
        return true;
    }

    public bool TryReload()
    {
        if (!isActiveAndEnabled || IsReloading || Magazine >= Mathf.Min(Definition.MagazineSize, ProgressionService.Ammo(EquippedWeapon)) ||
            GameFlowController.IsMatchEnded) return false;
        reload = StartCoroutine(Reload());
        Changed?.Invoke();
        return true;
    }

    private IEnumerator Reload()
    {
        yield return new WaitForSeconds(Definition.ReloadDuration);
        Magazine = Mathf.Min(Definition.MagazineSize, ProgressionService.Ammo(EquippedWeapon));
        reload = null;
        Changed?.Invoke();
    }

    public void CancelReload()
    {
        if (reload == null) return;
        StopCoroutine(reload);
        reload = null;
        Changed?.Invoke();
    }
    private void OnDisable() { CancelReload(); }
}
