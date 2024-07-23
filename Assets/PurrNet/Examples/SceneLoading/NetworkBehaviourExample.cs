using JetBrains.Annotations;
using PurrNet;
using PurrNet.Utils;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    protected override void OnSpawned(bool asServer)
    {
        Hasher.PrepareType<int>();
        Hasher.PrepareType<uint>();
        Hasher.PrepareType<double>();
        Hasher.PrepareType<string>();
        
        if (!asServer)
        {
            ServerRPCMethodGeneric(5.6969);
        }
    }
    
        
    [ServerRPC]
    private void ServerRPCMethodGeneric<T>(T data, RPCInfo info = default)
    {
        SendToObservers("FOR ALL: " + data);
        SendToTarget(info.sender, data);
    }

    [ObserversRPC]
    private void SendToObservers(string message)
    {
        Debug.Log("All: " + message);
    }
    
    [TargetRPC]
    private void SendToTarget<T>([UsedImplicitly] PlayerID target, T message)
    {
        Debug.Log("Targeted: " + message + " " + typeof(T).Name);
    }
}
