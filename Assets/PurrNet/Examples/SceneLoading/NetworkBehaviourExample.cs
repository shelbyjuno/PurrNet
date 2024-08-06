using JetBrains.Annotations;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

public class NetworkBehaviourExample : NetworkBehaviour
{
    [SerializeField] private NetworkIdentity someRef;

    private void Update()
    {
        if (isSpawned && isServer)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                ObserversRPCTest(Time.time, someRef);
            }
        }
    }


    [ObserversRPC(bufferLast: true)]
    private void ObserversRPCTest<T>(T data, NetworkIdentity someNetRef, RPCInfo info = default)
    {
        Debug.Log("Observers: " + data + " " + info.sender);

        if (someNetRef)
            Debug.Log(someNetRef.name);
        else
            Debug.Log("No ref");
    }
    
    [ServerRPC(Channel.Unreliable)]
    private void ServerRPCMethodGeneric<T>(T data, RPCInfo info = default)
    {
        SendToTarget(info.sender, data);
    }

    
    [TargetRPC(bufferLast: true)]
    private void SendToTarget<T>([UsedImplicitly] PlayerID target, T message)
    {
        Debug.Log("Targeted: " + message + " " + typeof(T).Name);
    }
}
