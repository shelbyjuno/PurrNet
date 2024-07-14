using PurrNet;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned()
    {
        ServerRPCMethod();
    }

    [ServerRPC]
    private void ServerRPCMethod()
    {
        // This method will be called on the server
        Debug.Log("ServerRPCMethod called on server"); 
    }
}
