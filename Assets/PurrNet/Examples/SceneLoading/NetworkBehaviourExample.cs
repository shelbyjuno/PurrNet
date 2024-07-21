using PurrNet;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned(bool asServer)
    {
        if (!asServer)
        {
            ServerRPCMethod(42, 69);
            ServerRPCMethod2();
            ServerRPCMethod3();
            
            ServerRPCMethodGeneric(4269);
            ServerRPCMethodGeneric("4269WTF");
        }
    }
    
    [ServerRPC]
    private void ServerRPCMethodGeneric<T>(T a)
    {
        // This method will be called on the server
        Debug.Log("ServerRPCMethodGeneric " + a); 
    }

    [ServerRPC]
    private void ServerRPCMethod(int a, int b)
    {
        // This method will be called on the server
        Debug.Log("NOPOOO " + a + " " + b); 
    }
    
    [ServerRPC, ContextMenu("ServerRPCMethod2")]
    private void ServerRPCMethod2()
    {
        // This method will be called on the server
        Debug.Log("NOPOOO 2"); 
    }
}
