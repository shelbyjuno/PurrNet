using PurrNet;
using UnityEngine;

public class PlayerHealth : NetworkIdentity
{
    [SerializeField] private int maxHealth = 100;
    
    public void ChangeHealth(int change)
    {
        
    }
}
