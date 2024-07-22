using PurrNet;
using PurrNet.Utils;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned(bool asServer)
    {
        Hasher.PrepareType<int>();
        Hasher.PrepareType<uint>();
        Hasher.PrepareType<string>();
        
        if (!asServer)
        {
            ServerRPCMethodGeneric("69STR");
        }
    }
    
        
    [ServerRPC]
    private void ServerRPCMethodGeneric(string data)
    {
        Debug.Log("Ping: " + data);
        SendToClient(data);
    }

    [ObserversRPC]
    private void SendToClient(string message)
    {
        Debug.Log("Pong: " + message);
    }
}
