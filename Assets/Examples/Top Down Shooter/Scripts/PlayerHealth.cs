using PurrNet;
using UnityEngine;

public class PlayerHealth : NetworkIdentity
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private TextMesh healthText;
    private int _health;

    protected override void OnSpawned()
    {
        Debug.Log("Spawned", this);
    }

    protected override void OnSpawned(bool asServer)
    {
        if(isServer)
            SetHealth_Observers(maxHealth);
    }

    protected override void OnDespawned()
    {
        Debug.Log("Despawned", this);
    }

    protected override void OnDespawned(bool asServer)
    {
        Debug.Log("Despawned " + asServer, this);
    }

    private void Update()
    {
        Vector3 direction = healthText.transform.position - Camera.main.transform.position;
        direction.x = 0;
        healthText.transform.rotation = Quaternion.LookRotation(direction);
    }

    [ContextMenu("Log optional")]
    private void LogOptional()
    {
        string allOptionalRules = $"Default Owner: {GetEffectiveDefaultOwner()}" +
                                  $"\nPropagate Ownership: {GetEffectivePropagateOwnership()}" +
                                  $"\nDespawn If Owner Disconnects: {GetEffectiveDespawnIfOwnerDisconnects()}" +
                                  $"\nSpawn Auth: {GetEffectiveSpawnAuth()}" +
                                  $"\nDespawn Auth: {GetEffectiveDespawnAuth()}";
        Debug.Log(allOptionalRules);
    }

    public void ChangeHealth(int change)
    {
        if (_health + change <= 0)
        {
            Destroy(gameObject);
            return;
        }
        
        ChangeHealth_Server(change);
    }

    [ServerRPC(requireOwnership: false)]
    private void ChangeHealth_Server(int change)
    {
        SetHealth_Observers(_health + change);
    }
    
    [ObserversRPC(bufferLast:true)]
    private void SetHealth_Observers(int health) 
    {
        _health = health;
        healthText.text = $"{health}hp"; 
    }
}
