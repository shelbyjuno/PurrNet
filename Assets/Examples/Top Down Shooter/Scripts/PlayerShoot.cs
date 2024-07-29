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
        
        var bullet = Instantiate(bulletPrefab, transform.position + transform.forward * 0.5f + Vector3.up, transform.rotation);
        bullet.GiveOwnership(owner.Value);
    }
}
