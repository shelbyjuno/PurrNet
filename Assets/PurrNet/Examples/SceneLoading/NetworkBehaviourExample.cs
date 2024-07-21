using PurrNet;
using UnityEngine;

public class NetworkBehaviourExample : NetworkIdentity
{
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            ServerRPCMethod();
            ServerRPCMethod2();
        }
    }

    [ServerRPC]
    private void ServerRPCMethod()
    {
        // This method will be called on the server
        Debug.Log("NOPOOO"); 
    }
    
    [ServerRPC]
    private void ServerRPCMethod2()
    {
        // This method will be called on the server
        Debug.Log("NOPOOO 2"); 
    }
}
