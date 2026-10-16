using System;
using System.Collections;
using UnityEngine;

public sealed class PlayerWeaponController : MonoBehaviour
{
    [SerializeField] private WeaponDefinition definition;
    private float nextShotTime;
    private Coroutine reload;
    private PlayerWeaponAudio weaponAudio;
    public bool IsCesar => CharacterWeapons.IsCesar(gameObject);
    public string AnimationWeapon => CharacterWeapons.State(EquippedWeapon, IsCesar);
    public WeaponDefinition Definition => !IsCesar && EquippedWeapon == 0 && definition != null
        ? definition : WeaponDefinition.ForCharacter(EquippedWeapon, IsCesar);
    public int Damage => Definition.DamageAt(ProgressionService.WeaponUpgrade(EquippedWeapon, IsCesar));
    public int Magazine { get; private set; }
    public bool IsReloading => reload != null;
    public int EquippedWeapon { get; private set; }
    public bool CanFire => isActiveAndEnabled && !IsReloading && Magazine > 0 &&
        Time.time >= nextShotTime && !GameFlowController.IsMatchEnded && !GameFlowController.IsPaused;
    private readonly int[] magazines = new int[3];
    public event Action Changed;

    private void Awake()
    {
        weaponAudio = GetComponent<PlayerWeaponAudio>();
        if (weaponAudio == null) weaponAudio = gameObject.AddComponent<PlayerWeaponAudio>();
        for (int i = 0; i < 3; i++) magazines[i] = Mathf.Min(WeaponDefinition.ForCharacter(i, IsCesar).MagazineSize, ProgressionService.Ammo(i, IsCesar));
        Magazine = magazines[0];
    }
    public bool TryEquip(int index)
    {
        if (GameFlowController.IsPaused || GameFlowController.IsMatchEnded || !ProgressionService.OwnsWeapon(index, IsCesar)) return false;
        if (index == EquippedWeapon) return true;
        CancelReload(); magazines[EquippedWeapon] = Magazine;
        EquippedWeapon = index; Magazine = Mathf.Min(magazines[index], ProgressionService.Ammo(index, IsCesar));
        Changed?.Invoke(); return true;
    }

    public bool TryFire(Vector2 origin, Vector2 direction, Transform owner)
    {
        if (!CanFire) return false;
        if (!CombatProjectile.TrySpawn(origin, direction, Damage, Definition.ProjectileSpeed,
            Definition.ProjectileRadius, Definition.Range, owner, EquippedWeapon,
            Definition.TargetsAt(ProgressionService.WeaponUpgrade(EquippedWeapon, IsCesar)),
            IsCesar && EquippedWeapon == 2, Camera.main)) return false;
        Magazine--;
        nextShotTime = Time.time + Definition.FireInterval;
        weaponAudio.PlayShot(EquippedWeapon);
        Changed?.Invoke();
        return true;
    }

    public bool TryReload()
    {
        if (!isActiveAndEnabled || IsReloading || Magazine >= Mathf.Min(Definition.MagazineSize, ProgressionService.Ammo(EquippedWeapon, IsCesar)) ||
            GameFlowController.IsMatchEnded || GameFlowController.IsPaused) return false;
        reload = StartCoroutine(Reload());
        weaponAudio.PlayReload(EquippedWeapon, Definition.ReloadDuration);
        Changed?.Invoke();
        return true;
    }

    private IEnumerator Reload()
    {
        yield return new WaitForSeconds(Definition.ReloadDuration);
        Magazine = Mathf.Min(Definition.MagazineSize, ProgressionService.Ammo(EquippedWeapon, IsCesar));
        weaponAudio.StopReload();
        reload = null;
        Changed?.Invoke();
    }

    public void CancelReload()
    {
        if (reload == null) return;
        StopCoroutine(reload);
        weaponAudio.StopReload();
        reload = null;
        Changed?.Invoke();
    }
    private void OnDisable()
    {
        CancelReload();
        if (weaponAudio != null) weaponAudio.StopAll();
    }
}
