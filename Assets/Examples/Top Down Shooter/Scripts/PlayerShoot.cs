using PurrNet;
using UnityEngine;

public class PlayerShoot : NetworkIdentity
{
    [SerializeField] private Bullet bulletPrefab;

    protected override void OnSpawned(bool asServer)
    {
        enabled = isOwner;
    }
    
    private void Update()
    {
        if (!Input.GetMouseButtonDown(0))
            return;

        var trs = transform;
        var bullet = Instantiate(bulletPrefab, trs.position + trs.forward * 0.5f + Vector3.up * 0.7f, trs.rotation);
        
        bullet.GiveOwnership(owner!.Value);
    }
}
