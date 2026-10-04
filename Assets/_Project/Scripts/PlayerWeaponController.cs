using System;
using System.Collections;
using UnityEngine;

public sealed class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private WeaponDefinition definition;
    private float nextShotTime;
    private Coroutine reload;
    public WeaponDefinition Definition => definition != null ? definition : WeaponDefinition.Pistol;
    public int Damage => Definition.DamageAt(ProgressionService.PistolUpgrade);
    public int Magazine { get; private set; }
    public bool IsReloading => reload != null;
    public int EquippedWeapon => 0;
    public event Action Changed;

    private void Awake() { Magazine = Definition.MagazineSize; }
    public bool TryEquip(int index) => ProgressionService.OwnsWeapon(index);

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
        if (!isActiveAndEnabled || IsReloading || Magazine == Definition.MagazineSize ||
            GameFlowController.IsMatchEnded) return false;
        reload = StartCoroutine(Reload());
        Changed?.Invoke();
        return true;
    }

    private IEnumerator Reload()
    {
        yield return new WaitForSeconds(Definition.ReloadDuration);
        Magazine = Definition.MagazineSize;
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
