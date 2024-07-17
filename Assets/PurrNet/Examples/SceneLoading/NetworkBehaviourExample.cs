using PurrNet;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned()
    {
        ServerRPCMethod();
        ServerRPCMethod2();
    }

    [ServerRPC]
    private void ServerRPCMethod()
    {
        // This method will be called on the server
        Debug.Log("ServerRPCMethod called on server"); 
    }
    
    [ServerRPC]
    private void ServerRPCMethod2()
    {
        // This method will be called on the server
        Debug.Log("ServerRPCMethod called on server 2"); 
    }
}
