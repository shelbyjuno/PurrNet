using PurrNet;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned()
    {
        ServerRPCMethod();
        ServerRPCMethod2();
    }
    
    private void ServerRPCMethodTest(string value)
    {
        var stream = RPCModule.AllocStream(false);
        
        stream.Serialize(ref value);
        
        var rpcData = RPCModule.BuildRawRPC(id, 0, stream);
        
        RPCModule.FreeStream(stream);
        
        // This method will be called on the server
        Debug.Log("ServerRPCMethod called on sfesfsefeserver"); 
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
